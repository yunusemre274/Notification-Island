using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NI.Services
{
    /// <summary>
    /// Wi-Fi network information.
    /// </summary>
    public class WifiNetwork
    {
        public string SSID { get; set; } = "";
        public int SignalStrength { get; set; }
        public bool IsSecured { get; set; }
        public bool IsConnected { get; set; }

        public string SignalIcon => SignalStrength switch
        {
            >= 75 => "📶",
            >= 50 => "📶",
            >= 25 => "📶",
            _ => "📶"
        };

        public string SecurityIcon => IsSecured ? "🔒" : "";
    }

    /// <summary>
    /// Static Wi-Fi service using Native Wi-Fi API (wlanapi.dll).
    /// Provides network scanning, connection, and disconnection.
    /// </summary>
    public static class WifiService
    {
        private static IntPtr _clientHandle = IntPtr.Zero;
        private static Guid _interfaceGuid = Guid.Empty;
        private static bool _initialized = false;

        public static event EventHandler? StateChanged;

        public static bool IsAvailable { get; private set; }
        public static bool IsEnabled { get; private set; } = true;
        public static string? ConnectedNetwork { get; private set; }

        static WifiService()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_initialized) return;

            try
            {
                uint negotiatedVersion;
                int result = WlanOpenHandle(2, IntPtr.Zero, out negotiatedVersion, out _clientHandle);
                
                if (result != 0)
                {
                    IsAvailable = false;
                    _initialized = true;
                    return;
                }

                // Get the first Wi-Fi interface
                IntPtr interfaceList;
                result = WlanEnumInterfaces(_clientHandle, IntPtr.Zero, out interfaceList);
                
                if (result != 0)
                {
                    IsAvailable = false;
                    _initialized = true;
                    return;
                }

                var infoList = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(interfaceList);
                if (infoList.dwNumberOfItems > 0)
                {
                    var interfaceInfo = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(
                        IntPtr.Add(interfaceList, 8));
                    _interfaceGuid = interfaceInfo.InterfaceGuid;
                    IsAvailable = true;
                    IsEnabled = interfaceInfo.isState != WLAN_INTERFACE_STATE.wlan_interface_state_not_ready;
                    
                    // Get current connection
                    UpdateCurrentConnection();
                }

                WlanFreeMemory(interfaceList);
                _initialized = true;
            }
            catch
            {
                IsAvailable = false;
                _initialized = true;
            }
        }

        private static void UpdateCurrentConnection()
        {
            ConnectedNetwork = null;
            
            if (!IsAvailable || _clientHandle == IntPtr.Zero)
                return;

            try
            {
                IntPtr dataPtr;
                uint dataSize;
                int result = WlanQueryInterface(_clientHandle, ref _interfaceGuid,
                    WLAN_INTF_OPCODE.wlan_intf_opcode_current_connection, IntPtr.Zero, out dataSize, out dataPtr, IntPtr.Zero);

                if (result != 0)
                    return;

                var connectionAttr = Marshal.PtrToStructure<WLAN_CONNECTION_ATTRIBUTES>(dataPtr);
                string ssid = Encoding.UTF8.GetString(
                    connectionAttr.wlanAssociationAttributes.dot11Ssid.ucSSID,
                    0,
                    (int)connectionAttr.wlanAssociationAttributes.dot11Ssid.uSSIDLength);

                WlanFreeMemory(dataPtr);

                if (!string.IsNullOrWhiteSpace(ssid))
                {
                    ConnectedNetwork = ssid;
                }
            }
            catch { }
        }

        /// <summary>
        /// Gets available Wi-Fi networks (synchronous version for compatibility).
        /// </summary>
        public static List<WifiNetwork> GetAvailableNetworks()
        {
            return GetAvailableNetworksAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets available Wi-Fi networks asynchronously.
        /// </summary>
        public static async Task<List<WifiNetwork>> GetAvailableNetworksAsync()
        {
            return await Task.Run(() =>
            {
                var networks = new List<WifiNetwork>();

                if (!IsAvailable || _clientHandle == IntPtr.Zero || !IsEnabled)
                    return networks;

                try
                {
                    // Trigger a scan
                    WlanScan(_clientHandle, ref _interfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    System.Threading.Thread.Sleep(300);

                    IntPtr networkList;
                    int result = WlanGetAvailableNetworkList(_clientHandle, ref _interfaceGuid, 0, IntPtr.Zero, out networkList);
                    
                    if (result != 0)
                        return networks;

                    var list = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK_LIST>(networkList);
                    IntPtr networkPtr = IntPtr.Add(networkList, 8);

                    var seenSsids = new HashSet<string>();

                    for (int i = 0; i < list.dwNumberOfItems; i++)
                    {
                        var network = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(networkPtr);
                        
                        string ssid = Encoding.UTF8.GetString(network.dot11Ssid.ucSSID, 0, (int)network.dot11Ssid.uSSIDLength);
                        
                        if (!string.IsNullOrWhiteSpace(ssid) && !seenSsids.Contains(ssid))
                        {
                            seenSsids.Add(ssid);
                            bool isConnected = (network.dwFlags & 1) != 0;
                            
                            if (isConnected)
                            {
                                ConnectedNetwork = ssid;
                            }
                            
                            networks.Add(new WifiNetwork
                            {
                                SSID = ssid,
                                SignalStrength = (int)network.wlanSignalQuality,
                                IsSecured = network.bSecurityEnabled,
                                IsConnected = isConnected
                            });
                        }

                        networkPtr = IntPtr.Add(networkPtr, Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>());
                    }

                    WlanFreeMemory(networkList);
                    networks.Sort((a, b) => b.SignalStrength.CompareTo(a.SignalStrength));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Wi-Fi scan error: {ex.Message}");
                }

                return networks;
            });
        }

        /// <summary>
        /// Connects to a Wi-Fi network (synchronous for compatibility).
        /// </summary>
        public static void Connect(string ssid, string? password = null)
        {
            ConnectAsync(ssid, password).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Connects to a Wi-Fi network asynchronously.
        /// </summary>
        public static async Task<bool> ConnectAsync(string ssid, string? password = null)
        {
            if (!IsAvailable || _clientHandle == IntPtr.Zero)
                return false;

            var networks = await GetAvailableNetworksAsync();
            var network = networks.Find(n => n.SSID == ssid);
            if (network == null) return false;

            return await Task.Run(() =>
            {
                try
                {
                    string profileXml;
                    
                    if (network.IsSecured && !string.IsNullOrEmpty(password))
                    {
                        profileXml = $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{EscapeXml(ssid)}</name>
    <SSIDConfig>
        <SSID>
            <name>{EscapeXml(ssid)}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>auto</connectionMode>
    <MSM>
        <security>
            <authEncryption>
                <authentication>WPA2PSK</authentication>
                <encryption>AES</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{EscapeXml(password)}</keyMaterial>
            </sharedKey>
        </security>
    </MSM>
</WLANProfile>";
                    }
                    else if (!network.IsSecured)
                    {
                        profileXml = $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{EscapeXml(ssid)}</name>
    <SSIDConfig>
        <SSID>
            <name>{EscapeXml(ssid)}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>auto</connectionMode>
    <MSM>
        <security>
            <authEncryption>
                <authentication>open</authentication>
                <encryption>none</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
        </security>
    </MSM>
</WLANProfile>";
                    }
                    else
                    {
                        // Try connecting with saved profile
                        var connectionParams = new WLAN_CONNECTION_PARAMETERS
                        {
                            wlanConnectionMode = WLAN_CONNECTION_MODE.wlan_connection_mode_profile,
                            strProfile = ssid,
                            dot11BssType = DOT11_BSS_TYPE.dot11_BSS_type_any,
                            dwFlags = 0
                        };

                        int res = WlanConnect(_clientHandle, ref _interfaceGuid, ref connectionParams, IntPtr.Zero);
                        if (res == 0)
                        {
                            ConnectedNetwork = ssid;
                            StateChanged?.Invoke(null, EventArgs.Empty);
                            return true;
                        }
                        return false;
                    }

                    uint reasonCode;
                    int result = WlanSetProfile(_clientHandle, ref _interfaceGuid, 0, profileXml, null, true, IntPtr.Zero, out reasonCode);
                    
                    if (result != 0)
                        return false;

                    var connParams = new WLAN_CONNECTION_PARAMETERS
                    {
                        wlanConnectionMode = WLAN_CONNECTION_MODE.wlan_connection_mode_profile,
                        strProfile = ssid,
                        dot11BssType = DOT11_BSS_TYPE.dot11_BSS_type_any,
                        dwFlags = 0
                    };

                    result = WlanConnect(_clientHandle, ref _interfaceGuid, ref connParams, IntPtr.Zero);
                    
                    if (result == 0)
                    {
                        ConnectedNetwork = ssid;
                        StateChanged?.Invoke(null, EventArgs.Empty);
                        return true;
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Disconnects from the current Wi-Fi network.
        /// </summary>
        public static void Disconnect()
        {
            if (!IsAvailable || _clientHandle == IntPtr.Zero)
                return;

            try
            {
                WlanDisconnect(_clientHandle, ref _interfaceGuid, IntPtr.Zero);
                ConnectedNetwork = null;
                StateChanged?.Invoke(null, EventArgs.Empty);
            }
            catch { }
        }

        /// <summary>
        /// Toggles Wi-Fi on/off using REAL Windows Radio API.
        /// This controls the actual Wi-Fi hardware radio.
        /// </summary>
        public static void ToggleWifi()
        {
            ToggleWifiAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Toggles Wi-Fi on/off asynchronously using Windows Radio API.
        /// </summary>
        public static async System.Threading.Tasks.Task ToggleWifiAsync()
        {
            try
            {
                // Use RadioService for REAL hardware control
                bool success = await RadioService.ToggleWifiAsync();
                
                if (success)
                {
                    IsEnabled = RadioService.IsWifiOn;
                    
                    if (!IsEnabled)
                    {
                        Disconnect();
                        ConnectedNetwork = null;
                    }
                    else
                    {
                        // Re-initialize and scan for networks
                        UpdateCurrentConnection();
                    }
                    
                    StateChanged?.Invoke(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ToggleWifiAsync error: {ex.Message}");
                // Fallback to software toggle
                IsEnabled = !IsEnabled;
                if (!IsEnabled) Disconnect();
                StateChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Sets Wi-Fi radio state explicitly.
        /// </summary>
        public static async System.Threading.Tasks.Task SetWifiStateAsync(bool enabled)
        {
            try
            {
                bool success = await RadioService.SetWifiStateAsync(enabled);
                
                if (success)
                {
                    IsEnabled = enabled;
                    
                    if (!enabled)
                    {
                        Disconnect();
                        ConnectedNetwork = null;
                    }
                    else
                    {
                        UpdateCurrentConnection();
                    }
                    
                    StateChanged?.Invoke(null, EventArgs.Empty);
                }
            }
            catch
            {
                IsEnabled = enabled;
                if (!enabled) Disconnect();
                StateChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Refreshes the Wi-Fi state from hardware.
        /// </summary>
        public static async System.Threading.Tasks.Task RefreshStateAsync()
        {
            try
            {
                await RadioService.RefreshStatesAsync();
                IsEnabled = RadioService.IsWifiOn;
                
                if (IsEnabled)
                {
                    UpdateCurrentConnection();
                }
                else
                {
                    ConnectedNetwork = null;
                }
                
                StateChanged?.Invoke(null, EventArgs.Empty);
            }
            catch { }
        }

        private static string EscapeXml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        /// <summary>
        /// Cleanup resources.
        /// </summary>
        public static void Dispose()
        {
            if (_clientHandle != IntPtr.Zero)
            {
                WlanCloseHandle(_clientHandle, IntPtr.Zero);
                _clientHandle = IntPtr.Zero;
            }
        }

        #region P/Invoke

        [DllImport("wlanapi.dll")]
        private static extern int WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        private static extern int WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern int WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll")]
        private static extern int WlanGetAvailableNetworkList(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, IntPtr pReserved, out IntPtr ppAvailableNetworkList);

        [DllImport("wlanapi.dll")]
        private static extern int WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern int WlanConnect(IntPtr hClientHandle, ref Guid pInterfaceGuid, ref WLAN_CONNECTION_PARAMETERS pConnectionParameters, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern int WlanDisconnect(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern int WlanSetProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] string strProfileXml, [MarshalAs(UnmanagedType.LPWStr)] string? strAllUserProfileSecurity, bool bOverwrite, IntPtr pReserved, out uint pdwReasonCode);

        [DllImport("wlanapi.dll")]
        private static extern int WlanQueryInterface(IntPtr hClientHandle, ref Guid pInterfaceGuid, WLAN_INTF_OPCODE OpCode, IntPtr pReserved, out uint pdwDataSize, out IntPtr ppData, IntPtr pWlanOpcodeValueType);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_INTERFACE_INFO_LIST
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
            public WLAN_INTERFACE_STATE isState;
        }

        private enum WLAN_INTERFACE_STATE
        {
            wlan_interface_state_not_ready = 0,
            wlan_interface_state_connected = 1,
            wlan_interface_state_ad_hoc_network_formed = 2,
            wlan_interface_state_disconnecting = 3,
            wlan_interface_state_disconnected = 4,
            wlan_interface_state_associating = 5,
            wlan_interface_state_discovering = 6,
            wlan_interface_state_authenticating = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_AVAILABLE_NETWORK_LIST
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_AVAILABLE_NETWORK
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public DOT11_SSID dot11Ssid;
            public DOT11_BSS_TYPE dot11BssType;
            public uint uNumberOfBssids;
            public bool bNetworkConnectable;
            public uint wlanNotConnectableReason;
            public uint uNumberOfPhyTypes;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] dot11PhyTypes;
            public bool bMorePhyTypes;
            public uint wlanSignalQuality;
            public bool bSecurityEnabled;
            public uint dot11DefaultAuthAlgorithm;
            public uint dot11DefaultCipherAlgorithm;
            public uint dwFlags;
            public uint dwReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        private enum DOT11_BSS_TYPE
        {
            dot11_BSS_type_infrastructure = 1,
            dot11_BSS_type_independent = 2,
            dot11_BSS_type_any = 3
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_CONNECTION_PARAMETERS
        {
            public WLAN_CONNECTION_MODE wlanConnectionMode;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string strProfile;
            public IntPtr pDot11Ssid;
            public IntPtr pDesiredBssidList;
            public DOT11_BSS_TYPE dot11BssType;
            public uint dwFlags;
        }

        private enum WLAN_CONNECTION_MODE
        {
            wlan_connection_mode_profile = 0,
            wlan_connection_mode_temporary_profile = 1,
            wlan_connection_mode_discovery_secure = 2,
            wlan_connection_mode_discovery_unsecure = 3,
            wlan_connection_mode_auto = 4,
            wlan_connection_mode_invalid = 5
        }

        private enum WLAN_INTF_OPCODE
        {
            wlan_intf_opcode_current_connection = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_CONNECTION_ATTRIBUTES
        {
            public WLAN_INTERFACE_STATE isState;
            public WLAN_CONNECTION_MODE wlanConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
            public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID dot11Ssid;
            public DOT11_BSS_TYPE dot11BssType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] dot11Bssid;
            public uint dot11PhyType;
            public uint uDot11PhyIndex;
            public uint wlanSignalQuality;
            public uint ulRxRate;
            public uint ulTxRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_SECURITY_ATTRIBUTES
        {
            public bool bSecurityEnabled;
            public bool bOneXEnabled;
            public uint dot11AuthAlgorithm;
            public uint dot11CipherAlgorithm;
        }

        #endregion
    }
}
