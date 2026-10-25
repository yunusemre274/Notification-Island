# Notification Island

Windows 10/11 için C# / .NET 8 / WPF masaüstü kontrol ve bildirim barı. Arayüz; koyu mavi-gri cam yüzeyler, native Acrylic, Fluent ikonlar ve kaydırma gerektirmeyen iki kolonlu bir kontrol merkezi kullanır. MVVM, Windows medya oturumları ve yerel Ollama entegrasyonu içerir.

## Yerel ajan ve kalıcı alarmlar

Yeni ajan gerçek `/api/chat` tool-calling döngüsü kullanır; model metni komut olarak yürütülmez. Doğrulanmış araç sonuçları görev durumunu belirler. Masaüstü/İndirilenler üzerinde geri alınabilir toplu dosya düzenleme, kalıcı alarm ve tekrar kuralları, medya duraklatma, ses yüzdesi ve RAM bilgisi desteklenir. Dosya/saat kapsamı belirsizse işlem uydurulmaz.

Örnekler:

- “Masaüstümü düzenle; belgeleri, görselleri ve arşivleri ayır.”
- “İndirilenler klasöründeki PDF dosyalarını ayrı klasöre taşı.”
- “Az önce yaptığın düzenlemeyi geri al.”
- “Yarın sabah 08.30’a alarm kur.” / “Bu alarmı hafta içi tekrarla.”
- “Spotify’ı durdur, sesi yüzde 20’ye indir ve 45 dakikalık sayaç başlat.”
- “RAM kullanımını göster.”

Asistan ikonunda görev durumu, gerçek ilerleme, iptal, belirli plana bağlı onay, ayrıntılar ve geri alma bulunur. Yeni bildirim görev durumunu silmez. **Alarm girişi Ollama olmadan da çalışır.** Alarmlar Windows Görev Zamanlayıcı’ya kaydedilir; uygulama kapalıyken bilinen executable’ın alarm helper kolu kullanılır. Kilit ekranı/ses/uyku davranışı garanti edilmez.

Çekirdek gerçek geçici dosya ve fake OS scheduler testleriyle doğrulandı; Windows adapter’larının çalıştırılması ve gerçek Ollama model başarısı bu macOS ortamında **doğrulanmadı**. Yerel sunucu/model otomatik indirilmez veya başlatılmaz. Ayarlarda yerel kurulu ve `tools` capability’si bildiren model seçilmelidir.

Normal bellek hedefi **20.000.000 bayt toplam uygulama süreç ailesi Private Bytes** olarak korunur. Bütçeye tüm paneller, alarm helper’ları ve normal özellikler dahildir. Bu hedef, %0,2 idle CPU ve AI sonrası 30 saniyelik toparlanma henüz ölçülüp sağlanmış değildir. [Kaynak sözleşmesi](PERFORMANCE_BUDGET.md), [ölçüm durumu](BENCHMARKS.md), [ajan mimarisi](AGENT_ARCHITECTURE.md), [teslimat tablosu](ROADMAP_UPDATE.md).

## Yeni görünüm

