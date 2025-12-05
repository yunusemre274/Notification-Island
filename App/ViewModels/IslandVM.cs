using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NI.App.Services;
using Microsoft.Win32;

namespace NI.App.ViewModels
{
    /// <summary>
    /// Lightweight ViewModel for Dynamic Island.
    /// Uses event-driven updates only - no constant polling.
    /// </summary>
    public class IslandVM : INotifyPropertyChanged
    {
        // Clock - updates once per minute only
        private DispatcherTimer? _clockTimer;
        private string _clockText = DateTime.Now.ToString("HH:mm");
        public string ClockText
        {
            get => _clockText;
            set { if (_clockText != value) { _clockText = value; OnPropertyChanged(); } }
        }

        // Notification data
        private string _appName = "";
        public string AppName
        {
            get => _appName;
            set { if (_appName != value) { _appName = value; OnPropertyChanged(); } }
        }

        private string _notificationText = "";
        public string NotificationText
        {
            get => _notificationText;
            set { if (_notificationText != value) { _notificationText = value; OnPropertyChanged(); } }
        }

        private BitmapImage? _currentIcon;
        public BitmapImage? CurrentIcon
        {
            get => _currentIcon;
            set { _currentIcon = value; OnPropertyChanged(); }
        }

        // Pet
        private PetVM _pet = new();
        public BitmapImage? PetFrame => _pet.CurrentFrame;

        // Settings
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

        private bool _petEnabled = true;
        public bool PetEnabled
        {
            get => _petEnabled;
            set
            {
                _petEnabled = value;
                if (!value) _pet.Stop();
                else _pet.Start();
                OnPropertyChanged();
            }
        }

        // Services
        private NotificationService? _notificationService;

        // Events
        public event EventHandler? NotificationArrived;

        public IslandVM()
        {
            _pet.FrameChanged += (s, e) => OnPropertyChanged(nameof(PetFrame));
            LoadSettings();
        }

        public void Start()
        {
            // Clock timer - updates every 30 seconds (enough for HH:mm display)
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _clockTimer.Tick += (s, e) => ClockText = DateTime.Now.ToString("HH:mm");
            _clockTimer.Start();

            // Pet idle animation
            _pet.Start();

            // Notification service
            _notificationService = new NotificationService();
            _notificationService.NotificationReceived += OnNotification;
            _notificationService.Start();
        }

        public void Stop()
        {
            _clockTimer?.Stop();
            _pet.Stop();
            _notificationService?.Stop();
        }

        private void OnNotification(object? sender, NotificationEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AppName = e.AppDisplayName ?? "Notification";
                NotificationText = e.Body ?? "";
                CurrentIcon = e.Icon;
                NotificationArrived?.Invoke(this, EventArgs.Empty);
            });
        }

        public void PetWave() => _pet.TriggerWave();
        public void PetJump() => _pet.TriggerJump();

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
