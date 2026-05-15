using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class SepetService : ISepetService
    {
        private readonly QRMenuDbContext _context;
        private readonly ILogger<SepetService> _logger;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        private static string SepetCacheKey(int sepetId) => $"sepet:{sepetId}";
        private static string OturumSepetCacheKey(int oturumId) => $"sepet:oturum:{oturumId}";

        public SepetService(QRMenuDbContext context, ILogger<SepetService> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        private void InvalidateCache(int sepetId, int? oturumId = null)
        {
            _cache.Remove(SepetCacheKey(sepetId));
            if (oturumId.HasValue)
                _cache.Remove(OturumSepetCacheKey(oturumId.Value));
        }

        /// <summary>
        /// Oturumun sepetini getir, yoksa yeni oluştur
        /// </summary>
        public async Task<Sepet> GetOrCreateSepetAsync(int oturumId)
        {
            var cacheKey = OturumSepetCacheKey(oturumId);
            if (_cache.TryGetValue(cacheKey, out int cachedSepetId))
            {
                var cached = await _context.Sepetler
                    .Include(s => s.SepetDetaylar)
                        .ThenInclude(sd => sd.Urun)
                    .Include(s => s.SepetDetaylar)
                        .ThenInclude(sd => sd.UrunVaryasyon)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(s => s.Id == cachedSepetId);
                if (cached != null) return cached;
            }

            var sepet = await _context.Sepetler
                .Include(s => s.SepetDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Include(s => s.SepetDetaylar)
                    .ThenInclude(sd => sd.UrunVaryasyon)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.OturumId == oturumId);

            if (sepet == null)
            {
                sepet = new Sepet
                {
                    OturumId = oturumId,
                    ToplamTutar = 0
                };
                _context.Sepetler.Add(sepet);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Yeni sepet oluşturuldu. OturumId={OturumId}, SepetId={SepetId}", oturumId, sepet.Id);
            }

            _cache.Set(cacheKey, sepet.Id, CacheDuration);
            return sepet;
        }

        /// <summary>
        /// Sepete ürün ekle — aynı ürün+opsiyon varsa adet artır
        /// </summary>
        public async Task<SepetDetay> AddItemAsync(int sepetId, int urunId, int adet, List<int>? opsiyonIds = null, decimal? forceDiscountRate = null, int? urunVaryasyonId = null)
        {
            if (adet <= 0)
                throw new InvalidOperationException("Adet 1 veya daha buyuk olmalidir.");

            var urun = await _context.Urunler.FindAsync(urunId);
            if (urun == null || !urun.AktifMi)
                throw new InvalidOperationException("Ürün bulunamadı veya aktif değil.");

            UrunVaryasyon? varyasyon = null;
            if (urunVaryasyonId.HasValue)
            {
                varyasyon = await _context.UrunVaryasyonlar
                    .FirstOrDefaultAsync(v => v.Id == urunVaryasyonId.Value && v.UrunId == urunId);

                if (varyasyon == null || !varyasyon.AktifMi)
                    throw new InvalidOperationException("Urun varyasyonu bulunamadi veya aktif degil.");
            }

            var mevcutSepetAdet = await _context.SepetDetaylar
                .Where(sd => sd.SepetId == sepetId
                    && sd.UrunId == urunId
                    && sd.UrunVaryasyonId == urunVaryasyonId)
                .SumAsync(sd => sd.Adet);

            var stokAdet = varyasyon?.StokAdet ?? urun.StokAdet;
            if (stokAdet <= 0)
                throw new InvalidOperationException("Stok tukenmistir.");

            if (mevcutSepetAdet + adet > stokAdet)
                throw new InvalidOperationException($"Stokta sadece {stokAdet} adet kalmistir.");

            // Opsiyon bilgilerini al
            string? opsiyonJson = null;
            decimal opsiyonEkFiyat = 0;

            if (opsiyonIds != null && opsiyonIds.Count > 0)
            {
                var opsiyonlar = await _context.Opsiyonlar
                    .Where(o => opsiyonIds.Contains(o.Id))
                    .ToListAsync();

                opsiyonEkFiyat = opsiyonlar.Sum(o => o.EkFiyat);
                opsiyonJson = JsonSerializer.Serialize(
                    opsiyonlar.Select(o => new { o.Id, o.Ad, o.Grup, o.EkFiyat })
                );
            }

            // Birim fiyat hesapla
            decimal basePrice = urun.Fiyat;
            if (forceDiscountRate.HasValue && forceDiscountRate.Value > 0)
            {
                basePrice = Math.Round(basePrice * (1 - forceDiscountRate.Value / 100m), 2);
            }
            
            decimal birimFiyat = basePrice + (varyasyon?.EkFiyat ?? 0) + opsiyonEkFiyat;

            // Aynı ürün + aynı opsiyonlarla zaten sepette var mı?
            var mevcutDetay = await _context.SepetDetaylar
                .FirstOrDefaultAsync(sd => sd.SepetId == sepetId
                                        && sd.UrunId == urunId
                                        && sd.UrunVaryasyonId == urunVaryasyonId
                                        && sd.SeciliOpsiyonlar == opsiyonJson);

            if (mevcutDetay != null)
            {
                // Aynı ürün varsa adet artır
                mevcutDetay.Adet += adet;
                // Birim fiyatı güncelle (indirim oranları değişmiş olabilir)
                mevcutDetay.BirimFiyat = birimFiyat;
                _logger.LogInformation("Sepetteki ürün adedi artırıldı. DetayId={DetayId}, YeniAdet={Adet}", mevcutDetay.Id, mevcutDetay.Adet);
            }
            else
            {
                // Yeni ürün ekle
                mevcutDetay = new SepetDetay
                {
                    SepetId = sepetId,
                    UrunId = urunId,
                    UrunVaryasyonId = urunVaryasyonId,
                    Adet = adet,
                    BirimFiyat = birimFiyat,
                    SeciliOpsiyonlar = opsiyonJson
                };
                _context.SepetDetaylar.Add(mevcutDetay);
                _logger.LogInformation("Sepete yeni ürün eklendi. SepetId={SepetId}, UrunId={UrunId}, Adet={Adet}", sepetId, urunId, adet);
            }

            // Toplam tutarı bellek üzerinde güncelle (DB'ye ekstra gitmeden)
            // EF Core "Relationship Fix-up" sayesinde Include ile gelen listede yeni eklenen ürün zaten bulunur.
            var sepet = await _context.Sepetler
                .Include(s => s.SepetDetaylar)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == sepetId);

            if (sepet != null)
            {
                sepet.ToplamTutar = sepet.SepetDetaylar.Sum(sd => sd.BirimFiyat * sd.Adet);
            }

            await _context.SaveChangesAsync();

            InvalidateCache(sepetId, sepet?.OturumId);
            return mevcutDetay;
        }

        /// <summary>
        /// Sepetten ürün çıkar
        /// </summary>
        public async Task<bool> RemoveItemAsync(int sepetDetayId)
        {
            var detay = await _context.SepetDetaylar.FindAsync(sepetDetayId);
            if (detay == null) return false;

            var sepetId = detay.SepetId;
            var sepet = await _context.Sepetler
                .Include(s => s.SepetDetaylar)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == sepetId);

            if (sepet != null)
            {
                sepet.SepetDetaylar.Remove(detay);
                sepet.ToplamTutar = sepet.SepetDetaylar.Sum(sd => sd.BirimFiyat * sd.Adet);
            }

            _context.SepetDetaylar.Remove(detay);
            await _context.SaveChangesAsync();

            InvalidateCache(sepetId, sepet?.OturumId);
            _logger.LogInformation("Sepetten ürün çıkarıldı. DetayId={DetayId}", sepetDetayId);
            return true;
        }

        /// <summary>
        /// Adet güncelle (0 veya altı → ürünü sil)
        /// </summary>
        public async Task<bool> RemoveItemAsync(int sepetDetayId, int oturumId)
        {
            var sahipMi = await _context.SepetDetaylar
                .AnyAsync(sd => sd.Id == sepetDetayId && sd.Sepet.OturumId == oturumId);

            if (!sahipMi) return false;

            return await RemoveItemAsync(sepetDetayId);
        }

        public async Task<bool> UpdateQuantityAsync(int sepetDetayId, int yeniAdet)
        {
            var detay = await _context.SepetDetaylar.FindAsync(sepetDetayId);
            if (detay == null) return false;

            if (yeniAdet <= 0)
                return await RemoveItemAsync(sepetDetayId);

            var stokAdet = detay.UrunVaryasyonId.HasValue
                ? await _context.UrunVaryasyonlar
                    .Where(v => v.Id == detay.UrunVaryasyonId.Value && v.AktifMi)
                    .Select(v => (int?)v.StokAdet)
                    .FirstOrDefaultAsync()
                : await _context.Urunler
                    .Where(u => u.Id == detay.UrunId && u.AktifMi)
                    .Select(u => (int?)u.StokAdet)
                    .FirstOrDefaultAsync();

            if (!stokAdet.HasValue || stokAdet.Value <= 0)
                throw new InvalidOperationException("Stok tukenmistir, urunu sepetten cikarin.");

            var digerSepetAdet = await _context.SepetDetaylar
                .Where(sd => sd.SepetId == detay.SepetId
                    && sd.Id != detay.Id
                    && sd.UrunId == detay.UrunId
                    && sd.UrunVaryasyonId == detay.UrunVaryasyonId)
                .SumAsync(sd => sd.Adet);

            if (digerSepetAdet + yeniAdet > stokAdet.Value)
                throw new InvalidOperationException($"Stokta sadece {stokAdet.Value} adet kalmistir.");

            detay.Adet = yeniAdet;

            var sepet = await _context.Sepetler
                .Include(s => s.SepetDetaylar)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == detay.SepetId);

            if (sepet != null)
            {
                sepet.ToplamTutar = sepet.SepetDetaylar.Sum(sd => sd.BirimFiyat * sd.Adet);
            }

            await _context.SaveChangesAsync();

            InvalidateCache(sepet?.Id ?? 0, sepet?.OturumId);
            _logger.LogInformation("Sepet ürün adedi güncellendi. DetayId={DetayId}, YeniAdet={Adet}", sepetDetayId, yeniAdet);
            return true;
        }

        /// <summary>
        /// Sepeti tamamen temizle
        /// </summary>
        public async Task<bool> UpdateQuantityAsync(int sepetDetayId, int yeniAdet, int oturumId)
        {
            var sahipMi = await _context.SepetDetaylar
                .AnyAsync(sd => sd.Id == sepetDetayId && sd.Sepet.OturumId == oturumId);

            if (!sahipMi) return false;

            return await UpdateQuantityAsync(sepetDetayId, yeniAdet);
        }

        public async Task ClearSepetAsync(int sepetId)
        {
            var detaylar = await _context.SepetDetaylar
                .Where(sd => sd.SepetId == sepetId)
                .ToListAsync();

            _context.SepetDetaylar.RemoveRange(detaylar);

            var sepet = await _context.Sepetler.FindAsync(sepetId);
            if (sepet != null)
                sepet.ToplamTutar = 0;

            await _context.SaveChangesAsync();

            InvalidateCache(sepetId, sepet?.OturumId);
            _logger.LogInformation("Sepet temizlendi. SepetId={SepetId}, SilinenUrun={Count}", sepetId, detaylar.Count);
        }

        /// <summary>
        /// Sepeti detaylarıyla birlikte getir
        /// </summary>
        public async Task<Sepet?> GetSepetWithDetailsAsync(int sepetId)
        {
            return await _context.Sepetler
                .Include(s => s.SepetDetaylar)
                    .ThenInclude(sd => sd.Urun)
                        .ThenInclude(u => u.Kategori)
                .Include(s => s.SepetDetaylar)
                    .ThenInclude(sd => sd.UrunVaryasyon)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == sepetId);
        }

        /// <summary>
        /// Oturum ID'ye göre sepeti getir
        /// </summary>
        public async Task<Sepet?> GetSepetByOturumAsync(int oturumId)
        {
            return await _context.Sepetler
                .Include(s => s.SepetDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Include(s => s.SepetDetaylar)
                    .ThenInclude(sd => sd.UrunVaryasyon)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.OturumId == oturumId);
        }

        public async Task<SepetStokKontrolDto> ValidateStockAsync(int sepetId)
        {
            var sonuc = new SepetStokKontrolDto();
            var sepet = await GetSepetWithDetailsAsync(sepetId);
            if (sepet == null || !sepet.SepetDetaylar.Any())
                return sonuc;

            foreach (var grup in sepet.SepetDetaylar.GroupBy(sd => new { sd.UrunId, sd.UrunVaryasyonId }))
            {
                var toplamAdet = grup.Sum(sd => sd.Adet);
                var ilkDetay = grup.First();
                var stokAdet = ilkDetay.UrunVaryasyon?.StokAdet ?? ilkDetay.Urun.StokAdet;
                var aktifMi = ilkDetay.UrunVaryasyon?.AktifMi ?? ilkDetay.Urun.AktifMi;

                if (!aktifMi || stokAdet <= 0)
                {
                    foreach (var detay in grup)
                    {
                        sonuc.Sorunlar.Add(new SepetStokSorunDto
                        {
                            SepetDetayId = detay.Id,
                            UrunId = detay.UrunId,
                            UrunVaryasyonId = detay.UrunVaryasyonId,
                            UrunAd = detay.Urun.Ad,
                            SepettekiAdet = detay.Adet,
                            StokAdet = Math.Max(stokAdet, 0),
                            StokTukendi = true,
                            Mesaj = "Stok tukenmistir, urunu sepetten cikarin."
                        });
                    }

                    continue;
                }

                if (toplamAdet > stokAdet)
                {
                    foreach (var detay in grup)
                    {
                        sonuc.Sorunlar.Add(new SepetStokSorunDto
                        {
                            SepetDetayId = detay.Id,
                            UrunId = detay.UrunId,
                            UrunVaryasyonId = detay.UrunVaryasyonId,
                            UrunAd = detay.Urun.Ad,
                            SepettekiAdet = detay.Adet,
                            StokAdet = stokAdet,
                            StokTukendi = false,
                            Mesaj = $"Stokta sadece {stokAdet} adet kalmistir, lutfen miktari guncelleyin."
                        });
                    }
                }
            }

            return sonuc;
        }

        /// <summary>
        /// Sepet toplamını yeniden hesapla
        /// </summary>
        private async Task RecalculateTotalAsync(int sepetId)
        {
            var sepet = await _context.Sepetler
                .Include(s => s.SepetDetaylar)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == sepetId);

            if (sepet != null)
            {
                sepet.ToplamTutar = sepet.SepetDetaylar.Sum(sd => sd.BirimFiyat * sd.Adet);
                await _context.SaveChangesAsync();
            }
        }
    }
}
