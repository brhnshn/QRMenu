using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Data.Data;
using QRMenu.Data.Services;
using Xunit;

namespace QRMenu.Tests
{
    public class AdminServicesTests
    {
        private QRMenuDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<QRMenuDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            var context = new QRMenuDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        private UserManager<Kullanici> CreateUserManager(QRMenuDbContext context)
        {
            var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<Kullanici>(context);
            var passwordHasher = new PasswordHasher<Kullanici>();
            var userValidators = new List<IUserValidator<Kullanici>>();
            var passwordValidators = new List<IPasswordValidator<Kullanici>>();
            var keyNormalizer = new UpperInvariantLookupNormalizer();
            var errors = new IdentityErrorDescriber();
            var services = new Mock<IServiceProvider>().Object;
            var logger = new Mock<ILogger<UserManager<Kullanici>>>().Object;

            return new UserManager<Kullanici>(
                userStore, null!, passwordHasher, userValidators, passwordValidators,
                keyNormalizer, errors, services, logger);
        }

        // ============================================================
        // 1. RAPOR YÖNETİM SERVİSİ TESTLERİ (Z Raporu, Gün Sonu)
        // ============================================================

        [Fact]
        public async Task RaporYonetimService_GunSonuKapat_WithOpenOrders_Rejects()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var userManager = CreateUserManager(context);
            var logger = new Mock<ILogger<RaporYonetimService>>().Object;
            var service = new RaporYonetimService(context, userManager, logger);

            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            context.Siparisler.Add(new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.Hazirlaniyor,
                ToplamTutar = 150m,
                OlusturmaTarihi = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Act
            var (success, message) = await service.GunSonuKapatAsync(DateTime.UtcNow.ToString("yyyy-MM-dd"), "admin-1");

            // Assert
            Assert.False(success);
            Assert.Contains("açık sipariş", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RaporYonetimService_GunSonuKapat_WithClosedOrders_Succeeds()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var userManager = CreateUserManager(context);
            var logger = new Mock<ILogger<RaporYonetimService>>().Object;
            var service = new RaporYonetimService(context, userManager, logger);

            var masa = new Masa { MasaNo = 2, AktifMi = true };
            context.Masalar.Add(masa);

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.TamOdendi,
                ToplamTutar = 250m,
                OlusturmaTarihi = DateTime.UtcNow
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            context.Odemeler.Add(new Odeme
            {
                SiparisId = siparis.Id,
                Tutar = 250m,
                OdemeTipi = OdemeTipi.Kart,
                OdemeTarihi = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Act
            var todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var (success, message) = await service.GunSonuKapatAsync(todayStr, "admin-1");

            // Assert
            Assert.True(success);
            Assert.Null(message);

            var rapor = await context.GunSonuRaporlari.FirstOrDefaultAsync();
            Assert.NotNull(rapor);
            Assert.Equal(250m, rapor.ToplamCiro);
            Assert.Equal(1, rapor.SiparisSayisi);

            // Closing again should fail
            var (secondSuccess, secondMessage) = await service.GunSonuKapatAsync(todayStr, "admin-1");
            Assert.False(secondSuccess);
            Assert.Contains("zaten kapatılmış", secondMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RaporYonetimService_GunSonuAc_ReopensClosedDay()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var userManager = CreateUserManager(context);
            var logger = new Mock<ILogger<RaporYonetimService>>().Object;
            var service = new RaporYonetimService(context, userManager, logger);

            var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            context.GunSonuRaporlari.Add(new GunSonuRapor
            {
                Tarih = today,
                ToplamCiro = 500m,
                SiparisSayisi = 3,
                KapanisTarihi = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Act
            var (success, message) = await service.GunSonuAcAsync(today.ToString("yyyy-MM-dd"));

            // Assert
            Assert.True(success);
            var rapor = await context.GunSonuRaporlari.FirstOrDefaultAsync(r => r.Tarih == today);
            Assert.Null(rapor);
        }

        [Fact]
        public async Task RaporYonetimService_BuildGunSonuExportModel_And_CreateExcel()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var userManager = CreateUserManager(context);
            var logger = new Mock<ILogger<RaporYonetimService>>().Object;
            var service = new RaporYonetimService(context, userManager, logger);

            var masa = new Masa { MasaNo = 10, AktifMi = true };
            context.Masalar.Add(masa);

            var urun = new Urun { Ad = "Espresso", Fiyat = 60m, AktifMi = true };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.TamOdendi,
                ToplamTutar = 120m,
                OlusturmaTarihi = DateTime.UtcNow,
                SiparisDetaylar = new List<SiparisDetay>
                {
                    new SiparisDetay { UrunId = urun.Id, Adet = 2, BirimFiyat = 60m, Durum = SiparisDurum.Onaylandi }
                }
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            context.Odemeler.Add(new Odeme
            {
                SiparisId = siparis.Id,
                Tutar = 120m,
                OdemeTipi = OdemeTipi.Nakit,
                OdemeTarihi = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Act: Build export model
            var exportModel = await service.BuildGunSonuExportModelAsync(DateTime.UtcNow.ToString("yyyy-MM-dd"));

            // Assert export model
            Assert.NotNull(exportModel);
            Assert.Equal(120m, exportModel.ToplamCiro);
            Assert.Equal(1, exportModel.SiparisSayisi);
            Assert.Contains(exportModel.OdemeTipleri, o => o.Tip == "Nakit" && o.Tutar == 120m);
            Assert.Single(exportModel.Siparisler);

            // Act: Create Excel
            var excelBytes = service.CreateGunSonuExcel(exportModel);

            // Assert excel output
            Assert.NotNull(excelBytes);
            Assert.True(excelBytes.Length > 100); // Valid XLSX stream is non-empty
        }

        [Fact]
        public async Task RaporYonetimService_GetMasaNoByMasaId_And_GetSiparislerJsonList()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var userManager = CreateUserManager(context);
            var logger = new Mock<ILogger<RaporYonetimService>>().Object;
            var service = new RaporYonetimService(context, userManager, logger);

            var masa = new Masa { MasaNo = 15, AktifMi = true };
            context.Masalar.Add(masa);

            var urun = new Urun { Ad = "Cheesecake", Fiyat = 90m, AktifMi = true };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.Hazir,
                ToplamTutar = 90m,
                OlusturmaTarihi = DateTime.UtcNow,
                SiparisDetaylar = new List<SiparisDetay>
                {
                    new SiparisDetay { UrunId = urun.Id, Adet = 1, BirimFiyat = 90m, Durum = SiparisDurum.Onaylandi }
                }
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            // Act & Assert MasaNo
            var masaNo = await service.GetMasaNoByMasaIdAsync(masa.Id);
            Assert.Equal(15, masaNo);

            // Act & Assert SiparislerJsonList
            var list = await service.GetSiparislerJsonListAsync(null, null);
            Assert.Single(list);
            Assert.Equal(siparis.Id, list[0].Id);
            Assert.Equal(15, list[0].MasaNo);
            Assert.Equal("Hazir", list[0].Durum);
            Assert.Contains("Cheesecake", list[0].DetayOzet);
        }

        // ============================================================
        // 2. HAPPY HOUR YÖNETİM SERVİSİ TESTLERİ
        // ============================================================

        [Fact]
        public async Task HappyHourYonetimService_GetHappyHourData_ReturnsDefault_WhenNoEntity()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var logger = new Mock<ILogger<HappyHourYonetimService>>().Object;
            var service = new HappyHourYonetimService(context, logger);

            // Act
            var (happyHour, kategoriler, urunler, seciliUrunIds) = await service.GetHappyHourDataAsync();

            // Assert
            Assert.NotNull(happyHour);
            Assert.False(happyHour.AktifMi);
            Assert.Equal(10, happyHour.IndirimOrani);
            Assert.Empty(seciliUrunIds);
        }

        [Fact]
        public async Task HappyHourYonetimService_Kaydet_Validation_RejectsInvalidRatesOrTimes()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var logger = new Mock<ILogger<HappyHourYonetimService>>().Object;
            var service = new HappyHourYonetimService(context, logger);

            // Rate < 1
            var res1 = await service.HappyHourKaydetAsync(new HappyHourKayitDto
            {
                AktifMi = true,
                IndirimOrani = 0,
                BaslangicSaati = "14:00",
                BitisSaati = "17:00"
            });
            Assert.False(res1.Success);
            Assert.Contains("1-99", res1.Message);

            // Rate > 99
            var res2 = await service.HappyHourKaydetAsync(new HappyHourKayitDto
            {
                AktifMi = true,
                IndirimOrani = 100,
                BaslangicSaati = "14:00",
                BitisSaati = "17:00"
            });
            Assert.False(res2.Success);

            // Equal start and end
            var res3 = await service.HappyHourKaydetAsync(new HappyHourKayitDto
            {
                AktifMi = true,
                IndirimOrani = 15,
                BaslangicSaati = "14:00",
                BitisSaati = "14:00"
            });
            Assert.False(res3.Success);
            Assert.Contains("aynı olamaz", res3.Message);
        }

        [Fact]
        public async Task HappyHourYonetimService_Kaydet_And_GetBilgi_Succeeds()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var logger = new Mock<ILogger<HappyHourYonetimService>>().Object;
            var service = new HappyHourYonetimService(context, logger);

            var kat = new Kategori { Ad = "İçecekler", SiraNo = 1, AktifMi = true };
            context.Kategoriler.Add(kat);
            var u1 = new Urun { Ad = "Limonata", Fiyat = 50m, AktifMi = true, Kategori = kat };
            var u2 = new Urun { Ad = "Çay", Fiyat = 20m, AktifMi = true, Kategori = kat };
            context.Urunler.AddRange(u1, u2);
            await context.SaveChangesAsync();

            // Act
            var saveResult = await service.HappyHourKaydetAsync(new HappyHourKayitDto
            {
                AktifMi = true,
                IndirimOrani = 20,
                BaslangicSaati = "00:00",
                BitisSaati = "23:59",
                UrunIds = new List<int> { u1.Id }
            });

            // Assert save
            Assert.True(saveResult.Success);
            Assert.Single(saveResult.EtkilenenUrunIds);
            Assert.Equal(u1.Id, saveResult.EtkilenenUrunIds[0]);

            // Act: Get info
            var bilgi = await service.GetHappyHourBilgiAsync();
            Assert.True(bilgi.AktifMi);
            Assert.True(bilgi.SuAnAktif);
            Assert.Equal(20, bilgi.IndirimOrani);
            Assert.Contains(u1.Id, bilgi.UrunIds);
            Assert.DoesNotContain(u2.Id, bilgi.UrunIds);
        }

        // ============================================================
        // 3. MASA YÖNETİM SERVİSİ TESTLERİ
        // ============================================================

        [Fact]
        public async Task MasaYonetimService_MasaEkle_And_QrOlustur_Succeeds()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var logger = new Mock<ILogger<MasaYonetimService>>().Object;
            var service = new MasaYonetimService(context, logger);

            var bolge = new Bolge { Ad = "Bahçe", SiraNo = 1 };
            context.Bolgeler.Add(bolge);
            await context.SaveChangesAsync();

            // Act: Masa Ekle
            var (success, msg) = await service.MasaEkleAsync(42, bolge.Id);
            Assert.True(success);
            Assert.Null(msg);

            // Adding same table number should fail
            var (dupSuccess, dupMsg) = await service.MasaEkleAsync(42, bolge.Id);
            Assert.False(dupSuccess);
            Assert.Contains("zaten var", dupMsg);

            // Act: QrOlustur
            var (qrSuccess, qrMsg, qrBase64, qrUrl, qrToken) = await service.QrOlusturAsync(42, "https://qrmenu.local");
            Assert.True(qrSuccess);
            Assert.NotNull(qrUrl);
            Assert.Contains("/qr/t/", qrUrl);
            Assert.NotNull(qrBase64);
            Assert.NotEmpty(qrBase64!);
            Assert.NotNull(qrToken);

            // Table details list
            var detaylar = await service.GetMasaDetayListAsync("https://qrmenu.local");
            var masa42 = Assert.Single(detaylar, d => d.MasaNo == 42);
            Assert.Equal("Bahçe", masa42.BolgeAd);
        }

        [Fact]
        public async Task MasaYonetimService_BolgeEkle_And_Sil()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var logger = new Mock<ILogger<MasaYonetimService>>().Object;
            var service = new MasaYonetimService(context, logger);

            // Act: Bölge Ekle
            var bolge = await service.BolgeEkleAsync("Teras", 2);
            Assert.NotNull(bolge);
            Assert.True(bolge.Id > 0);

            var bolgeler = await service.GetBolgelerAsync();
            Assert.Single(bolgeler);
            Assert.Equal("Teras", bolgeler[0].Ad);

            // Act: Bölge Sil
            var (delSuccess, delMsg) = await service.BolgeSilAsync(bolge.Id);
            Assert.True(delSuccess);

            var emptyBolgeler = await service.GetBolgelerAsync();
            Assert.Empty(emptyBolgeler);
        }

        // ============================================================
        // 4. KULLANICI YÖNETİM SERVİSİ TESTLERİ
        // ============================================================

        [Fact]
        public async Task KullaniciYonetimService_KullaniciEkle_And_Listele()
        {
            // Arrange
            var context = CreateInMemoryContext();
            context.Roles.Add(new IdentityRole { Id = "Garson", Name = "Garson", NormalizedName = "GARSON" });
            await context.SaveChangesAsync();

            var userManager = CreateUserManager(context);
            var logger = new Mock<ILogger<KullaniciYonetimService>>().Object;
            var service = new KullaniciYonetimService(userManager, logger);

            // Act: Kullanici Ekle
            var (success, error, id) = await service.KullaniciEkleAsync("garson_ali", "Ali Yılmaz", "1234", KullaniciRol.Garson);

            // Assert
            Assert.True(success, error ?? "Kullanici eklenemedi");
            Assert.NotNull(id);
            Assert.Null(error);

            // Act: Listele
            var list = await service.GetKullaniciListesiAsync();
            Assert.Single(list);
            Assert.Equal("garson_ali", list[0].KullaniciAdi);
            Assert.Equal("Garson", list[0].Rol);
        }
    }

    // Helper for mocking IQueryable on DbSet if needed
    internal static class MockDbSetExtensions
    {
        public static Mock<DbSet<T>> BuildMockDbSet<T>(this IQueryable<T> source) where T : class
        {
            var mock = new Mock<DbSet<T>>();
            mock.As<IQueryable<T>>().Setup(m => m.Provider).Returns(source.Provider);
            mock.As<IQueryable<T>>().Setup(m => m.Expression).Returns(source.Expression);
            mock.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(source.ElementType);
            mock.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(source.GetEnumerator());
            return mock;
        }
    }
}
