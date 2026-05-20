# QR Menü Otomasyonu

[![Kullanım Rehberi](https://img.shields.io/badge/Döküman-Kullanım%20Rehberi-success?style=for-the-badge&logo=adobeacrobatreader)](docs/Pdfler/QR%20Men%C3%BC%20Sistemi%20-%20Kullan%C4%B1m%20Rehberipdf.pdf)
[![Vize Raporu](https://img.shields.io/badge/Döküman-Vize%20Proje%20Raporu-blue?style=for-the-badge&logo=adobeacrobatreader)](docs/Pdfler/SAT_VizeRaporu_Burhan_Şahin_247017027.pdf)

Restoran ve kafeler için QR kod tabanlı dijital menü ve sipariş yönetim sistemi. Bu proje, modern web teknolojileri ve gerçek zamanlı iletişim altyapısı kullanılarak geliştirilmiştir.

## 🚀 Proje Durumu: Hafta 10 Tamamlandı
Proje, 10. hafta itibarıyla **CI/CD Otomasyon Altyapısı (GitHub Actions) ve Admin Paneli Yönetici Profil Entegrasyonu** aşamalarını başarıyla tamamlamıştır. Sistem tam kararlılıkla canlı ortama hazır hale getirilmiştir.

## 🛠️ Teknik Özellikler
- **Backend:** .NET 8, EF Core, PostgreSQL (Supabase)
- **Real-time:** ASP.NET Core SignalR (Anlık Mutfak & Garson Bildirimleri)
- **Güvenlik:** Role-Based Auth, CSRF (Anti-Forgery) Protection, Rate Limiting, Account Lockout
- **İzleme:** SecurityLog Middleware (401, 403, 429 Hatalarının Takibi) & Serilog (Günlük Dosya Loglama)
- **Takip:** AuditLog Interceptor (Her veritabanı değişikliğinin JSON olarak izlenmesi)
- **Test:** 67+ Unit Test (XUnit & Moq)

---

## 📂 Proje Dokümantasyonu

Haftalık ilerleme raporları ve teknik detaylara aşağıdaki tablodan ulaşabilirsiniz:

| Açıklama / Hafta | İçerik | Format |
|-------|---------|:------:|
| 📑 **KULLANIM REHBERİ** | **SİSTEM KULLANIM REHBERİ (ADIM ADIM)** | [**PDF GÖRÜNTÜLE**](docs/Pdfler/QR%20Men%C3%BC%20Sistemi%20-%20Kullan%C4%B1m%20Rehberipdf.pdf) |
| 📑 **VİZE RAPORU** | **SAT PROJE VİZE RAPORU (GENEL ÖZET)** | [**PDF GÖRÜNTÜLE**](docs/Pdfler/SAT_VizeRaporu_Burhan_Şahin_247017027.pdf) |
| **Hafta 1** | Proje Kurulumu & Veritabanı Altyapısı | [PDF](docs/Pdfler/Hafta%201%20—%20Proje%20Kurulumu%20%26%20Veritabanı%20Altyapısı.pdf) |
| **Hafta 2** | Güvenli Oturum Sistemi + Ekstra Geliştirmeler | [PDF](docs/Pdfler/Hafta%202%20—%20Güvenli%20Oturum%20Sistemi%20%2B%20Ekstra%20Geliştirmeler.pdf) |
| **Hafta 3** | Sepet Sistemi (Veritabanı) & Modern Mobil UX | [PDF](docs/Pdfler/Hafta%203%20—%20Sepet%20Sistemi%20(Veritabanı)%20%26%20Modern%20Mobil%20UX.pdf) |
| **Hafta 4** | Sipariş Motoru, Admin Siparişler & SignalR | [PDF](docs/Pdfler/Hafta%204%20—%20Sipariş%20Motoru%2C%20Admin%20Siparişler%2C%20SignalR%2C%20UX%20İyileştirmeleri%2C%20CI_CD%2C%20Garson%20Çağır%2C%20Fiş%20Overlay%2C%20Saat%20Dilimi%20Fix.pdf) |
| **Hafta 5** | **Personel Servisleri (Garson, Mutfak, Kasa), Giriş Sistemi, ACID Tahsilat, AuditLog & Ünite Testleri** | [PDF](https://github.com/brhnshn/QRMenu/blob/main/docs/Pdfler/Hafta%205%20%E2%80%94%20Personel%20Servisleri%20(Garson%2C%20Mutfak%2C%20Kasa)%2C%20Giri%C5%9F%20Sistemi%2C%20ACID%20Tahsilat%2C%20AuditLog%20%26%20%C3%9Cnite%20Testleri.pdf) |
| **Hafta 5-2** | **Veritabanı Taşıma, Görsel Optimizasyonu, Kalori Özelliği, QR Giriş Fix & Masa Durum Takibi** | [PDF](https://github.com/brhnshn/QRMenu/blob/main/docs/Pdfler/Hafta%205-2%20%E2%80%94%20Veritaban%C4%B1%20Ta%C5%9F%C4%B1ma%2C%20G%C3%B6rsel%20Optimizasyonu%2C%20Kalori%20%C3%96zelli%C4%9Fi%2C%20QR%20Giri%C5%9F%20Fix%20%26%20Masa%20Durum%20Takibi.pdf) |
| **Hafta 6** | **İndirim Saatleri ve diğer işler** | [PDF](https://github.com/brhnshn/QRMenu/blob/main/docs/Pdfler/Hafta%206%20%C4%B0ndirim%20Saatleri%20ve%20di%C4%9Fer%20i%C5%9Fler.pdf) |
| **Hafta 7** | **Performans, Canlı Ekran Soft Refresh, Oyunlaştırma ve TR-EN İyileştirmeleri** | [PDF](https://github.com/brhnshn/QRMenu/blob/main/docs/Pdfler/Hafta%207%20-%20%20Gelistirme%20Raporu.pdf) |
| **Hafta 8** | **Garson, Mutfak ve Kasa Panellerinde Soft Refresh, SignalR Uyum Checklisti ve Operasyonel UI Tamamlamaları** | [PDF](docs/Pdfler/8.%20Hafta%20Geli%C5%9Ftirme%20Raporu.pdf) |
| **Hafta 9** | **Stok & Varyasyon Yönetimi, Stitch Tasarım Sistemi, Gelişmiş PDF Raporlama ve Güvenlik Sertleştirme** | [**PDF**](https://github.com/brhnshn/QRMenu/blob/main/docs/Pdfler/9.%20Hafta%20Geli%C5%9Ftirme%20Raporu%20-%20QR%20Men%C3%BC%20Sistemi.pdf) |
| **Hafta 10** | **GitHub Actions CI/CD Altyapısı & Admin Profil Yönetimi Entegrasyonu** | [**PDF**](docs/Pdfler/10_Hafta_Ozet_Raporu.pdf.pdf) |
| **Plan Takvimi** | 10 Haftalık Süreç ve Plan Takvimi | [PDF](docs/10%20Haftalık%20Plan%20Takvimi.pdf) |
| **Dosya Yapısı** | QR Menü Otomasyonu Genel Dosya Mimarisi | [PDF](docs/Pdfler/Proje%20Dosya%20Yapısı%20—%20QR%20Menü%20Otomasyonu.pdf) |

---
    
## 👨‍💻 Geliştirme Notları
### Hafta 10 kapsamında;
- **CI/CD Entegrasyonu:** GitHub Actions iş akışı aktif edilerek otomatik derleme ve kalite kontrol süreçleri sisteme kazandırıldı.
- **Admin Profil Yönetimi:** Admin panelindeki statik yönetici bilgileri dinamikleştirilerek veritabanı üzerinden gerçek verilerle yönetilebilir hale getirildi.

### Hafta 9 kapsamında;
- **Stok & Varyasyon:** Ürünlere boyut/ek özellik (varyasyon) desteği ve akıllı stok takip sistemi eklendi.
- **Tasarım:** Tüm arayüz **Stitch Design System** ve **Tailwind CSS** kullanılarak premium görünüme kavuşturuldu.
- **Raporlama:** Z-Raporu, Sipariş Arşivi ve Güvenlik Logları için dinamik PDF oluşturma altyapısı kuruldu.
- **Güvenlik:** OWASP Top 10 uyumlu CSRF koruması, Rate Limiting ve gelişmiş Security Logging Middleware entegre edildi.
- **Operasyon:** Günlük sıfırlanan sipariş numaralandırma sistemi ile mutfak-garson iletişimi hızlandırıldı.
