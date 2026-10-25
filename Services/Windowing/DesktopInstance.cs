using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace NI.Services.Windowing;

public static class DesktopInstance
{
    private const string WindowMarker = "NotificationIsland.MainWindow.v1";
    public static uint ActivationMessage { get; } = RegisterWindowMessage("NotificationIsland.ShowMainWindow.v1");

    // Local\ scopes to a session, not a desktop. Test/automation desktops must not
    // hold the same UI singleton as the user's interactive desktop in that session.
    public static string MutexName
    {
        get
        {
            var station = ReadObjectName(GetProcessWindowStation());
            var desktop = ReadObjectName(GetThreadDesktop(GetCurrentThreadId()));
            var scope = Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(station + "\\" + desktop)))[..32];
            return @"Local\NotificationIsland.Desktop." + scope;
        }
    }
    public static void RegisterWindow(IntPtr hwnd)
    {
        if (!SetProp(hwnd, WindowMarker, new IntPtr(1))) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public static void UnregisterWindow(IntPtr hwnd) => RemoveProp(hwnd, WindowMarker);
    public static bool TryActivateExisting()
    {
        // A second launch can arrive between mutex acquisition and HWND construction.
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var hwnd = FindWindow(null, "Notification Island");
            if (hwnd != IntPtr.Zero && GetProp(hwnd, WindowMarker) == new IntPtr(1))
            {
                GetWindowThreadProcessId(hwnd, out var processId);
                AllowSetForegroundWindow(processId);
                return ActivationMessage != 0 && PostMessage(hwnd, ActivationMessage, IntPtr.Zero, IntPtr.Zero);
            }
            Thread.Sleep(25);
        }
        return false;
    }
    private static string ReadObjectName(IntPtr handle)
    {
        var name = new StringBuilder(512);
        if (handle == IntPtr.Zero || !GetUserObjectInformation(handle, 2, name, name.Capacity * sizeof(char), out _))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows desktop identity could not be read.");
        return name.ToString();
    }
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint threadId);
    [DllImport("user32.dll")] private static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder info, int length, out int needed);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetProp(IntPtr hwnd, string name, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProp(IntPtr hwnd, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr RemoveProp(IntPtr hwnd, string name);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint processId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
