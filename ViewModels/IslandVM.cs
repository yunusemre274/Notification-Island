using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NI.Models;
using NI.Services;
using Windows.Media.Control;

namespace NI.ViewModels
{
    /// <summary>
    /// Lightweight ViewModel for Dynamic Island.
    /// OPTIMIZED: Single consolidated timer for all periodic checks.
    /// Priority: System > HeadphoneBanner > Spotify > ActiveWindow > Smart > Idle
    /// </summary>
    public class IslandVM : INotifyPropertyChanged
    {
        #region Consolidated Timer

        private DispatcherTimer? _mainTimer;
        private int _tickCount = 0;
        private bool _isVisible = true;

        #endregion

        #region Clock

        private string _clockText = DateTime.Now.ToString("HH:mm");
        public string ClockText
        {
            get => _clockText;
            private set { if (_clockText != value) { _clockText = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Notification/Event State

        private string _appName = "";
        public string AppName
        {
            get => _appName;
            private set { if (_appName != value) { _appName = value; OnPropertyChanged(); } }
        }

        private string _notificationText = "";
        public string NotificationText
        {
            get => _notificationText;
            private set { if (_notificationText != value) { _notificationText = value; OnPropertyChanged(); } }
        }

        private string _compactText = "Hazır";
        public string CompactText
        {
            get => _compactText;
            private set { if (_compactText != value) { _compactText = value; OnPropertyChanged(); } }
        }

        private string _eventIcon = "✨";
        public string EventIcon
        {
            get => _eventIcon;
            private set { if (_eventIcon != value) { _eventIcon = value; OnPropertyChanged(); } }
        }

        private BitmapImage? _appIcon;
        public BitmapImage? AppIcon
        {
            get => _appIcon;
            private set { _appIcon = value; OnPropertyChanged(); }
        }

        private bool _hasNotification = false;
        public bool HasNotification
        {
            get => _hasNotification;
            private set { if (_hasNotification != value) { _hasNotification = value; OnPropertyChanged(); } }
        }

        private SmartEventPriority _currentPriority = SmartEventPriority.Idle;
        public SmartEventPriority CurrentPriority => _currentPriority;

        #endregion

        #region Spotify State (SINGLE SOURCE OF TRUTH)

        // CRITICAL: IsSpotifyActive = true → ONLY Spotify UI visible
        //          IsSpotifyActive = false → ONLY Active Window UI visible
        private bool _isSpotifyActive = false;
        public bool IsSpotifyActive
        {
            get => _isSpotifyActive;
            private set
            {
                if (_isSpotifyActive != value)
                {
                    _isSpotifyActive = value;
                    OnPropertyChanged();
                    System.Diagnostics.Debug.WriteLine($"[IslandVM] IsSpotifyActive = {value}");
                }
            }
        }

        private bool _isSpotifyPlaying = false;
        public bool IsSpotifyPlaying
        {
            get => _isSpotifyPlaying;
            private set
            {
                if (_isSpotifyPlaying != value)
                {
                    _isSpotifyPlaying = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _spotifySong = "";
        public string SpotifySong
        {
            get => _spotifySong;
            private set { if (_spotifySong != value) { _spotifySong = value; OnPropertyChanged(); } }
        }

        private string _spotifyArtist = "";
        public string SpotifyArtist
        {
            get => _spotifyArtist;
            private set { if (_spotifyArtist != value) { _spotifyArtist = value; OnPropertyChanged(); } }
        }

        private BitmapImage? _albumArtwork;
        public BitmapImage? AlbumArtwork
        {
            get => _albumArtwork;
            private set { if (_albumArtwork != value) { _albumArtwork = value; OnPropertyChanged(); } }
        }

        private TimeSpan _currentPosition;
        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            private set { if (_currentPosition != value) { _currentPosition = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionText)); OnPropertyChanged(nameof(ProgressPercentage)); } }
        }

        private TimeSpan _totalDuration;
        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            private set { if (_totalDuration != value) { _totalDuration = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationText)); OnPropertyChanged(nameof(ProgressPercentage)); } }
        }

