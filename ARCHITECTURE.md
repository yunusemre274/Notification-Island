# Notification Island mimarisi

Yeni kabuk tek bir pencere, tek bir `IslandVM` ve servis sözleşmeleri üzerinden çalışır. Görünüm code-behind’ında bildirim yakalama, sistem ölçümü veya donanım kontrolü bulunmaz.

## Sorumluluklar

| Konum | Sorumluluk |
|---|---|
| `Program.cs`, `MainWindow.xaml.cs` | Composition root, pencere yaşam döngüsü, DPI/konum, hover ve Escape |
| `ViewModels/IslandVM.cs` | Expanded/ayar/asistan durumu, compact içerik önceliği, komutlar, servis abonelikleri |
| `ViewModels/QuickControlsVM.cs` | Gerçek donanım durumunu UI’ya aktarma, parlaklık debounce |
| `Configuration/AppearanceSettings.cs` | Tipli, gözlemlenebilir, sınırları doğrulanan görünüm ayarları |
| `Models/AppSettings.cs` | Geriye uyumlu JSON ayar okuma ve atomik yazma |
| `Models/NotificationItem.cs` | Provider kimliği, kaynak uygulama kimliği, içerik ve yerel okunma durumu |
| `Services/Notifications/` | Windows capture, bounded liste senkronizasyonu, uygulama aktivasyonu |
| `Services/SystemMonitoring/ISystemMonitor.cs` | UI’dan bağımsız ölçüm sözleşmesi ve snapshot |
| `Services/SystemMonitorService.cs` | Native CPU/RAM ve PDH GPU toplayıcısı |
| `Services/Media/IMediaService.cs` | Medya sözleşmesi |
| `Services/MediaSessionService.cs` | GSMTC oturum seçimi, metadata/kapak ve transport kontrolleri |
| `Services/AudioEndpointService.cs` | Core Audio bildirimleri ve varsayılan çıkış aygıtı |
| `Services/RadioService.cs`, `BrightnessService.cs` | Windows donanım adaptörleri |
| `Services/Windowing/` | DWM materyali ve DPI’ya göre native köşeler |
| `Themes/`, `Styles/`, `Controls/` | Renk/font/yüzey token’ları, tema uygulama, reusable ikon ve metric kontrolü |
| `Views/IslandView`, `Views/Controls/` | Compact inline giriş, sayfalanan bildirimler, bağlantı/metrik/medya, sekmeli ayarlar ve küçük görev kartı |
| `Core/IslandMode`, `ViewModels/IslandVM.Presentation` | Tek sunum modu, görünürlük, kapsül köşeleri ve bildirim sayfalaması |
| `Controls/GlassSurface`, `ToggleTile`, `MediaSourceIcon`, `MediaWaveform` | Yuvarlak clipping, bağlantı kartı, vektör marka ve görünürlükle duran waveform |
| `Services/Diagnostics/CrashLog` | Yerel hata günlüğü ve boyuta göre önceki kayıt döndürme |
| `Tests/` | Platformdan bağımsız mantık ve Windows WPF görünüm kontrolleri |
| `Packaging/`, `scripts/` | MSIX manifesti, logo varlıkları ve paketleme |

## Durum ve veri akışı

```text
WindowsNotificationProvider ──snapshot──▶ NotificationRouter ──liste/arrived──▶ IslandVM
                                                                     │
ISystemMonitor ──SystemMetrics────────────────────────────────────────┤
IMediaService ──Changed───────────────────────────────────────────────┤
AppearanceSettings ──PropertyChanged─────────────────────────────────┤
                                                                     ▼
                           IslandView / DashboardPanel / bölüm görünümleri
```

`Core/IslandMode` tek sunum durumudur: `Compact`, `TaskInput`, `Dashboard`, `Settings`. `IslandVM.Presentation.cs` içindeki `SetMode` geçişi boyut, köşeler, görünürlük ve servis etkinliğini aynı durumdan türetir. Robot düğmesi ayrı panel oluşturmadan `TaskInput` moduna geçer. `IslandView` yalnızca görünüm odağını yönetir; Enter mevcut `AskCommand` üzerinden `ILocalAgentCoordinator` kullanır. Testler bu sözleşmeye izole bir görev fixture’ı bağlayabilir. Onay mevcut plan hash’ine bağlı kalır ve küçük dashboard kartında gösterilir.

Compact önceliği: etkin 9 saniyelik yeni bildirim preview’i → mevcut medya oturumu → idle. Kullanıcının kapattığı özetler atlanır; saat ve hava ayrı alanlardır. Başlangıç snapshot’ı listelenir fakat preview oluşturmaz. Gelen bildirim kullanıcının açık ayar/asistan panelini kapatmaz.

