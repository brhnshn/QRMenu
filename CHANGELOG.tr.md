# Değişiklik Günlüğü (Changelog)

Tüm önemli değişiklikler bu dosyada belgelenir.
Format [Keep a Changelog](https://keepachangelog.com/tr/1.0.0/) standardına dayanır.

## [1.0.1] - 2026-10-26

### Güvenlik (Security)
- `/Auth/Recovery` anonim admin oluşturma güvenlik açığı kaldırıldı, güvenli CLI tabanlı `--create-admin` yöneticisi eklendi.
- Kriptografik token tabanlı QR oturumu (`Masa.QrToken`, `/qr/t/{token}`) devreye alındı; sıralı masa numarası tahmin açığı kapatıldı.
- `MenuHub` SignalR masa grupları ve personel kanalları cookie ve rol tabanlı yetkilendirme ile izole edildi.
- IP ve token bölümlü katı rate limiting politikaları tanımlandı.
- Katı CSP (`frame-ancestors 'self'`, `connect-src 'self' wss: https://translate.googleapis.com`) ve `SameSite=Lax`, `HttpOnly`, `Secure` çerez başlıkları uygulandı.
- PostgreSQL bağlantı dizgisi ortam değişkenlerine bağlandı, yerel port `127.0.0.1:5432` ile dış dünyaya kapatıldı.
- Ürün görseli yüklemelerine JPEG, PNG, WEBP magic-byte doğrulaması eklendi.

### Düzeltilenler (Fixed)
- Ödeme durumlarının (`TamOdendi`, `KismiOdendi`, `Iade`) `DurumGuncelleAsync` üzerinden kasasız güncellenmesi engellendi; yalnızca `OdemeService` yetkisine bağlandı.
- Sipariş oluşturulurken fiyatlar sunucuda canlı ürün, varyasyon ve opsiyon veritabanı fiyatlarıyla yeniden hesaplanacak şekilde dinamikleştirildi (Happy Hour saat penceresi bypass engeli).
- Eşzamanlı parçalı tahsilatta çift ödeme ve concurrency token yarış durumları kilitlendi.
- Gün sonu kontrolleri Türkiye saat dilimine (`Europe/Istanbul`) ve işlem zamanına göre uyarlandı; açık siparişli günlerin kapatılması engellendi.
- Kalem iptalinde stok iadesi yapılması ve sipariş iptalinde yalnızca aktif kalemlerin iade edilmesi kuralı sağlandı.
- Mutfak ve Garson durum güncellemelerine whitelist getirildi (Mutfak sadece Hazırlanıyor/Hazır; Garson TeslimEdildi/İptal).
- Çarkıfelek ve oyun indirimleri sunucu tarafında `RandomNumberGenerator` ile belirlenir hale getirildi; ödenmiş siparişe indirim yapılması engellendi.

### Değiştirilenler (Changed)
- 2.300+ satırlık monolitik `AdminController`, 5 bağımsız controller'a (`AdminUrunController`, `AdminMasaController`, `AdminKullaniciController`, `AdminRaporController`, `AdminHappyHourController`) ve Core/Data servis katmanlarına ayrıştırıldı.
- Mutfak, Kasa, Admin, Menü ve Siparişler sayfalarındaki devasa inline `<script>` blokları `wwwroot/js/pages/` altındaki harici JS modüllerine taşındı (JSON Island & data attribute mimarisi).
- EF Core, Identity ve Npgsql bağımlılıkları en güncel 8.0.x sürümlerine yükseltildi.

### Eklenenler (Added)
- Multi-stage derleme ve `USER app` non-root güvenlikli üretim `Dockerfile`'ı eklendi.
- PostgreSQL 16 ve ASP.NET Core servisini izole köprü ağı (`qrmenu_network`), kalıcı hacimler ve sağlık kontrolleriyle bağlayan `docker-compose.yml` ve `.dockerignore` eklendi.
- GitHub Actions CI iş akışı (`.github/workflows/ci.yml`) ve otomatik bağımlılık güncellemesi için `.github/dependabot.yml` eklendi.
- Uçtan uca rol yetkilendirme, güvenlik başlıkları ve rate limit senaryolarını doğrulayan 41 testlik `QRMenu.IntegrationTests` paketi eklendi. Toplam test sayısı 147'ye çıkarıldı.
