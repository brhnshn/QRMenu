using Microsoft.Extensions.Caching.Memory;
using QRMenu.Core.Interfaces;

namespace QRMenu.Web.Middleware
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenValidationMiddleware> _logger;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan SessionRefreshInterval = TimeSpan.FromSeconds(60);

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
            "/error",        // Hata sayfalarını maskelememek için
            "/hubs"          // SignalR hub bağlantıları
        };

        public TokenValidationMiddleware(RequestDelegate next, ILogger<TokenValidationMiddleware> logger, IMemoryCache cache)
        {
            _next = next;
            _logger = logger;
            _cache = cache;
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

            // Personel girişi varsa ve QR token yoksa erişime izin ver.
            // Ancak QR token varsa yine de doğrulayıp HttpContext.Items'e yazalım;
            // böylece müşteri akışı (sepet/sipariş) personel cookie'si açıkken de çalışır.
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated && string.IsNullOrEmpty(rawToken))
            {
                await _next(context);
                return;
            }

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
            context.Items["MasaNo"] = oturum.Masa?.MasaNo ?? oturum.MasaId;

            // Sliding expiration: Her istekte yazma yerine 60 sn'de bir güncelle.
            var refreshKey = $"oturum-refresh:{oturum.Id}";
            if (!_cache.TryGetValue(refreshKey, out _))
            {
                await tokenService.RefreshSessionAsync(oturum.Id);
                _cache.Set(refreshKey, true, SessionRefreshInterval);
            }

            await _next(context);
        }

        private static async Task WriteUnauthorized(HttpContext context, string message)
        {
            var isAjax = context.Request.Headers["Content-Type"].ToString().Contains("application/json")
                      || context.Request.Headers["Accept"].ToString().Contains("application/json")
                      || context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (isAjax)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(new { success = false, message }));
            }
            else
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html; charset=utf-8";
                var masaNo = context.Request.Cookies["masa_no"];
                var masaNoValue = !string.IsNullOrWhiteSpace(masaNo) && int.TryParse(masaNo, out _)
                    ? masaNo
                    : "";
                var html = $@"<!DOCTYPE html>
<html lang=""tr"">
<head>
        <meta charset=""utf-8"" />
        <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
        <title>QR Menü - Oturum Gerekli</title>
        <script src=""https://cdn.tailwindcss.com?plugins=forms,container-queries""></script>
        <link href=""https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&amp;family=Be+Vietnam+Pro:wght@300;400;500&amp;display=swap"" rel=""stylesheet"" />
        <link href=""https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:wght,FILL@100..700,0..1&amp;display=swap"" rel=""stylesheet"" />
        <script>
            tailwind.config = {{
                darkMode: ""class"",
                theme: {{
                    extend: {{
                        colors: {{
                            ""on-error-container"": ""#570008"",
                            ""on-error"": ""#ffefee"",
                            ""surface-variant"": ""#dedcdc"",
                            ""on-secondary-fixed"": ""#453100"",
                            ""on-primary-fixed-variant"": ""#581200"",
                            ""on-background"": ""#2e2f2f"",
                            ""primary-fixed"": ""#fd7954"",
                            ""surface-container-highest"": ""#dedcdc"",
                            ""error-dim"": ""#9f0519"",
                            ""error"": ""#b31b25"",
                            ""surface-dim"": ""#d5d4d4"",
                            ""surface-container-lowest"": ""#ffffff"",
                            ""surface"": ""#f8f6f5"",
                            ""primary-container"": ""#fd7954"",
                            ""outline-variant"": ""#aeadac"",
                            ""tertiary-container"": ""#ffffff"",
                            ""tertiary-dim"": ""#504f4f"",
                            ""on-primary"": ""#ffefec"",
                            ""tertiary"": ""#5c5b5b"",
                            ""on-tertiary-fixed"": ""#505050"",
                            ""inverse-surface"": ""#0e0e0e"",
                            ""secondary-fixed-dim"": ""#f6ba2d"",
                            ""on-secondary"": ""#fff1dc"",
                            ""on-surface"": ""#2e2f2f"",
                            ""on-tertiary"": ""#f5f2f1"",
                            ""inverse-primary"": ""#fd7954"",
                            ""inverse-on-surface"": ""#9e9d9c"",
                            ""on-surface-variant"": ""#5c5b5b"",
                            ""surface-tint"": ""#a23718"",
                            ""on-tertiary-container"": ""#636262"",
                            ""primary-fixed-dim"": ""#ec6d49"",
                            ""primary"": ""#a23718"",
                            ""on-secondary-fixed-variant"": ""#694c00"",
                            ""tertiary-fixed-dim"": ""#f3f0f0"",
                            ""on-tertiary-fixed-variant"": ""#6e6d6d"",
                            ""on-primary-fixed"": ""#000000"",
                            ""tertiary-fixed"": ""#ffffff"",
                            ""surface-bright"": ""#f8f6f5"",
                            ""on-primary-container"": ""#480d00"",
                            ""on-secondary-container"": ""#5d4300"",
                            ""secondary"": ""#765600"",
                            ""surface-container"": ""#eae8e7"",
                            ""surface-container-low"": ""#f2f0f0"",
                            ""surface-container-high"": ""#e4e2e2"",
                            ""outline"": ""#777776"",
                            ""secondary-fixed"": ""#ffca57"",
                            ""background"": ""#f8f6f5"",
                            ""secondary-container"": ""#ffca57"",
                            ""error-container"": ""#fb5151"",
                            ""primary-dim"": ""#922c0c"",
                            ""secondary-dim"": ""#674b00""
                        }},
                        fontFamily: {{
                            ""headline"": [""Plus Jakarta Sans""],
                            ""body"": [""Be Vietnam Pro""],
                            ""label"": [""Plus Jakarta Sans""]
                        }},
                        borderRadius: {{ ""DEFAULT"": ""0.25rem"", ""lg"": ""0.5rem"", ""xl"": ""1.5rem"", ""full"": ""9999px"" }}
                    }}
                }}
            }}
        </script>
        <style>
            .material-symbols-outlined {{
                font-variation-settings: 'FILL' 0, 'wght' 300, 'GRAD' 0, 'opsz' 24;
            }}
            .text-editorial {{
                letter-spacing: -0.02em;
            }}
            .paper-lift {{
                box-shadow: 0 12px 32px rgba(46, 47, 47, 0.04);
            }}
            body {{
                min-height: max(884px, 100dvh);
            }}
            .toast {{
                position: fixed;
                left: 50%;
                bottom: 20px;
                transform: translateX(-50%) translateY(16px);
                background: #2e2f2f;
                color: #fff;
                padding: 10px 14px;
                border-radius: 999px;
                font-family: 'Be Vietnam Pro', sans-serif;
                font-size: 13px;
                opacity: 0;
                pointer-events: none;
                transition: opacity .2s ease, transform .2s ease;
                z-index: 200;
                box-shadow: 0 10px 24px rgba(0,0,0,.18);
            }}
            .toast.show {{
                opacity: 1;
                transform: translateX(-50%) translateY(0);
            }}
        </style>
