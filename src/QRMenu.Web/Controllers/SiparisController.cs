using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Web.Hubs;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using QRMenu.Core.Helpers;

namespace QRMenu.Web.Controllers
{
    public class SiparisController : Controller
    {
        private const int SiparisNotMaxLength = 50;

        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
        private static string ToTurkeyTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).ToString("dd.MM.yyyy HH:mm");

        private readonly ISiparisService _siparisService;
        private readonly ISepetService _sepetService;
        private readonly ILogger<SiparisController> _logger;
        private readonly IHubContext<OrderHub> _menuHub;
        private readonly QRMenu.Data.Data.QRMenuDbContext _db;
        private readonly IDataProtector _gameTokenProtector;

        public SiparisController(
            ISiparisService siparisService,
            ISepetService sepetService,
            ILogger<SiparisController> logger,
            IHubContext<OrderHub> menuHub,
            QRMenu.Data.Data.QRMenuDbContext db,
            IDataProtectionProvider dataProtectionProvider)
        {
            _siparisService = siparisService;
            _sepetService = sepetService;
            _logger = logger;
            _menuHub = menuHub;
            _db = db;
            _gameTokenProtector = dataProtectionProvider.CreateProtector("QRMenu.Game.Token.v1");
        }

        private int? GetOturumId()
        {
            if (!HttpContext.Items.TryGetValue("OturumId", out var oturumObj) || oturumObj == null)
                return null;

            if (oturumObj is int oturumId)
                return oturumId;

            return int.TryParse(oturumObj.ToString(), out var parsed) ? parsed : null;
        }

        private (int oturumId, int masaNo)? GetOturumBilgi()
        {
            var oturumId = GetOturumId();
            if (oturumId == null)
                return null;

            var masaNoObj = HttpContext.Items.TryGetValue("MasaNo", out var mObj) ? mObj : null;
            var masaIdObj = HttpContext.Items.TryGetValue("MasaId", out var miObj) ? miObj : null;

            int ParseOrZero(object? value) => value switch
            {
                int i => i,
                _ when int.TryParse(value?.ToString(), out var parsed) => parsed,
                _ => 0
            };

            var masaNo = ParseOrZero(masaNoObj);
            if (masaNo == 0)
                masaNo = ParseOrZero(masaIdObj);

            return (oturumId.Value, masaNo);
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
        public IActionResult SiparislerimSayfa()
        {
            var bilgi = GetOturumBilgi();
            if (bilgi == null) 
            {
                // Just load the view without a session. The JS will handle the "Session not found" state beautifully!
                return View("Siparislerim"); 
            }

            return View("Siparislerim");
        }

        /// <summary>
        /// Garson çağır: POST /siparis/garson-cagir (AJAX)
        /// </summary>
        [HttpPost("/siparis/garson-cagir")]
        [EnableRateLimiting("GarsonCagirPolicy")]
        public async Task<IActionResult> GarsonCagir()
        {
            var bilgi = GetOturumBilgi();
            if (bilgi == null) return Unauthorized();

            var masaNo = bilgi.Value.masaNo;
            _logger.LogInformation("Garson çağrıldı! Masa={MasaNo}", masaNo);

            await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("GarsonCagrisi", masaNo);

            return Json(new { success = true, message = "Garson çağrıldı!" });
        }

        /// <summary>
        /// Sepetten sipariş oluştur: POST /siparis/olustur (AJAX)
        /// </summary>
        [EnableRateLimiting("SiparisLimiti")]
        [HttpPost("/siparis/olustur")]
        public async Task<IActionResult> Olustur([FromBody] SiparisOlusturRequest? request)
        {
            var oturumId = GetOturumId();
            if (oturumId == null) return Unauthorized();

            try
            {
                var sepet = await _sepetService.GetSepetByOturumAsync(oturumId.Value);
                if (sepet == null)
                    return Json(new { success = false, message = "Sepet bulunamadı." });

                var normalizeNot = request?.Notlar?.Trim();
                if (string.IsNullOrWhiteSpace(normalizeNot))
                    normalizeNot = null;

                if (normalizeNot != null && normalizeNot.Length > SiparisNotMaxLength)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Sipariş notu en fazla {SiparisNotMaxLength} karakter olabilir."
                    });
                }

