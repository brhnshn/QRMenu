using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;

namespace QRMenu.Data.Services
{
    public class UrunYonetimService : IUrunYonetimService
    {
        private static readonly TimeZoneInfo _turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

        private static DateTime RaporTarihi(DateTime utc) =>
            DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _turkeyTz).Date, DateTimeKind.Utc);

        private static void ApplyUrunStokDurumu(Urun urun, int stokAdet, bool adminAktifIstiyor)
        {
            urun.StokAdet = Math.Max(0, stokAdet);

            if (!adminAktifIstiyor)
            {
                urun.AdminManuelPasifMi = true;
                urun.AktifMi = false;
                return;
            }

            urun.AdminManuelPasifMi = false;
            urun.AktifMi = urun.StokAdet > 0;
        }

        private readonly QRMenuDbContext _context;
        private readonly ILogger<UrunYonetimService> _logger;

        public UrunYonetimService(QRMenuDbContext context, ILogger<UrunYonetimService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Kategori>> GetKategorilerWithUrunlerAsync()
        {
            return await _context.Kategoriler
                .Include(k => k.Urunler)
                    .ThenInclude(u => u.UrunOpsiyonlar)
                        .ThenInclude(uo => uo.Opsiyon)
                .OrderBy(k => k.SiraNo)
                .AsSplitQuery()
                .ToListAsync();
        }

        public async Task<List<Kategori>> GetKategorilerAsync()
        {
            return await _context.Kategoriler
                .Include(k => k.Urunler)
                .OrderBy(k => k.SiraNo)
                .ToListAsync();
        }

        public async Task<Kategori> KategoriEkleAsync(string ad, string? adEN, int siraNo)
        {
            var kategori = new Kategori
            {
                Ad = ad,
                AdEN = adEN,
                SiraNo = siraNo,
                AktifMi = true
            };

            _context.Kategoriler.Add(kategori);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Kategori eklendi. Id={Id}, Ad={Ad}", kategori.Id, kategori.Ad);
            return kategori;
        }

        public async Task<bool> KategoriGuncelleAsync(int id, string ad, string? adEN, int siraNo)
        {
            var kategori = await _context.Kategoriler.FindAsync(id);
            if (kategori == null) return false;

            kategori.Ad = ad;
            kategori.AdEN = adEN;
            kategori.SiraNo = siraNo;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Kategori güncellendi. Id={Id}", id);
            return true;
        }

        public async Task<(bool Success, string? ErrorMessage)> KategoriSilAsync(int id)
        {
            var kategori = await _context.Kategoriler
                .Include(k => k.Urunler)
                .AsSplitQuery()
                .FirstOrDefaultAsync(k => k.Id == id);

            if (kategori == null)
                return (false, "Kategori bulunamadı.");

            if (kategori.Urunler.Any())
                return (false, $"Bu kategoride {kategori.Urunler.Count} ürün var. Önce ürünleri taşıyın veya silin.");

            _context.Kategoriler.Remove(kategori);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Kategori silindi. Id={Id}, Ad={Ad}", id, kategori.Ad);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage, int Count)> UrunTasiAsync(List<int> urunIds, int yeniKategoriId)
        {
            if (urunIds == null || !urunIds.Any())
                return (false, "Taşınacak ürün seçilmedi.", 0);

            var kategori = await _context.Kategoriler.FindAsync(yeniKategoriId);
            if (kategori == null)
                return (false, "Hedef kategori bulunamadı.", 0);

            var urunler = await _context.Urunler
                .Where(u => urunIds.Contains(u.Id))
                .ToListAsync();

            if (!urunler.Any())
                return (false, "Seçilen ürünler bulunamadı.", 0);

            foreach (var urun in urunler)
            {
                urun.KategoriId = yeniKategoriId;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Ürünler taşındı. Adet={Adet}, Yeni Kategori={KatId}", urunler.Count, yeniKategoriId);
            return (true, null, urunler.Count);
        }

        public async Task<Urun?> GetUrunDetayAsync(int id)
        {
            return await _context.Urunler
                .Include(u => u.Kategori)
                .Include(u => u.UrunOpsiyonlar)
                    .ThenInclude(uo => uo.Opsiyon)
                .AsSplitQuery()
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<Urun> UrunEkleAsync(Urun urun, int stokAdet, bool aktifMi)
        {
            ApplyUrunStokDurumu(urun, stokAdet, aktifMi);
            _context.Urunler.Add(urun);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Ürün eklendi. Id={Id}, Ad={Ad}", urun.Id, urun.Ad);
            return urun;
        }

        public async Task<(bool Success, string? ErrorMessage)> UrunGuncelleAsync(int id, Action<Urun> updateAction, int stokAdet, bool aktifMi)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null)
                return (false, "Ürün bulunamadı.");

            updateAction(urun);
            ApplyUrunStokDurumu(urun, stokAdet, aktifMi);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Ürün güncellendi. Id={Id}", id);
            return (true, null);
        }

        public async Task<(bool Success, string? Message)> UrunSilAsync(int id)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null)
                return (false, "Ürün bulunamadı.");

            // Aktif (tamamlanmamış) siparişlerde bağlı detay varsa pasife çek
            var aktifSiparisVar = await _context.SiparisDetaylar
                .AnyAsync(sd => sd.UrunId == id
                    && sd.Siparis.Durum != SiparisDurum.Iptal
                    && sd.Siparis.Durum != SiparisDurum.TamOdendi
                    && sd.Siparis.Durum != SiparisDurum.Iade);

            if (aktifSiparisVar)
            {
                urun.AktifMi = false;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Ürün pasife alındı (aktif sipariş var). Id={Id}", id);
                return (true, "Ürün pasife alındı (aktif sipariş kayıtları var).");
            }

            var urunSiparisTarihleri = await _context.SiparisDetaylar
                .Where(sd => sd.UrunId == id)
                .Select(sd => sd.Siparis.OlusturmaTarihi)
                .ToListAsync();

            var kilitliRaporTarihleri = urunSiparisTarihleri
                .Select(RaporTarihi)
                .Distinct()
                .ToList();

            var kilitliKayitVar = kilitliRaporTarihleri.Any()
                && await _context.GunSonuRaporlari.AnyAsync(r => kilitliRaporTarihleri.Contains(r.Tarih));

            if (kilitliKayitVar)
            {
                urun.AktifMi = false;
                urun.AdminManuelPasifMi = true;
                await _context.SaveChangesAsync();
                return (true, "Ürün kapatılmış gün sonu kayıtlarında yer aldığı için silinmedi, pasife alındı.");
            }

            // FK Restrict olduğu için ilişkili kayıtları temizle
            var sepetDetaylar = await _context.SepetDetaylar.Where(sd => sd.UrunId == id).ToListAsync();
            if (sepetDetaylar.Any())
                _context.SepetDetaylar.RemoveRange(sepetDetaylar);

            var siparisDetaylar = await _context.SiparisDetaylar.Where(sd => sd.UrunId == id).ToListAsync();
            if (siparisDetaylar.Any())
                _context.SiparisDetaylar.RemoveRange(siparisDetaylar);

            _context.Urunler.Remove(urun);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Ürün silindi. Id={Id}, Ad={Ad}", id, urun.Ad);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage, bool AktifMi)> UrunToggleAsync(int id)
        {
            var urun = await _context.Urunler.FindAsync(id);
            if (urun == null)
                return (false, "Ürün bulunamadı.", false);

            if (urun.AktifMi)
            {
                urun.AktifMi = false;
                urun.AdminManuelPasifMi = true;
            }
            else
            {
                if (urun.StokAdet <= 0)
                    return (false, "Stok 0 iken urun aktif yapilamaz.", false);

                urun.AdminManuelPasifMi = false;
                urun.AktifMi = true;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Ürün durumu değiştirildi. Id={Id}, AktifMi={Aktif}", id, urun.AktifMi);
            return (true, null, urun.AktifMi);
        }

        public async Task<(bool Success, string? ErrorMessage, UrunStokGuncelleSonucDto? Sonuc)> UrunStokGuncelleAsync(int id, int stokAdet)
        {
            if (stokAdet < 0)
                return (false, "Stok 0 veya daha buyuk olmalidir.", null);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var urun = await _context.Urunler.FirstOrDefaultAsync(u => u.Id == id);
                if (urun == null)
                    return (false, "Urun bulunamadi.", null);

                urun.StokAdet = stokAdet;
                if (urun.StokAdet <= 0)
                {
                    urun.AktifMi = false;
                }
                else if (!urun.AdminManuelPasifMi)
                {
                    urun.AktifMi = true;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, null, new UrunStokGuncelleSonucDto
                {
                    StokAdet = urun.StokAdet,
                    AktifMi = urun.AktifMi,
                    AdminManuelPasifMi = urun.AdminManuelPasifMi
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<(bool Success, string? ErrorMessage, int OpsiyonId)> OpsiyonEkleAsync(int urunId, string ad, string? adEN, string grup, string? grupEN, decimal ekFiyat, bool zorunlu)
        {
            var urun = await _context.Urunler.FindAsync(urunId);
            if (urun == null)
                return (false, "Ürün bulunamadı.", 0);

            var mevcutOpsiyon = await _context.Opsiyonlar
                .FirstOrDefaultAsync(o => o.Ad == ad && o.Grup == grup);

            Opsiyon opsiyon;
            if (mevcutOpsiyon != null)
            {
                if (mevcutOpsiyon.EkFiyat != ekFiyat || mevcutOpsiyon.Zorunlu != zorunlu ||
                    mevcutOpsiyon.AdEN != adEN || mevcutOpsiyon.GrupEN != grupEN)
                {
                    mevcutOpsiyon.EkFiyat = ekFiyat;
                    mevcutOpsiyon.Zorunlu = zorunlu;
                    mevcutOpsiyon.AdEN = adEN;
                    mevcutOpsiyon.GrupEN = grupEN;
                    await _context.SaveChangesAsync();
                }
                opsiyon = mevcutOpsiyon;
            }
            else
            {
                opsiyon = new Opsiyon
                {
                    Ad = ad,
                    AdEN = adEN,
                    Grup = grup,
                    GrupEN = grupEN,
                    EkFiyat = ekFiyat,
                    Zorunlu = zorunlu
                };
                _context.Opsiyonlar.Add(opsiyon);
                await _context.SaveChangesAsync();
            }

            var baglanti = await _context.UrunOpsiyonlar
                .AnyAsync(uo => uo.UrunId == urunId && uo.OpsiyonId == opsiyon.Id);

            if (baglanti)
                return (false, "Bu opsiyon zaten bu ürüne ekli.", 0);

            _context.UrunOpsiyonlar.Add(new UrunOpsiyon
            {
                UrunId = urunId,
                OpsiyonId = opsiyon.Id
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Opsiyon eklendi. UrunId={UrunId}, OpsiyonId={OpsiyonId}", urunId, opsiyon.Id);
            return (true, null, opsiyon.Id);
        }

        public async Task<(bool Success, string? ErrorMessage)> OpsiyonSilAsync(int urunId, int opsiyonId)
        {
            if (urunId == 0)
                return (false, "Ürün ID gerekli.");

            var baglanti = await _context.UrunOpsiyonlar
                .FirstOrDefaultAsync(uo => uo.UrunId == urunId && uo.OpsiyonId == opsiyonId);

            if (baglanti == null)
                return (false, "Opsiyon bağlantısı bulunamadı.");

            _context.UrunOpsiyonlar.Remove(baglanti);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Opsiyon kaldırıldı. UrunId={UrunId}, OpsiyonId={OpsiyonId}", urunId, opsiyonId);
            return (true, null);
        }

        public async Task<List<(string Grup, string? GrupEN)>> GetOpsiyonGruplariAsync()
        {
            var gruplar = await _context.Opsiyonlar
                .Select(o => new { o.Grup, o.GrupEN })
                .Distinct()
                .OrderBy(o => o.Grup)
                .ToListAsync();

            return gruplar.Select(g => (g.Grup, g.GrupEN)).ToList();
        }

        public async Task<(byte[]? Data, string? ContentType)> GetUrunGorselFromDbAsync(int id)
        {
            var gorsel = await _context.UrunGorseller
                .Where(g => g.UrunId == id)
                .Select(g => new { g.Data, g.ContentType })
                .FirstOrDefaultAsync();

            return (gorsel?.Data, gorsel?.ContentType);
        }

        public async Task SaveUrunGorselPathAsync(int urunId, string gorselUrl)
        {
            var urun = await _context.Urunler.FindAsync(urunId);
            if (urun != null)
            {
                urun.GorselUrl = gorselUrl;
                await _context.SaveChangesAsync();
            }
        }
    }
}
