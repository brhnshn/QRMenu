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
    public class SiparisServiceTests
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

        private SiparisService CreateService(QRMenuDbContext context)
        {
            var loggerMock = new Mock<ILogger<SiparisService>>();
            return new SiparisService(context, loggerMock.Object);
        }

        /// <summary>
        /// Sepet + Oturum + Masa + SepetDetay seed data oluşturur
        /// </summary>
        private async Task<(int sepetId, int masaId, int oturumId)> SeedSepetDataAsync(QRMenuDbContext context, int urunSayisi = 2)
        {
            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var oturum = new Oturum { MasaId = masa.Id, TokenHash = "testhash", AktifMi = true };
            context.Oturumlar.Add(oturum);
            await context.SaveChangesAsync();

            var kategori = new Kategori { Ad = "Kahveler", SiraNo = 1 };
            context.Kategoriler.Add(kategori);
            await context.SaveChangesAsync();

            var sepet = new Sepet { OturumId = oturum.Id, ToplamTutar = 0 };
            context.Sepetler.Add(sepet);
            await context.SaveChangesAsync();

            decimal toplam = 0;
            for (int i = 1; i <= urunSayisi; i++)
            {
                var urun = new Urun { Ad = $"Ürün {i}", Fiyat = 10m * i, KategoriId = kategori.Id, AktifMi = true };
                context.Urunler.Add(urun);
                await context.SaveChangesAsync();

                var detay = new SepetDetay
                {
                    SepetId = sepet.Id,
                    UrunId = urun.Id,
                    Adet = 1,
                    BirimFiyat = urun.Fiyat
                };
                context.SepetDetaylar.Add(detay);
                toplam += urun.Fiyat;
            }

            sepet.ToplamTutar = toplam;
            await context.SaveChangesAsync();

            return (sepet.Id, masa.Id, oturum.Id);
        }

        // ============================
        // SİPARİŞ OLUŞTURMA TESTLERİ
        // ============================

        [Fact]
        public async Task SiparisOlustur_BasariliSiparis_SepetTemizlenir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (sepetId, masaId, _) = await SeedSepetDataAsync(context, 2);

            var siparis = await service.SiparisOlusturAsync(sepetId);

            Assert.NotNull(siparis);
            Assert.Equal(SiparisDurum.Onaylandi, siparis.Durum);
            Assert.Equal(masaId, siparis.MasaId);
            Assert.Equal(30m, siparis.ToplamTutar); // 10 + 20

            // Sepet detayları temizlenmiş olmalı
            var sepet = await context.Sepetler.Include(s => s.SepetDetaylar).FirstAsync(s => s.Id == sepetId);
            Assert.Empty(sepet.SepetDetaylar);
            Assert.Equal(0m, sepet.ToplamTutar);

            // Sipariş detayları oluşmuş olmalı
            var detaylar = await context.SiparisDetaylar.Where(sd => sd.SiparisId == siparis.Id).ToListAsync();
            Assert.Equal(2, detaylar.Count);
        }

        [Fact]
        public async Task SiparisOlustur_BosSepet_HataFirlatir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            // Boş sepet oluştur (detaysız)
            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();
            var oturum = new Oturum { MasaId = masa.Id, TokenHash = "testhash", AktifMi = true };
            context.Oturumlar.Add(oturum);
            await context.SaveChangesAsync();
            var sepet = new Sepet { OturumId = oturum.Id, ToplamTutar = 0 };
            context.Sepetler.Add(sepet);
            await context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SiparisOlusturAsync(sepet.Id));
            Assert.Contains("boş", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SiparisOlustur_GecersizSepet_HataFirlatir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SiparisOlusturAsync(9999));
            Assert.Contains("bulunamadı", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SiparisOlustur_NotlarKaydolur()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (sepetId, _, _) = await SeedSepetDataAsync(context, 1);

            var siparis = await service.SiparisOlusturAsync(sepetId, "Şekersiz olsun");

            Assert.Equal("Şekersiz olsun", siparis.Notlar);
        }

        // ============================
        // STATE MACHINE TESTLERİ
        // ============================

        [Theory]
        [InlineData(SiparisDurum.Onaylandi, SiparisDurum.Hazirlaniyor)]
        [InlineData(SiparisDurum.Hazirlaniyor, SiparisDurum.Hazir)]
        [InlineData(SiparisDurum.Hazir, SiparisDurum.TeslimEdildi)]
        [InlineData(SiparisDurum.TeslimEdildi, SiparisDurum.TamOdendi)]
        [InlineData(SiparisDurum.TeslimEdildi, SiparisDurum.KismiOdendi)]
        [InlineData(SiparisDurum.KismiOdendi, SiparisDurum.TamOdendi)]
        [InlineData(SiparisDurum.Onaylandi, SiparisDurum.Iptal)]
        [InlineData(SiparisDurum.Hazirlaniyor, SiparisDurum.Iptal)]
        [InlineData(SiparisDurum.TeslimEdildi, SiparisDurum.Iade)]
        [InlineData(SiparisDurum.TamOdendi, SiparisDurum.Iade)]
        public async Task DurumGuncelle_GecerliGecis_Basarili(SiparisDurum mevcutDurum, SiparisDurum yeniDurum)
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = mevcutDurum,
                ToplamTutar = 50m,
                RowVersion = new byte[] { 1 }
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            var result = await service.DurumGuncelleAsync(siparis.Id, yeniDurum);

            Assert.Equal(yeniDurum, result.Durum);
            Assert.NotNull(result.GuncellemeTarihi);
        }

        [Theory]
        [InlineData(SiparisDurum.Hazir, SiparisDurum.Onaylandi)]
        [InlineData(SiparisDurum.TeslimEdildi, SiparisDurum.Hazirlaniyor)]
        [InlineData(SiparisDurum.TamOdendi, SiparisDurum.Hazir)]
        [InlineData(SiparisDurum.Iptal, SiparisDurum.Onaylandi)]
        [InlineData(SiparisDurum.Iade, SiparisDurum.TamOdendi)]
        [InlineData(SiparisDurum.Sepette, SiparisDurum.Hazir)]
        public async Task DurumGuncelle_GecersizGecis_HataFirlatir(SiparisDurum mevcutDurum, SiparisDurum yeniDurum)
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = mevcutDurum,
                ToplamTutar = 50m,
                RowVersion = new byte[] { 1 }
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DurumGuncelleAsync(siparis.Id, yeniDurum));
            Assert.Contains("Geçersiz durum geçişi", ex.Message);
        }

        [Fact]
        public async Task DurumGuncelle_SiparisYok_HataFirlatir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DurumGuncelleAsync(9999, SiparisDurum.Hazirlaniyor));
            Assert.Contains("bulunamadı", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ============================
        // İPTAL TESTİ
        // ============================

        [Fact]
        public async Task IptalEt_OnaylandiDurumundan_Basarili()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.Onaylandi,
                ToplamTutar = 100m,
                RowVersion = new byte[] { 1 }
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            var result = await service.IptalEtAsync(siparis.Id);

            Assert.Equal(SiparisDurum.Iptal, result.Durum);
        }

        [Fact]
        public async Task IptalEt_TeslimEdildiDurumundan_HataFirlatir()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            var siparis = new Siparis
            {
                MasaId = masa.Id,
                Durum = SiparisDurum.TeslimEdildi,
                ToplamTutar = 100m,
                RowVersion = new byte[] { 1 }
            };
            context.Siparisler.Add(siparis);
            await context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.IptalEtAsync(siparis.Id));
        }

        // ============================
        // SORGU TESTLERİ
        // ============================

        [Fact]
        public async Task GetSiparis_DetaylarDahil_Doner()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (sepetId, _, _) = await SeedSepetDataAsync(context, 3);

            var siparis = await service.SiparisOlusturAsync(sepetId);
            var loaded = await service.GetSiparisAsync(siparis.Id);

            Assert.NotNull(loaded);
            Assert.Equal(3, loaded!.SiparisDetaylar.Count);
            Assert.All(loaded.SiparisDetaylar, sd => Assert.NotNull(sd.Urun));
        }

        [Fact]
        public async Task GetSiparislerByMasa_IptalHaric_Doner()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var masa = new Masa { MasaNo = 1, AktifMi = true };
            context.Masalar.Add(masa);
            await context.SaveChangesAsync();

            // 1 aktif, 1 iptal sipariş
            context.Siparisler.Add(new Siparis { MasaId = masa.Id, Durum = SiparisDurum.Onaylandi, ToplamTutar = 50, RowVersion = new byte[] { 1 } });
            context.Siparisler.Add(new Siparis { MasaId = masa.Id, Durum = SiparisDurum.Iptal, ToplamTutar = 30, RowVersion = new byte[] { 1 } });
            await context.SaveChangesAsync();

            var liste = await service.GetSiparislerByMasaAsync(masa.Id);

            Assert.Single(liste); // İptal olanı hariç
        }

        [Fact]
        public async Task GetSiparislerByOturum_TumSiparisleriDoner()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);
            var (sepetId, _, oturumId) = await SeedSepetDataAsync(context, 1);

            // İlk sipariş
            await service.SiparisOlusturAsync(sepetId);

            // Sepete tekrar ürün ekleyip ikinci sipariş
            var sepet = await context.Sepetler.FindAsync(sepetId);
            var urun = await context.Urunler.FirstAsync();
            context.SepetDetaylar.Add(new SepetDetay { SepetId = sepetId, UrunId = urun.Id, Adet = 1, BirimFiyat = urun.Fiyat });
            sepet!.ToplamTutar = urun.Fiyat;
            await context.SaveChangesAsync();
            await service.SiparisOlusturAsync(sepetId);

            var liste = await service.GetSiparislerByOturumAsync(oturumId);

            Assert.Equal(2, liste.Count);
        }

        // ============================
        // GEÇERLİ GEÇİŞ KURALLAR TESTİ
        // ============================

        [Fact]
        public void GecerliGecisler_OnaylandiDurumu_DogruListeDoner()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var gecisler = service.GecerliGecisler(SiparisDurum.Onaylandi);

            Assert.Equal(2, gecisler.Count);
            Assert.Contains(SiparisDurum.Hazirlaniyor, gecisler);
            Assert.Contains(SiparisDurum.Iptal, gecisler);
        }

        [Fact]
        public void GecerliGecisler_IptalDurumu_BosListeDoner()
        {
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var gecisler = service.GecerliGecisler(SiparisDurum.Iptal);

            Assert.Empty(gecisler);
        }
    }
}
