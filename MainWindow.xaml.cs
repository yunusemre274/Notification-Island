using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NI.Services;
using NI.Views;
using NI.Views.Controls;

namespace NI
{
    public partial class MainWindow : Window
    {
        private DesktopVisibilityService? _visibilityService;
        private IntPtr _hwnd;
        private ControlCenterPanel? _currentPanel;

        public MainWindow()
        {
            // Use software rendering to ensure visibility in screen recordings
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            
            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnClosing;
            KeyDown += OnMainWindowKeyDown;
        }

        private void OnMainWindowKeyDown(object sender, KeyEventArgs e)
        {
            // REMOVED: System Info and AI Assistant keyboard shortcuts (features disabled for stability)
        }


        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            
            // Position at top center
            PositionWindow();
            
            // Make window click-through for areas outside the island
            SetWindowExStyle();
            
            // Start desktop visibility monitoring
            _visibilityService = new DesktopVisibilityService(this);
            _visibilityService.VisibilityChanged += OnVisibilityChanged;
            _visibilityService.Start();

            // Wire up panel events from IslandView
            Island.PanelRequested += OnPanelRequested;
        }

        private void OnVisibilityChanged(object? sender, bool visible)
        {
            // Notify ViewModel to pause/resume timers
            Island.SetVisibility(visible);
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Prevent accidental closing
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
            var exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        }

        #region Panel Management

        private void OnPanelRequested(object? sender, PanelType panelType)
        {
            if (panelType == PanelType.ControlCenter)
            {
                if (_currentPanel != null)
                {
                    ClosePanel();
                }
                else
                {
                    OpenControlCenter();
                }
            }
            else if (panelType == PanelType.None)
            {
                ClosePanel();
            }
        }

        private void OpenControlCenter()
        {
            Island.IsHitTestVisible = false;
            ClickAwayBackdrop.Visibility = Visibility.Visible;

            _currentPanel = new ControlCenterPanel();

            OverlayLayer.Children.Clear();
            OverlayLayer.Children.Add(_currentPanel);
            OverlayLayer.Visibility = Visibility.Visible;
            OverlayLayer.IsHitTestVisible = true;
        }

        private void ClosePanel()
        {
            if (_currentPanel == null) return;

            // Stop any Bluetooth discovery when closing
            _currentPanel.StopAllScanning();

            _currentPanel.AnimateOut(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Cleanup to free memory
                    OverlayLayer.Children.Clear();
                    OverlayLayer.Visibility = Visibility.Collapsed;
                    OverlayLayer.IsHitTestVisible = false;
                    ClickAwayBackdrop.Visibility = Visibility.Collapsed;
                    
                    // Dispose and null out panel
                    _currentPanel?.Dispose();
                    _currentPanel = null;

                    Island.IsHitTestVisible = true;
                });
            });
        }

        private void OnBackdropClick(object sender, MouseButtonEventArgs e)
        {
            ClosePanel();
            Island.CloseAllPanels();
        }

        #endregion

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
                    // Close panel first if open
                    if (_currentPanel != null)
                    {
                        ClosePanel();
                    }
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
