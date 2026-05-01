using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using QRMenu.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;

using Microsoft.AspNetCore.Authorization;
using QRMenu.Web.Models;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireWaiter")]
    public class GarsonController : Controller
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
        private static string ToTurkeyTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).ToString("dd.MM.yyyy HH:mm");
        private static string? ToTurkeyTime(DateTime? utc) =>
            utc.HasValue ? ToTurkeyTime(utc.Value) : null;
        private static (DateTime startUtc, DateTime endUtc) TodayUtcRange()
        {
            var nowTr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz);
            var startTr = nowTr.Date;
            var endTr = startTr.AddDays(1);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startTr, DateTimeKind.Unspecified), _turkeyTz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endTr, DateTimeKind.Unspecified), _turkeyTz);
            return (startUtc, endUtc);
        }

        private readonly QRMenuDbContext _context;
        private readonly ISiparisService _siparisService;
        private readonly IHubContext<OrderHub> _menuHub;
        private readonly ILogger<GarsonController> _logger;
        private readonly IWebHostEnvironment _env;

        public GarsonController(
            QRMenuDbContext context,
            ISiparisService siparisService,
            IHubContext<OrderHub> menuHub,
            ILogger<GarsonController> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _siparisService = siparisService;
            _menuHub = menuHub;
            _logger = logger;
            _env = env;
        }

        // GET: /Garson/Masalar
        [HttpGet("/Garson/Masalar")]
        public async Task<IActionResult> Masalar()
        {
            ViewData["ActivePage"] = "GarsonMasalar";
            ViewData["PageTitle"] = "Garson Paneli - Masalar";

            var masalar = await _context.Masalar
                .Where(m => m.AktifMi)
                .Include(m => m.Bolge)
                .Include(m => m.Siparisler.Where(s => s.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal
                    && s.Durum != QRMenu.Core.Enums.SiparisDurum.TamOdendi
                    && s.Durum != QRMenu.Core.Enums.SiparisDurum.Iade))
                    .ThenInclude(s => s.SiparisDetaylar)
                .AsSplitQuery()
                .OrderBy(m => m.MasaNo)
                .ToListAsync();

            return View(masalar);
        }

        // GET: /Garson/Ayarlar
        [HttpGet("/Garson/Ayarlar")]
        public IActionResult Ayarlar()
        {
            ViewData["GarsonPageTitle"] = "Garson Ayarları";
            ViewData["GarsonActivePage"] = "Ayarlar";
            ViewData["GarsonContentClass"] = "p-4 sm:p-6 lg:p-8 flex-1 overflow-y-auto";
            return View();
        }

        // GET: /Garson/Masa/{id}
        [HttpGet("/Garson/Masa/{id:int}")]
        public async Task<IActionResult> Masa(int id)
        {
            var (startUtc, endUtc) = TodayUtcRange();
            var aktifDurumlar = new[] { SiparisDurum.Onaylandi, SiparisDurum.Hazirlaniyor, SiparisDurum.Hazir, SiparisDurum.TeslimEdildi };
            var gecmisDurumlar = new[] { SiparisDurum.KismiOdendi, SiparisDurum.TamOdendi, SiparisDurum.Iade, SiparisDurum.Iptal };

            var masa = await _context.Masalar
                .Include(m => m.Bolge)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (masa == null)
                return NotFound();

            ViewData["ActivePage"] = "GarsonMasalar";
            ViewData["PageTitle"] = $"Masa {masa.MasaNo} Detayı";

            var aktifSiparisler = await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .Where(s => s.MasaId == id
                    && aktifDurumlar.Contains(s.Durum))
                .OrderByDescending(s => s.OlusturmaTarihi)
                .ToListAsync();

            var gecmisSiparisler = await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .Where(s => s.MasaId == id
                    && gecmisDurumlar.Contains(s.Durum)
                    && s.OlusturmaTarihi >= startUtc
                    && s.OlusturmaTarihi < endUtc)
                .OrderByDescending(s => s.OlusturmaTarihi)
                .ToListAsync();

            ViewBag.AktifSiparisler = aktifSiparisler;
            ViewBag.GecmisSiparisler = gecmisSiparisler;

            // Offcanvas için kategorileri alalım
            var kategoriler = await _context.Kategoriler
                .Include(k => k.Urunler.Where(u => u.AktifMi))
                    .ThenInclude(u => u.UrunOpsiyonlar)
                        .ThenInclude(uo => uo.Opsiyon)
                .AsSplitQuery()
                .Where(k => k.AktifMi)
                .OrderBy(k => k.SiraNo)
                .ToListAsync();

            ViewBag.Kategoriler = kategoriler;

            return View(masa);
        }

        // GET: /Garson/Masa/{id}/YeniSiparis
        [HttpGet("/Garson/Masa/{id:int}/YeniSiparis")]
        public async Task<IActionResult> YeniSiparis(int id)
        {
            var masa = await _context.Masalar
                .Include(m => m.Bolge)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (masa == null)
                return NotFound();

            ViewData["ActivePage"] = "GarsonMasalar";
            ViewData["PageTitle"] = $"Masa {masa.MasaNo} - Yeni Sipariş";

            var aktifSiparisler = await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .Where(s => s.MasaId == id
                    && s.Durum != SiparisDurum.Iptal
                    && s.Durum != SiparisDurum.TamOdendi
                    && s.Durum != SiparisDurum.Iade)
                .OrderByDescending(s => s.OlusturmaTarihi)
                .ToListAsync();

            var kategoriler = await _context.Kategoriler
                .Include(k => k.Urunler.Where(u => u.AktifMi))
                    .ThenInclude(u => u.UrunOpsiyonlar)
                        .ThenInclude(uo => uo.Opsiyon)
                .AsSplitQuery()
                .Where(k => k.AktifMi)
                .OrderBy(k => k.SiraNo)
                .ToListAsync();

            ViewBag.AktifSiparisler = aktifSiparisler;
            ViewBag.Kategoriler = kategoriler;

            return View(masa);
        }

        // GET: /Garson/UrunGorsel/{id}
        [HttpGet("/Garson/UrunGorsel/{id:int}")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> UrunGorsel(int id)
        {
            var dbGorsel = await _context.UrunGorseller
                .Where(g => g.UrunId == id)
                .Select(g => new { g.Data, g.ContentType })
                .FirstOrDefaultAsync();

            if (dbGorsel?.Data != null && !string.IsNullOrWhiteSpace(dbGorsel.ContentType))
            {
                return File(dbGorsel.Data, dbGorsel.ContentType);
            }

            var urun = await _context.Urunler
                .Where(u => u.Id == id)
                .Select(u => u.GorselUrl)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(urun))
            {
                return Redirect(urun);
            }

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "urunler");
            var staticFiles = Directory.Exists(uploadsDir)
                ? Directory.GetFiles(uploadsDir, $"{id}.*")
                : Array.Empty<string>();

            if (staticFiles.Length > 0)
            {
                return Redirect($"/uploads/urunler/{Path.GetFileName(staticFiles[0])}");
            }

            return NotFound();
        }

        // POST: /Garson/Masa/ManuelSiparis
        [HttpPost("/Garson/Masa/ManuelSiparis")]
        public async Task<IActionResult> ManuelSiparis([FromBody] ManuelSiparisRequest request)
        {
            try
            {
                var dtos = request.Urunler.Select(u => new QRMenu.Core.DTOs.ManuelSiparisDetayDto
                {
                    UrunId = u.UrunId,
                    Adet = u.Adet,
                    OpsiyonIds = u.OpsiyonIds
                }).ToList();

                var siparis = await _siparisService.ManuelSiparisOlusturAsync(request.MasaId, dtos, "Garson " + User?.Identity?.Name);
                var masaNo = await _context.Masalar
                    .Where(m => m.Id == request.MasaId)
                    .Select(m => m.MasaNo)
                    .FirstOrDefaultAsync();
                
                await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGeldi", siparis.Id, masaNo);
                await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisEklendi");
                await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisEklendi");
                await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Table(request.MasaId)).SendAsync("SiparisGuncellendi");

                return Json(new { success = true, siparisId = siparis.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Garson manuel sipariş hatası");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Garson/Masa/UrunIptal/{detayId}
        [HttpPost("/Garson/Masa/UrunIptal/{detayId:int}")]
        public async Task<IActionResult> UrunIptal(int detayId)
        {
            var detay = await _context.SiparisDetaylar
                .Include(sd => sd.Siparis)
                    .ThenInclude(s => s.Masa)
                .AsSplitQuery()
                .FirstOrDefaultAsync(sd => sd.Id == detayId);

            if (detay == null)
                return Json(new { success = false, message = "Ürün bulunamadı." });

            if (detay.Durum == QRMenu.Core.Enums.SiparisDurum.Iptal)
                return Json(new { success = false, message = "Ürün zaten iptal edilmiş." });

            if (detay.Siparis.Durum != QRMenu.Core.Enums.SiparisDurum.Hazir && detay.Siparis.Durum != QRMenu.Core.Enums.SiparisDurum.TeslimEdildi)
                return Json(new { success = false, message = "Yalnızca hazır veya teslim edilmiş sipariş iptal edilebilir." });

            detay.Durum = QRMenu.Core.Enums.SiparisDurum.Iptal;
            
            // Eğer siparişteki tüm ürünler iptal edildiyse, ana siparişi de iptal durumuna çek
            var tumHalenAktifMi = await _context.SiparisDetaylar
                .AnyAsync(sd => sd.SiparisId == detay.SiparisId && sd.Id != detayId && sd.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal);

            if (!tumHalenAktifMi)
            {
                detay.Siparis.Durum = QRMenu.Core.Enums.SiparisDurum.Iptal;
            }
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Garson ürünü iptal etti. SiparisDetayId={DetayId}", detayId);

            var masaId = detay.Siparis.MasaId;
            var masaNo = detay.Siparis.Masa?.MasaNo ?? 0;

            await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisIptal", masaNo);
            await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisIptal", masaNo);
            await _menuHub.Clients.Group(SignalRGroups.Table(masaId)).SendAsync("SiparisIptal", masaNo);

            await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi");
            await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi");
            await _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi");
            await _menuHub.Clients.Group(SignalRGroups.Table(masaId)).SendAsync("SiparisGuncellendi");

            return Json(new { success = true });
        }
        [HttpGet("/Garson/SiparisDetay/{id:int}")]
        public async Task<IActionResult> SiparisDetayJson(int id)
        {
            var siparis = await _siparisService.GetSiparisAsync(id);
            if (siparis == null)
                return Json(new { success = false, message = "Sipariş bulunamadı." });

            return Json(new
            {
                success = true,
                id = siparis.Id,
                gunlukSiparisNo = siparis.GunlukSiparisNo,
                masaNo = siparis.Masa?.MasaNo,
                durum = siparis.Durum.ToString(),
                durumInt = (int)siparis.Durum,
                toplamTutar = siparis.ToplamTutar,
                notlar = siparis.Notlar,
                olusturmaTarihi = ToTurkeyTime(siparis.OlusturmaTarihi),
                guncellemeTarihi = ToTurkeyTime(siparis.GuncellemeTarihi),
                detaylar = siparis.SiparisDetaylar
                    .Where(sd => sd.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal)
                    .Select(sd => new
                    {
                        detayId = sd.Id,
                        urunAd = sd.Urun.Ad,
                        adet = sd.Adet,
                        birimFiyat = sd.BirimFiyat,
                        opsiyonlar = sd.SeciliOpsiyonlar,
                        durum = sd.Durum.ToString()
                    })
            });
        }

        // POST: /Garson/SiparisDetayIptal
        [HttpPost("/Garson/SiparisDetayIptal")]
        public async Task<IActionResult> SiparisDetayIptal([FromBody] SiparisDetayIptalRequest request)
        {
            if (request == null || request.Detaylar == null || request.Detaylar.Count == 0)
                return Json(new { success = false, message = "İptal edilecek kalem seçilmedi." });

            try
            {
                // Garson sadece kendi masasına ait ve uygun durumdaki kalemleri iptal edebilmeli
                if (!request.MasaId.HasValue)
                    return Json(new { success = false, message = "Masa bilgisi bulunamadı." });

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

                var allowed = new[] { QRMenu.Core.Enums.SiparisDurum.Onaylandi, QRMenu.Core.Enums.SiparisDurum.Hazir, QRMenu.Core.Enums.SiparisDurum.TeslimEdildi };
                var invalid = dbDetaylar.Where(sd => !allowed.Contains(sd.Durum)).ToList();
                if (invalid.Any())
                    return Json(new { success = false, message = "Garson yalnızca onaylanmış, hazır veya teslim edilmiş ürünleri iptal edebilir." });

                var siparisler = await _siparisService.SiparisDetayIptalEtAsync(request.Detaylar, request.MasaId);

                var iptalSiparisler = siparisler
                    .Where(s => s.Durum == SiparisDurum.Iptal)
                    .DistinctBy(s => s.MasaId)
                    .ToList();

                foreach (var siparis in iptalSiparisler)
                {
                    var masaNo = siparis.Masa?.MasaNo ?? 0;
                    await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisIptal", masaNo);
                    await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisIptal", masaNo);
                    await _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisIptal", masaNo);
                }

                foreach (var masaId in siparisler.Select(s => s.MasaId).Distinct())
                {
                    await _menuHub.Clients.Group(SignalRGroups.Table(masaId)).SendAsync("SiparisGuncellendi");
                }

                await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Garson sipariş detay iptal hatası");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Garson/DurumGuncelle/{id}
        [HttpPost("/Garson/DurumGuncelle/{id:int}")]
        public async Task<IActionResult> DurumGuncelle(int id, [FromBody] DurumGuncelleRequest request)
        {
            try
            {
                var enumDurum = Enum.Parse<QRMenu.Core.Enums.SiparisDurum>(request.YeniDurum);

                if (enumDurum == SiparisDurum.Iptal)
                {
                    var mevcutDurum = await _context.Siparisler
                        .Where(s => s.Id == id)
                        .Select(s => (SiparisDurum?)s.Durum)
                        .FirstOrDefaultAsync();

                    if (mevcutDurum == null)
                    {
                        return Json(new { success = false, message = "Sipariş bulunamadı." });
                    }

                    if (mevcutDurum != SiparisDurum.Hazir && mevcutDurum != SiparisDurum.TeslimEdildi)
                    {
                        return Json(new { success = false, message = "Yalnızca hazır veya teslim edilmiş sipariş iptal edilebilir." });
                    }
                }

                var siparis = await _siparisService.DurumGuncelleAsync(id, enumDurum);
                
                await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisGuncellendi");

                if (enumDurum == SiparisDurum.Iptal)
                {
                    var masaNo = await _context.Masalar
                        .Where(m => m.Id == siparis.MasaId)
                        .Select(m => m.MasaNo)
                        .FirstOrDefaultAsync();

                    await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisIptal", masaNo);
                    await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisIptal", masaNo);
                    await _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisIptal", masaNo);
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Garson/Masa/{id}/TopluTeslimEt
        [HttpPost("/Garson/Masa/{id:int}/TopluTeslimEt")]
        public async Task<IActionResult> TopluTeslimEt(int id)
        {
            try
            {
                var hazirSiparisIds = await _context.Siparisler
                    .Where(s => s.MasaId == id
                        && s.Durum == SiparisDurum.Hazir)
                    .Select(s => s.Id)
                    .ToListAsync();

                if (!hazirSiparisIds.Any())
                {
                    return Json(new { success = false, message = "Teslim edilecek hazır sipariş bulunamadı." });
                }

                foreach (var siparisId in hazirSiparisIds)
                {
                    await _siparisService.DurumGuncelleAsync(siparisId, SiparisDurum.TeslimEdildi);
                }

                await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Table(id)).SendAsync("SiparisGuncellendi");

                return Json(new { success = true, affectedCount = hazirSiparisIds.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Toplu teslim hatası. MasaId={MasaId}", id);
                return Json(new { success = false, message = "Toplu teslim işlemi sırasında hata oluştu." });
            }
        }

        public class DurumGuncelleRequest
        {
            public string YeniDurum { get; set; } = "";
        }
    }

    public class ManuelSiparisRequest
    {
        public int MasaId { get; set; }
        public List<ManuelSiparisItem> Urunler { get; set; } = new();
    }

    public class ManuelSiparisItem
    {
        public int UrunId { get; set; }
        public int Adet { get; set; }
        public List<int>? OpsiyonIds { get; set; }
    }
}
