using Microsoft.AspNetCore.Mvc;
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
    }
}
