using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NI.Agent;
using NI.Configuration;
using NI.Controls;
using NI.Core;
using NI.Models;
using NI.Services.Agent;
using NI.Services.Media;
using NI.Services.Notifications;
using NI.Services.SystemMonitoring;
using NI.Services.Windowing;
using NI.Themes;
using NI.ViewModels;
using NI.Views;

namespace NI.LayoutChecks;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try { return RunChecks(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static int RunChecks()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Console.Error.WriteLine(e.Exception); e.Handled = true; Environment.Exit(1); };
        ThemeManager.Initialize();
        var output = Path.GetFullPath("artifacts/layout-checks");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "dimensions.log"), "");
        var settings = new AppSettings(Path.Combine(output, "settings.json"))
        { Appearance = new() { ReduceMotion = true, AutoCollapse = true } };
        var monitor = new MonitorFixture();
        var media = new MediaFixture();
        var agent = new AgentFixture();
        var provider = new ProviderFixture();
        using var vm = new IslandVM(settings, provider, monitor, media, new ActionFixture(), agent);
        // Exercise the real window lifecycle and HWND region, with isolated services/storage.
        var window = new MainWindow(vm, startServices: false);
        var view = (IslandView)window.Content;
        var errors = new StringWriter();
        using var listener = new TextWriterTraceListener(errors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        var captures = 0;
        var checks = 0;
        try
        {
            window.Show(); Drain();
            Check(DesktopInstance.MutexName == DesktopInstance.MutexName && DesktopInstance.MutexName != @"Local\NotificationIsland.Desktop", "UI singleton is scoped to the current Windows desktop, not the entire session");
            window.Hide(); Drain();
            Check(DesktopInstance.TryActivateExisting(), "A repeated launch can locate and signal a hidden existing window");
            Drain();
            Check(window.IsVisible && window.WindowState == WindowState.Normal, "The activation message restores the existing window instead of exiting silently");
            Capture("compact-spotify", () => vm.IsExpanded = false);
            Check(window.ActualWidth == 460 && window.ActualHeight <= 50, "Compact window remains a capsule");
            Check(!monitor.Active && !media.ProgressVisible, "Compact mode disables system and media timeline polling");
            Check(vm.ShowMediaBrand && vm.MediaSource == MediaSourceKind.Spotify, "Compact Spotify branding");
            Check(view.FindName("ShellSurface") is GlassSurface { Clip: RectangleGeometry clip } && !clip.FillContains(new Point(0, 0)), "WPF child visuals and hit area exclude capsule corners");
            AssertNativeCapsule(window);
            var waveform = (MediaWaveform)view.FindName("CompactWaveform");
            Check(waveform.ActiveAnimationCount == 0, "Reduced motion has no animation clocks");

            Capture("compact-youtube", () => media.SetSource(MediaSourceKind.YouTube, "YouTubeMusic"));
            Check(vm.MediaSource == MediaSourceKind.YouTube, "YouTube identity reaches the view");
            Capture("compact-generic", () => media.SetSource(MediaSourceKind.Generic, "OtherPlayer.exe"));
            Check(vm.MediaSource == MediaSourceKind.Generic, "Unknown source uses a generic fallback");

            Capture("compact-playing", () => { media.SetSource(MediaSourceKind.Spotify, "Spotify.exe"); settings.Appearance.ReduceMotion = false; });
            Check(!SystemParameters.ClientAreaAnimation || waveform.ActiveAnimationCount == 9, "Playing uses nine retained transform clocks");
            Capture("compact-paused", () => media.SetPlaying(false));
            Check(waveform.ActiveAnimationCount == 0, "Pause removes every animation clock");
            media.SetPlaying(true); Drain();
            waveform.Visibility = Visibility.Collapsed;
            Check(waveform.ActiveAnimationCount == 0, "Hidden waveform removes every animation clock");
            waveform.ClearValue(UIElement.VisibilityProperty); Drain();
            settings.Appearance.ReduceMotion = true;

            Capture("expanded-empty", () => vm.IsExpanded = true);
            Check(window.ActualWidth == settings.Appearance.PanelWidth, "Expanded width follows VM after native clipping without WindowChrome interference");
            Check(monitor.Active && media.ProgressVisible, "Expanded dashboard enables visible metrics and timeline");
            Check(!Descendants<ScrollBar>(view).Any(bar => bar.IsVisible), "Expanded dashboard has no visible scrollbar");
            Check(Descendants<ToggleTile>(view).Count(tile => tile.IsVisible) == 3, "All three connection tiles are visible");
            CheckTileText();
            monitor.Publish(new(100, 100, 100, 16, 16));
            Capture("metrics-100-percent", () => { });
            Check(vm.CpuText == "100%" && vm.GpuText == "100%" && vm.RamText == "100%", "Complete 100 percent values");
            CheckMetricText();
            monitor.Publish(new(null, null, null, null, null));
            Capture("metrics-unavailable", () => { });
            Check(vm.CpuText == "N/A" && vm.GpuText == "N/A" && vm.RamText == "N/A", "Unavailable metrics are not fabricated");
            monitor.Publish(new(24, 15, 65, 10.4, 16));

            vm.Notifications.Synchronize(new(Enumerable.Range(1, 60).Select(Notification).ToArray(), true));
            Capture("expanded-notifications", () => { });
            Check(vm.Notifications.Items.Count == 40 && vm.VisibleNotifications.Count == 3, "Bounded notifications are paginated instead of scrolled");
            var seen = new HashSet<string>();
            do
            {
                foreach (var item in vm.VisibleNotifications) seen.Add(item.Id);
                if (!vm.NextNotificationPageCommand.CanExecute(null)) break;
                vm.NextNotificationPageCommand.Execute(null);
            } while (true);
            Check(seen.Count == 40 && !vm.CanNextNotificationPage, "Every retained notification remains reachable");
            vm.Notifications.Synchronize(new([Notification(1)], true));
            Check(vm.VisibleNotifications.Count == 1 && vm.NotificationPageLabel == "1 / 1", "Page clamps after notifications disappear");

            // Legacy auto-collapse=true must not revive hover timers or focus-loss closing.
            window.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
            using (var other = new TestFocusWindow())
            {
                other.Show(); other.Activate(); SpinFor(900);
                Check(vm.IsExpanded, "Hover leave and another window taking focus never close an explicitly opened panel");
            }
            window.Activate(); Drain();

            Capture("inline-task-input", () => vm.ToggleAssistantCommand.Execute(null));
            Check(vm.Mode == IslandMode.TaskInput && !vm.IsExpanded && !monitor.Active && !media.ProgressVisible, "AI opens compact input and suspends expanded work");
            var input = (TextBox)view.FindName("TaskInputBox");
            Check(input.IsVisible && ReferenceEquals(FocusManager.GetFocusedElement(window), input), "Inline input receives logical focus");
            Check(!Descendants<NI.Views.Controls.TaskStatusPanel>(view).Any(panel => panel.IsVisible), "Input never constructs a separate AI panel");
            input.Text = "  Spotify'ı durdur  ";
            input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            var enter = input.InputBindings.OfType<KeyBinding>().Single(binding => binding.Key == Key.Enter);
            Check(ReferenceEquals(enter.Command, vm.AskCommand), "Enter is bound to the real task command");
            enter.Command.Execute(null); Drain();
            Check(agent.LastRequest == "Spotify'ı durdur" && agent.RunCount == 1 && vm.IsAssistantBusy && vm.Mode == IslandMode.Compact, "Inline Enter dispatches exactly one task and restores the compact bar");
            vm.AskCommand.Execute(null);
            Check(agent.RunCount == 1, "Repeated Enter cannot submit a second concurrent task");
            Capture("compact-task-running", () => { });
            agent.RequestApproval(); Drain();
            Capture("task-approval", () => { });
            Check(vm.HasApproval && vm.IsExpanded && vm.HasTaskCard && vm.VisibleNotifications.Count == 1, "Task approval uses a small dashboard card with a reflowed notification page");
            vm.ApproveTaskCommand.Execute(null);
            Check(agent.ApprovedHash == "fixture-plan-hash" && !vm.HasApproval, "Approval stays tied to the exact plan");
            agent.Complete(); Drain();
            Capture("task-completed", () => { });
            Check(!vm.IsAssistantBusy && vm.AssistantInput == "" && vm.TaskSummary == "Medya duraklatıldı.", "Completed task restores editing and shows verified result");
            var dashboardHeight = window.ActualHeight;
            vm.UndoTaskCommand.Execute(null); Drain();
            Capture("task-status-message", () => { });
            Check(vm.HasStatus && window.ActualHeight <= dashboardHeight + 1, "Status messages reuse the toolbar instead of making a scroll-free panel taller");
            vm.DismissStatusCommand.Execute(null);
            Capture("unpackaged-task-result", () => { vm.Notifications.Synchronize(new([], true)); provider.SetAccess(NotificationAccess.NeedsPackage); });
            foreach (var panel in Descendants<NI.Views.Controls.NotificationsPanel>(view))
                foreach (var text in Descendants<TextBlock>(panel).Where(text => text.IsVisible))
                    Check(text.TransformToAncestor(panel).TransformBounds(new Rect(text.RenderSize)).Bottom <= panel.ActualHeight + 1, "Unpackaged notification empty state fits above the task card");
            provider.SetAccess(NotificationAccess.Listening);

            Capture("settings-appearance", () => vm.IsSettingsOpen = true);
            Check(!monitor.Active && !media.ProgressVisible, "Settings suspend expanded collectors");
            var tabs = Descendants<TabControl>(view).Single();
            for (var index = 1; index < 4; index++)
            {
                var tabIndex = index;
                Capture("settings-tab-" + index, () => tabs.SelectedIndex = tabIndex);
                Check(!Descendants<ScrollBar>(view).Any(bar => bar.IsVisible), "Settings tab " + index + " fits without a scrollbar");
            }
            Capture("input-from-settings", () => vm.ToggleAssistantCommand.Execute(null));
            Check(vm.Mode == IslandMode.TaskInput && !vm.IsSettingsOpen && !vm.IsExpanded, "Settings to input is one exclusive transition");
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(vm.Mode == IslandMode.Compact, "Escape closes only the compact input");

            Capture("narrow-dashboard", () => { settings.Appearance.PanelWidth = 680; vm.IsExpanded = true; monitor.Publish(new(100, 100, 100, 16, 16)); });
            CheckTileText(); CheckMetricText();
            Capture("short-panel", () => settings.Appearance.ExpandedMaxHeight = 300);
            Check(settings.Appearance.ExpandedMaxHeight == 420, "Old undersized panel limits migrate to a readable scroll-free minimum");
            Capture("blue-glass", () => { settings.Appearance.Theme = GlassTheme.BlueGlass; ThemeManager.Apply(settings.Appearance, false); });
            Capture("compact-notification", () =>
            {
                vm.IsExpanded = false;
                vm.Notifications.Synchronize(new([Notification(999)], false));
            });
            Check(vm.HasPreview && !vm.ShowMediaBrand, "A new notification takes compact preview priority without changing shell mode");
            Check(!monitor.Active && !media.ProgressVisible, "Collapsed collectors stay stopped");
            var inputWidth = settings.Appearance.CompactWidth;
            settings.Appearance.CompactWidth = 360;
            Capture("narrow-inline-input", () => vm.IsTaskInputOpen = true);
            Check(input.ActualWidth >= 120 && input.IsVisible && window.ActualWidth == 360, "Inline task input remains usable at minimum compact width");
            settings.Appearance.CompactWidth = inputWidth;
            Capture("compact-64-dip", () => { vm.IsTaskInputOpen = false; settings.Appearance.CompactHeight = 64; });
            Check(vm.WindowCornerRadius == 33, "Capsule corner radius follows height independently of panel corner customization");
            AssertNativeCapsule(window);

            listener.Flush();
            File.WriteAllText(Path.Combine(output, "bindings.log"), errors.ToString());
            Check(string.IsNullOrWhiteSpace(errors.ToString()), "No WPF binding warnings or errors; see bindings.log");
            Console.WriteLine($"PASS: {captures} WPF renders; {checks} state, input, task, layout, branding, native clipping and animation checks. Screenshots: {output}");
            return 0;
        }
        catch (Exception ex)
        {
            listener.Flush(); File.WriteAllText(Path.Combine(output, "bindings.log"), errors.ToString());
            Console.Error.WriteLine(ex); return 1;
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            window.Close(); app.Shutdown();
        }

        void Capture(string name, Action configure)
        {
            configure(); Drain();
            foreach (var settingsPanel in Descendants<NI.Views.Controls.SettingsPanel>(view).Where(panel => panel.IsVisible))
                foreach (var text in Descendants<TextBlock>(settingsPanel).Where(text => text.IsVisible))
                {
                    var bounds = text.TransformToAncestor(settingsPanel).TransformBounds(new Rect(text.RenderSize));
                    Check(bounds.Bottom <= settingsPanel.ActualHeight + 1, name + ": settings text fits vertically: " + text.Text);
                }
            File.AppendAllText(Path.Combine(output, "dimensions.log"), $"{name}: target={vm.PanelWidth}, Width={window.Width}, Actual={window.ActualWidth}x{window.ActualHeight}, MaxWidth={window.MaxWidth}, work={SystemParameters.WorkArea}, input={inputSize()}{Environment.NewLine}");
            Check(window.ActualHeight <= settings.Appearance.ExpandedMaxHeight + 1, name + ": height fits configured limit");
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(view);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
            captures++;
            double inputSize() => ((TextBox)view.FindName("TaskInputBox")).ActualWidth;
        }
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++;
        }
        void CheckTileText()
        {
            foreach (var tile in Descendants<ToggleTile>(view).Where(tile => tile.IsVisible))
                foreach (var text in Descendants<TextBlock>(tile).Where(text => text is not IslandIcon))
                    Check(TextFits(text), "Connection text fits: " + text.Text);
        }
        void CheckMetricText()
        {
            foreach (var card in Descendants<MetricCard>(view).Where(card => card.IsVisible))
            {
                var text = (TextBlock)card.Template.FindName("MetricValue", card);
                Check(TextFits(text), "Metric percentage fits: " + text.Text);
            }
        }
        void AssertNativeCapsule(Window target)
        {
            var region = CreateRectRgn(0, 0, 0, 0);
            try
            {
                Check(GetWindowRgn(new WindowInteropHelper(target).Handle, region) > 0, "Native window has an explicit capsule region");
                Check(!PtInRegion(region, 0, 0), "Native top corner excludes rectangular host pixels");
                var dpi = VisualTreeHelper.GetDpi(target);
                Check(PtInRegion(region, (int)(target.ActualWidth * dpi.DpiScaleX / 2), (int)(target.ActualHeight * dpi.DpiScaleY / 2)), "Native center remains interactive");
            }
            finally { DeleteObject(region); }
        }
    }
    private static bool TextFits(TextBlock text)
    {
        var formatted = new FormattedText(text.Text, CultureInfo.CurrentCulture, text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, text.Foreground, VisualTreeHelper.GetDpi(text).PixelsPerDip);
        return formatted.WidthIncludingTrailingWhitespace <= text.ActualWidth + 1 && formatted.Height <= text.ActualHeight + 2;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T result) yield return result;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Drain()
    {
        foreach (Window window in Application.Current.Windows) window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        foreach (Window window in Application.Current.Windows) window.UpdateLayout();
    }
    private static void SpinFor(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); Drain();
    }
    private static NotificationItem Notification(int id) => new()
    {
        Id = id.ToString(), AppName = id % 2 == 0 ? "WhatsApp" : "Chrome", Title = "Yeni bildirim · " + id,
        Body = "Uzun bildirim metni kartın sınırları içinde kalır. Ayrıntılar araç ipucunda gösterilir.", CreatedAt = DateTimeOffset.Now.AddMinutes(-id)
    };
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr region);
}

