using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NI.Configuration;
using NI.Models;
using NI.Services.Media;
using NI.Services.Notifications;
using NI.Services.SystemMonitoring;
using NI.Themes;
using NI.ViewModels;
using NI.Views;

namespace NI.LayoutChecks;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        // Fixtures are isolated to this QA executable. The production app never inserts demo data.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeManager.Initialize();
        var output = Path.GetFullPath("artifacts/layout-checks");
        Directory.CreateDirectory(output);
        var settings = new AppSettings(Path.Combine(output, "settings.json")) { Appearance = new() { ReduceMotion = true } };
        var monitor = new MonitorFixture();
        using var vm = new IslandVM(settings, new ProviderFixture(), monitor, new MediaFixture(), new ActionFixture());
        ThemeManager.Apply(settings.Appearance, false);
        var view = new IslandView { DataContext = vm };
        var window = new Window { Content = view, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        window.SetBinding(FrameworkElement.WidthProperty, new Binding(nameof(vm.PanelWidth)) { Source = vm });
        var errors = new StringWriter();
        var listener = new TextWriterTraceListener(errors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            window.Show();
            Capture("compact", () => vm.IsExpanded = false);
            Assert(window.ActualWidth == 460 && window.ActualHeight <= 50, "Compact window must remain small");
            Capture("expanded-empty", () => vm.IsExpanded = true);
            Assert(monitor.Active, "Dashboard must enable the monitor");
            vm.Notifications.Synchronize(new(Enumerable.Range(1, 60).Select(id => new NotificationItem
            {
                Id = id.ToString(), AppName = id % 2 == 0 ? "WhatsApp" : "Chrome", Title = "Bildirim görünüm testi",
                Body = "Uzun bildirim metni kartın sınırları içinde kalmalı ve diğer kontrolleri aşağı itmemeli.", CreatedAt = DateTimeOffset.Now.AddMinutes(-id)
            }).ToArray(), true));
            Capture("expanded-notifications", () => vm.IsExpanded = true);
            Capture("settings", () => vm.IsSettingsOpen = true);
            Assert(!monitor.Active, "Settings must pause the monitor");
            Capture("assistant", () => vm.IsAssistantOpen = true);
            Capture("blue-glass", () => { vm.IsAssistantOpen = false; settings.Appearance.Theme = GlassTheme.BlueGlass; ThemeManager.Apply(settings.Appearance, false); });
            Capture("narrow-panel", () => settings.Appearance.PanelWidth = 680);
            Capture("short-panel", () => settings.Appearance.ExpandedMaxHeight = 300);
            Capture("compact-notification", () =>
            {
                vm.IsExpanded = false;
                vm.Notifications.Synchronize(new([new NotificationItem { Id = "new", AppName = "WhatsApp", Title = "Yeni mesaj", Body = "Önizleme testi", CreatedAt = DateTimeOffset.Now }], false));
            });
            Assert(vm.HasPreview, "A new notification must appear in the compact preview");
            Assert(!monitor.Active, "Compact mode must pause the monitor");
            listener.Flush();
            File.WriteAllText(Path.Combine(output, "bindings.log"), errors.ToString());
            Assert(string.IsNullOrWhiteSpace(errors.ToString()), "WPF binding warnings/errors must be resolved; see bindings.log");
            Console.WriteLine($"PASS: 9 real WPF layouts, bounds, preview state, monitor lifecycle and binding checks. Screenshots: {output}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { window.Close(); app.Shutdown(); }

        void Capture(string name, Action configure)
        {
            configure(); window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert(window.ActualHeight <= settings.Appearance.ExpandedMaxHeight + 1, $"{name}: window exceeds height limit");
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
internal sealed class ProviderFixture : INotificationProvider
{
    public NotificationAccess Access => NotificationAccess.Listening;
    public string StatusMessage => "Windows bağlantısı • test fixture";
    public event EventHandler<NotificationSnapshot>? SnapshotChanged { add { } remove { } }
    public event EventHandler? AccessChanged { add { } remove { } }
    public Task StartAsync(bool requestPermission = false) => Task.CompletedTask;
    public Task RefreshAsync() => Task.CompletedTask;
    public void Dispose() { }
}
internal sealed class ActionFixture : INotificationActionHandler
{
    public Task<NotificationActivationResult> ActivateAsync(NotificationItem item) => Task.FromResult(new NotificationActivationResult(false, "Layout check: no app activation"));
}
internal sealed class MonitorFixture : ISystemMonitor
{
    public bool Active { get; private set; }
    public event EventHandler<SystemMetrics>? Updated;
    public void SetActive(bool active) { Active = active; if (active) Updated?.Invoke(this, new(24, 12, 61, 9.8, 16)); }
    public void Dispose() { }
}
internal sealed class MediaFixture : IMediaService
{
    public bool HasSession => true;
    public bool IsPlaying => true;
    public string TrackTitle => "Medya görünüm testi";
    public string ArtistName => "Windows medya oturumu";
    public BitmapImage? AlbumArtwork => null;
    public TimeSpan CurrentPosition => TimeSpan.FromSeconds(72);
    public TimeSpan TotalDuration => TimeSpan.FromSeconds(240);
    public event EventHandler? Changed { add { } remove { } }
    public Task InitializeAsync() => Task.CompletedTask;
    public void SetProgressVisible(bool visible) { }
    public Task<bool> TryPauseAsync() => Task.FromResult(true);
    public Task<bool> TogglePlayPauseAsync() => Task.FromResult(true);
    public Task<bool> TrySkipPreviousAsync() => Task.FromResult(true);
    public Task<bool> TrySkipNextAsync() => Task.FromResult(true);
    public void Dispose() { }
}
