using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NI.Configuration;
using NI.Core;
using NI.Models;
using NI.Services;
using NI.Services.Agent;
using NI.Services.Media;
using NI.Services.Notifications;
using NI.Services.SystemMonitoring;
using Windows.System;

namespace NI.ViewModels;

public sealed partial class IslandVM : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly AppSettings _settings;
    private readonly INotificationProvider _provider;
    private readonly ISystemMonitor _monitor;
    private readonly IMediaService _media;
    private (bool HasSession, bool Playing, string Title, string Artist, MediaSourceKind Source, BitmapImage? Artwork)? _lastMediaPresentation;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _clockTimer, _previewTimer, _saveTimer;
    private bool _disposed, _started, _weatherLoading, _connectionsActive;
    private readonly WeatherService _weather = new();
    private DateTime _weatherUpdated = DateTime.MinValue;
    private string _weatherText = "Konum ayarla";
    public string WeatherText { get => _weatherText; private set => Set(ref _weatherText, value); }
    public string WeatherLatitude { get => _settings.WeatherLatitude; set { _settings.WeatherLatitude = value; Raise(); _weatherUpdated = DateTime.MinValue; QueueSave(); _ = UpdateWeatherAsync(); } }
    public string WeatherLongitude { get => _settings.WeatherLongitude; set { _settings.WeatherLongitude = value; Raise(); _weatherUpdated = DateTime.MinValue; QueueSave(); _ = UpdateWeatherAsync(); } }
    public AppearanceSettings Appearance => _settings.Appearance;
    public NotificationRouter Notifications { get; }
    public QuickControlsVM QuickControls { get; }
    public Array Themes { get; } = Enum.GetValues<GlassTheme>();
    public Array Materials { get; } = Enum.GetValues<GlassMaterial>();
    public string ClockText => DateTime.Now.ToString("HH:mm");
    public string DateText => DateTime.Now.ToString("d MMMM, dddd", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
    public string NotificationOpenHint => $"Uygulamayı aç; özgün bildirim eylemi için {NotificationActionHandler.NotificationShortcut}";
    public string NotificationStatus => _provider.StatusMessage;
    public bool NotificationsConnected => _provider.Access == NotificationAccess.Listening;
    public bool HasNotifications => Notifications.Items.Count > 0;
    public string NotificationCountText => Notifications.UnreadCount == 0 ? "Güncel" : $"{Notifications.UnreadCount} okunmamış";
    public string EmptyTitle => NotificationsConnected ? "Her şey güncel" : "Bildirimlerin tek bir yerde";
    public string EmptyDescription => NotificationsConnected ? "Yeni Windows bildirimleri burada görünür." : NotificationStatus;
    public string ConnectLabel => _provider.Access == NotificationAccess.Denied ? "Windows izinlerini aç" : "Bildirimleri bağla";

    private NotificationItem? _preview;
    public NotificationItem? Preview { get => _preview; private set { if (Set(ref _preview, value)) UpdateSummary(); } }
    public bool HasPreview => Appearance.ShowNotificationSummary && Preview != null;
    public bool ShowCompactMedia => !HasPreview && !HasTaskSummary && !HasAlarmAlert && Appearance.ShowMediaSummary && _media.HasSession;
    public string SummaryTitle => HasPreview ? Preview!.Title : HasAlarmAlert ? AlarmAlert : HasTaskSummary ? TaskSummary : ShowCompactMedia ? MediaTitle : "Notification Island";
    public string SummarySubtitle => HasPreview ? Preview!.AppName : HasAlarmAlert ? "Notification Island alarmı" : HasTaskSummary ? "Yerel görev" : ShowCompactMedia ? MediaArtist : "";
    public string SummaryIcon => HasPreview ? "Notification" : HasAlarmAlert ? "Notification" : HasTaskSummary ? "Assistant" : ShowCompactMedia ? "Music" : "Assistant";
    public byte[]? PreviewIcon => HasPreview ? Preview?.IconBytes : null;
    public bool HasPreviewIcon => PreviewIcon is { Length: > 0 };

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; private set { if (Set(ref _statusMessage, value)) Raise(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);
    private SystemMetrics _metrics = new(null, null, null, null, null);
    public string CpuText => Percent(_metrics.Cpu);
    public string GpuText => Percent(_metrics.Gpu);
    public string RamText => Percent(_metrics.Ram);
    public double CpuValue => _metrics.Cpu ?? 0;
    public double GpuValue => _metrics.Gpu ?? 0;
    public double RamValue => _metrics.Ram ?? 0;
    public string CpuDetail => _metrics.Cpu.HasValue ? "İşlemci" : "Ölçülüyor";
    public string GpuDetail => _metrics.Gpu.HasValue ? "3D motoru" : "Veri yok";
    public string RamDetail => _metrics.TotalGb.HasValue ? $"{_metrics.UsedGb:0.0} / {_metrics.TotalGb:0.#} GB" : "Veri yok";
    private static string Percent(double? value) => value.HasValue ? $"{Math.Round(value.Value):0}%" : "N/A";
    public bool HasMedia => _media.HasSession;
    public string MediaTitle => HasMedia ? _media.TrackTitle : "Biraz müzik iyi gelir";
    public string MediaArtist => HasMedia ? _media.ArtistName : "Spotify veya başka bir oynatıcı açın";
    public BitmapImage? AlbumArtwork => _media.AlbumArtwork;
    public MediaSourceKind MediaSource => _media.SourceKind;
    public string MediaSourceName => MediaSource switch { MediaSourceKind.Spotify => "Spotify", MediaSourceKind.YouTube => "YouTube", _ => "Medya" };
    public bool IsMediaPlaying => _media.HasSession && _media.IsPlaying;
    public bool ShowMediaBrand => ShowCompactMedia && !HasPreview;
    public string PlayIcon => _media.IsPlaying ? "Pause" : "Play";
    public string PlayLabel => _media.IsPlaying ? "Duraklat" : "Oynat";
    public double MediaProgress => _media.TotalDuration.TotalSeconds > 0 ? Math.Clamp(100 * _media.CurrentPosition.TotalSeconds / _media.TotalDuration.TotalSeconds, 0, 100) : 0;
    public string MediaPosition => FormatMediaTime(_media.CurrentPosition);
    public string MediaDuration => FormatMediaTime(_media.TotalDuration);
    private static string FormatMediaTime(TimeSpan time) => $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    private string _assistantInput = "", _assistantResponse = "Masaüstü düzenleme, geri alma, alarm veya sistem bilgisi isteyin.";
    private bool _isAssistantBusy;
    public bool AssistantEnabled => _settings.EnableAiAssistant;
    public string AssistantInput { get => _assistantInput; set => Set(ref _assistantInput, value); }
    public string AssistantResponse { get => _assistantResponse; private set => Set(ref _assistantResponse, value); }
    public bool IsAssistantBusy { get => _isAssistantBusy; private set { if (Set(ref _isAssistantBusy, value)) { Raise(nameof(HasTaskSummary)); RefreshTaskPresentation(); } } }
    public string OllamaBaseUrl { get => _settings.OllamaBaseUrl; set { _settings.OllamaBaseUrl = value; Raise(); QueueSave(); } }
    public string OllamaModel { get => _settings.OllamaModel; set { _settings.OllamaModel = value; Raise(); QueueSave(); } }
    public string MaterialStatus { get; private set; } = "Sistem materyali denetleniyor";
    public void SetMaterialStatus(string message) { MaterialStatus = message; Raise(nameof(MaterialStatus)); }

    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleSettingsCommand { get; }
    public ICommand ToggleAssistantCommand { get; }
    public ICommand OpenPreviewCommand { get; }
    public ICommand OpenNotificationCommand { get; }
    public ICommand MarkReadCommand { get; }
    public ICommand ConnectNotificationsCommand { get; }
    public ICommand TogglePlaybackCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand AskCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand DismissStatusCommand { get; }

    public IslandVM(AppSettings settings, INotificationProvider provider, ISystemMonitor monitor,
        IMediaService media, INotificationActionHandler actions, ILocalAgentCoordinator? agent = null)
    {
        _dispatcher = Dispatcher.CurrentDispatcher; _settings = settings;
        _statusMessage = settings.LoadWarning ?? ""; _provider = provider; _monitor = monitor; _media = media;
        Notifications = new NotificationRouter(actions);
        QuickControls = new QuickControlsVM(message => StatusMessage = message);
        ToggleExpandedCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
        ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
        ToggleAssistantCommand = new RelayCommand(_ => IsTaskInputOpen = !IsTaskInputOpen);
        OpenPreviewCommand = new AsyncCommand(async _ =>
        {
            if (HasPreview && Preview != null) await OpenNotificationAsync(Preview);
            else IsExpanded = !IsExpanded;
        }, ReportError);
        OpenNotificationCommand = new AsyncCommand(async item => { if (item is NotificationItem notification) await OpenNotificationAsync(notification); }, ReportError);
        MarkReadCommand = new RelayCommand(_ => { Notifications.MarkAllRead(); Preview = null; });
        ConnectNotificationsCommand = new AsyncCommand(async _ =>
        {
            if (_provider.Access == NotificationAccess.Denied) await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-notifications"));
            else await _provider.StartAsync(requestPermission: true);
        }, ReportError);
        TogglePlaybackCommand = new AsyncCommand(async _ => await MediaActionAsync(_media.TogglePlayPauseAsync), ReportError);
        PreviousCommand = new AsyncCommand(async _ => await MediaActionAsync(_media.TrySkipPreviousAsync), ReportError);
        NextCommand = new AsyncCommand(async _ => await MediaActionAsync(_media.TrySkipNextAsync), ReportError);
        AskCommand = new AsyncCommand(async _ => await RunAgentAsync(), ReportError);
        InitializeAgentCommands();
        InitializePresentationCommands();
        ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());
        DismissStatusCommand = new RelayCommand(_ => StatusMessage = "");
        _clockTimer = new DispatcherTimer { Interval = UntilNextMinute() };
        _clockTimer.Tick += OnClock;
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
        _previewTimer.Tick += OnPreviewExpired;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += OnSave;
        _provider.SnapshotChanged += OnSnapshot; _provider.AccessChanged += OnAccessChanged;
        Notifications.Arrived += OnArrived; Notifications.Changed += OnNotificationsChanged;
        _media.Changed += OnMediaChanged; _monitor.Updated += OnMetrics;
        Appearance.PropertyChanged += OnAppearanceChanged;
        if (agent != null) AttachAgent(agent);
    }

    public async Task StartAsync()
    {
        if (_started) return; _started = true;
        InitializeAgentServices();
        _clockTimer.Start();
        await _provider.StartAsync();
        if (_disposed) return;
        if (_settings.EnableMediaControls) await _media.InitializeAsync();
        if (_disposed) return;
        _connectionsActive = false;
        UpdateActivity();
        UpdateSummary();
        await UpdateWeatherAsync();
    }
    public async Task RefreshAccessAsync()
    {
        if (_started && !_disposed) await _provider.StartAsync();
    }
    private void UpdateActivity()
    {
        var dashboard = IsExpanded && IsDashboardVisible;
        _monitor.SetActive(dashboard && Appearance.ShowSystemStatus && _settings.EnableSystemMonitor);
        _media.SetProgressVisible(dashboard && Appearance.ShowMedia && _settings.EnableMediaControls);
        var connectionsActive = dashboard && Appearance.ShowConnections;
        if (_started && connectionsActive && !_connectionsActive) _ = InitializeQuickControlsAsync();
        if (!connectionsActive && _connectionsActive) QuickControls.Suspend();
        _connectionsActive = connectionsActive;
    }
    private async Task InitializeQuickControlsAsync()
    {
        try { await QuickControls.InitializeAsync(); if (!_disposed && _connectionsActive) await QuickControls.RefreshAsync(); }
        catch (Exception ex) { OnUi(() => StatusMessage = "Bağlantı kontrolleri açılamadı: " + ex.GetType().Name); }
    }
    private async Task OpenNotificationAsync(NotificationItem item)
    {
        var result = await Notifications.OpenAsync(item);
        StatusMessage = result.Message;
        Preview = null;
        if (result.Accepted) IsExpanded = false;
    }
    private async Task MediaActionAsync(Func<Task<bool>> action)
    {
        if (!await action()) StatusMessage = "Oynatıcı bu medya komutunu desteklemiyor.";
    }
    private void OnSnapshot(object? sender, NotificationSnapshot snapshot) => OnUi(() => Notifications.Synchronize(snapshot));
    private void OnAccessChanged(object? sender, EventArgs args) => OnUi(() =>
    {
        foreach (var name in new[] { nameof(NotificationStatus), nameof(NotificationsConnected), nameof(EmptyTitle), nameof(EmptyDescription), nameof(ConnectLabel) }) Raise(name);
    });
    private void OnArrived(object? sender, NotificationItem item)
    {
        Preview = item;
        _previewTimer.Stop(); _previewTimer.Start();
    }
    private void OnNotificationsChanged(object? sender, EventArgs e)
    {
        if (Preview != null && !Notifications.Items.Any(x => x.Id == Preview.Id)) Preview = null;
        Raise(nameof(HasNotifications)); Raise(nameof(NotificationCountText));
        RefreshNotificationPage();
    }
    private void OnMediaChanged(object? sender, EventArgs e) => OnUi(() =>
    {
        foreach (var name in new[] { nameof(MediaProgress), nameof(MediaPosition), nameof(MediaDuration) }) Raise(name);
        var presentation = (_media.HasSession, _media.IsPlaying, _media.TrackTitle, _media.ArtistName, _media.SourceKind, _media.AlbumArtwork);
        if (_lastMediaPresentation == presentation) return;
        _lastMediaPresentation = presentation;
        foreach (var name in new[] { nameof(HasMedia), nameof(MediaTitle), nameof(MediaArtist), nameof(AlbumArtwork), nameof(PlayIcon), nameof(PlayLabel), nameof(MediaSource), nameof(MediaSourceName), nameof(IsMediaPlaying) }) Raise(name);
        UpdateSummary();
    });
    private void OnMetrics(object? sender, SystemMetrics metrics) => OnUi(() =>
    {
        if (!IsExpanded) return;
        _metrics = metrics;
        foreach (var name in new[] { nameof(CpuText), nameof(GpuText), nameof(RamText), nameof(CpuValue), nameof(GpuValue), nameof(RamValue), nameof(CpuDetail), nameof(GpuDetail), nameof(RamDetail) }) Raise(name);
    });
    private void OnAppearanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var name in new[] { nameof(PanelWidth), nameof(CompactHeight), nameof(BodyMaxHeight), nameof(HasUtilitySections), nameof(WindowCornerRadius), nameof(SurfaceCornerRadius) }) Raise(name);
        UpdateSummary(); UpdateActivity(); QueueSave();
        if (e.PropertyName == nameof(Appearance.ShowWeather)) _ = UpdateWeatherAsync();
    }
    private void UpdateSummary()
    {
        foreach (var name in new[] { nameof(HasPreview), nameof(ShowCompactMedia), nameof(ShowMediaBrand), nameof(SummaryTitle), nameof(SummarySubtitle), nameof(SummaryIcon), nameof(PreviewIcon), nameof(HasPreviewIcon) }) Raise(name);
    }
    private void QueueSave() { _saveTimer.Stop(); _saveTimer.Start(); }
    private void OnSave(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        if (!_settings.Save()) StatusMessage = "Ayarlar kaydedilemedi. Uygulama veri klasörünün yazma iznini kontrol edin.";
    }
    private static TimeSpan UntilNextMinute() => TimeSpan.FromMilliseconds(60000 - DateTime.Now.TimeOfDay.TotalMilliseconds % 60000);
    private void OnClock(object? sender, EventArgs e) { Raise(nameof(ClockText)); Raise(nameof(DateText)); _clockTimer.Interval = UntilNextMinute(); _ = UpdateWeatherAsync(); }
    private async Task UpdateWeatherAsync()
    {
        if (_disposed || !Appearance.ShowWeather || _weatherLoading || DateTime.UtcNow - _weatherUpdated < TimeSpan.FromMinutes(30)) return;
        _weatherLoading = true;
        try
        {
            WeatherText = await _weather.ReadAsync(WeatherLatitude, WeatherLongitude, _lifetime.Token) ?? "Konum ayarla";
            _weatherUpdated = DateTime.UtcNow;
        }
        catch (OperationCanceledException) { }
        catch { WeatherText = "Hava durumu yok"; _weatherUpdated = DateTime.UtcNow.AddMinutes(-25); }
        finally { _weatherLoading = false; }
    }
    private void OnPreviewExpired(object? sender, EventArgs e) { _previewTimer.Stop(); Preview = null; }
    private void ReportError(Exception ex) => StatusMessage = $"İşlem tamamlanamadı: {ex.Message}";
    private void OnUi(Action action)
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(new Action(() => { if (!_disposed) action(); }));
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _lifetime.Cancel(); _clockTimer.Stop(); _previewTimer.Stop(); _saveTimer.Stop();
        Appearance.PropertyChanged -= OnAppearanceChanged;
        _provider.SnapshotChanged -= OnSnapshot; _provider.AccessChanged -= OnAccessChanged;
        Notifications.Arrived -= OnArrived; Notifications.Changed -= OnNotificationsChanged;
        _media.Changed -= OnMediaChanged; _monitor.Updated -= OnMetrics;
        _provider.Dispose(); _monitor.Dispose(); _media.Dispose(); QuickControls.Dispose();
        DisposeAgentServices();
        _weather.Dispose(); _lifetime.Dispose(); _settings.Save();
    }
}
