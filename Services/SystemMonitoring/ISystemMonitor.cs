namespace NI.Services.SystemMonitoring;

public sealed record SystemMetrics(double? Cpu, double? Gpu, double? Ram, double? UsedGb, double? TotalGb);
public interface ISystemMonitor : IDisposable
{
    event EventHandler<SystemMetrics>? Updated;
    void SetActive(bool active);
}
