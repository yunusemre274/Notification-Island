using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NI.Services.Media;
using Windows.Media.Control;

namespace NI.Services;

public sealed class MediaSessionService : IMediaService
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _disposed, _progressVisible;
    private long _generation;
    public bool HasSession => _session != null;
    public bool IsPlaying { get; private set; }
    public string SourceAppUserModelId { get; private set; } = "";
    public MediaSourceKind SourceKind { get; private set; }
    public string TrackTitle { get; private set; } = "";
    public string ArtistName { get; private set; } = "";
    public BitmapImage? AlbumArtwork { get; private set; }
    public TimeSpan CurrentPosition { get; private set; }
    public TimeSpan TotalDuration { get; private set; }
    public event EventHandler? Changed;

    public MediaSessionService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }
    public async Task InitializeAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed) return;
            _manager = manager;
            _manager.SessionsChanged += OnSessionsChanged;
            _manager.CurrentSessionChanged += OnCurrentChanged;
            await SelectSessionAsync();
        }
        catch (Exception ex) { Debug.WriteLine($"Media unavailable: {ex.Message}"); }
    }
    private async Task SelectSessionAsync()
    {
        if (_disposed || _manager == null) return;
        DetachSession();
        var sessions = _manager.GetSessions();
        _session = _manager.GetCurrentSession() ?? sessions.FirstOrDefault();
        if (_session != null)
        {
            _session.MediaPropertiesChanged += OnMediaChanged;
            _session.PlaybackInfoChanged += OnPlaybackChanged;
            _session.TimelinePropertiesChanged += OnTimelineChanged;
        }
        await RefreshAsync();
    }
    private async Task RefreshAsync()
    {
        var generation = ++_generation;
        var session = _session;
        try
        {
            if (session == null)
            {
                TrackTitle = ArtistName = ""; AlbumArtwork = null;
                CurrentPosition = TotalDuration = TimeSpan.Zero; IsPlaying = false;
                SourceAppUserModelId = ""; SourceKind = MediaSourceKind.Generic;
            }
            else
            {
                var properties = await session.TryGetMediaPropertiesAsync();
                if (_disposed || generation != _generation) return;
                TrackTitle = properties.Title ?? ""; ArtistName = properties.Artist ?? "";
                SourceAppUserModelId = session.SourceAppUserModelId ?? "";
                SourceKind = MediaSourceClassifier.Classify(SourceAppUserModelId, properties.AlbumTitle, properties.AlbumArtist);
                AlbumArtwork = null;
                if (_progressVisible && properties.Thumbnail != null)
                {
                    using var stream = await properties.Thumbnail.OpenReadAsync();
                    if (_disposed || generation != _generation) return;
                    if (stream.Size <= 524288)
                    {
                    var image = new BitmapImage();
                    image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 96; image.DecodePixelHeight = 96; image.StreamSource = stream.AsStreamForRead();
                    image.EndInit(); image.Freeze(); AlbumArtwork = image;
                    }
                }
                IsPlaying = session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                ReadTimeline();
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Media refresh: {ex.Message}"); }
        if (_disposed || generation != _generation) return;
        UpdateTimer(); Changed?.Invoke(this, EventArgs.Empty);
    }
    private void ReadTimeline()
    {
        if (_session == null) return;
        var timeline = _session.GetTimelineProperties();
        TotalDuration = timeline.EndTime - timeline.StartTime;
        var position = timeline.Position - timeline.StartTime;
        if (IsPlaying) position += DateTimeOffset.Now - timeline.LastUpdatedTime;
        CurrentPosition = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, Math.Max(0, TotalDuration.TotalSeconds)));
    }
    private void OnTick(object? sender, EventArgs e)
    {
        try { ReadTimeline(); Changed?.Invoke(this, EventArgs.Empty); }
        catch (Exception ex) { Debug.WriteLine($"Media timeline: {ex.Message}"); }
    }
    private void Queue(Func<Task> work) => _dispatcher.BeginInvoke(new Action(async () =>
    {
        if (_disposed) return;
        try { await work(); } catch (Exception ex) { Debug.WriteLine($"Media event: {ex.Message}"); }
    }));
    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => Queue(SelectSessionAsync);
    private void OnCurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => Queue(SelectSessionAsync);
    private void OnMediaChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => Queue(RefreshAsync);
    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => Queue(RefreshAsync);
    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => Queue(() => { OnTick(null, EventArgs.Empty); return Task.CompletedTask; });
    public void SetProgressVisible(bool visible)
    {
        if (_progressVisible == visible) return;
        _progressVisible = visible; UpdateTimer();
        if (visible && HasSession) Queue(RefreshAsync);
        else if (!visible && AlbumArtwork != null) { AlbumArtwork = null; Changed?.Invoke(this, EventArgs.Empty); }
    }
    private void UpdateTimer() { if (_progressVisible && IsPlaying && !_disposed) _timer.Start(); else _timer.Stop(); }
    public Task<bool> TryPauseAsync() => ControlAsync(s => s.TryPauseAsync().AsTask());
    public Task<bool> TogglePlayPauseAsync() => ControlAsync(s => s.TryTogglePlayPauseAsync().AsTask());
    public Task<bool> TrySkipPreviousAsync() => ControlAsync(s => s.TrySkipPreviousAsync().AsTask());
    public Task<bool> TrySkipNextAsync() => ControlAsync(s => s.TrySkipNextAsync().AsTask());
    private async Task<bool> ControlAsync(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
    {
        try { return _session != null && await action(_session); }
        catch (Exception ex) { Debug.WriteLine($"Media control: {ex.Message}"); return false; }
    }
    private void DetachSession()
    {
        if (_session == null) return;
        _session.MediaPropertiesChanged -= OnMediaChanged;
        _session.PlaybackInfoChanged -= OnPlaybackChanged;
        _session.TimelinePropertiesChanged -= OnTimelineChanged;
    }
    public void Dispose()
    {
        _disposed = true; ++_generation; _timer.Stop(); _timer.Tick -= OnTick;
        DetachSession();
        if (_manager == null) return;
        _manager.SessionsChanged -= OnSessionsChanged;
        _manager.CurrentSessionChanged -= OnCurrentChanged;
    }
}
