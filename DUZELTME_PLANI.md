# QR Menü: Bulgular ve Düzeltme Planı

> Kaynak: 26.10.2026 tarihli kod incelemesi (güvenlik, iş mantığı, mimari). Taban commit: `b0f7feb` (`origin/main`).
> Bu dosya, düzeltmeleri yapacak ajanlar için iş listesidir. Her madde tek başına uygulanabilecek şekilde yazıldı.

---

## 0. Ajanlar için genel kurallar

1. **Satır numaraları yaklaşıktır.** Kodu her zaman **metot/sınıf adıyla** bulun (ör. `OdemeService.ExecuteParcaliOdemeAsync`), satır numarasına körü körüne güvenmeyin.
2. **Paket sırası ve dosya sahipliği:** Her paket belirli dosyalara "sahip"tir. Aynı anda iki ajan **aynı dosyaya dokunmamalı**. Bağımlılıklar her paketin başında yazılı.
   - Paralel çalışabilir: **P1**, **P4**, **P6**
   - P1 bittikten sonra: **P2**, **P3**, **P5** (birbirleriyle paralel)
   - En son: **P7**
3. Her değişiklikten sonra repo kökünde çalıştırın, ikisi de yeşil olmadan iş bitmiş sayılmaz:
   ```
   dotnet build
   dotnet test
   ```
   Başlangıç durumu: 0 uyarı, 0 hata, 70/70 test geçiyor.
4. Her düzeltme için **en az bir test** ekleyin (`tests/QRMenu.Tests`, mevcut testler EF InMemory kullanıyor; aynı kalıbı izleyin).
5. Kod stilini koruyun: Türkçe isimlendirme (`Siparis`, `OdemeTipi`, `GunKapaliMiAsync`), mevcut yorum yoğunluğu, `async/await`.
6. Şema değişikliği gerekiyorsa migration'ı elle yazmayın:
   ```
   dotnet ef migrations add <Ad> --project src/QRMenu.Data --startup-project src/QRMenu.Web
   ```
