using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QRMenu.Core.Interfaces;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireAdmin")]
    public class AdminController : Controller
    {
        private readonly IRaporYonetimService _raporService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IRaporYonetimService raporService,
            ILogger<AdminController> logger)
        {
            _raporService = raporService;
            _logger = logger;
        }

        // Admin ana sayfa -> Dashboard yönlendirmesi
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

            var stats = await _raporService.GetDashboardStatsAsync(baslangic, bitis);

            ViewBag.BugunCiro = stats.BugunCiro;
            ViewBag.DunCiro = stats.DunCiro;
            ViewBag.CiroDegisim = stats.CiroDegisim;
            ViewBag.AktifMasalarCount = stats.AktifMasalarCount;
            ViewBag.ToplamMasaCount = stats.ToplamMasaCount;
            ViewBag.DolulukOrani = stats.DolulukOrani;
            ViewBag.OrtServisSuresi = stats.OrtServisSuresi;
            ViewBag.SaatlikData = stats.SaatlikData;
            ViewBag.EnCokSatanlar = stats.EnCokSatanlar.Select(x => new EnCokSatanViewModel
            {
                Ad = x.Ad,
                Adet = x.Adet,
                GorselUrl = x.GorselUrl,
                Fiyat = x.Fiyat
            }).ToList();
            ViewBag.BugunEnCokSatanlarTum = stats.BugunEnCokSatanlarTum.Select(x => new EnCokSatanViewModel
            {
                Ad = x.Ad,
                Adet = x.Adet,
                GorselUrl = x.GorselUrl,
                Fiyat = x.Fiyat
            }).ToList();
            ViewBag.CanliSiparisler = stats.CanliSiparisler;
            ViewBag.PersonelCount = stats.PersonelCount;
            ViewBag.Baslangic = stats.Baslangic.ToString("yyyy-MM-dd");
            ViewBag.Bitis = stats.Bitis.ToString("yyyy-MM-dd");
            ViewBag.ZRaporTarihi = stats.ZRaporTarihi.ToString("yyyy-MM-dd");
            ViewBag.ZToplamCiro = stats.ZToplamCiro;
            ViewBag.ZSiparisSayisi = stats.ZSiparisSayisi;
            ViewBag.ZOdemeTipleri = stats.ZOdemeTipleri.Select(x => new ZOdemeTipiViewModel
            {
                Tip = x.Tip,
                Tutar = x.Tutar,
                Adet = x.Adet
            }).ToList();
            ViewBag.GunKapaliMi = stats.GunKapaliMi;
            ViewBag.GunKapanisTarihi = stats.GunKapanisTarihi;
            ViewBag.ZNextOpeningIso = stats.ZNextOpeningIso;
            ViewBag.GunKapanisMetni = stats.GunKapanisMetni;

            return View();
        }

        // ============================================================
        // GERİYE DÖNÜK UYUMLULUK: VIEW VE EXPORT MODELLERİ
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

        public class OpsiyonSilRequest
        {
            public int UrunId { get; set; }
        }

        public class AdminDurumRequest
        {
            public string? YeniDurum { get; set; }
        }

        public class UrunStokGuncelleRequest
        {
            public int StokAdet { get; set; }
        }

        public class SaatlikCiroRow
        {
            public int Saat { get; set; }
            public decimal Ciro { get; set; }
        }
    }
}
