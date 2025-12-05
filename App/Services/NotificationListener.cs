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
    /// Simple notification listener.
    /// 
    /// NOTE: Capturing ALL Windows toast notifications from ANY app requires:
    /// 1. The app to be packaged (MSIX) with userNotificationListener capability.
    /// 2. Or use low-level Windows hooks which require admin and are fragile.
    /// 
    /// This implementation provides simulated notifications for demo purposes.
    /// See README.md for instructions on enabling real system notification listening.
    /// </summary>
    public class NotificationListener
    {
        public event EventHandler<NotificationEventArgs>? NotificationReceived;

        public void Start()
        {
            // Simulate demo notifications
            Task.Run(async () =>
            {
                await Task.Delay(2000);
                OnNotification(new NotificationEventArgs
                {
                    AppDisplayName = "Messages",
                    Body = "A New Message!"
                });

                await Task.Delay(5000);
                OnNotification(new NotificationEventArgs
                {
                    AppDisplayName = "Calendar",
                    Body = "Meeting in 15 minutes"
                });

                await Task.Delay(5000);
                OnNotification(new NotificationEventArgs
                {
                    AppDisplayName = "Mail",
                    Body = "You have 3 new emails"
                });
            });
        }

        protected void OnNotification(NotificationEventArgs e)
        {
            NotificationReceived?.Invoke(this, e);
        }
    }
}
