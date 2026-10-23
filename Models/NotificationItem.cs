using NI.Core;

namespace NI.Models;

// A provider owns its ID namespace. No executable or URI is inferred from notification text.
public sealed class NotificationItem : ObservableObject
{
    public required string Id { get; init; }
    public string ProviderId { get; init; } = "windows";
    public uint NativeId { get; init; }
    public string AppUserModelId { get; init; } = "";
    public string AppName { get; init; } = "Uygulama";
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public byte[]? IconBytes { get; init; }
    public string TimeLabel => CreatedAt.ToLocalTime().ToString("HH:mm");
    private bool _isRead;
    public bool IsRead { get => _isRead; set => Set(ref _isRead, value); }
}
