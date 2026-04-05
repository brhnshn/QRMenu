using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;

namespace QRMenu.Web.Controllers
{
    public class MenuController : Controller
    {
        private readonly IUrunService _urunService;
        private readonly ILogger<MenuController> _logger;
        private readonly QRMenu.Data.Data.QRMenuDbContext _dbContext;

        public MenuController(IUrunService urunService, ILogger<MenuController> logger, QRMenu.Data.Data.QRMenuDbContext dbContext)
        {
            _urunService = urunService;
            _logger = logger;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Menü sayfası — tüm ürünleri kategorilere göre gösterir
        /// AktifMi=false olanlar "Tükendi" rozeti ile gösterilir
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var urunler = await _urunService.GetAllAsync();
            var happyHour = await GetActiveHappyHourAsync();
            ViewBag.HappyHour = happyHour;
            return View(urunler);
        }

        /// <summary>
        /// Ürün detay sayfası
        /// </summary>
        public async Task<IActionResult> Detay(int id)
        {
            var urunler = await _urunService.GetAllAsync();
            var urun = urunler.FirstOrDefault(u => u.Id == id);
            if (urun == null) return NotFound();

            var happyHour = await GetActiveHappyHourAsync();
            ViewBag.HappyHour = happyHour;

            return View(urun);
        }

        /// <summary>
        /// Menü verisi JSON — SignalR güncellemesi sonrası frontend bu endpoint'i çağırır
        /// </summary>
        [HttpGet("/menu/json")]
        public async Task<IActionResult> MenuJson()
        {
            var urunler = await _urunService.GetAllAsync();

            var kategoriler = urunler
                .Select(u => u.Kategori)
                .DistinctBy(k => k.Id)
                .OrderBy(k => k.SiraNo)
                .Select(k => new
                {
                    k.Id,
                    k.Ad,
                    Urunler = urunler.Where(u => u.KategoriId == k.Id).Select(u => new
                    {
                        u.Id,
                        u.Ad,
                        u.Aciklama,
                        u.Fiyat,
                        u.GorselUrl,
                        u.PopulerMi,
                        u.AktifMi,
                        u.Kalori,
                        Opsiyonlar = (u.UrunOpsiyonlar ?? new List<UrunOpsiyon>()).OrderBy(uo => uo.Opsiyon.EkFiyat).Select(uo => new
                        {
                            id = uo.Opsiyon.Id,
                            ad = uo.Opsiyon.Ad,
                            grup = uo.Opsiyon.Grup,
                            ekFiyat = uo.Opsiyon.EkFiyat,
                            zorunlu = uo.Opsiyon.Zorunlu
                        })
                    })
                });

            var happyHour = await GetActiveHappyHourAsync();
            
            return Json(new {
                kategoriler,
                happyHour = happyHour != null ? new {
                    indirimOrani = happyHour.Value.IndirimOrani,
                    urunId = happyHour.Value.UrunId
                } : null
            });
        }
        
        private async Task<(decimal IndirimOrani, int? UrunId)?> GetActiveHappyHourAsync()
        {
            var hh = await _dbContext.HappyHourlar.Where(h => h.AktifMi).FirstOrDefaultAsync();
            if (hh == null) return null;

            var turkey = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
            var simdiki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkey).TimeOfDay;

            bool aktif;
            if (hh.BaslangicSaati <= hh.BitisSaati)
                aktif = simdiki >= hh.BaslangicSaati && simdiki <= hh.BitisSaati;
            else
                aktif = simdiki >= hh.BaslangicSaati || simdiki <= hh.BitisSaati;

            if (aktif) return (hh.IndirimOrani, hh.UrunId);
            return null;
        }
    }
}
