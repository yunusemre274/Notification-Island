using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace NI.Services
{
    /// <summary>
    /// Monitors desktop visibility and controls island visibility.
    /// Uses low-frequency polling (500ms) to minimize CPU usage.
    /// Island NEVER disappears when interacting with it.
    /// </summary>
    public class DesktopVisibilityService
    {
        private readonly MainWindow _mainWindow;
        private DispatcherTimer? _timer;
        private bool _isVisible = true;
        private IntPtr _ownHwnd;

        public DesktopVisibilityService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void Start()
        {
            // Get our own window handle
            _ownHwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
            
            // Check every 500ms - responsive but low CPU
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
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
                _mainWindow.ShowIsland();
            }
            else if (!shouldShow && _isVisible)
            {
                _isVisible = false;
                _mainWindow.HideIsland();
            }
        }

        private bool ShouldShowIsland()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return true;
            
            // IMPORTANT: If our own window is focused, always show!
            if (fg == _ownHwnd) return true;

            // Get window class name
            var className = new StringBuilder(256);
            GetClassName(fg, className, 256);
            var cls = className.ToString();

            // Desktop-related windows - always show
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                return true;

            // Check if window is maximized
            if (IsZoomed(fg))
                return false;

            // Check if foreground window overlaps our island area
            if (GetWindowRect(fg, out RECT fgRect))
            {
                // Get island position
                double islandLeft = _mainWindow.Left;
                double islandTop = _mainWindow.Top;
                double islandRight = islandLeft + _mainWindow.Width;
                double islandBottom = islandTop + _mainWindow.Height;

                // Check overlap
                bool overlapsHorizontally = !(fgRect.Right < islandLeft || fgRect.Left > islandRight);
                bool overlapsVertically = fgRect.Top < islandBottom;

                if (overlapsHorizontally && overlapsVertically)
                    return false;
            }

            return true;
        }

        #region Win32 Interop

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion
    }
}
