using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;
using QRMenu.Core.Enums;

namespace QRMenu.Data.Data
{
    public static class SeedData
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            // ===== MASALAR (5 adet) =====
            modelBuilder.Entity<Masa>().HasData(
                new Masa { Id = 1, MasaNo = 1, AktifMi = true },
                new Masa { Id = 2, MasaNo = 2, AktifMi = true },
                new Masa { Id = 3, MasaNo = 3, AktifMi = true },
                new Masa { Id = 4, MasaNo = 4, AktifMi = true },
                new Masa { Id = 5, MasaNo = 5, AktifMi = true }
            );

            // ===== KATEGORİLER (3 adet) =====
            modelBuilder.Entity<Kategori>().HasData(
                new Kategori { Id = 1, Ad = "Sıcak İçecekler", AdEN = "Hot Beverages", SiraNo = 1, AktifMi = true },
                new Kategori { Id = 2, Ad = "Soğuk İçecekler", AdEN = "Cold Beverages", SiraNo = 2, AktifMi = true },
                new Kategori { Id = 3, Ad = "Atıştırmalıklar", AdEN = "Snacks", SiraNo = 3, AktifMi = true }
            );

            // ===== ÜRÜNLER (10 adet) =====
            modelBuilder.Entity<Urun>().HasData(
                new Urun { Id = 1, KategoriId = 1, Ad = "Filtre Kahve", AdEN = "Filter Coffee", Aciklama = "Taze demlenmiş filtre kahve", AciklamaEN = "Freshly brewed filter coffee", Fiyat = 45.00m, AktifMi = true, PopulerMi = true, SatisSayisi = 150 },
                new Urun { Id = 2, KategoriId = 1, Ad = "Latte", AdEN = "Latte", Aciklama = "Espresso ve sütlü kahve", AciklamaEN = "Espresso with steamed milk", Fiyat = 65.00m, AktifMi = true, PopulerMi = true, SatisSayisi = 200 },
                new Urun { Id = 3, KategoriId = 1, Ad = "Americano", AdEN = "Americano", Aciklama = "Espresso ve sıcak su", AciklamaEN = "Espresso with hot water", Fiyat = 50.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 80 },
                new Urun { Id = 4, KategoriId = 1, Ad = "Türk Kahvesi", AdEN = "Turkish Coffee", Aciklama = "Geleneksel Türk kahvesi", AciklamaEN = "Traditional Turkish coffee", Fiyat = 40.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 120 },
                new Urun { Id = 5, KategoriId = 2, Ad = "Buzlu Latte", AdEN = "Iced Latte", Aciklama = "Soğuk süt ve espresso", AciklamaEN = "Cold milk and espresso", Fiyat = 70.00m, AktifMi = true, PopulerMi = true, SatisSayisi = 180 },
                new Urun { Id = 6, KategoriId = 2, Ad = "Limonata", AdEN = "Lemonade", Aciklama = "Taze sıkılmış limonata", AciklamaEN = "Freshly squeezed lemonade", Fiyat = 55.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 90 },
                new Urun { Id = 7, KategoriId = 2, Ad = "Smoothie", AdEN = "Smoothie", Aciklama = "Karışık meyveli smoothie", AciklamaEN = "Mixed fruit smoothie", Fiyat = 75.00m, AktifMi = false, PopulerMi = false, SatisSayisi = 60 },
                new Urun { Id = 8, KategoriId = 3, Ad = "Cheesecake", AdEN = "Cheesecake", Aciklama = "New York usulü cheesecake", AciklamaEN = "New York style cheesecake", Fiyat = 90.00m, AktifMi = true, PopulerMi = true, SatisSayisi = 160 },
                new Urun { Id = 9, KategoriId = 3, Ad = "Brownie", AdEN = "Brownie", Aciklama = "Çikolatalı brownie", AciklamaEN = "Chocolate brownie", Fiyat = 70.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 95 },
                new Urun { Id = 10, KategoriId = 3, Ad = "Sandviç", AdEN = "Sandwich", Aciklama = "Tavuklu kulüp sandviç", AciklamaEN = "Chicken club sandwich", Fiyat = 110.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 70 }
            );
        }
    }
}
