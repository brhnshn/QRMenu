using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class SiparisService : ISiparisService
    {
        private readonly QRMenuDbContext _context;
        private readonly ILogger<SiparisService> _logger;

        // State Machine: Hangi durumdan hangi durumlara geçilebilir?
        private static readonly Dictionary<SiparisDurum, SiparisDurum[]> _gecisKurallari = new()
        {
            [SiparisDurum.Sepette]       = new[] { SiparisDurum.Onaylandi },
            [SiparisDurum.Onaylandi]     = new[] { SiparisDurum.Hazirlaniyor, SiparisDurum.Iptal },
            [SiparisDurum.Hazirlaniyor]  = new[] { SiparisDurum.Hazir, SiparisDurum.Iptal },
            [SiparisDurum.Hazir]         = new[] { SiparisDurum.TeslimEdildi },
            [SiparisDurum.TeslimEdildi]  = new[] { SiparisDurum.KismiOdendi, SiparisDurum.TamOdendi, SiparisDurum.Iade },
            [SiparisDurum.KismiOdendi]   = new[] { SiparisDurum.TamOdendi },
            [SiparisDurum.TamOdendi]     = new[] { SiparisDurum.Iade },
            [SiparisDurum.Iptal]         = Array.Empty<SiparisDurum>(),
            [SiparisDurum.Iade]          = Array.Empty<SiparisDurum>()
        };

        public SiparisService(QRMenuDbContext context, ILogger<SiparisService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// State Machine: Belirli bir durumdan yapılabilecek geçişleri döner
        /// </summary>
        public IReadOnlyList<SiparisDurum> GecerliGecisler(SiparisDurum mevcutDurum)
        {
            return _gecisKurallari.TryGetValue(mevcutDurum, out var gecisler)
                ? gecisler
                : Array.Empty<SiparisDurum>();
        }

        /// <summary>
        /// Sepetteki ürünleri siparişe çevirir.
        /// Transaction ile atomik: sipariş oluşturma + sepet temizleme.
        /// </summary>
        public async Task<Siparis> SiparisOlusturAsync(int sepetId, string? notlar = null)
        {
            // Transaction desteği kontrol — InMemory provider'da transaction yok
            var supportsTransactions = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (supportsTransactions)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(() => ExecuteSiparisOlusturAsync(sepetId, notlar, true));
            }

            return await ExecuteSiparisOlusturAsync(sepetId, notlar, false);
        }

        private async Task<Siparis> ExecuteSiparisOlusturAsync(int sepetId, string? notlar, bool useTransaction)
        {
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            try
            {
                if (useTransaction)
                    transaction = await _context.Database.BeginTransactionAsync();

                var sepet = await _context.Sepetler
                    .Include(s => s.SepetDetaylar)
                        .ThenInclude(sd => sd.Urun)
                    .Include(s => s.Oturum)
                    .FirstOrDefaultAsync(s => s.Id == sepetId);

                if (sepet == null)
                    throw new InvalidOperationException("Sepet bulunamadı.");

                if (!sepet.SepetDetaylar.Any())
                    throw new InvalidOperationException("Sepet boş, sipariş oluşturulamaz.");

                // Sipariş oluştur
                var siparis = new Siparis
                {
                    MasaId = sepet.Oturum.MasaId,
                    OturumId = sepet.OturumId,
                    Durum = SiparisDurum.Onaylandi,
                    OlusturmaTarihi = DateTime.UtcNow,
                    ToplamTutar = sepet.SepetDetaylar.Sum(sd => sd.BirimFiyat * sd.Adet),
                    Notlar = notlar,
                    RowVersion = Guid.NewGuid().ToByteArray()
                };

                _context.Siparisler.Add(siparis);
                await _context.SaveChangesAsync();

                // SepetDetay → SiparisDetay kopyala
                foreach (var detay in sepet.SepetDetaylar)
                {
                    var siparisDetay = new SiparisDetay
                    {
                        SiparisId = siparis.Id,
                        UrunId = detay.UrunId,
                        Adet = detay.Adet,
                        BirimFiyat = detay.BirimFiyat,
                        SeciliOpsiyonlar = detay.SeciliOpsiyonlar,
                        Durum = SiparisDurum.Onaylandi
                    };
                    _context.SiparisDetaylar.Add(siparisDetay);
                }

                // Sepet detaylarını temizle
                _context.SepetDetaylar.RemoveRange(sepet.SepetDetaylar);
                sepet.ToplamTutar = 0;

                await _context.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();

                _logger.LogInformation(
                    "Sipariş oluşturuldu. SiparisId={SiparisId}, MasaId={MasaId}, Tutar={Tutar}",
                    siparis.Id, siparis.MasaId, siparis.ToplamTutar);

                return siparis;
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        /// <summary>
        /// Sipariş durumunu günceller — State Machine kurallarına uygun olmalı.
        /// RowVersion ile concurrency kontrolü.
        /// </summary>
        public async Task<Siparis> DurumGuncelleAsync(int siparisId, SiparisDurum yeniDurum)
        {
            var siparis = await _context.Siparisler.FindAsync(siparisId);
            if (siparis == null)
                throw new InvalidOperationException("Sipariş bulunamadı.");

            var gecerliGecisler = GecerliGecisler(siparis.Durum);
            if (!gecerliGecisler.Contains(yeniDurum))
            {
                throw new InvalidOperationException(
                    $"Geçersiz durum geçişi: {siparis.Durum} → {yeniDurum}. " +
                    $"İzin verilen: {string.Join(", ", gecerliGecisler)}");
            }

            var eskiDurum = siparis.Durum;
            siparis.Durum = yeniDurum;
            siparis.GuncellemeTarihi = DateTime.UtcNow;
            siparis.RowVersion = Guid.NewGuid().ToByteArray();

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex,
                    "Concurrency conflict! SiparisId={SiparisId}, Geçiş={EskiDurum}→{YeniDurum}",
                    siparisId, eskiDurum, yeniDurum);
                throw new InvalidOperationException(
                    "Bu sipariş başka bir kullanıcı tarafından güncellenmiş. Lütfen sayfayı yenileyip tekrar deneyin.", ex);
            }

            _logger.LogInformation(
                "Sipariş durumu güncellendi. SiparisId={SiparisId}, {EskiDurum} → {YeniDurum}",
                siparisId, eskiDurum, yeniDurum);

            return siparis;
        }

        /// <summary>
        /// Sipariş detayını getirir (SiparisDetaylar + Urun dahil)
        /// </summary>
        public async Task<Siparis?> GetSiparisAsync(int siparisId)
        {
            return await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Include(s => s.Masa)
                .FirstOrDefaultAsync(s => s.Id == siparisId);
        }

        /// <summary>
        /// Masanın aktif (iptal/iade dışı) siparişlerini getirir
        /// </summary>
        public async Task<List<Siparis>> GetSiparislerByMasaAsync(int masaId)
        {
            return await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Where(s => s.MasaId == masaId
                    && s.Durum != SiparisDurum.Iptal
                    && s.Durum != SiparisDurum.Iade)
                .OrderByDescending(s => s.OlusturmaTarihi)
                .ToListAsync();
        }

        /// <summary>
        /// Oturumun siparişlerini getirir
        /// </summary>
        public async Task<List<Siparis>> GetSiparislerByOturumAsync(int oturumId)
        {
            return await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Where(s => s.OturumId == oturumId)
                .OrderByDescending(s => s.OlusturmaTarihi)
                .ToListAsync();
        }

        /// <summary>
        /// Siparişi iptal eder — sadece Onaylandı veya Hazırlanıyor durumlarından iptal edilebilir
        /// </summary>
        public async Task<Siparis> IptalEtAsync(int siparisId)
        {
            return await DurumGuncelleAsync(siparisId, SiparisDurum.Iptal);
        }
    }
}
