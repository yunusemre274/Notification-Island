# Ölçümler ve doğrulama durumu

## Ortam ve baseline

İncelenen commit: `094c548d4aecbf9701fcf8aa5bcd7d29a57ba8a5`. Çalışma ağacı önceden Liquid Glass değişiklikleriyle dirty idi; korunmuştur. Önceki diff ve untracked dosyaların yedeği geliştirme ortamında `/tmp/notification-island-before-agent*` altında alındı; git geçmişi değiştirilmedi.

Geliştirme ortamı macOS/Darwin arm64, .NET SDK 10.0.301 ve runtime 10.0.9’dur. Ürün hedefi .NET 8 WPF/Windows olarak korundu. Platformdan bağımsız kontroller net8.0 hedefinden bu makinedeki runtime’a `RollForward=Major` ile yürütülür. Windows sürümü, Windows donanımı, DPI ve gerçek ürün render modu burada **ölçülemedi**.

Ollama CLI 0.30.8 kurulu; çalışan yerel sunucu yoktu. Sunucu başlatılmadı, model indirilmedi. Gerçek LLM/Türkçe model başarı oranı **doğrulanmadı**. Bir modeli “en iyi” olarak seçmedik.

Başlangıç kaynak incelemesi: eski singleton state/control-center dosyaları önceki çalışma ağacında zaten kaldırılmıştı. Mevcut ağaçta expanded alt görünümler başlangıçta oluşturuluyor, QuickControls startup’ta açılıyor, asistan her istekte `/api/generate` sohbet çağrısı yapıyor ve kalıcı alarm/çok adımlı runtime bulunmuyordu. Sistemin native CPU/RAM, GPU sayaçları ve görünürlüğe göre durdurulan progress timer’ı korunup geliştirildi. Karşılaştırmalı Windows baseline sayısı mevcut değildir.

## Bu ortamda yürütülebilen doğrulama

| Kontrol | Sonuç türü |
|---|---|
| `Tests/Agent/NI.Agent.Tests.csproj` | Fake model/scheduler ile gerçek geçici dosya ve kalıcı depo kontrolleri; test çıktısı `Benchmarks/results/validation-macos.json` içindedir |
| `Tests/NI.Core.Tests.csproj` | Önceki bildirim/ayar regresyon kontrolleri |
| Ana WPF uygulama + `Tests/Windows/NI.LayoutChecks.csproj` | Release çapraz derleme; Windows çalıştırma kanıtı değildir |
| `Benchmarks/WpfBaseline` | Minimal WPF pencere Release çapraz derleme; kaynak kullanımı ölçülmedi |
| `Benchmarks/Collector` | Harici Windows ölçüm aracı Release çapraz derleme; Windows sayaçları burada çalıştırılmadı |
| `Benchmarks/NativeHost/main.cpp` | İzole Win32/Direct2D/DirectWrite örneği; Windows derlemesi ve ölçümü yapılmadı |

Gerçek LLM grubu ayrı ve açıkça çalıştırılır; geçici fixture dışında dosya değiştirmez:

```powershell
dotnet run --project Tests/Agent/NI.Agent.Tests.csproj -- --live --model '<yerel-kurulu-araç-modeli>'
```

Bu grup gerçek modelin Türkçe düzenleme isteğinden araç seçmesini ve fixture dosyalarının doğru hedefe taşınmasını kontrol eder. OS alarm adaptörü bu grupta da fake’tir; Windows alarm doğrulamasının yerine geçmez. Sunucu/model yoksa başarısız olur; otomatik kurulum/fallback yapmaz.

## Windows için ölçüm komutları

Ürünü Release yayınlayın, debugger bağlamayın ve eski çalışan instance’ı kapatın. Model/özellik ayarlarını senaryo boyunca sabit tutun.

```powershell
dotnet publish NI.csproj -c Release -r win-x64 --self-contained true -o artifacts\product
.\scripts\Measure-Resources.ps1 -Executable "$PWD\artifacts\product\NI.exe" -Scenario compact-idle -Seconds 660 -SampleMs 100
```

Script kendi harici Collector’ını yayınlar; metadata için yalnızca test aşamasında CIM kullanır. `environment.json` içinde gerçek CPU/RAM/GPU bilgileri ile commit/dirty/config kaydedilir. **DPI, render tier, aktif özellikler ve model ayarları operatör tarafından tamamlanmalıdır.** Senaryo adı etkileşimleri otomatik yürütmez: medya açma, bildirim üretme, 100 döngü ve AI görevleri operatör tarafından uygulanır. Gerçek masaüstünü benchmark fixture’ı olarak düzenlemeyin.

Minimal platform maliyeti:

