using Microsoft.AspNetCore.Mvc;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using Microsoft.EntityFrameworkCore;

namespace QRMenu.Web.Controllers
{
    public class SepetController : Controller
    {
        private readonly ISepetService _sepetService;
        private readonly QRMenuDbContext _context;
        private readonly ILogger<SepetController> _logger;

        public SepetController(
            ISepetService sepetService,
            QRMenuDbContext context,
            ILogger<SepetController> logger)
        {
            _sepetService = sepetService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Aktif oturumun ID'sini cookie'deki token'dan bul
        /// </summary>
        private int? GetOturumId()
        {
            if (!HttpContext.Items.TryGetValue("OturumId", out var oturumObj) || oturumObj == null)
                return null;

            if (oturumObj is int oturumId)
                return oturumId;

            return int.TryParse(oturumObj.ToString(), out var parsed) ? parsed : null;
        }

        /// <summary>
        /// Sepet sayfası
        /// </summary>
        [HttpGet("/sepet")]
        public async Task<IActionResult> Index()
        {
            var oturumId = GetOturumId();
            if (oturumId == null) 
            {
                // Return view with null model so the user sees an empty cart or "Session not found" rather than a confusing redirect.
                return View((QRMenu.Core.Entities.Sepet?)null);
            }

            var sepet = await _sepetService.GetSepetByOturumAsync(oturumId.Value);
            if (sepet != null)
            {
                sepet = await _sepetService.GetSepetWithDetailsAsync(sepet.Id);
                if (sepet != null)
                    ViewBag.StokKontrol = await _sepetService.ValidateStockAsync(sepet.Id);
            }

            var happyHour = await GetActiveHappyHourAsync();
            ViewBag.HappyHour = happyHour;

            return View(sepet);
        }

        private async Task<(decimal IndirimOrani, List<int> UrunIds)?> GetActiveHappyHourAsync()
        {
            var hh = await _context.HappyHourlar
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

        /// <summary>
        /// Sepete ürün ekle: POST /sepet/ekle (AJAX)
        /// </summary>
        [HttpPost("/sepet/ekle")]
        public async Task<IActionResult> Ekle([FromBody] SepeteEkleRequest request)
        {
            var oturumId = GetOturumId();
            if (oturumId == null) return Unauthorized();

            try
            {
                var sepet = await _sepetService.GetOrCreateSepetAsync(oturumId.Value);
                
                decimal? discountRate = null;
                var hh = await GetActiveHappyHourAsync();
                if (hh != null && (!hh.Value.UrunIds.Any() || hh.Value.UrunIds.Contains(request.UrunId)))
                {
                    discountRate = hh.Value.IndirimOrani;
                }

                var detay = await _sepetService.AddItemAsync(sepet.Id, request.UrunId, request.Adet, request.OpsiyonIds, discountRate, request.UrunVaryasyonId);

                // Güncel sepet bilgisini döndür
                var guncelSepet = await _sepetService.GetSepetWithDetailsAsync(sepet.Id);
                return Json(new
                {
                    success = true,
                    message = "Ürün sepete eklendi",
                    toplamTutar = guncelSepet?.ToplamTutar ?? 0,
                    urunSayisi = guncelSepet?.SepetDetaylar.Sum(sd => sd.Adet) ?? 0
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Sepetten ürün çıkar: POST /sepet/cikar/{id} (AJAX)
        /// </summary>
        [HttpPost("/sepet/cikar/{detayId:int}")]
        public async Task<IActionResult> Cikar(int detayId)
        {
            var oturumId = GetOturumId();
            if (oturumId == null) return Unauthorized();

            var result = await _sepetService.RemoveItemAsync(detayId);
            if (!result) return Json(new { success = false, message = "Ürün bulunamadı" });

            var sepet = await _sepetService.GetSepetByOturumAsync(oturumId.Value);
            return Json(new
            {
                success = true,
                toplamTutar = sepet?.ToplamTutar ?? 0,
                urunSayisi = sepet?.SepetDetaylar.Sum(sd => sd.Adet) ?? 0
            });
        }

        /// <summary>
        /// Adet güncelle: POST /sepet/guncelle/{id} (AJAX)
        /// </summary>
        [HttpPost("/sepet/guncelle/{detayId:int}")]
        public async Task<IActionResult> Guncelle(int detayId, [FromBody] AdetGuncelleRequest request)
        {
            var oturumId = GetOturumId();
            if (oturumId == null) return Unauthorized();

            bool result;
            try
            {
                result = await _sepetService.UpdateQuantityAsync(detayId, request.Adet);
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            if (!result) return Json(new { success = false, message = "Ürün bulunamadı" });

            var sepet = await _sepetService.GetSepetByOturumAsync(oturumId.Value);
            return Json(new
            {
                success = true,
                toplamTutar = sepet?.ToplamTutar ?? 0,
                urunSayisi = sepet?.SepetDetaylar.Sum(sd => sd.Adet) ?? 0
            });
        }

        /// <summary>
        /// Sepeti temizle: POST /sepet/temizle (AJAX)
        /// </summary>
        [HttpPost("/sepet/temizle")]
        public async Task<IActionResult> Temizle()
        {
            var oturumId = GetOturumId();
            if (oturumId == null) return Unauthorized();

            var sepet = await _sepetService.GetSepetByOturumAsync(oturumId.Value);
            if (sepet == null) return Json(new { success = false, message = "Sepet bulunamadı" });

            await _sepetService.ClearSepetAsync(sepet.Id);
            return Json(new { success = true, toplamTutar = 0, urunSayisi = 0 });
        }

        /// <summary>
        /// Sepet badge bilgisi (menü sayfasından AJAX ile çekilir): GET /sepet/bilgi
        /// </summary>
        [HttpGet("/sepet/bilgi")]
        public async Task<IActionResult> Bilgi()
        {
            var oturumId = GetOturumId();
            if (oturumId == null) return Json(new { toplamTutar = 0, urunSayisi = 0 });

            var sepet = await _sepetService.GetSepetByOturumAsync(oturumId.Value);
            return Json(new
            {
                toplamTutar = sepet?.ToplamTutar ?? 0,
                urunSayisi = sepet?.SepetDetaylar.Sum(sd => sd.Adet) ?? 0
            });
        }

        /// <summary>
        /// Offcanvas sepet içeriğini yüklemek için JSON datası döndürür.
        /// </summary>
        [HttpGet("/sepet/detay-json")]
        public async Task<IActionResult> DetayJson()
        {
            var oturumId = GetOturumId();
            if (oturumId == null) return Json(new { success = false });

            var sepet = await _sepetService.GetSepetByOturumAsync(oturumId.Value);
            if (sepet == null || !sepet.SepetDetaylar.Any())
                return Json(new { success = true, toplamTutar = 0, urunSayisi = 0, items = new object[0] });

            var sepetWithDetails = await _sepetService.GetSepetWithDetailsAsync(sepet.Id);
            if (sepetWithDetails == null) return Json(new { success = false });

            return Json(new
            {
                success = true,
                toplamTutar = sepetWithDetails.ToplamTutar,
                urunSayisi = sepetWithDetails.SepetDetaylar.Sum(sd => sd.Adet),
                items = sepetWithDetails.SepetDetaylar.OrderBy(sd => sd.Id).Select(sd => new
                {
                    detayId = sd.Id,
                    urunAd = sd.Urun.Ad,
                    adet = sd.Adet,
                    birimFiyat = sd.BirimFiyat,
                    opsiyonlar = sd.SeciliOpsiyonlar
                })
            });
        }
    }

    // ===== Request DTO'lar =====
    public class SepeteEkleRequest
    {
        public int UrunId { get; set; }
        public int? UrunVaryasyonId { get; set; }
        public int Adet { get; set; } = 1;
        public List<int>? OpsiyonIds { get; set; }
    }

    public class AdetGuncelleRequest
    {
        public int Adet { get; set; }
    }
}
