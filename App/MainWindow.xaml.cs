using System;
using System.Windows;
using System.Windows.Interop;
using NI.App.Services;

namespace NI.App
{
    public partial class MainWindow : Window
    {
        private DesktopVisibilityService? _visibilityService;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PositionWindow();
            
            // Start desktop visibility monitoring
            _visibilityService = new DesktopVisibilityService(this);
            _visibilityService.Start();
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _visibilityService?.Stop();
        }

        private void PositionWindow()
        {
            var screen = SystemParameters.WorkArea;
            Left = (screen.Width - Width) / 2;
            Top = 20; // 20px from top edge
        }
    }
}
