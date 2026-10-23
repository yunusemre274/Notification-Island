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
| `Views/Controls/` | Bildirim, bağlantı, sistem, medya, ayar ve asistan görünümleri |
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

`IsExpanded`, `IsSettingsOpen` ve `IsAssistantOpen` yalnızca view model’de değiştirilir. Ayar ve asistan aynı anda açık kalamaz; daralma ikisini de kapatır. Panel boyutu, içerik görünürlüğü ve komutlar aynı durumdan türetilir. Önceki global `TopBarStateController` ve görünümdeki paralel mod alanları kaldırılmıştır.

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

`AllowsTransparency` katmanlı pencere yaklaşımı yerine normal WPF HWND ve `WindowChrome` kullanılır. Desteklenen Windows 11 sürümlerinde DWM materyali istenir. Böylece WPF `BlurEffect` ile arayüzün kendisi bulanıklaştırılmaz. Windows desteklemiyorsa `ThemeManager` tint’i tamamen opaklaştırır. Köşeler native region ile kırpılır. Özel region ile DWM gölgesi/materyalinin birlikte görünümü Windows QA gerektirir.

Pencere içeriğe göre boyutlanır; compact durumda ekranın büyük şeffaf bir alanını kaplayıp tıklamaları yakalayan 750×500 pencere yoktur. İki kolonlu dashboard, 460 DIP varsayılan toplam yükseklik sınırı ve scroll kullanır. Bildirim listesi sanallaştırılır. Konum birincil çalışma alanının üst ortasına göre hesaplanır, offset uygulanır ve ekrana sığdırılır. Tam monitör seçici henüz yoktur.

## Ayarlar ve genişletme noktaları

Yeni `Appearance` alanı eski JSON dosyalarında otomatik varsayılan değer alır; sayısal alanlar setter’da sınırlanır. Kaydetme UI tarafında 400 ms debounce edilir; geçici dosya üzerine yazılıp taşınır. Hata kullanıcıya durum mesajı olarak döner. Bozuk ayar dosyası `.invalid-{timestamp}.json` adıyla yedeklenir; sonra varsayılanlara dönülür. Yedek alınamıyorsa özgün dosyayı korumak için kaydetme reddedilir. Testler ayrı bir ayar dosyası yolu kullanır.

Yeni kaynaklar `INotificationProvider` ile snapshot sağlayabilir. Router, provider’a özgü olmayan `NotificationItem` kullanır; `NotificationActionHandler`, kaynak uygulama için güvenilir entegrasyon eklenecek noktadır. İleride uygulama destekli deep link handler’ları eklenebilir; bildirim metninden komut veya link çıkarılıp çalıştırılmaz.

Mevcut AI/SmartEvent/Wi-Fi/Bluetooth/Spotify yardımcı sınıflarının kullanılabilir kaynak kodları korunmuştur. Eski görev/masaüstü görünürlük modu, eski kontrol popup’ları ve mock notification servisi yeni kabuktan kaldırılmıştır. Yeni görev runtime’ı model metnini terminale çevirmez; kayıtlı, yetkilendirilmiş ve doğrulanan dosya/alarm araçlarını kullanır. Eski AI prototipleri kaynak olarak korunur, üretim derlemesinden çıkarılır. Bu geçiş eski görev giriş akışını kasıtlı olarak emekliye ayırır.

Windows bildirim dağıtımı ve sınırlamalar: [docs/NOTIFICATIONS.md](docs/NOTIFICATIONS.md). Doğrulama: [docs/WINDOWS-VALIDATION.md](docs/WINDOWS-VALIDATION.md).

## Yerel ajan ve kaynak bütçesi eki

Görev döngüsü artık [AGENT_ARCHITECTURE.md](AGENT_ARCHITECTURE.md) kapsamındadır. Alarm servisi UI/LLM’den bağımsızdır. Expanded içerik DataTemplate ile ertelenir, QuickControls açılış/kapanış yaşam döngüsünde hazırlanıp bırakılır. Bellek bütçesi ve gerçek ölçüm ayrımı [PERFORMANCE_BUDGET.md](PERFORMANCE_BUDGET.md) ile [BENCHMARKS.md](BENCHMARKS.md) içindedir. Önceki sürümün eagerly-created görünümlerine ve yalnızca sohbet eden asistana ilişkin açıklamalar bu ek ile güncellenmiştir.
