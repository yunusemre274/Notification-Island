using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace NI.Services
{
    /// <summary>
    /// MediaSessionService with album artwork, position tracking, and playback controls
    /// </summary>
    public class MediaSessionService : IDisposable
    {
        public bool IsSpotifyPlaying { get; private set; }
        public string TrackTitle { get; private set; } = "";
        public string ArtistName { get; private set; } = "";
        public BitmapImage? AlbumArtwork { get; private set; }
        public TimeSpan CurrentPosition { get; private set; }
        public TimeSpan TotalDuration { get; private set; }

        public event EventHandler<bool>? PlaybackStateChanged;
        public event EventHandler? MediaPropertiesChanged;
        public event EventHandler? PositionChanged;

        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private DispatcherTimer? _positionTimer;

        public async Task InitializeAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                _sessionManager.SessionsChanged += OnSessionsChanged;
                await UpdateCurrentSessionAsync();

                // Start position timer (updates every second when playing)
                _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _positionTimer.Tick += OnPositionTimerTick;
                _positionTimer.Start();

                Debug.WriteLine("[MediaSessionService] Initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] Init failed: {ex.Message}");
            }
        }

        private async Task UpdateCurrentSessionAsync()
        {
            try
            {
                if (_sessionManager == null) return;

                // Unsubscribe from old session
                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                }

                // SIMPLE: Enumerate ALL sessions, pick Spotify
                var sessions = _sessionManager.GetSessions();
                _currentSession = sessions.FirstOrDefault(s =>
                    s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase));

                if (_currentSession != null)
                {
                    Debug.WriteLine($"[MediaSessionService] Spotify found: {_currentSession.SourceAppUserModelId}");

                    // Subscribe to events
                    _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;

                    // Get initial state
                    var playbackInfo = _currentSession.GetPlaybackInfo();
                    IsSpotifyPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                    // Get track info
                    var mediaProps = await _currentSession.TryGetMediaPropertiesAsync();
                    if (mediaProps != null)
                    {
                        TrackTitle = mediaProps.Title ?? "";
                        ArtistName = mediaProps.Artist ?? "";

                        // Get album artwork
                        await LoadAlbumArtworkAsync(mediaProps);

                        Debug.WriteLine($"[MediaSessionService] Track: {TrackTitle} by {ArtistName}");
                    }

                    // Get timeline properties for duration
                    var timeline = _currentSession.GetTimelineProperties();
                    CurrentPosition = timeline.Position;
                    TotalDuration = timeline.EndTime;
                }
                else
                {
                    Debug.WriteLine("[MediaSessionService] No Spotify session");
                    IsSpotifyPlaying = false;
                    TrackTitle = "";
                    ArtistName = "";
                    AlbumArtwork = null;
                    CurrentPosition = TimeSpan.Zero;
                    TotalDuration = TimeSpan.Zero;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] UpdateSession failed: {ex.Message}");
            }
        }

        private async void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            Debug.WriteLine("[MediaSessionService] Sessions changed");
            await UpdateCurrentSessionAsync();
        }

        private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, Windows.Media.Control.MediaPropertiesChangedEventArgs args)
        {
            try
            {
                var mediaProps = await sender.TryGetMediaPropertiesAsync();
                if (mediaProps != null)
                {
                    TrackTitle = mediaProps.Title ?? "";
                    ArtistName = mediaProps.Artist ?? "";

                    // Get album artwork
                    await LoadAlbumArtworkAsync(mediaProps);

                    // Get timeline properties for duration
                    var timeline = sender.GetTimelineProperties();
                    CurrentPosition = timeline.Position;
                    TotalDuration = timeline.EndTime;

                    Debug.WriteLine($"[MediaSessionService] Track changed: {TrackTitle}");
                    MediaPropertiesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] MediaPropertiesChanged failed: {ex.Message}");
            }
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            try
            {
                var wasPlaying = IsSpotifyPlaying;
                IsSpotifyPlaying = sender.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                if (wasPlaying != IsSpotifyPlaying)
                {
                    Debug.WriteLine($"[MediaSessionService] Playback: {(IsSpotifyPlaying ? "Playing" : "Paused")}");
                    PlaybackStateChanged?.Invoke(this, IsSpotifyPlaying);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] PlaybackInfoChanged failed: {ex.Message}");
            }
        }

        public async Task<bool> TogglePlayPauseAsync()
        {
            try
            {
                if (_currentSession == null) return false;

                if (IsSpotifyPlaying)
                {
                    return await _currentSession.TryPauseAsync();
                }
                else
                {
                    return await _currentSession.TryPlayAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] TogglePlayPause failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TrySkipPreviousAsync()
        {
            try
            {
                if (_currentSession == null) return false;
                return await _currentSession.TrySkipPreviousAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] SkipPrevious failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TrySkipNextAsync()
        {
            try
            {
                if (_currentSession == null) return false;
                return await _currentSession.TrySkipNextAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] SkipNext failed: {ex.Message}");
                return false;
            }
        }

        private async Task LoadAlbumArtworkAsync(GlobalSystemMediaTransportControlsSessionMediaProperties mediaProps)
        {
            try
            {
                if (mediaProps.Thumbnail == null)
                {
                    AlbumArtwork = null;
                    return;
                }

                using (var stream = await mediaProps.Thumbnail.OpenReadAsync())
                {
                    var bitmap = new BitmapImage();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream.AsStreamForRead();
                        bitmap.EndInit();
                        bitmap.Freeze();
                        AlbumArtwork = bitmap;
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] LoadAlbumArtwork failed: {ex.Message}");
                AlbumArtwork = null;
            }
        }

        private void OnPositionTimerTick(object? sender, EventArgs e)
        {
            try
            {
                if (_currentSession == null || !IsSpotifyPlaying)
                    return;

                var timeline = _currentSession.GetTimelineProperties();
                CurrentPosition = timeline.Position;
                TotalDuration = timeline.EndTime;

                PositionChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] PositionTimer failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                _positionTimer?.Stop();
                _positionTimer = null;

                if (_sessionManager != null)
                {
                    _sessionManager.SessionsChanged -= OnSessionsChanged;
                }

                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaSessionService] Dispose failed: {ex.Message}");
            }
        }
    }
}
