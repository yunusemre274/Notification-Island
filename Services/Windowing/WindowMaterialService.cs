using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using NI.Configuration;

namespace NI.Services.Windowing;

public static class WindowMaterialService
{
    public static bool Apply(Window window, AppearanceSettings settings)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return false;
        var source = HwndSource.FromHwnd(handle);
        if (source?.CompositionTarget != null) source.CompositionTarget.BackgroundColor = Colors.Transparent;
        var dark = 1;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        // The shell owns its capsule outline. A DWM non-client border and OS corner mask
        // otherwise paint a second, squarer frame behind the rounded WPF surface.
        var noBorder = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        DwmSetWindowAttribute(handle, 34, ref noBorder, sizeof(int));
        var customCorners = 1; // DWMWCP_DONOTROUND: the window region supplies the exact radius.
        DwmSetWindowAttribute(handle, 33, ref customCorners, sizeof(int));
        var enabled = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)
            && !SystemParameters.HighContrast && settings.Material != GlassMaterial.None;
        var material = enabled ? settings.Material == GlassMaterial.Mica ? 2 : 3 : 1;
        var result = DwmSetWindowAttribute(handle, 38, ref material, sizeof(int));
        var glass = enabled && result == 0 ? -1 : 0;
        var margins = new Margins { Left = glass, Right = glass, Top = glass, Bottom = glass };
        DwmExtendFrameIntoClientArea(handle, ref margins);
        return enabled && result == 0;
    }

    public static void ApplyCorners(Window window, double radius)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || window.ActualWidth <= 0 || window.ActualHeight <= 0) return;
        radius = Math.Min(radius, Math.Min(window.ActualWidth, window.ActualHeight) / 2);
        var dpi = VisualTreeHelper.GetDpi(window);
        if (!GetWindowRect(handle, out var bounds)) return;
        var region = CreateRoundRectRgn(0, 0, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top,
            (int)Math.Round(radius * 2 * dpi.DpiScaleX), (int)Math.Round(radius * 2 * dpi.DpiScaleY));
        if (region == IntPtr.Zero) return;
        // A successful call transfers ownership to Windows. Custom radii use a window region;
        // native automatic corner rounding/shadow may differ by OS/compositor.
        if (SetWindowRgn(handle, region, true) == 0) DeleteObject(region);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
}
