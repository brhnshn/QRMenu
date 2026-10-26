using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using iText.Html2pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Web.Helpers;
using QRMenu.Web.Hubs;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireAdmin")]
    public class AdminRaporController : Controller
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

        private static string ToTurkeyTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).ToString("dd.MM.yyyy HH:mm");

        private static string? ToTurkeyTime(DateTime? utc) =>
            utc.HasValue ? ToTurkeyTime(utc.Value) : null;

        private readonly IRaporYonetimService _raporService;
        private readonly ISiparisService _siparisService;
        private readonly IHubContext<OrderHub> _menuHub;
        private readonly IRazorViewRenderer _viewRenderer;
        private readonly UserManager<Kullanici> _userManager;
        private readonly ILogger<AdminRaporController> _logger;

        public AdminRaporController(
            IRaporYonetimService raporService,
            ISiparisService siparisService,
            IHubContext<OrderHub> menuHub,
            IRazorViewRenderer viewRenderer,
            UserManager<Kullanici> userManager,
            ILogger<AdminRaporController> logger)
        {
            _raporService = raporService;
            _siparisService = siparisService;
            _menuHub = menuHub;
            _viewRenderer = viewRenderer;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpPost("/admin/gun-sonu-kapat")]
        public async Task<IActionResult> GunSonuKapat([FromBody] AdminController.GunSonuKapatRequest? request)
        {
            var userId = User != null ? _userManager.GetUserId(User) : null;
            var (success, message) = await _raporService.GunSonuKapatAsync(request?.Tarih, userId);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true });
        }

        [HttpPost("/admin/gun-sonu-ac")]
        public async Task<IActionResult> GunSonuAc([FromBody] AdminController.GunSonuKapatRequest? request)
        {
            var (success, message) = await _raporService.GunSonuAcAsync(request?.Tarih);
            if (!success)
                return Json(new { success = false, message });

            return Json(new { success = true });
        }

        [HttpGet("/admin/gun-sonu-pdf")]
        public async Task<IActionResult> GunSonuPdf(string? tarih)
        {
            var model = await _raporService.BuildGunSonuExportModelAsync(tarih);
            var exportVm = new AdminController.GunSonuExportViewModel
            {
                Tarih = model.Tarih,
                KapaliMi = model.KapaliMi,
                KapanisTarihi = model.KapanisTarihi,
                ToplamCiro = model.ToplamCiro,
                SiparisSayisi = model.SiparisSayisi,
                OdemeTipleri = model.OdemeTipleri.Select(o => new AdminController.ZOdemeTipiViewModel
                {
                    Tip = o.Tip,
                    Tutar = o.Tutar,
                    Adet = o.Adet
                }).ToList(),
                Siparisler = model.Siparisler.Select(s => new AdminController.GunSonuExportSiparisViewModel
                {
                    Id = s.Id,
                    MasaNo = s.MasaNo,
                    Durum = s.Durum,
                    Saat = s.Saat,
                    ToplamTutar = s.ToplamTutar,
                    Notlar = s.Notlar,
                    Urunler = s.Urunler.Select(u => new AdminController.GunSonuExportDetayViewModel
                    {
                        UrunAd = u.UrunAd,
                        Adet = u.Adet,
                        BirimFiyat = u.BirimFiyat,
                        Secenekler = u.Secenekler
                    }).ToList()
                }).ToList()
            };

            var html = await _viewRenderer.RenderViewToStringAsync("Admin/Export/ZReportTemplate", exportVm);
            using var stream = new MemoryStream();
            HtmlConverter.ConvertToPdf(html, stream);
            return File(stream.ToArray(), "application/pdf", $"z-raporu-{model.Tarih:yyyy-MM-dd}.pdf");
        }

        [HttpGet("/admin/siparisler-pdf")]
        public async Task<IActionResult> SiparislerArsivPdf(string? tarih, string? durum, int? masaId, string? arama)
        {
            var siparisler = await _raporService.BuildSiparislerPdfListAsync(tarih, durum, masaId, arama);
            var html = await _viewRenderer.RenderViewToStringAsync("Admin/Export/OrdersTemplate", siparisler);
            using var stream = new MemoryStream();
            HtmlConverter.ConvertToPdf(html, stream);
            return File(stream.ToArray(), "application/pdf", $"siparis-arsivi-{DateTime.Now:yyyy-MM-dd-HHmm}.pdf");
        }

        [HttpGet("/admin/gun-sonu-excel")]
        public async Task<IActionResult> GunSonuExcel(string? tarih)
        {
            var model = await _raporService.BuildGunSonuExportModelAsync(tarih);
            var excelBytes = _raporService.CreateGunSonuExcel(model);
            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"gunluk-satis-{model.Tarih:yyyy-MM-dd}.xlsx");
        }

        [HttpGet("/admin/loglar")]
        public IActionResult Loglar()
        {
            ViewData["ActivePage"] = "Loglar";
            ViewData["PageTitle"] = "Güvenlik Logları";
            return View("~/Views/Admin/Loglar.cshtml");
        }

        [HttpGet("/admin/loglar/data")]
        public async Task<IActionResult> LoglarData(string? search, string? area, string? range, int? take)
        {
            try
            {
                var logs = await _raporService.GetLoglarDataAsync(search, area, range, take);
                return Json(new { success = true, data = logs, total = logs.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loglar verisi çekilirken hata oluştu");
                return Json(new { success = false, message = "Loglar yüklenirken bir hata oluştu." });
            }
        }

        [HttpGet("/admin/loglar-pdf")]
        public async Task<IActionResult> LoglarPdf(string search, string eventType, string range, string tarih)
        {
            var logs = await _raporService.GetLoglarPdfQueryAsync(search, eventType, range, tarih);

            var users = await _userManager.Users.ToListAsync();
            var userNames = new Dictionary<string, string>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var displayName = roles.Contains("Müşteri")
                    ? "Müşteri"
                    : (user.AdSoyad ?? user.UserName ?? "Bilinmeyen Kullanıcı");
                userNames[user.Id] = displayName;
            }
            ViewBag.UserNames = userNames;

            var html = await _viewRenderer.RenderViewToStringAsync("Admin/Export/LogsTemplate", logs);
            using var stream = new MemoryStream();
            HtmlConverter.ConvertToPdf(html, stream);
            return File(stream.ToArray(), "application/pdf", $"GuvenlikLoglari_{DateTime.Now:yyyyMMdd}.pdf");
        }

        [HttpGet("/admin/siparisler")]
        public async Task<IActionResult> Siparisler([FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] int? masaId = null)
        {
            ViewData["ActivePage"] = "Siparisler";
            ViewData["PageTitle"] = "Sipariş Geçmişi & Raporlama";

            var ozet = await _raporService.GetSiparisArsivOzetAsync(page, pageSize, masaId);

            ViewBag.BugunToplamSayi = ozet.BugunToplamSayi;
            ViewBag.BugunCiro = ozet.BugunCiro;
            ViewBag.IptalIadeSayi = ozet.IptalIadeSayi;
            ViewBag.IptalIadeCiro = ozet.IptalIadeCiro;
            ViewBag.CurrentPage = ozet.CurrentPage;
            ViewBag.PageSize = ozet.PageSize;
            ViewBag.TotalPages = ozet.TotalPages;
            ViewBag.TotalCount = ozet.TotalCount;
            if (masaId.HasValue)
            {
                ViewBag.FiltreliMasaId = masaId.Value;
            }

            return View("~/Views/Admin/Siparisler.cshtml", ozet.Siparisler);
        }

        [HttpGet("/admin/siparis-detay/{id:int}")]
        public async Task<IActionResult> SiparisDetayJson(int id)
        {
            var siparis = await _siparisService.GetSiparisAsync(id);
            if (siparis == null)
                return Json(new { success = false, message = "Sipariş bulunamadı." });

            return Json(new
            {
                success = true,
                id = siparis.Id,
                masaNo = siparis.Masa?.MasaNo,
                durum = siparis.Durum.ToString(),
                durumInt = (int)siparis.Durum,
                toplamTutar = siparis.ToplamTutar,
                notlar = siparis.Notlar,
                olusturmaTarihi = ToTurkeyTime(siparis.OlusturmaTarihi),
                guncellemeTarihi = ToTurkeyTime(siparis.GuncellemeTarihi),
                detaylar = siparis.SiparisDetaylar.Select(sd => new
                {
                    urunAd = sd.Urun.Ad,
                    adet = sd.Adet,
                    birimFiyat = sd.BirimFiyat,
                    opsiyonlar = sd.SeciliOpsiyonlar,
                    durum = sd.Durum.ToString()
                })
            });
        }

        [HttpPost("/admin/siparis-durum/{id:int}")]
        public async Task<IActionResult> AdminDurumGuncelle(int id, [FromBody] AdminController.AdminDurumRequest request)
        {
            try
            {
                if (!Enum.TryParse<SiparisDurum>(request.YeniDurum, out var yeniDurum))
                    return Json(new { success = false, message = "Geçersiz durum." });

                var siparis = await _siparisService.DurumGuncelleAsync(id, yeniDurum);
                _logger.LogInformation("Admin sipariş durumu güncelledi. SiparisId={Id}, YeniDurum={Durum}", id, yeniDurum);

                var masaNo = await _raporService.GetMasaNoByMasaIdAsync(siparis.MasaId);

                await Task.WhenAll(
                    _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisGuncellendi"),
                    _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisGuncellendi"),
                    _menuHub.Clients.Group(SignalRGroups.Cashier).SendAsync("SiparisGuncellendi"),
                    _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisGuncellendi")
                );

                if (yeniDurum == SiparisDurum.Hazir)
                {
                    await Task.WhenAll(
                        _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisHazir", masaNo),
                        _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisHazir", masaNo)
                    );
                }

                if (yeniDurum == SiparisDurum.Iptal)
                {
                    await Task.WhenAll(
                        _menuHub.Clients.Group(SignalRGroups.Waiter).SendAsync("SiparisIptal", masaNo),
                        _menuHub.Clients.Group(SignalRGroups.Kitchen).SendAsync("SiparisIptal", masaNo),
                        _menuHub.Clients.Group(SignalRGroups.Table(siparis.MasaId)).SendAsync("SiparisIptal", masaNo)
                    );
                }

                return Json(new { success = true, durum = siparis.Durum.ToString(), durumInt = (int)siparis.Durum });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin sipariş durum güncelleme hatası. SiparisId={Id}", id);
                return Json(new { success = false, message = "İşlem sırasında bir hata oluştu." });
            }
        }

        [HttpGet("/admin/siparisler-json")]
        public async Task<IActionResult> SiparislerJson([FromQuery] string? durum, [FromQuery] int? masaId)
        {
            var siparisler = await _raporService.GetSiparislerJsonListAsync(durum, masaId);

            return Json(siparisler.Select(s => new
            {
                id = s.Id,
                masaNo = s.MasaNo,
                durum = s.Durum,
                durumInt = s.DurumInt,
                toplamTutar = s.ToplamTutar,
                olusturmaTarihi = s.OlusturmaTarihi,
                urunSayisi = s.UrunSayisi,
                detayOzet = s.DetayOzet
            }));
        }
    }
}
