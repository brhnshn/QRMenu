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
        ConnectionPruningInterval = 10
    };

    // Bağlantı dizesinde açıkça SslMode belirtilmemişse: SslMode.Prefer (SSL destekleniyorsa SSL, Docker/yerelde SSL yoksa güvenle düz bağlantı)
    if (!rawConnectionString.Contains("SslMode", StringComparison.OrdinalIgnoreCase) &&
        !rawConnectionString.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase))
    {
        csb.SslMode = SslMode.Prefer;
    }

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
builder.Services.AddScoped<IUrunYonetimService, UrunYonetimService>();
builder.Services.AddScoped<IMasaYonetimService, MasaYonetimService>();
builder.Services.AddScoped<IKullaniciYonetimService, KullaniciYonetimService>();
builder.Services.AddScoped<IHappyHourYonetimService, HappyHourYonetimService>();
builder.Services.AddScoped<IRaporYonetimService, RaporYonetimService>();
builder.Services.AddScoped<QRMenu.Web.Helpers.IRazorViewRenderer, QRMenu.Web.Helpers.RazorViewRenderer>();

// MVC
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
#if DEBUG
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}
#endif

// ASP.NET Identity
builder.Services.AddIdentity<Kullanici, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
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
        var token = context.Request.Cookies["qrmenu_token"] ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
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
        var token = context.Request.Cookies["qrmenu_token"] ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
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
        var token = context.Request.Cookies["qrmenu_token"] ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter($"siparis_{token}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("AuthIpRateLimitPolicy", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
        return RateLimitPartition.GetFixedWindowLimiter($"auth_{ip}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("QrRateLimitPolicy", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
        return RateLimitPartition.GetFixedWindowLimiter($"qr_{ip}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

// Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// CLI: --create-admin <kullanici> <parola>
if (args.Length >= 3 && args[0] == "--create-admin")
{
    var adminUser = args[1];
    var adminPass = args[2];
    using (var scope = app.Services.CreateScope())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Kullanici>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        var user = await userManager.FindByNameAsync(adminUser);
        if (user == null)
        {
            user = new Kullanici
            {
                UserName = adminUser,
                AdSoyad = "Sistem Yöneticisi",
                Email = $"{adminUser}@qrmenu.local",
                EmailConfirmed = true,
                AktifMi = true,
                Rol = QRMenu.Core.Enums.KullaniciRol.Admin
            };
            var result = await userManager.CreateAsync(user, adminPass);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Admin");
                Console.WriteLine($"[Admin CLI] Admin kullanıcısı '{adminUser}' başarıyla oluşturuldu.");
            }
            else
            {
                Console.WriteLine($"[Admin CLI] Hata: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, adminPass);
            if (result.Succeeded)
            {
                user.Rol = QRMenu.Core.Enums.KullaniciRol.Admin;
                user.AktifMi = true;
                await userManager.UpdateAsync(user);
                if (!await userManager.IsInRoleAsync(user, "Admin"))
                {
                    await userManager.AddToRoleAsync(user, "Admin");
                }
                Console.WriteLine($"[Admin CLI] Mevcut '{adminUser}' kullanıcısının parolası güncellendi ve Admin rolü doğrulandı.");
            }
            else
            {
                Console.WriteLine($"[Admin CLI] Hata: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
    return;
}

// ===== MIDDLEWARE PIPELINE =====
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

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
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

    // TODO: cdn.tailwindcss.com (Play CDN) prodüksiyon için uygun değildir.
    // İleride derlenmiş statik CSS oluşturma pipeline'ı eklenmelidir.
    var csp = "default-src 'self'; " +
              "script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://static.cloudflareinsights.com; " +
              "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; " +
              "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
              "img-src 'self' data: https: blob:; " +
              "media-src 'self' https: blob:; " +
              "connect-src 'self' wss: https://translate.googleapis.com; " +
              "frame-ancestors 'self';";

    context.Response.Headers.Append("Content-Security-Policy", csp);
    await next();
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

// Health Check endpoint
app.MapHealthChecks("/health");

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
        if (db.Database.IsRelational())
        {
            db.Database.Migrate();
            Log.Information("Veritabani migration basarili.");
        }
        else
        {
            db.Database.EnsureCreated();
        }

        string[] roller = ["Admin", "Garson", "Kasa", "Mutfak", "Barista"];
        foreach (var rol in roller)
        {
            if (!await roleManager.RoleExistsAsync(rol))
            {
                await roleManager.CreateAsync(new IdentityRole(rol));
            }
        }

        // İlk kurulumda admin kullanıcısı yoksa otomatik oluştur
        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser == null)
        {
            var adminPass = Environment.GetEnvironmentVariable("ADMIN_DEFAULT_PASSWORD") ?? "Admin123*";
            adminUser = new Kullanici
            {
                UserName = "admin",
                AdSoyad = "Sistem Yöneticisi",
                Email = "admin@qrmenu.local",
                EmailConfirmed = true,
                AktifMi = true,
                Rol = QRMenu.Core.Enums.KullaniciRol.Admin
            };
            var createRes = await userManager.CreateAsync(adminUser, adminPass);
            if (createRes.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Log.Information("İlk kurulum: Varsayılan Admin kullanıcısı oluşturuldu (Kullanıcı: admin).");
            }
        }

        // Masalarda boş QrToken varsa üret
        var masalar = await db.Masalar.Where(m => string.IsNullOrEmpty(m.QrToken)).ToListAsync();
        if (masalar.Any())
        {
            foreach (var masa in masalar)
            {
                masa.QrToken = QRMenu.Core.Helpers.QrTokenGenerator.GenerateToken();
            }
            await db.SaveChangesAsync();
            Log.Information("{Count} masaya QR token atandı.", masalar.Count);
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

public partial class Program { }
