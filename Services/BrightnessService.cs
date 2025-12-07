using System;
using System.Management;
using System.Runtime.InteropServices;

namespace NI.Services
{
    /// <summary>
    /// Service for controlling display brightness.
    /// Uses WMI for laptop displays, DXVA for external monitors.
    /// </summary>
    public static class BrightnessService
    {
        #region Native Methods

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public char[] szPhysicalMonitorDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private const uint MONITOR_DEFAULTTOPRIMARY = 1;

        #endregion

        private static bool _isLaptop = false;
        private static bool _checkedType = false;

        /// <summary>
        /// Gets the current brightness (0-100).
        /// </summary>
        public static int GetBrightness()
        {
            CheckDisplayType();

            if (_isLaptop)
            {
                return GetLaptopBrightness();
            }
            else
            {
                return GetMonitorBrightness();
            }
        }

        /// <summary>
        /// Sets the brightness (0-100).
        /// </summary>
        public static void SetBrightness(int brightness)
        {
            brightness = Math.Clamp(brightness, 0, 100);
            CheckDisplayType();

            if (_isLaptop)
            {
                SetLaptopBrightness(brightness);
            }
            else
            {
                SetMonitorBrightnessValue(brightness);
            }
        }

        /// <summary>
        /// Checks if brightness control is supported.
        /// </summary>
        public static bool IsSupported
        {
            get
            {
                try
                {
                    CheckDisplayType();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static void CheckDisplayType()
        {
            if (_checkedType) return;

            try
            {
                // Try WMI first (laptops)
                using var searcher = new ManagementObjectSearcher("SELECT * FROM WmiMonitorBrightness");
                using var collection = searcher.Get();
                _isLaptop = collection.Count > 0;
            }
            catch
            {
                _isLaptop = false;
            }

            _checkedType = true;
        }

        #region Laptop Brightness (WMI)

        private static int GetLaptopBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    return Convert.ToInt32(obj["CurrentBrightness"]);
                }
            }
            catch { }

            return 75; // Default
        }

        private static void SetLaptopBrightness(int brightness)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    obj.InvokeMethod("WmiSetBrightness", new object[] { 1, brightness });
                }
            }
            catch { }
        }

        #endregion

        #region External Monitor Brightness (DXVA2)

        private static int GetMonitorBrightness()
        {
            try
            {
                var hMonitor = MonitorFromWindow(IntPtr.Zero, MONITOR_DEFAULTTOPRIMARY);
                if (hMonitor == IntPtr.Zero) return 75;

                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
                    return 75;

                var monitors = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                    return 75;

                try
                {
                    if (GetMonitorBrightness(monitors[0].hPhysicalMonitor, out _, out uint current, out uint max))
                    {
                        return max > 0 ? (int)((current * 100) / max) : 75;
                    }
                }
                finally
                {
                    foreach (var monitor in monitors)
                        DestroyPhysicalMonitor(monitor.hPhysicalMonitor);
                }
            }
            catch { }

            return 75;
        }

        private static void SetMonitorBrightnessValue(int brightness)
        {
            try
            {
                var hMonitor = MonitorFromWindow(IntPtr.Zero, MONITOR_DEFAULTTOPRIMARY);
                if (hMonitor == IntPtr.Zero) return;

                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
                    return;

                var monitors = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                    return;

                try
                {
                    if (GetMonitorBrightness(monitors[0].hPhysicalMonitor, out uint min, out _, out uint max))
                    {
                        uint newValue = (uint)(min + ((max - min) * brightness / 100));
                        SetMonitorBrightness(monitors[0].hPhysicalMonitor, newValue);
                    }
                }
                finally
                {
                    foreach (var monitor in monitors)
                        DestroyPhysicalMonitor(monitor.hPhysicalMonitor);
                }
            }
            catch { }
        }

        #endregion
    }
}
