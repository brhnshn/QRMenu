using Microsoft.EntityFrameworkCore;
using QRMenu.Core.Entities;

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
                // Sıcak İçecekler
                new Urun { Id = 1, KategoriId = 1, Ad = "Filtre Kahve", AdEN = "Filter Coffee", Aciklama = "Taze demlenmiş filtre kahve", AciklamaEN = "Freshly brewed filter coffee", Fiyat = 45.00m, AktifMi = true, PopulerMi = true, SatisSayisi = 150 },
                new Urun { Id = 2, KategoriId = 1, Ad = "Latte", AdEN = "Latte", Aciklama = "Espresso ve sütlü kahve", AciklamaEN = "Espresso with steamed milk", Fiyat = 65.00m, AktifMi = true, PopulerMi = true, SatisSayisi = 200 },
                new Urun { Id = 3, KategoriId = 1, Ad = "Americano", AdEN = "Americano", Aciklama = "Espresso ve sıcak su", AciklamaEN = "Espresso with hot water", Fiyat = 50.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 80 },
                new Urun { Id = 4, KategoriId = 1, Ad = "Türk Kahvesi", AdEN = "Turkish Coffee", Aciklama = "Geleneksel Türk kahvesi", AciklamaEN = "Traditional Turkish coffee", Fiyat = 40.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 120 },
                // Soğuk İçecekler
                new Urun { Id = 5, KategoriId = 2, Ad = "Buzlu Latte", AdEN = "Iced Latte", Aciklama = "Soğuk süt ve espresso", AciklamaEN = "Cold milk and espresso", Fiyat = 70.00m, AktifMi = true, PopulerMi = true, SatisSayisi = 180 },
                new Urun { Id = 6, KategoriId = 2, Ad = "Limonata", AdEN = "Lemonade", Aciklama = "Taze sıkılmış limonata", AciklamaEN = "Freshly squeezed lemonade", Fiyat = 55.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 90 },
                new Urun { Id = 7, KategoriId = 2, Ad = "Smoothie", AdEN = "Smoothie", Aciklama = "Karışık meyveli smoothie", AciklamaEN = "Mixed fruit smoothie", Fiyat = 75.00m, AktifMi = false, PopulerMi = false, SatisSayisi = 60 }, // Tükendi
                                                                                                                                                                                                                                           // Atıştırmalıklar
                new Urun { Id = 8, KategoriId = 3, Ad = "Cheesecake", AdEN = "Cheesecake", Aciklama = "New York usulü cheesecake", AciklamaEN = "New York style cheesecake", Fiyat = 90.00m, AktifMi = true, PopulerMi = true, SatisSayisi = 160 },
                new Urun { Id = 9, KategoriId = 3, Ad = "Brownie", AdEN = "Brownie", Aciklama = "Çikolatalı brownie", AciklamaEN = "Chocolate brownie", Fiyat = 70.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 95 },
                new Urun { Id = 10, KategoriId = 3, Ad = "Sandviç", AdEN = "Sandwich", Aciklama = "Tavuklu kulüp sandviç", AciklamaEN = "Chicken club sandwich", Fiyat = 110.00m, AktifMi = true, PopulerMi = false, SatisSayisi = 70 }
            );

            // ===== OPSİYONLAR (5 adet) =====
            modelBuilder.Entity<Opsiyon>().HasData(
                // Boyut opsiyonları
                new Opsiyon { Id = 1, Ad = "Küçük", AdEN = "Small", Grup = "Boyut", EkFiyat = 0m },
                new Opsiyon { Id = 2, Ad = "Orta", AdEN = "Medium", Grup = "Boyut", EkFiyat = 10.00m },
                new Opsiyon { Id = 3, Ad = "Büyük", AdEN = "Large", Grup = "Boyut", EkFiyat = 20.00m },
                // Süt tipi opsiyonları
                new Opsiyon { Id = 4, Ad = "Tam Yağlı Süt", AdEN = "Whole Milk", Grup = "Süt Tipi", EkFiyat = 0m },
                new Opsiyon { Id = 5, Ad = "Yağsız Süt", AdEN = "Skim Milk", Grup = "Süt Tipi", EkFiyat = 0m }
            );

            // ===== ÜRÜN-OPSİYON BAĞLANTILARI (M2M) =====
            modelBuilder.Entity<UrunOpsiyon>().HasData(
                // Filtre Kahve → Boyut
                new UrunOpsiyon { UrunId = 1, OpsiyonId = 1 },
                new UrunOpsiyon { UrunId = 1, OpsiyonId = 2 },
                new UrunOpsiyon { UrunId = 1, OpsiyonId = 3 },
                // Latte → Boyut + Süt Tipi
                new UrunOpsiyon { UrunId = 2, OpsiyonId = 1 },
                new UrunOpsiyon { UrunId = 2, OpsiyonId = 2 },
                new UrunOpsiyon { UrunId = 2, OpsiyonId = 3 },
                new UrunOpsiyon { UrunId = 2, OpsiyonId = 4 },
                new UrunOpsiyon { UrunId = 2, OpsiyonId = 5 },
                // Americano → Boyut
                new UrunOpsiyon { UrunId = 3, OpsiyonId = 1 },
                new UrunOpsiyon { UrunId = 3, OpsiyonId = 2 },
                new UrunOpsiyon { UrunId = 3, OpsiyonId = 3 },
                // Buzlu Latte → Boyut + Süt Tipi
                new UrunOpsiyon { UrunId = 5, OpsiyonId = 1 },
                new UrunOpsiyon { UrunId = 5, OpsiyonId = 2 },
                new UrunOpsiyon { UrunId = 5, OpsiyonId = 3 },
                new UrunOpsiyon { UrunId = 5, OpsiyonId = 4 },
                new UrunOpsiyon { UrunId = 5, OpsiyonId = 5 }
            );
        }
    }
}