- **Compact:** Varsayılan genişlik 460 DIP, içerik yüksekliği 46 DIP. Tek cam kapsül; solda saat, ortada bildirim/medya/görev özeti, sağda görev girişi ve genişletme. Kapsül yarıçapı yüksekliğe göre hesaplanır; native çerçeve ve WPF kırpması aynı sınırı kullanır.
- **Inline görev:** Robot düğmesi compact barın ortasında metin girişini açar; Enter mevcut yerel ajanı çalıştırır, Escape girişi kapatır. Büyük bir AI paneli yoktur. İşlem ilerlemesi compact özette, plan onayı/iptal/geri alma kontrol merkezindeki küçük görev kartındadır.
- **Expanded:** Varsayılan genişlik 760 DIP. Sabit 312 DIP içerik gridinde solda bildirimler ve gerektiğinde görev kartı; sağda bağlantılar, ses/parlaklık, CPU/GPU/RAM ve medya bulunur. Panelde scrollbar yoktur. Son 40 bildirim üçlü sayfalarla, görev kartı açıkken tekli sayfalarla erişilebilir. Ayarlar dört sekmeye ayrılır. Eski 420 DIP altındaki yükseklik sınırları okunabilir minimuma yükseltilir.
- **Medya:** Spotify/YouTube kaynak kimliği veya açık YouTube metadata’sı varsa vektör logo gösterilir. Salt Chrome/Edge kimliği YouTube anlamına gelmez; tanınmayan kaynaklarda generic simge vardır. Dokuz çizgili dekoratif waveform 20 FPS render-transform animasyonu kullanır; pause, gizlenme ve azaltılmış harekette animasyon saatleri kaldırılır. Gerçek ses spektrumu ölçülmez.
- **Cam:** DarkGlass, DarkerGlass ve BlueGlass; native Acrylic/Mica, yarı saydam tint, ince kenarlık ve iç ışık yansıması. Görsel ağacı bulanıklaştıran `BlurEffect`, sürekli render callback’i ve animasyonlu gölge yoktur. Açılma opacity/transform ile, metrik çizgileri 280 ms geçişle değişir.
- **Kapanma politikası:** Panel ok düğmesi, Escape veya bir bildirimin kaynak uygulamasını açma eylemiyle kapanır. Hover çıkışı, dışarı tıklama ve odak kaybı paneli kapatmaz. Eski `AutoCollapse` alanı JSON uyumluluğu için korunur; kapanma davranışında kullanılmaz. İsteğe bağlı hover ile açılma, görev girişi açıkken devreye girmez.
- **Hata kaydı:** Çalıştırma betiği startup/dispatcher hata ayrıntılarını `artifacts/logs/crash.log` dosyasına yönlendirir ve başarısız çıkışta yeni kaydı gösterir. EXE doğrudan açılırsa varsayılan günlük `%LOCALAPPDATA%/NotificationIsland/logs/crash.log` konumundadır. Bozulmuş UI durumunda art arda hata penceresi açmak yerine kontrollü kapanma uygulanır.
- **Kişiselleştirme:** Compact öğeler, expanded bölümler, opaklık, materyal, panel köşeleri, boyutlar, konum, her zaman üstte, hover ile açılma ve azaltılmış hareket. Ayarlar otomatik kaydedilir.

Repository’de bulunan iki küçük PNG eski ses/Wi-Fi ikonlarıdır; yeni arayüz bunları kullanmaz. Genel ikonlar Windows 11’de Segoe Fluent Icons, Windows 10’da Segoe MDL2 Assets üzerinden; medya marka işaretleri çözünürlükten bağımsız vektörlerle çizilir.

## Derleme ve çalıştırma

Windows 10 **2004 / build 19041** veya üzeri, .NET 8 Desktop Runtime ve .NET SDK gerekir. Visual Studio 2022’de .NET desktop development iş yükü de kullanılabilir.

### Projeye özel kurulum ve günlük çalıştırma

Windows x64 üzerinde, proje klasöründe PowerShell açıp ilk kurulum için:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Setup.ps1
```

Bu betik Microsoft’un resmi `dotnet-install.ps1` betiğiyle .NET 8 SDK’yı (WPF Desktop Runtime dahil) `artifacts/dotnet/` içine kurar. `dotnet restore NI.sln` ile `NI.csproj` içindeki **NAudio 2.2.1**, **System.Management 10.0.0**, **System.Diagnostics.PerformanceCounter 8.0.1** ve bunların dolaylı bağımlılıklarını `artifacts/nuget/` içine indirir; ardından Release derlemesi yapar. İlk kurulum için Microsoft indirme sunucularına ve NuGet’e internet erişimi gerekir. Yönetici yetkisi gerekmez. Betikler gerekli ortam değişkenlerini çalıştıkları işlemde ayarlar; kalıcı sistem PATH ayarını değiştirmez.

Sonraki her açılışta aynı proje klasöründe:

**Kolay yol:** proje klasöründeki `Baslat.cmd` dosyasına çift tıklayın. PowerShell alternatifi:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start.ps1
```

