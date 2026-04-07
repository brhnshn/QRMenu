using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
        private static string ToTurkeyTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).ToString("dd.MM.yyyy HH:mm");
        private static string? ToTurkeyTime(DateTime? utc) =>
            utc.HasValue ? ToTurkeyTime(utc.Value) : null;

        private readonly QRMenuDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly ISiparisService _siparisService;
        private readonly IHubContext<MenuHub> _menuHub;
        private readonly UserManager<Kullanici> _userManager;

        public AdminController(
            QRMenuDbContext context,
            ILogger<AdminController> logger,
            IWebHostEnvironment env,
            ISiparisService siparisService,
            IHubContext<MenuHub> menuHub,
            UserManager<Kullanici> userManager)
        {
            _context = context;
            _logger = logger;
            _env = env;
            _siparisService = siparisService;
            _menuHub = menuHub;
            _userManager = userManager;
        }

        // Admin ana sayfa → masalara yönlendir
        [HttpGet("/admin")]
        public IActionResult Index() => RedirectToAction("Masalar");

        // Masa yönetimi sayfası
        [HttpGet("/admin/masalar")]
        public async Task<IActionResult> Masalar()
        {
            var masalar = await _context.Masalar
                .Where(m => m.AktifMi)
                .Include(m => m.Siparisler.Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.TamOdendi && s.Durum != SiparisDurum.Iade))
                .OrderBy(m => m.MasaNo)
                .ToListAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var liste = masalar.Select(m => new QrKodViewModel
            {
                MasaNo = m.MasaNo,
                QrUrl = m.QrKodUrl ?? "",
                QrBase64 = "",
                DoluMu = m.Siparisler.Any()
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

        // ============================================================
        // SİPARİŞ YÖNETİMİ
        // ============================================================

        [HttpGet("/admin/siparisler")]
        public async Task<IActionResult> Siparisler()
        {
            ViewData["ActivePage"] = "Siparisler";
            ViewData["PageTitle"] = "Sipariş Yönetimi";

            var siparisler = await _context.Siparisler
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .OrderByDescending(s => s.OlusturmaTarihi)
                .ToListAsync();

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

                await _menuHub.Clients.All.SendAsync("SiparisGuncellendi");

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
                detayOzet = string.Join(", ", s.SiparisDetaylar.Select(sd => $"{sd.Adet}× {sd.Urun.Ad}"))
            }));
        }

        // ============================================================
        // ÜRÜN YÖNETİMİ SAYFASI
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
                return Json(new { success = false, message = $"Bu kategoride {kategori.Urunler.Count} ürün var. Önce ürünleri taşıyın veya silin." });

            _context.Kategoriler.Remove(kategori);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Kategori silindi. Id={Id}, Ad={Ad}", id, kategori.Ad);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        // ============================================================
        // ÜRÜN CRUD
        // ============================================================

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
                return Json(new { success = false, message = "Ürün bulunamadı." });

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
                        uo.Opsiyon.Grup,
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

            // Fotoğraf upload — dosya sistemine kaydet
            if (model.Gorsel != null)
            {
                var savedPath = await SaveImageToFileAsync(model.Gorsel, urun.Id);
                if (savedPath == null)
                    return Json(new { success = false, message = "Görsel yüklenemedi. Max 2MB, sadece jpg/png/webp." });

                urun.GorselUrl = savedPath;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Ürün eklendi. Id={Id}, Ad={Ad}", urun.Id, urun.Ad);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, id = urun.Id });
        }

        [HttpPost("/admin/urun-guncelle/{id:int}")]
        public async Task<IActionResult> UrunGuncelle(int id, [FromForm] UrunFormViewModel model)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null)
                return Json(new { success = false, message = "Ürün bulunamadı." });

            urun.Ad = model.Ad;
            urun.AdEN = model.AdEN;
            urun.Aciklama = model.Aciklama;
            urun.AciklamaEN = model.AciklamaEN;
            urun.Fiyat = model.Fiyat;
            urun.KategoriId = model.KategoriId;
            urun.PopulerMi = model.PopulerMi;
            urun.AktifMi = model.AktifMi;
            urun.Kalori = model.Kalori;

            // Fotoğraf güncelleme — dosya sistemine kaydet
            if (model.Gorsel != null)
            {
                var savedPath = await SaveImageToFileAsync(model.Gorsel, urun.Id);
                if (savedPath == null)
                    return Json(new { success = false, message = "Görsel yüklenemedi. Max 2MB, sadece jpg/png/webp." });

                urun.GorselUrl = savedPath;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Ürün güncellendi. Id={Id}", id);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpPost("/admin/urun-sil/{id:int}")]
        public async Task<IActionResult> UrunSil(int id)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null)
                return Json(new { success = false, message = "Ürün bulunamadı." });

            // Aktif (iptal/iade olmayan) siparişlerde bağlı detay varsa pasife çek
            var aktifSiparisVar = await _context.SiparisDetaylar
                .AnyAsync(sd => sd.UrunId == id
                    && sd.Siparis.Durum != SiparisDurum.Iptal
                    && sd.Siparis.Durum != SiparisDurum.Iade);
            if (aktifSiparisVar)
            {
                urun.AktifMi = false;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Ürün pasife alındı (aktif sipariş var). Id={Id}", id);
                await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
                return Json(new { success = true, message = "Ürün pasife alındı (aktif sipariş kayıtları var)." });
            }

            // FK Restrict olduğu için ilişkili kayıtları temizle
            var sepetDetaylar = await _context.SepetDetaylar.Where(sd => sd.UrunId == id).ToListAsync();
            if (sepetDetaylar.Any())
                _context.SepetDetaylar.RemoveRange(sepetDetaylar);

            var siparisDetaylar = await _context.SiparisDetaylar.Where(sd => sd.UrunId == id).ToListAsync();
            if (siparisDetaylar.Any())
                _context.SiparisDetaylar.RemoveRange(siparisDetaylar);

            // UrunOpsiyonları + UrunGorseller cascade ile silinir
            _context.Urunler.Remove(urun);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ürün silindi. Id={Id}, Ad={Ad}", id, urun.Ad);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpPost("/admin/urun-toggle/{id:int}")]
        public async Task<IActionResult> UrunToggle(int id)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null)
                return Json(new { success = false, message = "Ürün bulunamadı." });

            urun.AktifMi = !urun.AktifMi;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ürün durumu değiştirildi. Id={Id}, AktifMi={Aktif}", id, urun.AktifMi);
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
                return Json(new { success = false, message = "Ürün bulunamadı." });

            // Aynı ad+grup ile opsiyon var mı kontrol et
            var mevcutOpsiyon = await _context.Opsiyonlar
                .FirstOrDefaultAsync(o => o.Ad == model.Ad && o.Grup == model.Grup);

            Opsiyon opsiyon;
            if (mevcutOpsiyon != null)
            {
                // Fiyat veya zorunluluk değiştiyse güncelle
                if (mevcutOpsiyon.EkFiyat != model.EkFiyat || mevcutOpsiyon.Zorunlu != model.Zorunlu)
                {
                    mevcutOpsiyon.EkFiyat = model.EkFiyat;
                    mevcutOpsiyon.Zorunlu = model.Zorunlu;
                    await _context.SaveChangesAsync();
                }
                opsiyon = mevcutOpsiyon;
            }
            else
            {
                opsiyon = new Opsiyon
                {
                    Ad = model.Ad,
                    Grup = model.Grup,
                    EkFiyat = model.EkFiyat,
                    Zorunlu = model.Zorunlu
                };
                _context.Opsiyonlar.Add(opsiyon);
                await _context.SaveChangesAsync();
            }

            // Ürün-Opsiyon bağlantısı zaten var mı?
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
                return Json(new { success = false, message = "Ürün ID gerekli." });

            var baglanti = await _context.UrunOpsiyonlar
                .FirstOrDefaultAsync(uo => uo.UrunId == urunId && uo.OpsiyonId == opsiyonId);

            if (baglanti == null)
                return Json(new { success = false, message = "Opsiyon bağlantısı bulunamadı." });

            _context.UrunOpsiyonlar.Remove(baglanti);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Opsiyon kaldırıldı. UrunId={UrunId}, OpsiyonId={OpsiyonId}", urunId, opsiyonId);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        // ============================================================
        // YARDIMCI METODLAR — Fotoğraf Upload (Dosya Sistemi)
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
            // Önce static dosya var mı bak
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
        // HAPPY HOUR YÖNETİMİ
        // ============================================================

        [HttpGet("/admin/happy-hour")]
        public async Task<IActionResult> HappyHour()
        {
            ViewData["ActivePage"] = "HappyHour";
            ViewData["PageTitle"] = "Happy Hour Yönetimi";
            ViewBag.Urunler = await _context.Urunler.Where(u => u.AktifMi).OrderBy(u => u.Ad).ToListAsync();
            var happyHour = await _context.HappyHourlar.FirstOrDefaultAsync();
            return View(happyHour);
        }

        [HttpGet("/admin/happy-hour-bilgi")]
        public async Task<IActionResult> HappyHourBilgi()
        {
            var hh = await _context.HappyHourlar.FirstOrDefaultAsync();
            if (hh == null)
                return Json(new { aktifMi = false, indirimOrani = 0, baslaingicSaati = "", bitisSaati = "", urunId = (int?)null });

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
                baslaingicSaati = hh.BaslangicSaati.ToString(@"hh\:mm"),
                bitisSaati = hh.BitisSaati.ToString(@"hh\:mm"),
                urunId = hh.UrunId
            });
        }

        [HttpPost("/admin/happy-hour-kaydet")]
        public async Task<IActionResult> HappyHourKaydet([FromBody] HappyHourFormViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Geçersiz veri." });

            var hh = await _context.HappyHourlar.FirstOrDefaultAsync();
            if (hh == null)
            {
                hh = new Core.Entities.HappyHour();
                _context.HappyHourlar.Add(hh);
            }

            if (!TimeSpan.TryParseExact(model.BaslangicSaati, @"hh\:mm", null, out var baslaingicTs))
                return Json(new { success = false, message = "Geçersiz başlangıç saati formatı. Örn: 14:00" });

            if (!TimeSpan.TryParseExact(model.BitisSaati, @"hh\:mm", null, out var bitisTs))
                return Json(new { success = false, message = "Geçersiz bitiş saati formatı. Örn: 17:00" });

            hh.BaslangicSaati = baslaingicTs;
            hh.BitisSaati = bitisTs;
            hh.IndirimOrani = model.IndirimOrani;
            hh.AktifMi = model.AktifMi;
            hh.UrunId = model.UrunId;
            hh.GuncellemeTarihi = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            
            // SignalR ile canlı yayını (Müşteri ekranlarına gönder)
            await _menuHub.Clients.All.SendAsync("HappyHourGuncellendi");
            _logger.LogInformation("Happy Hour güncellendi. Aktif={Aktif}, Oran=%{Oran}, {Baslangic}-{Bitis}",
                hh.AktifMi, hh.IndirimOrani, hh.BaslangicSaati, hh.BitisSaati);

            return Json(new { success = true });
        }

        // ============================================================
        // KULLANICI YÖNETİMİ
        // ============================================================

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

            // Admin rolden düşürme kontrolü (Koşulsuz yasak)
            if (kullanici.Rol == KullaniciRol.Admin && rol != KullaniciRol.Admin)
            {
                return Json(new { success = false, message = "Yönetici (Admin) rolü alt rollere düşürülemez!" });
            }

            // Rol değişmişse Identity rol tablosunu da güncelle
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

            // Identity ile şifre sıfırla (hash'leme otomatik)
            var token = await _userManager.GeneratePasswordResetTokenAsync(kullanici);
            var result = await _userManager.ResetPasswordAsync(kullanici, token, model.YeniSifre);

            if (!result.Succeeded)
            {
                var hatalar = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = hatalar });
            }

            _logger.LogInformation("Şifre değiştirildi. KullaniciId={Id}", id);
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

            // Son admin kontrolü
            if (kullanici.Rol == KullaniciRol.Admin)
            {
                var adminSayisi = await _userManager.Users
                    .CountAsync(k => k.Rol == KullaniciRol.Admin && k.AktifMi);
                if (adminSayisi <= 1)
                    return Json(new { success = false, message = "Son admin silinemez." });
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

            // Son admin pasife alınamaz
            if (kullanici.Rol == KullaniciRol.Admin && kullanici.AktifMi)
            {
                var adminSayisi = await _userManager.Users
                    .CountAsync(k => k.Rol == KullaniciRol.Admin && k.AktifMi);
                if (adminSayisi <= 1)
                    return Json(new { success = false, message = "Üzgünüz, sistemde sadece bir aktif Admin var ve bu kullanıcı pasife alınamaz." });
            }

            kullanici.AktifMi = !kullanici.AktifMi;
            await _userManager.UpdateAsync(kullanici);

            _logger.LogInformation("Kullanıcı durumu değiştirildi. Id={Id}, AktifMi={AktifMi}", id, kullanici.AktifMi);
            return Json(new { success = true, aktifMi = kullanici.AktifMi });
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
