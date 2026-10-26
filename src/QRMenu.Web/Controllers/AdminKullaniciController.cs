using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Web.ViewModels;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireAdmin")]
    public class AdminKullaniciController : Controller
    {
        private readonly IKullaniciYonetimService _kullaniciService;
        private readonly ILogger<AdminKullaniciController> _logger;

        public AdminKullaniciController(IKullaniciYonetimService kullaniciService, ILogger<AdminKullaniciController> logger)
        {
            _kullaniciService = kullaniciService;
            _logger = logger;
        }

        [HttpGet("/admin/kullanicilar")]
        public IActionResult Kullanicilar()
        {
            ViewData["ActivePage"] = "Kullanicilar";
            ViewData["PageTitle"] = "Kullanıcı Yönetimi";
            return View("~/Views/Admin/Kullanicilar.cshtml");
        }

        [HttpGet("/admin/kullanici-listesi")]
        public async Task<IActionResult> KullaniciListesi()
        {
            var kullanicilar = await _kullaniciService.GetKullaniciListesiAsync();
            return Json(new { success = true, data = kullanicilar });
        }

        [HttpPost("/admin/kullanici-ekle")]
        public async Task<IActionResult> KullaniciEkle([FromBody] KullaniciFormViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Geçersiz veri." });

            if (!Enum.TryParse<KullaniciRol>(model.Rol, out var rol))
                return Json(new { success = false, message = "Geçersiz rol." });

            var (success, errorMessage, kullaniciId) = await _kullaniciService.KullaniciEkleAsync(
                model.KullaniciAdi, model.AdSoyad, model.Sifre, rol);

            if (!success)
                return Json(new { success = false, message = errorMessage });

            return Json(new { success = true, id = kullaniciId });
        }

        [HttpPost("/admin/kullanici-guncelle/{id}")]
        public async Task<IActionResult> KullaniciGuncelle(string id, [FromBody] KullaniciGuncelleViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Geçersiz veri." });

            if (!Enum.TryParse<KullaniciRol>(model.Rol, out var rol))
                return Json(new { success = false, message = "Geçersiz rol." });

            var (success, errorMessage) = await _kullaniciService.KullaniciGuncelleAsync(
                id, model.KullaniciAdi, model.AdSoyad, rol, model.AktifMi);

            if (!success)
                return Json(new { success = false, message = errorMessage });

            return Json(new { success = true });
        }

        [HttpPost("/admin/kullanici-sifre/{id}")]
        public async Task<IActionResult> KullaniciSifreDegistir(string id, [FromBody] SifreDegistirViewModel model)
        {
            var (success, errorMessage) = await _kullaniciService.KullaniciSifreDegistirAsync(id, model.YeniSifre);
            if (!success)
                return Json(new { success = false, message = errorMessage });

            return Json(new { success = true });
        }

        [HttpPost("/admin/kullanici-sil/{id}")]
        public async Task<IActionResult> KullaniciSil(string id)
        {
            var currentUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, errorMessage) = await _kullaniciService.KullaniciSilAsync(id, currentUserId);
            if (!success)
                return Json(new { success = false, message = errorMessage });

            return Json(new { success = true });
        }

        [HttpPost("/admin/kullanici-toggle/{id}")]
        public async Task<IActionResult> KullaniciToggle(string id)
        {
            var (success, errorMessage, aktifMi) = await _kullaniciService.KullaniciToggleAsync(id);
            if (!success)
                return Json(new { success = false, message = errorMessage });

            return Json(new { success = true, aktifMi });
        }
    }
}
