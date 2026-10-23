using System.Collections.ObjectModel;
using NI.Models;

namespace NI.Services.Notifications;

// Call on the UI context. Bounded, in-memory mirror; bodies are never persisted to disk.
public sealed class NotificationRouter(INotificationActionHandler actions)
{
    public const int Capacity = 40;
    public ObservableCollection<NotificationItem> Items { get; } = new();
    public event EventHandler<NotificationItem>? Arrived;
    public event EventHandler? Changed;
    public int UnreadCount => Items.Count(x => !x.IsRead);

    public void Synchronize(NotificationSnapshot snapshot)
    {
        var incoming = snapshot.Items.OrderByDescending(x => x.CreatedAt)
            .DistinctBy(x => x.Id).Take(Capacity).ToArray();
        var ids = incoming.Select(x => x.Id).ToHashSet();
        for (var i = Items.Count - 1; i >= 0; i--)
            if (!ids.Contains(Items[i].Id)) Items.RemoveAt(i);
        var added = new List<NotificationItem>();
        for (var i = 0; i < incoming.Length; i++)
        {
            var existing = Items.FirstOrDefault(x => x.Id == incoming[i].Id);
            if (existing == null)
            {
                Items.Insert(i, incoming[i]);
                added.Add(incoming[i]);
            }
            else
            {
                // Keep read state, but replace changed payloads with the latest content.
                if (existing.Title != incoming[i].Title || existing.Body != incoming[i].Body)
                {
                    incoming[i].IsRead = existing.IsRead;
                    Items[Items.IndexOf(existing)] = incoming[i];
                    existing = incoming[i];
                }
                var index = Items.IndexOf(existing);
                if (index != i) Items.Move(index, i);
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
        if (!snapshot.Initial && added.Count > 0) Arrived?.Invoke(this, added[0]);
    }

    public void MarkAllRead()
    {
        foreach (var item in Items) item.IsRead = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<NotificationActivationResult> OpenAsync(NotificationItem item)
    {
        var result = await actions.ActivateAsync(item);
        if (result.Accepted) item.IsRead = true;
        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }
}
