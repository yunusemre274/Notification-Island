using System;
using System.Windows.Media.Imaging;

namespace NI.Services
{
    public class NotificationEventArgs : EventArgs
    {
        public string? AppDisplayName { get; set; }
        public string? Body { get; set; }
        public BitmapImage? Icon { get; set; }
    }

    /// <summary>
    /// Lightweight notification service.
    /// Ready for real system notification integration.
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

            // Real notification listener would be hooked here
            // Requires MSIX packaging with userNotificationListener capability
        }

        public void Stop()
        {
            _running = false;
        }

        /// <summary>
        /// Call this when a real system notification is received.
        /// </summary>
        public void EmitNotification(string appName, string body, BitmapImage? icon = null)
        {
            if (!_running) return;
            
            NotificationReceived?.Invoke(this, new NotificationEventArgs
            {
                AppDisplayName = appName,
                Body = body,
                Icon = icon
            });
        }
    }
}