Çalıştırma betiği yerel SDK’yı seçer, `dotnet build NI.csproj --configuration Release --no-restore` ile değişen kodu derler ve `dotnet run --project NI.csproj --configuration Release --no-build --no-restore` ile açar. Günlük açılış NuGet ağına başvurmaz; SDK’yı tekrar kurmaz. Bağımlılıklar değişirse kurulum betiğini yeniden çalıştırın. SDK ve NuGet önbelleği Git tarafından yok sayılan `artifacts/` altında tutulur; bu klasör silinirse kurulum gerekir. `ExecutionPolicy Bypass` yalnızca başlatılan PowerShell işlemi için geçerlidir.

Aynı Windows masaüstünde tekrar başlatma mevcut pencereyi gösterir. Tek örnek kilidi Windows masaüstüne özeldir; otomasyon/testin izole masaüstündeki bir süreç kullanıcının görünür masaüstünde açılışı engellemez.

Yerel kurulumdan sonra PowerShell üzerinden testleri çalıştırmak için:

```powershell
$env:DOTNET_ROOT = "$PWD\artifacts\dotnet"
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:DOTNET_CLI_HOME = "$PWD\artifacts\dotnet-home"
$env:NUGET_PACKAGES = "$PWD\artifacts\nuget\packages"
$env:NUGET_HTTP_CACHE_PATH = "$PWD\artifacts\nuget\http-cache"
dotnet run --project Tests/NI.Core.Tests.csproj
dotnet run --project Tests/Agent/NI.Agent.Tests.csproj
dotnet run --project Tests/Windows/NI.LayoutChecks.csproj
```

**Ollama isteğe bağlıdır:** temel arayüzü çalıştırmak için gerekmez. Yapay zekâ asistanı için ayrıca yerel Ollama sunucusu ve araç çağırmayı (`tools`) destekleyen kurulu bir model gerekir; kurulum betiği bunları indirmez. **Diğer uygulamaların Windows bildirimleri için** aşağıdaki MSIX kurulumu ve kullanıcı izni ayrıca gereklidir.

### Sistemde .NET SDK zaten kuruluysa

```powershell
dotnet build NI.sln -c Release
dotnet run --project NI.csproj
```

EXE olarak çalıştırıldığında kontrol merkezi, donanım kontrolleri, ölçümler ve medya kullanılabilir. **Diğer uygulamaların gerçek Windows bildirimlerini okumak için paket kimliği, manifest capability ve Windows kullanıcı izni gerekir.** Paketlenmemiş uygulama bunu arayüzde açıkça belirtir; örnek bildirim üretmez. [Microsoft bildirim dinleyicisi belgesi](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener)

## MSIX ve gerçek bildirimler

Paket manifesti [Packaging/Package.appxmanifest](Packaging/Package.appxmanifest), self-contained paket üreticisi [scripts/Build-Msix.ps1](scripts/Build-Msix.ps1) içindedir. Script `dotnet publish`, Windows SDK `MakeAppx` ve isteğe bağlı `SignTool` kullanır. x64 ve arm64 desteklenir.

```powershell
# Windows SDK ve .NET SDK kurulu Windows makinesinde:
.\scripts\Build-Msix.ps1 -Architecture x64 -Publisher 'CN=NotificationIsland' -CertificateThumbprint '<sertifika-thumbprint>'
Add-AppxPackage .\artifacts\NotificationIsland-x64.msix
```

Sertifikanın Subject değeri Publisher ile eşleşmeli ve hedef makinede güvenilir olmalıdır. Script sertifika oluşturmaz veya sisteme güven kaydı eklemez. Thumbprint verilmezse kurulamayan **imzasız** paket üretir; imzalama sonradan yapılabilir. Dağıtım bilgileri: [docs/NOTIFICATIONS.md](docs/NOTIFICATIONS.md).

