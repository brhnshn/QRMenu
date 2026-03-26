using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using QRMenu.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

using Microsoft.AspNetCore.Authorization;

namespace QRMenu.Web.Controllers
{
    [Authorize(Roles = "Admin,Garson,Kasa,Mutfak")]
    public class GarsonController : Controller
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
        private static string ToTurkeyTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).ToString("dd.MM.yyyy HH:mm");
        private static string? ToTurkeyTime(DateTime? utc) =>
            utc.HasValue ? ToTurkeyTime(utc.Value) : null;

        private readonly QRMenuDbContext _context;
        private readonly ISiparisService _siparisService;
        private readonly IHubContext<MenuHub> _menuHub;
        private readonly ILogger<GarsonController> _logger;

        public GarsonController(QRMenuDbContext context, ISiparisService siparisService, IHubContext<MenuHub> menuHub, ILogger<GarsonController> logger)
        {
            _context = context;
            _siparisService = siparisService;
            _menuHub = menuHub;
            _logger = logger;
        }

        // GET: /Garson/Masalar
        [HttpGet("/Garson/Masalar")]
        public async Task<IActionResult> Masalar()
        {
            ViewData["ActivePage"] = "GarsonMasalar";
            ViewData["PageTitle"] = "Garson Paneli - Masalar";

            var masalar = await _context.Masalar
                .Include(m => m.Siparisler.Where(s => s.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal && s.Durum != QRMenu.Core.Enums.SiparisDurum.TamOdendi && s.Durum != QRMenu.Core.Enums.SiparisDurum.Iade))
                    .ThenInclude(s => s.SiparisDetaylar)
                .OrderBy(m => m.MasaNo)
                .ToListAsync();

            return View(masalar);
        }

        // GET: /Garson/Masa/{id}
        [HttpGet("/Garson/Masa/{id:int}")]
        public async Task<IActionResult> Masa(int id)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.Id == id);
            if (masa == null)
                return NotFound();

            ViewData["ActivePage"] = "GarsonMasalar";
            ViewData["PageTitle"] = $"Masa {masa.MasaNo} Detayı";

            var aktifSiparisler = await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Where(s => s.MasaId == id && s.Durum != QRMenu.Core.Enums.SiparisDurum.Iptal && s.Durum != QRMenu.Core.Enums.SiparisDurum.TamOdendi && s.Durum != QRMenu.Core.Enums.SiparisDurum.Iade)
                .OrderByDescending(s => s.OlusturmaTarihi)
                .ToListAsync();

            ViewBag.AktifSiparisler = aktifSiparisler;

            // Offcanvas için kategorileri alalım
            var kategoriler = await _context.Kategoriler
                .Include(k => k.Urunler.Where(u => u.AktifMi))
                    .ThenInclude(u => u.UrunOpsiyonlar)
                        .ThenInclude(uo => uo.Opsiyon)
                .Where(k => k.AktifMi)
                .OrderBy(k => k.SiraNo)
                .ToListAsync();

            ViewBag.Kategoriler = kategoriler;

            return View(masa);
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
                
                // Mutfağa uyarı gönder
                await _menuHub.Clients.All.SendAsync("SiparisEklendi");
                // Diğer panellere (Kasa, Admin) güncelleme gönder
                await _menuHub.Clients.All.SendAsync("SiparisGuncellendi");

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
                .FirstOrDefaultAsync(sd => sd.Id == detayId);

            if (detay == null)
                return Json(new { success = false, message = "Ürün bulunamadı." });

            if (detay.Durum == QRMenu.Core.Enums.SiparisDurum.Iptal)
                return Json(new { success = false, message = "Ürün zaten iptal edilmiş." });

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

            // Mutfak ekranını canlı güncellemek için
            await _menuHub.Clients.All.SendAsync("SiparisGuncellendi");

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
                masaNo = siparis.Masa?.MasaNo,
                durum = siparis.Durum.ToString(),
                durumInt = (int)siparis.Durum,
                toplamTutar = siparis.ToplamTutar,
                notlar = siparis.Notlar,
                olusturmaTarihi = ToTurkeyTime(siparis.OlusturmaTarihi),
                guncellemeTarihi = ToTurkeyTime(siparis.GuncellemeTarihi),
                detaylar = siparis.SiparisDetaylar.Select(sd => new
                {
                    urunAd = sd.Urun.Ad,
                    adet = sd.Adet,
                    birimFiyat = sd.BirimFiyat,
                    opsiyonlar = sd.SeciliOpsiyonlar,
                    durum = sd.Durum.ToString()
                })
            });
        }

        // POST: /Garson/DurumGuncelle/{id}
        [HttpPost("/Garson/DurumGuncelle/{id:int}")]
        public async Task<IActionResult> DurumGuncelle(int id, [FromBody] DurumGuncelleRequest request)
        {
            try
            {
                var enumDurum = Enum.Parse<QRMenu.Core.Enums.SiparisDurum>(request.YeniDurum);
                await _siparisService.DurumGuncelleAsync(id, enumDurum);
                
                await _menuHub.Clients.All.SendAsync("SiparisGuncellendi");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
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
