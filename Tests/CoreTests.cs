using System.Text.Json;
using NI.Configuration;
using NI.Models;
using NI.Services.Notifications;

var passed = 0;
var failed = 0;
await Check("Initial Windows history does not trigger compact preview", () =>
{
    var router = Router(); var arrivals = 0;
    router.Arrived += (_, _) => arrivals++;
    router.Synchronize(new([Item(1), Item(2)], true));
    Assert(router.Items.Count == 2 && arrivals == 0);
});
await Check("Newest notification wins when a burst arrives", () =>
{
    var router = Router(); NotificationItem? preview = null;
    router.Arrived += (_, item) => preview = item;
    router.Synchronize(new([Item(1), Item(3), Item(2)], false));
    Assert(preview?.Id == "3" && router.Items[0].Id == "3");
});
await Check("Duplicate provider IDs never create duplicate cards or previews", () =>
{
    var router = Router(); var arrivals = 0;
    router.Arrived += (_, _) => arrivals++;
    router.Synchronize(new([Item(1), Item(1)], false));
    router.Synchronize(new([Item(1)], false));
    Assert(router.Items.Count == 1 && arrivals == 1);
});
await Check("Dismissed Windows notifications disappear from the mirror", () =>
{
    var router = Router(); router.Synchronize(new([Item(1), Item(2)], true));
    router.Synchronize(new([Item(2)], false));
    Assert(router.Items.Count == 1 && router.Items[0].Id == "2");
});
await Check("Read state survives new provider instances of unchanged notifications", () =>
{
    var router = Router(); router.Synchronize(new([Item(1)], true));
    router.MarkAllRead(); router.Synchronize(new([Item(1), Item(2)], false));
    Assert(router.Items.Single(x => x.Id == "1").IsRead && router.UnreadCount == 1);
});
await Check("Changed payload replaces a card without losing read state", () =>
{
    var router = Router(); router.Synchronize(new([Item(1)], true)); router.MarkAllRead();
    router.Synchronize(new([Item(1, "Updated")], false));
    Assert(router.Items[0].Title == "Updated" && router.Items[0].IsRead);
});
await Check("Notification collection remains bounded to the newest 40", () =>
{
    var router = Router(); router.Synchronize(new(Enumerable.Range(1, 200).Select(x => Item(x)).ToArray(), true));
    Assert(router.Items.Count == NotificationRouter.Capacity && router.Items.First().Id == "200" && router.Items.Last().Id == "161");
});
await Check("Successful activation marks only the selected notification read", async () =>
{
    var action = new TestAction(true); var router = new NotificationRouter(action);
    router.Synchronize(new([Item(1), Item(2)], true));
    await router.OpenAsync(router.Items.Single(x => x.Id == "1"));
    Assert(action.LastId == "1" && router.UnreadCount == 1 && router.Items.Single(x => x.Id == "1").IsRead);
});
await Check("Failed activation keeps notification unread", async () =>
{
    var router = new NotificationRouter(new TestAction(false)); router.Synchronize(new([Item(1)], true));
    var result = await router.OpenAsync(router.Items[0]);
    Assert(!result.Accepted && router.UnreadCount == 1);
});
await Check("Permission revocation clears every mirrored notification", () =>
{
    var router = Router(); router.Synchronize(new([Item(1), Item(2)], true));
    router.Synchronize(new([], true)); Assert(router.Items.Count == 0 && router.UnreadCount == 0);
});
await Check("Snapshot permutations keep order and identity consistent", () =>
{
    var router = Router(); var random = new Random(42);
    for (var run = 0; run < 500; run++)
    {
        var items = Enumerable.Range(1, 80).Where(_ => random.Next(2) == 1).OrderBy(_ => random.Next()).Select(x => Item(x)).ToArray();
        router.Synchronize(new(items, false));
        var expected = items.OrderByDescending(x => x.CreatedAt).Take(40).Select(x => x.Id);
        Assert(router.Items.Select(x => x.Id).SequenceEqual(expected));
    }
});
await Check("Legacy settings gain safe appearance defaults and preserve user model", () =>
{
    var settings = JsonSerializer.Deserialize<AppSettings>("{\"OllamaModel\":\"custom-model\",\"SoundEnabled\":false}")!;
    Assert(settings.Appearance.ShowClock && settings.Appearance.PanelWidth == 760 && settings.OllamaModel == "custom-model" && !settings.SoundEnabled);
});
await Check("Malformed dimensions and invalid enum values are constrained", () =>
{
    var settings = JsonSerializer.Deserialize<AppearanceSettings>("{\"PanelWidth\":-10,\"CompactHeight\":9999,\"SurfaceOpacity\":0,\"Theme\":900}")!;
    Assert(settings.PanelWidth == 680 && settings.CompactHeight == 64 && settings.SurfaceOpacity == .45 && settings.Theme == GlassTheme.DarkGlass);
    settings.CornerRadius = double.NaN; settings.HorizontalOffset = double.PositiveInfinity;
    Assert(settings.CornerRadius == 24 && settings.HorizontalOffset == 0);
});
await Check("Appearance customization survives JSON round trip", () =>
{
    var original = new AppSettings { Appearance = new() { Theme = GlassTheme.BlueGlass, Material = GlassMaterial.Mica, PanelWidth = 900, ShowMedia = false, ExpandOnHover = true, HorizontalOffset = -120 } };
    var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original))!;
    Assert(restored.Appearance.Theme == GlassTheme.BlueGlass && restored.Appearance.Material == GlassMaterial.Mica && restored.Appearance.PanelWidth == 900 && !restored.Appearance.ShowMedia && restored.Appearance.ExpandOnHover && restored.Appearance.HorizontalOffset == -120);
});
await Check("Atomic settings save and reload uses isolated storage", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "ni-settings-test-" + Guid.NewGuid());
    try
    {
        var path = Path.Combine(directory, "settings.json");
        var settings = new AppSettings(path) { OllamaModel = "saved-model", Appearance = new() { Theme = GlassTheme.BlueGlass } };
        Assert(settings.Save());
        var restored = AppSettings.Load(path);
        Assert(restored.OllamaModel == "saved-model" && restored.Appearance.Theme == GlassTheme.BlueGlass && !File.Exists(path + ".tmp"));
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
});
await Check("Malformed JSON is backed up before defaults may replace it", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "ni-recovery-test-" + Guid.NewGuid());
    try
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        File.WriteAllText(path, "{bad-json");
        var restored = AppSettings.Load(path);
        Assert(restored.LoadWarning != null && Directory.GetFiles(directory, "*.invalid-*.json").Length == 1);
        Assert(File.ReadAllText(Directory.GetFiles(directory, "*.invalid-*.json")[0]) == "{bad-json" && restored.Save());
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
});
Console.WriteLine($"{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;

NotificationRouter Router() => new(new TestAction(true));
NotificationItem Item(int id, string? title = null) => new() { Id = id.ToString(), AppName = "Test", Title = title ?? $"Notification {id}", CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(id) };
void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
async Task Check(string name, Delegate body)
{
    try
    {
        switch (body) { case Action action: action(); break; case Func<Task> asyncAction: await asyncAction(); break; default: throw new Exception("Unsupported test"); }
        passed++; Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
sealed class TestAction(bool success) : INotificationActionHandler
{
    public string? LastId { get; private set; }
    public Task<NotificationActivationResult> ActivateAsync(NotificationItem item)
    { LastId = item.Id; return Task.FromResult(new NotificationActivationResult(success, "Test result")); }
}
