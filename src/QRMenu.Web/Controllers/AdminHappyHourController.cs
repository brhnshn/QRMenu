using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using QRMenu.Core.DTOs;
using QRMenu.Core.Interfaces;
using QRMenu.Web.Hubs;
using QRMenu.Web.ViewModels;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireAdmin")]
    public class AdminHappyHourController : Controller
    {
        private readonly IHappyHourYonetimService _happyHourService;
        private readonly IHubContext<OrderHub> _menuHub;
        private readonly ILogger<AdminHappyHourController> _logger;

        public AdminHappyHourController(
            IHappyHourYonetimService happyHourService,
            IHubContext<OrderHub> menuHub,
            ILogger<AdminHappyHourController> logger)
        {
            _happyHourService = happyHourService;
            _menuHub = menuHub;
            _logger = logger;
        }

        [HttpGet("/admin/happy-hour")]
        [HttpGet("/admin/happyhour")]
        public async Task<IActionResult> HappyHour()
        {
            ViewData["ActivePage"] = "HappyHour";
            ViewData["PageTitle"] = "İndirim Saatleri";

            var (happyHour, kategoriler, urunler, seciliUrunIds) = await _happyHourService.GetHappyHourDataAsync();
            ViewBag.Kategoriler = kategoriler;
            ViewBag.Urunler = urunler;
            ViewBag.SeciliUrunIds = seciliUrunIds;

            return View("~/Views/Admin/HappyHour.cshtml", happyHour);
        }

        [HttpGet("/admin/happy-hour-bilgi")]
        public async Task<IActionResult> HappyHourBilgi()
        {
            var info = await _happyHourService.GetHappyHourBilgiAsync();
            return Json(new
            {
                id = info.Id,
                aktifMi = info.AktifMi,
                suAnAktif = info.SuAnAktif,
                indirimOrani = info.IndirimOrani,
                baslangicSaati = info.BaslangicSaati,
                bitisSaati = info.BitisSaati,
                urunIds = info.UrunIds
            });
        }

        [HttpPost("/admin/happy-hour-kaydet")]
        public async Task<IActionResult> HappyHourKaydet([FromBody] HappyHourFormViewModel model)
        {
            var dto = new HappyHourKayitDto
            {
                BaslangicSaati = model.BaslangicSaati,
                BitisSaati = model.BitisSaati,
                IndirimOrani = model.IndirimOrani,
                AktifMi = model.AktifMi,
                UrunIds = model.UrunIds
            };

            var (success, message, _, _) = await _happyHourService.HappyHourKaydetAsync(dto);
            if (!success)
                return Json(new { success = false, message });

            await _menuHub.Clients.All.SendAsync("HappyHourGuncellendi");
            return Json(new { success = true, message });
        }
    }
}
