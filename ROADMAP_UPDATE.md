# Yerel ajan teslimatı ve kalan doğrulamalar

Önceki Liquid Glass / Windows 11 tasarımı korunmuştur. Bu çalışma kod, dosya fixture’ları, kalıcı depo, görev UI’sı ve ölçüm araçlarını içerir. Windows ve gerçek model testleri yerine başarı varsayılmaz.

## Sonuç tablosu

| Alan | Uygulama | Doğrulama | Kabul durumu |
|---|---|---|---|
| Masaüstü düzenleme | Deterministik toplu plan/taşıma, native klasör çözümleme, kimlik/hash/kapsam kontrolü | Geçici dosyalarda gerçek taşımalar, 1.000 dosya, çatışma/kilit/değişiklik/cancellation testleri | Çekirdek test edildi; Windows native rename/OneDrive yarışı test edilmedi |
| Geri alma | Kalıcı günlükten, üzerine yazmadan, değişmiş hedefi koruyan ters taşıma | Fixture’da gerçek undo, kısmi iptal, çakışma ve crash uzlaştırması | Çekirdek test edildi; Windows entegrasyonu doğrulanmadı |
| Alarm | Kalıcı kayıt/makbuz, Türkçe zaman, weekday/günlük tekrar, OS Scheduler + native helper/IPC | Fake scheduler ile oluştur/değiştir/iptal/ertele/yeniden açılış/DST | Windows kayıt/gerçek çalma/kilit ekranı doğrulanmadı |
| Yerel araç çağırma | `/api/chat`, registry/schema/yetki, sıralı yürütme, timeout/iptal, bounded context | Fake model ve bozuk HTTP fixture’ları | Gerçek Ollama/Türkçe model başarısı doğrulanmadı; sunucu yok |
| Normal bellek bütçesi | Sınır **20.000.000 bayt Private Bytes**, bütün kendi süreçleri dahil | Collector ve baseline prototipi hazır; Windows ölçümü yok | **Doğrulanmadı; karşılıyor denemez** |
| AI sonrası toparlanma | Kaynak bırakma, 30 sn kabul testi ve ayrı model durumu ölçümü | Fake cancellation/release testi; gerçek bellek örneği yok | **Doğrulanmadı** |
| Idle CPU | Dakika hizalı saat, olaylar, görünürlükle durdurulan sampler, ertelenmiş UI | Yaşam döngüsü kodu ve çapraz derleme; Windows CPU yok | **%0,2 hedefi doğrulanmadı** |

## Uygulandı ve platformdan bağımsız test edildi

- Tek aktif görev, sıfır kapasitede açıkça reddedilen ek kuyruk, 12 tur/8 çağrı, toplam süre ve ayrı model/araç deadline’ları.
- Kayıtlı araç sözleşmesi, bilinmeyen araç/alan/tür reddi, belirli plan için onay ve yanlış onay hash’inin reddi.
- Negatif talimat ve prompt injection sınırları; listelemeyi “düzenleme tamamlandı” saymama; başarısız araçtan sonra modelin başarı iddiasını reddetme.
- Dosya yollarında traversal/reparse/device-name koruması, çakışma ve değişen kaynak kontrolü, idempotency, kısmi iptal, undo ve intent uzlaştırması.
- Alarm depolama, idempotent kayıtlar, eksik/geçmiş/belirsiz saat, DST ve weekday yerel saat kuralları.
- Yerel endpoint zorunluluğu, bozuk/kesilmiş/tamamlanmamış model yanıtlarının reddi, bağlam/çıktı sınırları.
- Önceki bildirim/ayar testleri. Kesin sayı ve araç çıktısı [doğrulama kaydında](Benchmarks/results/validation-macos.json).

## Uygulandı fakat ilgili ortamda çalıştırılmadı

- Windows `SHGetKnownFolderPath`, dosya handle’ı ile rename ve ancestor pinning.
- Windows Görev Zamanlayıcı kayıt/readback doğrulaması, kullanıcıya özel named pipe, kapalı ana uygulama için native alarm helper’ı.
- Windows Core Audio ses doğrulaması, medya pause doğrulaması, gerçek RAM okuması.
- Ertelenmiş WPF panelleri, quick-controls aboneliklerini bırakma, kapak/ikon bayt sınırları, gerçek WPF binding/görünüm testi.
- Collector, WPF framework baseline ve benchmark wrapper. Bunlar Windows sayısal performans kanıtı değildir.

## Henüz uygulanmadı / tamamlanmadı

- Windows kabul çalıştırmaları, gerçek model değerlendirme kümesinin yürütülmesi ve tüm performans senaryolarının ham ölçümü.
- Ölçümle gerekçelendirilmiş tam native UI geçişi veya AI-only C# helper mimarisi. Native örnek yalnızca izole, derlenmemiş bir maliyet prototipidir.
- Otomatik yönetilen Ollama kurulum/başlatma/sürüm kontrollü environment ayarlayıcısı. Paylaşımlı sunucu kapatılmaz; otomatik model indirilmez.
- Tek bir istekten keyfi sayıda alarm, bütün Türkçe zaman ifadeleri, PDF/Office içeriğe göre sınıflandırma ve genel `files_create_directory` aracı. Taşıma kategorileri içeriden oluşturulur.
- Diskler arası kopyala/doğrula/sil, silme veya kalıcı silme; genel shell/computer-use/ekran analizi/voice/indeksleme/MCP entegrasyonu.
- Alarm/operation arşivleme UI’sı, uygulama uninstall’ında Scheduler cleanup ve tam platform migration mekanizması. Kaynak/depo limitleri dolunca sessiz kayıt kaybı yerine açık ret verilir.
- Çoklu monitör seçici ve tam otomatik gizlenme önceki yol haritasında kalır.

## Sıradaki kabul çalışması

Windows üzerinde önce minimal WPF ve mevcut ürün aynı donanım/DPI/render şartlarıyla ölçülmelidir. 20 MB tepe aşılırsa sonuç **başarısız** olarak kaydedilmeli; ortalama veya GPU’ya kaydırılmış maliyetle başarılı sayılmamalıdır. Ardından izole native prototip ölçülerek teknoloji kararı verilmelidir. Windows olmayan bu oturumda böyle bir büyük geçiş yapılmadı.
