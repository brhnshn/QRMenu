using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using System.Data;

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
            [SiparisDurum.Hazir]         = new[] { SiparisDurum.TeslimEdildi, SiparisDurum.Iptal },
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
                    transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                var sepet = await _context.Sepetler
                    .Include(s => s.SepetDetaylar)
                        .ThenInclude(sd => sd.Urun)
                    .Include(s => s.SepetDetaylar)
                        .ThenInclude(sd => sd.UrunVaryasyon)
                    .Include(s => s.Oturum)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(s => s.Id == sepetId);

                if (sepet == null)
                    throw new InvalidOperationException("Sepet bulunamadı.");

                if (!sepet.SepetDetaylar.Any())
                    throw new InvalidOperationException("Sepet boş, sipariş oluşturulamaz.");

                var nowUtc = DateTime.UtcNow;
                var siparisGunu = TurkeyBusinessDay(nowUtc);
                var gunlukSiparisNo = await GetNextGunlukSiparisNoAsync(siparisGunu);

                // Happy Hour kontrolü
                var hh = await HappyHourBilgisiGetirAsync();

                decimal tumTutar = 0;
                string? hhNot = null;
                var yeniDetaylar = new List<SiparisDetay>();

                // Tutarı hesapla ve detay listesini hazırla
                foreach (var detay in sepet.SepetDetaylar)
                {
                    // Sepette kullanıcıya ne gösterildiyse onu kullan.
                    await StokDusAsync(detay.UrunId, detay.UrunVaryasyonId, detay.Adet);

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
                        UrunVaryasyonId = detay.UrunVaryasyonId,
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
                    OlusturmaTarihi = nowUtc,
                    SiparisTarihi = nowUtc,
                    SiparisGunu = siparisGunu,
                    GunlukSiparisNo = gunlukSiparisNo,
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
                    transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                decimal toplamTutar = 0;
                var siparisDetaylar = new List<SiparisDetay>();
                var indirimUygulandi = false;
                var nowUtc = DateTime.UtcNow;
                var siparisGunu = TurkeyBusinessDay(nowUtc);
                var gunlukSiparisNo = await GetNextGunlukSiparisNoAsync(siparisGunu);

                // Happy Hour kontrolü
                var hh = await HappyHourBilgisiGetirAsync();

                foreach (var item in urunler)
                {
                    var urunDB = await _context.Urunler.FindAsync(item.UrunId);
                    if (urunDB == null || !urunDB.AktifMi)
                        throw new InvalidOperationException($"Ürün bulunamadı veya pasif: {item.UrunId}");

                    decimal birimFiyat = urunDB.Fiyat;
                    UrunVaryasyon? varyasyon = null;
                    if (item.UrunVaryasyonId.HasValue)
                    {
                        varyasyon = await _context.UrunVaryasyonlar
                            .FirstOrDefaultAsync(v => v.Id == item.UrunVaryasyonId.Value && v.UrunId == item.UrunId);

                        if (varyasyon == null || !varyasyon.AktifMi)
                            throw new InvalidOperationException($"Urun varyasyonu bulunamadi veya pasif: {item.UrunVaryasyonId}");

                        birimFiyat += varyasyon.EkFiyat;
                    }

                    await StokDusAsync(item.UrunId, item.UrunVaryasyonId, item.Adet);
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
                        UrunVaryasyonId = item.UrunVaryasyonId,
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
                    OlusturmaTarihi = nowUtc,
                    SiparisTarihi = nowUtc,
                    SiparisGunu = siparisGunu,
                    GunlukSiparisNo = gunlukSiparisNo,
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
            var supportsTransactions = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            var strategy = supportsTransactions ? _context.Database.CreateExecutionStrategy() : null;
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

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
                if (supportsTransactions && strategy != null)
                {
                    await strategy.ExecuteAsync(async () =>
                    {
                        await using var localTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                        if (StokIadesiGerekiyor(eskiDurum, yeniDurum))
                        {
                            foreach (var detay in siparis.SiparisDetaylar)
                                await StokIadeAsync(detay.UrunId, detay.UrunVaryasyonId, detay.Adet);
                        }

                        await _context.SaveChangesAsync();
                        await localTransaction.CommitAsync();
                    });
                }
                else
                {
                    if (StokIadesiGerekiyor(eskiDurum, yeniDurum))
                    {
                        foreach (var detay in siparis.SiparisDetaylar)
                            await StokIadeAsync(detay.UrunId, detay.UrunVaryasyonId, detay.Adet);
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _logger.LogWarning(ex,
                    "Concurrency conflict! SiparisId={SiparisId}, Geçiş={EskiDurum}→{YeniDurum}",
                    siparisId, eskiDurum, yeniDurum);
                throw new InvalidOperationException(
                    "Bu sipariş başka bir kullanıcı tarafından güncellenmiş. Lütfen sayfayı yenileyip tekrar deneyin.", ex);
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
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.UrunVaryasyon)
                .Include(s => s.Masa)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == siparisId);
        }

        public async Task<Siparis?> GetSiparisByOturumAsync(int siparisId, int oturumId)
        {
            return await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.UrunVaryasyon)
                .Include(s => s.Masa)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == siparisId && s.OturumId == oturumId);
        }

        /// <summary>
        /// Masanın aktif (iptal/iade dışı) siparişlerini getirir
        /// </summary>
        public async Task<List<Siparis>> GetSiparislerByMasaAsync(int masaId)
        {
            return await _context.Siparisler
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.UrunVaryasyon)
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
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.UrunVaryasyon)
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

        /// <summary>
        /// Sipariş detaylarını (adet bazlı) iptal eder.
        /// </summary>
        public async Task<IReadOnlyList<Siparis>> SiparisDetayIptalEtAsync(List<SiparisDetayIptalDto> detaylar, int? masaId = null)
        {
            if (detaylar == null || detaylar.Count == 0)
                throw new InvalidOperationException("İptal edilecek kalem seçilmedi.");

            var supportsTransactions = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
            if (supportsTransactions)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(() => ExecuteSiparisDetayIptalAsync(detaylar, masaId, true));
            }

            return await ExecuteSiparisDetayIptalAsync(detaylar, masaId, false);
        }

        private async Task<IReadOnlyList<Siparis>> ExecuteSiparisDetayIptalAsync(List<SiparisDetayIptalDto> detaylar, int? masaId, bool useTransaction)
        {
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            try
            {
                if (useTransaction)
                    transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                var iptalHaritasi = detaylar
                    .Where(d => d.SiparisDetayId > 0)
                    .GroupBy(d => d.SiparisDetayId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.Adet)));

                if (!iptalHaritasi.Any())
                    throw new InvalidOperationException("İptal edilecek kalem seçilmedi.");

                var detayIds = iptalHaritasi.Keys.ToList();

                var dbDetaylar = await _context.SiparisDetaylar
                    .Include(sd => sd.Siparis)
                        .ThenInclude(s => s.Masa)
                    .Include(sd => sd.Siparis)
                        .ThenInclude(s => s.SiparisDetaylar)
                    .Where(sd => detayIds.Contains(sd.Id))
                    .ToListAsync();

                if (masaId.HasValue)
                {
                    dbDetaylar = dbDetaylar
                        .Where(sd => sd.Siparis.MasaId == masaId.Value)
                        .ToList();
                }

                if (!dbDetaylar.Any())
                    throw new InvalidOperationException("Geçerli iptal kalemi bulunamadı.");

                var siparisler = dbDetaylar
                    .Select(d => d.Siparis)
                    .DistinctBy(s => s.Id)
                    .ToList();

                foreach (var siparis in siparisler)
                {
                    if (await GunKapaliMiAsync(siparis.OlusturmaTarihi))
                        throw new InvalidOperationException("Gün sonu raporu kapatılmış siparişlerde iptal işlemi yapılamaz.");
                }

                var eskiBazToplamlar = siparisler.ToDictionary(
                    s => s.Id,
                    s => s.SiparisDetaylar
                        .Where(sd => sd.Durum != SiparisDurum.Iptal && sd.Durum != SiparisDurum.Iade)
                        .Sum(sd => sd.Adet * sd.BirimFiyat));

                var nowUtc = DateTime.UtcNow;
                var degisenSiparisler = new HashSet<int>();
                var iptalKayitlari = new List<SiparisDetay>();

                foreach (var detay in dbDetaylar)
                {
                    if (!iptalHaritasi.TryGetValue(detay.Id, out var istenenAdet) || istenenAdet <= 0)
                        continue;

                    if (detay.Durum == SiparisDurum.Iptal || detay.Durum == SiparisDurum.Iade || detay.Durum == SiparisDurum.TamOdendi)
                        continue;

                    var iptalAdet = Math.Min(istenenAdet, detay.Adet);
                    if (iptalAdet <= 0)
                        continue;

                    degisenSiparisler.Add(detay.SiparisId);

                    if (iptalAdet >= detay.Adet)
                    {
                        detay.Durum = SiparisDurum.Iptal;
                    }
                    else
                    {
                        detay.Adet -= iptalAdet;

                        var yeniDetay = new SiparisDetay
                        {
                            SiparisId = detay.SiparisId,
                            UrunId = detay.UrunId,
                            UrunVaryasyonId = detay.UrunVaryasyonId,
                            Adet = iptalAdet,
                            BirimFiyat = detay.BirimFiyat,
                            SeciliOpsiyonlar = detay.SeciliOpsiyonlar,
                            Durum = SiparisDurum.Iptal
                        };

                        iptalKayitlari.Add(yeniDetay);
                        detay.Siparis.SiparisDetaylar.Add(yeniDetay);
                    }
                }

                if (!degisenSiparisler.Any())
                    throw new InvalidOperationException("Geçerli iptal kalemi bulunamadı.");

                if (iptalKayitlari.Any())
                    _context.SiparisDetaylar.AddRange(iptalKayitlari);

                foreach (var siparis in siparisler.Where(s => degisenSiparisler.Contains(s.Id)))
                {
                    var aktifKaldiMi = siparis.SiparisDetaylar.Any(sd =>
                        sd.Durum != SiparisDurum.Iptal &&
                        sd.Durum != SiparisDurum.Iade);

                    if (!aktifKaldiMi)
                    {
                        siparis.Durum = SiparisDurum.Iptal;
                    }

                    var eskiBaz = eskiBazToplamlar.TryGetValue(siparis.Id, out var value) ? value : 0m;
                    var yeniBaz = siparis.SiparisDetaylar
                        .Where(sd => sd.Durum != SiparisDurum.Iptal && sd.Durum != SiparisDurum.Iade)
                        .Sum(sd => sd.Adet * sd.BirimFiyat);

                    if (eskiBaz > 0)
                    {
                        var oran = siparis.ToplamTutar / eskiBaz;
                        siparis.ToplamTutar = Math.Round(yeniBaz * oran, 2);
                    }
                    else
                    {
                        siparis.ToplamTutar = 0m;
                    }

                    siparis.GuncellemeTarihi = nowUtc;
                    siparis.RowVersion = Guid.NewGuid().ToByteArray();
                }

                await _context.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();

                _logger.LogInformation("Sipariş detay iptali tamamlandı. SiparisIds={SiparisIds}", string.Join(", ", degisenSiparisler));

                return siparisler.Where(s => degisenSiparisler.Contains(s.Id)).ToList();
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

        private async Task<bool> GunKapaliMiAsync(DateTime utcTarih)
        {
            var yerelGun = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcTarih, DateTimeKind.Utc), _turkeyTz).Date;
            var raporTarihi = DateTime.SpecifyKind(yerelGun, DateTimeKind.Utc);
            return await _context.GunSonuRaporlari.AnyAsync(r => r.Tarih == raporTarihi);
        }

        private static DateTime TurkeyBusinessDay(DateTime utcNow)
        {
            var yerelGun = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), _turkeyTz).Date;
            return DateTime.SpecifyKind(yerelGun, DateTimeKind.Utc);
        }

        private async Task<int> GetNextGunlukSiparisNoAsync(DateTime siparisGunu)
        {
            var gunBaslangicUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(siparisGunu.Date, DateTimeKind.Unspecified), _turkeyTz);
            var gunBitisUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(siparisGunu.Date.AddDays(1), DateTimeKind.Unspecified), _turkeyTz);

            var sonNo = await _context.Siparisler
                .Where(s => s.SiparisTarihi >= gunBaslangicUtc && s.SiparisTarihi < gunBitisUtc)
                .OrderByDescending(s => s.GunlukSiparisNo)
                .Select(s => (int?)s.GunlukSiparisNo)
                .FirstOrDefaultAsync();

            return (sonNo ?? 0) + 1;
        }

        private async Task StokDusAsync(int urunId, int? urunVaryasyonId, int adet)
        {
            if (adet <= 0)
                throw new InvalidOperationException("Siparis adedi gecersiz.");

            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                if (urunVaryasyonId.HasValue)
                {
                    var varyasyon = await _context.UrunVaryasyonlar.FirstOrDefaultAsync(v => v.Id == urunVaryasyonId.Value && v.UrunId == urunId);
                    if (varyasyon == null || !varyasyon.AktifMi || varyasyon.StokAdet < adet)
                        throw new InvalidOperationException("Stok yetersiz. Lutfen sepetinizi kontrol edin.");

                    varyasyon.StokAdet -= adet;
                    if (varyasyon.StokAdet <= 0)
                        varyasyon.AktifMi = false;
                    return;
                }

                var urun = await _context.Urunler.FirstOrDefaultAsync(u => u.Id == urunId);
                if (urun == null || !urun.AktifMi || urun.StokAdet < adet)
                    throw new InvalidOperationException("Stok yetersiz. Lutfen sepetinizi kontrol edin.");

                urun.StokAdet -= adet;
                if (urun.StokAdet <= 0)
                    urun.AktifMi = false;
                return;
            }

            int affected;
            if (urunVaryasyonId.HasValue)
            {
                affected = await _context.UrunVaryasyonlar
                    .Where(v => v.Id == urunVaryasyonId.Value && v.UrunId == urunId && v.AktifMi && v.StokAdet >= adet)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(v => v.StokAdet, v => v.StokAdet - adet)
                        .SetProperty(v => v.AktifMi, v => v.StokAdet - adet > 0 && !v.AdminManuelPasifMi));
            }
            else
            {
                affected = await _context.Urunler
                    .Where(u => u.Id == urunId && u.AktifMi && u.StokAdet >= adet)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(u => u.StokAdet, u => u.StokAdet - adet)
                        .SetProperty(u => u.AktifMi, u => u.StokAdet - adet > 0 && !u.AdminManuelPasifMi));
            }

            if (affected == 0)
                throw new InvalidOperationException("Stok yetersiz. Lutfen sepetinizi kontrol edin.");
        }

        private async Task StokIadeAsync(int urunId, int? urunVaryasyonId, int adet)
        {
            if (adet <= 0)
                return;

            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                if (urunVaryasyonId.HasValue)
                {
                    var varyasyon = await _context.UrunVaryasyonlar.FirstOrDefaultAsync(v => v.Id == urunVaryasyonId.Value && v.UrunId == urunId);
                    if (varyasyon == null) return;
                    varyasyon.StokAdet += adet;
                    if (varyasyon.StokAdet > 0 && !varyasyon.AdminManuelPasifMi)
                        varyasyon.AktifMi = true;
                    return;
                }

                var urun = await _context.Urunler.FirstOrDefaultAsync(u => u.Id == urunId);
                if (urun == null) return;
                urun.StokAdet += adet;
                if (urun.StokAdet > 0 && !urun.AdminManuelPasifMi)
                    urun.AktifMi = true;
                return;
            }

            if (urunVaryasyonId.HasValue)
            {
                await _context.UrunVaryasyonlar
                    .Where(v => v.Id == urunVaryasyonId.Value && v.UrunId == urunId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(v => v.StokAdet, v => v.StokAdet + adet)
                        .SetProperty(v => v.AktifMi, v => !v.AdminManuelPasifMi));
            }
            else
            {
                await _context.Urunler
                    .Where(u => u.Id == urunId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(u => u.StokAdet, u => u.StokAdet + adet)
                        .SetProperty(u => u.AktifMi, u => !u.AdminManuelPasifMi));
            }
        }

        private static bool StokIadesiGerekiyor(SiparisDurum eskiDurum, SiparisDurum yeniDurum)
        {
            var yeniTerminal = yeniDurum == SiparisDurum.Iptal || yeniDurum == SiparisDurum.Iade;
            var eskiTerminal = eskiDurum == SiparisDurum.Iptal || eskiDurum == SiparisDurum.Iade;
            return yeniTerminal && !eskiTerminal;
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
