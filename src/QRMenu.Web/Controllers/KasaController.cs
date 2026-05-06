using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using QRMenu.Web.Hubs;

using Microsoft.AspNetCore.Authorization;
using QRMenu.Web.Models;
using System.Security.Claims;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireCashier")]
    public class KasaController : Controller
    {
        private readonly QRMenuDbContext _context;
        private readonly IOdemeService _odemeService;
        private readonly ISiparisService _siparisService;
        private readonly IHubContext<OrderHub> _orderHub;
        private readonly ILogger<KasaController> _logger;

        public KasaController(QRMenuDbContext context, IOdemeService odemeService, ISiparisService siparisService, IHubContext<OrderHub> orderHub, ILogger<KasaController> logger)
        {
            _context = context;
            _odemeService = odemeService;
            _siparisService = siparisService;
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
                    .AsNoTracking()
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
                    .AsNoTracking()
                    .Include(s => s.Masa)
                        .ThenInclude(m => m.Bolge)
                    .Include(s => s.Odemeler)
                    .AsSplitQuery()
                    .Where(s => s.Durum == QRMenu.Core.Enums.SiparisDurum.TamOdendi || s.Durum == QRMenu.Core.Enums.SiparisDurum.Iade)
                    .OrderByDescending(s => s.GuncellemeTarihi ?? s.OlusturmaTarihi)
                    .Take(200)
                    .ToListAsync();

                var bugunUtc = DateTime.UtcNow.Date;
                var bugunKayitQuery = _context.Siparisler
                    .AsNoTracking()
                    .Where(s =>
                        (s.Durum == QRMenu.Core.Enums.SiparisDurum.TamOdendi || s.Durum == QRMenu.Core.Enums.SiparisDurum.Iade) &&
                        (s.GuncellemeTarihi ?? s.OlusturmaTarihi).Date == bugunUtc);

                ViewBag.GunlukCiro = await bugunKayitQuery
                    .Where(s => s.Durum == QRMenu.Core.Enums.SiparisDurum.TamOdendi)
                    .SumAsync(s => (decimal?)s.ToplamTutar) ?? 0m;

                ViewBag.IslemSayisi = await bugunKayitQuery.CountAsync();
                ViewBag.OrtalamaMasaTutari = await bugunKayitQuery
                    .AverageAsync(s => (decimal?)s.ToplamTutar) ?? 0m;

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
            var masa = await _context.Masalar
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (masa == null)
                return NotFound();

            ViewData["ActivePage"] = "KasaMasalar";
            ViewData["PageTitle"] = $"Masa {masa.MasaNo} Tahsilat Ekranı";

            var aktifSiparisler = await _context.Siparisler
                .AsNoTracking()
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .Where(s => s.MasaId == id
                    && s.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal
                    && s.Durum != QRMenu.Core.Enums.SiparisDurum.TamOdendi
                    && s.Durum != QRMenu.Core.Enums.SiparisDurum.Iade
                    && s.SiparisDetaylar.Any(sd => sd.Durum == QRMenu.Core.Enums.SiparisDurum.TeslimEdildi || sd.Durum == QRMenu.Core.Enums.SiparisDurum.KismiOdendi))
                .OrderBy(s => s.OlusturmaTarihi)
                .ToListAsync();

            ViewBag.AktifSiparisler = aktifSiparisler;
            return View(masa);
        }

        // POST: /Kasa/TahsilatYap
        [HttpPost("/Kasa/TahsilatYap")]
        public async Task<IActionResult> TahsilatYap([FromBody] TahsilatRequest request)
        {
            try
            {
                var secilenDetaylar = await _context.SiparisDetaylar
                    .AsNoTracking()
                    .Where(sd => request.SiparisDetayIds.Contains(sd.Id) && sd.Siparis.MasaId == request.MasaId)
                    .Select(sd => new { sd.Id, sd.SiparisId, sd.BirimFiyat, sd.Adet, SiparisToplam = sd.Siparis.ToplamTutar })
                    .ToListAsync();

                var seciliSiparisIds = secilenDetaylar.Select(x => x.SiparisId).Distinct().ToList();
                var siparisBazToplamlar = await _context.SiparisDetaylar
                    .AsNoTracking()
                    .Where(sd => seciliSiparisIds.Contains(sd.SiparisId) && sd.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal && sd.Durum != QRMenu.Core.Enums.SiparisDurum.Iade)
                    .GroupBy(sd => sd.SiparisId)
                    .Select(g => new { SiparisId = g.Key, BazToplam = g.Sum(x => x.BirimFiyat * x.Adet) })
                    .ToDictionaryAsync(x => x.SiparisId, x => x.BazToplam);

                var siparisToplamMap = secilenDetaylar
                    .GroupBy(x => x.SiparisId)
                    .ToDictionary(g => g.Key, g => g.First().SiparisToplam);

                decimal hesaplananTutar = 0;
                foreach (var detay in secilenDetaylar)
                {
                    var bazToplam = siparisBazToplamlar.TryGetValue(detay.SiparisId, out var bt) ? bt : 0m;
                    var siparisToplam = siparisToplamMap.TryGetValue(detay.SiparisId, out var st) ? st : 0m;
                    var oran = bazToplam > 0 ? (siparisToplam / bazToplam) : 1m;
                    hesaplananTutar += (detay.BirimFiyat * detay.Adet) * oran;
                }

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

                    _context.SecurityLogs.Add(new SecurityLog
                    {
                        EventType = "KasaTahsilat",
                        Message = $"Kasa tahsilat yaptı: Masa {masaNo}, Tutar={hesaplananTutar:N2}, Tip={request.OdemeTipi}, KalemSayısı={secilenDetaylar.Count}, Siparişler=[{string.Join(',', secilenDetaylar.Select(x => x.SiparisId).Distinct())}]",
                        Path = "/Kasa/TahsilatYap",
                        Method = "POST",
                        UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User?.Identity?.Name,
                        Severity = "Info",
                        TableId = masaNo > 0 ? masaNo.ToString() : null,
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

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

        // POST: /Kasa/SeciliKalemleriIptalEt
        [HttpPost("/Kasa/SeciliKalemleriIptalEt")]
        public async Task<IActionResult> SeciliKalemleriIptalEt([FromBody] SiparisDetayIptalRequest request)
        {
            if (request?.Detaylar == null || request.Detaylar.Count == 0)
                return Json(new { success = false, message = "İptal edilecek kalem seçilmedi." });

            if (!request.MasaId.HasValue)
                return Json(new { success = false, message = "Masa bilgisi bulunamadı." });

            try
            {
                // Kasa sadece verilen masa için, ödeme aşamasında olan (TeslimEdildi/KismiOdendi) kalemleri iptal edebilmeli
                var ids = request.Detaylar.Select(d => d.SiparisDetayId).ToList();
                var dbDetaylar = await _context.SiparisDetaylar
                    .Include(sd => sd.Siparis)
                        .ThenInclude(s => s.Masa)
                    .AsSplitQuery()
                    .Where(sd => ids.Contains(sd.Id))
                    .ToListAsync();

                if (dbDetaylar.Count != ids.Count)
                    return Json(new { success = false, message = "Bazı seçilen kalemler bulunamadı." });

                if (dbDetaylar.Any(sd => sd.Siparis.MasaId != request.MasaId.Value))
                    return Json(new { success = false, message = "Seçilen kalemler farklı masaya ait." });

                var allowed = new[] { QRMenu.Core.Enums.SiparisDurum.TeslimEdildi, QRMenu.Core.Enums.SiparisDurum.KismiOdendi };
                var invalid = dbDetaylar.Where(sd => !allowed.Contains(sd.Durum)).ToList();
                if (invalid.Any())
                    return Json(new { success = false, message = "Kasa sadece teslim edilmiş veya kısmi ödemeye uygun ürünleri iptal edebilir." });

                var siparisler = await _siparisService.SiparisDetayIptalEtAsync(request.Detaylar, request.MasaId);

                var masaNo = await _context.Masalar
                    .Where(m => m.Id == request.MasaId.Value)
                    .Select(m => m.MasaNo)
                    .FirstOrDefaultAsync();

                if (siparisler.Any(s => s.Durum == QRMenu.Core.Enums.SiparisDurum.Iptal))
                {
                    await _orderHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisIptal", masaNo);
                    await _orderHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisIptal", masaNo);
                    await _orderHub.Clients.Group(SignalRGroups.Table(request.MasaId.Value)).SendAsync("SiparisIptal", masaNo);
                }

                await _orderHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi");
                await _orderHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi");
                await _orderHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi");
                await _orderHub.Clients.Group(SignalRGroups.Table(request.MasaId.Value)).SendAsync("SiparisGuncellendi");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasa seçili kalem iptal işlemi sırasında hata oluştu");
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

