using Microsoft.AspNetCore.Mvc;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;

namespace QRMenu.Web.Controllers
{
    public class MenuController : Controller
    {
        private readonly IUrunService _urunService;
        private readonly ILogger<MenuController> _logger;

        public MenuController(IUrunService urunService, ILogger<MenuController> logger)
        {
            _urunService = urunService;
            _logger = logger;
        }

        /// <summary>
        /// Menü sayfası — tüm ürünleri kategorilere göre gösterir
        /// AktifMi=false olanlar "Tükendi" rozeti ile gösterilir
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var urunler = await _urunService.GetAllAsync();
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

            return Json(kategoriler);
        }
    }
}
