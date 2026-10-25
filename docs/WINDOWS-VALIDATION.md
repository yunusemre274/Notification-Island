# Windows kabul ve performans kontrolü

## Bu ortamda doğrulananlar

Windows x64 üzerinde Release derlemesi, çekirdek kontrolleri, izole dosya/alarm ajan testleri ve gerçek WPF pencere/render kontrolleri çalıştırılır. Testler gerçek Ollama modeli, Windows bildirim izni ve fiziksel donanım komutları için başarı iddiası değildir.

```powershell
dotnet build NI.sln -c Release
dotnet run --project Tests/NI.Core.Tests.csproj
```

Core kontrol programı üretimdeki `NotificationRouter`, `NotificationItem`, `AppearanceSettings` ve `AppSettings` kaynaklarını kullanır. Başlangıç/burst/duplicate/silinen bildirimler, okunma durumu, kapasite, aktivasyon sonucu, 500 snapshot permütasyonu, eski ayarların yüklenmesi, geçersiz değerler, JSON round trip, atomik kaydetme ve bozuk dosya yedeğini kapsar. Dosya kontrolleri geçici dizin kullanır.

## Windows’ta görünüm kontrolü

```powershell
dotnet run --project Tests/Windows/NI.LayoutChecks.csproj
```

Bu program üretim `MainWindow`, resource dictionary’leri ve `IslandView` bileşenini izole servislerle açar. Spotify/YouTube/fallback, playing/paused waveform, inline görev girişi, boş/dolu dashboard, N/A/100% metrikler, görev onayı/sonucu, dört ayar sekmesi ve dar boyutlar için PNG üretir. Gerçek native kapsül region’ı ve WPF clip geometrisi, genişlik değişimi, Enter/Escape/focus, hover/odak kaybında açık kalma, 40 bildirimin sayfalardan erişilebilirliği, görünür scrollbar olmaması, metinlerin sığması, animasyon saatlerinin durması ve monitor/timeline etkinliği denetlenir. Fixture’lar yalnızca QA executable’ındadır; kullanıcı ayarları ve gerçek masaüstü dosyaları değiştirilmez. Test ayarları, boyutlar ve binding günlüğü `artifacts/layout-checks/` altındadır. Yönetilen test hataları konsola yazılır; modal hata penceresi açılmaz.

PNG’ler WPF yüzeyini render eder. Native Acrylic/Mica’nın masaüstüyle birleşmiş piksel görüntüsü bu PNG’lerde doğrulanmaz; kısıtlı araç oturumunda masaüstü DC capture kullanılamaz. Native region geometrisi ayrıca kontrol edilir; materyalin gerçek masaüstündeki görünümü elle değerlendirilmelidir. Bu program macOS üzerinde çalıştırılamaz.

## Elle kabul kontrolü

| Senaryo | Beklenen sonuç |
|---|---|
| Compact idle | Saat solda; robot düğmesi inline input açar; pencere yalnızca kapsül alanını kaplar |
| Yeni toast | Başlık/uygulama compact’ta 9 saniye; yeni kayıt expanded’da; geçmiş açılışta preview tetiklemez |
| Aynı bildirim snapshot’ı | Duplicate kart/preview oluşmaz; yerel okunma durumu korunur |
| Toast tıklama | Uygulama foreground/launch denenir; özgün konuşma/action garantisi verilmez |
| Windows’tan kaldırma | Kaydın Island’dan düşmesi |
| Paketlenmemiş EXE | MSIX gereksinimi görünür; sahte bildirim yok |
| İzin reddi ve sonradan iptali | Doğru durum mesajı; ayarlara yönlendirme; izin kaybında listenin temizlenmesi |
| WhatsApp / Chrome | Windows native toast açıkken gerçek kaynak ikonu, metin ve uygulama yönlendirmesi |
| Çok sayıda bildirim | En fazla 40 kayıt; üçlü sayfalar, görev kartı açıkken tekli sayfalar; scrollbar yok |
| Wi-Fi / Bluetooth dışarıdan değişim | Gerçek radio olaylarının düğme durumunu güncellemesi |
| Uçak modu | Windows’un gerçek ayar sayfası; sahte state değiştirme yok |
| Ses dışarıdan değişim / aygıt takma | Ses seviyesi ve varsayılan endpoint’in güncellenmesi |
| Dahili ekran / DDC/CI ekran / desteksiz ekran | Destekli ekranda gerçek parlaklık; desteksizde pasif slider |
| Spotify / tarayıcı medyası | Kaynak logosu, başlık/artist, süre ve transport komutları; salt browser ID’de YouTube tahmini yapılmaz |
| Waveform | Playing sırasında dokuz hareketli çubuk; paused/hidden/reduced-motion sırasında animasyon saatleri yok |
| Inline görev | Robot compact input açar; Enter bir görev başlatır; Escape inputu kapatır; büyük AI paneli oluşmaz |
| Görev onayı | Kısa dashboard kartı aynı plan hash’ini onaylar; iptal ve geri alma erişilebilir kalır |
| Oturum kapanması veya parça değişimi | Eski metadata’nın yeni oturumu ezmemesi; timer’ın gereksiz sürmemesi |
| Her bölümün kapatılması | Boş bölümlerin yer kaplamaması; bildirim/sağ kolon alanının yeniden paylaşılması |
| Ayarlar ve yeniden başlatma | Tema/boyut/offset/görünürlük tercihlerinin korunması |
| 100/125/150/200% DPI | Okunaklı ikon/font, doğru native köşeler ve ekran içinde konum |
| Monitör ekleme/çıkarma | Birincil çalışma alanına yeniden sığdırma; çoklu monitör seçici henüz yok |
| Windows 11 22621+ / Windows 10 | Native materyal veya okunabilir opak fallback |
| Sistem saydamlığı kapalı / yüksek karşıtlık | Okunabilir metin ve kontrol durumları; destek dışı blur iddiası yok |
| Klavye | Tab ile komutlara erişim; Enter ile görev; Escape ile inline input/ayar/panel kapatma |
| Azaltılmış hareket | Expand/summary/metric ve waveform animasyonlarının devre dışı kalması |
| Hover, popup ve ayarlar | Panel hover çıkışı veya odak kaybıyla kapanmaz; isteğe bağlı hover açılması input sırasında durur |
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

## Hata kaydı

`scripts/Start.ps1` uygulama hatalarını `artifacts/logs/crash.log` dosyasına yönlendirir; başarısız çıkışta hata kodu ve son kayıtlar konsolda gösterilir. EXE doğrudan açılırsa varsayılan konum `%LOCALAPPDATA%/NotificationIsland/logs/crash.log` olur. Yaklaşık 256 KiB üzerinde önceki kayıt `crash.log.previous` olarak döndürülür. Startup/dispatcher hataları kaydedilir; bozulmuş UI durumunda devam etmek yerine uygulama kontrollü kapanır. Dosya/LLM görevlerinin başarısı yalnızca gerçek araç sonuçlarıyla belirlenir.

Windows ajan testindeki symlink kontrolü Developer Mode/yönetici ayrıcalığı yoksa açıkça `SKIP` raporlar. Bu sonuç link korumasının test edildiği anlamına gelmez. Win32 handle ile taşıma, tam Unicode hedef adı, kimlik koruma, kısmi iptal ve geri alma kontrolleri ayrı çalışır.
