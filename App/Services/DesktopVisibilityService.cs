using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using NI.App.Views;

namespace NI.App.Services
{
    /// <summary>
    /// Monitors desktop visibility and hides the island when apps overlap it.
    /// Uses low-frequency polling (500ms) to minimize CPU usage.
    /// </summary>
    public class DesktopVisibilityService
    {
        private readonly Window _mainWindow;
        private DispatcherTimer? _timer;
        private bool _isVisible = true;

        public DesktopVisibilityService(Window mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void Start()
        {
            // Poll every 500ms - low enough to be responsive, high enough to save CPU
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
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.Visibility = Visibility.Visible;
                    var island = FindIsland();
                    island?.Show();
                });
            }
            else if (!shouldShow && _isVisible)
            {
                _isVisible = false;
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    var island = FindIsland();
                    island?.Hide();
                });
            }
        }

        private DynamicIslandView? FindIsland()
        {
            if (_mainWindow.Content is System.Windows.Controls.Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is DynamicIslandView island)
                        return island;
                }
            }
            return null;
        }

        private bool ShouldShowIsland()
        {
            // Get foreground window
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return true;

            // Check if it's the desktop or shell
            GetWindowThreadProcessId(fg, out uint processId);
            
            // Get window class name
            var className = new System.Text.StringBuilder(256);
            GetClassName(fg, className, 256);
            var cls = className.ToString();

            // Desktop-related windows
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd")
                return true;

            // Check if the foreground window is maximized and overlaps our area
            if (IsZoomed(fg))
            {
                // Maximized window - hide island
                return false;
            }

            // Check if foreground window overlaps island area
            if (GetWindowRect(fg, out RECT rect))
            {
                var islandTop = (int)_mainWindow.Top;
                var islandBottom = islandTop + (int)_mainWindow.Height;
                var islandLeft = (int)_mainWindow.Left;
                var islandRight = islandLeft + (int)_mainWindow.Width;

                // Check for overlap
                bool overlaps = !(rect.Right < islandLeft || 
                                  rect.Left > islandRight || 
                                  rect.Bottom < islandTop || 
                                  rect.Top > islandBottom);

                if (overlaps && rect.Top <= islandBottom)
                    return false;
            }

            return true;
        }

        #region Win32 Interop

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

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
