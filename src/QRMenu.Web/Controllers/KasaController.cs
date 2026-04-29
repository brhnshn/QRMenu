using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using QRMenu.Web.Hubs;

using Microsoft.AspNetCore.Authorization;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireCashier")]
    public class KasaController : Controller
    {
        private readonly QRMenuDbContext _context;
        private readonly IOdemeService _odemeService;
        private readonly IHubContext<OrderHub> _orderHub;
        private readonly ILogger<KasaController> _logger;

        public KasaController(QRMenuDbContext context, IOdemeService odemeService, IHubContext<OrderHub> orderHub, ILogger<KasaController> logger)
        {
            _context = context;
            _odemeService = odemeService;
            _orderHub = orderHub;
            _logger = logger;
        }

        // GET: /Kasa/Masalar
        [HttpGet("/Kasa/Masalar")]
        public async Task<IActionResult> Masalar()
        {
            ViewData["ActivePage"] = "KasaMasalar";
            ViewData["PageTitle"] = "Kasa Yönetimi";

            try
            {
                // Tahsilat Bekleyen Masalar (Siparişi olanlar)
                var masalar = await _context.Masalar
                    .Where(m => m.AktifMi)
                    .Include(m => m.Bolge)
                    .Include(m => m.Siparisler.Where(s => 
                        s.Durum != QRMenu.Core.Enums.SiparisDurum.TamOdendi && 
                        s.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal &&
                        s.Durum != QRMenu.Core.Enums.SiparisDurum.Iade &&
                        s.SiparisDetaylar.Any(sd =>
                            sd.Durum != QRMenu.Core.Enums.SiparisDurum.TamOdendi &&
                            sd.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal &&
                            sd.Durum != QRMenu.Core.Enums.SiparisDurum.Iade)))
                        .ThenInclude(s => s.SiparisDetaylar)
                    .AsSplitQuery()
                    .OrderBy(m => m.MasaNo)
                    .ToListAsync();

                return View(masalar);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kasa/Masalar veri çekimi başarısız. Geçici bağlantı sorunu olabilir.");
                ViewBag.ConnectionWarning = "Veritabanı bağlantısı geçici olarak zayıf. Lütfen birkaç saniye sonra sayfayı yenileyin.";
                return View(new List<Masa>());
            }
        }

        // GET: /Kasa/OdenenSiparisler
        [HttpGet("/Kasa/OdenenSiparisler")]
        public async Task<IActionResult> OdenenSiparisler()
        {
            ViewData["ActivePage"] = "KasaOdenen";
            ViewData["PageTitle"] = "Ödenen Siparişler";

            try
            {
                var siparisler = await _context.Siparisler
                    .Include(s => s.Masa)
                        .ThenInclude(m => m.Bolge)
                    .Include(s => s.Odemeler)
                    .Include(s => s.SiparisDetaylar)
                        .ThenInclude(sd => sd.Urun)
                    .AsSplitQuery()
                    .Where(s => s.Durum == QRMenu.Core.Enums.SiparisDurum.TamOdendi || s.Durum == QRMenu.Core.Enums.SiparisDurum.Iade)
                    .OrderByDescending(s => s.GuncellemeTarihi ?? s.OlusturmaTarihi)
                    .Take(200)
                    .ToListAsync();

                var bugunUtc = DateTime.UtcNow.Date;
                var bugunKayitlari = siparisler
                    .Where(s => (s.GuncellemeTarihi ?? s.OlusturmaTarihi).Date == bugunUtc)
                    .ToList();

                ViewBag.GunlukCiro = bugunKayitlari
                    .Where(s => s.Durum == QRMenu.Core.Enums.SiparisDurum.TamOdendi)
                    .Sum(s => s.ToplamTutar);
                ViewBag.IslemSayisi = bugunKayitlari.Count;
                ViewBag.OrtalamaMasaTutari = bugunKayitlari.Count > 0
                    ? bugunKayitlari.Average(s => s.ToplamTutar)
                    : 0m;

                return View(siparisler);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kasa/OdenenSiparisler veri çekimi başarısız. Geçici bağlantı sorunu olabilir.");
                ViewBag.GunlukCiro = 0m;
                ViewBag.IslemSayisi = 0;
                ViewBag.OrtalamaMasaTutari = 0m;
                ViewBag.ConnectionWarning = "Veritabanı bağlantısı geçici olarak zayıf. Lütfen birkaç saniye sonra sayfayı yenileyin.";
                return View(new List<Siparis>());
            }
        }

        // GET: /Kasa/Ayarlar
        [HttpGet("/Kasa/Ayarlar")]
        public IActionResult Ayarlar()
        {
            ViewData["ActivePage"] = "KasaAyarlar";
            ViewData["PageTitle"] = "Kasa Ayarları";

            return View();
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
                .AsSplitQuery()
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
                {
                    var kalanOdemeVar = await _context.SiparisDetaylar
                        .AsNoTracking()
                        .AnyAsync(sd =>
                            sd.Siparis.MasaId == request.MasaId &&
                            (sd.Durum == QRMenu.Core.Enums.SiparisDurum.TeslimEdildi || sd.Durum == QRMenu.Core.Enums.SiparisDurum.KismiOdendi) &&
                            sd.Siparis.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal &&
                            sd.Siparis.Durum != QRMenu.Core.Enums.SiparisDurum.TamOdendi &&
                            sd.Siparis.Durum != QRMenu.Core.Enums.SiparisDurum.Iade);

                    var masaNo = await _context.Masalar
                        .Where(m => m.Id == request.MasaId)
                        .Select(m => m.MasaNo)
                        .FirstOrDefaultAsync();

                    await _orderHub.Clients.Group(SignalRGroups.Cashier).SendAsync("OdemeYapildi", request.MasaId, masaNo, request.OdemeTipi);
                    await _orderHub.Clients.Group(SignalRGroups.Waiter).SendAsync("OdemeYapildi", request.MasaId, masaNo, request.OdemeTipi);
                    await _orderHub.Clients.Group(SignalRGroups.Table(request.MasaId)).SendAsync("OdemeYapildi", request.MasaId, masaNo, request.OdemeTipi);

                    await _orderHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi");
                    await _orderHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi");
                    await _orderHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi");
                    await _orderHub.Clients.Group(SignalRGroups.Table(request.MasaId)).SendAsync("SiparisGuncellendi");

                    return Json(new { success = true, tumuOdendi = !kalanOdemeVar });
                }
                
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
