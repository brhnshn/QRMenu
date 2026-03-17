using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

using Microsoft.AspNetCore.Authorization;

namespace QRMenu.Web.Controllers
{
    [Authorize(Roles = "Admin, Kasa")]
    public class KasaController : Controller
    {
        private readonly QRMenuDbContext _context;
        private readonly IOdemeService _odemeService;
        private readonly ILogger<KasaController> _logger;

        public KasaController(QRMenuDbContext context, IOdemeService odemeService, ILogger<KasaController> logger)
        {
            _context = context;
            _odemeService = odemeService;
            _logger = logger;
        }

        // GET: /Kasa/Masalar
        [HttpGet("/Kasa/Masalar")]
        public async Task<IActionResult> Masalar()
        {
            ViewData["ActivePage"] = "KasaMasalar";
            ViewData["PageTitle"] = "Kasa Paneli";

            // Tahsilat Bekleyen Masalar (Siparişi olanlar)
            var masalar = await _context.Masalar
                .Include(m => m.Siparisler.Where(s => 
                    s.Durum != QRMenu.Core.Enums.SiparisDurum.TamOdendi && 
                    s.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal &&
                    s.Durum != QRMenu.Core.Enums.SiparisDurum.Iade))
                    .ThenInclude(s => s.SiparisDetaylar)
                .OrderBy(m => m.MasaNo)
                .ToListAsync();

            // Son Ödenen Siparişler (Tab için)
            var sonOdemeler = await _context.Siparisler
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Where(s => s.Durum == QRMenu.Core.Enums.SiparisDurum.TamOdendi || s.Durum == QRMenu.Core.Enums.SiparisDurum.Iade)
                .OrderByDescending(s => s.GuncellemeTarihi ?? s.OlusturmaTarihi)
                .Take(20)
                .ToListAsync();

            ViewBag.SonOdemeler = sonOdemeler;

            return View(masalar);
        }

        // GET: /Kasa/Odeme/{id}
        [HttpGet("/Kasa/Odeme/{id:int}")]
        public async Task<IActionResult> Odeme(int id)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.Id == id);
            if (masa == null)
                return NotFound();

            ViewData["ActivePage"] = "KasaMasalar";
            ViewData["PageTitle"] = $"Masa {masa.MasaNo} Tahsilat Ekranı";

            var aktifSiparisler = await _context.Siparisler
                .Include(s => s.SiparisDetaylar.Where(sd => sd.Durum == QRMenu.Core.Enums.SiparisDurum.TeslimEdildi || sd.Durum == QRMenu.Core.Enums.SiparisDurum.KismiOdendi))
                    .ThenInclude(sd => sd.Urun)
                .Where(s => s.MasaId == id && s.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal && s.Durum != QRMenu.Core.Enums.SiparisDurum.TamOdendi && s.Durum != QRMenu.Core.Enums.SiparisDurum.Iade)
                .OrderBy(s => s.OlusturmaTarihi)
                .ToListAsync();

            // Sadece içinde ürün (Detay) kalan siparişleri filtrele
            aktifSiparisler = aktifSiparisler.Where(s => s.SiparisDetaylar.Any()).ToList();

            ViewBag.AktifSiparisler = aktifSiparisler;
            return View(masa);
        }

        // POST: /Kasa/TahsilatYap
        [HttpPost("/Kasa/TahsilatYap")]
        public async Task<IActionResult> TahsilatYap([FromBody] TahsilatRequest request)
        {
            try
            {
                var success = await _odemeService.ParcaliOdemeAsync(request.MasaId, request.SiparisDetayIds, request.OdemeTipi);
                if (success)
                    return Json(new { success = true });
                
                return Json(new { success = false, message = "Geçerli ödenecek ürün bulunamadı." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tahsilat işlemi sırasında hata oluştu");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    public class TahsilatRequest
    {
        public int MasaId { get; set; }
        public List<int> SiparisDetayIds { get; set; } = new();
        public string OdemeTipi { get; set; } = "Nakit"; // Nakit, Kredi Karti vb.
    }
}
