using System.Diagnostics;
using System.Runtime.InteropServices;
using NI.Services.SystemMonitoring;

namespace NI.Services;

// One non-overlapping sampler. No WMI queries in the CPU/RAM sampling path.
public sealed class SystemMonitorService : ISystemMonitor
{
    private readonly Timer _timer;
    private readonly Dictionary<string, PerformanceCounter> _gpuCounters = new();
    private volatile bool _active, _disposed;
    private int _sampling, _generation, _baselineGeneration = -1;
    private ulong _lastIdle, _lastKernel, _lastUser;
    private DateTime _lastDiscovery = DateTime.MinValue;
    public event EventHandler<SystemMetrics>? Updated;
    public SystemMonitorService() => _timer = new Timer(Sample, null, Timeout.Infinite, Timeout.Infinite);

    public void SetActive(bool active)
    {
        // Never wait for PDH enumeration on the UI thread.
        if (_disposed || _active == active) return;
        _active = active;
        Interlocked.Increment(ref _generation);
        _timer.Change(0, Timeout.Infinite);
    }

    private void Sample(object? state)
    {
        if (Interlocked.CompareExchange(ref _sampling, 1, 0) != 0) return;
        try
        {
            if (!_active || _disposed) return;
            var generation = Volatile.Read(ref _generation);
            if (_baselineGeneration != generation)
            {
                _lastIdle = _lastKernel = _lastUser = 0;
                _baselineGeneration = generation;
            }
            double? cpu = null;
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                if (_lastKernel != 0)
                {
                    var total = (kernel - _lastKernel) + (user - _lastUser);
                    if (total > 0) cpu = Math.Clamp(100d * (1d - (double)(idle - _lastIdle) / total), 0, 100);
                }
                _lastIdle = idle; _lastKernel = kernel; _lastUser = user;
            }
            var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            var available = GlobalMemoryStatusEx(ref memory) && memory.TotalPhysical > 0;
            double? totalGb = available ? memory.TotalPhysical / 1073741824d : null;
            double? usedGb = available ? (memory.TotalPhysical - memory.AvailablePhysical) / 1073741824d : null;
            var metrics = new SystemMetrics(cpu, SampleGpu(), available ? memory.MemoryLoad : null, usedGb, totalGb);
            if (_active && !_disposed && generation == Volatile.Read(ref _generation)) Updated?.Invoke(this, metrics);
        }
        catch (Exception ex) { Debug.WriteLine($"System metrics unavailable: {ex.Message}"); }
        finally
        {
            if (!_active || _disposed) ReleaseGpuCounters();
            Volatile.Write(ref _sampling, 0);
            if (!_disposed)
            {
                try { _timer.Change(_active ? 1500 : Timeout.Infinite, Timeout.Infinite); }
                catch (ObjectDisposedException) { }
            }
        }
    }

    private double? SampleGpu()
    {
        // PDH GPU Engine counters require a compatible WDDM driver. Aggregate each physical
        // 3D engine, then show the busiest engine, rather than summing unrelated GPU engines.
        if (DateTime.UtcNow - _lastDiscovery > TimeSpan.FromSeconds(30))
        {
            _lastDiscovery = DateTime.UtcNow;
            try
            {
                var category = new PerformanceCounterCategory("GPU Engine");
                var names = category.GetInstanceNames().Where(x => x.Contains("engtype_3D", StringComparison.Ordinal)).ToHashSet();
                foreach (var removed in _gpuCounters.Keys.Where(x => !names.Contains(x)).ToArray())
                { _gpuCounters[removed].Dispose(); _gpuCounters.Remove(removed); }
                foreach (var name in names.Where(x => !_gpuCounters.ContainsKey(x)))
                {
                    PerformanceCounter? counter = null;
                    try
                    {
                        counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                        counter.NextValue();
                        _gpuCounters.Add(name, counter);
                        counter = null; // Ownership moved to the cache.
                    }
                    catch { /* A process/engine can disappear while enumerating. */ }
                    finally { counter?.Dispose(); }
                }
            }
            catch { ReleaseGpuCounters(resetDiscovery: false); }
        }
        if (_gpuCounters.Count == 0) return null;
        var engines = new Dictionary<string, double>();
        foreach (var pair in _gpuCounters)
        {
            try
            {
                var luidIndex = pair.Key.IndexOf("luid_", StringComparison.Ordinal);
                var key = luidIndex >= 0 ? pair.Key[luidIndex..] : pair.Key;
                engines[key] = engines.GetValueOrDefault(key) + pair.Value.NextValue();
            }
            catch { /* Stale counters are removed during the next discovery. */ }
        }
        return engines.Count == 0 ? null : Math.Clamp(engines.Values.Max(), 0, 100);
    }

    private void ReleaseGpuCounters(bool resetDiscovery = true)
    {
        foreach (var counter in _gpuCounters.Values) counter.Dispose();
        _gpuCounters.Clear();
        if (resetDiscovery) _lastDiscovery = DateTime.MinValue;
    }
    public static SystemMetrics ReadMemorySnapshot()
    {
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory) || memory.TotalPhysical == 0) return new(null, null, null, null, null);
        return new(null, null, memory.MemoryLoad, (memory.TotalPhysical - memory.AvailablePhysical) / 1073741824d, memory.TotalPhysical / 1073741824d);
    }
    public void Dispose()
    {
        _disposed = true; _active = false; _timer.Dispose();
        // Either this thread or the in-flight sampler owns counter cleanup, never both.
        if (Interlocked.CompareExchange(ref _sampling, 1, 0) == 0)
        {
            try { ReleaseGpuCounters(); }
            finally { Volatile.Write(ref _sampling, 0); }
        }
    }
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
}
