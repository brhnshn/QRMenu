using QRMenu.Core.Interfaces;

namespace QRMenu.Web.Middleware
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenValidationMiddleware> _logger;

        private static readonly string[] ExcludedPaths = new[]
        {
            "/qr",           // QR kod ile giriş
            "/auth",         // Personel giriş yolları (login, logout)
            "/login",        // Uyumluluk
            "/css",          // Statik dosyalar
            "/js",
            "/lib",
            "/images",
            "/favicon.ico",
            "/health",
            "/hubs"          // SignalR hub bağlantıları
        };

        public TokenValidationMiddleware(RequestDelegate next, ILogger<TokenValidationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Excluded path'ler için doğrulama yapma
            if (ExcludedPaths.Any(p => path.StartsWith(p)))
            {
                await _next(context);
                return;
            }

            // Personel paneli yolları için ayrı auth (ileride Identity ile)
            if (path.StartsWith("/admin") || path.StartsWith("/garson") ||
                path.StartsWith("/mutfak") || path.StartsWith("/kasa"))
            {
                await _next(context);
                return;
            }

            // Personel girişi yapmış kullanıcı ise her yere (menü dahil) serbestçe girebilir
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                await _next(context);
                return;
            }

            // Müşteri tarafı — cookie'den token oku
            var rawToken = context.Request.Cookies["qrmenu_token"];
            if (string.IsNullOrEmpty(rawToken))
            {
                _logger.LogWarning("Token cookie bulunamadı. Path: {Path}", path);
                await WriteUnauthorized(context, "Geçersiz oturum. Lütfen QR kodu tekrar okutun.");
                return;
            }

            // Token'ı hash'le ve doğrula
            var tokenHash = tokenService.HashToken(rawToken);
            var oturum = await tokenService.ValidateTokenAsync(tokenHash);

            if (oturum == null)
            {
                _logger.LogWarning("Geçersiz veya süresi dolmuş token. Path: {Path}", path);
                await WriteUnauthorized(context, "Oturum süresi dolmuş. Lütfen QR kodu tekrar okutun.");
                return;
            }

            // Oturum bilgisini HttpContext'e ekle (controller'lar kullanacak)
            context.Items["Oturum"] = oturum;
            context.Items["OturumId"] = oturum.Id;
            context.Items["MasaId"] = oturum.MasaId;

            // Sliding expiration: Son işlem zamanını güncelle
            await tokenService.RefreshSessionAsync(oturum.Id);

            await _next(context);
        }

        private static async Task WriteUnauthorized(HttpContext context, string message)
        {
            context.Response.StatusCode = 401;
            var isAjax = context.Request.Headers["Content-Type"].ToString().Contains("application/json")
                      || context.Request.Headers["Accept"].ToString().Contains("application/json")
                      || context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (isAjax)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(new { success = false, message }));
            }
            else
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                var html = $@"<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width,initial-scale=1"">
    <title>QR Menü — Oturum Gerekli</title>
    <style>
        *{{margin:0;padding:0;box-sizing:border-box}}
        body{{font-family:'Segoe UI',sans-serif;background:linear-gradient(135deg,#1a1a2e,#16213e);color:#fff;min-height:100vh;display:flex;align-items:center;justify-content:center}}
        .card{{background:rgba(255,255,255,.07);backdrop-filter:blur(12px);border-radius:20px;padding:48px 36px;text-align:center;max-width:420px;width:90%;box-shadow:0 8px 32px rgba(0,0,0,.3)}}
        .icon{{font-size:4rem;margin-bottom:16px}}
        h1{{font-size:1.4rem;margin-bottom:12px;color:#f9e2af}}
        p{{font-size:.95rem;color:#cdd6f4;line-height:1.6;margin-bottom:24px}}
        .badge{{display:inline-block;background:#e67e22;color:#fff;padding:8px 24px;border-radius:8px;font-weight:600;font-size:.9rem;text-decoration:none}}
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""icon"">📱</div>
        <h1>{System.Net.WebUtility.HtmlEncode(message)}</h1>
        <p>Sipariş verebilmek için masanızdaki QR kodu telefonunuzla okutun.</p>
        <span class=""badge"">QR Kodu Okutun</span>
    </div>
</body>
</html>";
                await context.Response.WriteAsync(html);
            }
        }
    }

    // Extension method for clean middleware registration
    public static class TokenValidationMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenValidationMiddleware>();
        }
    }
}