        public string PositionText => FormatTime(CurrentPosition);
        public string DurationText => FormatTime(TotalDuration);
        public double ProgressPercentage => TotalDuration.TotalSeconds > 0 ? (CurrentPosition.TotalSeconds / TotalDuration.TotalSeconds) * 100 : 0;

        private string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        #endregion

        #region Headphone Banner State

        private bool _showHeadphoneBanner = false;
        public bool ShowHeadphoneBanner
        {
            get => _showHeadphoneBanner;
            private set { if (_showHeadphoneBanner != value) { _showHeadphoneBanner = value; OnPropertyChanged(); } }
        }

        private string _headphoneDeviceName = "";
        public string HeadphoneDeviceName
        {
            get => _headphoneDeviceName;
            private set { if (_headphoneDeviceName != value) { _headphoneDeviceName = value; OnPropertyChanged(); } }
        }

        private DispatcherTimer? _headphoneBannerTimer;

        #endregion

        #region Active Window State

        private bool _showActiveWindow = false;
        public bool ShowActiveWindow
        {
            get => _showActiveWindow;
            private set { if (_showActiveWindow != value) { _showActiveWindow = value; OnPropertyChanged(); } }
        }

        private string _activeWindowName = "";
        public string ActiveWindowName
        {
            get => _activeWindowName;
            private set { if (_activeWindowName != value) { _activeWindowName = value; OnPropertyChanged(); } }
        }

