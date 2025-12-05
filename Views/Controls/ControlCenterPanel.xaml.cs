using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NI.Services;

namespace NI.Views.Controls
{
    public partial class ControlCenterPanel : UserControl, IDisposable
    {
        private bool _disposed = false;
        // Toggle states
        private bool _wifiEnabled = true;
        private bool _bluetoothEnabled = false;
        private bool _airplaneMode = false;
        private bool _hotspotEnabled = false;
        private bool _nightLightEnabled = false;
        private bool _accessibilityEnabled = false;
        private bool _isMuted = false;
        private bool _deviceDropdownOpen = false;
        private string _currentDevice = "";

        // Services
        private BluetoothService? _bluetoothService;

        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(0x00, 0x78, 0xD4));
        private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(0x55, 0x55, 0x55));
        private static readonly SolidColorBrush DisabledBrush = new(Color.FromRgb(0x33, 0x33, 0x33));

        public ControlCenterPanel()
        {
            InitializeComponent();
            InitializeServices();
            LoadState();
            LoadDevices();
            LoadBattery();
            LoadWifiNetworks();

            // Animate in after loaded
            Loaded += OnPanelLoaded;
        }

        private void InitializeServices()
        {
            // Initialize Audio Service
            AudioService.Initialize();

            // Initialize Bluetooth Service
            _bluetoothService = new BluetoothService();
            _bluetoothService.DevicesChanged += (s, e) => Dispatcher.Invoke(LoadBluetoothDevices);
        }

        private void OnPanelLoaded(object sender, RoutedEventArgs e)
        {
            AnimateIn();
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

        private void LoadState()
        {
            _wifiEnabled = WifiService.IsEnabled;
            UpdateToggleUI();

            // Load audio state
            VolumeSlider.Value = AudioService.Volume;
            VolumeText.Text = $"{AudioService.Volume}%";
            _isMuted = AudioService.IsMuted;
            UpdateVolumeIcon();
            UpdateMuteButton();

            _currentDevice = AudioService.CurrentDeviceId;
            UpdateOutputDevice();
        }

        private void LoadDevices()
        {
            DeviceList.Children.Clear();
            var devices = AudioService.GetOutputDevices();

            foreach (var device in devices)
            {
                var isSelected = device.Id == _currentDevice;
                var item = new Border
                {
                    Background = isSelected ? new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x78, 0xD4)) : Brushes.Transparent,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand,
                    Tag = device.Id
                };

                item.MouseEnter += (s, e) => { if (!isSelected) item.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)); };
                item.MouseLeave += (s, e) => { if (!isSelected) item.Background = Brushes.Transparent; };
                item.MouseLeftButtonDown += OnDeviceSelected;

                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                stack.Children.Add(new TextBlock { Text = device.Icon, FontSize = 14, Margin = new Thickness(0, 0, 8, 0) });
                stack.Children.Add(new TextBlock { Text = device.Name, Foreground = Brushes.White, FontSize = 12 });

                if (isSelected)
                    stack.Children.Add(new TextBlock { Text = " ✓", Foreground = ActiveBrush, FontWeight = FontWeights.Bold });

                item.Child = stack;
                DeviceList.Children.Add(item);
            }
        }

        private void LoadBattery()
        {
            // Get real battery info using WinRT
            try
            {
                var batteryReport = Windows.Devices.Power.Battery.AggregateBattery.GetReport();
                if (batteryReport.Status != Windows.System.Power.BatteryStatus.NotPresent)
                {
                    double? remaining = batteryReport.RemainingCapacityInMilliwattHours;
                    double? full = batteryReport.FullChargeCapacityInMilliwattHours;
                    
                    if (remaining.HasValue && full.HasValue && full.Value > 0)
                    {
                        int batteryPercent = (int)((remaining.Value / full.Value) * 100);
                        bool isCharging = batteryReport.Status == Windows.System.Power.BatteryStatus.Charging;
                        
                        BatteryText.Text = $"{batteryPercent}%";
                        BatteryStatus.Text = isCharging ? "Charging" : "On battery";
                        BatteryIcon.Text = isCharging ? "🔌" : batteryPercent switch
                        {
                            > 80 => "🔋",
                            > 50 => "🔋",
                            > 20 => "🪫",
                            _ => "🪫"
                        };
                        return;
                    }
                }
                
                // Desktop PC without battery
                BatteryText.Text = "N/A";
                BatteryStatus.Text = "Desktop";
                BatteryIcon.Text = "🔌";
            }
            catch
            {
                BatteryText.Text = "N/A";
                BatteryStatus.Text = "Desktop";
                BatteryIcon.Text = "🔌";
            }
        }

        private async void LoadWifiNetworks()
        {
            if (!_wifiEnabled) return;

            try
            {
                var networks = await WifiService.GetAvailableNetworksAsync();
                var connected = networks.FirstOrDefault(n => n.IsConnected);
                if (connected != null)
                {
                    WifiLabel.Text = connected.SSID;
                }
            }
            catch { }
        }

        private async void LoadBluetoothDevices()
        {
            if (_bluetoothService == null || !_bluetoothEnabled) return;

            try
            {
                var devices = await _bluetoothService.GetPairedDevicesAsync();
                var connected = devices.FirstOrDefault(d => d.IsConnected);
                if (connected != null)
                {
                    BluetoothLabel.Text = connected.Name;
                }
            }
            catch { }
        }

        #region Toggle Handlers

        private void OnWifiToggle(object sender, MouseButtonEventArgs e)
        {
            if (_airplaneMode) return;
            _wifiEnabled = !_wifiEnabled;
            WifiService.ToggleWifi();
            UpdateToggleUI();

            if (_wifiEnabled)
            {
                LoadWifiNetworks();
            }
        }

        private void OnBluetoothToggle(object sender, MouseButtonEventArgs e)
        {
            if (_airplaneMode) return;
            _bluetoothEnabled = !_bluetoothEnabled;
            _bluetoothService?.Toggle();
            UpdateToggleUI();

            if (_bluetoothEnabled)
            {
                LoadBluetoothDevices();
            }
        }

        private void OnAirplaneToggle(object sender, MouseButtonEventArgs e)
        {
            _airplaneMode = !_airplaneMode;
            if (_airplaneMode)
            {
                _wifiEnabled = false;
                _bluetoothEnabled = false;
                WifiService.ToggleWifi(); // Turn off
            }
            UpdateToggleUI();
        }

        private void OnHotspotToggle(object sender, MouseButtonEventArgs e)
        {
            if (_airplaneMode) return;
            _hotspotEnabled = !_hotspotEnabled;
            UpdateToggleUI();
        }

        private void OnNightLightToggle(object sender, MouseButtonEventArgs e)
        {
            _nightLightEnabled = !_nightLightEnabled;
            UpdateToggleUI();
        }

        private void OnAccessibilityToggle(object sender, MouseButtonEventArgs e)
        {
            _accessibilityEnabled = !_accessibilityEnabled;
            UpdateToggleUI();
        }

        private void UpdateToggleUI()
        {
            // WiFi
            WifiToggle.Background = _wifiEnabled && !_airplaneMode ? ActiveBrush : (_airplaneMode ? DisabledBrush : InactiveBrush);
            WifiLabel.Text = _wifiEnabled && !_airplaneMode ? WifiService.ConnectedNetwork ?? "WiFi" : "WiFi";

            // Bluetooth
            BluetoothToggle.Background = _bluetoothEnabled && !_airplaneMode ? ActiveBrush : (_airplaneMode ? DisabledBrush : InactiveBrush);

            // Airplane Mode
            AirplaneToggle.Background = _airplaneMode ? ActiveBrush : InactiveBrush;

            // Hotspot
            HotspotToggle.Background = _hotspotEnabled && !_airplaneMode ? ActiveBrush : (_airplaneMode ? DisabledBrush : InactiveBrush);

            // Night Light
            NightLightToggle.Background = _nightLightEnabled ? ActiveBrush : InactiveBrush;

            // Accessibility
            AccessibilityToggle.Background = _accessibilityEnabled ? ActiveBrush : InactiveBrush;
        }

        #endregion

        #region Slider Handlers

        private void OnBrightnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BrightnessText != null)
                BrightnessText.Text = $"{(int)e.NewValue}%";
        }

        private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumeText == null) return;

            var volume = (int)e.NewValue;
            AudioService.Volume = volume;
            VolumeText.Text = $"{volume}%";
            UpdateVolumeIcon();
        }

        private void OnMuteClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _isMuted = !_isMuted;
            AudioService.IsMuted = _isMuted;
            UpdateMuteButton();
            UpdateVolumeIcon();
        }

        private void UpdateVolumeIcon()
        {
            if (VolumeIcon == null) return;
            VolumeIcon.Text = AudioService.VolumeIcon;
        }

        private void UpdateMuteButton()
        {
            if (MuteButton == null) return;

            if (_isMuted)
            {
                MuteButton.Background = new SolidColorBrush(Color.FromRgb(0xCC, 0x33, 0x33));
                MuteIcon.Text = "🔊";
            }
            else
            {
                MuteButton.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
                MuteIcon.Text = "🔇";
            }
        }

        #endregion

        #region Output Device

        private void OnOutputDeviceClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _deviceDropdownOpen = !_deviceDropdownOpen;
            DeviceDropdown.Visibility = _deviceDropdownOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnDeviceSelected(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string deviceId)
            {
                _currentDevice = deviceId;
                AudioService.SetOutputDevice(deviceId);
                UpdateOutputDevice();
                LoadDevices();
                DeviceDropdown.Visibility = Visibility.Collapsed;
                _deviceDropdownOpen = false;
            }
        }

        private void UpdateOutputDevice()
        {
            var devices = AudioService.GetOutputDevices();
            var device = devices.Find(d => d.Id == _currentDevice);
            OutputDeviceText.Text = device?.Name ?? "Speakers";
        }

        #endregion

        #region Bottom Buttons

        private void OnSettingsClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
            }
            catch { }
        }

        #endregion

        #region Resource Cleanup

        /// <summary>
        /// Stops any active scanning (Bluetooth discovery, etc.)
        /// </summary>
        public void StopAllScanning()
        {
            _bluetoothService?.StopDiscovery();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                StopAllScanning();
                
                if (_bluetoothService != null)
                {
                    _bluetoothService.Dispose();
                    _bluetoothService = null;
                }
            }

            _disposed = true;
        }

        #endregion
    }
}
