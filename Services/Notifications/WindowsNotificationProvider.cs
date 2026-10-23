using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using NI.Models;

namespace NI.Services.Notifications;

public sealed class WindowsNotificationProvider : INotificationProvider
{
    private UserNotificationListener? _listener;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _subscribed, _initialized, _disposed, _starting;
    private readonly Dictionary<string, NotificationItem> _cache = new();
    public NotificationAccess Access { get; private set; } = NotificationAccess.NeedsPermission;
    public string StatusMessage { get; private set; } = "Windows bildirimlerini bağlayın.";
    public event EventHandler<NotificationSnapshot>? SnapshotChanged;
    public event EventHandler? AccessChanged;

    public async Task StartAsync(bool requestPermission = false)
    {
        if (_disposed || _starting) return;
        _starting = true;
        try
        {
            // Identity is checked before touching capability-gated WinRT APIs.
            uint length = 0;
            if (GetCurrentPackageFullName(ref length, IntPtr.Zero) == 15700)
            {
                SetAccess(NotificationAccess.NeedsPackage, "Bildirimleri bağlamak için MSIX kurulumu gerekli.");
                return;
            }
            _listener ??= UserNotificationListener.Current;
            var access = _listener.GetAccessStatus();
            if (requestPermission && access == UserNotificationListenerAccessStatus.Unspecified)
                access = await _listener.RequestAccessAsync(); // UI command, on the UI thread.
            if (_disposed) return;
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                SetAccess(access == UserNotificationListenerAccessStatus.Denied ? NotificationAccess.Denied : NotificationAccess.NeedsPermission,
                    access == UserNotificationListenerAccessStatus.Denied ? "Bildirim erişimi Windows ayarlarında kapalı." : "Bildirimleri görmek için erişim izni verin.");
                await _gate.WaitAsync();
                try
                {
                    _initialized = false;
                    _cache.Clear();
                    SnapshotChanged?.Invoke(this, new([], true));
                }
                finally { _gate.Release(); }
                return;
            }
            if (!_subscribed) { _listener.NotificationChanged += OnChanged; _subscribed = true; }
            SetAccess(NotificationAccess.Listening, "Windows bildirimleri bağlı");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Notification listener: {ex.GetType().Name} / 0x{ex.HResult:X8}");
            SetAccess(NotificationAccess.Unavailable, "Bildirim erişimi kullanılamıyor. Paket capability ve Windows izinlerini kontrol edin.");
        }
        finally { _starting = false; }
    }

    public async Task RefreshAsync()
    {
        if (_disposed || _listener == null) return;
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            if (_listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
            {
                SetAccess(NotificationAccess.Denied, "Bildirim erişimi kaldırıldı. Windows ayarlarından izin verin.");
                _initialized = false;
                _cache.Clear();
                SnapshotChanged?.Invoke(this, new([], true));
                return;
            }
            var native = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            var items = new List<NotificationItem>();
            var iconBytesBudgetUsed = _cache.Values.Sum(x => x.IconBytes?.Length ?? 0);
            foreach (var notification in native.OrderByDescending(x => x.CreationTime).Take(NotificationRouter.Capacity))
            {
                try
                {
                    var texts = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?.GetTextElements();
                    var id = $"windows:{notification.Id}:{notification.CreationTime.UtcTicks}";
                    var title = texts?.FirstOrDefault()?.Text ?? "Bildirim";
                    var body = string.Join(" · ", texts?.Skip(1).Select(x => x.Text) ?? []);
                    if (_cache.TryGetValue(id, out var cached) && cached.Title == title && cached.Body == body)
                    { items.Add(cached); continue; }
                    byte[]? icon = cached?.IconBytes;
                    try
                    {
                        if (icon == null)
                        {
                            using var logo = await notification.AppInfo.DisplayInfo.GetLogo(new Size(32, 32)).OpenReadAsync();
                            using var buffer = new MemoryStream();
                            using var input = logo.AsStreamForRead();
                            var chunk = new byte[4096]; int count;
                            while ((count = await input.ReadAsync(chunk)) > 0 && buffer.Length + count <= 32768)
                                buffer.Write(chunk, 0, count);
                            if (input.Position == input.Length && iconBytesBudgetUsed + buffer.Length <= 524288)
                            { icon = buffer.ToArray(); iconBytesBudgetUsed += icon.Length; }
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"Notification logo unavailable: {ex.HResult:X8}"); }
                    items.Add(new NotificationItem
                    {
                        Id = id, NativeId = notification.Id,
                        AppUserModelId = notification.AppInfo.AppUserModelId,
                        AppName = notification.AppInfo.DisplayInfo.DisplayName,
                        Title = title, Body = body,
                        CreatedAt = notification.CreationTime, IconBytes = icon
                    });
                }
                catch (Exception ex) { Debug.WriteLine($"Notification skipped: {ex.HResult:X8}"); }
            }
            if (_disposed) return;
            _cache.Clear();
            foreach (var item in items) _cache[item.Id] = item;
            SnapshotChanged?.Invoke(this, new(items, !_initialized));
            _initialized = true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Notification refresh: {ex.HResult:X8}");
            SetAccess(NotificationAccess.Unavailable, "Bildirimler yenilenemedi. Yeniden bağlanmayı deneyin.");
        }
        finally { _gate.Release(); }
    }

    private async void OnChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args) => await RefreshAsync();
    private void SetAccess(NotificationAccess access, string message)
    {
        Access = access; StatusMessage = message; AccessChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Dispose()
    {
        _disposed = true;
        if (_subscribed && _listener != null) _listener.NotificationChanged -= OnChanged;
        // In-flight WinRT operations may still own the semaphore; do not dispose it under them.
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
