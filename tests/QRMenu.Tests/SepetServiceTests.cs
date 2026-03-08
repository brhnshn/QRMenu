using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using QRMenu.Core.Entities;
using QRMenu.Data.Data;
using QRMenu.Data.Services;

namespace QRMenu.Tests
{
    public class SepetServiceTests
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

        private async Task SeedDataAsync(QRMenuDbContext context)
        {
            // Masa -> Oturum
            if (!context.Masalar.Any(m => m.Id == 100))
            {
                context.Masalar.Add(new Masa { Id = 100, MasaNo = 100, AktifMi = true });
                context.Oturumlar.Add(new Oturum { Id = 100, MasaId = 100, TokenHash = "abc" });

                // Kategori -> Urun -> Opsiyon
                context.Kategoriler.Add(new Kategori { Id = 100, Ad = "Test Kategori", SiraNo = 1 });
                context.Urunler.Add(new Urun { Id = 100, KategoriId = 100, Ad = "Çay", Fiyat = 20m, AktifMi = true });
                context.Urunler.Add(new Urun { Id = 101, KategoriId = 100, Ad = "Kahve", Fiyat = 50m, AktifMi = true });

                context.Opsiyonlar.Add(new Opsiyon { Id = 100, Ad = "Büyük Boy", Grup = "Boyut", EkFiyat = 20m });
                context.UrunOpsiyonlar.Add(new UrunOpsiyon { UrunId = 101, OpsiyonId = 100 });

                await context.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task GetOrCreateSepetAsync_Should_Create_New_If_Not_Exists()
        {
            // Arrange
            var context = CreateInMemoryContext();
            await SeedDataAsync(context);
            var loggerMock = new Mock<ILogger<SepetService>>();
            var service = new SepetService(context, loggerMock.Object);

            // Act
            var sepet = await service.GetOrCreateSepetAsync(100);

            // Assert
            Assert.NotNull(sepet);
            Assert.Equal(100, sepet.OturumId);
            Assert.Equal(0, sepet.ToplamTutar);
            Assert.True(sepet.Id > 0);
        }

        [Fact]
        public async Task AddItemAsync_Should_Add_New_Item_And_Update_Total()
        {
            // Arrange
            var context = CreateInMemoryContext();
            await SeedDataAsync(context);
            var loggerMock = new Mock<ILogger<SepetService>>();
            var service = new SepetService(context, loggerMock.Object);

            var sepet = await service.GetOrCreateSepetAsync(100);

            // Act
            var detay = await service.AddItemAsync(sepet.Id, 100, 2); // 2 adet Çay (20₺) = 40₺

            // Assert
            var guncelSepet = await service.GetSepetWithDetailsAsync(sepet.Id);
            Assert.NotNull(guncelSepet);
            Assert.Single(guncelSepet.SepetDetaylar);
            Assert.Equal(40m, guncelSepet.ToplamTutar);
            Assert.Equal(2, detay.Adet);
        }

        [Fact]
        public async Task AddItemAsync_Should_Increase_Quantity_If_Same_Product()
        {
            // Arrange
            var context = CreateInMemoryContext();
            await SeedDataAsync(context);
            var loggerMock = new Mock<ILogger<SepetService>>();
            var service = new SepetService(context, loggerMock.Object);

            var sepet = await service.GetOrCreateSepetAsync(100);

            // Act
            await service.AddItemAsync(sepet.Id, 100, 2); // 2 Çay
            await service.AddItemAsync(sepet.Id, 100, 3); // 3 Çay daha -> 5 Çay, 100₺

            // Assert
            var guncelSepet = await service.GetSepetWithDetailsAsync(sepet.Id);
            Assert.Single(guncelSepet!.SepetDetaylar);
            Assert.Equal(5, guncelSepet.SepetDetaylar.First().Adet);
            Assert.Equal(100m, guncelSepet.ToplamTutar);
        }

        [Fact]
        public async Task AddItemAsync_Should_Add_Opsiyon_And_Calculate_Total_Correctly()
        {
            // Arrange
            var context = CreateInMemoryContext();
            await SeedDataAsync(context);
            var loggerMock = new Mock<ILogger<SepetService>>();
            var service = new SepetService(context, loggerMock.Object);

            var sepet = await service.GetOrCreateSepetAsync(100);

            // Act
            // Kahve (50₺) + Büyük Boy (20₺) = 70₺, 2 adet = 140₺
            await service.AddItemAsync(sepet.Id, 101, 2, new List<int> { 100 });

            // Assert
            var guncelSepet = await service.GetSepetWithDetailsAsync(sepet.Id);
            var detay = guncelSepet!.SepetDetaylar.First();

            Assert.Equal(140m, guncelSepet.ToplamTutar);
            Assert.Equal(70m, detay.BirimFiyat);
            Assert.Contains("\"Id\":100", detay.SeciliOpsiyonlar);
        }

        [Fact]
        public async Task RemoveItemAsync_Should_Remove_And_Update_Total()
        {
            // Arrange
            var context = CreateInMemoryContext();
            await SeedDataAsync(context);
            var loggerMock = new Mock<ILogger<SepetService>>();
            var service = new SepetService(context, loggerMock.Object);

            var sepet = await service.GetOrCreateSepetAsync(100);
            var detay = await service.AddItemAsync(sepet.Id, 100, 2); // 40₺

            // Act
            var isSuccess = await service.RemoveItemAsync(detay.Id);

            // Assert
            Assert.True(isSuccess);
            var guncelSepet = await service.GetSepetWithDetailsAsync(sepet.Id);
            Assert.Empty(guncelSepet!.SepetDetaylar);
            Assert.Equal(0m, guncelSepet.ToplamTutar);
        }

        [Fact]
        public async Task UpdateQuantityAsync_Should_Update_Adet_And_Total()
        {
            // Arrange
            var context = CreateInMemoryContext();
            await SeedDataAsync(context);
            var loggerMock = new Mock<ILogger<SepetService>>();
            var service = new SepetService(context, loggerMock.Object);

            var sepet = await service.GetOrCreateSepetAsync(100);
            var detay = await service.AddItemAsync(sepet.Id, 100, 2); // 40₺

            // Act
            var isSuccess = await service.UpdateQuantityAsync(detay.Id, 5); // 5 * 20 = 100₺

            // Assert
            Assert.True(isSuccess);
            var guncelSepet = await service.GetSepetWithDetailsAsync(sepet.Id);
            Assert.Equal(5, guncelSepet!.SepetDetaylar.First().Adet);
            Assert.Equal(100m, guncelSepet.ToplamTutar);
        }

        [Fact]
        public async Task ClearSepetAsync_Should_Remove_All_Items_And_Set_Total_To_Zero()
        {
            // Arrange
            var context = CreateInMemoryContext();
            await SeedDataAsync(context);
            var loggerMock = new Mock<ILogger<SepetService>>();
            var service = new SepetService(context, loggerMock.Object);

            var sepet = await service.GetOrCreateSepetAsync(100);
            await service.AddItemAsync(sepet.Id, 100, 2); // 40₺
            await service.AddItemAsync(sepet.Id, 101, 1); // 50₺
            Assert.True((await service.GetSepetWithDetailsAsync(sepet.Id))?.ToplamTutar == 90m);

            // Act
            await service.ClearSepetAsync(sepet.Id);

            // Assert
            var guncelSepet = await service.GetSepetWithDetailsAsync(sepet.Id);
            Assert.Empty(guncelSepet!.SepetDetaylar);
            Assert.Equal(0m, guncelSepet.ToplamTutar);
        }
    }
}
