using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Data.Data;

namespace QRMenu.Web.Controllers
{
    [Authorize(Policy = "RequireAdmin")]
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
            var oyun = await _context.OyunAyarlar.Include(o => o.Oduller).FirstOrDefaultAsync(o => o.Id == id);
            if (oyun == null) return Json(new { success = false });

            if (!oyun.AktifMi)
            {
                var toplamIhtimal = oyun.Oduller.Sum(o => o.IhtimalYuzdesi);
                if (oyun.Oduller.Any() && toplamIhtimal != 100)
                {
                    return Json(new { success = false, message = "Oyun olasılıkları toplamı tam olarak 100 olmak zorundadır! Lütfen oranları düzenleyin." });
                }
            }

            oyun.AktifMi = !oyun.AktifMi;
            await _context.SaveChangesAsync();
            return Json(new { success = true, aktifMi = oyun.AktifMi });
        }

        [HttpPost("/admin/odul-toplu-kaydet/{id:int}")]
        public async Task<IActionResult> TopluOranKaydet(int id, [FromBody] Dictionary<int, decimal> oranlar)
        {
            var oyun = await _context.OyunAyarlar.Include(o => o.Oduller).FirstOrDefaultAsync(o => o.Id == id);
            if (oyun == null) return Json(new { success = false, message = "Oyun bulunamadı." });

            decimal toplam = 0;
            foreach(var odul in oyun.Oduller)
            {
                if(oranlar.TryGetValue(odul.Id, out decimal yeniOran))
                {
                    odul.IhtimalYuzdesi = yeniOran;
                    toplam += yeniOran;
                }
            }

            if (toplam > 100) return Json(new { success = false, message = $"Toplam ihtimal oranı %100'ü geçemez! Mevcut toplam: %{toplam}." });

            bool oyunKapatildi = false;
            if (oyun.AktifMi && toplam != 100 && oyun.Oduller.Any())
            {
                oyun.AktifMi = false;
                oyunKapatildi = true;
            }

            await _context.SaveChangesAsync();

            var mesaj = "Oranlar başarıyla kaydedildi!";
            if (oyunKapatildi)
            {
                mesaj = $"Oranlar kaydedildi ancak toplam %{toplam} yaptığı için hata çıkmaması adına oyun otomatik PASİFE çekildi.";
            }

            return Json(new { success = true, message = mesaj });
        }

        [HttpPost("/admin/odul-tekli-kaydet/{id:int}")]
        public async Task<IActionResult> TekliOranKaydet(int id, [FromForm] decimal yeniOran)
        {
            var odul = await _context.OyunOduller.FindAsync(id);
            if (odul == null) return Json(new { success = false, message = "Ödül bulunamadı." });

            odul.IhtimalYuzdesi = yeniOran;
            
            var oyun = await _context.OyunAyarlar.Include(o => o.Oduller).FirstOrDefaultAsync(o => o.Id == odul.OyunAyarId);
            bool oyunKapatildi = false;
            decimal toplamIhtimal = 0;

            if (oyun != null)
            {
                toplamIhtimal = oyun.Oduller.Sum(o => o.IhtimalYuzdesi);
                if (toplamIhtimal > 100) return Json(new { success = false, message = $"Toplam ihtimal oranı %100'ü geçemez! Mevcut toplam: %{toplamIhtimal}." });

                if (oyun.AktifMi && toplamIhtimal != 100)
                {
                    oyun.AktifMi = false;
                    oyunKapatildi = true;
                }
            }

            await _context.SaveChangesAsync();
            
            var mesaj = "Oran başarıyla güncellendi.";
            if (oyunKapatildi) 
            {
                mesaj = $"Oran güncellendi. Ancak toplam oran %{toplamIhtimal} yaptığı için hata çıkmaması adına oyun otomatik PASİFE çekildi.";
            }

            return Json(new { success = true, message = mesaj, kapatildi = oyunKapatildi, toplam = toplamIhtimal });
        }

        [HttpPost("/admin/odul-baslat/{id:int}")]
        public async Task<IActionResult> OdulEkle(int id, [FromForm] string tanim, [FromForm] decimal indirimYuzdesi, [FromForm] decimal indirimTutari, [FromForm] decimal ihtimal)
        {
            var oyun = await _context.OyunAyarlar.Include(o => o.Oduller).FirstOrDefaultAsync(o => o.Id == id);
            if (oyun == null) return Json(new { success = false, message = "Oyun bulunamadı." });

            decimal mevcutToplam = oyun.Oduller.Sum(o => o.IhtimalYuzdesi);
            if (mevcutToplam + ihtimal > 100)
            {
                return Json(new { success = false, message = $"Eklenecek ihtimal ile birlikte toplam oran %100'ü geçemez! Mevcut toplam: %{mevcutToplam}." });
            }

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
        [HttpGet("/admin/oyun-istatistik")]
        public async Task<IActionResult> GetIstatistik(string period = "today")
        {
            DateTime start, end;
            var now = DateTime.UtcNow;

            switch (period.ToLower())
            {
                case "yesterday":
                    start = now.Date.AddDays(-1);
                    end = now.Date;
                    break;
                case "week":
                    start = now.Date.AddDays(-(int)now.DayOfWeek);
                    end = now.Date.AddDays(1);
                    break;
                case "today":
                default:
                    start = now.Date;
                    end = now.Date.AddDays(1);
                    break;
            }

            var totalParticipations = await _context.Siparisler
                .CountAsync(s => s.OyunOynandiMi && s.OlusturmaTarihi >= start && s.OlusturmaTarihi < end);

            var totalWins = await _context.KazanilanIndirimler
                .CountAsync(k => k.KazanmaTarihi >= start && k.KazanmaTarihi < end);

            return Json(new { success = true, participations = totalParticipations, wins = totalWins });
        }
    }
}