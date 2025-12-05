using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NI.Services;

namespace NI
{
    public partial class MainWindow : Window
    {
        private DesktopVisibilityService? _visibilityService;
        private IntPtr _hwnd;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            
            // Position at top center, 20px from top
            PositionWindow();
            
            // Make window click-through for areas outside the island
            SetWindowExStyle();
            
            // Start desktop visibility monitoring
            _visibilityService = new DesktopVisibilityService(this);
            _visibilityService.Start();
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Prevent accidental closing - minimize instead
            e.Cancel = true;
            _visibilityService?.Stop();
        }

        private void PositionWindow()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = (screenWidth - Width) / 2;
            Top = 20;
        }

        private void SetWindowExStyle()
        {
            // Set as tool window (won't appear in Alt+Tab)
            var exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        }

        public void ShowIsland()
        {
            Dispatcher.Invoke(() =>
            {
                if (Visibility != Visibility.Visible)
                {
                    Visibility = Visibility.Visible;
                    Island.FadeIn();
                }
            });
        }

        public void HideIsland()
        {
            Dispatcher.Invoke(() =>
            {
                if (Visibility == Visibility.Visible)
                {
                    Island.FadeOut(() => Visibility = Visibility.Collapsed);
                }
            });
        }

        #region Win32

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion
    }
}
