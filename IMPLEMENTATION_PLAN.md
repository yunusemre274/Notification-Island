# Liquid Glass / Windows 11 yeniden tasarımı

Bu dosya önceki aşamalı prototip planının yerini alır. Uygulanan mimari [ARCHITECTURE.md](ARCHITECTURE.md), kullanım [README.md](README.md) içindedir.

## Uygulandı

1. Merkezi tema/font/yüzey kaynakları, üç slate cam tema, Fluent ikon kontrolü, reusable metric kartı ve tutarlı buton/slider stilleri.
2. Solda saatli compact bar; görev alanı kaldırıldı; bildirim → medya → idle önceliği ve kısa içerik geçişi.
3. En fazla 460 DIP varsayılan yükseklikte, yatay iki kolonlu expanded düzen. Bölüm görünümleri ayrı XAML dosyalarına ayrıldı.
4. Gerçek Windows listener provider’ı, erişim durumları, bounded router, yerel okunma durumu ve uygulama aktivasyon fallback’leri.
5. Native CPU/RAM ve opsiyonel GPU 3D sayaçları; panel görünürlüğüne göre arka plan örneklemesi.
6. GSMTC üzerinden Spotify ve diğer güncel medya oturumları; metadata generation kontrolü ve görünürlükle sınırlı progress timer.
7. Tipli, doğrulanan ve kalıcı özelleştirme. Eski ayar uyumluluğu, atomik yazma, bozuk JSON yedeği.
8. Event tabanlı ses; gerçek Wi-Fi/Bluetooth state; desteksiz donanımın açıkça gösterilmesi; debounce edilmiş parlaklık.
9. Ollama sohbet arayüzü ve seçilen modelin gerçekten kullanılması. Opsiyonel, açık koordinatlarla hava durumu.
10. MSIX manifesti, paket logoları, Windows paketleme script’i, mantık kontrolleri ve Windows WPF görünüm kontrol programı.

## Windows üzerinde tamamlanacak kabul doğrulamaları

Bu ortam macOS olduğundan imzalı MSIX kurulumu, izin UI’sı, gerçek WhatsApp/Chrome bildirimleri, DWM materyalleri ve donanım etkileşimleri çalıştırılamadı. Bunlar [Windows kontrol listesindeki](docs/WINDOWS-VALIDATION.md) açık kabul adımlarıdır. Kodun çapraz derlenmesi bu testlerin yerine geçmez.

## Sonraki geliştirmeler

- Windows üzerinde görsel QA ve gerçek idle CPU/RAM/FPS ölçümü; native köşe/gölge davranışına göre iyileştirme.
- Uygulamaların desteklediği belgelenmiş deep link handler’ları; özgün toast action’ı için genel API olmadığı sınırını koruma.
- Monitör seçici, istenirse tam otomatik gizlenme ve görev çubuğu/tray yönetimi.
- Dinamik ekran/GPU değişimlerinde ek sürücü uyumluluk testleri ve istenirse disk/ağ göstergeleri.
- Ticari hava durumu dağıtımı için servis sözleşmesinin/lisansın seçilmesi.
- İhtiyaç doğarsa uygulama kapalıyken listener background task’ı; kalıcı bildirim geçmişi için ayrı gizlilik/retention tasarımı.