`NotificationRouter.Synchronize` ID’ye göre duplicate engeller, snapshot zamanına göre sıralar, 40 kayıt tutar, kaldırılan öğeleri düşürür ve okunma durumunu korur. UI thread’inde çağrılır. Snapshot payload’ı değişirse kart güncellenir. Router yalnızca aktivasyon sonucu kabul edildiğinde yerel okunma durumunu değiştirir. AppsFolder sonucu hedefin eylemi işlediğinin onayı değildir.

## İş parçacıkları ve yaşam döngüsü

- WinRT listener izin isteği UI thread’inde, açık kullanıcı komutuyla yapılır. Snapshot okuma tek semaphore ile sıralanır; uygulama aktifken `NotificationChanged` kullanılır.
- Provider/monitor olayları UI koleksiyonunu doğrudan değiştirmez. `IslandVM` dispatcher üzerinden aktarır ve kapanıştan sonra gelen işleri yok sayar.
- Sistem örnekleyicisi thread pool’da çalışır, üst üste binmez. UI tarafındaki aç/kapat metodu PDH hazırlığını beklemez. CPU/RAM native API kullanır. GPU instance keşfi etkin örnekleme sırasında en fazla 30 saniyede bir yapılır; sayaçlar arada tekrar kullanılır.
- GPU verisi farklı motorları toplamak yerine aynı fiziksel 3D motorun süreç örneklerini toplar ve en yoğun motoru gösterir. Task Manager’ın bütün motorları kapsayan GPU değeriyle birebir aynı metrik değildir.
- Ölçüm panel kapalıyken, ayar/asistan açıkken veya bölüm devre dışıyken durur. GPU sayaçlarını örnekleyici serbest bırakır; UI thread’i pahalı native kaynak temizliğini beklemez.
- GSMTC değişiklikleri dispatcher’a taşınır. Async metadata işlemleri generation kontrolüyle eski oturum bilgisinin yeniyi ezmesini engeller. İlerleme timer’ı yalnızca oynatma sürerken ve medya bölümü görünürken çalışır.
- NAudio cihaz ve volume olayları kullanılır. DDC/WMI parlaklık okuması arka plandadır; yazmalar 200 ms debounce ile sıralanır.
- Kapanış; timer’ları durdurur, abonelikleri kaldırır, HTTP işlerini iptal eder ve servisleri dispose eder. Pencere kapatılmasını engelleyen önceki davranış kaldırılmıştır.

## Tasarım sistemi ve pencere

`Tokens.xaml` başlangıç paletini; `ThemeManager` seçilmiş tint/opacity ve yüksek karşıtlık renklerini; `Controls.xaml` etkileşim, kart ve slider stillerini sağlar. `MetricCard` okunabilir sayı, ince çizgi ve alt açıklamayı tekrar kullanır. Windows ikon fontları aynı outline ailesini kullanır.

Normal, borderless WPF HWND korunur. `WindowChrome` kaldırılmıştır: onun kendi region güncellemeleriyle özel `SetWindowRgn` çağrılarının çakışması genişliği eski değere döndürüyor ve ikinci çerçeve oluşturuyordu. Native region’ın tek sahibi `WindowMaterialService` olur. Boyut VM’den uygulanır; konum ve kırpma layout sonuna ertelenip birleştirilir. Windows 11’de native Acrylic/Mica istenir; destek yoksa `ThemeManager` okunabilir opak yüzey seçer. DWM’nin ikinci native border çizimi `DWMWA_COLOR_NONE` ile bastırılır, OS’nin farklı yarıçaplı otomatik köşe maskesi devreden çıkarılır. Tüm client alana frame genişletilir. `GlassSurface` çocuk görsellerini ve WPF hit alanını gerçek yuvarlak geometriyle kırpar; native region aynı yarıçapı fiziksel pencere boyutları/DPI ile kullanır. Compact yarıçap yükseklik/2’dir; expanded panelin köşeleri kişiselleştirilebilir. DWM gölgesinin görünümü Windows compositor’ına bağlıdır.

