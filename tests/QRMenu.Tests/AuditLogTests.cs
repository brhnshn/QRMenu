using Microsoft.EntityFrameworkCore;
using Moq;
using QRMenu.Core.Entities;
using QRMenu.Core.Interfaces;
using QRMenu.Data.Data;
using QRMenu.Data.Interceptors;
using System.Text.Json;

namespace QRMenu.Tests
{
    public class AuditLogTests
    {
        private Mock<ICurrentUserProvider> _userProviderMock;

        public AuditLogTests()
        {
            _userProviderMock = new Mock<ICurrentUserProvider>();
            _userProviderMock.Setup(x => x.GetUserId()).Returns(1);
        }

        private QRMenuDbContext CreateContext()
        {
            var interceptor = new AuditLogInterceptor(_userProviderMock.Object);
            
            var options = new DbContextOptionsBuilder<QRMenuDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;

            var context = new QRMenuDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public async Task SaveChanges_YeniUrun_AuditLogOlusturur()
        {
            using var context = CreateContext();

            var urun = new Urun { Ad = "Yeni Ürün", Fiyat = 10m, AktifMi = true, KategoriId = 1 };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            var log = await context.AuditLoglar.FirstOrDefaultAsync();
            Assert.NotNull(log);
            Assert.Equal("Urun", log!.TabloAdi);
            Assert.Equal("ADDED", log.Islem);
            Assert.Equal(1, log.KullaniciId);
            
            // Yeni değerleri kontrol et
            var yeniDeger = JsonSerializer.Deserialize<Dictionary<string, object>>(log.YeniDeger!);
            Assert.Equal("Yeni Ürün", yeniDeger!["Ad"].ToString());
        }

        [Fact]
        public async Task SaveChanges_UrunGuncelleme_EskiVeYeniDegerleriKaydeder()
        {
            using var context = CreateContext();

            // Önce ekle
            var urun = new Urun { Ad = "Eski Ad", Fiyat = 10m, AktifMi = true, KategoriId = 1 };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();
            
            // Logları temizle (ekleme logunu geçmek için)
            context.AuditLoglar.RemoveRange(context.AuditLoglar);
            await context.SaveChangesAsync();

            // Güncelle
            urun.Ad = "Yeni Ad";
            urun.Fiyat = 20m;
            await context.SaveChangesAsync();

            var log = await context.AuditLoglar.OrderByDescending(l => l.Id).FirstOrDefaultAsync();
            Assert.NotNull(log);
            Assert.Equal("MODIFIED", log!.Islem);
            
            var eski = JsonSerializer.Deserialize<Dictionary<string, object>>(log.EskiDeger!);
            var yeni = JsonSerializer.Deserialize<Dictionary<string, object>>(log.YeniDeger!);
            
            Assert.Equal("Eski Ad", eski!["Ad"].ToString());
            Assert.Equal("Yeni Ad", yeni!["Ad"].ToString());
            Assert.Equal(10, Convert.ToDecimal(eski["Fiyat"].ToString()));
            Assert.Equal(20, Convert.ToDecimal(yeni["Fiyat"].ToString()));
        }

        [Fact]
        public async Task SaveChanges_KritikOlmayanTablo_LogOlusturmaz()
        {
            using var context = CreateContext();

            // Kategori tablosu varsayılan olarak loglanmıyor (Interceptor'da tanımlanmamış olabilir)
            // Interceptor koduna baktığımızda: Siparis, SiparisDetay, Urun, Masa, Kullanici loglanıyor.
            var kategori = new Kategori { Ad = "Loglanmayan Kategori" };
            context.Kategoriler.Add(kategori);
            await context.SaveChangesAsync();

            var logCount = await context.AuditLoglar.CountAsync(l => l.TabloAdi == "Kategori");
            Assert.Equal(0, logCount);
        }
    }
}
