using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;
using QRMenu.Data.Data;
using QRMenu.Data.Services;

namespace QRMenu.Tests
{
    public class OdemeServiceTests
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

        private OdemeService CreateService(QRMenuDbContext context)
        {
            var loggerMock = new Mock<ILogger<OdemeService>>();
            return new OdemeService(context, loggerMock.Object);
        }

        private async Task<(int masaId, int siparisId, List<int> detayIds)> SeedSiparisDataAsync(QRMenuDbContext context)
        {
            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.TeslimEdildi,
                ToplamTutar = 50m,
                RowVersion = new byte[] { 1 }
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            var urun = new Urun { Ad = "Test Ürün", Fiyat = 25m, AktifMi = true };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            var detay1 = new SiparisDetay
            {
                SiparisId = siparis.Id,
                UrunId = urun.Id,
                Adet = 1,
                BirimFiyat = 25m,
                Durum = SiparisDurum.TeslimEdildi
            };
            var detay2 = new SiparisDetay
            {
                SiparisId = siparis.Id,
                UrunId = urun.Id,
                Adet = 1,
                BirimFiyat = 25m,
                Durum = SiparisDurum.TeslimEdildi
            };

            context.SiparisDetaylar.AddRange(detay1, detay2);
            await context.SaveChangesAsync();

            return (masa.Id, siparis.Id, new List<int> { detay1.Id, detay2.Id });
        }

        [Fact]
        public async Task ParcaliOdeme_TekUrun_Basarili()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (masaId, siparisId, detayIds) = await SeedSiparisDataAsync(context);

            var result = await service.ParcaliOdemeAsync(masaId, new List<int> { detayIds[0] }, "Nakit");

            Assert.True(result);

            // Ürün durumu güncellenmiş mi?
            var detay = await context.SiparisDetaylar.FindAsync(detayIds[0]);
            Assert.Equal(SiparisDurum.TamOdendi, detay!.Durum);

            // Diğer ürün hala TeslimEdildi mi?
            var detay2 = await context.SiparisDetaylar.FindAsync(detayIds[1]);
            Assert.Equal(SiparisDurum.TeslimEdildi, detay2!.Durum);

            // Ana sipariş durumu KismiOdendi veya degismemeli? 
            // Bizim mantikta ana siparis ancak tamami bitince guncelleniyor.
            // Ama Kasa ekranı bakiye üzerinden çalıştığı için KismiOdendi olması opsiyonel.
            // Mevcut kodda ana siparis ancak tamamı bitince TamOdendi oluyor.
        }

        [Fact]
        public async Task ParcaliOdeme_TumUrunler_SiparisKapanir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (masaId, siparisId, detayIds) = await SeedSiparisDataAsync(context);

            await service.ParcaliOdemeAsync(masaId, detayIds, "Kart");

            var siparis = await context.Siparisler.FindAsync(siparisId);
            Assert.Equal(SiparisDurum.TamOdendi, siparis!.Durum);
        }

        [Fact]
        public async Task ParcaliOdeme_OdemeKaydiAtilir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (masaId, siparisId, detayIds) = await SeedSiparisDataAsync(context);

            await service.ParcaliOdemeAsync(masaId, new List<int> { detayIds[0] }, "Kart");

            var odeme = await context.Odemeler.FirstOrDefaultAsync(o => o.SiparisId == siparisId);
            Assert.NotNull(odeme);
            Assert.Equal(25m, odeme!.Tutar);
            Assert.Equal(OdemeTipi.Kart, odeme.OdemeTipi);
        }

        [Fact]
        public async Task ParcaliOdeme_GecersizMasa_HataFirlatir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (_, _, detayIds) = await SeedSiparisDataAsync(context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ParcaliOdemeAsync(999, detayIds, "Nakit"));
        }

        [Fact]
        public async Task ParcaliOdeme_IptalUrunler_OdemeyeDahilEdilmez()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (masaId, siparisId, detayIds) = await SeedSiparisDataAsync(context);

            // Birini iptal et
            var d1 = await context.SiparisDetaylar.FindAsync(detayIds[0]);
            d1!.Durum = SiparisDurum.Iptal;
            await context.SaveChangesAsync();

            // Sadece diğerini öde
            await service.ParcaliOdemeAsync(masaId, new List<int> { detayIds[1] }, "Nakit");

            // Sipariş kapanmalı çünkü kalan tek ürün ödendi (diğeri iptal)
            var siparis = await context.Siparisler.FindAsync(siparisId);
            Assert.Equal(SiparisDurum.TamOdendi, siparis!.Durum);
        }
    }
}
