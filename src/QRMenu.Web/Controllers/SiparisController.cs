using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Web.Hubs;
using System.Text.Json;

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
        private readonly QRMenu.Data.Data.QRMenuDbContext _db;
        private readonly IDataProtector _gameTokenProtector;

        public SiparisController(
            ISiparisService siparisService,
            ISepetService sepetService,
            ITokenService tokenService,
            ILogger<SiparisController> logger,
            IHubContext<MenuHub> menuHub,
            QRMenu.Data.Data.QRMenuDbContext db,
            IDataProtectionProvider dataProtectionProvider)
        {
            _siparisService = siparisService;
            _sepetService = sepetService;
            _tokenService = tokenService;
            _logger = logger;
            _menuHub = menuHub;
            _db = db;
            _gameTokenProtector = dataProtectionProvider.CreateProtector("QRMenu.Game.Token.v1");
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

        private string CreateGameToken(GameTokenPayload payload)
        {
            var json = JsonSerializer.Serialize(payload);
            return _gameTokenProtector.Protect(json);
        }

        private bool TryReadGameToken(string? token, out GameTokenPayload? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(token)) return false;

            try
            {
                var json = _gameTokenProtector.Unprotect(token);
                payload = JsonSerializer.Deserialize<GameTokenPayload>(json);
                return payload != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool ValidateMemoryMoves(IReadOnlyList<int>? pairMap, IReadOnlyList<HafizaHamleDto>? moves)
        {
            if (pairMap == null || pairMap.Count != 16 || moves == null) return false;

            var matchedPairs = new HashSet<int>();
            var usedIndexes = new HashSet<int>();

            foreach (var move in moves)
            {
                if (move.IlkIndex < 0 || move.IlkIndex >= 16 || move.IkinciIndex < 0 || move.IkinciIndex >= 16)
                    return false;
                if (move.IlkIndex == move.IkinciIndex) return false;
                if (usedIndexes.Contains(move.IlkIndex) || usedIndexes.Contains(move.IkinciIndex))
                    return false;

                var leftPair = pairMap[move.IlkIndex];
                var rightPair = pairMap[move.IkinciIndex];
                if (leftPair != rightPair) return false;
                if (!matchedPairs.Add(leftPair)) return false;

                usedIndexes.Add(move.IlkIndex);
                usedIndexes.Add(move.IkinciIndex);
            }

            return matchedPairs.Count == 8;
        }

        /// <summary>
        /// Siparişlerim sayfası: GET /siparislerim
        /// </summary>
        [HttpGet("/siparislerim")]
        public async Task<IActionResult> SiparislerimSayfa()
        {
            var bilgi = await GetOturumBilgiAsync();
            if (bilgi == null) 
            {
                // Just load the view without a session. The JS will handle the "Session not found" state beautifully!
                return View("Siparislerim"); 
            }

            HttpContext.Items["MasaId"] = bilgi.Value.masaNo;
            return View("Siparislerim");
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
                    oyunOynandiMi = s.OyunOynandiMi,
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
        /// Oyun seçim verisi: GET /siparis/oyun-secimleri/{id}
        /// </summary>
        [HttpGet("/siparis/oyun-secimleri/{id:int}")]
        public async Task<IActionResult> OyunSecimleri(int id)
        {
            try
            {
                var oturumId = await GetOturumIdAsync();
                if (oturumId == null) return Unauthorized();

                var siparis = await _db.Siparisler
                    .FirstOrDefaultAsync(s => s.Id == id && s.OturumId == oturumId.Value);

                if (siparis == null)
                    return Json(new { success = false, message = "Sipariş bulunamadı." });

                if (siparis.Durum == SiparisDurum.Iptal || siparis.Durum == SiparisDurum.Iade || siparis.Durum == SiparisDurum.TamOdendi)
                    return Json(new { success = false, message = "Oyun bu sipariş durumunda oynanamaz." });

                if (siparis.OyunOynandiMi)
                    return Json(new { success = false, message = "Bu sipariş için daha önce şansınızı denediniz." });

                var aktifOyunlar = await _db.OyunAyarlar
                    .Include(o => o.Oduller)
                    .Where(o => o.AktifMi)
                    .ToListAsync();

                if (!aktifOyunlar.Any())
                {
                    return Json(new { success = false, message = "Şu an aktif oyun bulunmuyor." });
                }

                var memoryUrunler = await _db.Urunler
                    .AsNoTracking()
                    .Where(u => u.AktifMi)
                    .Select(u => new
                    {
                        u.Id,
                        u.Ad,
                        GorselUrl = !string.IsNullOrWhiteSpace(u.GorselUrl) ? u.GorselUrl : $"/images/urun/{u.Id}"
                    })
                    .ToListAsync();

                var selectedMemoryProducts = memoryUrunler
                    .GroupBy(u => u.GorselUrl)
                    .Select(g => g.First())
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(8)
                    .ToList();

                var memoryCards = selectedMemoryProducts
                    .SelectMany((u, index) => new[]
                    {
                        new
                        {
                            kartId = $"{index}-A-{Guid.NewGuid():N}",
                            eslesmeId = index,
                            urunId = u.Id,
                            urunAd = u.Ad,
                            gorselUrl = u.GorselUrl
                        },
                        new
                        {
                            kartId = $"{index}-B-{Guid.NewGuid():N}",
                            eslesmeId = index,
                            urunId = u.Id,
                            urunAd = u.Ad,
                            gorselUrl = u.GorselUrl
                        }
                    })
                    .OrderBy(_ => Guid.NewGuid())
                    .Select(u => new
                    {
                        u.kartId,
                        u.eslesmeId,
                        u.urunId,
                        u.urunAd,
                        u.gorselUrl
                    })
                    .ToList();

                var memoryPairMap = memoryCards.Select(c => c.eslesmeId).ToList();
                var expiresAtUtc = DateTime.UtcNow.AddMinutes(3);
                var carkToken = CreateGameToken(new GameTokenPayload
                {
                    SiparisId = siparis.Id,
                    OturumId = oturumId.Value,
                    OyunTipi = "CARKIFELEK",
                    ExpiresAtUtc = expiresAtUtc
                });
                var hafizaToken = CreateGameToken(new GameTokenPayload
                {
                    SiparisId = siparis.Id,
                    OturumId = oturumId.Value,
                    OyunTipi = "HAFIZA",
                    ExpiresAtUtc = expiresAtUtc,
                    HafizaEslesmeSirasi = memoryPairMap
                });
                var kaziToken = CreateGameToken(new GameTokenPayload
                {
                    SiparisId = siparis.Id,
                    OturumId = oturumId.Value,
                    OyunTipi = "KAZIKAZAN",
                    ExpiresAtUtc = expiresAtUtc
                });

                return Json(new
                {
                    success = true,
                    oyunlar = new List<object>
                    {
                        new
                        {
                            tip = "CARKIFELEK",
                            ad = "Çarkıfelek",
                            aciklama = "Çarkı döndür, çıkan ödülü anında kazan.",
                            oyunToken = carkToken,
                            aktif = aktifOyunlar.Any(o => o.Tip == "CARKIFELEK" && o.Oduller.Any()),
                            oduller = aktifOyunlar
                                .Where(o => o.Tip == "CARKIFELEK")
                                .SelectMany(o => o.Oduller)
                                .OrderBy(od => od.Id)
                                .Select(od => new
                                {
                                    odulTanim = !string.IsNullOrWhiteSpace(od.OdulTanim)
                                        ? od.OdulTanim
                                        : (od.IndirimYuzdesi > 0
                                            ? $"%{od.IndirimYuzdesi} İndirim"
                                            : $"{od.IndirimTutari} TL İndirim"),
                                    ihtimal = od.IhtimalYuzdesi
                                }),
                            hafizaKartlari = Array.Empty<object>()
                        },
                        new
                        {
                            tip = "HAFIZA",
                            ad = "Hafıza Kartı",
                            aciklama = "16 kartı 45 saniyede eşleştir, başarırsan ödülü kap.",
                            oyunToken = hafizaToken,
                            aktif = aktifOyunlar.Any(o => o.Tip == "HAFIZA" && o.Oduller.Any()) && memoryCards.Count == 16,
                            oduller = aktifOyunlar
                                .Where(o => o.Tip == "HAFIZA")
                                .SelectMany(o => o.Oduller)
                                .OrderBy(od => od.Id)
                                .Select(od => new
                                {
                                    odulTanim = !string.IsNullOrWhiteSpace(od.OdulTanim)
                                        ? od.OdulTanim
                                        : (od.IndirimYuzdesi > 0
                                            ? $"%{od.IndirimYuzdesi} İndirim"
                                            : $"{od.IndirimTutari} TL İndirim"),
                                    ihtimal = od.IhtimalYuzdesi
                                }),
                            hafizaKartlari = memoryCards
                        },
                        new
                        {
                            tip = "KAZIKAZAN",
                            ad = "Kazı Kazan",
                            aciklama = "Kazı alanını aç, şansın varsa sürpriz ödülü al.",
                            oyunToken = kaziToken,
                            aktif = aktifOyunlar.Any(o => o.Tip == "KAZIKAZAN" && o.Oduller.Any()),
                            oduller = aktifOyunlar
                                .Where(o => o.Tip == "KAZIKAZAN")
                                .SelectMany(o => o.Oduller)
                                .OrderBy(od => od.Id)
                                .Select(od => new
                                {
                                    odulTanim = !string.IsNullOrWhiteSpace(od.OdulTanim)
                                        ? od.OdulTanim
                                        : (od.IndirimYuzdesi > 0
                                            ? $"%{od.IndirimYuzdesi} İndirim"
                                            : $"{od.IndirimTutari} TL İndirim"),
                                    ihtimal = od.IhtimalYuzdesi
                                }),
                            hafizaKartlari = Array.Empty<object>()
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Seçilen oyun sonucu: POST /siparis/oyun-sonuclandir/{id}
        /// </summary>
        [HttpPost("/siparis/oyun-sonuclandir/{id:int}")]
        public async Task<IActionResult> OyunSonuclandir(int id, [FromBody] OyunSonucRequest request)
        {
            try
            {
                var oturumId = await GetOturumIdAsync();
                if (oturumId == null) return Unauthorized();

                var siparis = await _db.Siparisler
                    .FirstOrDefaultAsync(s => s.Id == id && s.OturumId == oturumId.Value);

                if (siparis == null)
                    return Json(new { success = false, message = "Sipariş bulunamadı." });

                if (siparis.Durum == SiparisDurum.Iptal || siparis.Durum == SiparisDurum.Iade || siparis.Durum == SiparisDurum.TamOdendi)
                    return Json(new { success = false, message = "Oyun bu sipariş durumunda oynanamaz." });

                if (siparis.OyunOynandiMi)
                    return Json(new { success = false, message = "Bu sipariş için daha önce şansınızı denediniz." });

                if (!TryReadGameToken(request.OyunToken, out var tokenPayload) || tokenPayload == null)
                    return Json(new { success = false, message = "Geçersiz oyun oturumu. Lütfen oyunu tekrar açın." });

                if (tokenPayload.SiparisId != siparis.Id || tokenPayload.OturumId != oturumId.Value)
                    return Json(new { success = false, message = "Oyun oturumu bu siparişe ait değil." });

                if (tokenPayload.ExpiresAtUtc < DateTime.UtcNow)
                    return Json(new { success = false, message = "Oyun süresi doldu. Lütfen tekrar deneyin." });

                var secilenTip = tokenPayload.OyunTipi;
                var oyun = await _db.OyunAyarlar
                    .Include(o => o.Oduller)
                    .FirstOrDefaultAsync(o => o.AktifMi && o.Tip == secilenTip);

                siparis.OyunOynandiMi = true;

                if (oyun == null || !oyun.Oduller.Any())
                {
                    await _db.SaveChangesAsync();
                    return Json(new { success = true, kazandiMi = false, message = "Seçtiğiniz oyunda şu an ödül havuzu yok." });
                }

                if (!request.BasariliMi)
                {
                    await _db.SaveChangesAsync();
                    return Json(new { success = true, kazandiMi = false, message = "Bu turda kazanamadınız. Bir dahaki sefere bol şans!" });
                }

                if (secilenTip == "HAFIZA")
                {
                    var validMoves = ValidateMemoryMoves(tokenPayload.HafizaEslesmeSirasi, request.HafizaHamleleri);
                    if (!validMoves)
                    {
                        await _db.SaveChangesAsync();
                        return Json(new { success = true, kazandiMi = false, message = "Hafıza oyunu doğrulanamadı. Ödül verilemedi." });
                    }
                }

                var oduller = oyun.Oduller.OrderBy(o => o.Id).ToList();
                var sansDegeri = (decimal)Random.Shared.NextDouble() * 100M;
                decimal kumulatif = 0;
                OyunOdul? kazanilanOdul = null;
                var hedefIndex = 0;

                for (var i = 0; i < oduller.Count; i++)
                {
                    kumulatif += oduller[i].IhtimalYuzdesi;
                    if (sansDegeri <= kumulatif)
                    {
                        kazanilanOdul = oduller[i];
                        hedefIndex = i;
                        break;
                    }
                }

                if (kazanilanOdul == null)
                {
                    var bosIndex = oduller.FindIndex(o => o.IndirimYuzdesi <= 0 && o.IndirimTutari <= 0);
                    if (bosIndex >= 0)
                    {
                        var bosEtiket = secilenTip == "CARKIFELEK" ? "Boş Dilim" : "Boş Sonuç";
                        var bosMesaj = secilenTip == "CARKIFELEK"
                            ? "Bu tur boş dilim geldi. Bu siparişte ekstra indirim uygulanmadı."
                            : "Bu tur boş sonuç geldi. Bu siparişte ekstra indirim uygulanmadı.";

                        await _db.SaveChangesAsync();
                        return Json(new
                        {
                            success = true,
                            kazandiMi = false,
                            oyunTipi = secilenTip,
                            hedefIndex = bosIndex,
                            odulTanim = string.IsNullOrWhiteSpace(oduller[bosIndex].OdulTanim) ? bosEtiket : oduller[bosIndex].OdulTanim,
                            message = bosMesaj
                        });
                    }

                    await _db.SaveChangesAsync();
                    return Json(new { success = true, kazandiMi = false, message = "Bu turda ödül çıkmadı." });
                }

                // Oyunlarda tanımlanan boş sonuç: indirim alanları 0 ise ödül verilmez.
                if (kazanilanOdul.IndirimYuzdesi <= 0 && kazanilanOdul.IndirimTutari <= 0)
                {
                    var bosEtiket = secilenTip == "CARKIFELEK" ? "Boş Dilim" : "Boş Sonuç";
                    var bosMesaj = secilenTip == "CARKIFELEK"
                        ? "Bu tur boş dilim geldi. Bu siparişte ekstra indirim uygulanmadı."
                        : "Bu tur boş sonuç geldi. Bu siparişte ekstra indirim uygulanmadı.";

                    await _db.SaveChangesAsync();
                    return Json(new
                    {
                        success = true,
                        kazandiMi = false,
                        oyunTipi = secilenTip,
                        hedefIndex,
                        odulTanim = string.IsNullOrWhiteSpace(kazanilanOdul.OdulTanim) ? bosEtiket : kazanilanOdul.OdulTanim,
                        message = bosMesaj
                    });
                }

                decimal kazanilanIndirimTutari = 0;
                if (kazanilanOdul.IndirimYuzdesi > 0)
                {
                    kazanilanIndirimTutari = (siparis.ToplamTutar * kazanilanOdul.IndirimYuzdesi) / 100;
                }
                else if (kazanilanOdul.IndirimTutari > 0)
                {
                    kazanilanIndirimTutari = kazanilanOdul.IndirimTutari;
                }

                siparis.ToplamTutar -= kazanilanIndirimTutari;
                if (siparis.ToplamTutar < 0) siparis.ToplamTutar = 0;

                _db.KazanilanIndirimler.Add(new KazanilanIndirim
                {
                    SiparisId = siparis.Id,
                    OdulTanim = kazanilanOdul.OdulTanim,
                    UgulananIndirimTutari = kazanilanIndirimTutari,
                    KazanmaTarihi = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await _menuHub.Clients.All.SendAsync("SiparisGuncellendi");

                return Json(new
                {
                    success = true,
                    kazandiMi = true,
                    oyunTipi = secilenTip,
                    hedefIndex,
                    message = $"Tebrikler! {kazanilanOdul.OdulTanim} kazandınız!",
                    odulTanim = kazanilanOdul.OdulTanim,
                    indirimTutari = kazanilanIndirimTutari,
                    yeniTutar = siparis.ToplamTutar
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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

    public class OyunSonucRequest
    {
        public string OyunTipi { get; set; } = string.Empty;
        public string OyunToken { get; set; } = string.Empty;
        public bool BasariliMi { get; set; }
        public List<HafizaHamleDto> HafizaHamleleri { get; set; } = new();
    }

    public class HafizaHamleDto
    {
        public int IlkIndex { get; set; }
        public int IkinciIndex { get; set; }
    }

    public class GameTokenPayload
    {
        public int SiparisId { get; set; }
        public int OturumId { get; set; }
        public string OyunTipi { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public List<int>? HafizaEslesmeSirasi { get; set; }
    }
}
