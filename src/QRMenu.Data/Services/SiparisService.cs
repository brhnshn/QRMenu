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
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

        // State Machine: Hangi durumdan hangi durumlara geçilebilir?
        private static readonly Dictionary<SiparisDurum, SiparisDurum[]> _gecisKurallari = new()
        {
            [SiparisDurum.Sepette]       = new[] { SiparisDurum.Onaylandi },
            [SiparisDurum.Onaylandi]     = new[] { SiparisDurum.Hazirlaniyor, SiparisDurum.Iptal },
            [SiparisDurum.Hazirlaniyor]  = new[] { SiparisDurum.Hazir, SiparisDurum.Iptal },
            [SiparisDurum.Hazir]         = new[] { SiparisDurum.TeslimEdildi },
            [SiparisDurum.TeslimEdildi]  = new[] { SiparisDurum.KismiOdendi, SiparisDurum.TamOdendi, SiparisDurum.Iade, SiparisDurum.Iptal },
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

            if (supportsTransactions)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(() => ExecuteSiparisOlusturAsync(sepetId, notlar, true));
            }

            return await ExecuteSiparisOlusturAsync(sepetId, notlar, false);
        }

        /// <summary>
        /// Garson panelinden sepetsiz, anında masaya sipariş ekler.
        /// </summary>
        public async Task<Siparis> ManuelSiparisOlusturAsync(int masaId, List<QRMenu.Core.DTOs.ManuelSiparisDetayDto> urunler, string? notlar = null)
        {
            if (urunler == null || !urunler.Any())
                throw new InvalidOperationException("Ürün listesi boş olamaz.");

            var supportsTransactions = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            if (supportsTransactions)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(() => ExecuteManuelSiparisOlusturAsync(masaId, urunler, notlar, true));
            }

            return await ExecuteManuelSiparisOlusturAsync(masaId, urunler, notlar, false);
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
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(s => s.Id == sepetId);

                if (sepet == null)
                    throw new InvalidOperationException("Sepet bulunamadı.");

                if (!sepet.SepetDetaylar.Any())
                    throw new InvalidOperationException("Sepet boş, sipariş oluşturulamaz.");

                // Happy Hour kontrolü
                var hh = await HappyHourBilgisiGetirAsync();

                decimal tumTutar = 0;
                string? hhNot = null;
                var yeniDetaylar = new List<SiparisDetay>();

                // Tutarı hesapla ve detay listesini hazırla
                foreach (var detay in sepet.SepetDetaylar)
                {
                    // Sepette kullanıcıya ne gösterildiyse onu kullan.
                    decimal birimFiyat = detay.BirimFiyat;

                    // Sadece fişte "Happy Hour uygulandı" notunu göstermek için indirimi tespit et
                    decimal purePrice = detay.Urun.Fiyat;
                    try {
                        if (!string.IsNullOrEmpty(detay.SeciliOpsiyonlar) && detay.SeciliOpsiyonlar != "[]") {
                            var opsJson = System.Text.Json.JsonDocument.Parse(detay.SeciliOpsiyonlar);
                            foreach(var ops in opsJson.RootElement.EnumerateArray()) {
                                purePrice += ops.GetProperty("EkFiyat").GetDecimal();
                            }
                        }
                    } catch {}

                    if (birimFiyat < purePrice) 
                    {
                        var uygulananIndirim = Math.Round((1 - birimFiyat / purePrice) * 100, 0);
                        if(hhNot == null) hhNot = $"🎉 Happy Hour -%{uygulananIndirim} uygulandı.";
                    }

                    tumTutar += (birimFiyat * detay.Adet);

                    yeniDetaylar.Add(new SiparisDetay
                    {
                        UrunId = detay.UrunId,
                        Adet = detay.Adet,
                        BirimFiyat = birimFiyat, // İndirimli fiyatı yansıt
                        SeciliOpsiyonlar = detay.SeciliOpsiyonlar,
                        Durum = SiparisDurum.Onaylandi
                    });
                }

                // Siparişi oluştur
                var siparis = new Siparis
                {
                    MasaId = sepet.Oturum.MasaId,
                    OturumId = sepet.OturumId,
                    Durum = SiparisDurum.Onaylandi,
                    OlusturmaTarihi = DateTime.UtcNow,
                    ToplamTutar = tumTutar,
                    Notlar = hhNot != null ? $"{hhNot} {(notlar != null ? "| " + notlar : "")}" : notlar,
                    RowVersion = Guid.NewGuid().ToByteArray()
                };

                _context.Siparisler.Add(siparis);
                await _context.SaveChangesAsync();

                // Id aldıktan sonra detaylara ekle
                foreach (var d in yeniDetaylar)
                {
                    d.SiparisId = siparis.Id;
                    _context.SiparisDetaylar.Add(d);
                }

                // Sepeti temizle
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

        private async Task<Siparis> ExecuteManuelSiparisOlusturAsync(int masaId, List<QRMenu.Core.DTOs.ManuelSiparisDetayDto> urunler, string? notlar, bool useTransaction)
        {
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            try
            {
                if (useTransaction)
                    transaction = await _context.Database.BeginTransactionAsync();

                decimal toplamTutar = 0;
                var siparisDetaylar = new List<SiparisDetay>();
                var indirimUygulandi = false;

                // Happy Hour kontrolü
                var hh = await HappyHourBilgisiGetirAsync();

                foreach (var item in urunler)
                {
                    var urunDB = await _context.Urunler.FindAsync(item.UrunId);
                    if (urunDB == null || !urunDB.AktifMi)
                        throw new InvalidOperationException($"Ürün bulunamadı veya pasif: {item.UrunId}");

                    decimal birimFiyat = urunDB.Fiyat;
                    string? seciliOpsiyonlarJson = null;

                    if (item.OpsiyonIds != null && item.OpsiyonIds.Any())
                    {
                        var opsiyonlar = await _context.Opsiyonlar
                            .Where(o => item.OpsiyonIds.Contains(o.Id))
                            .ToListAsync();
                        
                        birimFiyat += opsiyonlar.Sum(o => o.EkFiyat);
                        seciliOpsiyonlarJson = System.Text.Json.JsonSerializer.Serialize(opsiyonlar.Select(o => new { o.Id, o.Ad, o.EkFiyat }));
                    }

                    if (hh.IndirimOrani > 0 && (!hh.UrunIds.Any() || hh.UrunIds.Contains(item.UrunId)))
                    {
                        birimFiyat = Math.Round(birimFiyat * (1 - hh.IndirimOrani / 100m), 2);
                        indirimUygulandi = true;
                    }

                    toplamTutar += (birimFiyat * item.Adet);

                    siparisDetaylar.Add(new SiparisDetay
                    {
                        UrunId = urunDB.Id,
                        Adet = item.Adet,
                        BirimFiyat = birimFiyat,
                        SeciliOpsiyonlar = seciliOpsiyonlarJson,
                        Durum = SiparisDurum.Onaylandi
                    });
                }

                var siparis = new Siparis
                {
                    MasaId = masaId,
                    OturumId = null,
                    Durum = SiparisDurum.Onaylandi,
                    OlusturmaTarihi = DateTime.UtcNow,
                    ToplamTutar = toplamTutar,
                    Notlar = indirimUygulandi
                        ? $"🎉 Happy Hour -%{hh.IndirimOrani} uygulandı. {(notlar != null ? "| " + notlar : "")}"
                        : notlar,
                    RowVersion = Guid.NewGuid().ToByteArray()
                };

                _context.Siparisler.Add(siparis);
                await _context.SaveChangesAsync(); // siparis.Id almak için

                foreach (var detay in siparisDetaylar)
                {
                    detay.SiparisId = siparis.Id;
                    _context.SiparisDetaylar.Add(detay);
                }

                await _context.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();

                _logger.LogInformation(
                    "Garson tarafından manuel sipariş oluşturuldu. SiparisId={SiparisId}, MasaId={MasaId}, Tutar={Tutar}",
                    siparis.Id, masaId, siparis.ToplamTutar);

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
            var siparis = await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                .FirstOrDefaultAsync(s => s.Id == siparisId);

            if (siparis == null)
                throw new InvalidOperationException("Sipariş bulunamadı.");

            if (await GunKapaliMiAsync(siparis.OlusturmaTarihi))
                throw new InvalidOperationException("Bu siparişin gün sonu raporu kapatılmış; durum değiştirilemez.");

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

            // Alt ürünlerin (iptal ya da ödenmiş olmayan) durumlarını da ana siparişle eşzamanlı güncelle
            foreach(var detay in siparis.SiparisDetaylar)
            {
                if (detay.Durum != SiparisDurum.Iptal && detay.Durum != SiparisDurum.TamOdendi && detay.Durum != SiparisDurum.Iade)
                {
                    detay.Durum = yeniDurum;
                }
            }

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
                .AsSplitQuery()
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
                .AsSplitQuery()
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
        /// Siparişi iptal eder — Onaylandı, Hazırlanıyor veya TeslimEdildi durumlarından iptal edilebilir
        /// </summary>
        public async Task<Siparis> IptalEtAsync(int siparisId)
        {
            return await DurumGuncelleAsync(siparisId, SiparisDurum.Iptal);
        }

        private async Task<bool> GunKapaliMiAsync(DateTime utcTarih)
        {
            var yerelGun = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcTarih, DateTimeKind.Utc), _turkeyTz).Date;
            var raporTarihi = DateTime.SpecifyKind(yerelGun, DateTimeKind.Utc);
            return await _context.GunSonuRaporlari.AnyAsync(r => r.Tarih == raporTarihi);
        }

        /// <summary>
        /// Şu anda aktif bir Happy Hour varsa bilgilerini, yoksa boş döner.
        /// </summary>
        private async Task<(decimal IndirimOrani, HashSet<int> UrunIds)> HappyHourBilgisiGetirAsync()
        {
            var happyHour = await _context.HappyHourlar
                .Include(h => h.HappyHourUrunler)
                .Where(h => h.AktifMi)
                .FirstOrDefaultAsync();

            if (happyHour == null)
                return (0m, new HashSet<int>());

            var turkey = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
            var simdiki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkey).TimeOfDay;

            bool aktif;
            if (happyHour.BaslangicSaati <= happyHour.BitisSaati)
                aktif = simdiki >= happyHour.BaslangicSaati && simdiki <= happyHour.BitisSaati;
            else
                aktif = simdiki >= happyHour.BaslangicSaati || simdiki <= happyHour.BitisSaati;

            if (aktif)
            {
                var urunIds = happyHour.HappyHourUrunler.Select(x => x.UrunId).ToHashSet();
                _logger.LogInformation("Happy Hour aktif! İndirim oranı: %{Oran}, UrunSayisi: {UrunSayisi}", happyHour.IndirimOrani, urunIds.Count);
                return (happyHour.IndirimOrani, urunIds);
            }

            return (0m, new HashSet<int>());
        }
    }
}
