using System.Diagnostics;
using System.Text.RegularExpressions;

record GpuSample(long? AppDedicated, long? AppShared, long? OllamaDedicated, long? OllamaShared, double? AppEngine, object Dwm);
internal sealed class Counters : IDisposable
{
    private readonly Dictionary<string, (int Pid, string Kind, PerformanceCounter Counter)> _counters = new();
    private DateTimeOffset _nextDiscovery;
    private bool _memoryAvailable, _engineAvailable;
    public GpuSample Read(HashSet<int> app, HashSet<int> ollama, HashSet<int> dwm)
    {
        if (DateTimeOffset.UtcNow >= _nextDiscovery)
        {
            _nextDiscovery = DateTimeOffset.UtcNow.AddSeconds(1);
            var interested = app.Concat(ollama).Concat(dwm).ToHashSet();
            var keep = new HashSet<string>();
            _memoryAvailable = Discover("GPU Process Memory", ["Dedicated Usage", "Shared Usage"], interested, keep);
            _engineAvailable = Discover("GPU Engine", ["Utilization Percentage"], interested, keep);
            foreach (var stale in _counters.Keys.Where(x => !keep.Contains(x)).ToArray()) { _counters[stale].Counter.Dispose(); _counters.Remove(stale); }
        }
        var totals = new Dictionary<string, long>(); var engineTotals = new Dictionary<string, double>();
        foreach (var pair in _counters)
        {
            var (pid, kind, counter) = pair.Value;
            var family = app.Contains(pid) ? "app" : ollama.Contains(pid) ? "ollama" : dwm.Contains(pid) ? "dwm" : null;
            if (family == null) continue;
            try
            {
                var value = Math.Max(0, counter.NextValue());
                if (kind == "Utilization Percentage")
                {
                    var name = counter.InstanceName;
                    var index = name.IndexOf("luid_", StringComparison.Ordinal);
                    var key = family + ":" + (index >= 0 ? name[index..] : name);
                    engineTotals[key] = engineTotals.GetValueOrDefault(key) + value;
                }
                else totals[family + kind] = totals.GetValueOrDefault(family + kind) + (long)value;
            }
            catch { }
        }
        long? Memory(string family, string kind) => _memoryAvailable ? totals.GetValueOrDefault(family + kind) : null;
        double? Engine(string family) => _engineAvailable ? engineTotals.Where(x => x.Key.StartsWith(family + ":", StringComparison.Ordinal)).Select(x => Math.Min(100, x.Value)).DefaultIfEmpty(0).Max() : null;
        return new(Memory("app", "Dedicated Usage"), Memory("app", "Shared Usage"), Memory("ollama", "Dedicated Usage"), Memory("ollama", "Shared Usage"), Engine("app"),
            new { dedicatedBytes = Memory("dwm", "Dedicated Usage"), sharedBytes = Memory("dwm", "Shared Usage"), busiestEnginePercent = Engine("dwm"), attribution = "system-wide context, not charged entirely to app" });
    }
    private bool Discover(string category, string[] kinds, HashSet<int> pids, HashSet<string> keep)
    {
        try
        {
            foreach (var name in new PerformanceCounterCategory(category).GetInstanceNames())
            {
                var match = Regex.Match(name, @"(?:^|_)pid_(\d+)(?:_|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                if (!match.Success || !pids.Contains(int.Parse(match.Groups[1].Value))) continue;
                foreach (var kind in kinds)
                {
                    var key = category + ":" + kind + ":" + name; keep.Add(key);
                    if (_counters.ContainsKey(key)) continue;
                    PerformanceCounter? counter = null;
                    try { counter = new(category, kind, name, true); counter.NextValue(); _counters.Add(key, (int.Parse(match.Groups[1].Value), kind, counter)); counter = null; }
                    finally { counter?.Dispose(); }
                }
            }
            return true;
        }
        catch { return false; }
    }
    public void Dispose() { foreach (var item in _counters.Values) item.Counter.Dispose(); }
}
