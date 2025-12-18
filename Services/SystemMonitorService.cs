using System;
using System.Diagnostics;
using System.Management;
using System.Threading;

namespace NI.Services
{
    public class SystemMonitorService : IDisposable
    {
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramAvailableCounter;
        private Timer? _updateTimer;
        private readonly object _lock = new();

        private float _cpuUsage;
        private float _ramUsage;
        private float _ramUsedGB;
        private float _ramTotalGB;
        private float? _gpuUsage;

        public float CpuUsage
        {
            get { lock (_lock) return _cpuUsage; }
            private set { lock (_lock) _cpuUsage = value; }
        }

        public float RamUsage
        {
            get { lock (_lock) return _ramUsage; }
            private set { lock (_lock) _ramUsage = value; }
        }

        public float RamUsedGB
        {
            get { lock (_lock) return _ramUsedGB; }
            private set { lock (_lock) _ramUsedGB = value; }
        }

        public float RamTotalGB
        {
            get { lock (_lock) return _ramTotalGB; }
            private set { lock (_lock) _ramTotalGB = value; }
        }

        public float? GpuUsage
        {
            get { lock (_lock) return _gpuUsage; }
            private set { lock (_lock) _gpuUsage = value; }
        }

        public event EventHandler<SystemMetricsEventArgs>? MetricsUpdated;

        public void Start()
        {
            try
            {
                // Initialize performance counters
                _cpuCounter = new PerformanceCounter(
                    "Processor",
                    "% Processor Time",
                    "_Total",
                    true);

                _ramAvailableCounter = new PerformanceCounter(
                    "Memory",
                    "Available MBytes",
                    true);

                // Get total RAM
                _ramTotalGB = GetTotalPhysicalMemoryGB();

                // First read (always returns 0)
                _cpuCounter.NextValue();

                // Start background timer (1 second interval)
                _updateTimer = new Timer(
                    UpdateMetrics,
                    null,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize system monitor: {ex.Message}");
            }
        }

        private void UpdateMetrics(object? state)
        {
            try
            {
                // CPU Usage
                CpuUsage = _cpuCounter?.NextValue() ?? 0;

                // RAM Usage
                float ramAvailableMB = _ramAvailableCounter?.NextValue() ?? 0;
                float ramUsedMB = (_ramTotalGB * 1024) - ramAvailableMB;
                RamUsedGB = ramUsedMB / 1024f;
                RamUsage = (ramUsedMB / (_ramTotalGB * 1024)) * 100f;

                // GPU Usage (optional, can be expensive)
                GpuUsage = GetGpuUsage();

                // Fire event on background thread
                MetricsUpdated?.Invoke(this, new SystemMetricsEventArgs
                {
                    CpuUsage = CpuUsage,
                    RamUsage = RamUsage,
                    RamUsedGB = RamUsedGB,
                    RamTotalGB = RamTotalGB,
                    GpuUsage = GpuUsage
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating metrics: {ex.Message}");
            }
        }

        private float GetTotalPhysicalMemoryGB()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");

                foreach (var obj in searcher.Get())
                {
                    var totalBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                    return totalBytes / (1024f * 1024f * 1024f);
                }
            }
            catch
            {
                return 16; // Fallback
            }
            return 16;
        }

        private float? GetGpuUsage()
        {
            // Optional: Implement GPU monitoring via WMI or LibreHardwareMonitor
            // For minimal overhead, return null initially
            return null;
        }

        public void Stop()
        {
            _updateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Dispose()
        {
            Stop();
            _updateTimer?.Dispose();
            _cpuCounter?.Dispose();
            _ramAvailableCounter?.Dispose();
        }
    }

    public class SystemMetricsEventArgs : EventArgs
    {
        public float CpuUsage { get; init; }
        public float RamUsage { get; init; }
        public float RamUsedGB { get; init; }
        public float RamTotalGB { get; init; }
        public float? GpuUsage { get; init; }
    }
}
