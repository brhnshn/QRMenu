using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Data.Data;
using QRMenu.Data.Services;

namespace QRMenu.Tests
{
    public class UrunServiceTests
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

        private async Task SeedKategori(QRMenuDbContext context)
        {
            if (!context.Kategoriler.Any(k => k.Id == 200))
            {
                context.Kategoriler.Add(new Kategori { Id = 200, Ad = "Test Kategori", SiraNo = 1, AktifMi = true });
                await context.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task CreateAsync_Should_Add_Product()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new UrunService(context);
            await SeedKategori(context);

            var urun = new Urun
            {
                KategoriId = 200,
                Ad = "Test Kahve",
                Fiyat = 50.00m,
                AktifMi = true
            };

            // Act
            var result = await service.CreateAsync(urun);

            // Assert
            Assert.True(result.Id > 0);
            Assert.Equal("Test Kahve", result.Ad);
            Assert.Equal(50.00m, result.Fiyat);
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Product()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new UrunService(context);
            await SeedKategori(context);

            var urun = new Urun { KategoriId = 200, Ad = "Test Latte", Fiyat = 65.00m, AktifMi = true };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            // Act
            var result = await service.GetByIdAsync(urun.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Latte", result!.Ad);
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Null_For_Invalid_Id()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new UrunService(context);

            // Act
            var result = await service.GetByIdAsync(99999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateAsync_Should_Modify_Product()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new UrunService(context);
            await SeedKategori(context);

            var urun = new Urun { KategoriId = 200, Ad = "Eski Ad", Fiyat = 30.00m, AktifMi = true };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            // Act
            urun.Ad = "Yeni Ad";
            urun.Fiyat = 45.00m;
            var result = await service.UpdateAsync(urun);

            // Assert
            var updated = await context.Urunler.FindAsync(urun.Id);
            Assert.Equal("Yeni Ad", updated!.Ad);
            Assert.Equal(45.00m, updated.Fiyat);
        }

        [Fact]
        public async Task DeleteAsync_Should_Remove_Product()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new UrunService(context);
            await SeedKategori(context);

            var urun = new Urun { KategoriId = 200, Ad = "Silinecek Ürün", Fiyat = 25.00m, AktifMi = true };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            // Act
            var result = await service.DeleteAsync(urun.Id);

            // Assert
            Assert.True(result);
            var deleted = await context.Urunler.FindAsync(urun.Id);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task DeleteAsync_Should_Return_False_For_Invalid_Id()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new UrunService(context);

            // Act
            var result = await service.DeleteAsync(99999);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetAktifAsync_Should_Return_Only_Active_Products()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = new UrunService(context);
            await SeedKategori(context);

            context.Urunler.AddRange(
                new Urun { KategoriId = 200, Ad = "Aktif Ürün", Fiyat = 50m, AktifMi = true },
                new Urun { KategoriId = 200, Ad = "Pasif Ürün", Fiyat = 60m, AktifMi = false }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await service.GetAktifAsync();

            // Assert
            Assert.All(result, u => Assert.True(u.AktifMi));
            Assert.DoesNotContain(result, u => u.Ad == "Pasif Ürün");
        }
    }
}
