using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Web.Helpers;

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
            // Tükendi (AktifMi=false) rozetini göstermek için tüm ürünleri çekiyoruz
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
            var urun = await _urunService.GetByIdAsync(id);
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
            var lang = Request.Cookies["lang"] ?? "tr";
            bool isEn = lang == "en";

            string L(string? tr, string? en) => (isEn && !string.IsNullOrEmpty(en)) ? en : (tr ?? "");

            var kategoriler = urunler
                .Select(u => u.Kategori)
                .DistinctBy(k => k.Id)
                .OrderBy(k => k.SiraNo)
                .Select(k => new
                {
                    k.Id,
                    Ad = L(k.Ad, k.AdEN),
                    Urunler = urunler.Where(u => u.KategoriId == k.Id).Select(u => new
                    {
                        u.Id,
                        Ad = L(u.Ad, u.AdEN),
                        Aciklama = L(u.Aciklama, u.AciklamaEN),
                        u.Fiyat,
                        u.GorselUrl,
                        u.PopulerMi,
                        u.AktifMi,
                        u.Kalori,
                        Opsiyonlar = (u.UrunOpsiyonlar ?? new List<UrunOpsiyon>()).OrderBy(uo => uo.Opsiyon.EkFiyat).Select(uo => new
                        {
                            id = uo.Opsiyon.Id,
                            ad = OptionLocalization.LocalizeOptionText(uo.Opsiyon.Ad, uo.Opsiyon.AdEN, isEn),
                            grup = OptionLocalization.LocalizeOptionText(uo.Opsiyon.Grup, uo.Opsiyon.GrupEN, isEn),
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
                    urunIds = happyHour.Value.UrunIds
                } : null
            });
        }

        private async Task<(decimal IndirimOrani, List<int> UrunIds)?> GetActiveHappyHourAsync()
        {
            var hh = await _dbContext.HappyHourlar
                .Include(h => h.HappyHourUrunler)
                .Where(h => h.AktifMi)
                .FirstOrDefaultAsync();
            if (hh == null) return null;

            var turkey = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
            var simdiki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkey).TimeOfDay;

            bool aktif;
            if (hh.BaslangicSaati <= hh.BitisSaati)
                aktif = simdiki >= hh.BaslangicSaati && simdiki <= hh.BitisSaati;
            else
                aktif = simdiki >= hh.BaslangicSaati || simdiki <= hh.BitisSaati;

            if (aktif)
            {
                var urunIds = hh.HappyHourUrunler.Select(x => x.UrunId).ToList();
                return (hh.IndirimOrani, urunIds);
            }
            return null;
        }
    }
}
