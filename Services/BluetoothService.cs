using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace NI.Services
{
    /// <summary>
    /// Real Bluetooth service using Windows.Devices.Bluetooth APIs.
    /// Provides device discovery and connection management.
    /// </summary>
    public class BluetoothService : IDisposable
    {
        private DeviceWatcher? _watcher;
        private bool _disposed = false;
        private bool _isScanning = false;
        private readonly List<BluetoothDeviceInfo> _discoveredDevices = new();

        public event EventHandler? DevicesChanged;
        public event EventHandler? StateChanged;

        public bool IsEnabled { get; private set; } = true;
        public bool IsScanning => _isScanning;

        /// <summary>
        /// Gets paired Bluetooth devices.
        /// </summary>
        public async Task<List<BluetoothDeviceInfo>> GetPairedDevicesAsync()
        {
            var devices = new List<BluetoothDeviceInfo>();

            if (!IsEnabled)
                return devices;

            try
            {
                // Get paired Bluetooth Classic devices
                string classicSelector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                var classicDevices = await DeviceInformation.FindAllAsync(classicSelector);

                foreach (var device in classicDevices)
                {
                    try
                    {
                        var btDevice = await BluetoothDevice.FromIdAsync(device.Id);
                        if (btDevice != null)
                        {
                            devices.Add(new BluetoothDeviceInfo
                            {
                                Name = btDevice.Name ?? "Unknown Device",
                                Id = device.Id,
                                IsPaired = true,
                                IsConnected = btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected,
                                DeviceType = GetDeviceType(btDevice.ClassOfDevice)
                            });
                        }
                    }
                    catch { }
                }

                // Also get paired BLE devices
                string bleSelector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
                var bleDevices = await DeviceInformation.FindAllAsync(bleSelector);

                foreach (var device in bleDevices)
                {
                    // Avoid duplicates
                    if (devices.Any(d => d.Name == device.Name))
                        continue;

                    try
                    {
                        var bleDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                        if (bleDevice != null)
                        {
                            devices.Add(new BluetoothDeviceInfo
                            {
                                Name = bleDevice.Name ?? "Unknown Device",
                                Id = device.Id,
                                IsPaired = true,
                                IsConnected = bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected,
                                DeviceType = "BLE"
                            });
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bluetooth error: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Starts scanning for nearby Bluetooth devices.
        /// </summary>
        public void StartDiscovery()
        {
            if (_isScanning || !IsEnabled)
                return;

            try
            {
                _discoveredDevices.Clear();
                
                // Watch for all Bluetooth devices
                string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
                
                _watcher = DeviceInformation.CreateWatcher(
                    BluetoothDevice.GetDeviceSelectorFromPairingState(false),
                    requestedProperties,
                    DeviceInformationKind.AssociationEndpoint);

                _watcher.Added += Watcher_Added;
                _watcher.Updated += Watcher_Updated;
                _watcher.Removed += Watcher_Removed;
                _watcher.Stopped += Watcher_Stopped;

                _watcher.Start();
                _isScanning = true;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discovery error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops scanning for devices.
        /// </summary>
        public void StopDiscovery()
        {
            if (!_isScanning)
                return;

            try
            {
                _watcher?.Stop();
                _isScanning = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        /// <summary>
        /// Gets discovered (unpaired) devices from scanning.
        /// </summary>
        public List<BluetoothDeviceInfo> GetDiscoveredDevices()
        {
            lock (_discoveredDevices)
            {
                return _discoveredDevices.ToList();
            }
        }

        /// <summary>
        /// Attempts to connect to a Bluetooth device.
        /// </summary>
        public async Task<bool> ConnectAsync(BluetoothDeviceInfo device)
        {
            if (!IsEnabled)
                return false;

            try
            {
                // For classic Bluetooth devices
                var btDevice = await BluetoothDevice.FromIdAsync(device.Id);
                if (btDevice != null)
                {
                    // Request pairing if not paired
                    if (!device.IsPaired)
                    {
                        var pairingResult = await btDevice.DeviceInformation.Pairing.PairAsync();
                        if (pairingResult.Status == DevicePairingResultStatus.Paired ||
                            pairingResult.Status == DevicePairingResultStatus.AlreadyPaired)
                        {
                            device.IsPaired = true;
                            DevicesChanged?.Invoke(this, EventArgs.Empty);
                            return true;
                        }
                        return false;
                    }
                    
                    // Device is already paired - connection happens automatically for most devices
                    return true;
                }

                // Try as BLE device
                var bleDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                if (bleDevice != null)
                {
                    if (!device.IsPaired)
                    {
                        var pairingResult = await bleDevice.DeviceInformation.Pairing.PairAsync();
                        if (pairingResult.Status == DevicePairingResultStatus.Paired ||
                            pairingResult.Status == DevicePairingResultStatus.AlreadyPaired)
                        {
                            device.IsPaired = true;
                            DevicesChanged?.Invoke(this, EventArgs.Empty);
                            return true;
                        }
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connect error: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Attempts to disconnect a device (unpairs it).
        /// </summary>
        public async Task<bool> DisconnectAsync(BluetoothDeviceInfo device)
        {
            if (!IsEnabled)
                return false;

            try
            {
                // Try as classic device
                var btDevice = await BluetoothDevice.FromIdAsync(device.Id);
                if (btDevice != null && btDevice.DeviceInformation.Pairing.IsPaired)
                {
                    var result = await btDevice.DeviceInformation.Pairing.UnpairAsync();
                    if (result.Status == DeviceUnpairingResultStatus.Unpaired)
                    {
                        device.IsPaired = false;
                        device.IsConnected = false;
                        DevicesChanged?.Invoke(this, EventArgs.Empty);
                        return true;
                    }
                }

                // Try as BLE device
                var bleDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                if (bleDevice != null && bleDevice.DeviceInformation.Pairing.IsPaired)
                {
                    var result = await bleDevice.DeviceInformation.Pairing.UnpairAsync();
                    if (result.Status == DeviceUnpairingResultStatus.Unpaired)
                    {
                        device.IsPaired = false;
                        device.IsConnected = false;
                        DevicesChanged?.Invoke(this, EventArgs.Empty);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disconnect error: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Toggles Bluetooth enabled state using REAL Windows Radio API.
        /// This controls the actual Bluetooth hardware radio.
        /// </summary>
        public void Toggle()
        {
            ToggleAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Toggles Bluetooth asynchronously using Windows Radio API.
        /// </summary>
        public async Task ToggleAsync()
        {
            try
            {
                bool success = await RadioService.ToggleBluetoothAsync();
                
                if (success)
                {
                    IsEnabled = RadioService.IsBluetoothOn;
                    
                    if (!IsEnabled)
                    {
                        StopDiscovery();
                    }
                    
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ToggleAsync error: {ex.Message}");
                // Fallback to software toggle
                IsEnabled = !IsEnabled;
                if (!IsEnabled) StopDiscovery();
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Sets Bluetooth radio state explicitly.
        /// </summary>
        public async Task SetStateAsync(bool enabled)
        {
            try
            {
                bool success = await RadioService.SetBluetoothStateAsync(enabled);
                
                if (success)
                {
                    IsEnabled = enabled;
                    
                    if (!enabled)
                    {
                        StopDiscovery();
                    }
                    
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
                IsEnabled = enabled;
                if (!enabled) StopDiscovery();
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Refreshes the Bluetooth state from hardware.
        /// </summary>
        public async Task RefreshStateAsync()
        {
            try
            {
                await RadioService.RefreshStatesAsync();
                IsEnabled = RadioService.IsBluetoothOn;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        private void Watcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            if (string.IsNullOrEmpty(deviceInfo.Name))
                return;

            lock (_discoveredDevices)
            {
                if (!_discoveredDevices.Any(d => d.Id == deviceInfo.Id))
                {
                    _discoveredDevices.Add(new BluetoothDeviceInfo
                    {
                        Name = deviceInfo.Name,
                        Id = deviceInfo.Id,
                        IsPaired = deviceInfo.Pairing.IsPaired,
                        IsConnected = false,
                        DeviceType = "Unknown"
                    });
                }
            }
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Watcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            lock (_discoveredDevices)
            {
                var device = _discoveredDevices.FirstOrDefault(d => d.Id == deviceInfoUpdate.Id);
                if (device != null)
                {
                    // Update connection status if available
                    if (deviceInfoUpdate.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var isConnected))
                    {
                        device.IsConnected = isConnected is bool b && b;
                    }
                }
            }
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            lock (_discoveredDevices)
            {
                var device = _discoveredDevices.FirstOrDefault(d => d.Id == deviceInfoUpdate.Id);
                if (device != null)
                {
                    _discoveredDevices.Remove(device);
                }
            }
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Watcher_Stopped(DeviceWatcher sender, object args)
        {
            _isScanning = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private string GetDeviceType(BluetoothClassOfDevice classOfDevice)
        {
            return classOfDevice.MajorClass switch
            {
                BluetoothMajorClass.AudioVideo => "🎧",
                BluetoothMajorClass.Computer => "💻",
                BluetoothMajorClass.Phone => "📱",
                BluetoothMajorClass.Peripheral => "⌨️",
                _ => "🔵"
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            StopDiscovery();
            _watcher = null;
        }
    }

    /// <summary>
    /// Bluetooth device information.
    /// </summary>
    public class BluetoothDeviceInfo
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
        public bool IsPaired { get; set; }
        public bool IsConnected { get; set; }
        public string DeviceType { get; set; } = "🔵";
    }
}
