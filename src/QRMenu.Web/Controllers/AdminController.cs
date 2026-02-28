using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QRMenu.Web.ViewModels;
using QRMenu.Data.Data;
using QRMenu.Core.Entities;

namespace QRMenu.Web.Controllers
{
    public class AdminController : Controller
    {
        private readonly QRMenuDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(QRMenuDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Admin ana sayfa → masalara yönlendir
        [HttpGet("/admin")]
        public IActionResult Index() => RedirectToAction("Masalar");

        // Masa yönetimi sayfası
        [HttpGet("/admin/masalar")]
        public async Task<IActionResult> Masalar()
        {
            var masalar = await _context.Masalar
                .OrderBy(m => m.MasaNo)
                .ToListAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var liste = masalar.Select(m => new QrKodViewModel
            {
                MasaNo = m.MasaNo,
                QrUrl = m.QrKodUrl ?? "",
                QrBase64 = ""
            }).ToList();

            ViewBag.BaseUrl = baseUrl;
            return View(liste);
        }

        // AJAX: Masa oluştur
        [HttpGet("/admin/masa-olustur/{masaNo:int}")]
        public async Task<IActionResult> MasaOlustur(int masaNo)
        {
            if (await _context.Masalar.AnyAsync(m => m.MasaNo == masaNo))
                return Json(new { success = false, message = $"Masa {masaNo} zaten var" });

            _context.Masalar.Add(new Masa { MasaNo = masaNo, AktifMi = true });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Masa oluşturuldu. MasaNo={MasaNo}", masaNo);
            return Json(new { success = true });
        }

        // AJAX: Masa sil
        [HttpGet("/admin/masa-sil/{masaNo:int}")]
        public async Task<IActionResult> MasaSil(int masaNo)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (masa == null)
                return Json(new { success = false, message = "Masa bulunamadı" });

            // Bağlı oturum/sipariş varsa silme
            var oturumVar = await _context.Oturumlar.AnyAsync(o => o.MasaId == masa.Id);
            var siparisVar = await _context.Siparisler.AnyAsync(s => s.MasaId == masa.Id);
            if (oturumVar || siparisVar)
            {
                // Tamamen silmek yerine pasife çek
                masa.AktifMi = false;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Masa pasife alındı (bağlı kayıt var). MasaNo={MasaNo}", masaNo);
                return Json(new { success = true, message = "Masa pasife alındı (bağlı kayıtlar var)" });
            }

            _context.Masalar.Remove(masa);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Masa silindi. MasaNo={MasaNo}", masaNo);
            return Json(new { success = true });
        }

        // AJAX: QR oluştur ve DB'ye kaydet
        [HttpGet("/admin/qr-olustur/{masaNo:int}")]
        public async Task<IActionResult> QrOlustur(int masaNo)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (masa == null)
                return Json(new { success = false, message = "Masa bulunamadı" });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var qrUrl = $"{baseUrl}/qr/{masaNo}";

            using var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.H);
            using var qr = new PngByteQRCode(data);
            var bytes = qr.GetGraphic(10);

            masa.QrKodUrl = qrUrl;
            await _context.SaveChangesAsync();

            _logger.LogInformation("QR oluşturuldu. Masa={MasaNo}", masaNo);
            return Json(new { success = true, qrBase64 = Convert.ToBase64String(bytes), qrUrl });
        }

        // AJAX: QR sil
        [HttpGet("/admin/qr-sil/{masaNo:int}")]
        public async Task<IActionResult> QrSil(int masaNo)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (masa == null)
                return Json(new { success = false });

            masa.QrKodUrl = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("QR silindi. Masa={MasaNo}", masaNo);
            return Json(new { success = true });
        }
    }
}
