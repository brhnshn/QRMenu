# QR Menü Otomasyonu

Restoran ve kafeler için QR kod tabanlı dijital menü ve sipariş yönetim sistemi. Bu proje, modern web teknolojileri ve gerçek zamanlı iletişim altyapısı kullanılarak geliştirilmiştir.

## 🚀 Proje Durumu: Hafta 5 Tamamlandı
Proje şu an 10 haftalık yol haritasının tam ortasında olup, **Personel Servisleri** ve **Kasa Yönetimi** modülleri başarıyla devreye alınmıştır.

## 🛠️ Teknik Özellikler
- **Backend:** .NET 8, EF Core, PostgreSQL (Supabase)
- **Real-time:** ASP.NET Core SignalR (Anlık Mutfak & Garson Bildirimleri)
- **Güvenlik:** Cookie Authentication & Role-Based Authorization (Admin, Garson, Kasa, Mutfak)
- **Takip:** AuditLog Interceptor (Her veritabanı değişikliğinin JSON olarak izlenmesi)
- **Test:** 67+ Unit Test (XUnit & Moq)

---

## 📂 Proje Dokümantasyonu

Haftalık ilerleme raporları ve teknik detaylara aşağıdaki tablodan ulaşabilirsiniz:

| Hafta | İçerik | Format |
|-------|---------|:------:|
| **Hafta 1** | Proje Kurulumu & Veritabanı Altyapısı | [PDF](docs/Pdfler/Hafta%201%20—%20Proje%20Kurulumu%20%26%20Veritabanı%20Altyapısı.pdf) |
| **Hafta 2** | Güvenli Oturum Sistemi + Ekstra Geliştirmeler | [PDF](docs/Pdfler/Hafta%202%20—%20Güvenli%20Oturum%20Sistemi%20%2B%20Ekstra%20Geliştirmeler.pdf) |
| **Hafta 3** | Sepet Sistemi (Veritabanı) & Modern Mobil UX | [PDF](docs/Pdfler/Hafta%203%20—%20Sepet%20Sistemi%20(Veritabanı)%20%26%20Modern%20Mobil%20UX.pdf) |
| **Hafta 4** | Sipariş Motoru, Admin Siparişler & SignalR | [PDF](docs/Pdfler/Hafta%204%20—%20Sipariş%20Motoru%2C%20Admin%20Siparişler%2C%20SignalR%2C%20UX%20İyileştirmeleri%2C%20CI_CD%2C%20Garson%20Çağır%2C%20Fiş%20Overlay%2C%20Saat%20Dilimi%20Fix.pdf) |
| **Hafta 5** | **Personel Servisleri (Garson, Mutfak, Kasa), Giriş Sistemi, ACID Tahsilat, AuditLog & Ünite Testleri** | [PDF]([docs/Pdfler/Hafta%205%20Kapsamlı%20Rapor%20—%20Personel%20Servisleri%2C%20ACID%20Tahsilat%2C%20SignalR%20%26%20AuditLog.pdf](https://github.com/brhnshn/QRMenu/blob/main/docs/Pdfler/Hafta%205%20%E2%80%94%20Personel%20Servisleri%20(Garson%2C%20Mutfak%2C%20Kasa)%2C%20Giri%C5%9F%20Sistemi%2C%20ACID%20Tahsilat%2C%20AuditLog%20%26%20%C3%9Cnite%20Testleri.pdf)) |
| **Dosya Yapısı** | QR Menü Otomasyonu Genel Dosya Mimarisi | [PDF](docs/Pdfler/Proje%20Dosya%20Yapısı%20—%20QR%20Menü%20Otomasyonu.pdf) |

---

## 👨‍💻 Geliştirme Notları
Hafta 5 kapsamında; Garson, Mutfak ve Kasa ekranları arasındaki tüm iş akışları finalize edilmiştir. Ünite testleri (67 adet) ile sistemin kararlılığı doğrulanmış ve yerelleştirme (Turkish I) hataları giderilmiştir.
