using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ClosedXML.Excel;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using QRCoder;
using QRMenu.Web.ViewModels;
using QRMenu.Web.Hubs;
using QRMenu.Data.Data;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireAdmin")]
    public class AdminController : Controller
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
        private static string ToTurkeyTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).ToString("dd.MM.yyyy HH:mm");
        private static string? ToTurkeyTime(DateTime? utc) =>
            utc.HasValue ? ToTurkeyTime(utc.Value) : null;
        private static DateTime RaporTarihi(DateTime utc) =>
            DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).Date, DateTimeKind.Utc);
        private static DateTime ParseGunTarihi(string? tarih, DateTime fallbackDate)
        {
            return DateTime.TryParse(tarih, out var parsedDate) ? parsedDate.Date : fallbackDate.Date;
        }

        private readonly QRMenuDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly ISiparisService _siparisService;
        private readonly IHubContext<OrderHub> _menuHub;
        private readonly UserManager<Kullanici> _userManager;

        public AdminController(
            QRMenuDbContext context,
            ILogger<AdminController> logger,
            IWebHostEnvironment env,
            ISiparisService siparisService,
            IHubContext<OrderHub> menuHub,
            UserManager<Kullanici> userManager)
        {
            _context = context;
            _logger = logger;
            _env = env;
            _siparisService = siparisService;
            _menuHub = menuHub;
            _userManager = userManager;
        }

        // Admin ana sayfa -> Dashboard
        [HttpGet("/admin")]
        public IActionResult OldIndex()
        {
            return Redirect("/admin/panel");
        }

        [HttpGet("/admin/panel")]
        public async Task<IActionResult> Panel(DateTime? baslangic, DateTime? bitis)
        {
            ViewData["ActivePage"] = "Dashboard";
            ViewData["PageTitle"] = "Yönetim Paneli";

            // Tarih filtreleme (Varsayılan: Bugün)
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz);
            var today = now.Date;
            var startDate = baslangic?.Date ?? today;
            var endDate = bitis?.Date ?? today;

            // UTC Dönüşümleri (DB sorguları için)
            var startDateUtc = TimeZoneInfo.ConvertTimeToUtc(startDate, _turkeyTz);
            var endDateUtc = TimeZoneInfo.ConvertTimeToUtc(endDate.AddDays(1), _turkeyTz);

            // Stats
            var bugunSiparisler = await _context.Siparisler
                .Where(s => s.OlusturmaTarihi >= startDateUtc && s.OlusturmaTarihi < endDateUtc)
                .Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.Iade)
                .ToListAsync();

            var bugunCiro = bugunSiparisler.Sum(s => s.ToplamTutar);

            var dunStartDateUtc = TimeZoneInfo.ConvertTimeToUtc(startDate.AddDays(-1), _turkeyTz);
            var dunEndDateUtc = TimeZoneInfo.ConvertTimeToUtc(endDate.AddDays(-1).AddDays(1), _turkeyTz);
            var dunCiro = await _context.Siparisler
                .Where(s => s.OlusturmaTarihi >= dunStartDateUtc && s.OlusturmaTarihi < dunEndDateUtc)
                .Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.Iade)
                .SumAsync(s => s.ToplamTutar);

            var ciroDegisim = dunCiro > 0 ? (double)((bugunCiro - dunCiro) / dunCiro * 100) : 0;

            var aktifMasalarCount = await _context.Masalar
                .CountAsync(m => m.AktifMi && m.Siparisler.Any(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.TamOdendi && s.Durum != SiparisDurum.Iade));
            
            var toplamMasaCount = await _context.Masalar.CountAsync(m => m.AktifMi);
            var dolulukOrani = toplamMasaCount > 0 ? (int)((double)aktifMasalarCount / toplamMasaCount * 100) : 0;

            // Ort. Servis Süresi (Onaylandı -> TeslimEdildi arası fark)
            var teslimEdilenler = bugunSiparisler.Where(s => s.Durum == SiparisDurum.TeslimEdildi || s.Durum == SiparisDurum.TamOdendi).ToList();
            var ortServisSuresi = teslimEdilenler.Any() 
                ? teslimEdilenler.Average(s => (s.GuncellemeTarihi ?? s.OlusturmaTarihi).Subtract(s.OlusturmaTarihi).TotalMinutes)
                : 0;

            // Saatlik Trafik Analizi
            var saatlikData = bugunSiparisler
                .GroupBy(s => TimeZoneInfo.ConvertTimeFromUtc(s.OlusturmaTarihi, _turkeyTz).Hour)
                .Select(g => new { Saat = g.Key, Ciro = g.Sum(s => s.ToplamTutar) })
                .OrderBy(x => x.Saat)
                .ToList();

            // En Ã‡ok Satanlar (yeni verilerle)
            var enCokSatanlar = await _context.SiparisDetaylar
                .Include(sd => sd.Urun)
                .Where(sd => sd.Siparis.OlusturmaTarihi >= startDateUtc && sd.Siparis.OlusturmaTarihi < endDateUtc)
                .Where(sd => sd.Siparis.Durum != SiparisDurum.Iptal && sd.Siparis.Durum != SiparisDurum.Iade)
                .GroupBy(sd => new { sd.Urun.Ad, sd.Urun.GorselUrl, sd.BirimFiyat })
                .Select(g => new EnCokSatanViewModel { Ad = g.Key.Ad, Adet = g.Sum(sd => sd.Adet), GorselUrl = g.Key.GorselUrl, Fiyat = g.Key.BirimFiyat })
                .OrderByDescending(x => x.Adet)
                .Take(3)
                .ToListAsync();

            var bugunEnCokSatanlarTum = await _context.SiparisDetaylar
                .Include(sd => sd.Urun)
                .Where(sd => sd.Siparis.OlusturmaTarihi >= startDateUtc && sd.Siparis.OlusturmaTarihi < endDateUtc)
                .Where(sd => sd.Siparis.Durum != SiparisDurum.Iptal && sd.Siparis.Durum != SiparisDurum.Iade)
                .GroupBy(sd => new { sd.Urun.Ad, sd.Urun.GorselUrl, sd.BirimFiyat })
                .Select(g => new EnCokSatanViewModel { Ad = g.Key.Ad, Adet = g.Sum(sd => sd.Adet), GorselUrl = g.Key.GorselUrl, Fiyat = g.Key.BirimFiyat })
                .OrderByDescending(x => x.Adet)
                .ThenBy(x => x.Ad)
                .ToListAsync();

            // Canlı Siparişler (Yeni & Hazırlanıyor)
            var canliSiparisler = await _context.Siparisler
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Where(s => s.Durum == SiparisDurum.Onaylandi || s.Durum == SiparisDurum.Hazirlaniyor)
                .OrderByDescending(s => s.OlusturmaTarihi)
                .Take(4)
                .ToListAsync();

            // Personel Count
            var garsonCount = (await _userManager.GetUsersInRoleAsync("Garson")).Count;
            var mutfakCount = (await _userManager.GetUsersInRoleAsync("Mutfak")).Count;
            var kasaCount = (await _userManager.GetUsersInRoleAsync("Kasa")).Count;
            var personelCount = garsonCount + mutfakCount + kasaCount;

            var odemeTipleri = await _context.Odemeler
                .Where(o => o.OdemeTarihi >= startDateUtc && o.OdemeTarihi < endDateUtc)
                .GroupBy(o => o.OdemeTipi)
                .Select(g => new ZOdemeTipiViewModel
                {
                    Tip = g.Key.ToString(),
                    Tutar = g.Sum(o => o.Tutar),
                    Adet = g.Count()
                })
                .OrderByDescending(x => x.Tutar)
                .ToListAsync();

            var zRaporTarihi = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var gunSonuRapor = await _context.GunSonuRaporlari
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Tarih == zRaporTarihi);

            var zToplamCiro = odemeTipleri.Sum(x => x.Tutar);
            var zSiparisSayisi = bugunSiparisler.Count;

            ViewBag.BugunCiro = bugunCiro;
            ViewBag.DunCiro = dunCiro;
            ViewBag.CiroDegisim = ciroDegisim;
            ViewBag.AktifMasalarCount = aktifMasalarCount;
            ViewBag.ToplamMasaCount = toplamMasaCount;
            ViewBag.DolulukOrani = dolulukOrani;
            ViewBag.OrtServisSuresi = Math.Round(ortServisSuresi, 1);
            ViewBag.SaatlikData = saatlikData;
            ViewBag.EnCokSatanlar = enCokSatanlar;
            ViewBag.BugunEnCokSatanlarTum = bugunEnCokSatanlarTum;
            ViewBag.CanliSiparisler = canliSiparisler;
            ViewBag.PersonelCount = personelCount;
            ViewBag.Baslangic = startDate.ToString("yyyy-MM-dd");
            ViewBag.Bitis = endDate.ToString("yyyy-MM-dd");
            ViewBag.ZRaporTarihi = startDate.ToString("yyyy-MM-dd");
            ViewBag.ZToplamCiro = gunSonuRapor?.ToplamCiro ?? zToplamCiro;
            ViewBag.ZSiparisSayisi = gunSonuRapor?.SiparisSayisi ?? zSiparisSayisi;
            ViewBag.ZOdemeTipleri = gunSonuRapor != null
                ? JsonSerializer.Deserialize<List<ZOdemeTipiViewModel>>(gunSonuRapor.OdemeTipleriJson) ?? new List<ZOdemeTipiViewModel>()
                : odemeTipleri;
            ViewBag.GunKapaliMi = gunSonuRapor != null;
            ViewBag.GunKapanisTarihi = gunSonuRapor?.KapanisTarihi;
            ViewBag.ZNextOpeningIso = startDate.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss");
            ViewBag.GunKapanisMetni = gunSonuRapor?.KapanisTarihi != null
                ? ToTurkeyTime(gunSonuRapor.KapanisTarihi)
                : null;

            return View();
        }

        [HttpPost("/admin/gun-sonu-kapat")]
        public async Task<IActionResult> GunSonuKapat([FromBody] GunSonuKapatRequest? request)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz);
            var gun = DateTime.TryParse(request?.Tarih, out var parsedDate) ? parsedDate.Date : now.Date;
            var startDateUtc = TimeZoneInfo.ConvertTimeToUtc(gun, _turkeyTz);
            var endDateUtc = TimeZoneInfo.ConvertTimeToUtc(gun.AddDays(1), _turkeyTz);
            var raporTarihi = DateTime.SpecifyKind(gun, DateTimeKind.Utc);

            var mevcut = await _context.GunSonuRaporlari.FirstOrDefaultAsync(r => r.Tarih == raporTarihi);
            if (mevcut != null)
                return Json(new { success = false, message = "Bu gün zaten kapatılmış." });

            var siparisSayisi = await _context.Siparisler
                .Where(s => s.OlusturmaTarihi >= startDateUtc && s.OlusturmaTarihi < endDateUtc)
                .Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.Iade)
                .CountAsync();

            var odemeTipleri = await _context.Odemeler
                .Where(o => o.OdemeTarihi >= startDateUtc && o.OdemeTarihi < endDateUtc)
                .GroupBy(o => o.OdemeTipi)
                .Select(g => new ZOdemeTipiViewModel
                {
                    Tip = g.Key.ToString(),
                    Tutar = g.Sum(o => o.Tutar),
                    Adet = g.Count()
                })
                .OrderByDescending(x => x.Tutar)
                .ToListAsync();

            var rapor = new GunSonuRapor
            {
                Tarih = raporTarihi,
                ToplamCiro = odemeTipleri.Sum(x => x.Tutar),
                SiparisSayisi = siparisSayisi,
                OdemeTipleriJson = JsonSerializer.Serialize(odemeTipleri),
                KapanisTarihi = DateTime.UtcNow,
                KapatanKullaniciId = _userManager.GetUserId(User)
            };

            _context.GunSonuRaporlari.Add(rapor);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Gün sonu kapatıldı. Tarih={Tarih}, Ciro={Ciro}, SiparisSayisi={SiparisSayisi}", gun, rapor.ToplamCiro, rapor.SiparisSayisi);
            return Json(new { success = true });
        }

        [HttpPost("/admin/gun-sonu-ac")]
        public async Task<IActionResult> GunSonuAc([FromBody] GunSonuKapatRequest? request)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz);
            var gun = ParseGunTarihi(request?.Tarih, now);
            var raporTarihi = DateTime.SpecifyKind(gun, DateTimeKind.Utc);

            var rapor = await _context.GunSonuRaporlari.FirstOrDefaultAsync(r => r.Tarih == raporTarihi);
            if (rapor == null)
                return Json(new { success = false, message = "Açılacak kapalı gün bulunamadı." });

            _context.GunSonuRaporlari.Remove(rapor);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Gün sonu tekrar açıldı. Tarih={Tarih}", gun);
            return Json(new { success = true });
        }

        [HttpGet("/admin/gun-sonu-pdf")]
        public async Task<IActionResult> GunSonuPdf(string? tarih)
        {
            var model = await BuildGunSonuExportModelAsync(tarih);
            return File(CreateGunSonuPdf(model), "application/pdf", $"z-raporu-{model.Tarih:yyyy-MM-dd}.pdf");
        }

        [HttpGet("/admin/gun-sonu-excel")]
        public async Task<IActionResult> GunSonuExcel(string? tarih)
        {
            var model = await BuildGunSonuExportModelAsync(tarih);
            return File(
                CreateGunSonuExcel(model),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"gunluk-satis-{model.Tarih:yyyy-MM-dd}.xlsx");
        }

        [HttpGet("/admin/loglar")]
        public async Task<IActionResult> Loglar(string? eventType, string? sortOrder)
        {
            ViewData["ActivePage"] = "Loglar";
            ViewData["PageTitle"] = "Güvenlik Logları";

            var query = _context.SecurityLogs.AsQueryable();

            if (!string.IsNullOrEmpty(eventType))
            {
                query = query.Where(l => l.EventType == eventType);
            }

            if (sortOrder == "asc")
            {
                query = query.OrderBy(l => l.Timestamp);
            }
            else
            {
                query = query.OrderByDescending(l => l.Timestamp);
            }

            var logs = await query.Take(1000).ToListAsync();

            ViewBag.CurrentFilter = eventType;
            ViewBag.CurrentSort = sortOrder;
            
            return View(logs);
        }

        [HttpGet("/admin/masalar")]
        public async Task<IActionResult> Masalar()
        {
            var masalar = await _context.Masalar
                .Where(m => m.AktifMi)
                .Include(m => m.Bolge)
                .Include(m => m.Siparisler.Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.TamOdendi && s.Durum != SiparisDurum.Iade))
                .OrderBy(m => m.BolgeId.HasValue ? m.Bolge!.SiraNo : 9999)
                .ThenBy(m => m.MasaNo)
                .ToListAsync();

            var bolgeler = await _context.Bolgeler.OrderBy(b => b.SiraNo).ToListAsync();
            ViewBag.Bolgeler = bolgeler;

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            
            using var gen = new QRCodeGenerator();
            var liste = masalar.Select(m => 
            {
                string base64 = "";
                if (!string.IsNullOrEmpty(m.QrKodUrl))
                {
                    var data = gen.CreateQrCode(m.QrKodUrl, QRCodeGenerator.ECCLevel.H);
                    using var qr = new PngByteQRCode(data);
                    base64 = Convert.ToBase64String(qr.GetGraphic(10));
                }
                
                return new QrKodViewModel
                {
                    Id = m.Id,
                    MasaNo = m.MasaNo,
                    QrUrl = m.QrKodUrl ?? "",
                    QrBase64 = base64,
                    DoluMu = m.Siparisler.Any(),
                    BolgeId = m.BolgeId,
                    BolgeAd = m.Bolge?.Ad
                };
            }).ToList();

            ViewBag.BaseUrl = baseUrl;
            return View(liste);
        }

        // AJAX: Masa oluştur
        [HttpGet("/admin/masa-olustur/{masaNo:int}")]
        public async Task<IActionResult> MasaOlustur(int masaNo)
        {
            var mevcut = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (mevcut != null)
            {
                if (!mevcut.AktifMi)
                {
                    mevcut.AktifMi = true;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Masa pasif durumdan aktif yapıldı. MasaNo={MasaNo}", masaNo);
                    return Json(new { success = true });
                }
                return Json(new { success = false, message = $"Masa {masaNo} zaten var" });
            }

            _context.Masalar.Add(new Masa { MasaNo = masaNo, AktifMi = true });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Masa oluşturuldu. MasaNo={MasaNo}", masaNo);
            return Json(new { success = true });
        }

        // AJAX: Masa oluştur (Yeni)
        [HttpPost("/admin/masa-ekle")]
        public async Task<IActionResult> MasaEkle([FromBody] QRMenu.Web.ViewModels.MasaFormViewModel model)
        {
            var mevcut = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == model.MasaNo);
            if (mevcut != null)
            {
                if (!mevcut.AktifMi)
                {
                    mevcut.AktifMi = true;
                    mevcut.BolgeId = model.BolgeId;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Pasif masa aktif edildi. MasaNo={MasaNo} BolgeId={BolgeId}", model.MasaNo, model.BolgeId);
                    return Json(new { success = true });
                }
                return Json(new { success = false, message = $"Masa {model.MasaNo} zaten var" });
            }

            var yeniMasa = new Masa { MasaNo = model.MasaNo, BolgeId = model.BolgeId, AktifMi = true };
            _context.Masalar.Add(yeniMasa);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Masa oluşturuldu. MasaNo={MasaNo} BolgeId={BolgeId}", model.MasaNo, model.BolgeId);
            return Json(new { success = true });
        }

        // AJAX: Masa guncelle (Yeni)
        [HttpPost("/admin/masa-guncelle/{id:int}")]
        public async Task<IActionResult> MasaGuncelle(int id, [FromBody] QRMenu.Web.ViewModels.MasaFormViewModel model)
        {
            var masa = await _context.Masalar.FindAsync(id);
            if (masa == null) return Json(new { success = false, message = "Masa bulunamadı" });

            if (masa.MasaNo != model.MasaNo && await _context.Masalar.AnyAsync(m => m.MasaNo == model.MasaNo))
                return Json(new { success = false, message = "Bu numarada başka bir masa var!" });

            masa.MasaNo = model.MasaNo;
            masa.BolgeId = model.BolgeId;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("/admin/bolge-ekle")]
        public async Task<IActionResult> BolgeEkle([FromBody] QRMenu.Web.ViewModels.BolgeFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Ad)) return Json(new { success = false, message = "Bölge adı zorunlu." });
            var b = new Bolge { Ad = model.Ad, SiraNo = model.SiraNo };
            _context.Bolgeler.Add(b);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("/admin/bolge-guncelle/{id:int}")]
        public async Task<IActionResult> BolgeGuncelle(int id, [FromBody] QRMenu.Web.ViewModels.BolgeFormViewModel model)
        {
            var b = await _context.Bolgeler.FindAsync(id);
            if (b == null) return Json(new { success = false, message = "Bulunamadı" });
            b.Ad = model.Ad;
            b.SiraNo = model.SiraNo;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("/admin/bolge-sil/{id:int}")]
        public async Task<IActionResult> BolgeSil(int id)
        {
            var b = await _context.Bolgeler.FindAsync(id);
            if (b == null) return Json(new { success = false });

            if (await _context.Masalar.AnyAsync(m => m.BolgeId == id))
                return Json(new { success = false, message = "Bu bölgeye baÄŸlı masalar var. Ã–nce masaları kaldırın/taşıyın." });

            _context.Bolgeler.Remove(b);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet("/admin/masa-sil/{masaNo:int}")]
        public async Task<IActionResult> MasaSil(int masaNo, [FromQuery] bool force = false)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (masa == null)
                return Json(new { success = false, message = "Masa bulunamadı" });

            var oturumVar = await _context.Oturumlar.AnyAsync(o => o.MasaId == masa.Id);
            var siparisVar = await _context.Siparisler.AnyAsync(s => s.MasaId == masa.Id);
            
            if ((oturumVar || siparisVar) && !force)
            {
                // Soft delete and warn the user
                masa.AktifMi = false;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Masa pasife alındı (baÄŸlı kayıt var). MasaNo={MasaNo}", masaNo);
                return Json(new { success = true, hasRecords = true, masaId = masa.Id });
            }

            if (force)
            {
                var siparisler = await _context.Siparisler.Where(s => s.MasaId == masa.Id).ToListAsync();
                if (siparisler.Any())
                {
                    var kilitliRaporTarihleri = siparisler.Select(s => RaporTarihi(s.OlusturmaTarihi)).Distinct().ToList();
                    var kilitliKayitVar = await _context.GunSonuRaporlari.AnyAsync(r => kilitliRaporTarihleri.Contains(r.Tarih));
                    if (kilitliKayitVar)
                        return Json(new { success = false, message = "Bu masada kapatılmış gün sonu kayıtları var; baÄŸlı siparişler silinemez." });

                    var sipIds = siparisler.Select(s => s.Id).ToList();
                    var detaylar = await _context.SiparisDetaylar.Where(sd => sipIds.Contains(sd.SiparisId)).ToListAsync();
                    if (detaylar.Any()) _context.SiparisDetaylar.RemoveRange(detaylar);
                    _context.Siparisler.RemoveRange(siparisler);
                }
                var oturumlar = await _context.Oturumlar.Where(o => o.MasaId == masa.Id).ToListAsync();
                if (oturumlar.Any()) _context.Oturumlar.RemoveRange(oturumlar);
            }

            _context.Masalar.Remove(masa);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Masa silindi. MasaNo={MasaNo}", masaNo);
            return Json(new { success = true, hasRecords = false });
        }

        public class QrOlusturRequest { public string BaseUrl { get; set; } = ""; }

        // AJAX: QR oluştur ve DB'ye kaydet
        [HttpPost("/admin/qr-olustur/{masaNo:int}")]
        public async Task<IActionResult> QrOlustur(int masaNo, [FromBody] QrOlusturRequest req)
        {
            var masa = await _context.Masalar.FirstOrDefaultAsync(m => m.MasaNo == masaNo);
            if (masa == null)
                return Json(new { success = false, message = "Masa bulunamadı" });

            var baseUrl = string.IsNullOrWhiteSpace(req?.BaseUrl) ? $"{Request.Scheme}://{Request.Host}" : req.BaseUrl.TrimEnd('/');
            var qrUrl = $"{baseUrl}/qr/{masaNo}";

            using var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.H);
            using var qr = new PngByteQRCode(data);
            var bytes = qr.GetGraphic(10);

            masa.QrKodUrl = qrUrl;
            await _context.SaveChangesAsync();

            _logger.LogInformation("QR oluşturuldu. Masa={MasaNo} Url={Url}", masaNo, qrUrl);
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

        // ============================================================
        // SİPARİŞ YÃ–NETİMİ
        // ============================================================

        // Sipariş Arşivi
        [HttpGet("/admin/siparisler")]
        public async Task<IActionResult> Siparisler([FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] int? masaId = null)
        {
            ViewData["ActivePage"] = "Siparisler";
            ViewData["PageTitle"] = "Sipariş Geçmişi & Raporlama";

            if (page < 1) page = 1;
            pageSize = Math.Clamp(pageSize, 20, 200);

            var siparisBaseQuery = _context.Siparisler.AsNoTracking().AsQueryable();

            var bugunTr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz).Date;
            var bugunBaslangicUtc = TimeZoneInfo.ConvertTimeToUtc(bugunTr, _turkeyTz);
            var bugunBitisUtc = TimeZoneInfo.ConvertTimeToUtc(bugunTr.AddDays(1), _turkeyTz);

            var bugunToplamSayi = await siparisBaseQuery
                .Where(s => s.OlusturmaTarihi >= bugunBaslangicUtc && s.OlusturmaTarihi < bugunBitisUtc)
                .CountAsync();

            var bugunCiro = await siparisBaseQuery
                .Where(s => s.OlusturmaTarihi >= bugunBaslangicUtc
                            && s.OlusturmaTarihi < bugunBitisUtc
                            && s.Durum == SiparisDurum.TamOdendi)
                .SumAsync(s => (decimal?)s.ToplamTutar) ?? 0m;

            var iptalIadeStats = await siparisBaseQuery
                .Where(s => s.Durum == SiparisDurum.Iptal || s.Durum == SiparisDurum.Iade)
                .GroupBy(s => 1)
                .Select(g => new { Count = g.Count(), Total = g.Sum(x => (decimal?)x.ToplamTutar) ?? 0m })
                .FirstOrDefaultAsync();

            var iptalIadeSayi = iptalIadeStats?.Count ?? 0;
            var iptalIadeCiro = iptalIadeStats?.Total ?? 0m;

            var query = siparisBaseQuery;
            if (masaId.HasValue)
            {
                query = query.Where(s => s.MasaId == masaId.Value);
                ViewBag.FiltreliMasaId = masaId.Value;
            }

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var siparisler = await query
                .Include(s => s.Masa)
                .ThenInclude(m => m.Bolge)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .OrderByDescending(s => s.OlusturmaTarihi)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.BugunToplamSayi = bugunToplamSayi;
            ViewBag.BugunCiro = bugunCiro;
            ViewBag.IptalIadeSayi = iptalIadeSayi;
            ViewBag.IptalIadeCiro = iptalIadeCiro;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(siparisler);
        }

        [HttpGet("/admin/siparis-detay/{id:int}")]
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

        [HttpPost("/admin/siparis-durum/{id:int}")]
        public async Task<IActionResult> AdminDurumGuncelle(int id, [FromBody] AdminDurumRequest request)
        {
            try
            {
                if (!Enum.TryParse<SiparisDurum>(request.YeniDurum, out var yeniDurum))
                    return Json(new { success = false, message = "Geçersiz durum." });

                var siparis = await _siparisService.DurumGuncelleAsync(id, yeniDurum);
                _logger.LogInformation("Admin sipariş durumu güncelledi. SiparisId={Id}, YeniDurum={Durum}", id, yeniDurum);

                var masaNo = await _context.Masalar
                    .Where(m => m.Id == siparis.MasaId)
                    .Select(m => m.MasaNo)
                    .FirstOrDefaultAsync();

                await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisGuncellendi");

                if (yeniDurum == SiparisDurum.Hazir)
                {
                    await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisHazir", masaNo);
                    await _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisHazir", masaNo);
                }

                if (yeniDurum == SiparisDurum.Iptal)
                {
                    await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisIptal", masaNo);
                    await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisIptal", masaNo);
                    await _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisIptal", masaNo);
                }

                return Json(new { success = true, durum = siparis.Durum.ToString(), durumInt = (int)siparis.Durum });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/admin/siparisler-json")]
        public async Task<IActionResult> SiparislerJson([FromQuery] string? durum, [FromQuery] int? masaId)
        {
            var query = _context.Siparisler
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .AsQueryable();

            if (!string.IsNullOrEmpty(durum) && Enum.TryParse<SiparisDurum>(durum, out var durumEnum))
                query = query.Where(s => s.Durum == durumEnum);

            if (masaId.HasValue)
                query = query.Where(s => s.MasaId == masaId.Value);

            var siparisler = await query
                .OrderByDescending(s => s.OlusturmaTarihi)
                .ToListAsync();

            return Json(siparisler.Select(s => new
            {
                id = s.Id,
                masaNo = s.Masa?.MasaNo,
                durum = s.Durum.ToString(),
                durumInt = (int)s.Durum,
                toplamTutar = s.ToplamTutar,
                olusturmaTarihi = ToTurkeyTime(s.OlusturmaTarihi),
                urunSayisi = s.SiparisDetaylar.Sum(sd => sd.Adet),
                detayOzet = string.Join(", ", s.SiparisDetaylar.Select(sd => $"{sd.Adet}Ã— {sd.Urun.Ad}"))
            }));
        }

        // ============================================================
        // ÃœRÃœN YÃ–NETİMİ SAYFASI
        // ============================================================

        [HttpGet("/admin/urunler")]
        public async Task<IActionResult> Urunler()
        {
            ViewData["ActivePage"] = "Urunler";
            ViewData["PageTitle"] = "Ürün & Kategori Yönetimi";

            var kategoriler = await _context.Kategoriler
                .OrderBy(k => k.SiraNo)
                .ToListAsync();

            var urunler = await _context.Urunler
                .Include(u => u.Kategori)
                .Include(u => u.UrunOpsiyonlar)
                    .ThenInclude(uo => uo.Opsiyon)
                .AsSplitQuery()
                .OrderBy(u => u.Kategori.SiraNo)
                .ThenBy(u => u.Ad)
                .ToListAsync();

            ViewBag.Kategoriler = kategoriler;
            return View(urunler);
        }

        // ============================================================
        // KATEGORİ CRUD
        // ============================================================

        [HttpGet("/admin/kategoriler")]
        public async Task<IActionResult> KategoriListesi()
        {
            var kategoriler = await _context.Kategoriler
                .OrderBy(k => k.SiraNo)
                .Select(k => new { k.Id, k.Ad, k.AdEN, k.SiraNo, k.AktifMi, UrunSayisi = k.Urunler.Count })
                .ToListAsync();
            return Json(kategoriler);
        }

        [HttpPost("/admin/kategori-ekle")]
        public async Task<IActionResult> KategoriEkle([FromBody] KategoriFormViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Geçersiz veri." });

            var kategori = new Kategori
            {
                Ad = model.Ad,
                AdEN = model.AdEN,
                SiraNo = model.SiraNo,
                AktifMi = true
            };

            _context.Kategoriler.Add(kategori);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Kategori eklendi. Id={Id}, Ad={Ad}", kategori.Id, kategori.Ad);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, id = kategori.Id });
        }

        [HttpPost("/admin/kategori-guncelle/{id:int}")]
        public async Task<IActionResult> KategoriGuncelle(int id, [FromBody] KategoriFormViewModel model)
        {
            var kategori = await _context.Kategoriler.FindAsync(id);
            if (kategori == null)
                return Json(new { success = false, message = "Kategori bulunamadı." });

            kategori.Ad = model.Ad;
            kategori.AdEN = model.AdEN;
            kategori.SiraNo = model.SiraNo;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Kategori güncellendi. Id={Id}", id);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpPost("/admin/kategori-sil/{id:int}")]
        public async Task<IActionResult> KategoriSil(int id)
        {
            var kategori = await _context.Kategoriler
                .Include(k => k.Urunler)
                .AsSplitQuery()
                .FirstOrDefaultAsync(k => k.Id == id);

            if (kategori == null)
                return Json(new { success = false, message = "Kategori bulunamadı." });

            if (kategori.Urunler.Any())
                return Json(new { success = false, message = $"Bu kategoride {kategori.Urunler.Count} ürün var. Ã–nce ürünleri taşıyın veya silin." });

            _context.Kategoriler.Remove(kategori);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Kategori silindi. Id={Id}, Ad={Ad}", id, kategori.Ad);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        // ============================================================
        // ÃœRÃœN CRUD
        // ============================================================

        [HttpPost("/admin/urun-tasi")]
        public async Task<IActionResult> UrunTasi([FromBody] UrunTasiViewModel model)
        {
            if (model.UrunIds == null || !model.UrunIds.Any())
                return Json(new { success = false, message = "Taşınacak ürün seçilmedi." });

            var kategori = await _context.Kategoriler.FindAsync(model.YeniKategoriId);
            if (kategori == null)
                return Json(new { success = false, message = "Hedef kategori bulunamadı." });

            var urunler = await _context.Urunler
                .Where(u => model.UrunIds.Contains(u.Id))
                .ToListAsync();

            if (!urunler.Any())
                return Json(new { success = false, message = "Seçilen ürünler bulunamadı." });

            foreach (var urun in urunler)
                urun.KategoriId = model.YeniKategoriId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Ãœrünler taşındı. Adet={Adet}, Yeni Kategori={KatId}", urunler.Count, model.YeniKategoriId);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, tasinanAdet = urunler.Count });
        }

        [HttpGet("/admin/urun-detay/{id:int}")]
        public async Task<IActionResult> UrunDetay(int id)
        {
            var urun = await _context.Urunler
                .Include(u => u.Kategori)
                .Include(u => u.UrunOpsiyonlar)
                    .ThenInclude(uo => uo.Opsiyon)
                .AsSplitQuery()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (urun == null)
                return Json(new { success = false, message = "Ãœrün bulunamadı." });

            return Json(new
            {
                success = true,
                urun = new
                {
                    urun.Id,
                    urun.Ad,
                    urun.AdEN,
                    urun.Aciklama,
                    urun.AciklamaEN,
                    urun.Fiyat,
                    urun.KategoriId,
                    urun.GorselUrl,
                    urun.PopulerMi,
                    urun.AktifMi,
                    urun.Kalori,
                    Opsiyonlar = urun.UrunOpsiyonlar.OrderBy(uo => uo.Opsiyon.EkFiyat).Select(uo => new
                    {
                        uo.Opsiyon.Id,
                        uo.Opsiyon.Ad,
                        uo.Opsiyon.AdEN,
                        uo.Opsiyon.Grup,
                        uo.Opsiyon.GrupEN,
                        uo.Opsiyon.EkFiyat,
                        uo.Opsiyon.Zorunlu
                    })
                }
            });
        }

        [HttpPost("/admin/urun-ekle")]
        public async Task<IActionResult> UrunEkle([FromForm] UrunFormViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Geçersiz veri. Zorunlu alanları doldurunuz." });

            var urun = new Urun
            {
                Ad = model.Ad,
                AdEN = model.AdEN,
                Aciklama = model.Aciklama,
                AciklamaEN = model.AciklamaEN,
                Fiyat = model.Fiyat,
                KategoriId = model.KategoriId,
                PopulerMi = model.PopulerMi,
                AktifMi = model.AktifMi,
                Kalori = model.Kalori
            };

            _context.Urunler.Add(urun);
            await _context.SaveChangesAsync();

            // FotoÄŸraf upload - dosya sistemine kaydet
            if (model.Gorsel != null)
            {
                var savedPath = await SaveImageToFileAsync(model.Gorsel, urun.Id);
                if (savedPath == null)
                    return Json(new { success = false, message = "Görsel yüklenemedi. Max 2MB, sadece jpg/png/webp." });

                urun.GorselUrl = savedPath;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Ãœrün eklendi. Id={Id}, Ad={Ad}", urun.Id, urun.Ad);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, id = urun.Id });
        }

        [HttpPost("/admin/urun-guncelle/{id:int}")]
        public async Task<IActionResult> UrunGuncelle(int id, [FromForm] UrunFormViewModel model)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null)
                return Json(new { success = false, message = "Ãœrün bulunamadı." });

            urun.Ad = model.Ad;
            urun.AdEN = model.AdEN;
            urun.Aciklama = model.Aciklama;
            urun.AciklamaEN = model.AciklamaEN;
            urun.Fiyat = model.Fiyat;
            urun.KategoriId = model.KategoriId;
            urun.PopulerMi = model.PopulerMi;
            urun.AktifMi = model.AktifMi;
            urun.Kalori = model.Kalori;

            // FotoÄŸraf güncelleme - dosya sistemine kaydet
            if (model.Gorsel != null)
            {
                var savedPath = await SaveImageToFileAsync(model.Gorsel, urun.Id);
                if (savedPath == null)
                    return Json(new { success = false, message = "Görsel yüklenemedi. Max 2MB, sadece jpg/png/webp." });

                urun.GorselUrl = savedPath;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Ãœrün güncellendi. Id={Id}", id);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpPost("/admin/urun-sil/{id:int}")]
        public async Task<IActionResult> UrunSil(int id)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null)
                return Json(new { success = false, message = "Ãœrün bulunamadı." });

            // Aktif (tamamlanmamış) siparişlerde baÄŸlı detay varsa pasife çek
            var aktifSiparisVar = await _context.SiparisDetaylar
                .AnyAsync(sd => sd.UrunId == id
                    && sd.Siparis.Durum != SiparisDurum.Iptal
                    && sd.Siparis.Durum != SiparisDurum.TamOdendi
                    && sd.Siparis.Durum != SiparisDurum.Iade);
            if (aktifSiparisVar)
            {
                urun.AktifMi = false;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Ãœrün pasife alındı (aktif sipariş var). Id={Id}", id);
                await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
                return Json(new { success = true, message = "Ãœrün pasife alındı (aktif sipariş kayıtları var)." });
            }

            var urunSiparisTarihleri = await _context.SiparisDetaylar
                .Where(sd => sd.UrunId == id)
                .Select(sd => sd.Siparis.OlusturmaTarihi)
                .ToListAsync();
            var kilitliRaporTarihleri = urunSiparisTarihleri
                .Select(RaporTarihi)
                .Distinct()
                .ToList();
            var kilitliKayitVar = kilitliRaporTarihleri.Any()
                && await _context.GunSonuRaporlari.AnyAsync(r => kilitliRaporTarihleri.Contains(r.Tarih));
            if (kilitliKayitVar)
            {
                urun.AktifMi = false;
                await _context.SaveChangesAsync();
                await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
                return Json(new { success = true, message = "Ãœrün kapatılmış gün sonu kayıtlarında yer aldıÄŸı için silinmedi, pasife alındı." });
            }

            // FK Restrict olduÄŸu için ilişkili kayıtları temizle
            var sepetDetaylar = await _context.SepetDetaylar.Where(sd => sd.UrunId == id).ToListAsync();
            if (sepetDetaylar.Any())
                _context.SepetDetaylar.RemoveRange(sepetDetaylar);

            var siparisDetaylar = await _context.SiparisDetaylar.Where(sd => sd.UrunId == id).ToListAsync();
            if (siparisDetaylar.Any())
                _context.SiparisDetaylar.RemoveRange(siparisDetaylar);

            // UrunOpsiyonları + UrunGorseller cascade ile silinir
            _context.Urunler.Remove(urun);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ãœrün silindi. Id={Id}, Ad={Ad}", id, urun.Ad);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpPost("/admin/urun-toggle/{id:int}")]
        public async Task<IActionResult> UrunToggle(int id)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null)
                return Json(new { success = false, message = "Ãœrün bulunamadı." });

            urun.AktifMi = !urun.AktifMi;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ãœrün durumu deÄŸiştirildi. Id={Id}, AktifMi={Aktif}", id, urun.AktifMi);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, aktifMi = urun.AktifMi });
        }

        // ============================================================
        // OPSİYON CRUD
        // ============================================================

        [HttpPost("/admin/opsiyon-ekle")]
        public async Task<IActionResult> OpsiyonEkle([FromBody] OpsiyonFormViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Geçersiz veri." });

            var urun = await _context.Urunler.FindAsync(model.UrunId);
            if (urun == null)
                return Json(new { success = false, message = "Ãœrün bulunamadı." });

            // Aynı ad+grup ile opsiyon var mı kontrol et
            var mevcutOpsiyon = await _context.Opsiyonlar
                .FirstOrDefaultAsync(o => o.Ad == model.Ad && o.Grup == model.Grup);

            Opsiyon opsiyon;
            if (mevcutOpsiyon != null)
            {
                // Fiyat, zorunluluk veya İngilizce isimler deÄŸiştiyse güncelle
                if (mevcutOpsiyon.EkFiyat != model.EkFiyat || mevcutOpsiyon.Zorunlu != model.Zorunlu ||
                    mevcutOpsiyon.AdEN != model.AdEN || mevcutOpsiyon.GrupEN != model.GrupEN)
                {
                    mevcutOpsiyon.EkFiyat = model.EkFiyat;
                    mevcutOpsiyon.Zorunlu = model.Zorunlu;
                    mevcutOpsiyon.AdEN = model.AdEN;
                    mevcutOpsiyon.GrupEN = model.GrupEN;
                    await _context.SaveChangesAsync();
                }
                opsiyon = mevcutOpsiyon;
            }
            else
            {
                opsiyon = new Opsiyon
                {
                    Ad = model.Ad,
                    AdEN = model.AdEN,
                    Grup = model.Grup,
                    GrupEN = model.GrupEN,
                    EkFiyat = model.EkFiyat,
                    Zorunlu = model.Zorunlu
                };
                _context.Opsiyonlar.Add(opsiyon);
                await _context.SaveChangesAsync();
            }

            // Ãœrün-Opsiyon baÄŸlantısı zaten var mı?
            var baglanti = await _context.UrunOpsiyonlar
                .AnyAsync(uo => uo.UrunId == model.UrunId && uo.OpsiyonId == opsiyon.Id);

            if (baglanti)
                return Json(new { success = false, message = "Bu opsiyon zaten bu ürüne ekli." });

            _context.UrunOpsiyonlar.Add(new UrunOpsiyon
            {
                UrunId = model.UrunId,
                OpsiyonId = opsiyon.Id
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Opsiyon eklendi. UrunId={UrunId}, OpsiyonId={OpsiyonId}", model.UrunId, opsiyon.Id);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, opsiyonId = opsiyon.Id });
        }

        [HttpPost("/admin/opsiyon-sil/{opsiyonId:int}")]
        public async Task<IActionResult> OpsiyonSil(int opsiyonId, [FromBody] OpsiyonSilRequest? request)
        {
            var urunId = request?.UrunId ?? 0;
            if (urunId == 0)
                return Json(new { success = false, message = "Ãœrün ID gerekli." });

            var baglanti = await _context.UrunOpsiyonlar
                .FirstOrDefaultAsync(uo => uo.UrunId == urunId && uo.OpsiyonId == opsiyonId);

            if (baglanti == null)
                return Json(new { success = false, message = "Opsiyon baÄŸlantısı bulunamadı." });

            _context.UrunOpsiyonlar.Remove(baglanti);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Opsiyon kaldırıldı. UrunId={UrunId}, OpsiyonId={OpsiyonId}", urunId, opsiyonId);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpGet("/admin/opsiyon-gruplari")]
        public async Task<IActionResult> OpsiyonGruplari()
        {
            var gruplar = await _context.Opsiyonlar
                .Select(o => new { o.Grup, o.GrupEN })
                .Distinct()
                .OrderBy(o => o.Grup)
                .ToListAsync();
            return Json(gruplar);
        }

        // ============================================================
        // YARDIMCI METODLAR - FotoÄŸraf Upload (Dosya Sistemi)
        // ============================================================

        private static readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSize = 2 * 1024 * 1024; // 2MB

        /// <summary>
        /// Görseli wwwroot/uploads/urunler/ altına kaydeder, URL path döner
        /// </summary>
        private async Task<string?> SaveImageToFileAsync(IFormFile file, int urunId)
        {
            if (file.Length == 0 || file.Length > MaxFileSize)
                return null;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !_allowedExtensions.Contains(ext))
                return null;

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "urunler");
            Directory.CreateDirectory(uploadsDir);

            // Eski dosyaları temizle (farklı uzantıda olabilir)
            foreach (var oldFile in Directory.GetFiles(uploadsDir, $"{urunId}.*"))
                System.IO.File.Delete(oldFile);

            var fileName = $"{urunId}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/urunler/{fileName}";
        }

        /// <summary>
        /// Geriye dönük uyumluluk: Eski /images/urun/{id} URL'leri için
        /// DB'den serve et veya static dosyaya yönlendir
        /// </summary>
        [HttpGet("/images/urun/{id:int}")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> UrunGorsel(int id)
        {
            // Ã–nce static dosya var mı bak
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "urunler");
            var staticFiles = Directory.Exists(uploadsDir) ? Directory.GetFiles(uploadsDir, $"{id}.*") : Array.Empty<string>();
            if (staticFiles.Length > 0)
                return Redirect($"/uploads/urunler/{Path.GetFileName(staticFiles[0])}");

            // Yoksa DB'den serve et (eski veriler için)
            var gorsel = await _context.UrunGorseller
                .Where(g => g.UrunId == id)
                .Select(g => new { g.Data, g.ContentType })
                .FirstOrDefaultAsync();

            if (gorsel?.Data == null || string.IsNullOrEmpty(gorsel.ContentType))
                return NotFound();

            return File(gorsel.Data, gorsel.ContentType);
        }

        // ============================================================
        // HAPPY HOUR YÃ–NETİMİ
        // ============================================================

        [HttpGet("/admin/happy-hour")]
        public async Task<IActionResult> HappyHour()
        {
            ViewData["ActivePage"] = "HappyHour";
            ViewData["PageTitle"] = "İndirim Saatleri";
            
            var kategoriler = await _context.Kategoriler
                .Include(k => k.Urunler)
                .OrderBy(k => k.SiraNo)
                .ToListAsync();
            ViewBag.Kategoriler = kategoriler;

            ViewBag.Urunler = await _context.Urunler
                .Include(u => u.Kategori)
                .Where(u => u.AktifMi)
                .OrderBy(u => u.Kategori.SiraNo)
                .ThenBy(u => u.Ad)
                .ToListAsync();

            var happyHour = await _context.HappyHourlar
                .Include(h => h.HappyHourUrunler)
                .FirstOrDefaultAsync();

            // DB'de kayıt yoksa varsayılan model oluştur
            happyHour ??= new Core.Entities.HappyHour
            {
                BaslangicSaati = new TimeSpan(14, 0, 0),
                BitisSaati = new TimeSpan(17, 0, 0),
                IndirimOrani = 10,
                AktifMi = false
            };

            ViewBag.SeciliUrunIds = happyHour.HappyHourUrunler?.Select(x => x.UrunId).ToList() ?? new List<int>();
            return View(happyHour);
        }

        [HttpGet("/admin/happy-hour-bilgi")]
        public async Task<IActionResult> HappyHourBilgi()
        {
            var hh = await _context.HappyHourlar
                .Include(h => h.HappyHourUrunler)
                .FirstOrDefaultAsync();
            if (hh == null)
                return Json(new { aktifMi = false, indirimOrani = 0, baslaingicSaati = "", bitisSaati = "", urunIds = Array.Empty<int>() });

            var turkey = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
            var simdiki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkey).TimeOfDay;

            bool suAnAktif = false;
            if (hh.AktifMi)
            {
                if (hh.BaslangicSaati <= hh.BitisSaati)
                    suAnAktif = simdiki >= hh.BaslangicSaati && simdiki <= hh.BitisSaati;
                else
                    suAnAktif = simdiki >= hh.BaslangicSaati || simdiki <= hh.BitisSaati;
            }

            return Json(new
            {
                id = hh.Id,
                aktifMi = hh.AktifMi,
                suAnAktif,
                indirimOrani = hh.IndirimOrani,
                baslangicSaati = hh.BaslangicSaati.ToString(@"hh\:mm"),
                bitisSaati = hh.BitisSaati.ToString(@"hh\:mm"),
                urunIds = hh.HappyHourUrunler.Select(x => x.UrunId).ToArray()
            });
        }

        [HttpPost("/admin/happy-hour-kaydet")]
        public async Task<IActionResult> HappyHourKaydet([FromBody] HappyHourFormViewModel model)
        {
            var hh = await _context.HappyHourlar
                .Include(h => h.HappyHourUrunler)
                .FirstOrDefaultAsync();
            if (hh == null)
            {
                hh = new Core.Entities.HappyHour();
                _context.HappyHourlar.Add(hh);
            }

            // â”€â”€ Kapatma akışı: sadece AktifMi=false yap, geri kalanı dokunma â”€â”€
            if (!model.AktifMi)
            {
                hh.AktifMi = false;
                hh.GuncellemeTarihi = DateTime.UtcNow;

                // Saatler ve oran geldiyse güncelle (gelmediyse mevcut kalır)
                if (!string.IsNullOrWhiteSpace(model.BaslangicSaati) &&
                    TimeSpan.TryParse(model.BaslangicSaati.Trim().Replace('.', ':'), CultureInfo.InvariantCulture, out var bTs))
                    hh.BaslangicSaati = bTs;

                if (!string.IsNullOrWhiteSpace(model.BitisSaati) &&
                    TimeSpan.TryParse(model.BitisSaati.Trim().Replace('.', ':'), CultureInfo.InvariantCulture, out var btTs))
                    hh.BitisSaati = btTs;

                if (model.IndirimOrani > 0)
                    hh.IndirimOrani = model.IndirimOrani;

                // Ãœrün seçimlerini güncelle (gelirse)
                if (model.UrunIds != null)
                {
                    var yeniIds = model.UrunIds.Where(x => x > 0).Distinct().ToList();
                    _context.HappyHourUrunler.RemoveRange(hh.HappyHourUrunler);
                    hh.HappyHourUrunler.Clear();
                    foreach (var uid in yeniIds)
                        hh.HappyHourUrunler.Add(new HappyHourUrun { UrunId = uid });
                }

                await _context.SaveChangesAsync();

                // Sepetteki indirimleri kaldır (orijinal fiyata döndür)
                await UpdateSepetFiyatlariAsync(null, new List<int>());

                await _menuHub.Clients.All.SendAsync("HappyHourGuncellendi");
                _logger.LogInformation("Happy Hour kapatıldı.");
                return Json(new { success = true, message = "İndirim sistemi kapatıldı." });
            }

            // â”€â”€ Açma/Güncelleme akışı: tam validasyon â”€â”€
            if (model.IndirimOrani < 1 || model.IndirimOrani > 99)
                return Json(new { success = true, message = "İndirim oranı 1-99 arasında olmalıdır." });

            var baslangicRaw = (model.BaslangicSaati ?? string.Empty).Trim().Replace('.', ':');
            var bitisRaw = (model.BitisSaati ?? string.Empty).Trim().Replace('.', ':');

            if (string.IsNullOrWhiteSpace(baslangicRaw) || string.IsNullOrWhiteSpace(bitisRaw))
                return Json(new { success = false, message = "Başlangıç ve bitiş saatleri boş bırakılamaz." });

            if (!TimeSpan.TryParse(baslangicRaw, CultureInfo.InvariantCulture, out var baslangicTs))
                return Json(new { success = false, message = "Geçersiz başlangıç saati formatı. Örn: 14:00" });

            if (!TimeSpan.TryParse(bitisRaw, CultureInfo.InvariantCulture, out var bitisTs))
                return Json(new { success = false, message = "Geçersiz bitiş saati formatı. Örn: 17:00" });

            hh.BaslangicSaati = baslangicTs;
            hh.BitisSaati = bitisTs;
            hh.IndirimOrani = model.IndirimOrani;
            hh.AktifMi = true;
            hh.UrunId = null;
            hh.GuncellemeTarihi = DateTime.UtcNow;

            var yeniUrunIds = (model.UrunIds ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            _context.HappyHourUrunler.RemoveRange(hh.HappyHourUrunler);
            hh.HappyHourUrunler.Clear();
            foreach (var urunId in yeniUrunIds)
            {
                hh.HappyHourUrunler.Add(new HappyHourUrun
                {
                    UrunId = urunId
                });
            }

            await _context.SaveChangesAsync();

            // Sepetteki ürünlerin birim fiyatlarını güncelle
            await UpdateSepetFiyatlariAsync(hh, yeniUrunIds);

            await _menuHub.Clients.All.SendAsync("HappyHourGuncellendi");
            _logger.LogInformation("İndirim saatleri güncellendi. Aktif={Aktif}, Oran=%{Oran}, {Baslangic}-{Bitis}, UrunSayisi={UrunSayisi}",
                hh.AktifMi, hh.IndirimOrani, hh.BaslangicSaati, hh.BitisSaati, yeniUrunIds.Count);

            return Json(new { success = true, message = "İndirim ayarları kaydedildi." });
        }

        // ============================================================
        // KULLANICI YÃ–NETİMİ
        // ============================================================

        public class EnCokSatanViewModel
        {
            public string Ad { get; set; } = string.Empty;
            public int Adet { get; set; }
            public string? GorselUrl { get; set; }
            public decimal Fiyat { get; set; }
        }

        public class ZOdemeTipiViewModel
        {
            public string Tip { get; set; } = string.Empty;
            public decimal Tutar { get; set; }
            public int Adet { get; set; }
        }

        public class GunSonuExportViewModel
        {
            public DateTime Tarih { get; set; }
            public bool KapaliMi { get; set; }
            public DateTime? KapanisTarihi { get; set; }
            public decimal ToplamCiro { get; set; }
            public int SiparisSayisi { get; set; }
            public List<ZOdemeTipiViewModel> OdemeTipleri { get; set; } = new();
            public List<GunSonuExportSiparisViewModel> Siparisler { get; set; } = new();
        }

        public class GunSonuExportSiparisViewModel
        {
            public int Id { get; set; }
            public int? MasaNo { get; set; }
            public string Durum { get; set; } = string.Empty;
            public string Saat { get; set; } = string.Empty;
            public decimal ToplamTutar { get; set; }
            public string? Notlar { get; set; }
            public List<GunSonuExportDetayViewModel> Urunler { get; set; } = new();
        }

        public class GunSonuExportDetayViewModel
        {
            public string UrunAd { get; set; } = string.Empty;
            public int Adet { get; set; }
            public decimal BirimFiyat { get; set; }
            public string? Secenekler { get; set; }
        }

        public class GunSonuKapatRequest
        {
            public string? Tarih { get; set; }
        }

        [HttpGet("/admin/kullanicilar")]
        public IActionResult Kullanicilar()
        {
            ViewData["ActivePage"] = "Kullanicilar";
            ViewData["PageTitle"] = "Kullanıcı Yönetimi";
            return View();
        }

        [HttpGet("/admin/kullanici-listesi")]
        public async Task<IActionResult> KullaniciListesi()
        {
            var kullanicilar = await _userManager.Users
                .OrderBy(k => k.Rol)
                .ThenBy(k => k.UserName)
                .Select(k => new { k.Id, KullaniciAdi = k.UserName, k.AdSoyad, Rol = k.Rol.ToString(), k.AktifMi })
                .ToListAsync();

            return Json(new { success = true, data = kullanicilar });
        }

        [HttpPost("/admin/kullanici-ekle")]
        public async Task<IActionResult> KullaniciEkle([FromBody] KullaniciFormViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Geçersiz veri." });

            if (!Enum.TryParse<KullaniciRol>(model.Rol, out var rol))
                return Json(new { success = false, message = "Geçersiz rol." });

            var kullanici = new Kullanici
            {
                UserName = model.KullaniciAdi,
                AdSoyad = model.AdSoyad,
                Rol = rol,
                AktifMi = true
            };

            // Identity şifre hash'leme ve validation
            var result = await _userManager.CreateAsync(kullanici, model.Sifre);
            if (!result.Succeeded)
            {
                var hatalar = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = hatalar });
            }

            // Role ekle (Identity rol sistemi)
            await _userManager.AddToRoleAsync(kullanici, rol.ToString());

            _logger.LogInformation("Kullanıcı eklendi. Id={Id}, UserName={Ad}, Rol={Rol}",
                kullanici.Id, kullanici.UserName, kullanici.Rol);

            return Json(new { success = true, id = kullanici.Id });
        }

        [HttpPost("/admin/kullanici-guncelle/{id}")]
        public async Task<IActionResult> KullaniciGuncelle(string id, [FromBody] KullaniciGuncelleViewModel model)
        {
            var kullanici = await _userManager.FindByIdAsync(id);
            if (kullanici == null)
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });

            if (!Enum.TryParse<KullaniciRol>(model.Rol, out var rol))
                return Json(new { success = false, message = "Geçersiz rol." });

            if (kullanici.Rol == KullaniciRol.Admin)
            {
                return Json(new { success = false, message = "Güvenlik: Admin hesapları bu ekrandan güncellenemez." });
            }

            // Rol deÄŸişmişse Identity rol tablosunu da güncelle
            if (kullanici.Rol != rol)
            {
                var eskiRoller = await _userManager.GetRolesAsync(kullanici);
                await _userManager.RemoveFromRolesAsync(kullanici, eskiRoller);
                await _userManager.AddToRoleAsync(kullanici, rol.ToString());
            }

            kullanici.UserName = model.KullaniciAdi;
            kullanici.AdSoyad = model.AdSoyad;
            kullanici.Rol = rol;
            kullanici.AktifMi = model.AktifMi;

            var updateResult = await _userManager.UpdateAsync(kullanici);
            if (!updateResult.Succeeded)
            {
                var hatalar = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return Json(new { success = false, message = hatalar });
            }

            // Rol deÄŸiştirildiÄŸinde veya kullanıcı pasife alındıÄŸında, kullanıcının mevcut oturumunu anında sonlandırıyoruz (damga yenileyerek)
            await _userManager.UpdateSecurityStampAsync(kullanici);

            _logger.LogInformation("Kullanıcı güncellendi. Id={Id}, Rol={Rol}, Aktif={Aktif}", id, rol, model.AktifMi);
            return Json(new { success = true });
        }

        [HttpPost("/admin/kullanici-sifre/{id}")]
        public async Task<IActionResult> KullaniciSifreDegistir(string id, [FromBody] SifreDegistirViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.YeniSifre) || model.YeniSifre.Length < 6)
                return Json(new { success = false, message = "Şifre en az 6 karakter olmalıdır." });

            var kullanici = await _userManager.FindByIdAsync(id);
            if (kullanici == null)
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });

            if (kullanici.Rol == KullaniciRol.Admin)
                return Json(new { success = false, message = "Güvenlik: Admin şifreleri bu ekrandan deÄŸiştirilemez." });

            // Identity ile şifre sıfırla (hash'leme otomatik)
            var token = await _userManager.GeneratePasswordResetTokenAsync(kullanici);
            var result = await _userManager.ResetPasswordAsync(kullanici, token, model.YeniSifre);

            if (!result.Succeeded)
            {
                var hatalar = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = hatalar });
            }

            _logger.LogInformation("Şifre deÄŸiştirildi. KullaniciId={Id}", id);
            return Json(new { success = true });
        }

        [HttpPost("/admin/kullanici-sil/{id}")]
        public async Task<IActionResult> KullaniciSil(string id)
        {
            var kullanici = await _userManager.FindByIdAsync(id);
            if (kullanici == null)
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });

            // Kendini sileme kontrolü
            var mevcutKullaniciId = _userManager.GetUserId(User);
            if (mevcutKullaniciId == id)
                return Json(new { success = false, message = "Kendinizi silemezsiniz." });

            // Admin kontrolü
            if (kullanici.Rol == KullaniciRol.Admin)
            {
                return Json(new { success = false, message = "Güvenlik: Admin hesapları silinemez." });
            }

            // Hard delete (Identity)
            var result = await _userManager.DeleteAsync(kullanici);
            if (!result.Succeeded)
                return Json(new { success = false, message = "Silme işlemi başarısız." });

            _logger.LogInformation("Kullanıcı silindi. Id={Id}, UserName={Ad}", id, kullanici.UserName);
            return Json(new { success = true });
        }

        [HttpPost("/admin/kullanici-toggle/{id}")]
        public async Task<IActionResult> KullaniciToggle(string id)
        {
            var kullanici = await _userManager.FindByIdAsync(id);
            if (kullanici == null)
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });

            // Admin pasife alınamaz (tamamen yasaklandı)
            if (kullanici.Rol == KullaniciRol.Admin)
            {
                return Json(new { success = false, message = "Güvenlik: Admin hesaplarının durumu deÄŸiştirilemez." });
            }

            kullanici.AktifMi = !kullanici.AktifMi;
            await _userManager.UpdateAsync(kullanici);

            // Damgayı yenile, böylece hesap pasife alındıÄŸında kullanıcı anında sistemden çıkarılsın
            await _userManager.UpdateSecurityStampAsync(kullanici);

            _logger.LogInformation("Kullanıcı durumu deÄŸiştirildi. Id={Id}, AktifMi={AktifMi}", id, kullanici.AktifMi);
            return Json(new { success = true, aktifMi = kullanici.AktifMi });
        }

        // ============================================================
        // YARDIMCI: Sepet Fiyat Güncelleme
        // ============================================================

        /// <summary>
        /// Happy Hour kaydedildiÄŸinde sepetteki ürünlerin BirimFiyatını günceller.
        /// happyHour null ise indirim kaldırılmış demektir â€” orijinal fiyata döner.
        /// </summary>
        private async Task<GunSonuExportViewModel> BuildGunSonuExportModelAsync(string? tarih)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz);
            var gun = ParseGunTarihi(tarih, now);
            var startDateUtc = TimeZoneInfo.ConvertTimeToUtc(gun, _turkeyTz);
            var endDateUtc = TimeZoneInfo.ConvertTimeToUtc(gun.AddDays(1), _turkeyTz);
            var raporTarihi = DateTime.SpecifyKind(gun, DateTimeKind.Utc);

            var siparisler = await _context.Siparisler
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Where(s => s.OlusturmaTarihi >= startDateUtc && s.OlusturmaTarihi < endDateUtc)
                .Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.Iade)
                .OrderBy(s => s.OlusturmaTarihi)
                .AsSplitQuery()
                .ToListAsync();

            var odemeTipleri = await _context.Odemeler
                .Where(o => o.OdemeTarihi >= startDateUtc && o.OdemeTarihi < endDateUtc)
                .GroupBy(o => o.OdemeTipi)
                .Select(g => new ZOdemeTipiViewModel
                {
                    Tip = g.Key.ToString(),
                    Tutar = g.Sum(o => o.Tutar),
                    Adet = g.Count()
                })
                .OrderByDescending(x => x.Tutar)
                .ToListAsync();

            var gunSonuRapor = await _context.GunSonuRaporlari
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Tarih == raporTarihi);

            return new GunSonuExportViewModel
            {
                Tarih = gun,
                KapaliMi = gunSonuRapor != null,
                KapanisTarihi = gunSonuRapor?.KapanisTarihi,
                ToplamCiro = gunSonuRapor?.ToplamCiro ?? odemeTipleri.Sum(x => x.Tutar),
                SiparisSayisi = gunSonuRapor?.SiparisSayisi ?? siparisler.Count,
                OdemeTipleri = gunSonuRapor != null
                    ? JsonSerializer.Deserialize<List<ZOdemeTipiViewModel>>(gunSonuRapor.OdemeTipleriJson) ?? new List<ZOdemeTipiViewModel>()
                    : odemeTipleri,
                Siparisler = siparisler.Select(s => new GunSonuExportSiparisViewModel
                {
                    Id = s.Id,
                    MasaNo = s.Masa?.MasaNo,
                    Durum = s.Durum.ToString(),
                    Saat = ToTurkeyTime(s.OlusturmaTarihi),
                    ToplamTutar = s.ToplamTutar,
                    Notlar = s.Notlar,
                    Urunler = s.SiparisDetaylar.Select(sd => new GunSonuExportDetayViewModel
                    {
                        UrunAd = sd.Urun.Ad,
                        Adet = sd.Adet,
                        BirimFiyat = sd.BirimFiyat,
                        Secenekler = ParseOpsiyonOzet(sd.SeciliOpsiyonlar)
                    }).ToList()
                }).ToList()
            };
        }

        private static byte[] CreateGunSonuPdf(GunSonuExportViewModel model)
        {
            using var stream = new MemoryStream();
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            var pdfFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);

            document.SetMargins(24, 24, 24, 24);
            document.SetFont(pdfFont);

            document.Add(new Paragraph("Gün Sonu Z Raporu").SetFontSize(20).SetBold());
            document.Add(new Paragraph($"{model.Tarih:dd.MM.yyyy} tarihli satış özeti")
                .SetFontSize(11)
                .SetFontColor(ColorConstants.DARK_GRAY));

            if (model.KapanisTarihi.HasValue)
            {
                document.Add(new Paragraph($"Kapanış: {ToTurkeyTime(model.KapanisTarihi.Value)}")
                    .SetFontSize(10)
                    .SetFontColor(ColorConstants.GRAY));
            }

            var ozetTablo = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1, 1 })).UseAllAvailableWidth();
            ozetTablo.AddHeaderCell("Toplam Ciro");
            ozetTablo.AddHeaderCell("Sipariş Sayısı");
            ozetTablo.AddHeaderCell("Toplam Ürün");
            ozetTablo.AddCell($"₺{model.ToplamCiro:N2}");
            ozetTablo.AddCell(model.SiparisSayisi.ToString());
            ozetTablo.AddCell(model.Siparisler.Sum(x => x.Urunler.Sum(y => y.Adet)).ToString());
            document.Add(new Paragraph(" "));
            document.Add(ozetTablo);

            document.Add(new Paragraph("Ödeme Dağılımı").SetFontSize(14).SetBold().SetMarginTop(18));
            var odemeTablo = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 1 })).UseAllAvailableWidth();
            odemeTablo.AddHeaderCell("Ödeme Tipi");
            odemeTablo.AddHeaderCell("Adet");
            odemeTablo.AddHeaderCell("Tutar");
            foreach (var odeme in model.OdemeTipleri)
            {
                odemeTablo.AddCell(odeme.Tip);
                odemeTablo.AddCell(odeme.Adet.ToString());
                odemeTablo.AddCell($"₺{odeme.Tutar:N2}");
            }
            if (!model.OdemeTipleri.Any())
            {
                odemeTablo.AddCell(new Cell(1, 3).Add(new Paragraph("Ödeme kaydı yok.")));
            }
            document.Add(odemeTablo);

            document.Add(new Paragraph("Detaylı Günlük Satış Dökümü").SetFontSize(14).SetBold().SetMarginTop(18));
            var detayTablo = new Table(UnitValue.CreatePercentArray(new float[] { 1.2f, 1f, 1.1f, 4.2f, 1.2f })).UseAllAvailableWidth();
            detayTablo.AddHeaderCell("Saat");
            detayTablo.AddHeaderCell("Masa");
            detayTablo.AddHeaderCell("Sipariş");
            detayTablo.AddHeaderCell("Ürünler");
            detayTablo.AddHeaderCell("Tutar");

            foreach (var siparis in model.Siparisler)
            {
                var urunMetni = string.Join("\n", siparis.Urunler.Select(u =>
                    $"{u.Adet}x {u.UrunAd}" +
                    (string.IsNullOrWhiteSpace(u.Secenekler) ? string.Empty : $" ({u.Secenekler})")));

                if (!string.IsNullOrWhiteSpace(siparis.Notlar))
                {
                    urunMetni += $"\nNot: {siparis.Notlar}";
                }

                detayTablo.AddCell(siparis.Saat);
                detayTablo.AddCell(siparis.MasaNo?.ToString() ?? "-");
                detayTablo.AddCell($"#{siparis.Id}\n{siparis.Durum}");
                detayTablo.AddCell(urunMetni);
                detayTablo.AddCell($"₺{siparis.ToplamTutar:N2}");
            }

            if (!model.Siparisler.Any())
            {
                detayTablo.AddCell(new Cell(1, 5).Add(new Paragraph("Seçilen gün için satış bulunamadı.")));
            }

            document.Add(detayTablo);
            document.Close();
            return stream.ToArray();
        }

        private static byte[] CreateGunSonuExcel(GunSonuExportViewModel model)
        {
            using var workbook = new XLWorkbook();

            var ozet = workbook.Worksheets.Add("Z Raporu");
            ozet.Cell(1, 1).Value = "Gun Sonu Z Raporu";
            ozet.Cell(2, 1).Value = "Tarih";
            ozet.Cell(2, 2).Value = model.Tarih;
            ozet.Cell(3, 1).Value = "Toplam Ciro";
            ozet.Cell(3, 2).Value = model.ToplamCiro;
            ozet.Cell(4, 1).Value = "Siparis Sayisi";
            ozet.Cell(4, 2).Value = model.SiparisSayisi;
            ozet.Cell(5, 1).Value = "Toplam Urun";
            ozet.Cell(5, 2).Value = model.Siparisler.Sum(x => x.Urunler.Sum(y => y.Adet));
            ozet.Cell(7, 1).Value = "Odeme Tipi";
            ozet.Cell(7, 2).Value = "Adet";
            ozet.Cell(7, 3).Value = "Tutar";

            var odemeSatir = 8;
            foreach (var odeme in model.OdemeTipleri)
            {
                ozet.Cell(odemeSatir, 1).Value = odeme.Tip;
                ozet.Cell(odemeSatir, 2).Value = odeme.Adet;
                ozet.Cell(odemeSatir, 3).Value = odeme.Tutar;
                odemeSatir++;
            }

            var detay = workbook.Worksheets.Add("Detayli Satislar");
            detay.Cell(1, 1).Value = "Siparis No";
            detay.Cell(1, 2).Value = "Saat";
            detay.Cell(1, 3).Value = "Masa";
            detay.Cell(1, 4).Value = "Durum";
            detay.Cell(1, 5).Value = "Urun";
            detay.Cell(1, 6).Value = "Adet";
            detay.Cell(1, 7).Value = "Birim Fiyat";
            detay.Cell(1, 8).Value = "Satir Toplami";
            detay.Cell(1, 9).Value = "Secenekler";
            detay.Cell(1, 10).Value = "Siparis Notu";

            var detaySatir = 2;
            foreach (var siparis in model.Siparisler)
            {
                foreach (var urun in siparis.Urunler)
                {
                    detay.Cell(detaySatir, 1).Value = siparis.Id;
                    detay.Cell(detaySatir, 2).Value = siparis.Saat;
                    detay.Cell(detaySatir, 3).Value = siparis.MasaNo?.ToString() ?? "-";
                    detay.Cell(detaySatir, 4).Value = siparis.Durum;
                    detay.Cell(detaySatir, 5).Value = urun.UrunAd;
                    detay.Cell(detaySatir, 6).Value = urun.Adet;
                    detay.Cell(detaySatir, 7).Value = urun.BirimFiyat;
                    detay.Cell(detaySatir, 8).Value = urun.Adet * urun.BirimFiyat;
                    detay.Cell(detaySatir, 9).Value = urun.Secenekler ?? string.Empty;
                    detay.Cell(detaySatir, 10).Value = siparis.Notlar ?? string.Empty;
                    detaySatir++;
                }
            }

            ozet.Range(1, 1, 1, 3).Merge().Style.Font.SetBold();
            ozet.Range(7, 1, 7, 3).Style.Font.SetBold();
            detay.Range(1, 1, 1, 10).Style.Font.SetBold();

            ozet.Cell(2, 2).Style.DateFormat.Format = "dd.MM.yyyy";
            ozet.Cell(3, 2).Style.NumberFormat.Format = "#,##0.00";
            ozet.Cell(4, 2).Style.NumberFormat.Format = "0";
            ozet.Cell(5, 2).Style.NumberFormat.Format = "0";

            if (odemeSatir > 8)
            {
                ozet.Range(8, 2, odemeSatir - 1, 2).Style.NumberFormat.Format = "0";
                ozet.Range(8, 3, odemeSatir - 1, 3).Style.NumberFormat.Format = "#,##0.00";
            }

            if (detaySatir > 2)
            {
                detay.Range(2, 1, detaySatir - 1, 1).Style.NumberFormat.Format = "0";
                detay.Range(2, 6, detaySatir - 1, 6).Style.NumberFormat.Format = "0";
                detay.Range(2, 7, detaySatir - 1, 8).Style.NumberFormat.Format = "#,##0.00";
            }

            foreach (var ws in workbook.Worksheets)
            {
                ws.Columns().AdjustToContents();
                ws.Rows().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static string? ParseOpsiyonOzet(string? seciliOpsiyonlar)
        {
            if (string.IsNullOrWhiteSpace(seciliOpsiyonlar) || seciliOpsiyonlar == "[]")
                return null;

            try
            {
                using var doc = JsonDocument.Parse(seciliOpsiyonlar);
                return string.Join(", ", doc.RootElement.EnumerateArray()
                    .Select(x => x.TryGetProperty("Ad", out var ad) ? ad.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            }
            catch
            {
                return null;
            }
        }

        private async Task UpdateSepetFiyatlariAsync(Core.Entities.HappyHour? happyHour, List<int> etkilenenUrunIds)
        {
            // Tüm aktif sepet detaylarını çek (ürün fiyatı ile birlikte)
            var sepetDetaylar = await _context.SepetDetaylar
                .Include(sd => sd.Urun)
                .ToListAsync();

            bool herhangiGuncellendi = false;

            foreach (var detay in sepetDetaylar)
            {
                var urun = detay.Urun;
                if (urun == null) continue;

                // Opsiyonlardan ek fiyat hesapla
                decimal opsiyonEkFiyat = 0;
                if (!string.IsNullOrEmpty(detay.SeciliOpsiyonlar) && detay.SeciliOpsiyonlar != "[]")
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(detay.SeciliOpsiyonlar);
                        foreach (var ops in doc.RootElement.EnumerateArray())
                            opsiyonEkFiyat += ops.GetProperty("EkFiyat").GetDecimal();
                    }
                    catch { }
                }

                decimal yeniBirimFiyat;

                if (happyHour != null && happyHour.IndirimOrani > 0 &&
                    (!etkilenenUrunIds.Any() || etkilenenUrunIds.Contains(detay.UrunId)))
                {
                    // İndirimli fiyat
                    var indirimliFiyat = Math.Round(urun.Fiyat * (1 - happyHour.IndirimOrani / 100m), 2);
                    yeniBirimFiyat = indirimliFiyat + opsiyonEkFiyat;
                }
                else
                {
                    // İndirim yok â€” orijinal fiyat
                    yeniBirimFiyat = urun.Fiyat + opsiyonEkFiyat;
                }

                if (detay.BirimFiyat != yeniBirimFiyat)
                {
                    detay.BirimFiyat = yeniBirimFiyat;
                    herhangiGuncellendi = true;
                }
            }

            if (!herhangiGuncellendi) return;

            // Sepet toplamlarını da güncelle
            var etkilenenSepetler = sepetDetaylar
                .Where(sd => sd.Urun != null)
                .GroupBy(sd => sd.SepetId);

            foreach (var grup in etkilenenSepetler)
            {
                var sepet = await _context.Sepetler
                    .Include(s => s.SepetDetaylar)
                    .FirstOrDefaultAsync(s => s.Id == grup.Key);
                if (sepet != null)
                    sepet.ToplamTutar = sepet.SepetDetaylar.Sum(sd => sd.BirimFiyat * sd.Adet);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Sepet fiyatları Happy Hour deÄŸişikliÄŸine göre güncellendi.");
        }
    }

    public class OpsiyonSilRequest
    {
        public int UrunId { get; set; }
    }

    public class AdminDurumRequest
    {
        public string YeniDurum { get; set; } = "";
    }
}

