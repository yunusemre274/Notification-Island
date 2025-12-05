using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace NI.Services
{
    /// <summary>
    /// Monitors desktop visibility and controls island visibility.
    /// OPTIMIZED: Uses 750ms polling to minimize CPU usage.
    /// Only hides when TRUE fullscreen apps are running (games, videos).
    /// Stays visible for normal windowed/maximized windows.
    /// </summary>
    public class DesktopVisibilityService
    {
        private readonly MainWindow _mainWindow;
        private DispatcherTimer? _timer;
        private bool _isVisible = true;
        private IntPtr _ownHwnd;

        public event EventHandler<bool>? VisibilityChanged;

        public bool IsVisible => _isVisible;

        public DesktopVisibilityService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void Start()
        {
            _ownHwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
            
            // Check every 750ms - low CPU while still responsive
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _timer.Tick += CheckVisibility;
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer = null;
        }

        private void CheckVisibility(object? sender, EventArgs e)
        {
            bool shouldShow = ShouldShowIsland();

            if (shouldShow && !_isVisible)
            {
                _isVisible = true;
                VisibilityChanged?.Invoke(this, true);
                _mainWindow.ShowIsland();
            }
            else if (!shouldShow && _isVisible)
            {
                _isVisible = false;
                VisibilityChanged?.Invoke(this, false);
                _mainWindow.HideIsland();
            }
        }

        private bool ShouldShowIsland()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return true;
            
            // If our own window is focused, always show
            if (fg == _ownHwnd) return true;

            // Get window class name
            var className = new StringBuilder(64);
            GetClassName(fg, className, 64);
            var cls = className.ToString();

            // Desktop-related windows - always show
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                return true;

            // Only hide for TRUE fullscreen applications (covers entire screen without taskbar)
            // This includes games, fullscreen videos, presentations, etc.
            if (IsFullscreenApp(fg))
                return false;

            // For all other cases (windowed, maximized), stay visible
            return true;
        }

        /// <summary>
        /// Detects if a window is a TRUE fullscreen application.
        /// True fullscreen = covers entire screen including taskbar area.
        /// Maximized windows have a different style and don't cover taskbar.
        /// </summary>
        private bool IsFullscreenApp(IntPtr hwnd)
        {
            // Get the window rect
            if (!GetWindowRect(hwnd, out RECT windowRect))
                return false;

            // Get the monitor info for this window
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            
            if (!GetMonitorInfo(monitor, ref monitorInfo))
                return false;

            // True fullscreen = window covers the ENTIRE monitor (including taskbar area)
            // Compare with rcMonitor (full monitor bounds), not rcWork (work area without taskbar)
            RECT screenRect = monitorInfo.rcMonitor;

            bool isFullscreen = 
                windowRect.Left <= screenRect.Left &&
                windowRect.Top <= screenRect.Top &&
                windowRect.Right >= screenRect.Right &&
                windowRect.Bottom >= screenRect.Bottom;

            if (!isFullscreen)
                return false;

            // Additional check: make sure it's not a maximized window with WS_CAPTION
            // Fullscreen apps typically have no caption/border
            uint style = GetWindowLong(hwnd, GWL_STYLE);
            bool hasCaption = (style & WS_CAPTION) == WS_CAPTION;
            bool hasThickFrame = (style & WS_THICKFRAME) != 0;

            // If it covers full screen but still has window decorations, it's just maximized
            // True fullscreen apps have no caption or thick frame
            if (hasCaption && hasThickFrame)
                return false;

            return true;
        }

        #region Win32 Interop

        private const int GWL_STYLE = -16;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_THICKFRAME = 0x00040000;
        private const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        #endregion
    }
}
