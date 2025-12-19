using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NI.Core;
using NI.Services;
using NI.Views;
using NI.Views.Controls;

namespace NI
{
    public partial class MainWindow : Window
    {
        private DesktopVisibilityService? _visibilityService;
        private IntPtr _hwnd;
        private ControlCenterPanel? _controlPanel;

        // TopBarStateController integration
        private TopBarStateController _stateController = TopBarStateController.Instance;

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
            // ESC key closes control panel and returns to Idle
            if (e.Key == Key.Escape && _controlPanel != null && _controlPanel.Visibility == Visibility.Visible)
            {
                System.Diagnostics.Debug.WriteLine("[CRITICAL] ESC pressed -> Instant close");
                ClosePanel();
                e.Handled = true;
            }
        }


        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            // Position at top center
            PositionWindow();

            // Make window click-through for areas outside the island
            SetWindowExStyle();

            // CRITICAL: Preload Control Panel at startup (INSTANT response on click)
            System.Diagnostics.Debug.WriteLine("[CRITICAL] Preloading Control Panel...");
            _controlPanel = new ControlCenterPanel();
            _controlPanel.Opacity = 0; // Hidden initially
            _controlPanel.Visibility = Visibility.Collapsed;
            OverlayLayer.Children.Add(_controlPanel);
            System.Diagnostics.Debug.WriteLine("[CRITICAL] Control Panel preloaded (ready for instant open)");

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
                // CRITICAL: Toggle panel (instant response)
                if (_controlPanel != null && _controlPanel.Visibility == Visibility.Visible)
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
            if (_controlPanel == null) return;

            // CRITICAL: UI update FIRST (instant response <100ms)
            System.Diagnostics.Debug.WriteLine("[CRITICAL] OpenControlCenter -> INSTANT show");

            // 1. Set state immediately
            _stateController.SetMode(TopBarMode.ControlPanel);

            // 2. Show UI instantly (just visibility toggle)
            Island.IsHitTestVisible = false;
            ClickAwayBackdrop.Visibility = Visibility.Visible;
            OverlayLayer.Visibility = Visibility.Visible;
            OverlayLayer.IsHitTestVisible = true;

            // 3. Animate panel in (already preloaded)
            _controlPanel.Opacity = 1;
            _controlPanel.Visibility = Visibility.Visible;
            _controlPanel.AnimateIn();

            System.Diagnostics.Debug.WriteLine("[CRITICAL] Control Panel shown (instant)");
        }

        private void ClosePanel()
        {
            if (_controlPanel == null || _controlPanel.Visibility != Visibility.Visible) return;

            System.Diagnostics.Debug.WriteLine("[CRITICAL] ClosePanel -> INSTANT hide");

            // Stop any Bluetooth discovery when closing
            _controlPanel.StopAllScanning();

            // CRITICAL: Animate out, then hide (fast)
            _controlPanel.AnimateOut(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Hide UI (don't destroy - keep preloaded)
                    _controlPanel.Visibility = Visibility.Collapsed;
                    _controlPanel.Opacity = 0;
                    OverlayLayer.Visibility = Visibility.Collapsed;
                    OverlayLayer.IsHitTestVisible = false;
                    ClickAwayBackdrop.Visibility = Visibility.Collapsed;

                    Island.IsHitTestVisible = true;

                    // Return to Idle
                    System.Diagnostics.Debug.WriteLine("[CRITICAL] ClosePanel -> ReturnToIdle()");
                    _stateController.ReturnToIdle();
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
                    if (_controlPanel != null && _controlPanel.Visibility == Visibility.Visible)
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
