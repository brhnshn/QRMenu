using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Serilog;
using QRMenu.Data.Data;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Services;
using QRMenu.Web.Middleware;

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
builder.Services.AddDbContext<QRMenuDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// DI — Uygulama Servisleri
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUrunService, UrunService>();

// MVC
builder.Services.AddControllersWithViews();

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Rate Limiting
app.UseRateLimiter();

// Token doğrulama middleware (müşteri istekleri için)
app.UseTokenValidation();

app.UseAuthorization();

// ===== ROUTING =====
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Menu}/{action=Index}/{id?}");

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

Log.Information("QR Menü uygulaması başlatılıyor...");
app.Run();
