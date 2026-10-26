using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Helpers;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class OdemeService : IOdemeService
    {
        private readonly QRMenuDbContext _context;
        private readonly ILogger<OdemeService> _logger;

        public OdemeService(QRMenuDbContext context, ILogger<OdemeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> ParcaliOdemeAsync(int masaId, List<int> siparisDetayIds, string odemeTipi)
        {
            if (siparisDetayIds == null || !siparisDetayIds.Any()) return false;

            var supportsTransactions = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            if (supportsTransactions)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(() => ExecuteParcaliOdemeAsync(masaId, siparisDetayIds, odemeTipi, true));
            }

            return await ExecuteParcaliOdemeAsync(masaId, siparisDetayIds, odemeTipi, false);
        }

        private async Task<bool> ExecuteParcaliOdemeAsync(int masaId, List<int> siparisDetayIds, string odemeTipi, bool useTransaction)
        {
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            try
            {
                // P1.11: Geçersiz ödeme tipi kontrolü
                if (!Enum.TryParse<OdemeTipi>(odemeTipi, true, out var tip))
                {
                    _logger.LogWarning("Geçersiz ödeme tipi ile ödeme denendi: {OdemeTipi}", odemeTipi);
                    return false;
                }

                if (useTransaction)
                {
                    transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                }

                // P1.5: Gün sonu kapatılmış mı kontrolü - Ödeme tarihine (şimdi) göre
                var bugunRaporTarihi = DateTimeHelper.GetTurkeyBusinessDayUtc(DateTime.UtcNow);
                var kapaliGunVar = await _context.GunSonuRaporlari.AnyAsync(r => r.Tarih == bugunRaporTarihi);
                if (kapaliGunVar)
                    throw new InvalidOperationException("Gün sonu raporu kapatılmış, ödeme işlemi yapılamaz.");

                // Seçilen ürünleri bul
                var distinctIds = siparisDetayIds.Distinct().ToList();
                var detaylar = await _context.SiparisDetaylar
                    .Include(sd => sd.Siparis)
                    .Where(sd => distinctIds.Contains(sd.Id) && sd.Siparis.MasaId == masaId)
                    .ToListAsync();

                if (!detaylar.Any() || detaylar.Count != distinctIds.Count)
                    throw new InvalidOperationException("Ödenecek ürün bulunamadı.");

                // P1.4: Yalnızca TeslimEdildi ve KismiOdendi kalemler ödenebilir
                if (detaylar.Any(d => d.Durum != SiparisDurum.TeslimEdildi && d.Durum != SiparisDurum.KismiOdendi))
                {
                    _logger.LogWarning("Ödeme başarısız: Teslim edilmemiş veya zaten ödenmiş/iptal kalemler içeriyor.");
                    return false;
                }

                // P1.3: Concurrency kilidi ve durum güncelleme
                if (_context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
                {
                    var etkilenen = await _context.SiparisDetaylar
                        .Where(d => distinctIds.Contains(d.Id) && (d.Durum == SiparisDurum.TeslimEdildi || d.Durum == SiparisDurum.KismiOdendi))
                        .ExecuteUpdateAsync(s => s.SetProperty(d => d.Durum, SiparisDurum.TamOdendi));

                    if (etkilenen != distinctIds.Count)
                    {
                        if (transaction != null) await transaction.RollbackAsync();
                        return false;
                    }
                }
                else
                {
                    foreach (var d in detaylar)
                    {
                        d.Durum = SiparisDurum.TamOdendi;
                    }
                }

                decimal odenenTutar = 0;
                var siparisBazliTutarlar = new Dictionary<int, decimal>();

                var siparisIdler = detaylar
                    .Select(d => d.SiparisId)
                    .Distinct()
                    .ToList();

                var siparisBazToplamlari = await _context.SiparisDetaylar
                    .Where(sd => siparisIdler.Contains(sd.SiparisId) &&
                                 sd.Durum != SiparisDurum.Iptal &&
                                 sd.Durum != SiparisDurum.Iade)
                    .GroupBy(sd => sd.SiparisId)
                    .Select(g => new
                    {
                        SiparisId = g.Key,
                        BazToplam = g.Sum(sd => sd.BirimFiyat * sd.Adet)
                    })
                    .ToDictionaryAsync(x => x.SiparisId, x => x.BazToplam);

                var siparisMap = detaylar
                    .Select(d => d.Siparis)
                    .DistinctBy(s => s.Id)
                    .ToDictionary(s => s.Id, s => s);

                foreach (var siparisId in siparisIdler)
                {
                    var siparis = siparisMap[siparisId];
                    var siparisDetaylari = detaylar.Where(d => d.SiparisId == siparisId).ToList();

                    var bazToplam = siparisBazToplamlari.TryGetValue(siparisId, out var bToplam) ? bToplam : 0m;
                    decimal oran = (bazToplam > 0) ? (siparis.ToplamTutar / bazToplam) : 1m;

                    var oncekiOdemeler = await _context.Odemeler
                        .Where(o => o.SiparisId == siparisId)
                        .SumAsync(o => o.Tutar);

                    // Bu siparişte bu işlemden sonra ödenmemiş kalem kalıyor mu?
                    var kalanKalemSayisi = await _context.SiparisDetaylar
                        .Where(sd => sd.SiparisId == siparisId &&
                                     !distinctIds.Contains(sd.Id) &&
                                     sd.Durum != SiparisDurum.TamOdendi &&
                                     sd.Durum != SiparisDurum.Iptal &&
                                     sd.Durum != SiparisDurum.Iade)
                        .CountAsync();

                    decimal siparisBuOdemeTutari;
                    if (kalanKalemSayisi == 0)
                    {
                        // Kuruş farkı kalmaması için siparişin kalan borcunun tamamı ödenir
                        siparisBuOdemeTutari = siparis.ToplamTutar - oncekiOdemeler;
                    }
                    else
                    {
                        decimal kalemlerToplami = 0m;
                        foreach (var d in siparisDetaylari)
                        {
                            var kalemTutar = Math.Round((d.BirimFiyat * d.Adet) * oran, 2, MidpointRounding.AwayFromZero);
                            kalemlerToplami += kalemTutar;
                        }
                        siparisBuOdemeTutari = kalemlerToplami;
                    }

                    if (siparisBuOdemeTutari > 0)
                    {
                        siparisBazliTutarlar[siparisId] = siparisBuOdemeTutari;
                        odenenTutar += siparisBuOdemeTutari;
                    }
                }

                // P1.4: Ödenecek tutar <= 0 ise false dön
                if (odenenTutar <= 0)
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    return false;
                }

                // Odeme kayıtlarını oluştur
                foreach (var kvp in siparisBazliTutarlar)
                {
                    var odeme = new Odeme
                    {
                        SiparisId = kvp.Key,
                        Tutar = kvp.Value,
                        OdemeTipi = tip,
                        OdemeTarihi = DateTime.UtcNow
                    };
                    _context.Odemeler.Add(odeme);
                }

                await _context.SaveChangesAsync();

                // P1.3: Sipariş durumlarını ve RowVersion güncelle
                var etkilenenSiparisIdler = siparisBazliTutarlar.Keys.ToList();
                var nowUtc = DateTime.UtcNow;

                foreach (var siparisId in etkilenenSiparisIdler)
                {
                    var sip = siparisMap[siparisId];

                    var odemeBekleyenVarMi = await _context.SiparisDetaylar
                        .AnyAsync(sd => sd.SiparisId == siparisId &&
                                        sd.Durum != SiparisDurum.TamOdendi &&
                                        sd.Durum != SiparisDurum.Iptal &&
                                        sd.Durum != SiparisDurum.Iade);

                    if (!odemeBekleyenVarMi)
                    {
                        sip.Durum = SiparisDurum.TamOdendi;
                    }
                    else
                    {
                        sip.Durum = SiparisDurum.KismiOdendi;
                    }

                    sip.GuncellemeTarihi = nowUtc;
                    sip.RowVersion = Guid.NewGuid().ToByteArray();
                }

                await _context.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();

                _logger.LogInformation("Kasa tahsilatı yapıldı. MasaId={MasaId}, Tutar={Tutar}, Tip={Tip}", masaId, odenenTutar, odemeTipi);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kasa ödeme işlemi sırasında hata oluştu!");
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
    }
}
