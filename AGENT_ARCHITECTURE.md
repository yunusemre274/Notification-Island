# Yerel görev ve alarm mimarisi

Bu belge glassmorphism / Windows 11 kabuğundaki görev yürütmesini anlatır. Compact inline giriş `AskCommand → ILocalAgentCoordinator → LocalAgentCoordinator` üzerinden aynı ajanı kullanır; ayrı AI paneli yoktur. Plan onayı, iptal ve geri alma küçük dashboard görev kartındadır. Tasarım sistemi ve pencere mimarisi [ARCHITECTURE.md](ARCHITECTURE.md) içindedir. Gerçek model, Windows bildirim izinleri ve fiziksel donanım davranışları fixture testlerinden ayrı doğrulanmalıdır.

## Görev akışı ve tek doğruluk kaynağı

`IslandVM → LocalAgentCoordinator → AgentRuntime → OllamaChatClient / ToolRegistry → gerçek adaptör → ToolResult → TaskSnapshot`.

`Agent/` WPF’den bağımsızdır. Windows kullanıcı klasörü, ses/medya ve Görev Zamanlayıcı adaptörleri `Services/Agent`, `Services/Alarms` ve mevcut servislerdedir. Ayrı agent framework’ü, web sunucusu veya sürekli çalışan model süreci eklenmedi.

Durumlar `Pending`, `Planning`, `Running`, `AwaitingApproval`, `Completed`, `PartiallyCompleted`, `Failed`, `Cancelled` şeklindedir. Durum, doğrulanan araç sonuçlarından türetilir. Modelin “yaptım” metni başarı kanıtı değildir. Düzenleme isteğinde yalnızca dosyaları listelemek `Completed` üretmez; çok adımlı istekte medya/ses/alarm gibi istenen kapsamların sonuçları da aranır. Kısmi etkiler ayrı raporlanır.

Aynı anda bir görev kabul edilir. Kuyruk kapasitesi **0**: ikinci istek açıkça reddedilir; görünmez sohbet kuyruğu tutulmaz. Varsayılan sınırlar: 12 model turu, tur başına 8 çağrı, model isteği 90 saniye, araç 120 saniye, toplam görev 600 saniye, onay bekleme 120 saniye. Ayarlar sınırlandırılır. İptal uçtan uca taşınır; dosya I/O/native çağrılarının iptali kooperatiftir, bloklanmış OS çağrısının anında kesileceği iddia edilmez. Mutasyon arka planda terk edilip başarı raporlanmaz.

UI’ya işlem ilerlemesi en fazla yaklaşık 120 ms aralıklarla aktarılır. Son görev snapshot’ı küçük bir dosyada tutulur; kullanıcı isteği ve belgelerin içeriği görev günlüğüne yazılmaz. Yeniden açılışta yarım görev otomatik yürütülmez; `Interrupted` açıklaması ve günlükten geri alma yolu korunur. Sonuç/istek bağlamı görev bitiminde bırakılır. Modelin düşünce metni işlem günlüğü olarak gösterilmez.

## Ollama protokolü ve kaynak sahipliği

