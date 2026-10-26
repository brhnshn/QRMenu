using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Web.Controllers
{
    [EnableRateLimiting("QrRateLimitPolicy")]
    public class QRController : Controller
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<QRController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly QRMenuDbContext _context;
        private readonly IConfiguration _config;

        public QRController(
            ITokenService tokenService,
            ILogger<QRController> logger,
            IWebHostEnvironment env,
            QRMenuDbContext context,
            IConfiguration config)
        {
            _tokenService = tokenService;
            _logger = logger;
            _env = env;
            _context = context;
            _config = config;
        }

        /// <summary>
        /// P4.3: Güvenli Token'lı QR giriş noktası
        /// URL: /qr/t/{token}
        /// </summary>
        [HttpGet("/qr/t/{token}")]
        public async Task<IActionResult> TokenGiris(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("QR giriş: Boş token ile giriş denendi.");
                return BadRequest("Geçersiz QR kod.");
            }

            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.QrToken == token && m.AktifMi);
            if (masa == null)
            {
                _logger.LogWarning("QR giriş: Token ile masa bulunamadı. Token={Token}", token);
                return NotFound("Bu masa bulunamadı veya pasif.");
            }

            return await OturumBaslatVeyaKullanAsync(masa);
        }

        /// <summary>
        /// Eski QR kod ile giriş noktası (Legacy)
        /// Development ortamında veya Security:LegacyQrEnabled açıkken çalışır.
        /// Prod ortamında 404 döner.
        /// </summary>
        [HttpGet("/qr/{masaNo:int}")]
        public async Task<IActionResult> Index(int masaNo)
        {
            var legacyEnabled = _env.IsDevelopment() || _config.GetValue<bool>("Security:LegacyQrEnabled", false);
            if (!legacyEnabled)
            {
                _logger.LogWarning("QR giriş: Legacy QR endpoint prod ortamında çağrıldı ve reddedildi. MasaNo={MasaNo}", masaNo);
                return NotFound();
            }

            if (masaNo <= 0)
            {
                _logger.LogWarning("QR giriş: Geçersiz masa numarası. MasaNo={MasaNo}", masaNo);
                return BadRequest("Geçersiz QR kod.");
            }

            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo && m.AktifMi);
            if (masa == null)
            {
                _logger.LogWarning("QR giriş: Masa bulunamadı. MasaNo={MasaNo}", masaNo);
                return NotFound("Bu masa bulunamadı.");
            }

            return await OturumBaslatVeyaKullanAsync(masa);
        }

        private async Task<IActionResult> OturumBaslatVeyaKullanAsync(Masa masa)
        {
            var isSecure = !_env.IsDevelopment() || Request.IsHttps;
            var masaId = masa.Id;
            var masaNo = masa.MasaNo;

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
                        HttpOnly = true,
                        Secure = isSecure,
                        SameSite = SameSiteMode.Lax,
                        Path = "/",
                        MaxAge = TimeSpan.FromDays(1)
                    });

                    _logger.LogInformation("QR giriş: Mevcut oturum kullanılıyor. MasaNo={MasaNo}, MasaId={MasaId}", masaNo, masaId);
                    return RedirectToAction("Index", "Menu");
                }

                // Farklı masa veya geçersiz oturum → eski cookie'yi sil
                Response.Cookies.Delete("qrmenu_token");
            }

            // Yeni oturum oluştur
            var (oturum, rawToken) = await _tokenService.CreateSessionAsync(masaId);

            Response.Cookies.Append("qrmenu_token", rawToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });

            Response.Cookies.Append("masa_no", masaNo.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(1)
            });

            _logger.LogInformation("QR giriş başarılı. MasaNo={MasaNo}, MasaId={MasaId}, OturumId={OturumId}", masaNo, masaId, oturum.Id);
            return RedirectToAction("Index", "Menu");
        }

        /// <summary>
        /// P4.3: QR yönlendirmesi öncesi masa doğrulama (Legacy - yalnızca Development/LegacyEnabled modunda).
        /// Asla gizli QrToken bilgisini istemciye ifşa etmez.
        /// URL: /qr/validate/{masaNo}
        /// </summary>
        [HttpGet("/qr/validate/{masaNo:int}")]
        public async Task<IActionResult> Validate(int masaNo)
        {
            var legacyEnabled = _env.IsDevelopment() || _config.GetValue<bool>("Security:LegacyQrEnabled", false);
            if (!legacyEnabled)
            {
                return NotFound();
            }

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

        /// <summary>
        /// P4.3: Token ile masa doğrulama (güvenli)
        /// URL: /qr/validate-token/{token}
        /// </summary>
        [HttpGet("/qr/validate-token/{token}")]
        public async Task<IActionResult> ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Json(new { success = false, message = "Geçersiz token." });
            }

            var masa = await _context.Masalar
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.QrToken == token && m.AktifMi);

            if (masa == null)
            {
                return Json(new { success = false, message = "Bu QR koduna ait aktif masa bulunamadı." });
            }

            return Json(new { success = true, masaNo = masa.MasaNo });
        }
    }
}
