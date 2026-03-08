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
            "/login",        // Personel giriş
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
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(new { success = false, message }));
            }
            else
            {
                await context.Response.WriteAsync(message);
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
