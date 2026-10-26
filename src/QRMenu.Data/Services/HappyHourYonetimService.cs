using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Helpers;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class HappyHourYonetimService : IHappyHourYonetimService
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

        private readonly QRMenuDbContext _context;
        private readonly ILogger<HappyHourYonetimService> _logger;

        public HappyHourYonetimService(QRMenuDbContext context, ILogger<HappyHourYonetimService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(HappyHour HappyHour, List<Kategori> Kategoriler, List<Urun> Urunler, List<int> SeciliUrunIds)> GetHappyHourDataAsync()
        {
            var kategoriler = await _context.Kategoriler
                .Include(k => k.Urunler)
                .OrderBy(k => k.SiraNo)
                .ToListAsync();

            var urunler = await _context.Urunler
                .Include(u => u.Kategori)
                .Where(u => u.AktifMi)
                .OrderBy(u => u.Kategori.SiraNo)
                .ThenBy(u => u.Ad)
                .ToListAsync();

            var happyHour = await _context.HappyHourlar
                .Include(h => h.HappyHourUrunler)
                .FirstOrDefaultAsync();

            happyHour ??= new HappyHour
            {
                BaslangicSaati = new TimeSpan(14, 0, 0),
                BitisSaati = new TimeSpan(17, 0, 0),
                IndirimOrani = 10,
                AktifMi = false
            };

            var seciliUrunIds = happyHour.HappyHourUrunler?.Select(x => x.UrunId).ToList() ?? new List<int>();
            return (happyHour, kategoriler, urunler, seciliUrunIds);
        }

        public async Task<HappyHourBilgiDto> GetHappyHourBilgiAsync()
        {
            var hh = await _context.HappyHourlar
                .Include(h => h.HappyHourUrunler)
                .FirstOrDefaultAsync();

            if (hh == null)
                return new HappyHourBilgiDto { AktifMi = false, IndirimOrani = 0, BaslangicSaati = "", BitisSaati = "", UrunIds = Array.Empty<int>() };

            var turkey = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
            var simdiki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkey).TimeOfDay;

            bool suAnAktif = false;
            if (hh.AktifMi)
            {
                if (hh.BaslangicSaati <= hh.BitisSaati)
                    suAnAktif = simdiki >= hh.BaslangicSaati && simdiki <= hh.BitisSaati;
                else
                    suAnAktif = simdiki >= hh.BaslangicSaati || simdiki <= hh.BitisSaati;
            }

            return new HappyHourBilgiDto
            {
                Id = hh.Id,
                AktifMi = hh.AktifMi,
                SuAnAktif = suAnAktif,
                IndirimOrani = hh.IndirimOrani,
                BaslangicSaati = hh.BaslangicSaati.ToString(@"hh\:mm"),
                BitisSaati = hh.BitisSaati.ToString(@"hh\:mm"),
                UrunIds = hh.HappyHourUrunler.Select(x => x.UrunId).ToArray()
            };
        }

        public async Task<(bool Success, string Message, List<int> EtkilenenUrunIds, HappyHour? HappyHour)> HappyHourKaydetAsync(HappyHourKayitDto model)
        {
            var hh = await _context.HappyHourlar
                .Include(h => h.HappyHourUrunler)
                .FirstOrDefaultAsync();

            if (hh == null)
            {
                hh = new HappyHour();
                _context.HappyHourlar.Add(hh);
            }

            // Kapatma akışı: sadece AktifMi=false yap, geri kalana dokunma
            if (!model.AktifMi)
            {
                hh.AktifMi = false;
                hh.GuncellemeTarihi = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(model.BaslangicSaati) &&
                    TimeSpan.TryParse(model.BaslangicSaati.Trim().Replace('.', ':'), CultureInfo.InvariantCulture, out var bTs))
                    hh.BaslangicSaati = bTs;

                if (!string.IsNullOrWhiteSpace(model.BitisSaati) &&
                    TimeSpan.TryParse(model.BitisSaati.Trim().Replace('.', ':'), CultureInfo.InvariantCulture, out var btTs))
                    hh.BitisSaati = btTs;

                if (model.IndirimOrani > 0)
                    hh.IndirimOrani = model.IndirimOrani;

                if (model.UrunIds != null)
                {
                    var yeniIds = model.UrunIds.Where(x => x > 0).Distinct().ToList();
                    _context.HappyHourUrunler.RemoveRange(hh.HappyHourUrunler);
                    hh.HappyHourUrunler.Clear();
                    foreach (var uid in yeniIds)
                        hh.HappyHourUrunler.Add(new HappyHourUrun { UrunId = uid });
                }

                await _context.SaveChangesAsync();
                await UpdateSepetFiyatlariAsync(null, new List<int>());

                _logger.LogInformation("Happy Hour kapatıldı.");
                return (true, "İndirim sistemi kapatıldı.", new List<int>(), null);
            }

            // Açma / Güncelleme akışı: tam validasyon
            if (model.IndirimOrani < 1 || model.IndirimOrani > 99)
                return (false, "İndirim oranı 1-99 arasında olmalıdır.", new List<int>(), null);

            var baslangicRaw = (model.BaslangicSaati ?? string.Empty).Trim().Replace('.', ':');
            var bitisRaw = (model.BitisSaati ?? string.Empty).Trim().Replace('.', ':');

            if (string.IsNullOrWhiteSpace(baslangicRaw) || string.IsNullOrWhiteSpace(bitisRaw))
                return (false, "Başlangıç ve bitiş saatleri boş bırakılamaz.", new List<int>(), null);

            if (!TimeSpan.TryParse(baslangicRaw, CultureInfo.InvariantCulture, out var baslangicTs))
                return (false, "Geçersiz başlangıç saati formatı. Örn: 14:00", new List<int>(), null);

            if (!TimeSpan.TryParse(bitisRaw, CultureInfo.InvariantCulture, out var bitisTs))
                return (false, "Geçersiz bitiş saati formatı. Örn: 17:00", new List<int>(), null);

            if (baslangicTs == bitisTs)
                return (false, "Başlangıç ve bitiş saatleri aynı olamaz.", new List<int>(), null);

            hh.BaslangicSaati = baslangicTs;
            hh.BitisSaati = bitisTs;
            hh.IndirimOrani = model.IndirimOrani;
            hh.AktifMi = true;
            hh.UrunId = null;
            hh.GuncellemeTarihi = DateTime.UtcNow;

            var yeniUrunIds = (model.UrunIds ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            _context.HappyHourUrunler.RemoveRange(hh.HappyHourUrunler);
            hh.HappyHourUrunler.Clear();
            foreach (var urunId in yeniUrunIds)
            {
                hh.HappyHourUrunler.Add(new HappyHourUrun
                {
                    UrunId = urunId
                });
            }

            await _context.SaveChangesAsync();
            await UpdateSepetFiyatlariAsync(hh, yeniUrunIds);

            _logger.LogInformation("İndirim saatleri güncellendi. Aktif={Aktif}, Oran=%{Oran}, {Baslangic}-{Bitis}, UrunSayisi={UrunSayisi}",
                hh.AktifMi, hh.IndirimOrani, hh.BaslangicSaati, hh.BitisSaati, yeniUrunIds.Count);

            return (true, "İndirim ayarları kaydedildi.", yeniUrunIds, hh);
        }

        public async Task UpdateSepetFiyatlariAsync(HappyHour? happyHour, List<int> etkilenenUrunIds)
        {
            var sepetDetaylar = await _context.SepetDetaylar
                .Include(sd => sd.Urun)
                .Include(sd => sd.UrunVaryasyon)
                .ToListAsync();

            var trNow = DateTimeHelper.GetTurkeyTime(DateTime.UtcNow);
            var trTime = trNow.TimeOfDay;
            bool isWithinHhWindow = false;
            if (happyHour != null && happyHour.AktifMi)
            {
                if (happyHour.BaslangicSaati <= happyHour.BitisSaati)
                {
                    isWithinHhWindow = trTime >= happyHour.BaslangicSaati && trTime <= happyHour.BitisSaati;
                }
                else
                {
                    isWithinHhWindow = trTime >= happyHour.BaslangicSaati || trTime <= happyHour.BitisSaati;
                }
            }

            bool herhangiGuncellendi = false;

            foreach (var detay in sepetDetaylar)
            {
                var urun = detay.Urun;
                if (urun == null) continue;

                decimal varyasyonEkFiyat = detay.UrunVaryasyon?.EkFiyat ?? 0m;
                decimal opsiyonEkFiyat = 0;
                if (!string.IsNullOrEmpty(detay.SeciliOpsiyonlar) && detay.SeciliOpsiyonlar != "[]")
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(detay.SeciliOpsiyonlar);
                        foreach (var ops in doc.RootElement.EnumerateArray())
                        {
                            if (ops.TryGetProperty("EkFiyat", out var ekFiyatProp))
                                opsiyonEkFiyat += ekFiyatProp.GetDecimal();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SepetDetay (Id={Id}) SeciliOpsiyonlar JSON ayrıştırma hatası.", detay.Id);
                    }
                }

                bool urunHhKapsaminda = happyHour != null && happyHour.IndirimOrani > 0 &&
                    (!etkilenenUrunIds.Any() || etkilenenUrunIds.Contains(detay.UrunId));

                bool hhUygulanabilir = isWithinHhWindow && urunHhKapsaminda;
                decimal hhOrani = happyHour?.IndirimOrani ?? 0m;

                decimal yeniBirimFiyat = FiyatHesaplayici.BirimFiyatHesapla(
                    urun.Fiyat,
                    varyasyonEkFiyat,
                    opsiyonEkFiyat,
                    hhOrani,
                    hhUygulanabilir);

                if (detay.BirimFiyat != yeniBirimFiyat)
                {
                    detay.BirimFiyat = yeniBirimFiyat;
                    herhangiGuncellendi = true;
                }
            }
            if (!herhangiGuncellendi) return;

            var sepetIds = sepetDetaylar.Select(sd => sd.SepetId).Distinct().ToList();
            var sepetler = await _context.Sepetler
                .Where(s => sepetIds.Contains(s.Id))
                .ToListAsync();

            var toplamlar = sepetDetaylar
                .GroupBy(sd => sd.SepetId)
                .ToDictionary(g => g.Key, g => g.Sum(sd => sd.BirimFiyat * sd.Adet));

            foreach (var sepet in sepetler)
            {
                sepet.ToplamTutar = toplamlar.TryGetValue(sepet.Id, out var toplam) ? toplam : 0m;
            }

            await _context.SaveChangesAsync();
        }
    }
}