```powershell
dotnet publish Benchmarks\WpfBaseline\WpfBaseline.csproj -c Release -r win-x64 --self-contained true -o artifacts\wpf-baseline
.\scripts\Measure-Resources.ps1 -Executable "$PWD\artifacts\wpf-baseline\WpfBaseline.exe" -Scenario compact-idle -Seconds 660
# WPF prototipi ayrıca --software argümanıyla manuel çalıştırılıp karşılaştırılabilir.
```

Native örneği Windows Developer Command Prompt’ta kendi README yorumundaki `cl` komutuyla derleyin ve aynı Collector ile ölçün. Bu örneğin küçüklüğü veya boş pencerenin sonucu ürünün 20 MB kabulü değildir.

## Ham veri ve hesaplama

Her çalışmada:

- `processes.jsonl`: zaman damgalı süreç satırları, PID/parent, application/ollama/dwm-system ayrımı, Private Bytes, private/total working set, CPU saniyesi, handle/thread, uygulama ve Ollama GPU değerleri; DWM GPU ayrı bağlamdır.
- `samples.csv`: süreç ailesi toplamları ve normal/active-ai/recovery fazları.
- `summary.json`: ortalama/p95/gözlenen tepe, normal bütçe sonucu, CPU sonucu, görev sonrası dönüş ve kapsam sınırlamaları.
- `ollama-models.jsonl`: `/api/ps` gözlemleri; uygulamanın belleğinden ayrı model yüklenme/unload kanıtı.
- `environment.json`: build/donanım ve operatörün tamamlayacağı ortam bilgileri.

CPU: `100 × CPU delta saniyesi / örnek aralığı saniyesi / mantıksal işlemci`. Özet ayrıca zaman ağırlıklı ortalama ve CPU saniyesini verir. p95/tepe örneklenmiş değerlerdir. Private Working Set yeni EX2 API’siyle okunur; desteklenmeyen OS veya eksik sayaçlar **null**, sıfır değildir. [PROCESS_MEMORY_COUNTERS_EX2](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex2).

Aynı executable’ın OS tarafından başlatılmış alarm helper’ları da uygulamaya dahil edilir. Gözlenen alt süreçler dahil edilir. Ollama/runner ve birleşik toplam ayrıca raporlanır. DWM bütünüyle uygulamaya mal edilmez; baseline DWM bağlamıyla farkı değerlendirilmelidir. Shared working set/GPU toplamları paylaşılan sayfaları iki kez sayabilir; benzersiz fiziksel RAM olarak yorumlanmaz.

Collector örnekleme taraması mutlak tepeyi ve çok kısa süreçleri garanti edemez. **Observed PASS mutlak maksimum kabulü değildir.** Soğuk başlangıç/çok kısa yardımcıları tamamlamak için process lifetime/commit tahsislerini kapsayan uygun ETW/WPR profili gereklidir. Collector’ın kendi maliyeti ürün toplamında değildir, fakat ölçüme etkisi değerlendirilmelidir. GPU instance keşfi 1 saniyelik, normal örnekleme varsayılan 100 ms’lik çözünürlüktedir; gerçek maksimum örnek aralığı rapora yazılır.

## Zorunlu performans senaryoları

| Senaryo | App Private Bytes ort./p95/tepe | CPU / GPU | Durum |
|---|---|---|---|
| Soğuk başlangıç | — | — | Windows yok; doğrulanmadı |
| Compact idle ≥10 dakika | — | — | Doğrulanmadı |
| Medya açık, AI kapalı | — | — | Doğrulanmadı |
| Expanded + sistem ölçümü | — | — | Doğrulanmadı |
| Bildirim yoğunluğu/uzun liste | — | — | Doğrulanmadı |
| 100 aç/kapa döngüsü | — | — | Doğrulanmadı |
| Ardışık AI + 30 sn toparlanma | — | — | Doğrulanmadı |
| Büyük fixture düzenleme | — | — | 1.000 dosyalık işlev testi var; Windows bellek ölçümü yok |
| AI kapalı alarm çalma | — | — | Çekirdek testi var; Windows/helper ölçümü yok |
| Uzun süre handle/thread sızıntısı | — | — | Doğrulanmadı; daha seyrek 1.000 ms örnekleme kullanılabilir |

20.000.000 bayt hedefi **başarılı olarak raporlanamaz**. Aşıldığı da ölçülmediği için sayısal bir başarısızlık uydurulmaz. Windows gözlemlerinde herhangi bir normal örnek aşarsa ilgili sonuç FAIL olmalıdır; düşük ortalama bunu değiştirmez. Aynı şekilde idle %0,2 CPU ve 30 saniyelik toparlanma için henüz PASS yoktur.
