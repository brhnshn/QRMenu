using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QRMenu.Core.Interfaces;
using QRMenu.Web.ViewModels;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireAdmin")]
    public class AdminMasaController : Controller
    {
        private readonly IMasaYonetimService _masaService;
        private readonly ILogger<AdminMasaController> _logger;

        public AdminMasaController(IMasaYonetimService masaService, ILogger<AdminMasaController> logger)
        {
            _masaService = masaService;
            _logger = logger;
        }

        [HttpGet("/admin/masalar")]
        public async Task<IActionResult> Masalar()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var bolgeler = await _masaService.GetBolgelerAsync();
            ViewBag.Bolgeler = bolgeler;
            ViewBag.BaseUrl = baseUrl;

            var liste = await _masaService.GetMasaDetayListAsync(baseUrl);
            var viewModels = liste.Select(m => new QrKodViewModel
            {
                Id = m.Id,
                MasaNo = m.MasaNo,
                QrUrl = m.QrUrl,
                QrBase64 = m.QrBase64,
                DoluMu = m.DoluMu,
                BolgeId = m.BolgeId,
                BolgeAd = m.BolgeAd
            }).ToList();

            return View("~/Views/Admin/Masalar.cshtml", viewModels);
        }

        [HttpGet("/admin/masa-olustur/{masaNo:int}")]
        public async Task<IActionResult> MasaOlustur(int masaNo)
        {
            var (success, message) = await _masaService.MasaOlusturAsync(masaNo);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true });
        }

        [HttpPost("/admin/masa-ekle")]
        public async Task<IActionResult> MasaEkle([FromBody] MasaFormViewModel model)
        {
            var (success, message) = await _masaService.MasaEkleAsync(model.MasaNo, model.BolgeId);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true });
        }

        [HttpPost("/admin/masa-guncelle/{id:int}")]
        public async Task<IActionResult> MasaGuncelle(int id, [FromBody] MasaFormViewModel model)
        {
            var (success, message) = await _masaService.MasaGuncelleAsync(id, model.MasaNo, model.BolgeId);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true });
        }

        [HttpPost("/admin/bolge-ekle")]
        public async Task<IActionResult> BolgeEkle([FromBody] BolgeFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Ad))
                return Json(new { success = false, message = "Bölge adı zorunlu." });

            await _masaService.BolgeEkleAsync(model.Ad, model.SiraNo);
            return Json(new { success = true });
        }

        [HttpPost("/admin/bolge-guncelle/{id:int}")]
        public async Task<IActionResult> BolgeGuncelle(int id, [FromBody] BolgeFormViewModel model)
        {
            var (success, message) = await _masaService.BolgeGuncelleAsync(id, model.Ad, model.SiraNo);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true });
        }

        [HttpPost("/admin/bolge-sil/{id:int}")]
        public async Task<IActionResult> BolgeSil(int id)
        {
            var (success, message) = await _masaService.BolgeSilAsync(id);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true });
        }

        [HttpPost("/admin/masa-sil/{masaNo:int}")]
        public async Task<IActionResult> MasaSil(int masaNo, [FromQuery] bool force = false)
        {
            var (success, hasRecords, message, masaId) = await _masaService.MasaSilAsync(masaNo, force);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true, hasRecords, masaId });
        }

        [HttpPost("/admin/qr-olustur/{masaNo:int}")]
        public async Task<IActionResult> QrOlustur(int masaNo, [FromBody] QrOlusturRequest req)
        {
            var baseUrl = string.IsNullOrWhiteSpace(req?.BaseUrl) ? $"{Request.Scheme}://{Request.Host}" : req.BaseUrl.TrimEnd('/');
            var (success, message, qrBase64, qrUrl, qrToken) = await _masaService.QrOlusturAsync(masaNo, baseUrl);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true, qrBase64, qrUrl, qrToken });
        }

        [HttpPost("/admin/qr-yenile/{masaNo:int}")]
        public async Task<IActionResult> QrYenile(int masaNo, [FromBody] QrOlusturRequest? req)
        {
            var baseUrl = string.IsNullOrWhiteSpace(req?.BaseUrl) ? $"{Request.Scheme}://{Request.Host}" : req.BaseUrl.TrimEnd('/');
            var (success, message, qrBase64, qrUrl, qrToken) = await _masaService.QrYenileAsync(masaNo, baseUrl);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true, qrBase64, qrUrl, qrToken });
        }

        [HttpPost("/admin/qr-sil/{masaNo:int}")]
        public async Task<IActionResult> QrSil(int masaNo)
        {
            var success = await _masaService.QrSilAsync(masaNo);
            if (!success)
                return Json(new { success = false });

            return Json(new { success = true });
        }
    }

    public class QrOlusturRequest
    {
        public string? BaseUrl { get; set; }
    }
}
