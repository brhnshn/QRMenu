using Microsoft.EntityFrameworkCore;
using QRMenu.Data.Data;

namespace QRMenu.Web.Services
{
    public class OturumTemizleyici : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OturumTemizleyici> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);
        private readonly TimeSpan _maxAge = TimeSpan.FromDays(1);

        public OturumTemizleyici(IServiceProvider serviceProvider, ILogger<OturumTemizleyici> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
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

            var sinir = DateTime.UtcNow - _maxAge;
            var eskiOturumlar = await context.Oturumlar
                .Where(o => o.SonIslemTarihi < sinir)
                .ToListAsync();

            if (eskiOturumlar.Count == 0) return;

            context.Oturumlar.RemoveRange(eskiOturumlar);
            await context.SaveChangesAsync();

            _logger.LogInformation("{Count} adet eski oturum silindi (1 günden eski).", eskiOturumlar.Count);
        }
    }
}
