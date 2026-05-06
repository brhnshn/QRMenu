using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class OdemeService : IOdemeService
    {
        private readonly QRMenuDbContext _context;
        private readonly ILogger<OdemeService> _logger;
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

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
                if (useTransaction)
                {
                    // ReadCommitted seviyesi ile Transaction oluştur
                    transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                }

                // Seçilen ürünleri bul ve "TamOdendi" (ya da Parçalı Ödendi? Ürün bazında TamÖdendi yapıyoruz) durumuna çek
                var detaylar = await _context.SiparisDetaylar
                    .Include(sd => sd.Siparis)
                    .Where(sd => siparisDetayIds.Contains(sd.Id) && sd.Siparis.MasaId == masaId)
                    .ToListAsync();

                if (!detaylar.Any())
                    throw new InvalidOperationException("Ödenecek ürün bulunamadı.");

                var kapaliGunTarihleri = detaylar
                    .Select(d => d.Siparis.OlusturmaTarihi)
                    .Distinct()
                    .Select(t => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t, DateTimeKind.Utc), _turkeyTz).Date, DateTimeKind.Utc))
                    .ToList();

                var kapaliGunVar = await _context.GunSonuRaporlari
                    .AnyAsync(r => kapaliGunTarihleri.Contains(r.Tarih));

                if (kapaliGunVar)
                    throw new InvalidOperationException("Gün sonu raporu kapatılmış siparişlerde ödeme işlemi yapılamaz.");

                decimal odenenTutar = 0;
                var siparisBazliTutarlar = new Dictionary<int, decimal>();

                // Siparişe uygulanan indirimleri ödeme anında satır tutarlarına oransal dağıt.
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

                var siparisOranlari = detaylar
                    .Select(d => d.Siparis)
                    .Where(s => s != null)
                    .DistinctBy(s => s!.Id)
                    .ToDictionary(
                        s => s!.Id,
                        s =>
                        {
                            var bazToplam = siparisBazToplamlari.TryGetValue(s!.Id, out var toplam)
                                ? toplam
                                : 0m;

                            if (bazToplam <= 0) return 1m;
                            return s.ToplamTutar / bazToplam;
                        });

                foreach (var detay in detaylar)
                {
                    if (detay.Durum == SiparisDurum.TamOdendi || detay.Durum == SiparisDurum.Iptal) continue;

                    detay.Durum = SiparisDurum.TamOdendi;
                    var oran = siparisOranlari.TryGetValue(detay.SiparisId, out var value) ? value : 1m;
                    var tutar = (detay.BirimFiyat * detay.Adet) * oran;
                    odenenTutar += tutar;
                    
                    if (!siparisBazliTutarlar.ContainsKey(detay.SiparisId))
                        siparisBazliTutarlar[detay.SiparisId] = 0;
                    siparisBazliTutarlar[detay.SiparisId] += tutar;
                }

                if (odenenTutar > 0)
                {
                    var tip = Enum.TryParse<OdemeTipi>(odemeTipi, out var result) ? result : OdemeTipi.Nakit;
                    
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
                }

                await _context.SaveChangesAsync();

                var etkilenenSiparisIdler = siparisBazliTutarlar.Keys.ToList();

                // Şimdi etkilenen siparişleri kontrol et, tamamı ödendiyse ana siparişi "TamOdendi" yap
                var odemeBekleyenSiparisIdler = await _context.SiparisDetaylar
                    .Where(sd => etkilenenSiparisIdler.Contains(sd.SiparisId) &&
                                 sd.Durum != SiparisDurum.TamOdendi &&
                                 sd.Durum != SiparisDurum.Iptal &&
                                 sd.Durum != SiparisDurum.Iade)
                    .Select(sd => sd.SiparisId)
                    .Distinct()
                    .ToListAsync();

                var tamamlananSiparisIdler = etkilenenSiparisIdler
                    .Except(odemeBekleyenSiparisIdler)
                    .ToList();

                if (tamamlananSiparisIdler.Any())
                {
                    var tamamlananSiparisler = await _context.Siparisler
                        .Where(s => tamamlananSiparisIdler.Contains(s.Id))
                        .ToListAsync();

                    foreach (var sip in tamamlananSiparisler)
                    {
                        sip.Durum = SiparisDurum.TamOdendi;
                    }
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
