using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NI.Models;
using Windows.System;

namespace NI.Services.Notifications;

public sealed class NotificationActionHandler : INotificationActionHandler
{
    public static string NotificationShortcut => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? "Win+N" : "Win+A";
    public async Task<NotificationActivationResult> ActivateAsync(NotificationItem item)
    {
        var appId = item.AppUserModelId;
        if (!string.IsNullOrWhiteSpace(appId) && !appId.Any(char.IsControl))
        {
            if (TryForeground(appId)) return new(true, $"Uygulama öne getirildi. Özgün bildirim eylemi için {NotificationShortcut} kullanın.");
            // Documented activation contract for registered packaged applications.
            IApplicationActivationManager? manager = null;
            try
            {
                manager = (IApplicationActivationManager)new ApplicationActivationManager();
                if (manager.ActivateApplication(appId, null, 0, out _) >= 0)
                    return new(true, $"Uygulama açıldı. Özgün bildirim eylemi için {NotificationShortcut} kullanın.");
            }
            catch (Exception ex) { Debug.WriteLine($"Notification activation: {ex.HResult:X8}"); }
            finally { if (manager != null) Marshal.ReleaseComObject(manager); }
            // AppsFolder also resolves registered Win32 IDs. This is a launch request:
            // Explorer cannot confirm the target handled it or opened a particular conversation.
            try
            {
                var request = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
                request.ArgumentList.Add($"shell:AppsFolder\\{appId}");
                Process.Start(request);
                return new(true, $"Uygulama açma isteği gönderildi. Hedef doğrulanamıyorsa {NotificationShortcut} ile özgün bildirimi açın.");
            }
            catch (Exception ex) { Debug.WriteLine($"AppsFolder activation: {ex.HResult:X8}"); }
        }
        await Launcher.LaunchUriAsync(new Uri("ms-settings:notifications"));
        return new(false, $"Uygulama açılamadı. Bildirim ayarları açıldı; özgün eylem için {NotificationShortcut} kullanın.");
    }
    private static bool TryForeground(string appId)
    {
        var found = false;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window)) return true;
            GetWindowThreadProcessId(window, out var pid);
            try
            {
                using var process = Process.GetProcessById((int)pid);
                uint length = 0;
                GetApplicationUserModelId(process.Handle, ref length, null);
                if (length == 0 || length > 4096) return true;
                var id = new StringBuilder((int)length);
                if (GetApplicationUserModelId(process.Handle, ref length, id) != 0 || id.ToString() != appId) return true;
                if (IsIconic(window)) ShowWindowAsync(window, 9); // Restore minimized windows.
                found = SetForegroundWindow(window);
                return !found;
            }
            catch { return true; }
        }, IntPtr.Zero);
        return found;
    }
    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")] private class ApplicationActivationManager { }
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId, [MarshalAs(UnmanagedType.LPWStr)] string? arguments, uint options, out uint pid);
        [PreserveSig] int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string appId, IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint pid);
        [PreserveSig] int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string appId, IntPtr items, out uint pid);
    }
    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetApplicationUserModelId(IntPtr process, ref uint length, StringBuilder? id);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
}
