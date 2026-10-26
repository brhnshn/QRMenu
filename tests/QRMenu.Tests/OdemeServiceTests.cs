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

        [Fact]
        public async Task ParcaliOdeme_HazirlaniyorDurumundakiKalem_FalseDoner()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (masaId, _, detayIds) = await SeedSiparisDataAsync(context);

            // Kalemi Hazırlanıyor yap (P1.4: sadece TeslimEdildi veya KismiOdendi ödenebilir)
            var d1 = await context.SiparisDetaylar.FindAsync(detayIds[0]);
            d1!.Durum = SiparisDurum.Hazirlaniyor;
            await context.SaveChangesAsync();

            var result = await service.ParcaliOdemeAsync(masaId, new List<int> { detayIds[0] }, "Nakit");
            Assert.False(result);
        }

        [Fact]
        public async Task ParcaliOdeme_AyniKalemIkinciKezOdenemez_CiftTahsilatEngellenir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (masaId, _, detayIds) = await SeedSiparisDataAsync(context);

            // İlk ödeme başarılı
            var ilkOdeme = await service.ParcaliOdemeAsync(masaId, new List<int> { detayIds[0] }, "Nakit");
            Assert.True(ilkOdeme);

            // Aynı kalemi ikinci kez ödemeye çalışma (P1.3)
            var ikinciOdeme = await service.ParcaliOdemeAsync(masaId, new List<int> { detayIds[0] }, "Nakit");
            Assert.False(ikinciOdeme);

            // Yalnızca 1 ödeme kaydı olmalı
            var odemeSayisi = await context.Odemeler.CountAsync();
            Assert.Equal(1, odemeSayisi);
        }

        [Fact]
        public async Task ParcaliOdeme_GecersizOdemeTipi_FalseDoner()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (masaId, _, detayIds) = await SeedSiparisDataAsync(context);

            var result = await service.ParcaliOdemeAsync(masaId, new List<int> { detayIds[0] }, "GecersizTip");
            Assert.False(result);
        }

        [Fact]
        public async Task ParcaliOdeme_ProRataYuvarlamaVeToplamTamEslesir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var masa = new Masa { MasaNo = 5, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            // 100 TL toplam tutarlı, 3 kalemli sipariş (33.33 + 33.33 + 33.34)
            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.TeslimEdildi,
                ToplamTutar = 100.00m,
                RowVersion = new byte[] { 1 }
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            var urun = new Urun { Ad = "Kalem Ürün", Fiyat = 33.33m, AktifMi = true };
            context.Urunler.Add(urun);
            await context.SaveChangesAsync();

            var d1 = new SiparisDetay { SiparisId = siparis.Id, UrunId = urun.Id, Adet = 1, BirimFiyat = 33.33m, Durum = SiparisDurum.TeslimEdildi };
            var d2 = new SiparisDetay { SiparisId = siparis.Id, UrunId = urun.Id, Adet = 1, BirimFiyat = 33.33m, Durum = SiparisDurum.TeslimEdildi };
            var d3 = new SiparisDetay { SiparisId = siparis.Id, UrunId = urun.Id, Adet = 1, BirimFiyat = 33.34m, Durum = SiparisDurum.TeslimEdildi };
            context.SiparisDetaylar.AddRange(d1, d2, d3);
            await context.SaveChangesAsync();

            // Sırayla öde
            await service.ParcaliOdemeAsync(masa.Id, new List<int> { d1.Id }, "Nakit");
            await service.ParcaliOdemeAsync(masa.Id, new List<int> { d2.Id }, "Nakit");
            await service.ParcaliOdemeAsync(masa.Id, new List<int> { d3.Id }, "Nakit");

            var toplamOdenen = await context.Odemeler.Where(o => o.SiparisId == siparis.Id).SumAsync(o => o.Tutar);
            Assert.Equal(100.00m, toplamOdenen);

            var sonSiparis = await context.Siparisler.FindAsync(siparis.Id);
            Assert.Equal(SiparisDurum.TamOdendi, sonSiparis!.Durum);
        }

        [Fact]
        public async Task ParcaliOdeme_GunKapaliIse_HataFirlatir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (masaId, _, detayIds) = await SeedSiparisDataAsync(context);

            var bugun = QRMenu.Core.Helpers.DateTimeHelper.GetTurkeyBusinessDayUtc(DateTime.UtcNow);
            context.GunSonuRaporlari.Add(new GunSonuRapor
            {
                Tarih = bugun,
                ToplamCiro = 500,
                KapanisTarihi = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ParcaliOdemeAsync(masaId, new List<int> { detayIds[0] }, "Nakit"));
            Assert.Contains("Gün sonu raporu kapatılmış", ex.Message);
        }
    }
}
