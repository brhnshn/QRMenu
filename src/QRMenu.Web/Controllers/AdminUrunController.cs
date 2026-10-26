using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Web.Hubs;
using QRMenu.Web.ViewModels;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireAdmin")]
    public class AdminUrunController : Controller
    {
        private static readonly System.Collections.Generic.HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSize = 2 * 1024 * 1024; // 2MB

        private readonly IUrunYonetimService _urunService;
        private readonly IHubContext<OrderHub> _menuHub;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AdminUrunController> _logger;

        public AdminUrunController(
            IUrunYonetimService urunService,
            IHubContext<OrderHub> menuHub,
            IWebHostEnvironment env,
            ILogger<AdminUrunController> logger)
        {
            _urunService = urunService;
            _menuHub = menuHub;
            _env = env;
            _logger = logger;
        }

        [HttpGet("/admin/urunler")]
        public async Task<IActionResult> Urunler()
        {
            ViewData["ActivePage"] = "Urunler";
            ViewData["PageTitle"] = "Ürün & Kategori Yönetimi";

            var kategoriler = await _urunService.GetKategorilerAsync();
            var allKategorilerWithUrunler = await _urunService.GetKategorilerWithUrunlerAsync();
            var urunler = allKategorilerWithUrunler
                .SelectMany(k => k.Urunler)
                .OrderBy(u => u.Kategori?.SiraNo ?? 0)
                .ThenBy(u => u.Ad)
                .ToList();

            ViewBag.Kategoriler = kategoriler;
            return View("~/Views/Admin/Urunler.cshtml", urunler);
        }

        [HttpGet("/admin/kategoriler")]
        public async Task<IActionResult> KategoriListesi()
        {
            var kategoriler = await _urunService.GetKategorilerAsync();
            var result = kategoriler.Select(k => new
            {
                k.Id,
                k.Ad,
                k.AdEN,
                k.SiraNo,
                k.AktifMi,
                UrunSayisi = k.Urunler?.Count ?? 0
            });
            return Json(result);
        }

        [HttpPost("/admin/kategori-ekle")]
        public async Task<IActionResult> KategoriEkle([FromBody] KategoriFormViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Geçersiz veri." });

            var kategori = await _urunService.KategoriEkleAsync(model.Ad, model.AdEN, model.SiraNo);
            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, id = kategori.Id });
        }

        [HttpPost("/admin/kategori-guncelle/{id:int}")]
        public async Task<IActionResult> KategoriGuncelle(int id, [FromBody] KategoriFormViewModel model)
        {
            var updated = await _urunService.KategoriGuncelleAsync(id, model.Ad, model.AdEN, model.SiraNo);
            if (!updated)
                return Json(new { success = false, message = "Kategori bulunamadı." });

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpPost("/admin/kategori-sil/{id:int}")]
        public async Task<IActionResult> KategoriSil(int id)
        {
            var (success, errorMessage) = await _urunService.KategoriSilAsync(id);
            if (!success)
                return Json(new { success = false, message = errorMessage });

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpPost("/admin/urun-tasi")]
        public async Task<IActionResult> UrunTasi([FromBody] UrunTasiViewModel model)
        {
            var (success, errorMessage, count) = await _urunService.UrunTasiAsync(model.UrunIds, model.YeniKategoriId);
            if (!success)
                return Json(new { success = false, message = errorMessage });

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, tasinanAdet = count });
        }

        [HttpGet("/admin/urun-detay/{id:int}")]
        public async Task<IActionResult> UrunDetay(int id)
        {
            var urun = await _urunService.GetUrunDetayAsync(id);
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
                    urun.StokAdet,
                    urun.AdminManuelPasifMi,
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
                Kalori = model.Kalori
            };

            await _urunService.UrunEkleAsync(urun, model.StokAdet, model.AktifMi);

            if (model.Gorsel != null)
            {
                var savedPath = await SaveImageToFileAsync(model.Gorsel, urun.Id);
                if (savedPath == null)
                    return Json(new { success = false, message = "Görsel yüklenemedi. Max 2MB, sadece jpg/png/webp." });

                await _urunService.SaveUrunGorselPathAsync(urun.Id, savedPath);
            }

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, id = urun.Id });
        }

        [HttpPost("/admin/urun-guncelle/{id:int}")]
        public async Task<IActionResult> UrunGuncelle(int id, [FromForm] UrunFormViewModel model)
        {
            string? savedGorselPath = null;
            if (model.Gorsel != null)
            {
                savedGorselPath = await SaveImageToFileAsync(model.Gorsel, id);
                if (savedGorselPath == null)
                    return Json(new { success = false, message = "Görsel yüklenemedi. Max 2MB, sadece jpg/png/webp." });
            }

            var (success, errorMessage) = await _urunService.UrunGuncelleAsync(id, u =>
            {
                u.Ad = model.Ad;
                u.AdEN = model.AdEN;
                u.Aciklama = model.Aciklama;
                u.AciklamaEN = model.AciklamaEN;
                u.Fiyat = model.Fiyat;
                u.KategoriId = model.KategoriId;
                u.PopulerMi = model.PopulerMi;
                u.Kalori = model.Kalori;
                if (savedGorselPath != null)
                {
                    u.GorselUrl = savedGorselPath;
                }
            }, model.StokAdet, model.AktifMi);

            if (!success)
                return Json(new { success = false, message = errorMessage });

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpPost("/admin/urun-sil/{id:int}")]
        public async Task<IActionResult> UrunSil(int id)
        {
            var (success, message) = await _urunService.UrunSilAsync(id);
            if (!success)
                return Json(new { success = false, message });

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, message });
        }

        [HttpPost("/admin/urun-toggle/{id:int}")]
        public async Task<IActionResult> UrunToggle(int id)
        {
            var (success, errorMessage, aktifMi) = await _urunService.UrunToggleAsync(id);
            if (!success)
                return Json(new { success = false, message = errorMessage });

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, aktifMi });
        }

        [HttpPost("/admin/urun-stok-guncelle/{id:int}")]
        public async Task<IActionResult> UrunStokGuncelle(int id, [FromBody] AdminController.UrunStokGuncelleRequest request)
        {
            var (success, errorMessage, sonuc) = await _urunService.UrunStokGuncelleAsync(id, request.StokAdet);
            if (!success || sonuc == null)
                return Json(new { success = false, message = errorMessage });

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new
            {
                success = true,
                stokAdet = sonuc.StokAdet,
                aktifMi = sonuc.AktifMi,
                adminManuelPasifMi = sonuc.AdminManuelPasifMi
            });
        }

        [HttpPost("/admin/opsiyon-ekle")]
        public async Task<IActionResult> OpsiyonEkle([FromBody] OpsiyonFormViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Geçersiz veri." });

            var (success, errorMessage, opsiyonId) = await _urunService.OpsiyonEkleAsync(
                model.UrunId, model.Ad, model.AdEN, model.Grup, model.GrupEN, model.EkFiyat, model.Zorunlu);

            if (!success)
                return Json(new { success = false, message = errorMessage });

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true, opsiyonId });
        }

        [HttpPost("/admin/opsiyon-sil/{opsiyonId:int}")]
        public async Task<IActionResult> OpsiyonSil(int opsiyonId, [FromBody] AdminController.OpsiyonSilRequest? request)
        {
            var urunId = request?.UrunId ?? 0;
            var (success, errorMessage) = await _urunService.OpsiyonSilAsync(urunId, opsiyonId);
            if (!success)
                return Json(new { success = false, message = errorMessage });

            await _menuHub.Clients.All.SendAsync("MenuGuncellendi");
            return Json(new { success = true });
        }

        [HttpGet("/admin/opsiyon-gruplari")]
        public async Task<IActionResult> OpsiyonGruplari()
        {
            var gruplar = await _urunService.GetOpsiyonGruplariAsync();
            return Json(gruplar.Select(g => new { grup = g.Grup, grupEN = g.GrupEN }));
        }

        [AllowAnonymous]
        [HttpGet("/images/urun/{id:int}")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> UrunGorsel(int id)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "urunler");
            var staticFiles = Directory.Exists(uploadsDir)
                ? Directory.GetFiles(uploadsDir, $"{id}_*").Concat(Directory.GetFiles(uploadsDir, $"{id}.*")).ToArray()
                : Array.Empty<string>();

            if (staticFiles.Length > 0)
                return Redirect($"/uploads/urunler/{Path.GetFileName(staticFiles[0])}");

            var (data, contentType) = await _urunService.GetUrunGorselFromDbAsync(id);
            if (data == null || string.IsNullOrEmpty(contentType))
                return NotFound();

            return File(data, contentType);
        }

        private static bool IsValidImageHeader(Stream stream, string ext)
        {
            var header = new byte[12];
            var bytesRead = stream.Read(header, 0, header.Length);
            if (stream.CanSeek) stream.Position = 0;
            if (bytesRead < 12)
                return false;

            if (ext == ".jpg" || ext == ".jpeg")
            {
                return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            }
            if (ext == ".png")
            {
                return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
            }
            if (ext == ".webp")
            {
                return header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                       header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
            }
            return false;
        }

        private async Task<string?> SaveImageToFileAsync(IFormFile file, int urunId)
        {
            if (file.Length == 0 || file.Length > MaxFileSize)
                return null;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !_allowedExtensions.Contains(ext))
                return null;

            using (var readStream = file.OpenReadStream())
            {
                if (!IsValidImageHeader(readStream, ext))
                    return null;
            }

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "urunler");
            Directory.CreateDirectory(uploadsDir);

            foreach (var oldFile in Directory.GetFiles(uploadsDir, $"{urunId}_*").Concat(Directory.GetFiles(uploadsDir, $"{urunId}.*")))
            {
                try { System.IO.File.Delete(oldFile); } catch { }
            }

            var fileName = $"{urunId}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/urunler/{fileName}";
        }
    }
}
