using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace NI.Services
{
    /// <summary>
    /// Audio output device information.
    /// </summary>
    public class AudioDevice
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "🔊";
        public bool IsDefault { get; set; }
        public bool IsBluetooth { get; set; }
        public bool IsHeadphone { get; set; }
    }

    /// <summary>
    /// Event args for headphone connection.
    /// </summary>
    public class HeadphoneConnectedEventArgs : EventArgs
    {
        public string DeviceName { get; set; } = "";
        public bool IsBluetooth { get; set; }
    }

    /// <summary>
    /// Real Audio Service using NAudio/Core Audio API.
    /// Controls system volume, mute, and output device selection.
    /// Detects Bluetooth headphone connections.
    /// </summary>
    public static class AudioService
    {
        private static MMDeviceEnumerator? _enumerator;
        private static MMDevice? _defaultDevice;
        private static string _currentDeviceId = "";
        private static string _lastDefaultDeviceId = "";

        public static event EventHandler? VolumeChanged;
        public static event EventHandler<HeadphoneConnectedEventArgs>? HeadphoneConnected;

        /// <summary>
        /// Initialize the audio service.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                _enumerator = new MMDeviceEnumerator();
                RefreshDefaultDevice();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audio init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh the default audio device reference.
        /// </summary>
        private static void RefreshDefaultDevice()
        {
            try
            {
                _defaultDevice = _enumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (_defaultDevice != null)
                {
                    _currentDeviceId = _defaultDevice.ID;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh device error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets or sets the master volume (0-100).
        /// </summary>
        public static int Volume
        {
            get
            {
                try
                {
                    if (_defaultDevice?.AudioEndpointVolume != null)
                    {
                        return (int)Math.Round(_defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Get volume error: {ex.Message}");
                }
                return 50;
            }
            set
            {
                try
                {
                    if (_defaultDevice?.AudioEndpointVolume != null)
                    {
                        float newVolume = Math.Clamp(value, 0, 100) / 100f;
                        _defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume;
                        VolumeChanged?.Invoke(null, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Set volume error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets or sets the mute state.
        /// </summary>
        public static bool IsMuted
        {
            get
            {
                try
                {
                    if (_defaultDevice?.AudioEndpointVolume != null)
                    {
                        return _defaultDevice.AudioEndpointVolume.Mute;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Get mute error: {ex.Message}");
                }
                return false;
            }
            set
            {
                try
                {
                    if (_defaultDevice?.AudioEndpointVolume != null)
                    {
                        _defaultDevice.AudioEndpointVolume.Mute = value;
                        VolumeChanged?.Invoke(null, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Set mute error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the current device ID.
        /// </summary>
        public static string CurrentDeviceId => _currentDeviceId;

        /// <summary>
        /// Gets the appropriate volume icon based on current state.
        /// </summary>
        public static string VolumeIcon
        {
            get
            {
                if (IsMuted) return "🔇";
                int vol = Volume;
                return vol switch
                {
                    0 => "🔇",
                    < 33 => "🔈",
                    < 66 => "🔉",
                    _ => "🔊"
                };
            }
        }

        /// <summary>
        /// Gets all available audio output devices.
        /// </summary>
        public static List<AudioDevice> GetOutputDevices()
        {
            var devices = new List<AudioDevice>();

            try
            {
                if (_enumerator == null)
                {
                    Initialize();
                }

                var mmDevices = _enumerator?.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                if (mmDevices != null)
                {
                    foreach (var device in mmDevices)
                    {
                        string icon = GetDeviceIcon(device.FriendlyName);
                        devices.Add(new AudioDevice
                        {
                            Id = device.ID,
                            Name = device.FriendlyName,
                            Icon = icon,
                            IsDefault = device.ID == _currentDeviceId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get devices error: {ex.Message}");
            }

            // Return at least a placeholder if no devices found
            if (devices.Count == 0)
            {
                devices.Add(new AudioDevice
                {
                    Id = "default",
                    Name = "Default Output",
                    Icon = "🔊",
                    IsDefault = true
                });
            }

            return devices;
        }

        /// <summary>
        /// Sets the default audio output device.
        /// Note: Windows doesn't have a public API for changing default device.
        /// This uses PolicyConfig COM interface (undocumented but widely used).
        /// </summary>
        public static void SetOutputDevice(string deviceId)
        {
            try
            {
                // Use PolicyConfig to set default device
                var policyConfig = new PolicyConfigClient();
                policyConfig.SetDefaultEndpoint(deviceId, Role.Multimedia);
                policyConfig.SetDefaultEndpoint(deviceId, Role.Communications);
                
                _currentDeviceId = deviceId;
                RefreshDefaultDevice();
                VolumeChanged?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Set device error: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles the mute state.
        /// </summary>
        public static void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        /// <summary>
        /// Gets an appropriate icon for the device based on its name.
        /// </summary>
        private static string GetDeviceIcon(string deviceName)
        {
            string name = deviceName.ToLowerInvariant();
            
            if (name.Contains("headphone") || name.Contains("earphone") || name.Contains("airpods") || name.Contains("buds"))
                return "🎧";
            if (name.Contains("bluetooth"))
                return "🔵";
            if (name.Contains("usb"))
                return "🔌";
            if (name.Contains("hdmi") || name.Contains("display"))
                return "🖥️";
            if (name.Contains("realtek") || name.Contains("speaker"))
                return "🔊";
            
            return "🔊";
        }

        /// <summary>
        /// Checks if device name indicates a Bluetooth device.
        /// </summary>
        private static bool IsBluetoothDevice(string deviceName)
        {
            string name = deviceName.ToLowerInvariant();
            return name.Contains("bluetooth") || 
                   name.Contains("airpods") || 
                   name.Contains("buds") ||
                   name.Contains("wh-") || // Sony WH-1000XM series
                   name.Contains("wf-") || // Sony WF earbuds
                   name.Contains("jabra") ||
                   name.Contains("bose") ||
                   name.Contains("beats");
        }

        /// <summary>
        /// Checks if device name indicates headphones/earbuds.
        /// </summary>
        private static bool IsHeadphoneDevice(string deviceName)
        {
            string name = deviceName.ToLowerInvariant();
            return name.Contains("headphone") || 
                   name.Contains("earphone") ||
                   name.Contains("headset") ||
                   name.Contains("airpods") || 
                   name.Contains("buds") ||
                   name.Contains("wh-") ||
                   name.Contains("wf-") ||
                   name.Contains("jabra") ||
                   name.Contains("bose") ||
                   name.Contains("beats");
        }

        /// <summary>
        /// Checks for default device change (called by timer).
        /// Detects when a Bluetooth headphone becomes the default device.
        /// </summary>
        public static void CheckDeviceChange()
        {
            try
            {
                RefreshDefaultDevice();
                
                if (_defaultDevice != null && _defaultDevice.ID != _lastDefaultDeviceId)
                {
                    string deviceName = _defaultDevice.FriendlyName;
                    bool isBluetooth = IsBluetoothDevice(deviceName);
                    bool isHeadphone = IsHeadphoneDevice(deviceName);

                    // Fire event if a Bluetooth/headphone device just became default
                    if ((isBluetooth || isHeadphone) && !string.IsNullOrEmpty(_lastDefaultDeviceId))
                    {
                        HeadphoneConnected?.Invoke(null, new HeadphoneConnectedEventArgs
                        {
                            DeviceName = deviceName,
                            IsBluetooth = isBluetooth
                        });
                    }

                    _lastDefaultDeviceId = _defaultDevice.ID;
                }
            }
            catch { }
        }

        /// <summary>
        /// Cleanup resources.
        /// </summary>
        public static void Dispose()
        {
            try
            {
                _defaultDevice?.Dispose();
                _enumerator?.Dispose();
            }
            catch { }
        }
    }

    #region PolicyConfig COM Interface (for changing default audio device)
    
    /// <summary>
    /// Helper class to change the default audio endpoint using the undocumented PolicyConfig COM interface.
    /// </summary>
    internal class PolicyConfigClient
    {
        private IPolicyConfig? _policyConfig;

        public PolicyConfigClient()
        {
            try
            {
                // Create the PolicyConfig COM object
                var type = Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"));
                if (type != null)
                {
                    var instance = Activator.CreateInstance(type);
                    _policyConfig = instance as IPolicyConfig;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PolicyConfig error: {ex.Message}");
            }
        }

        public void SetDefaultEndpoint(string deviceId, Role role)
        {
            try
            {
                _policyConfig?.SetDefaultEndpoint(deviceId, role);
            }
            catch { }
        }
    }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        void GetMixFormat(string deviceId, out IntPtr format);
        void GetDeviceFormat(string deviceId, bool @default, out IntPtr format);
        void ResetDeviceFormat(string deviceId);
        void SetDeviceFormat(string deviceId, IntPtr format, IntPtr mixFormat);
        void GetProcessingPeriod(string deviceId, bool @default, out long defaultPeriod, out long minimumPeriod);
        void SetProcessingPeriod(string deviceId, long period);
        void GetShareMode(string deviceId, out int mode);
        void SetShareMode(string deviceId, int mode);
        void GetPropertyValue(string deviceId, ref Guid key, out object value);
        void SetPropertyValue(string deviceId, ref Guid key, ref object value);
        void SetDefaultEndpoint(string deviceId, Role role);
        void SetEndpointVisibility(string deviceId, bool visible);
    }

    #endregion
}
