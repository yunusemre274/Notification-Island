using System.Management;
using System.Runtime.InteropServices;

namespace NI.Services;

public static class BrightnessService
{
    // Only real readings enable the slider. DDC/CI and WMI run off the UI thread.
    public static bool TryGetBrightness(out int brightness)
    {
        brightness = 0;
        try
        {
            using var query = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True");
            using var results = query.Get();
            foreach (ManagementObject item in results)
            { using (item) { brightness = Convert.ToInt32(item["CurrentBrightness"]); return true; } }
        }
        catch { }
        var value = 0;
        var success = WithMonitor(handle =>
        {
            if (!GetMonitorBrightness(handle, out var min, out var current, out var max) || max <= min) return false;
            value = (int)Math.Clamp(100d * (current - min) / (max - min), 0, 100);
            return true;
        });
        brightness = value;
        return success;
    }
    public static bool TrySetBrightness(int value)
    {
        value = Math.Clamp(value, 0, 100);
        try
        {
            using var query = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True");
            using var results = query.Get();
            foreach (ManagementObject item in results)
            { using (item) { item.InvokeMethod("WmiSetBrightness", new object[] { 1u, (byte)value }); return true; } }
        }
        catch { }
        return WithMonitor(handle => GetMonitorBrightness(handle, out var min, out _, out var max)
            && SetMonitorBrightness(handle, min + (uint)((max - min) * value / 100d)));
    }
    private static bool WithMonitor(Func<IntPtr, bool> operation)
    {
        try
        {
            var primary = MonitorFromPoint(new Point(), 1);
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(primary, out var count) || count == 0) return false;
            var monitors = new PhysicalMonitor[count];
            if (!GetPhysicalMonitorsFromHMONITOR(primary, count, monitors)) return false;
            try { return operation(monitors[0].Handle); }
            finally { foreach (var monitor in monitors) DestroyPhysicalMonitor(monitor.Handle); }
        }
        catch { return false; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PhysicalMonitor { public IntPtr Handle; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("dxva2.dll")] private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
    [DllImport("dxva2.dll", CharSet = CharSet.Unicode)] private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll")] private static extern bool GetMonitorBrightness(IntPtr monitor, out uint min, out uint current, out uint max);
    [DllImport("dxva2.dll")] private static extern bool SetMonitorBrightness(IntPtr monitor, uint value);
    [DllImport("dxva2.dll")] private static extern bool DestroyPhysicalMonitor(IntPtr monitor);
}
