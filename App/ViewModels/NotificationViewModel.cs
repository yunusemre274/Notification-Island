using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NI.App.Services;

namespace NI.App.ViewModels
{
    public class NotificationModel : INotifyPropertyChanged
    {
        private string _appName = "";
        private BitmapImage? _icon;
        private string _message = "";

        public string AppName
        {
            get => _appName;
            set { _appName = value; OnPropertyChanged(); }
        }

        public BitmapImage? Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class NotificationViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<NotificationModel> Queue { get; } = new();

        private NotificationModel _current = new() { AppName = "Ready", Message = "No notifications" };
        public NotificationModel CurrentNotification
        {
            get => _current;
            set { _current = value; OnPropertyChanged(); }
        }

        private string _clockText = DateTime.Now.ToString("HH:mm");
        public string ClockText
        {
            get => _clockText;
            set { _clockText = value; OnPropertyChanged(); }
        }

        private DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private NotificationListener? _listener;

        public NotificationViewModel()
        {
            _clockTimer.Tick += (s, e) => ClockText = DateTime.Now.ToString("HH:mm");
            _listener = new NotificationListener();
            _listener.NotificationReceived += OnNotificationReceived;
        }

        public void Start()
        {
            _clockTimer.Start();
            _listener?.Start();
        }

        private void OnNotificationReceived(object? sender, NotificationEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var model = new NotificationModel
                {
                    AppName = e.AppDisplayName ?? "Unknown",
                    Message = e.Body ?? "",
                    Icon = e.Icon
                };
                Queue.Add(model);
                if (Queue.Count == 1)
                    ShowNext();
            });
        }

        private void ShowNext()
        {
            if (Queue.Count == 0) return;
            CurrentNotification = Queue[0];

            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                if (Queue.Count > 0)
                    Queue.RemoveAt(0);
                if (Queue.Count > 0)
                    ShowNext();
                else
                    CurrentNotification = new NotificationModel { AppName = "Ready", Message = "No notifications" };
            };
            hideTimer.Start();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