                var siparis = await _siparisService.SiparisOlusturAsync(sepet.Id, normalizeNot);

                var masaNo = await _db.Masalar
                    .Where(m => m.Id == siparis.MasaId)
                    .Select(m => m.MasaNo)
                    .FirstOrDefaultAsync();

                await Task.WhenAll(
                    _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGeldi", siparis.Id, masaNo),
                    _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGeldi", siparis.Id, masaNo),
                    _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisEklendi"),
                    _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisEklendi"),
                    _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi"),
                    _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi"),
                    _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi"),
                    _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisGuncellendi")
                );

                return Json(new
                {
                    success = true,
                    message = "Siparişiniz alındı!",
                    siparisId = siparis.Id,
                    gunlukSiparisNo = siparis.GunlukSiparisNo,
                    toplamTutar = siparis.ToplamTutar,
                    durum = siparis.Durum.ToString()
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş oluşturulurken beklenmeyen bir hata oluştu.");
                return Json(new { success = false, message = "İşlem sırasında bir hata oluştu." });
            }
        }

        /// <summary>
        /// Sipariş detayı: GET /siparis/{id} (AJAX)
        /// </summary>
        [HttpGet("/siparis/{id:int}")]
        public async Task<IActionResult> Detay(int id)
        {
            var oturumId = GetOturumId();
            if (oturumId == null) return Unauthorized();

            var siparis = await _siparisService.GetSiparisByOturumAsync(id, oturumId.Value);
            if (siparis == null)
                return Json(new { success = false, message = "Sipariş bulunamadı." });

            return Json(new
            {
                success = true,
                siparisId = siparis.Id,
                gunlukSiparisNo = siparis.GunlukSiparisNo,
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
        /// Aktif oturumun siparişleri: GET /siparis/siparislerim (AJAX)
        /// </summary>
        [HttpGet("/siparis/siparislerim")]
        public async Task<IActionResult> Siparislerim()
        {
            var oturumId = GetOturumId();
            if (oturumId == null) return Unauthorized();

            var siparisler = await _siparisService.GetSiparislerByOturumAsync(oturumId.Value);

            return Json(new
            {
                success = true,
                siparisler = siparisler.Select(s => new
                {
                    siparisId = s.Id,
                    gunlukSiparisNo = s.GunlukSiparisNo,
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
                var oturumId = GetOturumId();
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
                    return Json(new { success = false, message = "Åu an aktif oyun bulunmuyor." });
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
                var nowUtc = DateTime.UtcNow;
                var expiresAtUtc = nowUtc.AddMinutes(3);
                var carkToken = CreateGameToken(new GameTokenPayload
                {
                    SiparisId = siparis.Id,
                    OturumId = oturumId.Value,
                    OyunTipi = "CARKIFELEK",
                    CreatedAtUtc = nowUtc,
                    ExpiresAtUtc = expiresAtUtc
                });
                var hafizaToken = CreateGameToken(new GameTokenPayload
                {
                    SiparisId = siparis.Id,
                    OturumId = oturumId.Value,
                    OyunTipi = "HAFIZA",
                    CreatedAtUtc = nowUtc,
                    ExpiresAtUtc = expiresAtUtc,
                    HafizaEslesmeSirasi = memoryPairMap
                });
                var kaziToken = CreateGameToken(new GameTokenPayload
                {
                    SiparisId = siparis.Id,
                    OturumId = oturumId.Value,
                    OyunTipi = "KAZIKAZAN",
                    CreatedAtUtc = nowUtc,
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
                _logger.LogError(ex, "Oyun seçimleri yüklenirken hata oluştu. SiparisId={Id}", id);
                return Json(new { success = false, message = "İşlem sırasında bir hata oluştu." });
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
                var oturumId = GetOturumId();
                if (oturumId == null) return Unauthorized();

                // P5.2: Gün kapalı kontrolü
                var businessDate = DateTimeHelper.GetTurkeyBusinessDayUtc(DateTime.UtcNow);
                var gunKapali = await _db.GunSonuRaporlari.AnyAsync(r => r.Tarih == businessDate);
                if (gunKapali)
                {
                    return Json(new { success = false, message = "Gün sonu kapatılmış, oyun oynanamaz." });
                }

                var siparis = await _db.Siparisler
                    .Include(s => s.SiparisDetaylar)
                    .FirstOrDefaultAsync(s => s.Id == id && s.OturumId == oturumId.Value);

                if (siparis == null)
                    return Json(new { success = false, message = "Sipariş bulunamadı." });

                if (siparis.Durum == SiparisDurum.Iptal || siparis.Durum == SiparisDurum.Iade || siparis.Durum == SiparisDurum.TamOdendi)
                    return Json(new { success = false, message = "Oyun bu sipariş durumunda oynanamaz." });

                if (siparis.OyunOynandiMi)
                    return Json(new { success = false, message = "Bu sipariş için daha önce şansınızı denediniz." });

                // P5.2: Kısmen veya tamamen ödenmiş kalem varsa oyunu reddet
                if (siparis.SiparisDetaylar.Any(d => d.Durum == SiparisDurum.TamOdendi || d.Durum == SiparisDurum.KismiOdendi))
                {
                    return Json(new { success = false, message = "Kısmen veya tamamen ödenmiş siparişlerde oyun oynanamaz." });
                }

                if (!TryReadGameToken(request.OyunToken, out var tokenPayload) || tokenPayload == null)
                    return Json(new { success = false, message = "Geçersiz oyun oturumu. Lütfen oyunu tekrar açın." });

                if (tokenPayload.SiparisId != siparis.Id || tokenPayload.OturumId != oturumId.Value)
                    return Json(new { success = false, message = "Oyun oturumu bu siparişe ait değil." });

                if (tokenPayload.ExpiresAtUtc < DateTime.UtcNow)
                    return Json(new { success = false, message = "Oyun süresi doldu. Lütfen tekrar deneyin." });

                var secilenTip = tokenPayload.OyunTipi;

                // P5.1: Hafıza oyunu için süre ve hamle kontrolü
                if (secilenTip == "HAFIZA")
                {
                    var gecenSure = (DateTime.UtcNow - tokenPayload.CreatedAtUtc).TotalSeconds;
                    if (gecenSure < 5)
                    {
                        return Json(new { success = false, message = "Oyun çok kısa sürede tamamlandı. Lütfen geçerli bir süreyle oynayın." });
                    }

                    if (!request.BasariliMi)
                    {
                        siparis.OyunOynandiMi = true;
                        await _db.SaveChangesAsync();
                        return Json(new { success = true, kazandiMi = false, message = "Bu turda kazanamadınız. Bir dahaki sefere bol şans!" });
                    }

                    var validMoves = ValidateMemoryMoves(tokenPayload.HafizaEslesmeSirasi, request.HafizaHamleleri);
                    if (!validMoves)
                    {
                        siparis.OyunOynandiMi = true;
                        await _db.SaveChangesAsync();
                        return Json(new { success = true, kazandiMi = false, message = "Hafıza oyunu doğrulanamadı. Ödül verilemedi." });
                    }
                }

                // P5.2: Concurrency koruması - OyunOynandiMi atomik olarak ayarlanır
                if (_db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
                {
                    var etkilenen = await _db.Siparisler
                        .Where(s => s.Id == id && !s.OyunOynandiMi)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.OyunOynandiMi, true));

                    if (etkilenen == 0)
                        return Json(new { success = false, message = "Bu sipariş için daha önce şansınızı denediniz." });

                    siparis.OyunOynandiMi = true;
                }
                else
                {
                    if (siparis.OyunOynandiMi)
                        return Json(new { success = false, message = "Bu sipariş için daha önce şansınızı denediniz." });

                    siparis.OyunOynandiMi = true;
                }

                var oyun = await _db.OyunAyarlar
                    .Include(o => o.Oduller)
                    .FirstOrDefaultAsync(o => o.AktifMi && o.Tip == secilenTip);

                if (oyun == null || !oyun.Oduller.Any())
                {
                    await _db.SaveChangesAsync();
                    return Json(new { success = true, kazandiMi = false, message = "Seçtiğiniz oyunda şu an ödül havuzu yok." });
                }

                // P5.1: Sonuç sunucuda RandomNumberGenerator ile belirlenir
                var oduller = oyun.Oduller.OrderBy(o => o.Id).ToList();
                var sansDegeri = (decimal)RandomNumberGenerator.GetInt32(0, 100000) / 1000M; // 0..100%
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

                if (kazanilanOdul == null && oduller.Any())
                {
                    kazanilanOdul = oduller.Last();
                    hedefIndex = oduller.Count - 1;
                }

                // Boş dilim / boş sonuç kontrolü
                if (kazanilanOdul == null || (kazanilanOdul.IndirimYuzdesi <= 0 && kazanilanOdul.IndirimTutari <= 0))
                {
                    var bosIndex = kazanilanOdul != null ? hedefIndex : oduller.FindIndex(o => o.IndirimYuzdesi <= 0 && o.IndirimTutari <= 0);
                    if (bosIndex < 0) bosIndex = 0;
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

                // P5.2: İndirimi yalnızca ödenmemiş kalemlere uygula ve ToplamTutar'ı yeniden hesapla
                var odenmemisKalemler = siparis.SiparisDetaylar
                    .Where(d => d.Durum != SiparisDurum.Iptal && d.Durum != SiparisDurum.Iade &&
                                d.Durum != SiparisDurum.TamOdendi && d.Durum != SiparisDurum.KismiOdendi)
                    .ToList();

                decimal kazanilanIndirimTutari = 0;
                var eskiToplam = odenmemisKalemler.Sum(d => d.BirimFiyat * d.Adet);

                if (kazanilanOdul.IndirimYuzdesi > 0)
                {
                    foreach (var d in odenmemisKalemler)
                    {
                        d.BirimFiyat = Math.Round(d.BirimFiyat * (1 - kazanilanOdul.IndirimYuzdesi / 100m), 2, MidpointRounding.AwayFromZero);
                    }
                    var yeniToplam = odenmemisKalemler.Sum(d => d.BirimFiyat * d.Adet);
                    kazanilanIndirimTutari = Math.Max(0, eskiToplam - yeniToplam);
                }
                else if (kazanilanOdul.IndirimTutari > 0)
                {
                    kazanilanIndirimTutari = Math.Min(eskiToplam, kazanilanOdul.IndirimTutari);
                    if (eskiToplam > 0)
                    {
                        decimal kalanIndirim = kazanilanIndirimTutari;
                        for (int i = 0; i < odenmemisKalemler.Count; i++)
                        {
                            var d = odenmemisKalemler[i];
                            decimal kalemToplam = d.BirimFiyat * d.Adet;
                            decimal pay = i == odenmemisKalemler.Count - 1
                                ? kalanIndirim
                                : Math.Round(kazanilanIndirimTutari * (kalemToplam / eskiToplam), 2, MidpointRounding.AwayFromZero);

                            pay = Math.Min(pay, kalanIndirim);
                            kalanIndirim -= pay;

                            if (d.Adet > 0)
                            {
                                var birimIndirim = pay / d.Adet;
                                d.BirimFiyat = Math.Max(0, Math.Round(d.BirimFiyat - birimIndirim, 2, MidpointRounding.AwayFromZero));
                            }
                        }
                    }
                }

                // Siparişin toplam tutarını güncel kalemlerden yeniden hesapla
                siparis.ToplamTutar = siparis.SiparisDetaylar
                    .Where(d => d.Durum != SiparisDurum.Iptal && d.Durum != SiparisDurum.Iade)
                    .Sum(d => d.BirimFiyat * d.Adet);

                if (siparis.ToplamTutar < 0) siparis.ToplamTutar = 0;

                _db.KazanilanIndirimler.Add(new KazanilanIndirim
                {
                    SiparisId = siparis.Id,
                    OdulTanim = kazanilanOdul.OdulTanim,
                    UgulananIndirimTutari = kazanilanIndirimTutari,
                    KazanmaTarihi = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await Task.WhenAll(
                    _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi"),
                    _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi"),
                    _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi"),
                    _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisGuncellendi")
                );

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
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oyun sonuçlandırılırken hata oluştu. SiparisId={Id}", id);
                return Json(new { success = false, message = "İşlem sırasında bir hata oluştu." });
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
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAtUtc { get; set; }
        public List<int>? HafizaEslesmeSirasi { get; set; }
    }
}



