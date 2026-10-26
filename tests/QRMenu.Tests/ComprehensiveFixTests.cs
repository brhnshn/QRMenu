using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using QRMenu.Core.DTOs;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Core.Helpers;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using QRMenu.Data.Services;
using QRMenu.Web.Controllers;
using QRMenu.Web.Hubs;
using QRMenu.Web.ViewModels;
using Xunit;

namespace QRMenu.Tests
{
    public class ComprehensiveFixTests
    {
        private QRMenuDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<QRMenuDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var context = new QRMenuDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        private static bool GetSuccessValue(object? value)
        {
            if (value == null) return false;
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("success", out var prop) && prop.GetBoolean();
        }

        private static string GetMessageValue(object? value)
        {
            if (value == null) return string.Empty;
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("message", out var prop) ? prop.GetString() ?? string.Empty : string.Empty;
        }

        // ==========================================
        // P1 & CORE HELPERS TESTS
        // ==========================================

        [Fact]
        public void FiyatHesaplayici_BirimFiyatHesapla_CalculatesWithHappyHour()
        {
            // Base: 100, Variation: 20, Options: 10 => Total: 130
            // HH: 20% discount => 130 * 0.8 = 104
            var fiyat = FiyatHesaplayici.BirimFiyatHesapla(100m, 20m, 10m, 20m, true);
            Assert.Equal(104m, fiyat);

            // Without HH => 130
            var fullFiyat = FiyatHesaplayici.BirimFiyatHesapla(100m, 20m, 10m, 20m, false);
            Assert.Equal(130m, fullFiyat);
        }

        [Fact]
        public void QrTokenGenerator_GeneratesSecureUrlSafeToken()
        {
            var token1 = QrTokenGenerator.GenerateToken();
            var token2 = QrTokenGenerator.Generate();

            Assert.NotNull(token1);
            Assert.NotNull(token2);
            Assert.True(token1.Length >= 32);
            Assert.True(token2.Length >= 32);
            Assert.NotEqual(token1, token2);
            Assert.DoesNotContain("/", token1);
            Assert.DoesNotContain("+", token1);
            Assert.DoesNotContain("=", token1);
        }

        [Fact]
        public void DateTimeHelper_GetTurkeyDayUtcRange_CalculatesAccurately()
        {
            var range = DateTimeHelper.GetTurkeyDayUtcRange(new DateTime(2026, 10, 26));
            Assert.True(range.EndUtc > range.StartUtc);
            Assert.Equal(TimeSpan.FromDays(1), range.EndUtc - range.StartUtc);
        }

