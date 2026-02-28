using Microsoft.AspNetCore.Mvc;
using QRMenu.Core.Interfaces;

namespace QRMenu.Web.Controllers
{
    public class QRController : Controller
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<QRController> _logger;
        private readonly IWebHostEnvironment _env;

        public QRController(ITokenService tokenService, ILogger<QRController> logger, IWebHostEnvironment env)
        {
            _tokenService = tokenService;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// QR kod ile giriş noktası
        /// URL: /qr/{masaId}  (sabit, masaya yapışık QR)
        /// Token sunucu tarafında üretilir, cookie'ye yazılır
        /// </summary>
        [HttpGet("/qr/{masaId:int}")]
        public async Task<IActionResult> Index(int masaId)
        {
            if (masaId <= 0)
            {
                _logger.LogWarning("QR giriş: Geçersiz masa numarası. masaId={MasaId}", masaId);
                return BadRequest("Geçersiz QR kod.");
            }

            // Zaten aktif oturumu var mı? (Cookie kontrolü)
            var existingToken = Request.Cookies["qrmenu_token"];
            if (!string.IsNullOrEmpty(existingToken))
            {
                var existingHash = _tokenService.HashToken(existingToken);
                var existingOturum = await _tokenService.ValidateTokenAsync(existingHash);

                if (existingOturum != null && existingOturum.MasaId == masaId)
                {
                    // Aynı masada geçerli oturumu var, direkt menüye
                    _logger.LogInformation("QR giriş: Mevcut oturum kullanılıyor. MasaId={MasaId}", masaId);
                    return RedirectToAction("Index", "Menu");
                }
                // Farklı masa veya geçersiz oturum → eski cookie'yi sil
                Response.Cookies.Delete("qrmenu_token");
            }

            // Yeni oturum oluştur
            var (oturum, rawToken) = await _tokenService.CreateSessionAsync(masaId);

            // Cookie'ye raw token'ı yaz
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = !_env.IsDevelopment(), // Development'ta HTTP, Production'da HTTPS
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddMinutes(90),
                Path = "/"
            };
            Response.Cookies.Append("qrmenu_token", rawToken, cookieOptions);

            _logger.LogInformation("QR giriş başarılı. MasaId={MasaId}, OturumId={OturumId}", masaId, oturum.Id);

            return RedirectToAction("Index", "Menu");
        }
    }
}