7. `appsettings.json` / `appsettings.Development.json` dosyalarına **dokunmayın** ve içeriğini hiçbir yere yazmayın (gerçek parola içeriyor, git'te değil).
8. Commit/push yapmayın; değişiklikleri çalışma alanında bırakın (sahibi gözden geçirecek).
9. Kullanıcıya dönen hata mesajları Türkçe ve genel olsun; ayrıntı `_logger` ile loglansın.

**Not:** Proje şu an yayında değil. Sunucu/deploy maddeleri (P6'daki) bu yüzden düşük öncelikli; yine de yayına almadan önce yapılmalı.

---

## P1: Sipariş, ödeme ve sepet servisleri (çekirdek iş mantığı)

**Sahip olduğu dosyalar:**
- `src/QRMenu.Data/Services/SiparisService.cs`
- `src/QRMenu.Data/Services/OdemeService.cs`
- `src/QRMenu.Data/Services/SepetService.cs`
- `src/QRMenu.Core/DTOs/*`
- `src/QRMenu.Core/Interfaces/ISiparisService.cs`, `IOdemeService.cs`, `ISepetService.cs`
- İlgili test dosyaları

**Bağımlılık:** Yok.

### P1.1 [YÜKSEK] Ödeme yapılmadan sipariş "ödendi/iade" yapılabiliyor
- **Sorun:** `SiparisService._gecisKurallari` içinde `TeslimEdildi → TamOdendi / KismiOdendi / Iade` geçişine izin var. `DurumGuncelleAsync` bu geçişleri herhangi bir çağırana açıyor. Garson/Mutfak controller'ları istemciden gelen `YeniDurum`'u doğrudan iletiyor. Sonuç: `Odeme` kaydı oluşmadan sipariş ödenmiş görünüyor ve Z raporu eksik çıkıyor.
- **Düzeltme:**
  1. `DurumGuncelleAsync(int siparisId, SiparisDurum yeniDurum)` içinde, hedef durum `TamOdendi`, `KismiOdendi` veya `Iade` ise `InvalidOperationException("Bu durum yalnızca kasa işlemiyle ayarlanabilir.")` fırlat.
  2. Ödeme durumlarını yalnızca `OdemeService` ayarlasın; bunun için `DurumGuncelleAsync`'i çağırmayın, durumu `OdemeService` içinde doğrudan set edin. Bunun zaten böyle olup olmadığını kontrol edin.
  3. İade akışı şimdilik desteklenmiyorsa `Iade` geçişini tamamen kapatın. Kalacaksa ayrı bir `IadeEtAsync` metodu yazın: negatif `Odeme` kaydı oluştursun, stok iadesini P1.6 kuralına göre yapsın.
- **Kabul:** `DurumGuncelleAsync(id, TamOdendi)` exception fırlatıyor. Kasadan ödeme hâlâ çalışıyor. İkisi için test var.

### P1.2 [YÜKSEK] Sipariş fiyatı sepetteki kayıtlı fiyata güveniyor (Happy Hour penceresi bypass)
- **Sorun:** `ExecuteSiparisOlusturAsync`, `HappyHourBilgisiGetirAsync` ile Happy Hour bilgisini okuyor ama fiyat için `SepetDetay.BirimFiyat`'ı kullanıyor. Bu fiyat, Happy Hour kaydedildiği anda saat penceresine bakılmadan topluca güncelleniyor (bkz. P3.1). HH bitince de indirimli kalıyor.
- **Düzeltme:** `ExecuteSiparisOlusturAsync` içinde her kalemin birim fiyatını **sunucuda yeniden hesaplayın**:
  - Formül: `ürün.Fiyat (veya varyasyon fiyatı) + seçili opsiyonların EkFiyat toplamı`.
  - Ardından **şu an** aktif ve saat penceresi içinde olan Happy Hour'u uygulayın.
  - Tek bir private yardımcı yazın, ör. `BirimFiyatHesaplaAsync(urunId, varyasyonId, opsiyonIds, hhBilgisi)`. `SepetService.AddItemAsync` ve manuel sipariş de aynı yardımcıyı kullansın. Böylece HH'nin baz fiyata mı toplam fiyata mı uygulandığı tutarsızlığı da biter (şu an sepette baz fiyata, manuel siparişte toplama uygulanıyor). **Karar: HH, opsiyonlar dahil toplam birim fiyata uygulansın.**
  - Hesaplanan fiyat sepettekinden farklıysa yeni fiyatı kullanın; `SepetDetay`'ı da güncelleyin.
- **Kabul:** HH penceresi dışında sipariş, sepet indirimli kaydedilmiş olsa bile tam fiyatla oluşuyor. Test var.

### P1.3 [YÜKSEK] Aynı kalemin iki kasada eşzamanlı ödenmesi (çift tahsilat)
- **Sorun:** `OdemeService.ExecuteParcaliOdemeAsync` önce kalemleri okuyup durumlarını kontrol ediyor, sonra `Odeme` satırı ekliyor. `SiparisDetay` üzerinde concurrency token yok ve isolation ReadCommitted. İki kasiyer aynı kalemi aynı anda öderse iki `Odeme` oluşuyor.
- **Düzeltme:**
  1. Transaction içinde kalemleri **koşullu güncelleme** ile kilitleyin:
     ```csharp
     var etkilenen = await _context.SiparisDetaylar
         .Where(d => ids.Contains(d.Id) && (d.Durum == SiparisDurum.TeslimEdildi || d.Durum == SiparisDurum.KismiOdendi))
         .ExecuteUpdateAsync(s => s.SetProperty(d => d.Durum, SiparisDurum.TamOdendi));
     if (etkilenen != ids.Count) { rollback; return false; }
     ```
     Gerçek alan adlarını entity'den kontrol edin. `ExecuteUpdate` InMemory sağlayıcıda çalışmıyorsa:
     - Mevcut kodda `ExecuteUpdate` nasıl kullanıldıysa (stok düşümünde kullanılıyor) aynı yaklaşımı izleyin.
     - Testte bu yolu atlayacak şekilde (ör. sağlayıcı kontrolü) kurgulayın.
     - Alternatif olarak `SiparisDetay`'a `RowVersion` ekleyip `DbUpdateConcurrencyException` yakalayın.
  2. Sipariş tamamen ödendiğinde `Siparis.GuncellemeTarihi = DateTime.UtcNow` set edin. Böylece RowVersion güncellenir ve eşzamanlı iptal çakışma verir.
- **Kabul:** Aynı kalem için ikinci ödeme çağrısı `false` dönüyor ve ikinci `Odeme` satırı oluşmuyor.

### P1.4 [ORTA] Ödeme servisi kalem durumunu doğrulamıyor
- **Sorun:** Yalnızca `TamOdendi` ve `Iptal` kalemler atlanıyor. `Onaylandi`, `Hazirlaniyor`, `Hazir` kalemler de ödenebiliyor; hazırlanırken ödenen kalem mutfak panelinden kayboluyor. Ödenecek tutar 0 olduğunda da `true` dönüyor.
- **Düzeltme:** Yalnızca `TeslimEdildi` ve `KismiOdendi` kalemleri kabul edin. Ödenecek tutar `<= 0` ise `false` dönün.
  > İş kararı: "Teslim edilmeden önce ödeme" isteniyorsa `Hazir` de eklenebilir. Varsayılan olarak **sadece teslim edilmiş** kalemler ödensin.
- **Kabul:** Hazırlanıyor durumundaki kalemle ödeme `false` dönüyor. Test var.

### P1.5 [YÜKSEK] Gün sonu kapatılınca açık siparişler kilitleniyor
- **Sorun:** `GunKapaliMiAsync` kontrolü `DurumGuncelleAsync`, `ExecuteSiparisDetayIptalAsync` ve `OdemeService` içinde siparişin `OlusturmaTarihi`'ne göre yapılıyor. Gün kapandıktan sonra o günün açık siparişleri ödenemiyor ve iptal edilemiyor. `SiparisOlusturAsync` ise kapalı günü hiç kontrol etmiyor.
- **Düzeltme (servis tarafı):**
  1. `ExecuteSiparisOlusturAsync` ve `ExecuteManuelSiparisOlusturAsync`: bugün (TR saati) kapalıysa `InvalidOperationException("Gün sonu kapatılmış, yeni sipariş alınamaz.")`.
  2. `OdemeService`: kapalı gün kontrolünü siparişin oluşturma tarihine göre değil, **ödeme tarihine** (şimdi) göre yapın.
  3. Controller tarafındaki kontrol P3.3'te: gün kapatılırken açık sipariş varsa reddedilecek.
- **Gün hesabı:** TR günü için tek bir yardımcı kullanın. Projede zaten varsa onu kullanın; yoksa `Europe/Istanbul` (Windows'ta `Turkey Standard Time`) ile `TimeZoneInfo` dönüşümü yapın. `DateTime.UtcNow.Date` **kullanmayın**.
- **Kabul:** Kapalı günde yeni sipariş reddediliyor. Kapanıştan önce oluşmuş bir sipariş ertesi gün ödenebiliyor.

### P1.6 [ORTA] Stok iadesi tutarsız
- **Sorun:**
  - `ExecuteSiparisDetayIptalAsync` (kalem iptali) stok iadesi yapmıyor.
  - `DurumGuncelleAsync` içindeki `Iptal` dalı **tüm** detaylar için `StokIadeAsync` çağırıyor; zaten iptal edilmiş veya ödenmiş kalemler de buna dahil.
- **Düzeltme:** Kural: **bir kalem iptal edildiği anda, bir kez stok iadesi yapılır.**
  - Kalem iptali → o kalemin stoğu iade edilir.
  - Sipariş iptali → yalnızca durumu `Iptal` veya `TamOdendi` **olmayan** kalemler iade edilir ve `Iptal` yapılır.
- **Kabul:** Kalem iptali stoğu artırıyor. Daha önce iptal edilmiş kalem, sipariş iptalinde ikinci kez iade edilmiyor. Test var.

### P1.7 [ORTA] Kısmi iptalden sonra sipariş `TamOdendi`'ye geçmiyor
- **Sorun:** `ExecuteSiparisDetayIptalAsync` sonunda `aktifKaldiMi` hesaplanırken `TamOdendi` kalemler de aktif sayılıyor. Örnek: A ödenir, B iptal edilir; sipariş sonsuza dek `TeslimEdildi` kalır ve masa "dolu" görünür.
- **Düzeltme:**
  - Kalan kalemlerin tamamı `Iptal` ise sipariş `Iptal` olsun.
  - `Iptal` olmayanların tamamı `TamOdendi` ise sipariş `TamOdendi` olsun.
  - Aksi halde mevcut durum korunsun.
- **Kabul:** Yukarıdaki senaryo için test var.

### P1.8 [ORTA] Manuel (garson) siparişi varyasyonu yok sayıyor
- **Sorun:** `ManuelSiparisDetay` DTO'sunda `UrunVaryasyonId` yok. Varyasyonlu ürün baz fiyattan satılıyor ve stok ana üründen düşüyor. Masanın geçerliliği ve opsiyonun ürüne ait olup olmadığı da kontrol edilmiyor.
- **Düzeltme:**
  1. DTO'ya `int? UrunVaryasyonId` ekleyin.
  2. `ExecuteManuelSiparisOlusturAsync` içinde:
     - masa var ve aktif mi kontrol edin;
     - varyasyon ürüne ait mi kontrol edin;
     - opsiyonlar ürüne ait ve aktif mi kontrol edin;
     - fiyatı P1.2'deki yardımcıyla hesaplayın;
     - stoğu varyasyondan düşün (`StokDusAsync(urunId, urunVaryasyonId, adet)` zaten destekliyor).
  3. Controller ve view tarafı P2.3'te.
- **Kabul:** Varyasyonlu manuel siparişte doğru fiyat ve doğru stok. Test var.

### P1.9 [DÜŞÜK] Sepete eklenen opsiyonlar doğrulanmıyor
- **Sorun:** `SepetService.AddItemAsync` içinde `opsiyonIds` için, opsiyonun bu ürüne ait ve aktif olup olmadığı kontrol edilmiyor. Bu yüzden başka ürünün opsiyonu eklenebiliyor.
- **Düzeltme:** Ürüne ait olmayan ya da pasif opsiyon ID'si varsa `InvalidOperationException` fırlatın.
- **Kabul:** Test var.

### P1.10 [DÜŞÜK] Sepette aynı satıra ekleme, eski adetlerin fiyatını değiştiriyor
- **Sorun:** `AddItemAsync`, mevcut satıra adet eklerken `BirimFiyat`'ı yeni fiyatla eziyor. Fiyatı P1.2 ile sipariş anında yeniden hesaplayacağımız için bu artık sadece görsel bir sorun.
- **Düzeltme:** Satır birleştirmede fiyatı güncel fiyata çekmek kabul edilebilir; davranışı bir yorumla belirtin. Daha temiz yol: HH durumu farklıysa ayrı satır açmak. Basit tutun.

### P1.11 [DÜŞÜK] Pro-rata tutarlar yuvarlanmıyor ve ödeme tipi sessizce Nakit oluyor
- **Düzeltme:**
  - `OdemeService` içinde kalem bazlı tutarları `Math.Round(x, 2, MidpointRounding.AwayFromZero)` ile yuvarlayın. Son kaleme farkı ekleyerek toplamın siparişle kuruşu kuruşuna tutmasını sağlayın.
  - `OdemeTipi` parse edilemezse `Nakit`'e düşmek yerine `false` dönün (veya exception fırlatın).
- **Kabul:** 3 kalemli, 100 TL'lik siparişte ödemelerin toplamı tam 100,00 çıkıyor.

---

## P2: Personel controller'ları (Garson, Mutfak, Kasa)

**Sahip olduğu dosyalar:**
- `src/QRMenu.Web/Controllers/GarsonController.cs`
- `src/QRMenu.Web/Controllers/MutfakController.cs`
- `src/QRMenu.Web/Controllers/KasaController.cs`
- `Views/Garson/*`, `Views/Mutfak/*`, `Views/Kasa/*`

**Bağımlılık:** P1 bitmiş olmalı (P1.1, P1.8).

### P2.1 [YÜKSEK] Rol bazlı izinli hedef durumlar
- **Düzeltme:** Her controller'ın `DurumGuncelle` action'ında, servisi çağırmadan önce hedef durumu bir beyaz listeyle kontrol edin. Liste dışındaysa `Json(new { success = false, message = "Bu işlem için yetkiniz yok." })` dönün.

  | Controller | Action | İzinli hedef durumlar |
  |---|---|---|
  | `MutfakController` | `DurumGuncelle` | `Hazirlaniyor`, `Hazir` |
  | `GarsonController` | `DurumGuncelle` | `TeslimEdildi`, `Iptal` |

  `SiparisController.DurumGuncelle` (`[Authorize(Policy="RequireStaff")]`, `/siparis/durum-guncelle`) **P5'e aittir**, oraya dokunmayın.
- **Kabul:** Mutfak kullanıcısı `TeslimEdildi`/`TamOdendi` gönderdiğinde reddediliyor.

### P2.2 [ORTA] `GarsonController.UrunIptal` servisi atlıyor
- **Sorun:** `UrunIptal(int detayId)` doğrudan DbContext ile kalemi iptal ediyor:
  - `ToplamTutar`'ı güncellemiyor;
  - stok iadesi yapmıyor;
  - gün-kapalı kontrolü yok;
  - ödenmiş kalemi de iptal edebiliyor.
  
  Sonuç: kasada pro-rata hesap bozuluyor ve fazla tahsilat oluyor. View'larda kullanılmıyor görünüyor.
- **Düzeltme:**
  1. `grep -rn "UrunIptal" src/` ile kullanımları doğrulayın.
  2. Kullanılmıyorsa action'ı **silin**. Kullanılıyorsa gövdesini `_siparisService.SiparisDetayIptalEtAsync(...)` çağrısına çevirin (aynı controller'daki `SiparisDetayIptal` action'ı gibi).
- **Kabul:** Doğrudan DbContext ile iptal eden kod kalmıyor.

### P2.3 [ORTA] Manuel siparişte varyasyon
- **Düzeltme:**
  1. `ManuelSiparisRequest` içindeki kalem modeline `UrunVaryasyonId` ekleyin ve DTO'ya aktarın (P1.8).
  2. `Views/Garson/YeniSiparis.cshtml` içinde varyasyon seçimi yoksa ekleyin. Varyasyonlu ürünlerde seçim zorunlu olsun; `Menu/Index.cshtml`'deki varyasyon seçim mantığına bakın.
- **Kabul:** Garson varyasyonlu ürün eklerken varyasyon seçiyor ve doğru fiyat görünüyor.

### P2.4 [ORTA] Kasa "Ödenen Siparişler" günlük ciro hesabı
- **Sorun:** `KasaController.OdenenSiparisler` gün sınırı için `DateTime.UtcNow.Date` kullanıyor (TR'de gün 03:00'te dönüyor). Ayrıca sipariş tarihine göre filtreliyor ve `Iade` siparişlerini işlem sayısına katıyor.
- **Düzeltme:**
  - TR günü aralığını P1.5'teki yardımcıyla hesaplayın.
  - Ciroyu `Odemeler` tablosundan `OdemeTarihi` aralığına göre toplayın.
  - `Iade`/`Iptal` siparişleri sayım ve ortalamadan çıkarın.
- **Kabul:** Dün açılıp bugün ödenen sipariş bugünün cirosunda görünüyor.

### P2.5 [DÜŞÜK] `TahsilatYap` başarısız ödemede de log yazıyor
- **Düzeltme:** Güvenlik/işlem logunu ve SignalR `OdemeYapildi` bildirimini yalnızca `ParcaliOdemeAsync` `true` döndüğünde gönderin.

### P2.6 [ORTA] Ham `ex.Message` istemciye dönüyor
- **Düzeltme:** Bu üç controller'daki `catch (Exception ex)` bloklarında:
  - `_logger.LogError(ex, ...)` ile loglayın;
  - istemciye `"İşlem sırasında bir hata oluştu."` dönün.
  
  `InvalidOperationException` mesajları iş kuralı mesajıdır ve servisimiz Türkçe yazıyor; bunlar dönmeye devam edebilir. Ama `DbUpdateException`, `NpgsqlException` ve genel `Exception` asla dönmesin.

---

## P3: Admin paneli ve arka plan servisi

**Sahip olduğu dosyalar:**
- `src/QRMenu.Web/Controllers/AdminController.cs`
- `src/QRMenu.Web/Services/OturumTemizleyici.cs`
- `Views/Admin/*`

**Bağımlılık:** P1 bitmiş olmalı (P1.2 fiyat yardımcısı).

### P3.1 [YÜKSEK] Happy Hour kaydedilince tüm sepetlerin fiyatı anında değişiyor
- **Sorun:** `HappyHourKaydet` → `UpdateSepetFiyatlariAsync(hh, yeniUrunIds)`:
  - saat penceresine bakmadan bütün açık sepetleri indirimli yapıyor;
  - fiyatı `urun.Fiyat + opsiyon` olarak hesapladığı için varyasyonların ek fiyatını düşürüyor.
- **Düzeltme:**
  - P1.2 sonrası fiyat sipariş anında yeniden hesaplanacak. Bu metot sadece **görsel tutarlılık** için kalmalı.
  - Fiyatı P1.2'deki ortak hesaplamayla yapın: varyasyon dahil, HH yalnızca pencere içindeyse.
  - Daha basit alternatif: metodu tamamen kaldırın. Sepet sayfası açılırken fiyatları güncel hesapla gösterin.
  - `catch {}` ile yutulan JSON hatalarını loglayın.
- **Kabul:** 14:00-17:00 HH'si 10:00'da kaydedildiğinde sepetler değişmiyor.

### P3.2 [DÜŞÜK] `HappyHourKaydet` geçersiz oranda `success=true` dönüyor
- **Düzeltme:** Oran 0-100 dışında veya başlangıç ≥ bitiş ise `success=false` ve açıklayıcı mesaj dönün. Gece yarısını aşan pencere destekleniyorsa bu kuralı ona göre uyarlayın.

### P3.3 [YÜKSEK] `GunSonuKapat` açık sipariş kontrolü yapmıyor ve çift tıkta 500 veriyor
- **Düzeltme:**
  1. Kapatılan gün için `Onaylandi`, `Hazirlaniyor`, `Hazir`, `TeslimEdildi` veya `KismiOdendi` durumunda sipariş varsa reddedin. Mesajda kaç açık sipariş olduğunu yazın.
  2. Aynı gün için `GunSonuRapor` zaten varsa `success=false, "Bu gün zaten kapatılmış."` dönün.
  3. Unique index'ten gelen `DbUpdateException`'ı da yakalayın.
  4. Z raporu tutarlarını `Odemeler` tablosundan, `OdemeTarihi` TR günü aralığına göre hesaplayın.
- **Kabul:** Açık siparişli günde kapatma reddediliyor. Çift tık 500 vermiyor.

### P3.4 [ORTA] Panel ciro ve en çok satanlar tanımları tutarsız
- **Sorun:** `Panel` action'ında:
  - `bugunCiro`'ya ödenmemiş siparişler de giriyor;
  - `enCokSatanlar` iptal edilmiş kalemleri sayıyor;
  - aynı ürün `BirimFiyat`'a göre gruplandığı için HH ve normal fiyat ayrı satır oluyor.
- **Düzeltme:**
  - Ciro = bugünün (TR) `Odemeler` toplamı. İstenirse "açık hesap" ayrı bir metrik olarak gösterilebilir.
  - En çok satanlar: `Iptal`/`Iade` kalemleri hariç tutun, `UrunId`'ye göre gruplayın; tutar alanı `Sum(Adet*BirimFiyat)` olsun.

### P3.5 [ORTA] `OturumTemizleyici` Happy Hour'u kalıcı olarak kapatıyor
- **Sorun:** Pencere bitince `AktifMi=false` yapılıyor. Bu yüzden günlük tekrarlanan HH ertesi gün çalışmıyor ve "sonraki saat" için kaydedilen HH (ör. 08-09, şimdi 10:00) 30 dk içinde kapanıyor. Saat penceresi zaten sipariş anında kontrol ediliyor.
- **Düzeltme:** HH kapatan bloğu kaldırın. `AktifMi` yalnızca admin tarafından değiştirilsin.

### P3.6 [ORTA] `OturumTemizleyici` ödenmemiş siparişi olan oturumu kapatıyor
- **Sorun:** 2 saat işlem görmeyen oturum, masada ödenmemiş sipariş olsa da pasife alınıyor ve sepeti siliniyor.
- **Düzeltme:** Oturuma bağlı `TamOdendi`/`Iptal`/`Iade` dışında sipariş varsa oturumu kapatmayın. "Sahipsiz sepet" temizliği cascade nedeniyle ölü kodsa:
  - doğrulayın;
  - ölü kodsa silin.

### P3.7 [DÜŞÜK] `/images/urun/{id}` müşteriler için çalışmıyor
- **Sorun:** Route `AdminController` içinde, sınıf düzeyindeki `[Authorize]` nedeniyle müşteriye 401/302 dönüyor.
- **Düzeltme:** Action'a `[AllowAnonymous]` ekleyin. Daha temiz yol: action'ı yeni bir `GorselController`'a (anonim) taşıyın. Taşırsanız `GarsonController.UrunGorsel`'deki tekrarı da buradan kullanın.

### P3.8 [DÜŞÜK] Admin XSS ve `ex.Message`
- `Views/Admin/Siparisler.cshtml`: JS'te `d.urunAd` kaçışsız `innerHTML`'e yazılıyor. Projedeki `escapeHtml` yardımcısıyla kaçışlayın.
- `AdminController` (`ex.Message` dönen `catch` bloğu, ~1080): P2.6 kuralını uygulayın; tüm dosyada `ex.Message` araması yapın.

### P3.9 [DÜŞÜK] Görsel yükleme: magic-byte kontrolü
- Ürün görseli yükleyen action'da (uzantı beyaz listesi ve 2 MB kontrolünün olduğu yer, ~1600) ilk baytları kontrol edin: JPEG `FF D8 FF`, PNG `89 50 4E 47`, WEBP `RIFF....WEBP`. Dosya adını `Guid` ile üretin (zaten öyleyse dokunmayın).

---

## P4: Güvenlik: kimlik doğrulama, QR oturumu, SignalR, HTTP başlıkları

**Sahip olduğu dosyalar:**
- `src/QRMenu.Web/Controllers/AuthController.cs`
- `src/QRMenu.Web/Controllers/QRController.cs`
- `src/QRMenu.Web/Hubs/MenuHub.cs`
- `src/QRMenu.Web/Program.cs`
- `src/QRMenu.Web/Middleware/*`
- `Views/Auth/*`
- `src/QRMenu.Core/Entities/Masa.cs` + yeni migration
- `src/QRMenu.Data/Data/*` (seed)

**Bağımlılık:** Yok. P1 ile paralel çalışabilir. **İstisna:** P4.3'teki QR görseli üretimi `AdminController` içinde; o küçük değişikliği P3 bittikten sonra yapın veya P3 ajanına devredin.

### P4.1 [YÜKSEK] `/Auth/Recovery` anonim Admin oluşturuyor
- **Sorun:** `AuthController.Recovery` (GET+POST), yapılandırmadaki `Security:RecoveryCode` ile eşleşen herkese istediği kullanıcı adı/parola ile Admin hesabı açtırıyor. Karşılaştırma `!=` ile yapılıyor (timing). `AdminLogin.cshtml`'de herkese görünen "ACİL YÖNETİCİ KURTARMA" linki var.
- **Düzeltme:**
  1. Web endpoint'ini kaldırın; `Recovery` action'larını ve `Views/Auth/Recovery.cshtml`'i silin.
  2. `AdminLogin.cshtml`'deki linki kaldırın.
  3. `RecoveryLimitPolicy` rate limit kaydı varsa `Program.cs`'den temizleyin.
  4. Yerine bir CLI yolu ekleyin:
     - `Program.cs` başında `args` içinde `--create-admin <kullanici> <parola>` varsa scope açın;
     - `UserManager` ile Admin oluşturun ve programdan çıkın (`return`);
     - kullanım şekli README'de belgelensin.
- **Kabul:** `/Auth/Recovery` 404 dönüyor ve CLI ile admin oluşturulabiliyor.

### P4.2 [ORTA] SignalR masa grubuna doğrulamasız katılım
- **Sorun:** `MenuHub.OnConnectedAsync`, `?tableId=N` sorgu parametresiyle istemciyi o masanın grubuna alıyor; `JoinTableGroup(int tableId)` da herkese açık. Herkes herhangi bir masanın sipariş ve ödeme olaylarını dinleyebiliyor.
- **Düzeltme:**
  1. Müşteri bağlantısında masa ID'sini istemciden **almayın**. `qrmenu_token` cookie'sinden oturumu çözün (`TokenService`/`TokenValidationMiddleware`'in kullandığı yöntemle) ve masa ID'sini oradan türetin.
  2. `JoinTableGroup`:
     - personel (`Context.User` kimlikli ve personel rolünde) ise izin verin;
     - değilse yalnızca cookie'den türetilen masaya izin verin.
  3. Personel gruplarına (mutfak/garson/kasa) katılan metotlar varsa `Context.User` rol kontrolü ekleyin.
  4. İstemci JS'inde `tableId` göndermeye devam edilebilir; sunucu yok sayar.
- **Kabul:** Cookie'siz bir bağlantı `JoinTableGroup(3)` ile 3 numaralı masanın olaylarını alamıyor.

### P4.3 [ORTA] QR adresi tahmin edilebilir (`/qr/{masaNo}`)
- **Sorun:** Herkes `/qr/1..N` açarak uzaktan geçerli oturum alıp sipariş verebiliyor. `/qr/validate/{n}` masa listesini ifşa ediyor.
- **Düzeltme:**
  1. `Masa` entity'sine `string QrToken` ekleyin: 32+ karakter, `RandomNumberGenerator` ile üretilen URL-safe Base64 ve unique index. Migration'da mevcut masalar için token üretin; SQL'de `gen_random_uuid()` veya seed/başlangıçta doldurma kullanılabilir.
  2. Yeni route: `/qr/t/{token}`. Eski `/qr/{masaNo}` route'u:
     - development'ta çalışmaya devam etsin;
     - prod'da 404 dönsün (veya bir ayar bayrağıyla açılsın: `Security:LegacyQrEnabled`).
  3. `Validate` endpoint'i ya token ile çalışsın ya da kaldırılsın (kullanımını `grep` ile kontrol edin).
  4. Admin QR üretimi (`AdminController`, `QRCoder` kullanılan yer) URL'yi token'lı yeni route ile üretsin. Admin'e "QR'ı yenile" (token döndürme) butonu eklemek iyi olur. Bu adım `AdminController`'a dokunduğu için P3 ile çakışmamaya dikkat edin.
  5. `/qr/*` endpoint'ine IP bazlı rate limit uygulayın (P4.4).
- **Kabul:** Sıralı masa numarasıyla oturum açılamıyor; token'lı URL ile açılıyor.

### P4.4 [ORTA] Rate limit atlatılabiliyor
- **Sorun:** `Program.cs` içindeki rate limiter anahtarı `qrmenu_token` cookie'si. Saldırgan yeni oturum alarak limiti sıfırlıyor. Cookie'siz tüm istekler tek "anonymous" kovasında toplanıyor ve meşru kullanıcıları da kısıtlıyor. Login için IP limiti yok.
- **Düzeltme:**
  - Partition anahtarı: cookie varsa `token`, yoksa `HttpContext.Connection.RemoteIpAddress`.
  - `/qr/*`, `/Auth/Login`, `/Auth/AdminLogin` için ayrı IP bazlı policy ekleyin (ör. dakikada 10).
  - Reverse proxy arkasında çalışılacaksa `UseForwardedHeaders` yapılandırmasını not edin.
  - `AuthController`'daki lockout mesajı "5 dakika" diyor ama süre 15 dk. Mesajı düzeltin.

### P4.5 [ORTA] HTTP güvenlik başlıkları ve cookie'ler
- **Düzeltme:**
  - `QRController` içindeki `qrmenu_token` ve `masa_no` cookie'lerinde:
    - `Secure = !env.IsDevelopment()` (veya `Request.IsHttps`);
    - `HttpOnly = true`;
    - `SameSite = Lax`.
  - `Program.cs` header middleware'ine şunları ekleyin:
    - `Referrer-Policy: strict-origin-when-cross-origin`
    - `Permissions-Policy: camera=(), microphone=(), geolocation=()`
    - CSP'ye `frame-ancestors 'self'`
  - CSP'yi daraltın: `connect-src 'self' wss:`.
  - `cdn.tailwindcss.com` (Play CDN) prod için uygun değil. Tailwind'i build adımına almak ayrı bir iştir; şimdilik bir TODO yorumu bırakın.
  - `unsafe-inline` kaldırma P7 kapsamında (inline JS'ler dışarı çıkarılınca).

### P4.6 [DÜŞÜK] Kimlik doğrulama küçük düzeltmeleri
- `AuthController.Login`: Admin hesabıyla personel girişinde farklı mesaj dönüyor (hesap numaralandırma). Tüm başarısız durumlarda aynı genel mesajı dönün.
- `Logout` GET ile çalışıyor (CSRF ile zorla çıkış mümkün). `[HttpPost]` yapın ve view'lardaki çıkış linklerini antiforgery token'lı küçük formlara çevirin. `grep -rn "Logout" src/QRMenu.Web/Views` ile hepsini bulun.
- Parola politikası (`Program.cs`, Identity options): `RequiredLength = 8` olsun. Seed'de mevcut kullanıcı parolası buna uymuyorsa seed'i de güncelleyin.

### P4.7 [DÜŞÜK] `Html.Raw` ile tek tırnaklı attribute
- `Views/Menu/Index.cshtml` (`data-opsiyonlar='@Html.Raw(json)'`) ve `Views/Garson/YeniSiparis.cshtml` (iki yer): JSON içindeki `'` attribute'u kırabilir.
- **Düzeltme:** `Html.Raw` kaldırılıp `data-opsiyonlar="@json"` yazılsın (Razor HTML-encode eder, `dataset` okuyunca JSON geri gelir). Alternatif: `JsonSerializer.Serialize(x, new JsonSerializerOptions { Encoder = JavaScriptEncoder.Default })`. JS tarafında `JSON.parse(el.dataset.opsiyonlar)` çalışmaya devam etmeli; test edin.
  > `Views/Garson/YeniSiparis.cshtml` P2'ye ait. Bu değişikliği P2 bittikten sonra yapın veya P2 ajanına devredin.

### P4.8 [YÜKSEK, yayına almadan önce] Veritabanı bağlantısı
- `Program.cs` (~54): prod'da `SslMode.Disable` var. `SslMode.Require` yapın; sertifika doğrulaması için gerekirse `Trust Server Certificate` ayarını yapılandırmadan okuyun.
- Connection string'i ortam değişkeninden okuyun: `ConnectionStrings__DefaultConnection`. Development'ta ise `dotnet user-secrets` kullanılsın; `QRMenu.Web.csproj`'a `UserSecretsId` ekleyin.
- README'ye kurulum notu ekleyin.
- **Sahip tarafından yapılacak (ajan değil):** DB parolasını değiştirmek ve sunucuda 5432 portunu dışarıya kapatmak.

---

## P5: Müşteri sipariş controller'ı ve oyunlar

**Sahip olduğu dosyalar:**
- `src/QRMenu.Web/Controllers/SiparisController.cs`
- `src/QRMenu.Web/Controllers/OyunController.cs`
- `Views/Siparis/*`

**Bağımlılık:** P1 bitmiş olmalı.

### P5.1 [ORTA] Oyun sonucu istemciden geliyor
- **Sorun:** `OyunSonuclandir` içinde `BasariliMi` istemciden geliyor; Çarkıfelek ve Kazı-kazan sonucu sunucuda belirlenmiyor. Hafıza oyununda eşleşme haritası (`eslesmeId`) kartlarla istemciye gönderiliyor (`OyunSecimleri`). Süre kontrolü de yok.
- **Düzeltme:**
  - **Çarkıfelek / Kazı-kazan:** Sonucu sunucuda `RandomNumberGenerator` ile, admin'in tanımladığı olasılıklara göre belirleyin. İstemci yalnızca "oyna" isteği atsın, sunucu sonucu dönsün; animasyon bu sonuca göre oynasın.
  - **Hafıza oyunu:**
    - eşleşme bilgisini istemciye göndermeyin;
    - kart açma isteği sunucuya gitsin, sunucu kartın yüzünü dönsün (oyun durumu sunucuda: session veya DB);
    - en az bir minimum süre kontrolü ekleyin (ör. 8 kart için < 5 sn ise reddedin).
  - Kapsam büyükse önce Çarkıfelek/Kazı-kazan'ı sunucuya taşıyın, hafıza oyununa minimum süre kontrolü ekleyin; tam sunucu tarafı hafıza oyununu ayrı iş olarak bırakın.
- **Kabul:** İstemci `BasariliMi=true` gönderse bile sunucu kendi sonucunu kullanıyor.

### P5.2 [ORTA] Oyun ödülü yarışı ve ödenmiş siparişe indirim
- **Sorun:** `OyunOynandiMi` kontrolü atomik değil; iki eşzamanlı istek ikisi de indirim alıyor. İndirim kısmen ödenmiş siparişe de uygulanıyor: 100 TL'nin 50'si ödenmişse %50 indirim kalan kalemi 25'e düşürüyor ve toplam tahsilat 75 oluyor. Gün-kapalı kontrolü de yok.
- **Düzeltme:**
  1. Koşullu güncelleme yapın: `ExecuteUpdateAsync(... WHERE Id = id AND OyunOynandiMi = false)`. Etkilenen satır 0 ise "Bu sipariş için oyun zaten oynandı." dönün. InMemory notu için P1.3'e bakın.
  2. Siparişte `TamOdendi`/`KismiOdendi` kalem varsa veya sipariş `Iptal`/`Iade` ise oyunu reddedin.
  3. İndirimi yalnızca ödenmemiş kalemlerin `BirimFiyat`'ına uygulayın ve `ToplamTutar`'ı kalemlerden yeniden hesaplayın.
- **Kabul:** Paralel iki istekte yalnızca biri başarılı. Kısmen ödenmiş siparişte oyun reddediliyor.

### P5.3 [ORTA] `ex.Message` sızıntısı
- `SiparisController` içinde anonim müşteriye dönen genel `catch (Exception ex)` bloklarını P2.6 kuralına göre düzeltin. `OyunController` için de aynısı.

### P5.4 [DÜŞÜK] `SiparisController.DurumGuncelle` (`/siparis/durum-guncelle`)
- `RequireStaff` ile korunuyor ve herhangi bir hedef durumu kabul ediyor.
- **Düzeltme:**
  - View/JS'te kullanılıyor mu kontrol edin: `grep -rn "durum-guncelle" src/QRMenu.Web`.
  - Kullanılmıyorsa silin. Kullanılıyorsa P2.1'deki gibi kullanıcının rolüne göre beyaz liste uygulayın (`User.IsInRole`).

---

## P6: Repo hijyeni, bağımlılıklar, CI/CD, dokümantasyon

**Sahip olduğu dosyalar:**
- `.github/workflows/*`
- `.gitignore`
- `*.csproj`
- `README.md`
- yeni `Dockerfile` ve `docker-compose.yml`
- `.github/dependabot.yml`

**Bağımlılık:** Yok. `README.md` güncellemesini diğer paketler bittikten sonra son haline getirin.

### P6.1 CI: PR'larda build ve test
- **Yeni dosya `.github/workflows/ci.yml`:**
  - Tetikleyiciler: `pull_request` ve `push` (main hariç tüm branch'ler).
  - Adımlar:
    1. `actions/checkout@v4`
    2. `actions/setup-dotnet@v4` (`dotnet-version: 8.0.x`)
    3. `dotnet restore`
    4. `dotnet build --no-restore -c Release`
    5. `dotnet test --no-build -c Release --collect:"XPlat Code Coverage"`
- **`deploy.yml`:**
  - `test` ve `deploy` job'larını ayırın; deploy job'una `needs: test` ekleyin.
  - Action sürümlerini güncelleyin (`checkout@v4`, `setup-dotnet@v4`, `appleboy/ssh-action` ve `scp-action` için güncel sürüm).

### P6.2 Deploy güvenliği (yayına almadan önce, düşük öncelik)
- `deploy.yml`:
  - **SSH:** parola (`VDS_PASSWORD`) yerine SSH anahtarı (`key: ${{ secrets.VDS_SSH_KEY }}`) kullanın.
  - **Connection string:** artık `appsettings.json`'a yazılmasın. Sunucuda ortam değişkeni veya systemd `Environment=` kullanılsın. Workflow'daki ilgili adımı kaldırın ve README'de belgeleyin.
  - **Yükleme klasörü:** `unzip -o` öncesinde sunucudaki `wwwroot/uploads` ve sunucu ayarları korunmalı. Yükleme klasörünü uygulama dizini dışına alın (ör. `/var/qrmenu/uploads`) ve yolunu yapılandırmadan okuyun. Bu değişiklik koda da dokunur, P3.9 ile koordine edin.
  - **Script yolu:** `find / -name project-manager.sh` ve `bash -i` yerine sabit yol veya `systemctl restart qrmenu` kullanılsın.
  - **Health check:** basit bir health check endpoint'i (`app.MapHealthChecks("/health")`) ve deploy sonrası `curl -f` adımı ekleyin. Health check'i `Program.cs`'e eklemek P4 ile çakışır; P4 bittikten sonra yapın.

### P6.3 Git'te olmaması gerekenler
- `src/QRMenu.Web/wwwroot/uploads/` klasörünü `.gitignore`'a ekleyin.
- Mevcut dosyaları git'ten kaldırın (diskte kalsın): `git rm -r --cached src/QRMenu.Web/wwwroot/uploads`.
  > Bu görseller seed verisi olarak gerekiyorsa `src/QRMenu.Web/SeedImages/` gibi ayrı bir klasöre taşıyın ve seed kodunu oraya yönlendirin. Önce seed kodunun görsel yollarını kontrol edin.
- `Properties/PublishProfiles/FolderProfile.pubxml`: içinde kişisel yol varsa `.gitignore`'a alın.
- Migration `20260429162000_AddGunSonuRapor.cs` için `.Designer.cs` yok:
  - `dotnet ef migrations list` ile durumu kontrol edin;
  - migration'ı silip yeniden oluşturmayın (DB'ye uygulanmış olabilir);
  - eksik dosyayı sadece **rapor edin**.

### P6.4 NuGet paketleri
- EF Core, Identity ve Npgsql paketlerini en son **8.0.x** yamasına yükseltin; major sürüm değiştirmeyin.
- `Serilog.Sinks.File` sürümleri tutarsız: Data'da 5.0.0, Web'de 7.0.0. İkisini de eşitleyin.
- Diğer güncellemeler:
  - `itext` → 9.8.x
  - `QRCoder` → 1.8.x (API değişikliği varsa uyarlayın)
  - `BCrypt.Net-Next` → 4.2.x (kullanılmıyorsa kaldırın; Identity hash'leme kullanılıyor)
- Test paketleri:
  - `xunit` → 2.9.x
  - `xunit.runner.visualstudio` uyumlu sürüm
  - `Microsoft.NET.Test.Sdk` → 17.12+
  - `Moq` → 4.20.72+
  - `coverlet.collector` → güncel
- `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` yalnızca Debug'da gerekli: `Condition="'$(Configuration)'=='Debug'"` ekleyin ve `Program.cs`'teki kullanımı `#if DEBUG` veya `IsDevelopment` ile sınırlayın (Program.cs P4'e ait, koordine edin).
- `.github/dependabot.yml` ekleyin: `nuget` ve `github-actions` için haftalık.

### P6.5 Docker
- `Dockerfile`: multi-stage, `mcr.microsoft.com/dotnet/sdk:8.0` → `aspnet:8.0`, non-root kullanıcı.
- `docker-compose.yml`:
  - `postgres:16` + uygulama servisi;
  - connection string ortam değişkeniyle;
  - `uploads` için volume;
  - `.env.example` dosyası.
- README'ye "Docker ile çalıştırma" bölümü ekleyin.

### P6.6 README'yi kodla eşitleyin
- README'de geçen ama kodda olmayan veya farklı adlandırılmış şeyleri düzeltin:
  - Repository/UnitOfWork deseni → yok; servis katmanı var;
  - `AppDbContext` → gerçek adı `QRMenuDbContext`;
  - `OrderHub` → gerçek adı `MenuHub`;
  - "Google Translation API" → yok; `OptionLocalization` var;
  - test sayısı → güncel sayıyı yazın.
- Yeni kurulum adımlarını ekleyin: user-secrets, CLI ile admin oluşturma (P4.1), Docker.

---

## P7: Mimari iyileştirmeler (en son, isteğe bağlı)

**Bağımlılık:** P1-P5 bitmiş olmalı.

### P7.1 `AdminController`'ı bölün (L)
- 2300+ satır ve DbContext'e doğrudan erişiyor. Alan bazlı controller'lara ayırın:
  - `AdminUrunController`
  - `AdminMasaController`
  - `AdminKullaniciController`
  - `AdminRaporController`
  - `AdminHappyHourController`
- Route'ları **değiştirmeyin**; `[Route]` ile mevcut URL'leri koruyun ki view'lar bozulmasın.
- Sorguları servislere taşıyın: `IRaporService`, `IHappyHourService`, `IKullaniciService`, `IMasaService` (arayüzler `QRMenu.Core/Interfaces`, implementasyonlar `QRMenu.Data/Services`).
- Taşınan mantık için test yazın: Z raporu, HH ve gün sonu.

### P7.2 Inline JS/CSS'i dışarı çıkarın (M)
- Büyük view'lardaki `<script>` bloklarını `wwwroot/js/<sayfa>.js` dosyalarına taşıyın:
  - `Mutfak/Panel` (1393 satır)
  - `Kasa/Odeme` (1012)
  - `Admin/Urunler`
  - `Siparis/Siparislerim`
  - `Menu/Index`
- Razor'dan gelen verileri `data-*` attribute'larıyla veya bir `<script type="application/json">` bloğuyla aktarın.
- Sonrasında CSP'den `'unsafe-inline'` (script) kaldırılabilir.

### P7.3 Entegrasyon testleri (M)
- Yeni proje: `tests/QRMenu.IntegrationTests`, `WebApplicationFactory` + `Testcontainers.PostgreSql` ile.
- Test senaryoları:
  - eşzamanlı çift ödeme (P1.3);
  - eşzamanlı oyun ödülü (P5.2);
  - stok yarışı;
  - rol yetkileri (Mutfak `TamOdendi` yapamaz).
- CI'da Docker mevcut olduğu için `ci.yml`'e eklenebilir.

---

## Gelecek özellikler (yol haritası, düzeltmelerden sonra)

| # | Özellik | Efor | Not |
|---|---|---|---|
| 1 | Çoklu işletme (multi-tenant / SaaS) | L | `Isletme` entity'si, tüm tablolara `IsletmeId`, EF global query filter. En yüksek ticari değer; P7.1 sonrası. |
| 2 | Online ödeme (iyzico / PayTR / Stripe) | M | Masadan ödeme; `OdemeService` üzerine yeni `OdemeTipi`. Kasa yükünü azaltır. |
| 3 | PWA + çok dilli menü | M | Manifest + service worker ile menü önbelleği. `GrupEN` gibi EN alanları zaten var; çeviriyi tablo tabanlı genelleştirin. |
| 4 | Stok/reçete takibi ve analitik | M | Stok alanı mevcut. Hammadde düşümü, kritik stok uyarısı, saatlik satış ısı haritası, en çok satan raporları. |
| 5 | Tailwind build pipeline | S | Play CDN yerine derlenmiş CSS. Performans ve CSP için gerekli. |

---

## Özet kontrol listesi

- [x] P1.1 Ödeme durumları yalnızca OdemeService ile
- [x] P1.2 Sipariş anında sunucuda fiyat hesabı
- [x] P1.3 Çift tahsilat engeli
- [x] P1.4 Ödenebilir kalem durumu doğrulaması
- [x] P1.5 Gün sonu: yeni siparişi engelle, ödemeyi ödeme tarihine göre değerlendir
- [x] P1.6 Stok iadesi tutarlılığı
- [x] P1.7 Kısmi iptal sonrası durum
- [x] P1.8 Manuel siparişte varyasyon (servis)
- [x] P1.9 Opsiyon doğrulama
- [x] P1.10 Sepet satır birleştirme fiyatı
- [x] P1.11 Yuvarlama ve ödeme tipi
- [x] P2.1 Rol bazlı durum beyaz listesi
- [x] P2.2 `UrunIptal` kaldır/servise yönlendir
- [x] P2.3 Manuel siparişte varyasyon (UI)
- [x] P2.4 Kasa günlük ciro
- [x] P2.5 Başarısız tahsilatta log/bildirim yok
- [x] P2.6 `ex.Message` (Garson/Mutfak/Kasa)
- [x] P3.1 HH sepet fiyat güncellemesi
- [x] P3.2 HH doğrulama
- [x] P3.3 Gün sonu kapatma kontrolleri
- [x] P3.4 Panel ciro ve en çok satanlar
- [x] P3.5 OturumTemizleyici HH kapatmasın
- [x] P3.6 OturumTemizleyici açık siparişli oturumu kapatmasın
- [x] P3.7 `/images/urun` anonim
- [x] P3.8 Admin XSS ve `ex.Message`
- [x] P3.9 Görsel magic-byte
- [x] P4.1 Recovery endpoint'ini kaldır, CLI ekle
- [x] P4.2 SignalR masa grubu doğrulaması
- [x] P4.3 Token'lı QR
- [x] P4.4 Rate limit
- [x] P4.5 Başlıklar ve cookie'ler
- [x] P4.6 Login mesajı, POST logout, parola politikası
- [x] P4.7 `Html.Raw` attribute
- [x] P4.8 DB SSL ve secret yönetimi
- [x] P5.1 Oyun sonucu sunucuda
- [x] P5.2 Oyun yarışı ve ödenmiş siparişe indirim
- [x] P5.3 `ex.Message` (Siparis/Oyun)
- [x] P5.4 `/siparis/durum-guncelle`
- [x] P6.1 CI workflow
- [x] P6.2 Deploy güvenliği
- [x] P6.3 Uploads git'ten çıkar
- [x] P6.4 Paket güncellemeleri ve Dependabot
- [x] P6.5 Docker
- [x] P6.6 README eşitleme
- [x] P7.1 AdminController bölme
- [x] P7.2 Inline JS dışarı
- [x] P7.3 Entegrasyon testleri
