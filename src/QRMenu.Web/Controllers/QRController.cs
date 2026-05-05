using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Web.Controllers
{
    public class QRController : Controller
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<QRController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly QRMenuDbContext _context;

        public QRController(ITokenService tokenService, ILogger<QRController> logger, IWebHostEnvironment env, QRMenuDbContext context)
        {
            _tokenService = tokenService;
            _logger = logger;
            _env = env;
            _context = context;
        }

        /// <summary>
        /// QR kod ile giriş noktası
        /// URL: /qr/{masaNo}  (sabit, masaya yapışık QR)
        /// Token sunucu tarafında üretilir, cookie'ye yazılır
        /// </summary>
        [HttpGet("/qr/{masaNo:int}")]
        public async Task<IActionResult> Index(int masaNo)
        {
            if (masaNo <= 0)
            {
                _logger.LogWarning("QR giriş: Geçersiz masa numarası. MasaNo={MasaNo}", masaNo);
                return BadRequest("Geçersiz QR kod.");
            }

            // MasaNo'dan gerçek Masa kaydını bul
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo && m.AktifMi);
            if (masa == null)
            {
                _logger.LogWarning("QR giriş: Masa bulunamadı. MasaNo={MasaNo}", masaNo);
                return NotFound("Bu masa bulunamadı.");
            }

            var masaId = masa.Id;

            // Zaten aktif oturumu var mı? (Cookie kontrolü)
            var existingToken = Request.Cookies["qrmenu_token"];
            if (!string.IsNullOrEmpty(existingToken))
            {
                var existingHash = _tokenService.HashToken(existingToken);
                var existingOturum = await _tokenService.ValidateTokenAsync(existingHash);

                if (existingOturum != null && existingOturum.MasaId == masaId)
                {
                    Response.Cookies.Append("masa_no", masaNo.ToString(), new CookieOptions
                    {
                        HttpOnly = false,
                        Secure = false,
                        SameSite = SameSiteMode.Lax,
                        Path = "/",
                        MaxAge = TimeSpan.FromDays(1)
                    });

                    // Aynı masada geçerli oturumu var, direkt menüye
                    _logger.LogInformation("QR giriş: Mevcut oturum kullanılıyor. MasaNo={MasaNo}, MasaId={MasaId}", masaNo, masaId);
                    return RedirectToAction("Index", "Menu");
                }
                // Farklı masa veya geçersiz oturum → eski cookie'yi sil
                Response.Cookies.Delete("qrmenu_token");
            }

            // Yeni oturum oluştur (gerçek MasaId ile)
            var (oturum, rawToken) = await _tokenService.CreateSessionAsync(masaId);

            // Cookie'ye raw token'ı yaz
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Reverse proxy uyumluluğu için
                SameSite = SameSiteMode.Lax,
                Path = "/"
            };
            Response.Cookies.Append("qrmenu_token", rawToken, cookieOptions);
            Response.Cookies.Append("masa_no", masaNo.ToString(), new CookieOptions
            {
                HttpOnly = false,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(1)
            });

            _logger.LogInformation("QR giriş başarılı. MasaNo={MasaNo}, MasaId={MasaId}, OturumId={OturumId}", masaNo, masaId, oturum.Id);

            return RedirectToAction("Index", "Menu");
        }

        /// <summary>
        /// QR yönlendirmesi öncesi masa doğrulama
        /// URL: /qr/validate/{masaNo}
        /// </summary>
        [HttpGet("/qr/validate/{masaNo:int}")]
        public async Task<IActionResult> Validate(int masaNo)
        {
            if (masaNo <= 0)
            {
                return Json(new { success = false, message = "Geçersiz masa numarası." });
            }

            var masa = await _context.Masalar
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MasaNo == masaNo);

            if (masa == null)
            {
                return Json(new { success = false, message = "Böyle bir masa yok." });
            }

            if (!masa.AktifMi)
            {
                return Json(new { success = false, message = "Bu masa şu anda aktif değil." });
            }

            return Json(new { success = true });
        }
    }
}
