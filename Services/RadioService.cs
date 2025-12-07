using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Radios;

namespace NI.Services
{
    /// <summary>
    /// Service for controlling system radios (Wi-Fi, Bluetooth) using Windows Radio Management API.
    /// This provides REAL hardware radio control, identical to Windows Quick Settings.
    /// </summary>
    public static class RadioService
    {
        private static Radio? _wifiRadio;
        private static Radio? _bluetoothRadio;
        private static bool _initialized = false;
        private static readonly object _lock = new();

        public static event EventHandler? WifiStateChanged;
        public static event EventHandler? BluetoothStateChanged;

        /// <summary>
        /// Gets the current Wi-Fi radio state.
        /// </summary>
        public static bool IsWifiOn => _wifiRadio?.State == RadioState.On;

        /// <summary>
        /// Gets the current Bluetooth radio state.
        /// </summary>
        public static bool IsBluetoothOn => _bluetoothRadio?.State == RadioState.On;

        /// <summary>
        /// Gets whether Wi-Fi radio is available on this device.
        /// </summary>
        public static bool IsWifiAvailable => _wifiRadio != null;

        /// <summary>
        /// Gets whether Bluetooth radio is available on this device.
        /// </summary>
        public static bool IsBluetoothAvailable => _bluetoothRadio != null;

        /// <summary>
        /// Initializes the radio service and discovers available radios.
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;
            }

            try
            {
                // Request access to radios
                var accessStatus = await Radio.RequestAccessAsync();
                if (accessStatus != RadioAccessStatus.Allowed)
                {
                    System.Diagnostics.Debug.WriteLine($"Radio access denied: {accessStatus}");
                    _initialized = true;
                    return;
                }

                // Get all radios
                var radios = await Radio.GetRadiosAsync();

                foreach (var radio in radios)
                {
                    switch (radio.Kind)
                    {
                        case RadioKind.WiFi:
                            _wifiRadio = radio;
                            _wifiRadio.StateChanged += (s, e) => WifiStateChanged?.Invoke(null, EventArgs.Empty);
                            break;

                        case RadioKind.Bluetooth:
                            _bluetoothRadio = radio;
                            _bluetoothRadio.StateChanged += (s, e) => BluetoothStateChanged?.Invoke(null, EventArgs.Empty);
                            break;
                    }
                }

                _initialized = true;
                System.Diagnostics.Debug.WriteLine($"RadioService initialized. WiFi: {IsWifiAvailable}, BT: {IsBluetoothAvailable}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RadioService init error: {ex.Message}");
                _initialized = true;
            }
        }

        /// <summary>
        /// Sets the Wi-Fi radio state (ON/OFF).
        /// This is REAL hardware control - Windows taskbar icon will update.
        /// </summary>
        public static async Task<bool> SetWifiStateAsync(bool enabled)
        {
            if (_wifiRadio == null)
            {
                await InitializeAsync();
                if (_wifiRadio == null) return false;
            }

            try
            {
                var targetState = enabled ? RadioState.On : RadioState.Off;
                var result = await _wifiRadio.SetStateAsync(targetState);
                
                if (result == RadioAccessStatus.Allowed)
                {
                    WifiStateChanged?.Invoke(null, EventArgs.Empty);
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine($"WiFi state change failed: {result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WiFi toggle error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Toggles the Wi-Fi radio state.
        /// </summary>
        public static async Task<bool> ToggleWifiAsync()
        {
            return await SetWifiStateAsync(!IsWifiOn);
        }

        /// <summary>
        /// Sets the Bluetooth radio state (ON/OFF).
        /// This is REAL hardware control - Windows taskbar icon will update.
        /// </summary>
        public static async Task<bool> SetBluetoothStateAsync(bool enabled)
        {
            if (_bluetoothRadio == null)
            {
                await InitializeAsync();
                if (_bluetoothRadio == null) return false;
            }

            try
            {
                var targetState = enabled ? RadioState.On : RadioState.Off;
                var result = await _bluetoothRadio.SetStateAsync(targetState);
                
                if (result == RadioAccessStatus.Allowed)
                {
                    BluetoothStateChanged?.Invoke(null, EventArgs.Empty);
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine($"Bluetooth state change failed: {result}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bluetooth toggle error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Toggles the Bluetooth radio state.
        /// </summary>
        public static async Task<bool> ToggleBluetoothAsync()
        {
            return await SetBluetoothStateAsync(!IsBluetoothOn);
        }

        /// <summary>
        /// Gets the current state of all radios.
        /// </summary>
        public static async Task<(bool wifiOn, bool btOn)> GetRadioStatesAsync()
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            return (IsWifiOn, IsBluetoothOn);
        }

        /// <summary>
        /// Refreshes the radio states from hardware.
        /// Call this to sync UI with actual hardware state.
        /// </summary>
        public static async Task RefreshStatesAsync()
        {
            if (!_initialized)
            {
                await InitializeAsync();
                return;
            }

            // Re-query radios to get fresh state
            try
            {
                var radios = await Radio.GetRadiosAsync();
                
                foreach (var radio in radios)
                {
                    switch (radio.Kind)
                    {
                        case RadioKind.WiFi:
                            if (_wifiRadio == null)
                            {
                                _wifiRadio = radio;
                                _wifiRadio.StateChanged += (s, e) => WifiStateChanged?.Invoke(null, EventArgs.Empty);
                            }
                            break;

                        case RadioKind.Bluetooth:
                            if (_bluetoothRadio == null)
                            {
                                _bluetoothRadio = radio;
                                _bluetoothRadio.StateChanged += (s, e) => BluetoothStateChanged?.Invoke(null, EventArgs.Empty);
                            }
                            break;
                    }
                }
            }
            catch { }
        }
    }
}
