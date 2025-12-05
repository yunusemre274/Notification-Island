using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace NI.App.Services
{
    public class NotificationEventArgs : EventArgs
    {
        public string? AppDisplayName { get; set; }
        public string? Body { get; set; }
        public BitmapImage? Icon { get; set; }
    }

    /// <summary>
    /// Lightweight notification service.
    /// Provides simulated notifications for demo.
    /// 
    /// NOTE: Real system-wide toast capture requires MSIX packaging
    /// with userNotificationListener capability.
    /// </summary>
    public class NotificationService
    {
        public event EventHandler<NotificationEventArgs>? NotificationReceived;
        private bool _running = false;

        public void Start()
        {
            if (_running) return;
            _running = true;

            // Demo notifications
            Task.Run(async () =>
            {
                await Task.Delay(2500);
                if (!_running) return;
                
                Emit(new NotificationEventArgs
                {
                    AppDisplayName = "Messages",
                    Body = "A New Message!"
                });

                await Task.Delay(8000);
                if (!_running) return;
                
                Emit(new NotificationEventArgs
                {
                    AppDisplayName = "Calendar",
                    Body = "Meeting in 15 minutes"
                });
            });
        }

        public void Stop()
        {
            _running = false;
        }

        private void Emit(NotificationEventArgs e)
        {
            NotificationReceived?.Invoke(this, e);
        }
    }
}
