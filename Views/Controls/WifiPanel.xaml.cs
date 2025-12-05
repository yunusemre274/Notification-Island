using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NI.Services;

namespace NI.Views.Controls
{
    public partial class WifiPanel : UserControl
    {
        public WifiPanel()
        {
            InitializeComponent();
            RefreshNetworks();
            UpdateToggleState();
        }

        public void AnimateIn()
        {
            var slideIn = new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            PanelTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, fadeIn);
        }

        public void AnimateOut(Action? onComplete = null)
        {
            var slideOut = new DoubleAnimation(0, -10, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            
            if (onComplete != null)
                fadeOut.Completed += (s, e) => onComplete();
            
            PanelTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void RefreshNetworks()
        {
            NetworkList.Children.Clear();

            var networks = WifiService.GetAvailableNetworks();
            var connected = WifiService.ConnectedNetwork;

            // Update connected display
            ConnectedSSID.Text = connected ?? "";
            ConnectedIcon.Text = WifiService.IsEnabled ? "📶" : "📵";

            foreach (var network in networks)
            {
                if (network.IsConnected) continue; // Skip connected (shown above)

                var item = CreateNetworkItem(network);
                NetworkList.Children.Add(item);
            }
        }

        private Border CreateNetworkItem(WifiNetwork network)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Tag = network.SSID
            };

            border.MouseEnter += (s, e) => border.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
            border.MouseLeave += (s, e) => border.Background = Brushes.Transparent;
            border.MouseLeftButtonDown += OnNetworkClick;

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            // Signal icon
            var signalText = new TextBlock
            {
                Text = network.SignalIcon,
                FontSize = 14,
                Margin = new Thickness(0, 0, 8, 0),
                Opacity = network.SignalStrength / 100.0
            };
            stack.Children.Add(signalText);

            // SSID
            var ssidText = new TextBlock
            {
                Text = network.SSID,
                Foreground = Brushes.White,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(ssidText);

            // Lock icon
            if (network.IsSecured)
            {
                var lockText = new TextBlock
                {
                    Text = "🔒",
                    FontSize = 10,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.7
                };
                stack.Children.Add(lockText);
            }

            border.Child = stack;
            return border;
        }

        private void OnNetworkClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string ssid)
            {
                WifiService.Connect(ssid);
                RefreshNetworks();
            }
        }

        private void OnWifiToggleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            WifiService.ToggleWifi();
            UpdateToggleState();
            RefreshNetworks();
        }

        private void UpdateToggleState()
        {
            if (WifiService.IsEnabled)
            {
                WifiToggle.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
                ToggleKnob.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                WifiToggle.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                ToggleKnob.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        private void OnRefreshClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            // Spin animation
            var rotate = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(500))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            RefreshIcon.RenderTransform = new RotateTransform();
            RefreshIcon.RenderTransformOrigin = new Point(0.5, 0.5);
            RefreshIcon.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, rotate);

            RefreshNetworks();
        }
    }
}