        private string _activeWindowIcon = "App";
        public string ActiveWindowIcon
        {
            get => _activeWindowIcon;
            private set { if (_activeWindowIcon != value) { _activeWindowIcon = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Settings

        private AppSettings _settings = new();

        public bool AutostartEnabled
        {
            get => _settings.AutostartEnabled;
            set
            {
                if (_settings.AutostartEnabled != value)
                {
                    _settings.AutostartEnabled = value;
                    _settings.Save();
                    SetAutostart(value);
                    OnPropertyChanged();
                }
            }
        }

        public bool SoundEnabled
        {
            get => _settings.SoundEnabled;
            set
            {
                if (_settings.SoundEnabled != value)
                {
                    _settings.SoundEnabled = value;
                    _settings.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowNationalEvents
        {
            get => _settings.ShowNationalEvents;
            set
            {
                if (_settings.ShowNationalEvents != value)
                {
                    _settings.ShowNationalEvents = value;
                    _settings.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowGlobalEvents
        {
            get => _settings.ShowGlobalEvents;
            set
            {
                if (_settings.ShowGlobalEvents != value)
                {
                    _settings.ShowGlobalEvents = value;
                    _settings.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool IdleMessagesEnabled
        {
            get => _settings.IdleMessagesEnabled;
            set
            {
                if (_settings.IdleMessagesEnabled != value)
                {
                    _settings.IdleMessagesEnabled = value;
                    _settings.Save();
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Services

        private NotificationService? _notificationService;
        private SmartEventService? _smartEventService;
        private MediaSessionService? _mediaSessionService;
        private ActiveWindowService? _activeWindowService;

        #endregion

        #region Events

        public event EventHandler? NotificationArrived;
        public event EventHandler? SmartEventArrived;
        public event EventHandler? SpotifyChanged;
        public event EventHandler? HeadphoneBannerArrived;
        public event EventHandler? ActiveWindowChanged;

        #endregion

        public IslandVM()
        {
            LoadSettings();
        }

        public void Start()
        {
            // System notification service (event-driven, no polling)
            _notificationService = new NotificationService();
            _notificationService.NotificationReceived += OnSystemNotification;
            _notificationService.Start();

            // Smart event service (no internal timer - we call it)
            _smartEventService = new SmartEventService(_settings);
            _smartEventService.SmartEventGenerated += OnSmartEvent;

            // Media session service (SINGLE SOURCE OF TRUTH for Spotify)
            _mediaSessionService = new MediaSessionService();
            _mediaSessionService.PlaybackStateChanged += OnPlaybackStateChanged;
            _mediaSessionService.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _mediaSessionService.PositionChanged += OnPositionChanged;
            _ = _mediaSessionService.InitializeAsync(); // Fire and forget - intentional

            // Active window service
            _activeWindowService = new ActiveWindowService();
            _activeWindowService.ActiveWindowChanged += OnActiveWindowChanged;

            // Headphone connection detection
            AudioService.HeadphoneConnected += OnHeadphoneConnected;

            // SINGLE CONSOLIDATED TIMER - 3 second interval
            // Handles: Clock, Spotify, Active Window, Smart Events, Audio device check
            _mainTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _mainTimer.Tick += OnMainTimerTick;
            _mainTimer.Start();

            // Initial state - show active window instead of smart events
            UpdateActiveWindow();
        }

        private void OnMainTimerTick(object? sender, EventArgs e)
        {
            if (!_isVisible) return;

            _tickCount++;

            // Clock update every 30 seconds (every 10 ticks)
            if (_tickCount % 10 == 0)
            {
                ClockText = DateTime.Now.ToString("HH:mm");
            }

            // Spotify is handled by events, no timer updates needed

            // Active window check every tick (3s) - only if not showing higher priority content
            if (_currentPriority == SmartEventPriority.Idle || _currentPriority == SmartEventPriority.Smart)
            {
                UpdateActiveWindow();
            }

            // Audio device change check every 2 ticks (6s)
            if (_tickCount % 2 == 0)
            {
                AudioService.CheckDeviceChange();
            }

            // Smart events check every minute (every 20 ticks)
            if (_tickCount % 20 == 0)
            {
                _smartEventService?.CheckSmartEvents();
            }
        }

        private void UpdateActiveWindow()
        {
            var info = _activeWindowService?.CheckActiveWindow();
            if (info != null && !info.IsDesktop)
            {
                // SIMPLIFIED: Don't override Spotify (single source of truth) or higher priority items
                if (_currentPriority <= SmartEventPriority.Smart && !ShowHeadphoneBanner && !IsSpotifyActive)
                {
                    ShowActiveWindow = true;
                    ActiveWindowName = info.AppName;
                    ActiveWindowIcon = info.IconKey;
                    CompactText = info.AppName;
                }
            }
        }

        private void OnActiveWindowChanged(object? sender, ActiveWindowInfo e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // SIMPLIFIED: Use single source of truth IsSpotifyActive
                if (!ShowHeadphoneBanner && !IsSpotifyActive && _currentPriority != SmartEventPriority.System)
                {
                    ShowActiveWindow = true;
                    ActiveWindowName = e.AppName;
                    ActiveWindowIcon = e.IconKey;
                    CompactText = e.AppName;
                    ActiveWindowChanged?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        private void OnHeadphoneConnected(object? sender, HeadphoneConnectedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Show headphone banner (high priority, below system notifications)
                ShowHeadphoneBanner = true;
                HeadphoneDeviceName = e.DeviceName;
                ShowActiveWindow = false;

                HeadphoneBannerArrived?.Invoke(this, EventArgs.Empty);

                // Auto-hide after 4 seconds (this timer is acceptable - single instance, cleaned up)
                _headphoneBannerTimer?.Stop();
                _headphoneBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                _headphoneBannerTimer.Tick += (s, args) =>
                {
                    _headphoneBannerTimer.Stop();
                    ShowHeadphoneBanner = false;
                    HeadphoneDeviceName = "";

                    // SIMPLIFIED: Return to normal content using single source of truth
                    if (IsSpotifyActive)
                    {
                        SpotifyChanged?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        UpdateActiveWindow();
                    }
                };
                _headphoneBannerTimer.Start();
            });
        }

        /// <summary>
        /// Called when island visibility changes. Pauses/resumes timer.
        /// </summary>
        public void SetVisibility(bool visible)
        {
            _isVisible = visible;
        }

        public void Stop()
        {
            _mainTimer?.Stop();
            _mainTimer = null;
            _headphoneBannerTimer?.Stop();
            _headphoneBannerTimer = null;
            _notificationService?.Stop();

            // CRITICAL: Dispose media session service to clean up event subscriptions
            _mediaSessionService?.Dispose();
            _mediaSessionService = null;
        }

        private void OnSystemNotification(object? sender, NotificationEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // System notifications have highest priority
                _currentPriority = SmartEventPriority.System;
                ShowActiveWindow = false;
                ShowHeadphoneBanner = false;
                
                AppName = e.AppDisplayName ?? "Bildirim";
                NotificationText = e.Body ?? "";
                CompactText = e.AppDisplayName ?? "Yeni bildirim";
                EventIcon = "🔔";
                AppIcon = e.Icon;
                HasNotification = true;

                NotificationArrived?.Invoke(this, EventArgs.Empty);
            });
        }

        private void OnSmartEvent(object? sender, SmartEvent e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // SIMPLIFIED: Smart events only show if not showing higher priority content
                if (ShowHeadphoneBanner || IsSpotifyActive || _currentPriority == SmartEventPriority.System)
                    return;

                if (e.Priority == SmartEventPriority.Smart)
                {
                    ShowActiveWindow = false;
                    ApplySmartEvent(e);
                    SmartEventArrived?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        private void OnPlaybackStateChanged(object? sender, bool isPlaying)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsSpotifyPlaying = isPlaying;
                UpdateSpotifyActive();
            });
        }

        private void OnMediaPropertiesChanged(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_mediaSessionService != null)
                {
                    SpotifySong = _mediaSessionService.TrackTitle;
                    SpotifyArtist = _mediaSessionService.ArtistName;
                    AlbumArtwork = _mediaSessionService.AlbumArtwork;
                    CurrentPosition = _mediaSessionService.CurrentPosition;
                    TotalDuration = _mediaSessionService.TotalDuration;
                    UpdateSpotifyActive();
                }
            });
        }

        private void OnPositionChanged(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_mediaSessionService != null)
                {
                    CurrentPosition = _mediaSessionService.CurrentPosition;
                }
            });
        }

        private void UpdateSpotifyActive()
        {
            // RULE: IsSpotifyActive = true ONLY if track title AND artist exist
            bool hasTrackInfo = !string.IsNullOrEmpty(SpotifySong) && !string.IsNullOrEmpty(SpotifyArtist);
            IsSpotifyActive = hasTrackInfo;

            if (IsSpotifyActive)
            {
                // Hide active window when Spotify is active
                ShowActiveWindow = false;
            }
            else
            {
                // Show active window when Spotify is not active
                UpdateActiveWindow();
            }
        }


        #region Media Control Commands

        public async void TogglePlayPause()
        {
            if (_mediaSessionService != null)
            {
                await _mediaSessionService.TogglePlayPauseAsync();
            }
        }

        public async void SkipPrevious()
        {
            if (_mediaSessionService != null)
            {
                await _mediaSessionService.TrySkipPreviousAsync();
            }
        }

        public async void SkipNext()
        {
            if (_mediaSessionService != null)
            {
                await _mediaSessionService.TrySkipNextAsync();
            }
        }

        #endregion

        private void ApplySmartEvent(SmartEvent e)
        {
            _currentPriority = e.Priority;
            AppName = e.Title;
            NotificationText = e.Message;
            CompactText = e.Message.Length > 30 ? e.Message.Substring(0, 27) + "..." : e.Message;
            EventIcon = e.Icon;
            HasNotification = e.Priority != SmartEventPriority.Idle;
        }

        public void ResetToIdle()
        {
            _currentPriority = SmartEventPriority.Idle;
            UpdateActiveWindow();
        }

        #region Settings Persistence

        private void LoadSettings()
        {
            _settings = AppSettings.Load();
        }

        private void SetAutostart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                        key.SetValue("NotificationIsland", exePath);
                }
                else
                {
                    key.DeleteValue("NotificationIsland", false);
                }
            }
            catch { }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}