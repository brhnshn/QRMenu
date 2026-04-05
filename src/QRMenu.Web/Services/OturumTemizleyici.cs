using Microsoft.EntityFrameworkCore;
using QRMenu.Data.Data;

namespace QRMenu.Web.Services
{
    /// <summary>
    /// Arka plan temizlik servisi — Her 30 dakikada bir 3 iş çalıştırır:
    /// 1. 2 saatten fazla işlem görmeyen oturumları (ve bağlı sepetleri) temizle
    /// 2. Aktif olmayan oturumların token'larını pasife al
    /// 3. Oturumu silinmiş sahipsiz sepetleri temizle
    /// </summary>
    public class OturumTemizleyici : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OturumTemizleyici> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(30); // Her 30 dakikada çalış
        private readonly TimeSpan _inaktifSure = TimeSpan.FromHours(2); // 2 saat inaktif = temizle

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

            var sinir = DateTime.UtcNow - _inaktifSure;

            // ─── İŞ 1: 2 saatten fazla işlem görmeyen oturumları temizle ───
            var inaktifOturumlar = await context.Oturumlar
                .Where(o => o.SonIslemTarihi < sinir && o.AktifMi)
                .ToListAsync();

            if (inaktifOturumlar.Count > 0)
            {
                // Bağlı sepetleri de temizle (cascade olabilir ama explicit yapalım)
                var oturumIds = inaktifOturumlar.Select(o => o.Id).ToList();
                var ilgiliSepetler = await context.Sepetler
                    .Include(s => s.SepetDetaylar)
                    .Where(s => oturumIds.Contains(s.OturumId))
                    .ToListAsync();

                foreach (var sepet in ilgiliSepetler)
                    context.SepetDetaylar.RemoveRange(sepet.SepetDetaylar);

                // Oturumları pasife al (hard delete yerine soft delete — sipariş geçmişi korunsun)
                foreach (var oturum in inaktifOturumlar)
                    oturum.AktifMi = false;

                await context.SaveChangesAsync();
                _logger.LogInformation("İş 1: {Count} inaktif oturum pasife alındı (2 saat üzeri).", inaktifOturumlar.Count);
            }

            // ─── İŞ 2: Süresi dolmuş token'ları (pasif + 7 günden eski) sil ───
            var tokenSiniri = DateTime.UtcNow - TimeSpan.FromDays(7);
            var eskiTokenlar = await context.Oturumlar
                .Where(o => !o.AktifMi && o.SonIslemTarihi < tokenSiniri)
                .ToListAsync();

            if (eskiTokenlar.Count > 0)
            {
                context.Oturumlar.RemoveRange(eskiTokenlar);
                await context.SaveChangesAsync();
                _logger.LogInformation("İş 2: {Count} süresi dolmuş token silindi (7+ gün eski pasif oturum).", eskiTokenlar.Count);
            }

            // ─── İŞ 3: Sahipsiz (oturumu olmayan) sepetleri temizle ───
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
                _logger.LogInformation("İş 3: {Count} sahipsiz sepet temizlendi.", sahipsizSepetler.Count);
            }

            // ─── İŞ 4: Süresi biten Happy Hour'ları pasife al (Opsiyonel ama iyi olur) ───
            var turkey = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
            var simdikiZaman = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkey).TimeOfDay;

            var aktifHappyHourlar = await context.HappyHourlar.Where(h => h.AktifMi).ToListAsync();
            foreach (var hh in aktifHappyHourlar)
            {
                if (hh.BaslangicSaati <= hh.BitisSaati)
                {
                    // Aynı gün içinde (örn 14:00 - 17:00), şu an > 17:00 ise bitmiştir
                    if (simdikiZaman > hh.BitisSaati)
                    {
                        hh.AktifMi = false;
                        _logger.LogInformation("Happy Hour süresi doldu ve pasife alındı.");
                    }
                }
                else
                {
                    // Gece yarısı geçişi (örn 22:00 - 02:00)
                    // Öğlen 12 ile 22 arasında mıyız? O zaman bitmiş olmalı.
                    if (simdikiZaman > hh.BitisSaati && simdikiZaman < hh.BaslangicSaati)
                    {
                        hh.AktifMi = false;
                        _logger.LogInformation("Happy Hour (Gece geçişli) süresi doldu ve pasife alındı.");
                    }
                }
            }
            if (aktifHappyHourlar.Any(h => !h.AktifMi))
            {
                await context.SaveChangesAsync();
            }

            _logger.LogInformation("Temizlik tamamlandı. UTC: {Zaman}", DateTime.UtcNow);
        }
    }
}
