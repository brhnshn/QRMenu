using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;
using Serilog;
using QRMenu.Data.Data;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Services;
using QRMenu.Web.Middleware;
using QRMenu.Web.Hubs;
using QRMenu.Web.Services;
using QRMenu.Data.Interceptors;
using Microsoft.AspNetCore.HttpOverrides;

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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    
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

// MVC
builder.Services.AddControllersWithViews();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.Cookie.Name = "QRMenuAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
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
});

var app = builder.Build();

// ===== MIDDLEWARE PIPELINE =====
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseStaticFiles();
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
app.MapHub<MenuHub>("/hubs/menu");

// ===== VERİTABANI MIGRATION (Development) =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QRMenuDbContext>();
    try
    {
        db.Database.Migrate();
        Log.Information("Veritabanı migration başarılı.");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Veritabanı migration hatası!");
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