Kurulan uygulamayı Başlat menüsünden açın, kontrol merkezinde **Bildirimleri bağla** düğmesine basın ve Windows izin penceresini yanıtlayın. Erişim reddedilmişse düğme ilgili Windows gizlilik ayarını açar. Uygulama tekrar etkinleştiğinde izin denetlenir.

## Bildirim davranışı

`WindowsNotificationProvider → NotificationRouter → IslandVM → görünüm` akışı kullanılır. Son 40 Windows bildirimi bellekte tutulur; uygulama adı/ikonu, başlık, açıklama, zaman ve yerel okunma durumu gösterilir. Windows’tan kaldırılan bildirim listeden düşer. Başlangıç geçmişi preview tetiklemez. Yeni bildirim compact içerikte **9 saniye** öncelik kazanır, ardından medya/idle içerik geri gelir. Bildirimler diske yazılmaz.

Tıklamada önce uygulamanın mevcut penceresi öne getirilmeye çalışılır; ardından kayıtlı uygulama aktivasyonu ve AppsFolder başlatması denenir. Bunlar uygulamayı açar; her uygulamada ilgili sohbeti/sekmesini açma garantisi vermez. Genel listener API’si başka uygulamanın özgün toast düğmesini veya activation payload’ını tekrar çalıştırma metodu sağlamaz. Özgün eylemler Windows Bildirim Merkezi’nde kalır (Windows 11: Win+N, Windows 10: Win+A). [UserNotificationListener API](https://learn.microsoft.com/en-us/uwp/api/windows.ui.notifications.management.usernotificationlistener)

WhatsApp, Chrome ve diğer uygulamalar **Windows toast** üretiyorlarsa aynı provider üzerinden görünür. Uygulama içi popup’lar, devre dışı bildirimler veya Windows’un paylaşmadığı sistem uyarıları yakalanamaz. “Tüm bildirimleri okundu işaretle” yalnızca Island’ın bellekteki durumunu değiştirir.

## Donanım, medya ve performans

| Alan | Uygulanan yaklaşım |
|---|---|
| CPU | `GetSystemTimes` delta ölçümü; ilk örnek hazır değilse `—` |
| RAM | `GlobalMemoryStatusEx`; gerçek kullanılan/toplam GB |
| GPU | WDDM `GPU Engine / Utilization Percentage`; fiziksel 3D motor grupları içinde en yoğun motor; destek yoksa `—` |
| Ölçüm sıklığı | Panel açıkken yaklaşık 1,5 saniye; kapalıyken/ayarlar açıkken durur |
| Wi-Fi / Bluetooth | Windows Radio API; gerçek durum olayları; erişilemiyorsa ilgili Windows ayarı |
| Uçak modu | Windows’un uçak modu ayarını açar; sahte toggle uygulanmaz |
| Ses | Core Audio / NAudio; dışarıdan değişen ses ve varsayılan çıkış aygıtı olayları |
| Parlaklık | Dahili ekran WMI veya birincil monitör DDC/CI; destek yoksa slider pasif; yazmalar 200 ms bekletilir |
| Medya | Windows’un güncel GSMTC oturumu; Spotify dahil; kapak, süre, önceki/sonraki/oynat/duraklat |
| Medya zamanlayıcısı | Yalnızca medya bölümü görünürken ve oynatma sürerken 1 Hz |
| Saat | Bir sonraki dakika sınırına ayarlı tek UI zamanlayıcısı |

WPF donanım hızlandırması açık bırakılır. Sürekli `BlurEffect`, animasyonlu gölge veya render döngüsü yoktur. Donanımda ölçülmüş CPU/RAM/FPS garantisi verilmez; [Windows kontrol listesi](docs/WINDOWS-VALIDATION.md) ile profil alınmalıdır.

## Acrylic / Mica sınırları

Windows 11 **22621+** üzerinde belgelenmiş DWM system backdrop seçimi kullanılır: None, Mica veya Acrylic. Mica, duvar kâğıdını temel alan opak bir materyaldir; Acrylic masaüstü bulanıklığı verir. Sistem saydamlık politikaları görsel sonucu değiştirebilir. [DWM materyal seçenekleri](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type)

Windows 10, eski Windows 11 sürümleri, yüksek karşıtlık veya başarısız DWM çağrısında opak, okunabilir yüzey kullanılır. Opaklık kontrolü native backdrop etkin olduğunda tint yüzeyini değiştirir; metni soldurmaz. Windows bu API’de sayısal blur yarıçapı sunmaz, bu nedenle çalışmayan bir “blur yoğunluğu” slider’ı eklenmedi. Özel köşe yarıçapı window region ile uygulanır; native gölge görünümü compositor’a göre değişebilir. Native efektlerin Windows’ta görsel doğrulaması henüz gereklidir.

## Ayarlar ve yerel asistan

Ayar dosyası: `%LOCALAPPDATA%\NotificationIsland\settings.json`. Eski alanlar korunur, eksik `Appearance` varsayılanlarla tamamlanır; boyut ve opaklık sınırlandırılır. Yazma önce geçici dosyaya, sonra atomik replace ile yapılır. Bildirim geçmişi ve okunma durumu uygulama kapanınca silinir.

Robot düğmesi compact görev girişini açar. Sunucu adresi ve model ayarların Asistan sekmesinden değiştirilebilir; görevler seçilen modeli kullanır. Ollama otomatik başlatılmaz; yerel sunucuyu ve `tools` destekli modeli önceden hazırlayın. Alarmlar sekmesindeki doğrudan alarm girişi Ollama gerektirmez. Önceki AI prototiplerinin kaynakları korunmuş, `NI.csproj` üzerinden üretim derlemesinden çıkarılmıştır. Yeni runtime yalnızca kayıtlı ve yetkilendirilmiş araçları yürütür.

Hava durumu varsayılan kapalıdır. Etkinleştirip enlem/boylam girildiğinde sıcaklık Open-Meteo’dan 30 dakikada bir alınır. Koordinatlar yalnızca bu istek için gönderilir; otomatik konum sorgusu yapılmaz. Bağlantı yoksa sıcaklık uydurulmaz. [Open-Meteo API ve kullanım koşulları](https://open-meteo.com/en/docs) (ücretsiz uç noktanın ticari kullanım uygunluğu dağıtımdan önce değerlendirilmelidir).

## Doğrulama

Platformdan bağımsız üretim mantığı kontrolleri (gerçek kullanıcı masaüstüne dokunmaz):

```powershell
dotnet run --project Tests/NI.Core.Tests.csproj
dotnet run --project Tests/Agent/NI.Agent.Tests.csproj
```

Windows üzerinde gerçek WPF resource/binding ve görünüm sınırı kontrolü:

```powershell
dotnet run --project Tests/Windows/NI.LayoutChecks.csproj
```

Windows kontrolü izole fixture’larla gerçek `MainWindow` açar; compact/expanded, inline görev, kaynak logoları, tüm ayar sekmeleri, dar boyutlar, görev onayı ve N/A/100% metriklerini render eder. PNG ve binding günlüğünü `artifacts/layout-checks/` altına yazar. Native kapsül region’ı, giriş/focus/Enter/Escape, kapanma politikası, bildirim sayfalaması ve animasyon saatlerinin durması da denetlenir. Gerçek Ollama modeli, Windows bildirim izinleri ve fiziksel donanım kontrolleri ayrı canlı doğrulama gerektirir; üretime örnek veri eklenmez.

Bu değişiklikler macOS ortamında çapraz derlenmiştir. Windows runtime, MSIX kurulum/izinleri, native materyal, donanım ve çoklu DPI kabul kontrolleri ayrıca [docs/WINDOWS-VALIDATION.md](docs/WINDOWS-VALIDATION.md) üzerinden yapılmalıdır. Mimari: [ARCHITECTURE.md](ARCHITECTURE.md).
