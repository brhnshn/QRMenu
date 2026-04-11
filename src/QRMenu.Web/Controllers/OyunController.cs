using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Data.Data;

namespace QRMenu.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class OyunController : Controller
    {
        private readonly QRMenuDbContext _context;

        public OyunController(QRMenuDbContext context)
        {
            _context = context;
        }

        [HttpGet("/admin/oyunlar")]
        public async Task<IActionResult> Index()
        {
            ViewData["ActivePage"] = "Oyunlar";
            var oyunlar = await _context.OyunAyarlar
                .Include(o => o.Oduller)
                .ToListAsync();

            if (!oyunlar.Any())
            {
                // Seed
                _context.OyunAyarlar.AddRange(
                    new OyunAyar { Ad = "Çarkıfelek", Tip = "CARKIFELEK", AktifMi = false },
                    new OyunAyar { Ad = "Hafıza Kartı", Tip = "HAFIZA", AktifMi = false },
                    new OyunAyar { Ad = "Kazı Kazan", Tip = "KAZIKAZAN", AktifMi = false }
                );
                await _context.SaveChangesAsync();
                oyunlar = await _context.OyunAyarlar.Include(o => o.Oduller).ToListAsync();
            }

            return View("~/Views/Admin/Oyunlar.cshtml", oyunlar);
        }

        [HttpPost("/admin/oyun-toggle/{id:int}")]
        public async Task<IActionResult> Toggle(int id)
        {
            var oyun = await _context.OyunAyarlar.FindAsync(id);
            if (oyun == null) return Json(new { success = false });

            oyun.AktifMi = !oyun.AktifMi;
            await _context.SaveChangesAsync();
            return Json(new { success = true, aktifMi = oyun.AktifMi });
        }

        [HttpPost("/admin/odul-baslat/{id:int}")]
        public async Task<IActionResult> OdulEkle(int id, [FromForm] string tanim, [FromForm] decimal indirimYuzdesi, [FromForm] decimal indirimTutari, [FromForm] decimal ihtimal)
        {
            var oyun = await _context.OyunAyarlar.FindAsync(id);
            if (oyun == null) return Json(new { success = false });

            oyun.Oduller.Add(new OyunOdul
            {
                OdulTanim = tanim,
                IndirimYuzdesi = indirimYuzdesi,
                IndirimTutari = indirimTutari,
                IhtimalYuzdesi = ihtimal
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("/admin/odul-sil/{id:int}")]
        public async Task<IActionResult> OdulSil(int id)
        {
            var odul = await _context.OyunOduller.FindAsync(id);
            if (odul == null) return Json(new { success = false });

            _context.OyunOduller.Remove(odul);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}