using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using Serilog;
using QRMenu.Data.Data;
using QRMenu.Core.Interfaces;
using QRMenu.Core.Entities;
using QRMenu.Data.Services;
using QRMenu.Web.Middleware;
using QRMenu.Web.Hubs;
using QRMenu.Web.Services;
using QRMenu.Data.Interceptors;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;

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

// DbContext — PostgreSQL (Supabase) bağlantısı
builder.Services.AddDbContext<QRMenuDbContext>((sp, options) =>
{
    var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection tanımlı değil.");

    var configuredMaxPoolSize = builder.Configuration.GetValue<int?>("Database:MaxPoolSize");

    var csb = new NpgsqlConnectionStringBuilder(rawConnectionString)
    {
        // İnternet dalgalanmalarında daha hızlı toparlanma için bağlantı/read tarafını sınırla.
        Timeout = 15,
        CommandTimeout = 30,
        KeepAlive = 30,
        TcpKeepAlive = true,
        Pooling = true,
        MaxPoolSize = configuredMaxPoolSize
            ?? (rawConnectionString.Contains("pooler.supabase.com", StringComparison.OrdinalIgnoreCase) ? 10 : 100),
        MinPoolSize = 0,
        ConnectionIdleLifetime = 60,
        ConnectionPruningInterval = 10,
        SslMode = SslMode.Require
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
    
    // AuditLogInterceptor'ı DI üzerinden alıp ekle
    var interceptor = sp.GetRequiredService<AuditLogInterceptor>();
    options.AddInterceptors(interceptor);
});

// Memory Cache — Sepet okumalarını hızlandırmak için (Supabase Stockholm latency çözümü)
builder.Services.AddMemoryCache();

// DI — Uygulama Servisleri
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
var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

// ASP.NET Identity
builder.Services.AddIdentity<Kullanici, IdentityRole>(options =>
{
    // Parola gereksinimleri
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    // Hesap kilitleme
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    // Kullanıcı adı uniq, email isteğe bağlı
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<QRMenuDbContext>()
.AddDefaultTokenProviders();

// Identity cookie ayarları
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

    options.Events = new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            // Eğer AJAX isteği değilse ve auth hatasıysa, middleware'in yakalaması için asıl statüyü Items'a at
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

// Güvenlik damgası (SecurityStamp) doğrulamasını her istekte yap
// Böylece rol değişikliği veya hesabın pasife alınması anında yansır (kullanıcı çıkışa zorlanır)
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

// SignalR — Gerçek zamanlı menü güncellemeleri
builder.Services.AddSignalR();

// Background Service — 1 günden eski oturumları sil
builder.Services.AddHostedService<OturumTemizleyici>();

// ===== RATE LIMITING =====
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // Token bazlı rate limiting (müşteri endpoint'leri)
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

    // Garson çağır butonu için daha sıkı limit
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
    // Siparis Limiti
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

    // Gizli Kurtarma (Sistem Yöneticisi Oluşturma) ekranı için brute-force (kaba kuvvet) engelleme
    // 15 dakikada en fazla 5 deneme yapılabilir (IP bazlı)
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

// Güvenlik Başlıkları (Security Headers)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    
    var csp = "default-src 'self'; " +
              "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.tailwindcss.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://static.cloudflareinsights.com; " +
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

// Güvenlik Olayları Loglayıcı
app.UseMiddleware<SecurityLogMiddleware>();

app.UseRouting();

// Rate Limiting
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Token doğrulama middleware (müşteri istekleri için) - Auth'dan SONRA çalışmalı ki personeli tanıyabilsin
app.UseTokenValidation();

// ===== ROUTING =====
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Menu}/{action=Index}/{id?}");

// SignalR Hub endpoint
app.MapHub<OrderHub>("/hubs/order");
// Geriye dönük uyumluluk: eski istemciler bir süre daha /hubs/menu üzerinden bağlanabilir.
app.MapHub<OrderHub>("/hubs/menu");

// ===== VERİTABANI MIGRATION + IDENTITY SEED =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QRMenuDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Kullanici>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    try
    {
        db.Database.Migrate();
        Log.Information("Veritabanı migration başarılı.");

        // ===== ROL SEED =====
        string[] roller = ["Admin", "Garson", "Kasa", "Mutfak", "Barista"];
        foreach (var rol in roller)
        {
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));
        }

    }
    catch (Exception ex)
    {
        Log.Error(ex, "Veritabanı migration veya Seed hatası! Hatalı migration uygulamanın çökmesine sebep oluyor.");
    }
}

try
{
    Log.Information("QR Menü uygulaması başlatılıyor...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Uygulama başlatılırken kritik bir hata oluştu!");
}
finally
{
    Log.CloseAndFlush();
}