        [Fact]
        public async Task SiparisService_DurumGuncelle_BlocksPaymentStatuses()
        {
            var context = CreateInMemoryContext();
            var loggerMock = new Mock<ILogger<SiparisService>>();
            var service = new SiparisService(context, loggerMock.Object);

            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.TeslimEdildi,
                ToplamTutar = 100m,
                OlusturmaTarihi = DateTime.UtcNow
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DurumGuncelleAsync(siparis.Id, SiparisDurum.TamOdendi));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DurumGuncelleAsync(siparis.Id, SiparisDurum.KismiOdendi));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DurumGuncelleAsync(siparis.Id, SiparisDurum.Iade));
        }

        [Fact]
        public async Task SiparisService_ManuelSiparis_WithVariation_AppliesVariationPriceAndStock()
        {
            var context = CreateInMemoryContext();
            var loggerMock = new Mock<ILogger<SiparisService>>();
            var service = new SiparisService(context, loggerMock.Object);

            var masa = new Masa { MasaNo = 10, AktifMi = true };
            context.Masalar.Add(masa);

            var kategori = new Kategori { Ad = "Tatlılar", SiraNo = 1 };
            context.Kategoriler.Add(kategori);
            await context.SaveChangesAsync();

            var urun = new Urun { Ad = "Cheesecake", Fiyat = 100m, StokAdet = 10, AktifMi = true, KategoriId = kategori.Id };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            var varyasyon = new UrunVaryasyon { UrunId = urun.Id, Ad = "Büyük Boy", EkFiyat = 25m, StokAdet = 5, AktifMi = true };
            context.UrunVaryasyonlar.Add(varyasyon);
            await context.SaveChangesAsync();

            var urunler = new List<ManuelSiparisDetayDto>
            {
                new ManuelSiparisDetayDto
                {
                    UrunId = urun.Id,
                    UrunVaryasyonId = varyasyon.Id,
                    Adet = 2
                }
            };

            var siparis = await service.ManuelSiparisOlusturAsync(masa.Id, urunler);

            Assert.NotNull(siparis);
            Assert.Equal(250m, siparis.ToplamTutar); // (100 + 25) * 2 = 250
            var guncelVaryasyon = await context.UrunVaryasyonlar.FindAsync(varyasyon.Id);
            Assert.Equal(3, guncelVaryasyon!.StokAdet); // 5 - 2 = 3
        }

        // ==========================================
        // P3: ADMIN CONTROLLER & GUN SONU TESTS
        // ==========================================

        [Fact]
        public async Task AdminController_HappyHourKaydet_InvalidDiscountRate_ReturnsSuccessFalse()
        {
            var context = CreateInMemoryContext();
            var hubContextMock = new Mock<IHubContext<OrderHub>>();
            var hhService = new QRMenu.Data.Services.HappyHourYonetimService(context, new Mock<ILogger<QRMenu.Data.Services.HappyHourYonetimService>>().Object);
            var controller = new AdminHappyHourController(hhService, hubContextMock.Object, new Mock<ILogger<AdminHappyHourController>>().Object);

            // Rate > 99
            var result = await controller.HappyHourKaydet(new HappyHourFormViewModel
            {
                AktifMi = true,
                IndirimOrani = 120,
                BaslangicSaati = "14:00",
                BitisSaati = "17:00"
            }) as JsonResult;

            Assert.NotNull(result);
            Assert.False(GetSuccessValue(result.Value));

            // Equal start and end time
            var resultEqualTime = await controller.HappyHourKaydet(new HappyHourFormViewModel
            {
                AktifMi = true,
                IndirimOrani = 20,
                BaslangicSaati = "14:00",
                BitisSaati = "14:00"
            }) as JsonResult;

            Assert.NotNull(resultEqualTime);
            Assert.False(GetSuccessValue(resultEqualTime.Value));
        }

        [Fact]
        public async Task AdminController_GunSonuKapat_WithOpenOrders_RejectsClosure()
        {
            var context = CreateInMemoryContext();
            var siparisServiceMock = new Mock<ISiparisService>();
            var hubContextMock = new Mock<IHubContext<OrderHub>>();
            var userStoreMock = new Mock<IUserStore<Kullanici>>();
            var userManager = new UserManager<Kullanici>(userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            var rendererMock = new Mock<QRMenu.Web.Helpers.IRazorViewRenderer>();

            var raporService = new QRMenu.Data.Services.RaporYonetimService(context, userManager, new Mock<ILogger<QRMenu.Data.Services.RaporYonetimService>>().Object);
            var controller = new AdminRaporController(
                raporService, siparisServiceMock.Object, hubContextMock.Object,
                rendererMock.Object, userManager, new Mock<ILogger<AdminRaporController>>().Object);

            var masa = new Masa { MasaNo = 3, AktifMi = true };
            context.Masalar.Add(masa);

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.Hazirlaniyor, // Açık sipariş
                ToplamTutar = 80m,
                OlusturmaTarihi = DateTime.UtcNow
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            var result = await controller.GunSonuKapat(new AdminController.GunSonuKapatRequest
            {
                Tarih = DateTime.UtcNow.ToString("yyyy-MM-dd")
            }) as JsonResult;

            Assert.NotNull(result);
            Assert.False(GetSuccessValue(result.Value));
            Assert.Contains("açık sipariş", GetMessageValue(result.Value));
        }

        // ==========================================
        // P5: GAME LOGIC & SECURITY TESTS
        // ==========================================

        [Fact]
        public async Task SiparisController_OyunSonuclandir_RejectsIfDayClosed()
        {
            var context = CreateInMemoryContext();
            var businessDate = DateTimeHelper.GetTurkeyBusinessDayUtc(DateTime.UtcNow);
            context.GunSonuRaporlari.Add(new GunSonuRapor
            {
                Tarih = businessDate,
                ToplamCiro = 500m,
                SiparisSayisi = 5,
                KapanisTarihi = DateTime.UtcNow
            });

            var masa = new Masa { MasaNo = 5, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var oturum = new Oturum { MasaId = masa.Id, TokenHash = "hash123", AktifMi = true };
            context.Oturumlar.Add(oturum);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                OturumId = oturum.Id,
                Durum = SiparisDurum.TeslimEdildi,
                ToplamTutar = 100m,
                OlusturmaTarihi = DateTime.UtcNow
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            var siparisServiceMock = new Mock<ISiparisService>();
            var sepetServiceMock = new Mock<ISepetService>();
            var loggerMock = new Mock<ILogger<SiparisController>>();
            var hubMock = new Mock<IHubContext<OrderHub>>();
            var dataProtectionProvider = new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();

            var controller = new SiparisController(
                siparisServiceMock.Object,
                sepetServiceMock.Object,
                loggerMock.Object,
                hubMock.Object,
                context,
                dataProtectionProvider);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Cookie"] = "qrmenu_token=hash123";
            httpContext.Items["OturumId"] = oturum.Id;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = await controller.OyunSonuclandir(siparis.Id, new OyunSonucRequest
            {
                OyunTipi = "CARKIFELEK",
                OyunToken = "dummy",
                BasariliMi = true
            }) as JsonResult;

            Assert.NotNull(result);
            Assert.False(GetSuccessValue(result.Value));
            Assert.Contains("Gün sonu kapatılmış", GetMessageValue(result.Value));
        }

        [Fact]
        public async Task SiparisController_OyunSonuclandir_RejectsIfOrderHasPaidItems()
        {
            var context = CreateInMemoryContext();
            var masa = new Masa { MasaNo = 6, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var oturum = new Oturum { MasaId = masa.Id, TokenHash = "hash456", AktifMi = true };
            context.Oturumlar.Add(oturum);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                OturumId = oturum.Id,
                Durum = SiparisDurum.TeslimEdildi,
                ToplamTutar = 100m,
                OlusturmaTarihi = DateTime.UtcNow
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            var urun = new Urun { Ad = "Pizza", Fiyat = 50m, StokAdet = 10, AktifMi = true };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            // Item that is already TamOdendi
            var detay1 = new SiparisDetay
            {
                SiparisId = siparis.Id,
                UrunId = urun.Id,
                Adet = 1,
                BirimFiyat = 50m,
                Durum = SiparisDurum.TamOdendi
            };
            var detay2 = new SiparisDetay
            {
                SiparisId = siparis.Id,
                UrunId = urun.Id,
                Adet = 1,
                BirimFiyat = 50m,
                Durum = SiparisDurum.TeslimEdildi
            };
            context.SiparisDetaylar.AddRange(detay1, detay2);
            await context.SaveChangesAsync();

            var siparisServiceMock = new Mock<ISiparisService>();
            var sepetServiceMock = new Mock<ISepetService>();
            var loggerMock = new Mock<ILogger<SiparisController>>();
            var hubMock = new Mock<IHubContext<OrderHub>>();
            var dataProtectionProvider = new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();

            var controller = new SiparisController(
                siparisServiceMock.Object,
                sepetServiceMock.Object,
                loggerMock.Object,
                hubMock.Object,
                context,
                dataProtectionProvider);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Cookie"] = "qrmenu_token=hash456";
            httpContext.Items["OturumId"] = oturum.Id;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = await controller.OyunSonuclandir(siparis.Id, new OyunSonucRequest
            {
                OyunTipi = "CARKIFELEK",
                OyunToken = "dummy",
                BasariliMi = true
            }) as JsonResult;

            Assert.NotNull(result);
            Assert.False(GetSuccessValue(result.Value));
            Assert.Contains("ödenmiş siparişlerde oyun oynanamaz", GetMessageValue(result.Value));
        }

        // ==========================================
        // P4 & SECURITY HARDENING TESTS
        // ==========================================

        [Fact]
        public async Task QRController_Validate_InProduction_ReturnsNotFound_AndDoesNotLeakToken()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var masa = await context.Masalar.FindAsync(5);
            Assert.NotNull(masa);
            masa.QrToken = "secret-token-xyz";
            await context.SaveChangesAsync();

            var tokenServiceMock = new Mock<ITokenService>();
            var loggerMock = new Mock<ILogger<QRController>>();
            var envMock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            envMock.Setup(e => e.EnvironmentName).Returns("Production");

            var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var configSectionMock = new Mock<Microsoft.Extensions.Configuration.IConfigurationSection>();
            configSectionMock.Setup(s => s.Value).Returns("false");
            configMock.Setup(c => c.GetSection("Security:LegacyQrEnabled")).Returns(configSectionMock.Object);

            var controller = new QRController(
                tokenServiceMock.Object,
                loggerMock.Object,
                envMock.Object,
                context,
                configMock.Object);

            // Act
            var result = await controller.Validate(5);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task QRController_ValidateToken_ValidToken_ReturnsSuccess_WithoutTokenValue()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var masa = new Masa { Id = 912, MasaNo = 912, AktifMi = true, QrToken = "valid-token-123" };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var tokenServiceMock = new Mock<ITokenService>();
            var loggerMock = new Mock<ILogger<QRController>>();
            var envMock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();

            var controller = new QRController(
                tokenServiceMock.Object,
                loggerMock.Object,
                envMock.Object,
                context,
                configMock.Object);

            // Act
            var result = await controller.ValidateToken("valid-token-123") as JsonResult;

            // Assert
            Assert.NotNull(result);
            Assert.True(GetSuccessValue(result.Value));
            var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("masaNo", out var masaNoProp) && masaNoProp.GetInt32() == 912);
            // Verify token itself is NOT returned
            Assert.False(doc.RootElement.TryGetProperty("token", out _));
        }

        [Fact]
        public async Task QRController_ValidateToken_InvalidOrInactiveToken_ReturnsSuccessFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var masa = new Masa { Id = 915, MasaNo = 915, AktifMi = false, QrToken = "inactive-token" };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var tokenServiceMock = new Mock<ITokenService>();
            var loggerMock = new Mock<ILogger<QRController>>();
            var envMock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();

            var controller = new QRController(
                tokenServiceMock.Object,
                loggerMock.Object,
                envMock.Object,
                context,
                configMock.Object);

            // Act - non-existent
            var result1 = await controller.ValidateToken("non-existent-token") as JsonResult;
            // Act - inactive
            var result2 = await controller.ValidateToken("inactive-token") as JsonResult;

            // Assert
            Assert.NotNull(result1);
            Assert.False(GetSuccessValue(result1.Value));
            Assert.NotNull(result2);
            Assert.False(GetSuccessValue(result2.Value));
        }

        [Fact]
        public async Task SiparisService_ExecuteSiparisOlusturAsync_CalculatesLiveOptionPricesFromDatabase()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var loggerMock = new Mock<ILogger<SiparisService>>();
            var service = new SiparisService(context, loggerMock.Object);

            var masa = new Masa { Id = 901, MasaNo = 901, AktifMi = true };
            var oturum = new Oturum { Id = 901, MasaId = 901, AktifMi = true, TokenHash = "tok1" };
            var urun = new Urun { Id = 901, Ad = "Özel Kahve", Fiyat = 50m, StokAdet = 100, AktifMi = true };
            var opsiyon = new Opsiyon { Id = 910, Ad = "Süt", EkFiyat = 15m };
            context.Masalar.Add(masa);
            context.Oturumlar.Add(oturum);
            context.Urunler.Add(urun);
            context.Opsiyonlar.Add(opsiyon);

            // SepetDetay has stale EkFiyat = 5m in JSON, but live DB has 15m
            var sepet = new Sepet
            {
                Id = 901,
                OturumId = 901,
                ToplamTutar = 55m
            };
            var sepetDetay = new SepetDetay
            {
                SepetId = 901,
                UrunId = 901,
                Adet = 1,
                BirimFiyat = 55m,
                SeciliOpsiyonlar = "[{\"Id\":910,\"Ad\":\"Süt\",\"EkFiyat\":5.0}]"
            };
            context.Sepetler.Add(sepet);
            context.SepetDetaylar.Add(sepetDetay);
            await context.SaveChangesAsync();

            // Act
            var siparis = await service.SiparisOlusturAsync(901);

            // Assert
            // Live price: 50 base + 15 live option = 65m
            Assert.NotNull(siparis);
            Assert.Equal(65m, siparis.ToplamTutar);
            Assert.Single(siparis.SiparisDetaylar);
            Assert.Equal(65m, siparis.SiparisDetaylar.First().BirimFiyat);
        }

        [Fact]
        public async Task SiparisService_DurumGuncelleAsync_AllowsUpdatingOrder_WhenTodayIsNotClosed()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var loggerMock = new Mock<ILogger<SiparisService>>();
            var service = new SiparisService(context, loggerMock.Object);

            var masa = new Masa { Id = 902, MasaNo = 902, AktifMi = true };
            context.Masalar.Add(masa);

            // Order created 2 days ago
            var siparis = new Siparis
            {
                Id = 910,
                MasaId = 902,
                Durum = SiparisDurum.Onaylandi,
                OlusturmaTarihi = DateTime.UtcNow.AddDays(-2),
                SiparisTarihi = DateTime.UtcNow.AddDays(-2),
                SiparisGunu = DateTime.UtcNow.AddDays(-2).Date,
                GunlukSiparisNo = 1,
                ToplamTutar = 100m,
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            context.Siparisler.Add(siparis);

            // Add a closed report for 2 days ago
            var raporTarihi2DaysAgo = DateTimeHelper.GetTurkeyBusinessDayUtc(DateTime.UtcNow.AddDays(-2));
            context.GunSonuRaporlari.Add(new GunSonuRapor
            {
                Tarih = raporTarihi2DaysAgo,
                KapanisTarihi = DateTime.UtcNow.AddDays(-2),
                ToplamCiro = 100m,
                SiparisSayisi = 1
            });
            await context.SaveChangesAsync();

            // Act - Updating order today (when today is NOT closed) should succeed!
            var updated = await service.DurumGuncelleAsync(siparis.Id, SiparisDurum.Hazirlaniyor);

            // Assert
            Assert.Equal(SiparisDurum.Hazirlaniyor, updated.Durum);
        }
    }
}
