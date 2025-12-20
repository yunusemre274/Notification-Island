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

        // PHASE 0: State polling timer REMOVED - use events only
        private int _lastBrightness = -1;

        // Services
        private BluetoothService? _bluetoothService;

        // System metrics
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramAvailableCounter;
        private DispatcherTimer? _metricsTimer;
        private float _currentCpu = 0f;
        private ulong _totalRamBytes = 0;

        // Brushes
        private static readonly SolidColorBrush TileActiveBrush = new(Color.FromArgb(0xCC, 0x3A, 0x7B, 0xFF));
        private static readonly SolidColorBrush TileInactiveBrush = new(Color.FromRgb(0x2C, 0x2C, 0x2E));
        private static readonly SolidColorBrush TileDisabledBrush = new(Color.FromRgb(0x1C, 0x1C, 0x1E));
        private static readonly SolidColorBrush HyperOrangeBrush = new(Color.FromRgb(0xFF, 0x8C, 0x1A));
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
            InitializeSystemMetrics();
            LoadState();
            LoadDevices();
            LoadBattery();
            LoadWifiNetworks();

            Loaded += OnPanelLoaded;
        }

        private void InitializeTileTransforms()
        {
            // Replace frozen transforms from XAML styles with animatable ones
            var tiles = new[] { WifiTile, BluetoothTile, AirplaneTile,
                               NightLightTile, FocusTile, SettingsTile };
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

            // PHASE 0: State polling timer REMOVED - rely on events and manual checks only
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

        private void InitializeSystemMetrics()
        {
            try
            {
                // Initialize CPU performance counter
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // First call always returns 0, prime it

                // Initialize RAM performance counter
                _ramAvailableCounter = new PerformanceCounter("Memory", "Available Bytes");

                // Get total RAM using WMI
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher(
                        "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                    foreach (var obj in searcher.Get())
                    {
                        _totalRamBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                        break;
                    }
                }
                catch
                {
                    _totalRamBytes = 16UL * 1024 * 1024 * 1024; // Fallback: 16GB
                }

                // Create timer for updating metrics (every 1000ms)
                _metricsTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1000)
                };
                _metricsTimer.Tick += OnMetricsTimerTick;
                _metricsTimer.Start();

                // Initial update
                UpdateSystemMetrics();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"System metrics init error: {ex.Message}");
            }
        }

        private void OnMetricsTimerTick(object? sender, EventArgs e)
        {
            try
            {
                UpdateSystemMetrics();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Metrics update error: {ex.Message}");
            }
        }

        private void UpdateSystemMetrics()
        {
            // Update CPU
            UpdateCpuUsage();

            // Update RAM
            UpdateRamUsage();

            // Update SSD
            UpdateSsdUsage();
        }

        private void UpdateCpuUsage()
        {
            if (_cpuCounter == null || CpuArc == null) return;

            try
            {
                _currentCpu = _cpuCounter.NextValue();
                float percentage = Math.Min(_currentCpu, 100f);

                // Draw CPU arc (0-360 degrees based on percentage)
                double angle = (percentage / 100.0) * 360.0;
                double radius = 42; // (96 - 12) / 2 = 42 (radius to center of stroke)
                double centerX = 48;
                double centerY = 48;

                // Calculate arc path
                if (angle >= 360)
                {
                    // Full circle
                    var geometry = new EllipseGeometry(new Point(centerX, centerY), radius, radius);
                    CpuArc.Data = geometry;
                }
                else if (angle > 0)
                {
                    // Partial arc
                    double startAngle = -90; // Start from top
                    double endAngle = startAngle + angle;

                    double startX = centerX + radius * Math.Cos(startAngle * Math.PI / 180);
                    double startY = centerY + radius * Math.Sin(startAngle * Math.PI / 180);
                    double endX = centerX + radius * Math.Cos(endAngle * Math.PI / 180);
                    double endY = centerY + radius * Math.Sin(endAngle * Math.PI / 180);

                    bool isLargeArc = angle > 180;
                    var pathFigure = new PathFigure { StartPoint = new Point(startX, startY) };
                    pathFigure.Segments.Add(new ArcSegment
                    {
                        Point = new Point(endX, endY),
                        Size = new Size(radius, radius),
                        IsLargeArc = isLargeArc,
                        SweepDirection = SweepDirection.Clockwise
                    });

                    var pathGeometry = new PathGeometry();
                    pathGeometry.Figures.Add(pathFigure);
                    CpuArc.Data = pathGeometry;
                }
                else
                {
                    // No arc
                    CpuArc.Data = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CPU update error: {ex.Message}");
            }
        }

        private void UpdateRamUsage()
        {
            if (RamFill == null || RamText == null || _ramAvailableCounter == null) return;

            try
            {
                // Get available RAM in bytes
                float availableBytes = _ramAvailableCounter.NextValue();
                ulong usedRam = _totalRamBytes - (ulong)availableBytes;

                double usagePercent = (double)usedRam / _totalRamBytes * 100.0;
                int percentage = (int)Math.Round(usagePercent);

                // Update RAM fill width (240px total width)
                double maxWidth = 240;
                RamFill.Width = (percentage / 100.0) * maxWidth;

                // Update text
                RamText.Text = $"RAM {percentage}%";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RAM update error: {ex.Message}");
            }
        }

        private void UpdateSsdUsage()
        {
            if (SsdUsedColumn == null || SsdFreeColumn == null ||
                SsdUsedText == null || SsdFreeText == null) return;

            try
            {
                var drive = new System.IO.DriveInfo("C:\\");
                if (drive.IsReady)
                {
                    long totalBytes = drive.TotalSize;
                    long freeBytes = drive.AvailableFreeSpace;
                    long usedBytes = totalBytes - freeBytes;

                    // Convert to GB
                    int usedGB = (int)(usedBytes / (1024 * 1024 * 1024));
                    int freeGB = (int)(freeBytes / (1024 * 1024 * 1024));

                    // Calculate proportion for grid columns
                    double usedProportion = (double)usedBytes / totalBytes;
                    double freeProportion = 1.0 - usedProportion;

                    // Update column widths
                    SsdUsedColumn.Width = new GridLength(usedProportion, GridUnitType.Star);
                    SsdFreeColumn.Width = new GridLength(freeProportion, GridUnitType.Star);

                    // Update text
                    SsdUsedText.Text = $"{usedGB} GB";
                    SsdFreeText.Text = $"{freeGB} GB";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SSD update error: {ex.Message}");
            }
        }

        // PHASE 0: OnStatePollTick and PollHardwareStates REMOVED
        // Hardware state changes now handled by events only

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
            // UpdateMuteButton(); // Removed: MuteButton no longer exists

            // Initialize vertical volume bar fill
            if (VolumeFill != null)
            {
                double maxHeight = 480;
                VolumeFill.Height = (AudioService.Volume / 100.0) * maxHeight;
            }

            // Brightness
            try
            {
                var brightness = BrightnessService.GetBrightness();
                BrightnessSlider.Value = brightness;
                // BrightnessText removed from UI
                _lastBrightness = brightness;
            }
            catch
            {
                BrightnessSlider.Value = 75;
                // BrightnessText removed from UI
                _lastBrightness = 75;
            }

            _currentDevice = AudioService.CurrentDeviceId;
            UpdateOutputDevice();
        }

        private void LoadDevices()
        {
            // DeviceList removed from new UI design - method disabled
            /*
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
            */
        }

        private void LoadBattery()
        {
            // Battery UI removed from new design - method disabled
        }

        private async void LoadWifiNetworks()
        {
            // WifiSubLabel removed from new design - method disabled
        }

        private async void LoadBluetoothDevices()
        {
            // BluetoothSubLabel removed from new design - method disabled
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
            // AnimateTilePress(HotspotTile); // HotspotTile removed from new design

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
            // WiFi - Active: purple-pink gradient, Inactive: white with black text
            bool wifiActive = _wifiEnabled && !_airplaneMode;
            if (wifiActive)
            {
                var wifiGradient = new LinearGradientBrush();
                wifiGradient.StartPoint = new Point(0, 0.5);
                wifiGradient.EndPoint = new Point(1, 0.5);
                wifiGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x8B, 0x2C, 0xF5), 0));
                wifiGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xE9, 0x1E, 0x63), 1));
                WifiTile.Background = wifiGradient;
                WifiLabel.Foreground = Brushes.White;
                // WifiIcon color is controlled by the image resource
            }
            else
            {
                WifiTile.Background = Brushes.White;
                WifiLabel.Foreground = Brushes.Black;
            }

            // Bluetooth - Active: orange-red gradient, Inactive: white with black text
            bool btActive = _bluetoothEnabled && !_airplaneMode;
            if (btActive)
            {
                var btGradient = new LinearGradientBrush();
                btGradient.StartPoint = new Point(0, 0.5);
                btGradient.EndPoint = new Point(1, 0.5);
                btGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0x98, 0x00), 0));
                btGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xF4, 0x43, 0x36), 1));
                BluetoothTile.Background = btGradient;
                BluetoothLabel.Foreground = Brushes.White;
            }
            else
            {
                BluetoothTile.Background = Brushes.White;
                BluetoothLabel.Foreground = Brushes.Black;
            }

            // Airplane - gold button, changes color when active
            AirplaneTile.Background = _airplaneMode ? new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x1A)) : new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));

            // Night Light - gold button
            NightLightTile.Background = _nightLightEnabled ? new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x1A)) : new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));

            // Focus - gold button
            FocusTile.Background = _focusEnabled ? new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x1A)) : new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));
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
            // BrightnessText removed from new design
            int brightness = (int)e.NewValue;
            // BrightnessText.Text = $"{brightness}%"; // Removed

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

            // Update vertical volume bar fill height
            if (VolumeFill != null)
            {
                // Calculate fill height based on volume (0-100)
                // Volume bar total height is approx 480px (520 - 40px margins)
                double maxHeight = 480;
                VolumeFill.Height = (volume / 100.0) * maxHeight;
            }
        }

        private void OnMuteClick(object sender, MouseButtonEventArgs e)
        {
            // MuteButton removed from new design
            e.Handled = true;
            _isMuted = !_isMuted;
            AudioService.IsMuted = _isMuted;
        }

        private void UpdateMuteButton()
        {
            // MuteButton removed from new design - method disabled
        }

        #endregion

        #region Output Device

        private void OnOutputDeviceClick(object sender, MouseButtonEventArgs e)
        {
            // DeviceDropdown removed from new design - method disabled
            e.Handled = true;
        }

        private void OnDeviceSelected(object sender, MouseButtonEventArgs e)
        {
            // DeviceDropdown removed from new design - method disabled
        }

        private void UpdateOutputDevice()
        {
            // OutputDeviceText removed from new design - method disabled
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
            // PHASE 0: _stateTimer removed
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

                // Stop and dispose metrics timer
                if (_metricsTimer != null)
                {
                    _metricsTimer.Stop();
                    _metricsTimer.Tick -= OnMetricsTimerTick;
                    _metricsTimer = null;
                }

                // Dispose performance counters
                _cpuCounter?.Dispose();
                _cpuCounter = null;
                _ramAvailableCounter?.Dispose();
                _ramAvailableCounter = null;

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
