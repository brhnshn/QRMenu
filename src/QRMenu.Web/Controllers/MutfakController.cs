using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Data.Data;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace QRMenu.Web.Controllers
{
    [Authorize(Roles = "Admin, Mutfak, Barista")]
    public class MutfakController : Controller
    {
        private readonly QRMenuDbContext _context;
        private readonly ISiparisService _siparisService;
        private readonly IHubContext<OrderHub> _menuHub;
        private readonly ILogger<MutfakController> _logger;

        public MutfakController(QRMenuDbContext context, ISiparisService siparisService, IHubContext<OrderHub> menuHub, ILogger<MutfakController> logger)
        {
            _context = context;
            _siparisService = siparisService;
            _menuHub = menuHub;
            _logger = logger;
        }

        [HttpGet("/Mutfak/Panel")]
        public async Task<IActionResult> Panel()
        {
            ViewData["ActivePage"] = "MutfakPanel";
            ViewData["PageTitle"] = "Mutfak KDS Ekranı";

            // Sadece Onaylandı veya Hazırlanıyor ürün barındıran ve son 24 saat içinde olan siparişleri getir
            var sinirTarih = DateTime.UtcNow.AddHours(-24);
            var bekleyenSiparisler = await _context.Siparisler
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .Where(s => s.OlusturmaTarihi >= sinirTarih 
                    && s.Durum != SiparisDurum.Iptal 
                    && s.Durum != SiparisDurum.Iade 
                    && s.Durum != SiparisDurum.TamOdendi
                    && s.SiparisDetaylar.Any(sd => sd.Durum == SiparisDurum.Onaylandi || sd.Durum == SiparisDurum.Hazirlaniyor))
                .OrderBy(s => s.OlusturmaTarihi)
                .ToListAsync();

            return View(bekleyenSiparisler);
        }

        [HttpPost("/Mutfak/DurumGuncelle/{siparisId:int}")]
        public async Task<IActionResult> DurumGuncelle(int siparisId, [FromBody] MutfakDurumRequest request)
        {
            try
            {
                if (!Enum.TryParse<SiparisDurum>(request.YeniDurum, out var durum))
                    return Json(new { success = false, message = "Geçersiz durum." });

                // ISiparisService ile güvenli durum değişimi yapılıyor
                var siparis = await _siparisService.DurumGuncelleAsync(siparisId, durum);
                
                _logger.LogInformation("Mutfak sipariş güncelledi. SiparisId={Id}, YeniDurum={Durum}", siparisId, durum);

                var sip = await _context.Siparisler
                    .Include(s => s.Masa)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(s => s.Id == siparisId);

                var masaId = sip?.MasaId ?? siparis.MasaId;
                var masaNo = sip?.Masa?.MasaNo ?? 0;

                // Eğer durum "Hazır" ise Garson'a özel bildirim fırlat
                if (durum == SiparisDurum.Hazir)
                {
                    await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisHazir", masaNo);
                    await _menuHub.Clients.Group(SignalRGroups.Table(masaId)).SendAsync("SiparisHazir", masaNo);
                }

                await _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi");
                await _menuHub.Clients.Group(SignalRGroups.Table(masaId)).SendAsync("SiparisGuncellendi");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mutfak sipariş durum güncelleme hatası");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    public class MutfakDurumRequest
    {
        public string YeniDurum { get; set; } = "";
    }
}
