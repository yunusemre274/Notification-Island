using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NI.Services;

namespace NI.ViewModels
{
    /// <summary>
    /// Lightweight ViewModel for Dynamic Island.
    /// Event-driven updates only - no continuous polling.
    /// </summary>
    public class IslandVM : INotifyPropertyChanged
    {
        #region Clock

        private DispatcherTimer? _clockTimer;
        private string _clockText = DateTime.Now.ToString("HH:mm");
        public string ClockText
        {
            get => _clockText;
            private set { if (_clockText != value) { _clockText = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Notification State

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

        private string _compactText = "Ready";
        public string CompactText
        {
            get => _compactText;
            private set { if (_compactText != value) { _compactText = value; OnPropertyChanged(); } }
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

        #endregion

        #region Settings

        private bool _autostartEnabled;
        public bool AutostartEnabled
        {
            get => _autostartEnabled;
            set
            {
                if (_autostartEnabled != value)
                {
                    _autostartEnabled = value;
                    SetAutostart(value);
                    OnPropertyChanged();
                }
            }
        }

        private bool _soundEnabled = true;
        public bool SoundEnabled
        {
            get => _soundEnabled;
            set { _soundEnabled = value; OnPropertyChanged(); }
        }

        #endregion

        #region Services

        private NotificationService? _notificationService;

        #endregion

        #region Events

        public event EventHandler? NotificationArrived;

        #endregion

        public IslandVM()
        {
            LoadSettings();
        }

        public void Start()
        {
            // Clock updates every 30 seconds (enough for HH:mm)
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _clockTimer.Tick += (s, e) => ClockText = DateTime.Now.ToString("HH:mm");
            _clockTimer.Start();

            // Notification service
            _notificationService = new NotificationService();
            _notificationService.NotificationReceived += OnNotification;
            _notificationService.Start();
        }

        public void Stop()
        {
            _clockTimer?.Stop();
            _notificationService?.Stop();
        }

        private void OnNotification(object? sender, NotificationEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AppName = e.AppDisplayName ?? "Notification";
                NotificationText = e.Body ?? "";
                CompactText = e.AppDisplayName ?? "New notification";
                AppIcon = e.Icon;
                HasNotification = true;

                NotificationArrived?.Invoke(this, EventArgs.Empty);
            });
        }

        #region Settings Persistence

        private void LoadSettings()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                _autostartEnabled = key?.GetValue("NI") != null;
            }
            catch { }
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
                        key.SetValue("NI", exePath);
                }
                else
                {
                    key.DeleteValue("NI", false);
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
