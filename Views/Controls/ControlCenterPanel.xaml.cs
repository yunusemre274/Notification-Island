using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NI.Services;

namespace NI.Views.Controls
{
    /// <summary>
    /// Converter for slider fill width calculation.
    /// </summary>
    public class PercentToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percent)
                return percent; // Percentage value (0-100)
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public partial class ControlCenterPanel : UserControl, IDisposable
    {
        private bool _disposed = false;

        // Toggle states (synced with hardware)
        private bool _wifiEnabled = true;
        private bool _bluetoothEnabled = false;
        private bool _airplaneMode = false;
        private bool _hotspotEnabled = false;
        private bool _nightLightEnabled = false;
        private bool _focusEnabled = false;
        private bool _isMuted = false;
        private bool _deviceDropdownOpen = false;
        private string _currentDevice = "";

        // State polling timer (1.5 second interval)
        private DispatcherTimer? _stateTimer;
        private int _lastBrightness = -1;

        // Services
        private BluetoothService? _bluetoothService;

        // Brushes
        private static readonly SolidColorBrush TileActiveBrush = new(Color.FromArgb(0xCC, 0x4C, 0x8D, 0xFF));
        private static readonly SolidColorBrush TileInactiveBrush = new(Color.FromArgb(0xCC, 0x20, 0x20, 0x20));
        private static readonly SolidColorBrush TileDisabledBrush = new(Color.FromArgb(0xCC, 0x18, 0x18, 0x18));
        private static readonly SolidColorBrush HyperOrangeBrush = new(Color.FromRgb(0xFF, 0x95, 0x00));
        private static readonly SolidColorBrush HyperPurpleBrush = new(Color.FromRgb(0xAF, 0x52, 0xDE));
        private static readonly SolidColorBrush MuteActiveBrush = new(Color.FromRgb(0xCC, 0x33, 0x33));
        private static readonly SolidColorBrush MuteInactiveBrush = new(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));

        // Animation durations (faster for HyperOS feel)
        private static readonly Duration TilePressDuration = TimeSpan.FromMilliseconds(80);
        private static readonly Duration TileReleaseDuration = TimeSpan.FromMilliseconds(100);
        private static readonly Duration PanelAnimDuration = TimeSpan.FromMilliseconds(160);

        public ControlCenterPanel()
        {
            InitializeComponent();
            InitializeTileTransforms();
            InitializeServices();
            LoadState();
            LoadDevices();
            LoadBattery();
            LoadWifiNetworks();

            Loaded += OnPanelLoaded;
        }

        private void InitializeTileTransforms()
        {
            // Replace frozen transforms from XAML styles with animatable ones
            var tiles = new[] { WifiTile, BluetoothTile, AirplaneTile, HotspotTile, 
                               NightLightTile, FocusTile, SettingsTile, MuteButton };
            foreach (var tile in tiles)
            {
                if (tile != null)
                {
                    tile.RenderTransformOrigin = new Point(0.5, 0.5);
                    tile.RenderTransform = new ScaleTransform(1, 1);
                }
            }
        }

        private void InitializeServices()
        {
            AudioService.Initialize();
            _bluetoothService = new BluetoothService();
            _bluetoothService.DevicesChanged += (s, e) => Dispatcher.Invoke(LoadBluetoothDevices);

            // Initialize RadioService for real hardware control
            InitializeRadioServiceAsync();

            // Subscribe to hardware state changes
            RadioService.WifiStateChanged += OnWifiHardwareStateChanged;
            RadioService.BluetoothStateChanged += OnBluetoothHardwareStateChanged;

            // Start state polling timer (1.5 second interval)
            _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _stateTimer.Tick += OnStatePollTick;
            _stateTimer.Start();
        }

        private async void InitializeRadioServiceAsync()
        {
            try
            {
                await RadioService.InitializeAsync();
                
                // Sync UI with actual hardware state
                Dispatcher.Invoke(() =>
                {
                    _wifiEnabled = RadioService.IsWifiOn;
                    _bluetoothEnabled = RadioService.IsBluetoothOn;
                    UpdateAllToggleUI();
                    
                    if (_wifiEnabled) LoadWifiNetworks();
                    if (_bluetoothEnabled) LoadBluetoothDevices();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RadioService init error: {ex.Message}");
            }
        }

        private void OnWifiHardwareStateChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _wifiEnabled = RadioService.IsWifiOn;
                UpdateAllToggleUI();
                
                if (_wifiEnabled) LoadWifiNetworks();
            });
        }

        private void OnBluetoothHardwareStateChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _bluetoothEnabled = RadioService.IsBluetoothOn;
                UpdateAllToggleUI();
                
                if (_bluetoothEnabled) LoadBluetoothDevices();
            });
        }

        private void OnStatePollTick(object? sender, EventArgs e)
        {
            // Poll hardware states to keep UI in sync
            PollHardwareStates();
        }

        private async void PollHardwareStates()
        {
            try
            {
                // Check Wi-Fi hardware state
                var (wifiOn, btOn) = await RadioService.GetRadioStatesAsync();
                
                Dispatcher.Invoke(() =>
                {
                    // Update Wi-Fi if changed
                    if (_wifiEnabled != wifiOn)
                    {
                        _wifiEnabled = wifiOn;
                        UpdateAllToggleUI();
                        if (_wifiEnabled) LoadWifiNetworks();
                    }
                    
                    // Update Bluetooth if changed
                    if (_bluetoothEnabled != btOn)
                    {
                        _bluetoothEnabled = btOn;
                        UpdateAllToggleUI();
                        if (_bluetoothEnabled) LoadBluetoothDevices();
                    }
                });

                // Check brightness (for FN key changes)
                int currentBrightness = BrightnessService.GetBrightness();
                if (_lastBrightness != currentBrightness && _lastBrightness != -1)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Only update if user isn't dragging the slider
                        if (!BrightnessSlider.IsMouseCaptured)
                        {
                            BrightnessSlider.Value = currentBrightness;
                            BrightnessText.Text = $"{currentBrightness}%";
                        }
                    });
                }
                _lastBrightness = currentBrightness;

                // Check volume (for hardware key changes)
                Dispatcher.Invoke(() =>
                {
                    if (!VolumeSlider.IsMouseCaptured)
                    {
                        int currentVolume = AudioService.Volume;
                        if ((int)VolumeSlider.Value != currentVolume)
                        {
                            VolumeSlider.Value = currentVolume;
                            VolumeText.Text = $"{currentVolume}%";
                        }
                        
                        bool isMuted = AudioService.IsMuted;
                        if (_isMuted != isMuted)
                        {
                            _isMuted = isMuted;
                            UpdateMuteButton();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"State poll error: {ex.Message}");
            }
        }

        private void OnPanelLoaded(object sender, RoutedEventArgs e)
        {
            AnimateIn();
        }

        #region Panel Animations (Fast HyperOS Style)

        public void AnimateIn()
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Scale from 0.95 to 1.0
            var scaleXAnim = new DoubleAnimation(0.96, 1.0, PanelAnimDuration) { EasingFunction = ease };
            var scaleYAnim = new DoubleAnimation(0.96, 1.0, PanelAnimDuration) { EasingFunction = ease };
            PanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            PanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

            // Slide from -8 to 0
            var slideIn = new DoubleAnimation(-8, 0, PanelAnimDuration) { EasingFunction = ease };
            PanelTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

            // Fade in
            var fadeIn = new DoubleAnimation(0, 1, PanelAnimDuration) { EasingFunction = ease };
            BeginAnimation(OpacityProperty, fadeIn);
        }

        public void AnimateOut(Action? onComplete = null)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var scaleXAnim = new DoubleAnimation(1.0, 0.96, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease };
            var scaleYAnim = new DoubleAnimation(1.0, 0.96, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease };
            PanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            PanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

            var slideOut = new DoubleAnimation(0, -8, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease };
            PanelTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120)) { EasingFunction = ease };
            if (onComplete != null)
                fadeOut.Completed += (s, e) => onComplete();
            BeginAnimation(OpacityProperty, fadeOut);
        }

        #endregion

        #region Tile Press Animations

        private void OnTileMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                EnsureAnimatableTransform(border);
                if (border.RenderTransform is ScaleTransform scale)
                {
                    var anim = new DoubleAnimation(1.0, 1.03, TimeSpan.FromMilliseconds(100))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                }
            }
        }

        private void OnTileMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                EnsureAnimatableTransform(border);
                if (border.RenderTransform is ScaleTransform scale)
                {
                    var anim = new DoubleAnimation(1.0, TileReleaseDuration)
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                }
            }
        }

        private void EnsureAnimatableTransform(Border border)
        {
            if (border.RenderTransform == null || border.RenderTransform.IsFrozen || !(border.RenderTransform is ScaleTransform))
            {
                border.RenderTransformOrigin = new Point(0.5, 0.5);
                border.RenderTransform = new ScaleTransform(1, 1);
            }
        }

        private void AnimateTilePress(Border tile)
        {
            EnsureAnimatableTransform(tile);
            if (tile.RenderTransform is ScaleTransform scale)
            {
                var pressAnim = new DoubleAnimation(0.95, TilePressDuration)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var releaseAnim = new DoubleAnimation(1.0, TileReleaseDuration)
                {
                    BeginTime = TilePressDuration.TimeSpan,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                scale.BeginAnimation(ScaleTransform.ScaleXProperty, pressAnim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, pressAnim);

                // Chain release
                pressAnim.Completed += (s, e) =>
                {
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, releaseAnim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, releaseAnim);
                };
            }
        }

        #endregion

        #region State Loading

        private void LoadState()
        {
            // Sync with real hardware state
            _wifiEnabled = RadioService.IsWifiAvailable ? RadioService.IsWifiOn : WifiService.IsEnabled;
            _bluetoothEnabled = RadioService.IsBluetoothAvailable ? RadioService.IsBluetoothOn : false;
            UpdateAllToggleUI();

            // Audio
            VolumeSlider.Value = AudioService.Volume;
            VolumeText.Text = $"{AudioService.Volume}%";
            _isMuted = AudioService.IsMuted;
            UpdateMuteButton();

            // Brightness
            try
            {
                var brightness = BrightnessService.GetBrightness();
                BrightnessSlider.Value = brightness;
                BrightnessText.Text = $"{brightness}%";
                _lastBrightness = brightness;
            }
            catch
            {
                BrightnessSlider.Value = 75;
                BrightnessText.Text = "75%";
                _lastBrightness = 75;
            }

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
                    Background = isSelected ? new SolidColorBrush(Color.FromArgb(0x40, 0x4C, 0x8D, 0xFF)) : Brushes.Transparent,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand,
                    Tag = device.Id
                };

                item.MouseEnter += (s, e) => { if (!isSelected) item.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)); };
                item.MouseLeave += (s, e) => { if (!isSelected) item.Background = Brushes.Transparent; };
                item.MouseLeftButtonDown += OnDeviceSelected;

                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                stack.Children.Add(new TextBlock { Text = device.Icon, FontSize = 14, Margin = new Thickness(0, 0, 10, 0) });
                stack.Children.Add(new TextBlock { Text = device.Name, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Medium });

                if (isSelected)
                    stack.Children.Add(new TextBlock { Text = " ✓", Foreground = TileActiveBrush, FontWeight = FontWeights.Bold });

                item.Child = stack;
                DeviceList.Children.Add(item);
            }
        }

        private void LoadBattery()
        {
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
                        BatteryIcon.Text = isCharging ? "⚡" : batteryPercent switch
                        {
                            > 80 => "🔋",
                            > 50 => "🔋",
                            > 20 => "🪫",
                            _ => "🪫"
                        };
                        return;
                    }
                }

                // Desktop
                BatteryText.Text = "AC";
                BatteryStatus.Text = "Desktop";
                BatteryIcon.Text = "🔌";
            }
            catch
            {
                BatteryText.Text = "AC";
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
                    WifiSubLabel.Text = connected.SSID;
                }
                else
                {
                    WifiSubLabel.Text = "Connected";
                }
            }
            catch
            {
                WifiSubLabel.Text = "Connected";
            }
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
                    BluetoothSubLabel.Text = connected.Name;
                }
                else
                {
                    BluetoothSubLabel.Text = "On";
                }
            }
            catch
            {
                BluetoothSubLabel.Text = "On";
            }
        }

        #endregion

        #region Toggle Handlers

        private async void OnWifiToggle(object sender, MouseButtonEventArgs e)
        {
            if (_airplaneMode) return;
            AnimateTilePress(WifiTile);

            try
            {
                // Use REAL hardware radio control
                bool success = await RadioService.ToggleWifiAsync();
                
                if (success)
                {
                    _wifiEnabled = RadioService.IsWifiOn;
                    UpdateAllToggleUI();
                    
                    if (_wifiEnabled)
                        LoadWifiNetworks();
                }
                else
                {
                    // Fallback to software toggle
                    _wifiEnabled = !_wifiEnabled;
                    await WifiService.SetWifiStateAsync(_wifiEnabled);
                    UpdateAllToggleUI();
                    
                    if (_wifiEnabled)
                        LoadWifiNetworks();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WiFi toggle error: {ex.Message}");
                _wifiEnabled = !_wifiEnabled;
                UpdateAllToggleUI();
            }
        }

        private async void OnBluetoothToggle(object sender, MouseButtonEventArgs e)
        {
            if (_airplaneMode) return;
            AnimateTilePress(BluetoothTile);

            try
            {
                // Use REAL hardware radio control
                bool success = await RadioService.ToggleBluetoothAsync();
                
                if (success)
                {
                    _bluetoothEnabled = RadioService.IsBluetoothOn;
                    UpdateAllToggleUI();
                    
                    if (_bluetoothEnabled)
                        LoadBluetoothDevices();
                }
                else
                {
                    // Fallback to software toggle
                    _bluetoothEnabled = !_bluetoothEnabled;
                    if (_bluetoothService != null)
                        await _bluetoothService.SetStateAsync(_bluetoothEnabled);
                    UpdateAllToggleUI();
                    
                    if (_bluetoothEnabled)
                        LoadBluetoothDevices();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bluetooth toggle error: {ex.Message}");
                _bluetoothEnabled = !_bluetoothEnabled;
                UpdateAllToggleUI();
            }
        }

        private async void OnAirplaneToggle(object sender, MouseButtonEventArgs e)
        {
            AnimateTilePress(AirplaneTile);

            _airplaneMode = !_airplaneMode;
            
            if (_airplaneMode)
            {
                // Turn off all radios
                try
                {
                    await RadioService.SetWifiStateAsync(false);
                    await RadioService.SetBluetoothStateAsync(false);
                    _wifiEnabled = false;
                    _bluetoothEnabled = false;
                }
                catch
                {
                    _wifiEnabled = false;
                    _bluetoothEnabled = false;
                }
            }
            else
            {
                // Restore previous state - turn Wi-Fi back on by default
                try
                {
                    await RadioService.SetWifiStateAsync(true);
                    _wifiEnabled = RadioService.IsWifiOn;
                }
                catch { }
            }
            
            UpdateAllToggleUI();
        }

        private void OnHotspotToggle(object sender, MouseButtonEventArgs e)
        {
            if (_airplaneMode) return;
            AnimateTilePress(HotspotTile);

            _hotspotEnabled = !_hotspotEnabled;
            UpdateAllToggleUI();
            
            // Open hotspot settings if enabled
            if (_hotspotEnabled)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:network-mobilehotspot") { UseShellExecute = true });
                }
                catch { }
            }
        }

        private void OnNightLightToggle(object sender, MouseButtonEventArgs e)
        {
            AnimateTilePress(NightLightTile);

            _nightLightEnabled = !_nightLightEnabled;
            UpdateAllToggleUI();

            // Toggle Windows Night Light via registry
            try
            {
                ToggleNightLight(_nightLightEnabled);
            }
            catch
            {
                // Fallback to settings
                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:nightlight") { UseShellExecute = true });
                }
                catch { }
            }
        }

        private void ToggleNightLight(bool enabled)
        {
            try
            {
                // Try to toggle Night Light via Windows Settings URI
                var uri = enabled ? "ms-settings:nightlight" : "ms-settings:nightlight";
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            }
            catch { }
        }

        private void OnFocusToggle(object sender, MouseButtonEventArgs e)
        {
            AnimateTilePress(FocusTile);

            _focusEnabled = !_focusEnabled;
            UpdateAllToggleUI();

            // Toggle Focus Assist
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:quiethours") { UseShellExecute = true });
            }
            catch { }
        }

        private void UpdateAllToggleUI()
        {
            // WiFi - reflect REAL hardware state
            bool wifiActive = _wifiEnabled && !_airplaneMode;
            WifiTile.Background = wifiActive ? TileActiveBrush : (_airplaneMode ? TileDisabledBrush : TileInactiveBrush);
            WifiLabel.Foreground = wifiActive ? Brushes.White : new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
            
            // Update Wi-Fi sub-label based on real state
            if (!wifiActive)
            {
                WifiSubLabel.Text = _airplaneMode ? "Kapalı" : "Kapalı";
            }
            else
            {
                WifiSubLabel.Text = WifiService.ConnectedNetwork ?? "Bağlı";
            }
            SetTileGlow(WifiTile, wifiActive);
            
            // Update Wi-Fi icon opacity
            var wifiIcon = WifiTile.FindName("WifiIcon") as System.Windows.Controls.Image;
            if (wifiIcon != null) wifiIcon.Opacity = wifiActive ? 1.0 : 0.5;

            // Bluetooth - reflect REAL hardware state
            bool btActive = _bluetoothEnabled && !_airplaneMode;
            BluetoothTile.Background = btActive ? TileActiveBrush : (_airplaneMode ? TileDisabledBrush : TileInactiveBrush);
            BluetoothLabel.Foreground = btActive ? Brushes.White : new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
            BluetoothSubLabel.Text = btActive ? "Açık" : "Kapalı";
            SetTileGlow(BluetoothTile, btActive);

            // Airplane
            AirplaneTile.Background = _airplaneMode ? HyperOrangeBrush : TileInactiveBrush;
            SetTileGlow(AirplaneTile, _airplaneMode, Color.FromRgb(0xFF, 0x95, 0x00));

            // Hotspot
            bool hotspotActive = _hotspotEnabled && !_airplaneMode;
            HotspotTile.Background = hotspotActive ? TileActiveBrush : (_airplaneMode ? TileDisabledBrush : TileInactiveBrush);
            SetTileGlow(HotspotTile, hotspotActive);

            // Night Light
            NightLightTile.Background = _nightLightEnabled ? HyperOrangeBrush : TileInactiveBrush;
            SetTileGlow(NightLightTile, _nightLightEnabled, Color.FromRgb(0xFF, 0x95, 0x00));

            // Focus
            FocusTile.Background = _focusEnabled ? HyperPurpleBrush : TileInactiveBrush;
            SetTileGlow(FocusTile, _focusEnabled, Color.FromRgb(0xAF, 0x52, 0xDE));
        }

        private void SetTileGlow(Border tile, bool active, Color? glowColor = null)
        {
            if (active)
            {
                var color = glowColor ?? Color.FromRgb(0x4C, 0x8D, 0xFF);
                tile.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = color,
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.4
                };
            }
            else
            {
                tile.Effect = null;
            }
        }

        #endregion

        #region Slider Handlers

        private void OnBrightnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BrightnessText == null) return;

            int brightness = (int)e.NewValue;
            BrightnessText.Text = $"{brightness}%";

            // Real brightness control
            try
            {
                BrightnessService.SetBrightness(brightness);
            }
            catch { }
        }

        private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumeText == null) return;

            int volume = (int)e.NewValue;
            AudioService.Volume = volume;
            VolumeText.Text = $"{volume}%";
        }

        private void OnMuteClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AnimateTilePress(MuteButton);

            _isMuted = !_isMuted;
            AudioService.IsMuted = _isMuted;
            UpdateMuteButton();
        }

        private void UpdateMuteButton()
        {
            if (_isMuted)
            {
                MuteButton.Background = MuteActiveBrush;
                MuteIcon.Text = "🔊";
            }
            else
            {
                MuteButton.Background = MuteInactiveBrush;
                MuteIcon.Text = "🔇";
            }
        }

        #endregion

        #region Output Device

        private void OnOutputDeviceClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _deviceDropdownOpen = !_deviceDropdownOpen;

            if (_deviceDropdownOpen)
            {
                DeviceDropdown.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100));
                DeviceDropdown.BeginAnimation(OpacityProperty, fadeIn);
            }
            else
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(80));
                fadeOut.Completed += (s, args) => DeviceDropdown.Visibility = Visibility.Collapsed;
                DeviceDropdown.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private void OnDeviceSelected(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string deviceId)
            {
                _currentDevice = deviceId;
                AudioService.SetOutputDevice(deviceId);
                UpdateOutputDevice();
                LoadDevices();

                // Close dropdown with animation
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(80));
                fadeOut.Completed += (s, args) =>
                {
                    DeviceDropdown.Visibility = Visibility.Collapsed;
                    _deviceDropdownOpen = false;
                };
                DeviceDropdown.BeginAnimation(OpacityProperty, fadeOut);
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

        private void OnEditClick(object sender, MouseButtonEventArgs e)
        {
            // Future: Open Control Center edit mode
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:controlcenter") { UseShellExecute = true });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
                }
                catch { }
            }
        }

        #endregion

        #region Resource Cleanup

        public void StopAllScanning()
        {
            _stateTimer?.Stop();
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

                // Stop state polling
                if (_stateTimer != null)
                {
                    _stateTimer.Stop();
                    _stateTimer = null;
                }

                // Unsubscribe from radio state changes
                RadioService.WifiStateChanged -= OnWifiHardwareStateChanged;
                RadioService.BluetoothStateChanged -= OnBluetoothHardwareStateChanged;

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
