# Windows kabul ve performans kontrolü

## Bu ortamda doğrulananlar

macOS üzerinde Windows target reference assembly’leri kullanılarak WPF projesi çapraz derlenebilir. Bu, XAML/C# derleme kontrolüdür; Windows DWM, WinRT izinleri veya donanımın çalıştığının kanıtı değildir.

```powershell
dotnet build NI.sln -c Release
dotnet run --project Tests/NI.Core.Tests.csproj
```

Core kontrol programı üretimdeki `NotificationRouter`, `NotificationItem`, `AppearanceSettings` ve `AppSettings` kaynaklarını kullanır. Başlangıç/burst/duplicate/silinen bildirimler, okunma durumu, kapasite, aktivasyon sonucu, 500 snapshot permütasyonu, eski ayarların yüklenmesi, geçersiz değerler, JSON round trip, atomik kaydetme ve bozuk dosya yedeğini kapsar. Dosya kontrolleri geçici dizin kullanır.

## Windows’ta görünüm kontrolü

```powershell
dotnet run --project Tests/Windows/NI.LayoutChecks.csproj
```

Bu program gerçek WPF resource dictionary’lerini ve üretim `IslandView` bileşenini açar. Compact, boş/dolu expanded, ayarlar, asistan, mavi tema, dar panel, kısa panel ve compact preview için dokuz PNG üretir. Pencere yüksekliği, compact genişliği, binding uyarıları ve monitor aktif/pasif durumunu kontrol eder. Test fixture’ları yalnızca bu QA executable’ında kullanılır; donanım servisleri başlatılmaz. Test ayarları `artifacts/layout-checks/settings.json` dosyasına yazılır, kullanıcının ayarları değiştirilmez.

PNG’ler WPF yüzeyini render eder. Native Acrylic/Mica masaüstü kompozisyonu bu PNG’lerde doğrulanmaz. Çıktılar elle incelenmeli; satır taşması, metin kırpılması ve scroll ergonomisi değerlendirilmelidir. Bu program macOS üzerinde çalıştırılamaz.

## Elle kabul kontrolü

| Senaryo | Beklenen sonuç |
|---|---|
| Compact idle | Saat solda; görev giriş metni yok; pencere yalnızca kapsül alanını kaplar |
| Yeni toast | Başlık/uygulama compact’ta 9 saniye; yeni kayıt expanded’da; geçmiş açılışta preview tetiklemez |
| Aynı bildirim snapshot’ı | Duplicate kart/preview oluşmaz; yerel okunma durumu korunur |
| Toast tıklama | Uygulama foreground/launch denenir; özgün konuşma/action garantisi verilmez |
| Windows’tan kaldırma | Kaydın Island’dan düşmesi |
| Paketlenmemiş EXE | MSIX gereksinimi görünür; sahte bildirim yok |
| İzin reddi ve sonradan iptali | Doğru durum mesajı; ayarlara yönlendirme; izin kaybında listenin temizlenmesi |
| WhatsApp / Chrome | Windows native toast açıkken gerçek kaynak ikonu, metin ve uygulama yönlendirmesi |
| Çok sayıda bildirim | En fazla 40 kart; listede scroll; expanded maksimum yükseklik korunur |
| Wi-Fi / Bluetooth dışarıdan değişim | Gerçek radio olaylarının düğme durumunu güncellemesi |
| Uçak modu | Windows’un gerçek ayar sayfası; sahte state değiştirme yok |
| Ses dışarıdan değişim / aygıt takma | Ses seviyesi ve varsayılan endpoint’in güncellenmesi |
| Dahili ekran / DDC/CI ekran / desteksiz ekran | Destekli ekranda gerçek parlaklık; desteksizde pasif slider |
| Spotify / tarayıcı medyası | Güncel Windows oturumu, kapak, başlık, artist, süre ve desteklenen transport komutları |
| Oturum kapanması veya parça değişimi | Eski metadata’nın yeni oturumu ezmemesi; timer’ın gereksiz sürmemesi |
| Her bölümün kapatılması | Boş bölümlerin yer kaplamaması; bildirim/sağ kolon alanının yeniden paylaşılması |
| Ayarlar ve yeniden başlatma | Tema/boyut/offset/görünürlük tercihlerinin korunması |
| 100/125/150/200% DPI | Okunaklı ikon/font, doğru native köşeler ve ekran içinde konum |
| Monitör ekleme/çıkarma | Birincil çalışma alanına yeniden sığdırma; çoklu monitör seçici henüz yok |
| Windows 11 22621+ / Windows 10 | Native materyal veya okunabilir opak fallback |
| Sistem saydamlığı kapalı / yüksek karşıtlık | Okunabilir metin ve kontrol durumları; destek dışı blur iddiası yok |
| Klavye | Tab ile komutlara erişim; Space/Enter ile işlem; Escape ile ayar/asistan/panel kapatma |
| Azaltılmış hareket | Expand/width/summary/metric animasyonlarının devre dışı kalması |
| Hover, popup ve ayarlar | Hover gecikmesi; slider veya combo kullanırken panelin beklenmedik kapanmaması |
| Çıkış ve tekrar açma | Servis aboneliklerinin temizlenmesi; tek uygulama instance’ı |

## Profil alma

Release çıktısını Visual Studio Diagnostic Tools, Windows Performance Recorder veya Process Explorer ile ölçün:

1. Compact idle: 60 saniye, hava durumu kapalı, medya yok. Ortalama CPU, private bytes ve handle sayısını not edin.
2. Expanded: 60 saniye, CPU/GPU/RAM görünür. Yaklaşık 1,5 saniyelik örnekleme ve GPU keşfinin UI thread’ini bloke etmediğini denetleyin.
3. Daraltın: CPU/GPU timer’ının durduğunu ve PDH handle’larının serbest kaldığını doğrulayın.
4. Spotify oynatın: Kapalı panelde timeline polling olmamalı; expanded medya görünürken 1 Hz olmalı.
5. 100 kez aç/kapat, medya oturumu değiştir ve ses cihazı tak/çıkar: handle, timer ve bellek büyümesini kontrol edin.
6. DWM materyalleri açık/kapalı animasyonları karşılaştırın. 60 FPS hedefini gerçek GPU ve DPI üzerinde ölçün; bu repository henüz ölçülmüş bir FPS sonucu iddia etmez.

Native region ile özel köşe yarıçapının DWM gölge ve Acrylic ile birlikte görünümü özellikle kontrol edilmelidir. Bu kombinasyonun davranışı Windows sürümüne göre değişebilir.
