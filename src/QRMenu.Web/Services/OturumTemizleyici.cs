using Microsoft.EntityFrameworkCore;
using QRMenu.Data.Data;

namespace QRMenu.Web.Services
{
    /// <summary>
    /// Arka plan temizlik servisi: Her 30 dakikada bir 4 iş çalıştırır.
    /// </summary>
    public class OturumTemizleyici : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OturumTemizleyici> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(30);
        private readonly TimeSpan _inaktifSure = TimeSpan.FromHours(2);

        public OturumTemizleyici(IServiceProvider serviceProvider, ILogger<OturumTemizleyici> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OturumTemizleyici başlatıldı. Aralık: {Interval}, İnaktif sınırı: {InaktifSure}",
                _interval, _inaktifSure);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TemizleAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Oturum temizliği sırasında hata oluştu.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task TemizleAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QRMenuDbContext>();
            var turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

            var sinir = DateTime.UtcNow - _inaktifSure;
            var pasifeAlinanOturumSayisi = 0;
            var silinenPasifOturumSayisi = 0;
            var temizlenenSahipsizSepetSayisi = 0;
            var pasifeAlinanHappyHourSayisi = 0;

            var inaktifOturumlar = await context.Oturumlar
                .Where(o => o.SonIslemTarihi < sinir && o.AktifMi)
                .ToListAsync();

            if (inaktifOturumlar.Count > 0)
            {
                var oturumIds = inaktifOturumlar.Select(o => o.Id).ToList();
                var ilgiliSepetler = await context.Sepetler
                    .Include(s => s.SepetDetaylar)
                    .Where(s => oturumIds.Contains(s.OturumId))
                    .ToListAsync();

                foreach (var sepet in ilgiliSepetler)
                    context.SepetDetaylar.RemoveRange(sepet.SepetDetaylar);

                foreach (var oturum in inaktifOturumlar)
                    oturum.AktifMi = false;

                await context.SaveChangesAsync();
                pasifeAlinanOturumSayisi = inaktifOturumlar.Count;
                _logger.LogInformation("İş 1: {Count} inaktif oturum pasife alındı (2 saat üzeri).", inaktifOturumlar.Count);
            }

            var tokenSiniri = DateTime.UtcNow - TimeSpan.FromDays(7);
            var eskiTokenlar = await context.Oturumlar
                .Where(o => !o.AktifMi && o.SonIslemTarihi < tokenSiniri)
                .ToListAsync();

            if (eskiTokenlar.Count > 0)
            {
                context.Oturumlar.RemoveRange(eskiTokenlar);
                await context.SaveChangesAsync();
                silinenPasifOturumSayisi = eskiTokenlar.Count;
                _logger.LogInformation("İş 2: {Count} süresi dolmuş token silindi (7+ gün eski pasif oturum).", eskiTokenlar.Count);
            }

            var sahipsizSepetler = await context.Sepetler
                .Include(s => s.SepetDetaylar)
                .Where(s => !context.Oturumlar.Any(o => o.Id == s.OturumId))
                .ToListAsync();

            if (sahipsizSepetler.Count > 0)
            {
                foreach (var sepet in sahipsizSepetler)
                    context.SepetDetaylar.RemoveRange(sepet.SepetDetaylar);

                context.Sepetler.RemoveRange(sahipsizSepetler);
                await context.SaveChangesAsync();
                temizlenenSahipsizSepetSayisi = sahipsizSepetler.Count;
                _logger.LogInformation("İş 3: {Count} sahipsiz sepet temizlendi.", sahipsizSepetler.Count);
            }

            var simdikiZaman = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkeyTz).TimeOfDay;
            var aktifHappyHourlar = await context.HappyHourlar.Where(h => h.AktifMi).ToListAsync();
            foreach (var hh in aktifHappyHourlar)
            {
                if (hh.BaslangicSaati <= hh.BitisSaati)
                {
                    if (simdikiZaman > hh.BitisSaati)
                    {
                        hh.AktifMi = false;
                        pasifeAlinanHappyHourSayisi++;
                        _logger.LogInformation("Happy Hour süresi doldu ve pasife alındı.");
                    }
                }
                else
                {
                    if (simdikiZaman > hh.BitisSaati && simdikiZaman < hh.BaslangicSaati)
                    {
                        hh.AktifMi = false;
                        pasifeAlinanHappyHourSayisi++;
                        _logger.LogInformation("Happy Hour (gece geçişli) süresi doldu ve pasife alındı.");
                    }
                }
            }

            if (pasifeAlinanHappyHourSayisi > 0)
                await context.SaveChangesAsync();

            var trNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkeyTz);
            _logger.LogInformation(
                "Temizlik tamamlandı. TR: {Zaman}. İş1(PasifeAlınanOturum)={Is1}, İş2(SilinenPasifOturum)={Is2}, İş3(SahipsizSepet)={Is3}, İş4(PasifeAlınanHappyHour)={Is4}",
                trNow, pasifeAlinanOturumSayisi, silinenPasifOturumSayisi, temizlenenSahipsizSepetSayisi, pasifeAlinanHappyHourSayisi);
        }
    }
}
