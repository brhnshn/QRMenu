using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using QRMenu.Web.Hubs;
using QRMenu.Web.Models;
using QRMenu.Web.ViewModels;
using System.ComponentModel.DataAnnotations;
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

        [HttpGet("/Kasa/Masalar")]
        public async Task<IActionResult> Masalar()
        {
            ViewData["ActivePage"] = "KasaMasalar";
            ViewData["PageTitle"] = "Kasa Yönetimi";

            try
            {
                var model = await BuildKasaMasalarViewModelAsync();
                if (IsAjaxRequest())
                {
                    return PartialView("_KasaMasalarContent", model);
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kasa/Masalar veri çekimi başarısız. Geçici bağlantı sorunu olabilir.");
                var fallback = new KasaMasalarPageViewModel
                {
                    ConnectionWarning = "Veritabanı bağlantısı geçici olarak zayıf. Lütfen birkaç saniye sonra sayfayı yenileyin."
                };

                if (IsAjaxRequest())
                {
                    return PartialView("_KasaMasalarContent", fallback);
                }

                return View(fallback);
            }
        }

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
                    .Where(s => s.Durum == SiparisDurum.TamOdendi || s.Durum == SiparisDurum.Iade)
                    .OrderByDescending(s => s.GuncellemeTarihi ?? s.OlusturmaTarihi)
                    .Take(200)
                    .ToListAsync();

                var bugunUtc = DateTime.UtcNow.Date;
                var bugunKayitQuery = _context.Siparisler
                    .AsNoTracking()
                    .Where(s =>
                        (s.Durum == SiparisDurum.TamOdendi || s.Durum == SiparisDurum.Iade) &&
                        (s.GuncellemeTarihi ?? s.OlusturmaTarihi).Date == bugunUtc);

                ViewBag.GunlukCiro = await bugunKayitQuery
                    .Where(s => s.Durum == SiparisDurum.TamOdendi)
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

        [HttpGet("/Kasa/Ayarlar")]
        public IActionResult Ayarlar()
        {
            ViewData["ActivePage"] = "KasaAyarlar";
            ViewData["PageTitle"] = "Kasa Ayarları";

            return View();
        }

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
                    && s.Durum != SiparisDurum.Iptal
                    && s.Durum != SiparisDurum.TamOdendi
                    && s.Durum != SiparisDurum.Iade
                    && s.SiparisDetaylar.Any(sd => sd.Durum == SiparisDurum.TeslimEdildi || sd.Durum == SiparisDurum.KismiOdendi))
                .OrderBy(s => s.OlusturmaTarihi)
                .ToListAsync();

            ViewBag.AktifSiparisler = aktifSiparisler;
            return View(masa);
        }

        [HttpPost("/Kasa/TahsilatYap")]
        public async Task<IActionResult> TahsilatYap([FromBody] TahsilatRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Gecersiz istek." });

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
                    .Where(sd => seciliSiparisIds.Contains(sd.SiparisId) && sd.Durum != SiparisDurum.Iptal && sd.Durum != SiparisDurum.Iade)
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
                            (sd.Durum == SiparisDurum.TeslimEdildi || sd.Durum == SiparisDurum.KismiOdendi) &&
                            sd.Siparis.Durum != SiparisDurum.Iptal &&
                            sd.Siparis.Durum != SiparisDurum.TamOdendi &&
                            sd.Siparis.Durum != SiparisDurum.Iade);

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

                    await Task.WhenAll(
                        _orderHub.Clients.Group(SignalRGroups.Cashier).SendAsync("OdemeYapildi", request.MasaId, masaNo, request.OdemeTipi),
                        _orderHub.Clients.Group(SignalRGroups.Waiter).SendAsync("OdemeYapildi", request.MasaId, masaNo, request.OdemeTipi),
                        _orderHub.Clients.Group(SignalRGroups.Table(request.MasaId)).SendAsync("OdemeYapildi", request.MasaId, masaNo, request.OdemeTipi),
                        _orderHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi"),
                        _orderHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi"),
                        _orderHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi"),
                        _orderHub.Clients.Group(SignalRGroups.Table(request.MasaId)).SendAsync("SiparisGuncellendi")
                    );
                    return Json(new { success = true, tumuOdendi = !kalanOdemeVar });
                }

                return Json(new { success = false, message = "Geçerli ödenecek ürün bulunamadı." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tahsilat işlemi sırasında hata oluştu");
                return StatusCode(500, new { success = false, message = "Islem sirasinda hata olustu." });
            }
        }

        [HttpPost("/Kasa/SeciliKalemleriIptalEt")]
        public async Task<IActionResult> SeciliKalemleriIptalEt([FromBody] SiparisDetayIptalRequest request)
        {
            if (request?.Detaylar == null || request.Detaylar.Count == 0)
                return Json(new { success = false, message = "İptal edilecek kalem seçilmedi." });

            if (!request.MasaId.HasValue)
                return Json(new { success = false, message = "Masa bilgisi bulunamadı." });

            try
            {
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

                var allowed = new[] { SiparisDurum.TeslimEdildi, SiparisDurum.KismiOdendi };
                var invalid = dbDetaylar.Where(sd => !allowed.Contains(sd.Durum)).ToList();
                if (invalid.Any())
                    return Json(new { success = false, message = "Kasa sadece teslim edilmiş veya kısmi ödemeye uygun ürünleri iptal edebilir." });

                var siparisler = await _siparisService.SiparisDetayIptalEtAsync(request.Detaylar, request.MasaId);

                var masaNo = await _context.Masalar
                    .Where(m => m.Id == request.MasaId.Value)
                    .Select(m => m.MasaNo)
                    .FirstOrDefaultAsync();

                if (siparisler.Any(s => s.Durum == SiparisDurum.Iptal))
                {
                    await Task.WhenAll(
                        _orderHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisIptal", masaNo),
                        _orderHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisIptal", masaNo),
                        _orderHub.Clients.Group(SignalRGroups.Table(request.MasaId.Value)).SendAsync("SiparisIptal", masaNo)
                    );
                }

                await Task.WhenAll(
                    _orderHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi"),
                    _orderHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi"),
                    _orderHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi"),
                    _orderHub.Clients.Group(SignalRGroups.Table(request.MasaId.Value)).SendAsync("SiparisGuncellendi")
                );
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasa seçili kalem iptal işlemi sırasında hata oluştu");
                return StatusCode(500, new { success = false, message = "Islem sirasinda hata olustu." });
            }
        }

        private async Task<KasaMasalarPageViewModel> BuildKasaMasalarViewModelAsync()
        {
            var mutfakZamanEsigi = DateTime.UtcNow.AddHours(-24);
            var nowUtc = DateTime.UtcNow;

            var masalar = await _context.Masalar
                .Where(m => m.AktifMi)
                .AsNoTracking()
                .OrderBy(m => m.MasaNo)
                .Select(m => new
                {
                    m.Id,
                    m.MasaNo,
                    BolgeAd = m.Bolge != null && !string.IsNullOrWhiteSpace(m.Bolge.Ad) ? m.Bolge.Ad : "Salon",
                    Siparisler = m.Siparisler
                        .Where(s => s.Durum != SiparisDurum.TamOdendi
                            && s.Durum != SiparisDurum.Iptal
                            && s.Durum != SiparisDurum.Iade)
                        .Select(s => new
                        {
                            s.Durum,
                            s.OlusturmaTarihi,
                            s.ToplamTutar,
                            BazToplam = s.SiparisDetaylar
                                .Where(sd => sd.Durum != SiparisDurum.Iptal && sd.Durum != SiparisDurum.Iade)
                                .Sum(sd => (decimal?)(sd.Adet * sd.BirimFiyat)) ?? 0m,
                            KalanBazToplam = s.SiparisDetaylar
                                .Where(sd => sd.Durum != SiparisDurum.TamOdendi
                                    && sd.Durum != SiparisDurum.Iptal
                                    && sd.Durum != SiparisDurum.Iade)
                                .Sum(sd => (decimal?)(sd.Adet * sd.BirimFiyat)) ?? 0m
                        })
                        .ToList()
                })
                .ToListAsync();

            var masaKartlari = masalar.Select(m =>
            {
                var aktifDetayVarmi = m.Siparisler.Any(s => s.KalanBazToplam > 0);
                var mutfaktaVarMi = m.Siparisler.Any(s =>
                    s.OlusturmaTarihi >= mutfakZamanEsigi &&
                    (s.Durum == SiparisDurum.Onaylandi || s.Durum == SiparisDurum.Hazirlaniyor));
                var garsondaVarMi = m.Siparisler.Any(s => s.Durum == SiparisDurum.Hazir);
                var odemeBekliyorMu = m.Siparisler.Any(s =>
                    s.Durum == SiparisDurum.TeslimEdildi || s.Durum == SiparisDurum.KismiOdendi);

                var kalanBakiye = m.Siparisler.Sum(s =>
                {
                    if (s.BazToplam <= 0 || s.KalanBazToplam <= 0)
                    {
                        return 0m;
                    }

                    var oran = s.ToplamTutar / s.BazToplam;
                    return s.KalanBazToplam * oran;
                });

                var doluMu = aktifDetayVarmi && kalanBakiye > 0;
                var enEskiSiparis = m.Siparisler.Select(s => (DateTime?)s.OlusturmaTarihi).OrderBy(t => t).FirstOrDefault();
                var beklemeDakika = enEskiSiparis.HasValue
                    ? (int)Math.Max(0, (nowUtc - enEskiSiparis.Value).TotalMinutes)
                    : 0;

                var durumMetni = "Hazır";
                if (doluMu)
                {
                    if (odemeBekliyorMu)
                    {
                        durumMetni = "Ödeme Bekliyor";
                    }
                    else if (garsondaVarMi)
                    {
                        durumMetni = "Garsonda";
                    }
                    else if (mutfaktaVarMi)
                    {
                        durumMetni = "Mutfakta";
                    }
                    else
                    {
                        durumMetni = "Sipariş Var";
                    }
                }

                return new KasaTableCardViewModel
                {
                    MasaId = m.Id,
                    MasaNo = m.MasaNo,
                    BolgeAd = m.BolgeAd,
                    DoluMu = doluMu,
                    DurumMetni = durumMetni,
                    KalanBakiye = kalanBakiye,
                    BeklemeDakika = beklemeDakika,
                    AcilMi = doluMu && beklemeDakika >= 40,
                    OdemedeMi = odemeBekliyorMu,
                    MutfaktaMi = mutfaktaVarMi,
                    GarsondaMi = garsondaVarMi
                };
            }).ToList();

            return new KasaMasalarPageViewModel
            {
                MasaKartlari = masaKartlari,
                ToplamOdemeBekleyen = masaKartlari.Count(x => x.DoluMu),
                BolgeChipleri = masaKartlari
                    .GroupBy(x => x.BolgeAd)
                    .Select(g => new KasaBolgeChipViewModel
                    {
                        Bolge = g.Key,
                        Bekleyen = g.Count(x => x.DoluMu)
                    })
                    .OrderByDescending(x => x.Bekleyen)
                    .ThenBy(x => x.Bolge)
                    .ToList()
            };
        }

        private bool IsAjaxRequest() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    public class TahsilatRequest
    {
        [Range(1, int.MaxValue)]
        public int MasaId { get; set; }

        [Required]
        [MinLength(1)]
        public List<int> SiparisDetayIds { get; set; } = new();

        [Required]
        [MaxLength(30)]
        public string OdemeTipi { get; set; } = "Nakit";
    }
}



