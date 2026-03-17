using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Web.Hubs;

namespace QRMenu.Web.Controllers
{
    public class SiparisController : Controller
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
        private static string ToTurkeyTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).ToString("dd.MM.yyyy HH:mm");

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

        private async Task<(int oturumId, int masaNo)?> GetOturumBilgiAsync()
        {
            var token = Request.Cookies["qrmenu_token"];
            if (string.IsNullOrEmpty(token)) return null;

            var hash = _tokenService.HashToken(token);
            var oturum = await _tokenService.ValidateTokenAsync(hash);
            if (oturum == null) return null;
            return (oturum.Id, oturum.Masa?.MasaNo ?? 0);
        }

        /// <summary>
        /// Garson çağır: POST /siparis/garson-cagir (AJAX)
        /// </summary>
        [HttpPost("/siparis/garson-cagir")]
        [EnableRateLimiting("GarsonCagirPolicy")]
        public async Task<IActionResult> GarsonCagir()
        {
            var bilgi = await GetOturumBilgiAsync();
            if (bilgi == null) return Unauthorized();

            var masaNo = bilgi.Value.masaNo;
            _logger.LogInformation("Garson çağrıldı! Masa={MasaNo}", masaNo);

            await _menuHub.Clients.All.SendAsync("GarsonCagrisi", masaNo);

            return Json(new { success = true, message = "Garson çağrıldı!" });
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
                await _menuHub.Clients.All.SendAsync("SiparisEklendi");

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
                olusturmaTarihi = ToTurkeyTime(siparis.OlusturmaTarihi),
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
                    olusturmaTarihi = ToTurkeyTime(s.OlusturmaTarihi),
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
