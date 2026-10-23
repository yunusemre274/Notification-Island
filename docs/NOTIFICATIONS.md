# Gerçek Windows bildirimleri

## Seçilen yaklaşım

Üretim provider’ı `Windows.UI.Notifications.Management.UserNotificationListener` kullanır. Sahte callback veya Windows notification veritabanını tarama uygulanmaz. `AppNotificationManager` bu görev için seçilmedi: diğer uygulamaların toast’larını dinlemek yerine kendi uygulamanızın bildirimlerini yönetir. [Microsoft notification listener rehberi](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener)

Paket kimliği `GetCurrentPackageFullName` ile denetlenir. Paket yoksa provider `NeedsPackage` döner. Paket varsa erişim durumu denetlenir; izin yalnızca kullanıcı **Bildirimleri bağla** komutunu seçtiğinde UI thread’inde istenir. `Denied`, `NeedsPermission`, `Listening` ve `Unavailable` ayrı durumlardır. Uygulama etkinleşirken ve her snapshot öncesinde izin yeniden denetlenir. Erişim kalkarsa liste temizlenir. [API erişim sözleşmesi](https://learn.microsoft.com/en-us/uwp/api/windows.ui.notifications.management.usernotificationlistener)

Manifestteki `uap3:Capability Name="userNotificationListener"` bildirimi zorunludur. WPF masaüstü giriş noktası için `runFullTrust`; Radio API için `radios` da tanımlanır. `app.manifest` tek başına paket kimliği veya bu capability’yi sağlamaz. [uap3 manifest şeması](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-uap3-capability-manual)

## Kurulum yolu

1. Windows’ta .NET SDK ve MakeAppx/SignTool içeren Windows SDK’yı kurun.
2. Kullanacağınız imzalama sertifikasının Subject değerini belirleyin. Manifest Publisher ve sertifika Subject eşleşmeli; sertifika hedef sistemde güvenilir olmalı.
3. `scripts/Build-Msix.ps1 -Architecture x64 -Publisher 'CN=...' -CertificateThumbprint '...'` çalıştırın. ARM cihazlar için `arm64` seçin.
4. Script self-contained .NET 8 çıktısını stage eder, manifestin mimari/publisher alanlarını günceller, PNG logolarını kopyalar ve MakeAppx ile paketler. Önceki stage dosyaları temizlenir; kaynak repository temizlenmez.
5. Üretilen imzalı paketi `Add-AppxPackage` ile kurup Başlat menüsündeki **Notification Island** girişini açın.
6. Bildirimleri bağlayıp Windows izin penceresini yanıtlayın. Aynı kullanıcı hesabına bir Windows toast göndererek hem compact preview’i hem listeyi doğrulayın.

Script sertifika üretmez, güven deposunu değiştirmez ve paketi otomatik kurmaz. Bu repository’de gerçek imzalı MSIX üretimi/kurulumu macOS ortamı nedeniyle doğrulanmamıştır. Kullanılan publisher örnek değerdir; ürün dağıtımı için kendi kimliğinizi kullanın. Tam MSIX seçilmiştir; mevcut EXE kurucusunu korumak gerekiyorsa aynı provider ileride sparse package kimliğiyle değerlendirilebilir. [Paketleme seçenekleri](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/)

## Liste ve preview

Provider, toast snapshot’ını okur ve ikonları bounded cache içinde tekrar kullanır. `NotificationChanged` olaylarıyla güncellenir; sürekli polling yoktur. Her bildirim ayrı hata sınırındadır; bozuk metadata bütün listeyi düşürmez. Snapshot’lar semaphore ile sıraya alınır.

Router en yeni 40 kaydı sıralar, duplicate ID’leri eler ve okunma durumunu korur. Native ID ile creation time birlikte kullanılır. Yeni snapshot içermeyen eski kayıtlar kaldırılır. Başlangıç veya erişimin yeniden kazanılmasıyla gelen geçmiş, compact bildirimi tetiklemez. Üretim uygulamasında test bildirimi enjekte edilmez.

Compact preview 9 saniye sürer. Bu sırada yeni bildirim gelirse yeni olan kazanır ve süre yenilenir. Listedeki okunmuş/okunmamış durumu Island’a aittir; Windows’un veya WhatsApp’ın okundu bilgisi değildir. Liste içerikleri ve ikonlar yalnızca bellektedir.

## Tıklama ve özgün eylemler

Sıra şöyledir:

1. Kaynak AUMID ile eşleşen görünür süreç penceresi bulunabiliyorsa restore/foreground denenir. Windows foreground kuralları nedeniyle sonuç garanti edilemez.
2. `IApplicationActivationManager.ActivateApplication` ile kayıtlı paketli uygulama başlatılır. [Belgelenmiş aktivasyon API’si](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateapplication)
3. Kayıtlı Win32 uygulamalar için `explorer.exe shell:AppsFolder\<AUMID>` başlatma isteği gönderilir. İstek kabulü hedefin ilgili konuşmayı açtığı anlamına gelmez.
4. Kullanılabilir uygulama kimliği yoksa Windows bildirim ayarları açılır; kullanıcıya özgün bildirim için Windows 11’de **Win+N** bilgisi verilir. Windows 10’da Bildirim Merkezi **Win+A** ile açılır.

Listener sözleşmesi görsel metin/logo ve bildirim kimliği sağlar; başka uygulamanın özgün activation argümanını veya action düğmesini yeniden yürütme metodu sunmaz. Bu nedenle WhatsApp sohbeti/Chrome sekmesi için kesin yönlendirme vaat edilmez. Uygulama bazında belgelenmiş ve güvenilir deep link entegrasyonları `INotificationActionHandler` üzerinden eklenebilir. Bildirim metni asla komut/URI olarak çalıştırılmaz.

## Kapsam sınırları ve sonraki adımlar

- Windows’un sunduğu toast’lar kapsanır. Uygulama içi balonlar, özel çizilen popup’lar ve her sistem diyalogu kapsanmaz.
- Windows’ta engellenmiş veya geçmişte artık bulunmayan kayıtlar geri üretilemez.
- Uygulama çalışmıyorken değişiklik yakalamak için background task kaydı uygulanmadı. Yeniden açılış mevcut Windows geçmişini senkronize eder.
- Bildirim içerikleri OS gizlilik/politika ayarları ve uygulamanın yayınladığı metadata ile sınırlıdır.
- İleride kalıcı geçmiş istenirse retention, şifreleme ve kullanıcı temizleme davranışı ayrı tasarlanmalıdır. Şu an gövdeler diske yazılmaz.
- Dağıtım öncesi MSIX kurulum, izin reddi/iptali, yeniden izin verme ve WhatsApp/Chrome üzerinde uçtan uca test yapılmalıdır.
