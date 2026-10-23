using NI.Models;

namespace NI.Services.Notifications;

public enum NotificationAccess { NeedsPackage, NeedsPermission, Denied, Listening, Unavailable }
public sealed record NotificationSnapshot(IReadOnlyList<NotificationItem> Items, bool Initial);

public interface INotificationProvider : IDisposable
{
    NotificationAccess Access { get; }
    string StatusMessage { get; }
    event EventHandler<NotificationSnapshot>? SnapshotChanged;
    event EventHandler? AccessChanged;
    Task StartAsync(bool requestPermission = false);
    Task RefreshAsync();
}

public sealed record NotificationActivationResult(bool Accepted, string Message);
public interface INotificationActionHandler
{
    Task<NotificationActivationResult> ActivateAsync(NotificationItem item);
}