</head>
<body class=""bg-surface font-body text-on-surface antialiased min-h-screen flex flex-col"">
    <header class=""fixed top-0 w-full z-50 bg-surface/80 backdrop-blur-xl flex items-center justify-between px-6 h-16"">
        <div class=""flex items-center gap-2"">
            <span class=""material-symbols-outlined text-primary"">lock_clock</span>
        </div>
        <h1 class=""text-xl font-headline font-bold tracking-widest uppercase text-[#2e2f2f]"">QR Menü</h1>
        <div class=""w-6""></div>
    </header>

    <main class=""flex-grow flex items-center justify-center px-6 pt-16 pb-12"">
        <div class=""max-w-md w-full text-center space-y-10"">
            <div class=""relative mx-auto w-48 h-48 flex items-center justify-center"">
                <div class=""absolute inset-0 bg-surface-container rounded-full scale-110 opacity-50""></div>
                <div class=""relative bg-surface-container-lowest p-8 rounded-xl paper-lift flex flex-col items-center justify-center"">
                    <span class=""material-symbols-outlined text-6xl text-primary mb-2"">qr_code_2</span>
                    <div class=""absolute -bottom-2 -right-2 bg-primary text-on-primary p-3 rounded-full shadow-lg"">
                        <span class=""material-symbols-outlined text-2xl"">refresh</span>
                    </div>
                </div>
            </div>

            <div class=""space-y-4"">
                <h2 class=""text-3xl font-headline font-extrabold text-on-surface text-editorial leading-tight"">
                    {System.Net.WebUtility.HtmlEncode(message)}
                </h2>
                <p class=""text-on-surface-variant font-body leading-relaxed max-w-[300px] mx-auto"">
                    Güvenliğiniz ve güncel menüye erişiminiz için lütfen masanızdaki QR kodunu tekrar okutunuz.
                </p>
                <div class=""pt-2 flex flex-col items-center gap-3"">
                    <input id=""masaNoInput"" type=""number"" min=""1"" inputmode=""numeric"" value=""{masaNoValue}"" placeholder=""Masa numarası"" class=""w-44 text-center px-4 py-2 rounded-xl border border-outline-variant bg-white text-on-surface font-headline font-bold"" />
                    <button type=""button"" onclick=""goQr()"" class=""inline-flex items-center justify-center px-5 py-2.5 rounded-full bg-primary text-on-primary font-headline font-bold tracking-wide"">
                        Masaya Geç
                    </button>
                </div>
            </div>


        </div>
    </main>

    <footer class=""w-full py-8 px-6 text-center opacity-40"">
        <div class=""flex flex-col items-center gap-2"">
            <div class=""w-12 h-[1px] bg-on-surface/20""></div>
            <p class=""text-[10px] font-label tracking-[0.2em] uppercase text-on-surface"">QR Menü</p>
        </div>
    </footer>
    <div id=""pageToast"" class=""toast""></div>
    <script>
        function showToast(message) {{
            var toast = document.getElementById('pageToast');
            if (!toast) return;
            toast.textContent = message;
            toast.classList.add('show');
            clearTimeout(window.__toastTimer);
            window.__toastTimer = setTimeout(function() {{
                toast.classList.remove('show');
            }}, 2200);
        }}

        async function goQr() {{
            var el = document.getElementById('masaNoInput');
            var val = el ? parseInt(el.value, 10) : NaN;
            if (!Number.isFinite(val) || val <= 0) {{
                showToast('Lütfen geçerli bir masa numarası girin.');
                return;
            }}

            try {{
                var response = await fetch('/qr/validate/' + val, {{
                    method: 'GET',
                    headers: {{ 'Accept': 'application/json' }}
                }});

                if (!response.ok) {{
                    showToast('Masa doğrulanırken bir hata oluştu.');
                    return;
                }}

                var data = await response.json();
                if (!data || data.success !== true) {{
                    showToast((data && data.message) ? data.message : 'Masa doğrulanamadı.');
                    return;
                }}

                window.location.href = '/qr/' + val;
            }} catch (e) {{
                showToast('Bağlantı hatası. Lütfen tekrar deneyin.');
            }}
        }}
    </script>
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



