# Kaynak sözleşmesi: 20.000.000 bayt

## Kabul sınırları

AI görevi aktif değilken **uygulama süreç ailesinin toplam Private Bytes değeri en fazla 20.000.000 bayt** olmalıdır. Ortalama/p95 bu maksimumun yerine geçmez. Compact, expanded, medya, bildirim, ayarlar, alarm çalma ve alarm yardımcıları kapsam içindedir. Ayrı bir kendi sürecimize bellek taşıma bütçe dışına çıkarmaz.

Private Bytes fiziksel RAM değildir. Private Working Set, Total Working Set ve GPU dedicated/shared kullanım ayrı raporlanır. DWM/sürücü maliyeti sistem bağlamında ayrıca ölçülmeye çalışılır; bütün DWM maliyeti uygulamaya yazılmaz. Ollama sunucusu/runner, uygulama ve birleşik toplam ayrı gösterilir. Paylaşımlı sunucuda diğer istemcilerin maliyeti bütünüyle bu göreve atfedilemez.

**Şu an bu hedef karşılanmış olarak işaretlenmemiştir.** Windows runtime olmadığından ürünün veya minimal WPF pencerenin Private Bytes değeri ölçülemedi. macOS testleri bu sözleşmenin kanıtı değildir.

## AI istisnası ve toparlanma

Ollama’nın açık olması istisna oluşturmaz. Aktif kullanıcı görevinin GUID’si, başlangıcı, durumu ve bitişi vardır. Collector yalnızca Planning/Running durumunu `active-ai` olarak ayırır; AwaitingApproval tek başına bütçeyi sınırsız esnetmez. Global görev sınırı 600 saniye, onay sınırı 120 saniyedir; hayali sonsuz oturum yoktur.

Görev tamamlandığında/iptal edildiğinde bağlam koleksiyonları, tool/model timeout CTS’leri, response/stream ve görev HttpClient’ı bırakılır. Uygulamanın **30 saniye içinde** bütçeye dönmesi bir kabul hedefidir; ölçülmedi. Modelin `/api/ps` listesinden kaybolması ve uygulama Private Bytes değerinin azalması ayrı kontrollerdir. Paylaşımlı Ollama kill edilmez; 30 saniye keep_alive talebinin gerçek sonucu ölçülmelidir.

Normal akışta `GC.Collect`, EmptyWorkingSet veya yeniden başlatma ile bellek saklama uygulanmaz. WPF trimming, AOT, single-file veya ReadyToRun bir bellek çözümü olarak eklenmedi.

## Uygulanan kaynak sınırlamaları

- Expanded görsel ağaç ve dashboard/ayar/asistan görünümleri DataTemplate ile ihtiyaç anında oluşturulur. Kapandığında template kaldırılır; yalnızca Collapsed kullanılmasına güvenilmez. Gerçek GC/native release ve 100 döngü testi Windows’ta gereklidir.
- Saat bir sonraki dakika sınırında güncellenir. Hover/geçişler olay bazlıdır; sürekli shimmer/CompositionTarget.Rendering döngüsü yoktur.
- CPU/RAM/GPU tek örnekleyiciden gelir; görünür dashboard’da yaklaşık 1,5 saniye, gizliyken durur. WMI/alt süreç her örnekte başlatılmaz.
- Bağlantı/QuickControls kaynakları ilk panel açılışında hazırlanır; panelden ayrılınca Core Audio/Radio abonelikleri ve referansları bırakılır. Agent ses aracı kısa ömürlü endpoint açıp kapatabilir.
- Medya oturumu compact özet için olaylarla izlenir. Kapak yalnızca görünür medya kartı için okunur; kapanınca bırakılır. Decode 96×96, compressed stream sınırı 512 KiB’dir. Progress timer yalnızca görünürken/oynarken 1 Hz.
- Bildirim kaydı en fazla 40; ikon başına 32 KiB, toplam cache giriş eşiği 512 KiB; decode 40×40. Gövdeler ve ikonlar diske yazılmaz.
- Statik Freezable brush’lar dondurulur; tema değişikliği nesneyi yerinde mutasyona uğratmak yerine resource değiştirir.
- Görev geçmişi sınırlıdır, araç sonucu en fazla 4.000 karakterdir; model response en fazla 65.536 bayt. Toplu dosya planı akış halinde işlenir; içerik hash’i 64 KiB pooled buffer kullanır.
- Alarmda sürekli timer/polling veya inference yoktur. OS Scheduler kayıtları kullanılır. IPC tek kullanıcıya ait tek sunucu bağlantısıyla bekler; payload sabit boyuttadır. Helper çalıştığında bütçeye eklenir.

Bunlar kodda uygulanmış yaşam döngüsü kurallarıdır; ölçülmüş bellek/CPU kazancı olarak sunulmaz. WPF/CLR ve COM/DWM temel maliyetleri hâlâ ölçülmelidir.

## Ölçüm ve karar sırası

1. Mevcut WPF ürünü ve minimal `Benchmarks/WpfBaseline` Release/debugger olmadan ölçülür.
2. Aynı özellik/donanım/DPI/render ayarıyla optimize ürün ölçülür. Yazılımsal render yalnızca karşılaştırma testinde açıkça seçilir.
3. Tek bir normal örnekte Private Bytes >20.000.000 ise ilgili gözlenen test başarısızdır. Örneklenmemiş sıçramalar için ETW/WPR gerekir; düşük örnek tepe mutlak maksimum garantisi değildir.
4. WPF hedefi aşarsa başarısız kabul edilir. İzole `Benchmarks/NativeHost/main.cpp` Win32 + Direct2D/DirectWrite maliyetini değerlendirmek içindir; ürün geçişi değildir, burada derlenmedi/ölçülmedi.
5. Native tam panel + yalnızca AI sırasında çalışan C# çekirdek seçeneği ancak Windows ölçümü sonrasında kararlaştırılmalıdır. Normal expanded WPF helper bütçe dışında bırakılamaz.

Compact idle CPU hedefi bütün mantıksal işlemcilere normalize edilmiş ortalama **%0,2 altı**dır: `100 × toplam CPU saniyesi / geçen duvar saniyesi / mantıksal işlemci sayısı`. GPU için sıfır iddiası yoktur; sürekli uygulama render döngüsü olmaması ve gözlenen kullanım raporlanır.

Benchmark’ın ham CSV/JSON, örnekleme çözünürlüğü, ortalama/p95/gözlenen tepe ve tüm sınırlamaları [BENCHMARKS.md](BENCHMARKS.md) içindedir.