internal sealed class TestFocusWindow : Window, IDisposable
{
    public TestFocusWindow() { Width = 100; Height = 80; ShowInTaskbar = false; WindowStyle = WindowStyle.ToolWindow; }
    public void Dispose() => Close();
}
internal sealed class ProviderFixture : INotificationProvider
{
    public NotificationAccess Access { get; private set; } = NotificationAccess.Listening;
    public string StatusMessage => "Windows bağlantısı · test fixture";
    public event EventHandler<NotificationSnapshot>? SnapshotChanged { add { } remove { } }
    public event EventHandler? AccessChanged;
    public void SetAccess(NotificationAccess access) { Access = access; AccessChanged?.Invoke(this, EventArgs.Empty); }
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
    public void SetActive(bool active) { Active = active; if (active) Publish(new(24, 15, 65, 10.4, 16)); }
    public void Publish(SystemMetrics metrics) => Updated?.Invoke(this, metrics);
    public void Dispose() { }
}
internal sealed class MediaFixture : IMediaService
{
    public bool HasSession => true;
    public bool IsPlaying { get; private set; } = true;
    public string SourceAppUserModelId { get; private set; } = "Spotify.exe";
    public MediaSourceKind SourceKind { get; private set; } = MediaSourceKind.Spotify;
    public string TrackTitle => "Midnight City";
    public string ArtistName => "M83";
    public BitmapImage? AlbumArtwork => null;
    public TimeSpan CurrentPosition => TimeSpan.FromSeconds(72);
    public TimeSpan TotalDuration => TimeSpan.FromSeconds(240);
    public bool ProgressVisible { get; private set; }
    public event EventHandler? Changed;
    public void SetSource(MediaSourceKind source, string appId) { SourceKind = source; SourceAppUserModelId = appId; Changed?.Invoke(this, EventArgs.Empty); }
    public void SetPlaying(bool playing) { IsPlaying = playing; Changed?.Invoke(this, EventArgs.Empty); }
    public Task InitializeAsync() => Task.CompletedTask;
    public void SetProgressVisible(bool visible) => ProgressVisible = visible;
    public Task<bool> TryPauseAsync() { SetPlaying(false); return Task.FromResult(true); }
    public Task<bool> TogglePlayPauseAsync() { SetPlaying(!IsPlaying); return Task.FromResult(true); }
    public Task<bool> TrySkipPreviousAsync() => Task.FromResult(true);
    public Task<bool> TrySkipNextAsync() => Task.FromResult(true);
    public void Dispose() { }
}
internal sealed class AgentFixture : ILocalAgentCoordinator
{
    public string? LastRequest;
    public int RunCount;
    public string? ApprovedHash;
    private readonly Guid _id = Guid.NewGuid();
    private TaskCompletionSource<TaskSnapshot>? _pending;
    public event EventHandler<TaskSnapshot>? Changed;
    public event EventHandler<ApprovalRequest>? ApprovalRequired;
    public Task<TaskSnapshot> RunAsync(string request, CancellationToken token)
    {
        LastRequest = request; RunCount++;
        _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Changed?.Invoke(this, new(_id, AgentTaskState.Running, "Medya duraklatılıyor…", DateTimeOffset.Now, null, 0, 0, null));
        return _pending.Task;
    }
    public void RequestApproval() => ApprovalRequired?.Invoke(this, new(_id, "fixture-plan-hash", "Bu test planı yalnızca geçici dosyalar içindir. Onay verilen planın kimliği değiştirilemez."));
    public void Complete()
    {
        var snapshot = new TaskSnapshot(_id, AgentTaskState.Completed, "Medya duraklatıldı.", DateTimeOffset.Now, DateTimeOffset.Now, 1, 1, null);
        Changed?.Invoke(this, snapshot); _pending!.TrySetResult(snapshot);
    }
    public Task<ToolResult> UndoAsync(CancellationToken token) => Task.FromResult(ToolResult.Fail("NO_UNDO", "Fixture has no file changes."));
    public Task<string> ReadDetailsAsync(CancellationToken token) => Task.FromResult("Isolated task fixture.");
    public bool Approve(string hash, bool accepted) { ApprovedHash = hash; return accepted; }
    public void Cancel() => _pending?.TrySetCanceled();
    public void RecoverInterrupted() { }
    public void Dispose() => Cancel();
}
