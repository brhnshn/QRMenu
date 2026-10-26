using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class RaporYonetimService : IRaporYonetimService
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

        private static string ToTurkeyTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).ToString("dd.MM.yyyy HH:mm");

        private static string? ToTurkeyTime(DateTime? utc) =>
            utc.HasValue ? ToTurkeyTime(utc.Value) : null;

        private static DateTime ParseGunTarihi(string? tarih, DateTime fallbackDate)
        {
            return DateTime.TryParse(tarih, out var parsedDate) ? parsedDate.Date : fallbackDate.Date;
        }

        private static DateTime? ResolveLogCutoff(string? range)
        {
            var now = DateTime.UtcNow;
            return range switch
            {
                "15m" => now.AddMinutes(-15),
                "1h" => now.AddHours(-1),
                "24h" => now.AddHours(-24),
                "7d" => now.AddDays(-7),
                _ => null
            };
        }

        private static string? ParseOpsiyonOzet(string? seciliOpsiyonlar)
        {
            if (string.IsNullOrWhiteSpace(seciliOpsiyonlar) || seciliOpsiyonlar == "[]")
                return null;

            try
            {
                using var doc = JsonDocument.Parse(seciliOpsiyonlar);
                return string.Join(", ", doc.RootElement.EnumerateArray()
                    .Select(x => x.TryGetProperty("Ad", out var ad) ? ad.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            }
            catch
            {
                return null;
            }
        }

        private readonly QRMenuDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly ILogger<RaporYonetimService> _logger;

        public RaporYonetimService(
            QRMenuDbContext context,
            UserManager<Kullanici> userManager,
            ILogger<RaporYonetimService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync(DateTime? baslangic, DateTime? bitis)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz);
            var today = now.Date;
            var startDate = baslangic?.Date ?? today;
            var endDate = bitis?.Date ?? today;

            var startDateUtc = TimeZoneInfo.ConvertTimeToUtc(startDate, _turkeyTz);
            var endDateUtc = TimeZoneInfo.ConvertTimeToUtc(endDate.AddDays(1), _turkeyTz);

            var bugunSiparisQuery = _context.Siparisler
                .Where(s => s.OlusturmaTarihi >= startDateUtc && s.OlusturmaTarihi < endDateUtc)
                .Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.Iade)
                .AsNoTracking();

            var bugunCiro = await _context.Odemeler
                .Where(o => o.OdemeTarihi >= startDateUtc && o.OdemeTarihi < endDateUtc)
                .SumAsync(o => (decimal?)o.Tutar) ?? 0m;

            var zSiparisSayisi = await bugunSiparisQuery.CountAsync();

            var dunStartDateUtc = TimeZoneInfo.ConvertTimeToUtc(startDate.AddDays(-1), _turkeyTz);
            var dunEndDateUtc = TimeZoneInfo.ConvertTimeToUtc(endDate.AddDays(-1).AddDays(1), _turkeyTz);
            var dunCiro = await _context.Odemeler
                .Where(o => o.OdemeTarihi >= dunStartDateUtc && o.OdemeTarihi < dunEndDateUtc)
                .SumAsync(o => (decimal?)o.Tutar) ?? 0m;

            var ciroDegisim = dunCiro > 0 ? (double)((bugunCiro - dunCiro) / dunCiro * 100) : 0;

            var aktifMasalarCount = await _context.Masalar
                .CountAsync(m => m.AktifMi && m.Siparisler.Any(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.TamOdendi && s.Durum != SiparisDurum.Iade));

            var toplamMasaCount = await _context.Masalar.CountAsync(m => m.AktifMi);
            var dolulukOrani = toplamMasaCount > 0 ? (int)((double)aktifMasalarCount / toplamMasaCount * 100) : 0;

            var ortServisSuresi = await bugunSiparisQuery
                .Where(s => s.Durum == SiparisDurum.TeslimEdildi || s.Durum == SiparisDurum.TamOdendi)
                .AverageAsync(s => (double?)((s.GuncellemeTarihi ?? s.OlusturmaTarihi) - s.OlusturmaTarihi).TotalMinutes) ?? 0;

            List<SaatlikCiroRow> saatlikData;
            if (_context.Database.IsNpgsql())
            {
                saatlikData = await _context.Database
                    .SqlQuery<SaatlikCiroRow>($"""
                        SELECT date_part('hour', s."OlusturmaTarihi" AT TIME ZONE 'Europe/Istanbul')::int AS "Saat",
                               COALESCE(sum(s."ToplamTutar"), 0.0)::numeric AS "Ciro"
                        FROM "Siparisler" AS s
                        WHERE s."OlusturmaTarihi" >= {startDateUtc}
                          AND s."OlusturmaTarihi" < {endDateUtc}
                          AND s."Durum" NOT IN ({(int)SiparisDurum.Iptal}, {(int)SiparisDurum.Iade})
                        GROUP BY 1
                        ORDER BY 1
                        """)
                    .ToListAsync();
            }
            else
            {
                var siparisler = await _context.Siparisler
                    .Where(s => s.OlusturmaTarihi >= startDateUtc && s.OlusturmaTarihi < endDateUtc)
                    .Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.Iade)
                    .ToListAsync();

                saatlikData = siparisler
                    .GroupBy(s => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.OlusturmaTarihi, DateTimeKind.Utc), _turkeyTz).Hour)
                    .Select(g => new SaatlikCiroRow { Saat = g.Key, Ciro = g.Sum(s => s.ToplamTutar) })
                    .OrderBy(x => x.Saat)
                    .ToList();
            }

            var enCokSatanlar = await _context.SiparisDetaylar
                .Where(sd => sd.Siparis.OlusturmaTarihi >= startDateUtc && sd.Siparis.OlusturmaTarihi < endDateUtc)
                .Where(sd => sd.Siparis.Durum != SiparisDurum.Iptal && sd.Siparis.Durum != SiparisDurum.Iade &&
                             sd.Durum != SiparisDurum.Iptal && sd.Durum != SiparisDurum.Iade)
                .GroupBy(sd => new { sd.UrunId, sd.Urun.Ad, sd.Urun.GorselUrl })
                .Select(g => new EnCokSatanItemDto
                {
                    Ad = g.Key.Ad,
                    Adet = g.Sum(sd => sd.Adet),
                    GorselUrl = g.Key.GorselUrl,
                    Fiyat = g.Sum(sd => sd.Adet * sd.BirimFiyat)
                })
                .OrderByDescending(x => x.Adet)
                .Take(3)
                .ToListAsync();

            var bugunEnCokSatanlarTum = await _context.SiparisDetaylar
                .Where(sd => sd.Siparis.OlusturmaTarihi >= startDateUtc && sd.Siparis.OlusturmaTarihi < endDateUtc)
                .Where(sd => sd.Siparis.Durum != SiparisDurum.Iptal && sd.Siparis.Durum != SiparisDurum.Iade &&
                             sd.Durum != SiparisDurum.Iptal && sd.Durum != SiparisDurum.Iade)
                .GroupBy(sd => new { sd.UrunId, sd.Urun.Ad, sd.Urun.GorselUrl })
                .Select(g => new EnCokSatanItemDto
                {
                    Ad = g.Key.Ad,
                    Adet = g.Sum(sd => sd.Adet),
                    GorselUrl = g.Key.GorselUrl,
                    Fiyat = g.Sum(sd => sd.Adet * sd.BirimFiyat)
                })
                .OrderByDescending(x => x.Adet)
                .ThenBy(x => x.Ad)
                .ToListAsync();

            var canliSiparisler = await _context.Siparisler
                .AsNoTracking()
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Where(s => s.Durum == SiparisDurum.Onaylandi || s.Durum == SiparisDurum.Hazirlaniyor)
                .OrderByDescending(s => s.OlusturmaTarihi)
                .Take(4)
                .ToListAsync();

            var garsonCount = (await _userManager.GetUsersInRoleAsync("Garson")).Count;
            var mutfakCount = (await _userManager.GetUsersInRoleAsync("Mutfak")).Count;
            var kasaCount = (await _userManager.GetUsersInRoleAsync("Kasa")).Count;
            var personelCount = garsonCount + mutfakCount + kasaCount;

            var odemeTipleri = await _context.Odemeler
                .Where(o => o.OdemeTarihi >= startDateUtc && o.OdemeTarihi < endDateUtc)
                .GroupBy(o => o.OdemeTipi)
                .Select(g => new ZOdemeTipiItemDto
                {
                    Tip = g.Key.ToString(),
                    Tutar = g.Sum(o => o.Tutar),
                    Adet = g.Count()
                })
                .OrderByDescending(x => x.Tutar)
                .ToListAsync();

            var zRaporTarihi = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var gunSonuRapor = await _context.GunSonuRaporlari
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Tarih == zRaporTarihi);

            var zToplamCiro = odemeTipleri.Sum(x => x.Tutar);

            return new DashboardStatsDto
            {
                BugunCiro = bugunCiro,
                DunCiro = dunCiro,
                CiroDegisim = ciroDegisim,
                AktifMasalarCount = aktifMasalarCount,
                ToplamMasaCount = toplamMasaCount,
                DolulukOrani = dolulukOrani,
                OrtServisSuresi = Math.Round(ortServisSuresi, 1),
                SaatlikData = saatlikData,
                EnCokSatanlar = enCokSatanlar,
                BugunEnCokSatanlarTum = bugunEnCokSatanlarTum,
                CanliSiparisler = canliSiparisler,
                PersonelCount = personelCount,
                Baslangic = startDate,
                Bitis = endDate,
                ZRaporTarihi = startDate,
                ZToplamCiro = gunSonuRapor?.ToplamCiro ?? zToplamCiro,
                ZSiparisSayisi = gunSonuRapor?.SiparisSayisi ?? zSiparisSayisi,
                ZOdemeTipleri = gunSonuRapor != null
                    ? JsonSerializer.Deserialize<List<ZOdemeTipiItemDto>>(gunSonuRapor.OdemeTipleriJson) ?? new List<ZOdemeTipiItemDto>()
                    : odemeTipleri,
                GunKapaliMi = gunSonuRapor != null,
                GunKapanisTarihi = gunSonuRapor?.KapanisTarihi,
                ZNextOpeningIso = startDate.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss"),
                GunKapanisMetni = gunSonuRapor?.KapanisTarihi != null
                    ? ToTurkeyTime(gunSonuRapor.KapanisTarihi)
                    : null
            };
        }

        public async Task<(bool Success, string? Message)> GunSonuKapatAsync(string? tarih, string? userId)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz);
            var gun = DateTime.TryParse(tarih, out var parsedDate) ? parsedDate.Date : now.Date;
            var startDateUtc = TimeZoneInfo.ConvertTimeToUtc(gun, _turkeyTz);
            var endDateUtc = TimeZoneInfo.ConvertTimeToUtc(gun.AddDays(1), _turkeyTz);
            var raporTarihi = DateTime.SpecifyKind(gun, DateTimeKind.Utc);

            var mevcut = await _context.GunSonuRaporlari.FirstOrDefaultAsync(r => r.Tarih == raporTarihi);
            if (mevcut != null)
                return (false, "Bu gün zaten kapatılmış.");

            var acikSiparisSayisi = await _context.Siparisler
                .Where(s => s.OlusturmaTarihi >= startDateUtc && s.OlusturmaTarihi < endDateUtc)
                .Where(s => s.Durum == SiparisDurum.Onaylandi ||
                            s.Durum == SiparisDurum.Hazirlaniyor ||
                            s.Durum == SiparisDurum.Hazir ||
                            s.Durum == SiparisDurum.TeslimEdildi ||
                            s.Durum == SiparisDurum.KismiOdendi)
                .CountAsync();

            if (acikSiparisSayisi > 0)
                return (false, $"Kapatılmamış {acikSiparisSayisi} adet açık sipariş bulunmaktadır. Gün sonu kapatılamaz.");

            var siparisSayisi = await _context.Siparisler
                .Where(s => s.OlusturmaTarihi >= startDateUtc && s.OlusturmaTarihi < endDateUtc)
                .Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.Iade)
                .CountAsync();

            var odemeTipleri = await _context.Odemeler
                .Where(o => o.OdemeTarihi >= startDateUtc && o.OdemeTarihi < endDateUtc)
                .GroupBy(o => o.OdemeTipi)
                .Select(g => new ZOdemeTipiItemDto
                {
                    Tip = g.Key.ToString(),
                    Tutar = g.Sum(o => o.Tutar),
                    Adet = g.Count()
                })
                .OrderByDescending(x => x.Tutar)
                .ToListAsync();

            var rapor = new GunSonuRapor
            {
                Tarih = raporTarihi,
                ToplamCiro = odemeTipleri.Sum(x => x.Tutar),
                SiparisSayisi = siparisSayisi,
                OdemeTipleriJson = JsonSerializer.Serialize(odemeTipleri),
                KapanisTarihi = DateTime.UtcNow,
                KapatanKullaniciId = userId
            };

            try
            {
                _context.GunSonuRaporlari.Add(rapor);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Gün sonu kapatılırken çakışma oluştu. Tarih={Tarih}", gun);
                return (false, "Bu gün zaten kapatılmış.");
            }

            _logger.LogInformation("Gün sonu kapatıldı. Tarih={Tarih}, Ciro={Ciro}, SiparisSayisi={SiparisSayisi}", gun, rapor.ToplamCiro, rapor.SiparisSayisi);
            return (true, null);
        }

        public async Task<(bool Success, string? Message)> GunSonuAcAsync(string? tarih)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz);
            var gun = ParseGunTarihi(tarih, now);
            var raporTarihi = DateTime.SpecifyKind(gun, DateTimeKind.Utc);

            var rapor = await _context.GunSonuRaporlari.FirstOrDefaultAsync(r => r.Tarih == raporTarihi);
            if (rapor == null)
                return (false, "Açılacak kapalı gün bulunamadı.");

            _context.GunSonuRaporlari.Remove(rapor);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Gün sonu tekrar açıldı. Tarih={Tarih}", gun);
            return (true, null);
        }

        public async Task<GunSonuExportDto> BuildGunSonuExportModelAsync(string? tarih)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz);
            var gun = ParseGunTarihi(tarih, now);
            var startDateUtc = TimeZoneInfo.ConvertTimeToUtc(gun, _turkeyTz);
            var endDateUtc = TimeZoneInfo.ConvertTimeToUtc(gun.AddDays(1), _turkeyTz);
            var raporTarihi = DateTime.SpecifyKind(gun, DateTimeKind.Utc);

            var siparisler = await _context.Siparisler
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .Where(s => s.OlusturmaTarihi >= startDateUtc && s.OlusturmaTarihi < endDateUtc)
                .Where(s => s.Durum != SiparisDurum.Iptal && s.Durum != SiparisDurum.Iade)
                .OrderBy(s => s.OlusturmaTarihi)
                .AsSplitQuery()
                .ToListAsync();

            var odemeTipleri = await _context.Odemeler
                .Where(o => o.OdemeTarihi >= startDateUtc && o.OdemeTarihi < endDateUtc)
                .GroupBy(o => o.OdemeTipi)
                .Select(g => new ZOdemeTipiItemDto
                {
                    Tip = g.Key.ToString(),
                    Tutar = g.Sum(o => o.Tutar),
                    Adet = g.Count()
                })
                .OrderByDescending(x => x.Tutar)
                .ToListAsync();

            var gunSonuRapor = await _context.GunSonuRaporlari
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Tarih == raporTarihi);

            return new GunSonuExportDto
            {
                Tarih = gun,
                KapaliMi = gunSonuRapor != null,
                KapanisTarihi = gunSonuRapor?.KapanisTarihi,
                ToplamCiro = gunSonuRapor?.ToplamCiro ?? odemeTipleri.Sum(x => x.Tutar),
                SiparisSayisi = gunSonuRapor?.SiparisSayisi ?? siparisler.Count,
                OdemeTipleri = gunSonuRapor != null
                    ? JsonSerializer.Deserialize<List<ZOdemeTipiItemDto>>(gunSonuRapor.OdemeTipleriJson) ?? new List<ZOdemeTipiItemDto>()
                    : odemeTipleri,
                Siparisler = siparisler.Select(s => new GunSonuExportSiparisDto
                {
                    Id = s.Id,
                    MasaNo = s.Masa?.MasaNo,
                    Durum = s.Durum.ToString(),
                    Saat = ToTurkeyTime(s.OlusturmaTarihi),
                    ToplamTutar = s.ToplamTutar,
                    Notlar = s.Notlar,
                    Urunler = s.SiparisDetaylar.Select(sd => new GunSonuExportDetayDto
                    {
                        UrunAd = sd.Urun.Ad,
                        Adet = sd.Adet,
                        BirimFiyat = sd.BirimFiyat,
                        Secenekler = ParseOpsiyonOzet(sd.SeciliOpsiyonlar)
                    }).ToList()
                }).ToList()
            };
        }

        public byte[] CreateGunSonuExcel(GunSonuExportDto model)
        {
            using var workbook = new XLWorkbook();

            var ozet = workbook.Worksheets.Add("Z Raporu");
            ozet.Cell(1, 1).Value = "Gun Sonu Z Raporu";
            ozet.Cell(2, 1).Value = "Tarih";
            ozet.Cell(2, 2).Value = model.Tarih;
            ozet.Cell(3, 1).Value = "Toplam Ciro";
            ozet.Cell(3, 2).Value = model.ToplamCiro;
            ozet.Cell(4, 1).Value = "Siparis Sayisi";
            ozet.Cell(4, 2).Value = model.SiparisSayisi;
            ozet.Cell(5, 1).Value = "Toplam Urun";
            ozet.Cell(5, 2).Value = model.Siparisler.Sum(x => x.Urunler.Sum(y => y.Adet));
            ozet.Cell(7, 1).Value = "Odeme Tipi";
            ozet.Cell(7, 2).Value = "Adet";
            ozet.Cell(7, 3).Value = "Tutar";

            var odemeSatir = 8;
            foreach (var odeme in model.OdemeTipleri)
            {
                ozet.Cell(odemeSatir, 1).Value = odeme.Tip;
                ozet.Cell(odemeSatir, 2).Value = odeme.Adet;
                ozet.Cell(odemeSatir, 3).Value = odeme.Tutar;
                odemeSatir++;
            }

            var detay = workbook.Worksheets.Add("Detayli Satislar");
            detay.Cell(1, 1).Value = "Siparis No";
            detay.Cell(1, 2).Value = "Saat";
            detay.Cell(1, 3).Value = "Masa";
            detay.Cell(1, 4).Value = "Durum";
            detay.Cell(1, 5).Value = "Urun";
            detay.Cell(1, 6).Value = "Adet";
            detay.Cell(1, 7).Value = "Birim Fiyat";
            detay.Cell(1, 8).Value = "Satir Toplami";
            detay.Cell(1, 9).Value = "Secenekler";
            detay.Cell(1, 10).Value = "Siparis Notu";

            var detaySatir = 2;
            foreach (var siparis in model.Siparisler)
            {
                foreach (var urun in siparis.Urunler)
                {
                    detay.Cell(detaySatir, 1).Value = siparis.Id;
                    detay.Cell(detaySatir, 2).Value = siparis.Saat;
                    detay.Cell(detaySatir, 3).Value = siparis.MasaNo?.ToString() ?? "-";
                    detay.Cell(detaySatir, 4).Value = siparis.Durum;
                    detay.Cell(detaySatir, 5).Value = urun.UrunAd;
                    detay.Cell(detaySatir, 6).Value = urun.Adet;
                    detay.Cell(detaySatir, 7).Value = urun.BirimFiyat;
                    detay.Cell(detaySatir, 8).Value = urun.Adet * urun.BirimFiyat;
                    detay.Cell(detaySatir, 9).Value = urun.Secenekler ?? string.Empty;
                    detay.Cell(detaySatir, 10).Value = siparis.Notlar ?? string.Empty;
                    detaySatir++;
                }
            }

            ozet.Range(1, 1, 1, 3).Merge().Style.Font.SetBold();
            ozet.Range(7, 1, 7, 3).Style.Font.SetBold();
            detay.Range(1, 1, 1, 10).Style.Font.SetBold();

            ozet.Cell(2, 2).Style.DateFormat.Format = "dd.MM.yyyy";
            ozet.Cell(3, 2).Style.NumberFormat.Format = "#,##0.00";
            ozet.Cell(4, 2).Style.NumberFormat.Format = "0";
            ozet.Cell(5, 2).Style.NumberFormat.Format = "0";

            if (odemeSatir > 8)
            {
                ozet.Range(8, 2, odemeSatir - 1, 2).Style.NumberFormat.Format = "0";
                ozet.Range(8, 3, odemeSatir - 1, 3).Style.NumberFormat.Format = "#,##0.00";
            }

            if (detaySatir > 2)
            {
                detay.Range(2, 1, detaySatir - 1, 1).Style.NumberFormat.Format = "0";
                detay.Range(2, 6, detaySatir - 1, 6).Style.NumberFormat.Format = "0";
                detay.Range(2, 7, detaySatir - 1, 8).Style.NumberFormat.Format = "#,##0.00";
            }

            foreach (var ws in workbook.Worksheets)
            {
                ws.Columns().AdjustToContents();
                ws.Rows().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<List<Siparis>> BuildSiparislerPdfListAsync(string? tarih, string? durum, int? masaId, string? arama)
        {
            var query = _context.Siparisler
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsNoTracking()
                .AsQueryable();

            if (tarih == "today")
            {
                var bugunTr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz).Date;
                var bas = TimeZoneInfo.ConvertTimeToUtc(bugunTr, _turkeyTz);
                var bit = TimeZoneInfo.ConvertTimeToUtc(bugunTr.AddDays(1), _turkeyTz);
                query = query.Where(s => s.OlusturmaTarihi >= bas && s.OlusturmaTarihi < bit);
            }
            else if (tarih == "yesterday")
            {
                var dunTr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz).Date.AddDays(-1);
                var bas = TimeZoneInfo.ConvertTimeToUtc(dunTr, _turkeyTz);
                var bit = TimeZoneInfo.ConvertTimeToUtc(dunTr.AddDays(1), _turkeyTz);
                query = query.Where(s => s.OlusturmaTarihi >= bas && s.OlusturmaTarihi < bit);
            }
            else if (tarih == "haftalik")
            {
                var bugunTr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz).Date;
                int gunFarki = ((int)bugunTr.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                var haftaBasiTr = bugunTr.AddDays(-gunFarki);
                var bas = TimeZoneInfo.ConvertTimeToUtc(haftaBasiTr, _turkeyTz);
                var bit = TimeZoneInfo.ConvertTimeToUtc(bugunTr.AddDays(1), _turkeyTz);
                query = query.Where(s => s.OlusturmaTarihi >= bas && s.OlusturmaTarihi < bit);
            }
            else if (tarih == "aylik")
            {
                var bugunTr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz).Date;
                var ayBasiTr = new DateTime(bugunTr.Year, bugunTr.Month, 1);
                var bas = TimeZoneInfo.ConvertTimeToUtc(ayBasiTr, _turkeyTz);
                var bit = TimeZoneInfo.ConvertTimeToUtc(bugunTr.AddDays(1), _turkeyTz);
                query = query.Where(s => s.OlusturmaTarihi >= bas && s.OlusturmaTarihi < bit);
            }

            if (durum == "bekleyen")
            {
                var aktifDurumlar = new[] { SiparisDurum.Onaylandi, SiparisDurum.Hazirlaniyor, SiparisDurum.Hazir, SiparisDurum.TeslimEdildi, SiparisDurum.KismiOdendi };
                query = query.Where(s => aktifDurumlar.Contains(s.Durum));
            }
            else if (!string.IsNullOrEmpty(durum) && durum != "all" && Enum.TryParse<SiparisDurum>(durum, out var durumEnum))
            {
                query = query.Where(s => s.Durum == durumEnum);
            }

            if (masaId.HasValue && masaId > 0)
                query = query.Where(s => s.MasaId == masaId.Value);

            if (!string.IsNullOrWhiteSpace(arama))
            {
                var clean = arama.Replace("#", "").Trim();
                if (int.TryParse(clean, out var noSearch))
                    query = query.Where(s => s.Id == noSearch || s.GunlukSiparisNo == noSearch);
                else
                    query = query.Where(s => s.Notlar != null && s.Notlar.Contains(arama));
            }

            return await query.OrderByDescending(s => s.OlusturmaTarihi).ToListAsync();
        }

        public async Task<List<LogListItemDto>> GetLoglarDataAsync(string? search, string? area, string? range, int? take)
        {
            var cutoff = ResolveLogCutoff(range);
            var maxTake = Math.Clamp(take ?? 700, 1, 2000);

            var securityQuery = _context.SecurityLogs.AsNoTracking();

            if (cutoff.HasValue)
                securityQuery = securityQuery.Where(l => l.Timestamp >= cutoff.Value);

            if (!string.IsNullOrWhiteSpace(area))
            {
                if (area.Equals("Kasa", StringComparison.OrdinalIgnoreCase))
                    securityQuery = securityQuery.Where(l => (l.EventType != null && l.EventType.Contains("Kasa")) || (l.Path != null && l.Path.Contains("/Kasa")));
                else if (area.Equals("Mutfak", StringComparison.OrdinalIgnoreCase))
                    securityQuery = securityQuery.Where(l => (l.EventType != null && l.EventType.Contains("Mutfak")) || (l.Path != null && l.Path.Contains("/Mutfak")));
                else if (area.Equals("Garson", StringComparison.OrdinalIgnoreCase))
                    securityQuery = securityQuery.Where(l => (l.EventType != null && l.EventType.Contains("Garson")) || (l.Path != null && l.Path.Contains("/Garson")));
                else if (area.Equals("Güvenlik", StringComparison.OrdinalIgnoreCase))
                    securityQuery = securityQuery.Where(l => (l.EventType == null || !l.EventType.Contains("Kasa")) && (l.Path == null || !l.Path.Contains("/Kasa")) &&
                                                             (l.EventType == null || !l.EventType.Contains("Mutfak")) && (l.Path == null || !l.Path.Contains("/Mutfak")) &&
                                                             (l.EventType == null || !l.EventType.Contains("Garson")) && (l.Path == null || !l.Path.Contains("/Garson")));
            }

            var securityLogs = await securityQuery
                .OrderByDescending(l => l.Timestamp)
                .Take(maxTake)
                .ToListAsync();

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

            var items = securityLogs.Select(l => new LogListItemDto
            {
                Id = l.Id,
                Timestamp = ToTurkeyTime(l.Timestamp),
                TimestampIso = l.Timestamp.ToString("o"),
                EventType = l.EventType,
                Severity = l.Severity,
                Message = l.Message,
                Method = l.Method,
                Path = l.Path,
                User = l.UserId != null && userNames.TryGetValue(l.UserId, out var name) ? name : (l.UserId ?? "Anonim"),
                Meta = l.IpAddress
            }).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                items = items.Where(i =>
                    i.Message.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (i.Path != null && i.Path.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (i.Meta != null && i.Meta.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (i.User != null && i.User.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    i.EventType.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return items;
        }

        public async Task<List<SecurityLog>> GetLoglarPdfQueryAsync(string search, string eventType, string range, string tarih)
        {
            var query = _context.SecurityLogs.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l =>
                    (l.Message != null && l.Message.Contains(search)) ||
                    (l.IpAddress != null && l.IpAddress.Contains(search)) ||
                    (l.Path != null && l.Path.Contains(search)));
            }

            if (!string.IsNullOrEmpty(eventType))
            {
                query = query.Where(l => l.EventType == eventType);
            }

            var nowUtc = DateTime.UtcNow;
            var turkeyNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _turkeyTz);
            DateTime? cutoffUtc = null;

            if (!string.IsNullOrEmpty(tarih))
            {
                switch (tarih)
                {
                    case "today":
                        cutoffUtc = TimeZoneInfo.ConvertTimeToUtc(turkeyNow.Date, _turkeyTz);
                        break;
                    case "yesterday":
                        var yesterdayStart = turkeyNow.Date.AddDays(-1);
                        var yesterdayEnd = turkeyNow.Date;
                        var yesterdayStartUtc = TimeZoneInfo.ConvertTimeToUtc(yesterdayStart, _turkeyTz);
                        var yesterdayEndUtc = TimeZoneInfo.ConvertTimeToUtc(yesterdayEnd, _turkeyTz);
                        query = query.Where(l => l.Timestamp >= yesterdayStartUtc && l.Timestamp < yesterdayEndUtc);
                        break;
                    case "haftalik":
                        int diff = (7 + (turkeyNow.DayOfWeek - DayOfWeek.Monday)) % 7;
                        var weekStart = turkeyNow.Date.AddDays(-1 * diff);
                        cutoffUtc = TimeZoneInfo.ConvertTimeToUtc(weekStart, _turkeyTz);
                        break;
                    case "aylik":
                        var monthStart = new DateTime(turkeyNow.Year, turkeyNow.Month, 1);
                        cutoffUtc = TimeZoneInfo.ConvertTimeToUtc(monthStart, _turkeyTz);
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(range))
            {
                cutoffUtc = range switch
                {
                    "15m" => nowUtc.AddMinutes(-15),
                    "1h" => nowUtc.AddHours(-1),
                    "24h" => nowUtc.AddHours(-24),
                    _ => null
                };
            }

            if (cutoffUtc.HasValue)
            {
                query = query.Where(l => l.Timestamp >= cutoffUtc.Value);
            }

            return await query.OrderByDescending(l => l.Timestamp).Take(2000).ToListAsync();
        }

        public async Task<SiparisArsivOzetDto> GetSiparisArsivOzetAsync(int page, int pageSize, int? masaId)
        {
            if (page < 1) page = 1;
            pageSize = Math.Clamp(pageSize, 20, 200);

            var siparisBaseQuery = _context.Siparisler.AsNoTracking().AsQueryable();

            var bugunTr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _turkeyTz).Date;
            var bugunBaslangicUtc = TimeZoneInfo.ConvertTimeToUtc(bugunTr, _turkeyTz);
            var bugunBitisUtc = TimeZoneInfo.ConvertTimeToUtc(bugunTr.AddDays(1), _turkeyTz);

            var bugunToplamSayi = await siparisBaseQuery
                .Where(s => s.OlusturmaTarihi >= bugunBaslangicUtc && s.OlusturmaTarihi < bugunBitisUtc)
                .CountAsync();

            var bugunCiro = await siparisBaseQuery
                .Where(s => s.OlusturmaTarihi >= bugunBaslangicUtc
                            && s.OlusturmaTarihi < bugunBitisUtc
                            && s.Durum == SiparisDurum.TamOdendi)
                .SumAsync(s => (decimal?)s.ToplamTutar) ?? 0m;

            var iptalIadeStats = await siparisBaseQuery
                .Where(s => s.Durum == SiparisDurum.Iptal || s.Durum == SiparisDurum.Iade)
                .GroupBy(s => 1)
                .Select(g => new { Count = g.Count(), Total = g.Sum(x => (decimal?)x.ToplamTutar) ?? 0m })
                .FirstOrDefaultAsync();

            var query = siparisBaseQuery;
            if (masaId.HasValue)
            {
                query = query.Where(s => s.MasaId == masaId.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var siparisler = await query
                .Include(s => s.Masa)
                .ThenInclude(m => m.Bolge)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .OrderByDescending(s => s.OlusturmaTarihi)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new SiparisArsivOzetDto
            {
                BugunToplamSayi = bugunToplamSayi,
                BugunCiro = bugunCiro,
                IptalIadeSayi = iptalIadeStats?.Count ?? 0,
                IptalIadeCiro = iptalIadeStats?.Total ?? 0m,
                TotalCount = totalCount,
                TotalPages = totalPages,
                CurrentPage = page,
                PageSize = pageSize,
                Siparisler = siparisler
            };
        }

        public async Task<int?> GetMasaNoByMasaIdAsync(int masaId)
        {
            return await _context.Masalar
                .Where(m => m.Id == masaId)
                .Select(m => (int?)m.MasaNo)
                .FirstOrDefaultAsync();
        }

        public async Task<List<SiparisListItemDto>> GetSiparislerJsonListAsync(string? durum, int? masaId)
        {
            var query = _context.Siparisler
                .Include(s => s.Masa)
                .Include(s => s.SiparisDetaylar)
                    .ThenInclude(sd => sd.Urun)
                .AsSplitQuery()
                .AsQueryable();

            if (!string.IsNullOrEmpty(durum) && Enum.TryParse<SiparisDurum>(durum, out var durumEnum))
                query = query.Where(s => s.Durum == durumEnum);

            if (masaId.HasValue)
                query = query.Where(s => s.MasaId == masaId.Value);

            var siparisler = await query
                .OrderByDescending(s => s.OlusturmaTarihi)
                .ToListAsync();

            return siparisler.Select(s => new SiparisListItemDto
            {
                Id = s.Id,
                MasaNo = s.Masa?.MasaNo,
                Durum = s.Durum.ToString(),
                DurumInt = (int)s.Durum,
                ToplamTutar = s.ToplamTutar,
                OlusturmaTarihi = ToTurkeyTime(s.OlusturmaTarihi),
                UrunSayisi = s.SiparisDetaylar.Sum(sd => sd.Adet),
                DetayOzet = string.Join(", ", s.SiparisDetaylar.Select(sd => $"{sd.Adet}x {sd.Urun.Ad}"))
            }).ToList();
        }
    }
}