Pencere içeriğe göre boyutlanır. Dashboard sabit 312 DIP grid kullanır: solda sayfalanan bildirimler ve koşullu küçük görev kartı; sağda 132 DIP bağlantılar, 76 DIP metrikler ve medya. Listeyi kırpmak veya scrollbar’ı gizlemek yerine bildirim sayfa boyutu görev kartına göre üç/bir olarak değişir; 40 kaydın tamamı erişilebilir kalır. Ayarlar dört sekmeye ayrılır. Açık panel yalnızca ok, Escape veya bildirimi açma ile kapanır; hover çıkışı ve Deactivated kapatma yolu yoktur. Eski `AutoCollapse` ayarı yalnızca JSON uyumluluğu için kalır. Konum birincil çalışma alanının üst ortasına göre hesaplanır. Tam monitör seçici henüz yoktur.

`MediaSourceClassifier` GSMTC `SourceAppUserModelId` ve açık yayıncı metadata’sından Spotify/YouTube sınıfını üretir. Tarayıcı kimliğinden sekme tahmini yapılmaz. `MediaSourceIcon` vektör logo/fallback çizer. `MediaWaveform` dokuz retained visual üzerindeki ScaleY transform saatlerini en fazla 20 FPS ile çalıştırır; pause/gizlenme/unload/reduced-motion saatleri kaldırır. Ses örneklemesi, timer veya `CompositionTarget.Rendering` aboneliği yoktur. Medya zaman çizgisi olayları yalnızca süre/ilerleme özelliklerini günceller; kaynak/başlık/oynatma değişmedikçe compact özet yeniden bildirilmez.

## Ayarlar ve genişletme noktaları

`DesktopInstance` tek-örnek mutex adını mevcut window station ve Win32 desktop kimliğinden üretir. Aynı oturumdaki izole test masaüstü kullanıcının görünür masaüstünü engellemez. İkinci açılış mevcut HWND’ye kayıtlı aktivasyon mesajı gönderir; `MainWindow` gizlenmiş pencereyi gösterir, küçültmeyi kaldırır ve görünür konuma getirir. Odak Windows foreground kurallarına bağlıdır. Pencere henüz oluşmamışsa en fazla 1,5 saniye beklenir; bulunamazsa tanılanabilir hata kaydedilir.

Yeni `Appearance` alanı eski JSON dosyalarında otomatik varsayılan değer alır; sayısal alanlar setter’da sınırlanır. Kaydetme UI tarafında 400 ms debounce edilir; geçici dosya üzerine yazılıp taşınır. Hata kullanıcıya durum mesajı olarak döner. Bozuk ayar dosyası `.invalid-{timestamp}.json` adıyla yedeklenir; sonra varsayılanlara dönülür. Yedek alınamıyorsa özgün dosyayı korumak için kaydetme reddedilir. Testler ayrı bir ayar dosyası yolu kullanır.

Yeni kaynaklar `INotificationProvider` ile snapshot sağlayabilir. Router, provider’a özgü olmayan `NotificationItem` kullanır; `NotificationActionHandler`, kaynak uygulama için güvenilir entegrasyon eklenecek noktadır. İleride uygulama destekli deep link handler’ları eklenebilir; bildirim metninden komut veya link çıkarılıp çalıştırılmaz.

Mevcut AI/SmartEvent/Wi-Fi/Bluetooth/Spotify yardımcı sınıflarının kullanılabilir kaynak kodları korunmuştur. Eski görev/masaüstü görünürlük modu, eski kontrol popup’ları ve mock notification servisi yeni kabuktan kaldırılmıştır. Yeni görev runtime’ı model metnini terminale çevirmez; kayıtlı, yetkilendirilmiş ve doğrulanan dosya/alarm araçlarını kullanır. Eski AI prototipleri kaynak olarak korunur, üretim derlemesinden çıkarılır. Bu geçiş eski görev giriş akışını kasıtlı olarak emekliye ayırır.

Windows bildirim dağıtımı ve sınırlamalar: [docs/NOTIFICATIONS.md](docs/NOTIFICATIONS.md). Doğrulama: [docs/WINDOWS-VALIDATION.md](docs/WINDOWS-VALIDATION.md).

## Yerel ajan ve kaynak bütçesi eki

Görev döngüsü artık [AGENT_ARCHITECTURE.md](AGENT_ARCHITECTURE.md) kapsamındadır. Alarm servisi UI/LLM’den bağımsızdır. Expanded içerik DataTemplate ile ertelenir, QuickControls açılış/kapanış yaşam döngüsünde hazırlanıp bırakılır. Bellek bütçesi ve gerçek ölçüm ayrımı [PERFORMANCE_BUDGET.md](PERFORMANCE_BUDGET.md) ile [BENCHMARKS.md](BENCHMARKS.md) içindedir. Önceki sürümün eagerly-created görünümlerine ve yalnızca sohbet eden asistana ilişkin açıklamalar bu ek ile güncellenmiştir.