İstemci resmi `/api/chat` `messages`, `tools`, `tool_calls`, `role=tool` sözleşmesini kullanır. Her görevde tek HttpClient tüm turlarda tekrar kullanılır, sonra kapatılır. Streaming kapalıdır; tamamlanmamış veya `done_reason=length` yanıtlar yürütülmez. Yanıt en fazla 65.536 bayt; JSON derinliği 20’dir. [Resmi tool-calling akışı](https://docs.ollama.com/capabilities/tool-calling), [chat API](https://docs.ollama.com/api/chat).

Endpoint yalnızca loopback HTTP/HTTPS olabilir; redirect/proxy kapalıdır. `/api/tags` ile yerel kayıt, `/api/show` ile `tools` capability’si denetlenir. `remote_host`, `remote_model` ve cloud adları reddedilir. Eski/sözleşmeyi bildirmeyen sunucular fail-closed davranır. Başlangıçta preload/health-check döngüsü, otomatik indirme veya bulut fallback yoktur. Bu politika değiştirilmiş/kötü niyetli yerel sunucunun dürüstlüğünü kriptografik olarak kanıtlamaz; güvenilen yerel Ollama varsayılır.

Başlangıç bağlamı `num_ctx=4096`, çıktı `num_predict=768` ve görev bağlamı yaklaşık 14.000 karakter sınırındadır. Karakter sayımı gerçek tokenizer ölçümü değildir. Aşımda eski tamamlanmış araç grupları, uygulamanın doğrulama sayıları ve son operationId’yi taşıyan kısa bir ledger’a çevrilir; sistem kuralları, özgün kullanıcı isteği ve son tam araç grubu korunur. Hâlâ sığmıyorsa görev durur; güvenlik kuralları sessizce kırpılmaz.

Paylaşımlı sunucuda istek başına `keep_alive=30s` kullanılır, process kill veya global ayar değişikliği yapılmaz. Bu bir talep edilen tutma süresidir; gerçek unload ayrı `/api/ps` ölçümü gerektirir. Açıkça uygulamaya ayrılmış sunucu olarak yapılandırılmışsa `Agent.ManagedServer=true` ve `UnloadOwnedModelAfterTask=true`, görev sonunda `keep_alive=0` isteği ve `/api/ps` kontrolü yapılır. Sonuç `TaskSnapshot.ModelRelease` alanındadır. Sunucunun tamamen kapandığı veya uygulamanın kendi belleğinin azaldığı bununla kanıtlanmaz. [Keep-alive / sunucu sahipliği](https://docs.ollama.com/faq).

Uygulama bir Ollama sunucusu kurmaz/başlatmaz. Yönetilen sunucuyu başlatan tam yaşam döngüsü ve sürüme bağlı `OLLAMA_MAX_LOADED_MODELS`, `OLLAMA_NUM_PARALLEL`, `OLLAMA_NO_CLOUD` ortam yapılandırıcısı henüz uygulanmadı. Kullanıcının ortam değişkenleri değiştirilmez. Yerel-only kontrolü istemci tarafında ayrıca uygulanır.

## Araç sözleşmesi

Her kayıt sabit isim, sürüm **1**, açıklama, JSON giriş şeması, kapsam, risk, iptal, idempotency ve undo açıklaması taşır. Bilinmeyen alan/araç, yanlış tür, aşırı uzun parametre ve desteklenmeyen eylem reddedilir. Dönüş: `success`, `errorCode`, `userMessage`, `operationId`, sınırlı `data`, `verifiedChanges`.

| Araç | Kapsam / sonuç | Yan etki ve geri alma |
|---|---|---|
| `files_list` | desktop/downloads, offset, 1–20 kayıt | Salt okunur; kısa fileId ve sınırlı metadata |
| `files_read_text` | fileId; en fazla 1.500 karakter | TXT/MD/CSV/JSON/LOG; içerik güvenilmeyen veri |
| `files_prepare_moves` | type, pdf-only veya tek belge/görsel/arşiv kategorisi | Kullanıcı dosyalarını değiştirmez; kalıcı toplu plan |
| `files_apply_moves` | Hazırlanan operationId | Aynı kökte, üzerine yazmadan taşıma; doğrulama ve günlük |
| `files_undo_operation` | En son geri alınabilir işlem | Değişmiş/çakışan dosyaları ezmeden ters taşıma |
| `alarms_create` | Özgün kullanıcı metni + isteğe bağlı başlık | Kalıcı kayıt + doğrulanmış OS zamanlaması |
| `alarms_list` | İlk 12 kayıt | Salt okunur |
| `alarms_update` | Kullanıcının seçtiği alarm | Saati/tekrar kuralını değiştirir |
| `alarms_cancel` | Kullanıcının seçtiği alarm | Tombstone + scheduler temizliği |
| `alarms_snooze` | Kullanıcı metnindeki süre | Tek tetiklemeyi erteler; tekrarın yerel saatini korur |
| `media_pause` | Güncel Windows medya oturumu | Duraklatma sonrası durum doğrulanır |
| `audio_set_volume` | 0–100 ve kullanıcı metniyle aynı yüzde | Core Audio sonucu tekrar okunur |
| `system_metrics` | Gerçek sistem RAM anlık okuması | CPU/GPU yoksa değer uydurulmaz |

Kategori klasörleri taşıma uygulaması içinde oluşturulur ve günlükte yer alır. Ayrı genel `files_create_directory` aracı, içerik temelli belge sınıflandırması, shell/PowerShell/Python, silme, genel computer-use, sesli asistan veya vector database bu sürümde yoktur. PDF desteği uzantıya göre taşımadır; PDF içeriği okunmaz.

## Yetki ve güvenilmeyen veri

Kullanıcı komutundan yalnızca dar görev yetkisi çıkarılır: açık Masaüstü/İndirilenler kapsamı ve açık pozitif işlem fiili. Negatif “taşıma / kurma / durdurma” ifadeleri otomatik mutasyon yetkisi vermez. Daha karmaşık dilde şüphe varsa somut işlem onayı gerekir; bu kurallar genel Türkçe dil anlama garantisi değildir. LLM aracı seçer, uygulama yetkiyi ayrı doğrular.

Yetki genişletmek için dosya adı, belge içeriği, bildirim veya model metni kullanılamaz. Onay belirli görev/araç parametre imzasına bağlıdır; yanlış plan hash’i reddedilir. Plan uygulamasında kapsam ve dosya durumu yeniden kontrol edilir. Kullanıcı yeni klasör kapsamını söylememişse klasör tahminiyle işlem yapılmaz. Genel terminal aracı olmadığı için model metni komuta çevrilmez.

## Dosya işlemleri ve geri alma

Gerçek klasörler `SHGetKnownFolderPath` ile çözülür; kullanıcı adı/dil sabit değildir, yönlendirilmiş/OneDrive yolları desteklenebilir. Reparse point/cloud placeholder yolları otomatik işleme alınmaz. Bu nedenle bazı OneDrive çevrimiçi öğeleri bilinçli olarak dışarıda kalır. Senkronize kökte yapılan gerçek taşımaların buluta yayılabileceği hazırlama sonucunda belirtilir.

Üst düzey bağımsız dosyalar işlenir; mevcut klasörler, kısayollar ve gizli/sistem/reparse öğeleri taşınmaz. Uzantı sınıflandırması deterministiktir. Plan en fazla 10.000 dosya içerir ve JSONL olarak akış halinde okunur; bin dosya için bin LLM isteği yoktur. Kategori: Belgeler, Görseller, Arşivler; pdf-only için PDF. Kullanıcının PDF/tek kategori filtresi planla karşılaştırılır; seçilmiş tek dosya adını genel toplu taşıma iznine çevirmek reddedilir. Desteklenmeyen filtre birleşimleri ek açıklama gerektirir.

Windows uygulamasında metadata, içerik SHA-256 ve native dosya kimliği alınır. Üst klasörler delete-sharing vermeyen handle’larla sabitlenir; kaynak açık handle üzerinden rename edilir. Yol göreli segmentlerle kontrol edilir; salt string-prefix yeterli kabul edilmez. ADS, traversal, reserved device name, symlink ve junction reddedilir. Aynı dosya sistemi dışına kopyala/sil akışı uygulanmaz; böyle bir işlem başarısız olur, kaynak körlemesine silinmez. **Bu Windows yarış koşulu adaptörü burada yalnızca derlendi; gerçek Windows doğrulaması gereklidir.**

Her adımın niyeti önce diske flush edilir, ardından rename ve hedef kontrolü yapılır. Crash sonrası kaynak/hedef/journal uzlaştırılır; belirsiz sonuçta tekrar yan etki üretilmez. Exactly-once veya her koşulda kusursuz rollback iddiası yoktur. Geri alma orijinal ad çakışmasını ve sonradan değişen içeriği korur. Boş kategori klasörleri geri alma sonrasında bırakılır. Kilitli dosyalar işlenmez; taşıma sırasındaki atlama ve yapılmayan adımlar ayrı sayılır. Hazırlama aşamasında okunamayan/kilitli dosyalar taşınabilir plana dahil edilmez ve ayrıca atlanan sayısında raporlanır.

İşlem günlükleri metadata/kimlik/hash ve gerekli kaynak kökünü içerir; belge gövdesi yoktur. 128 işlem ve 64 MB yeni işlem giriş eşiği vardır; tamamlanmış/geri alınmış/iptal edilmiş kapalı kayıtlar 30 gün sonra temizlenebilir. Devam eden işlem güvenlik kayıtlarını yazmak için eşik üstü ek alan kullanabilir; bu eşik RAM bütçesi değildir. Belirsiz kayıtlar otomatik yeniden yürütülmez.

## Alarm yaşam döngüsü

Sürüm 1 JSON deposu atomik yazılır; bozuk veya bilinmeyen sürümün üstüne varsayılan yazılmaz. 256 alarm kaydı ve 1.024 idempotency makbuzu sınırı vardır. Ağır ORM/SQLite veya alarmı bekleyen LLM/tight polling yoktur. İptal/değişiklik işlem kimlikleri kalıcıdır; belirsiz intent önce uzlaştırılır.

Zaman gerçek saat ve `Agent.TimeZoneId` üzerinden çözülür. Göreli dakika/saat, yarım saat, yarın 08.30, bu akşam, açık tarih ve günlük/hafta içi tekrar desteklenir. Sadece “Alarm kur” veya günü belirtilmemiş tek seferlik saat ek soru üretir. Desteklenmeyen buçuk/çeyrek gibi ifadeler tahmin edilmez; açık sayısal saat istenir. DST’te tek seferlik belirsiz/mevcut olmayan saat reddedilir; tekrarın böyle bir günü atlanır. Tek alarm niyeti işlenir; bir istekte çok sayıda farklı alarmı otomatik çıkarmak desteklenmez.

Windows Scheduler yalnızca mevcut executable, GUID ve UTC ticks kaydeder; InteractiveToken/LeastPrivilege kullanır. Kaydı ve executable/argüman/principal/trigger bilgilerini okuyup doğrulamadan “kuruldu” sonucu dönmez. İptal önce tombstone yazar; eski helper alarmı tekrar çaldıramaz. Yeniden açılışta kayıtlar uzlaştırılır. [Task Scheduler StartWhenAvailable](https://learn.microsoft.com/en-us/windows/win32/taskschd/tasksettings-startwhenavailable).

Ana uygulama açıksa helper, kullanıcıya özel `CurrentUserOnly` named pipe üzerinden 24 baytlık GUID/ticks paketi gönderir; kullanıcı metni/komut taşımaz. Ana süreç alarmı kalıcı kayıttan doğrular, Island’da kalıcı uyarı ve sistem sesi gösterir. Tamamen kapalıysa aynı executable’ın erken `--alarm-fire` kolu WPF Application oluşturmadan native MessageBox + ses fallback’i kullanır. **Helper’ın bütün belleği bütçeye dahildir.**

WakeToRun kapalıdır. Oturum açık olmalıdır; kilit ekranı, sessiz mod, uyku ve OS güç politikaları için garanti yoktur. Scheduler gecikmeli çalışabilir. En fazla 24 saat kaçırılmış alarm sunulur; daha eskisi expired sayılır. Bilgisayar tamamen kapalıyken çalma sözü verilmez. Delivery claim ile görüntüleme arasındaki crash sonucu belirsiz kalabilir; kayıt bunu `ClaimedDeliveryUnconfirmed` olarak taşır. OS kaydı ile gerçek ses duyulması aynı kanıt değildir.
