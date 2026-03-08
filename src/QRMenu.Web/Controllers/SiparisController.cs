using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Web.Hubs;

namespace QRMenu.Web.Controllers
{
    public class SiparisController : Controller
    {
        private readonly ISiparisService _siparisService;
        private readonly ISepetService _sepetService;
        private readonly ITokenService _tokenService;
        private readonly ILogger<SiparisController> _logger;
        private readonly IHubContext<MenuHub> _menuHub;

        public SiparisController(
            ISiparisService siparisService,
            ISepetService sepetService,
            ITokenService tokenService,
            ILogger<SiparisController> logger,
            IHubContext<MenuHub> menuHub)
        {
            _siparisService = siparisService;
            _sepetService = sepetService;
            _tokenService = tokenService;
            _logger = logger;
            _menuHub = menuHub;
        }

        private async Task<int?> GetOturumIdAsync()
        {
            var token = Request.Cookies["qrmenu_token"];
            if (string.IsNullOrEmpty(token)) return null;

            var hash = _tokenService.HashToken(token);
            var oturum = await _tokenService.ValidateTokenAsync(hash);
            return oturum?.Id;
        }

        /// <summary>
        /// Sepetten sipariş oluştur: POST /siparis/olustur (AJAX)
        /// </summary>
        [HttpPost("/siparis/olustur")]
        public async Task<IActionResult> Olustur([FromBody] SiparisOlusturRequest? request)
        {
            var oturumId = await GetOturumIdAsync();
            if (oturumId == null) return Unauthorized();

            try
            {
                var sepet = await _sepetService.GetSepetByOturumAsync(oturumId.Value);
                if (sepet == null)
                    return Json(new { success = false, message = "Sepet bulunamadı." });

                var siparis = await _siparisService.SiparisOlusturAsync(sepet.Id, request?.Notlar);

                await _menuHub.Clients.All.SendAsync("SiparisGuncellendi");

                return Json(new
                {
                    success = true,
                    message = "Siparişiniz alındı!",
                    siparisId = siparis.Id,
                    toplamTutar = siparis.ToplamTutar,
                    durum = siparis.Durum.ToString()
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Sipariş detayı: GET /siparis/{id} (AJAX)
        /// </summary>
        [HttpGet("/siparis/{id:int}")]
        public async Task<IActionResult> Detay(int id)
        {
            var siparis = await _siparisService.GetSiparisAsync(id);
            if (siparis == null)
                return Json(new { success = false, message = "Sipariş bulunamadı." });

            return Json(new
            {
                success = true,
                siparisId = siparis.Id,
                durum = siparis.Durum.ToString(),
                toplamTutar = siparis.ToplamTutar,
                olusturmaTarihi = siparis.OlusturmaTarihi,
                notlar = siparis.Notlar,
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

        /// <summary>
        /// Sipariş durumu güncelle: POST /siparis/durum-guncelle (AJAX)
        /// </summary>
        [HttpPost("/siparis/durum-guncelle")]
        public async Task<IActionResult> DurumGuncelle([FromBody] DurumGuncelleRequest request)
        {
            try
            {
                var siparis = await _siparisService.DurumGuncelleAsync(request.SiparisId, request.YeniDurum);
                return Json(new
                {
                    success = true,
                    siparisId = siparis.Id,
                    durum = siparis.Durum.ToString(),
                    message = $"Sipariş durumu güncellendi: {siparis.Durum}"
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Aktif oturumun siparişleri: GET /siparis/siparislerim (AJAX)
        /// </summary>
        [HttpGet("/siparis/siparislerim")]
        public async Task<IActionResult> Siparislerim()
        {
            var oturumId = await GetOturumIdAsync();
            if (oturumId == null) return Unauthorized();

            var siparisler = await _siparisService.GetSiparislerByOturumAsync(oturumId.Value);

            return Json(new
            {
                success = true,
                siparisler = siparisler.Select(s => new
                {
                    siparisId = s.Id,
                    durum = s.Durum.ToString(),
                    toplamTutar = s.ToplamTutar,
                    olusturmaTarihi = s.OlusturmaTarihi,
                    detaylar = s.SiparisDetaylar.Select(sd => new
                    {
                        urunAd = sd.Urun.Ad,
                        adet = sd.Adet,
                        birimFiyat = sd.BirimFiyat
                    })
                })
            });
        }

        /// <summary>
        /// Sipariş iptal: POST /siparis/iptal/{id} (AJAX)
        /// </summary>
        [HttpPost("/siparis/iptal/{id:int}")]
        public async Task<IActionResult> Iptal(int id)
        {
            try
            {
                var siparis = await _siparisService.IptalEtAsync(id);
                return Json(new
                {
                    success = true,
                    message = "Sipariş iptal edildi.",
                    durum = siparis.Durum.ToString()
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    // ===== Request DTO'lar =====
    public class SiparisOlusturRequest
    {
        public string? Notlar { get; set; }
    }

    public class DurumGuncelleRequest
    {
        public int SiparisId { get; set; }
        public SiparisDurum YeniDurum { get; set; }
    }
}
