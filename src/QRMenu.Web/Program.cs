using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using QRMenu.Data.Interceptors;
using QRMenu.Data.Services;
using QRMenu.Web.Hubs;
using QRMenu.Web.Middleware;
using QRMenu.Web.Services;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ===== SERILOG YAPILANDIRMASI =====
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ===== SERVICES =====

// DbContext - PostgreSQL baglantisi
builder.Services.AddDbContext<QRMenuDbContext>((sp, options) =>
{
    var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' tanimli degil.");

    var configuredMaxPoolSize = builder.Configuration.GetValue<int?>("Database:MaxPoolSize");
    var environment = sp.GetRequiredService<IWebHostEnvironment>();

    var csb = new NpgsqlConnectionStringBuilder(rawConnectionString)
    {
        Timeout = 15,
        CommandTimeout = 30,
        KeepAlive = 30,
        TcpKeepAlive = true,
        Pooling = true,
        MaxPoolSize = configuredMaxPoolSize ?? 100,
        MinPoolSize = 0,
        ConnectionIdleLifetime = 60,
        ConnectionPruningInterval = 10,
        SslMode = environment.IsDevelopment() ? SslMode.Prefer : SslMode.Disable
    };

    options.UseNpgsql(csb.ConnectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 6,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(30);
        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });

    var interceptor = sp.GetRequiredService<AuditLogInterceptor>();
    options.AddInterceptors(interceptor);
});

// Memory Cache - Sepet okumalarini hizlandirmak icin
builder.Services.AddMemoryCache();

// DI - Uygulama Servisleri
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();
builder.Services.AddScoped<AuditLogInterceptor>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUrunService, UrunService>();
builder.Services.AddScoped<ISepetService, SepetService>();
builder.Services.AddScoped<ISiparisService, SiparisService>();
builder.Services.AddScoped<IOdemeService, OdemeService>();
builder.Services.AddScoped<QRMenu.Web.Helpers.IRazorViewRenderer, QRMenu.Web.Helpers.RazorViewRenderer>();

// MVC
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

// ASP.NET Identity
builder.Services.AddIdentity<Kullanici, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<QRMenuDbContext>()
.AddDefaultTokenProviders();

// Identity cookie ayarlari
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/Login";
    options.Cookie.Name = "QRMenuAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (!context.Request.Path.StartsWithSegments("/Auth/Login"))
            {
                context.HttpContext.Items["SecurityStatusCode"] = 401;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            context.HttpContext.Items["SecurityStatusCode"] = 403;
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }
    };
});

// SecurityStamp dogrulamasini her istekte yap
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// Authorization Policies (RBAC)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireKitchen", policy => policy.RequireRole("Admin", "Mutfak", "Barista"));
    options.AddPolicy("RequireCashier", policy => policy.RequireRole("Admin", "Kasa"));
    options.AddPolicy("RequireWaiter", policy => policy.RequireRole("Admin", "Garson"));
    options.AddPolicy("RequireStaff", policy => policy.RequireRole("Admin", "Garson", "Kasa", "Mutfak", "Barista"));
});

// SignalR - Gercek zamanli menu guncellemeleri
builder.Services.AddSignalR();

// Background Service - 1 gunden eski oturumlari sil
builder.Services.AddHostedService<OturumTemizleyici>();

// ===== RATE LIMITING =====
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("TokenBasedPolicy", context =>
    {
        var token = context.Request.Cookies["qrmenu_token"] ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(token, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.AddPolicy("GarsonCagirPolicy", context =>
    {
        var token = context.Request.Cookies["qrmenu_token"] ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter($"garson_{token}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.AddPolicy("SiparisLimiti", context =>
    {
        var token = context.Request.Cookies["qrmenu_token"] ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter($"siparis_{token}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("RecoveryLimitPolicy", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
        return RateLimitPartition.GetFixedWindowLimiter($"recovery_{ip}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

// ===== MIDDLEWARE PIPELINE =====
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
    app.UseHsts();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
}

// Guvenlik basliklari
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

    var csp = "default-src 'self'; " +
              "script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://static.cloudflareinsights.com; " +
              "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; " +
              "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
              "img-src 'self' data: https: blob:; " +
              "media-src 'self' https: blob:; " +
              "connect-src 'self' wss: https:;";

    context.Response.Headers.Append("Content-Security-Policy", csp);
    await next();
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseStaticFiles();

// Guvenlik olaylari loglayici
app.UseMiddleware<SecurityLogMiddleware>();

app.UseRouting();

// Rate Limiting
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Token dogrulama middleware (musteri istekleri icin)
app.UseTokenValidation();

// ===== ROUTING =====
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Menu}/{action=Index}/{id?}");

// SignalR Hub endpoint
app.MapHub<OrderHub>("/hubs/order");
app.MapHub<OrderHub>("/hubs/menu");

// ===== VERITABANI MIGRATION + IDENTITY SEED =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QRMenuDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Kullanici>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    try
    {
        db.Database.Migrate();
        Log.Information("Veritabani migration basarili.");

        string[] roller = ["Admin", "Garson", "Kasa", "Mutfak", "Barista"];
        foreach (var rol in roller)
        {
            if (!await roleManager.RoleExistsAsync(rol))
            {
                await roleManager.CreateAsync(new IdentityRole(rol));
            }
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Veritabani migration veya seed hatasi.");
    }
}

try
{
    Log.Information("QR Menu uygulamasi baslatiliyor...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Uygulama baslatilirken kritik bir hata olustu.");
}
finally
{
    Log.CloseAndFlush();
}
