using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NI.Services
{
    /// <summary>
    /// Information about the currently active window.
    /// </summary>
    public class ActiveWindowInfo
    {
        public string AppName { get; set; } = "";
        public string WindowTitle { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string IconKey { get; set; } = "App"; // Key for icon lookup
        public bool IsDesktop { get; set; }
    }

    /// <summary>
    /// Service to detect the currently active/foreground window.
    /// Maps process names to friendly labels and icon keys.
    /// </summary>
    public class ActiveWindowService
    {
        private string _lastProcessName = "";
        private ActiveWindowInfo? _lastInfo;

        public event EventHandler<ActiveWindowInfo>? ActiveWindowChanged;

        /// <summary>
        /// Gets current active window info.
        /// Returns cached value if same process, fires event on change.
        /// </summary>
        public ActiveWindowInfo? CheckActiveWindow()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return _lastInfo;

                // Get window class
                var className = new StringBuilder(64);
                GetClassName(hwnd, className, 64);
                var cls = className.ToString();

                // Check for desktop
                if (cls == "Progman" || cls == "WorkerW")
                {
                    if (_lastProcessName != "Desktop")
                    {
                        _lastProcessName = "Desktop";
                        _lastInfo = new ActiveWindowInfo
                        {
                            AppName = "Masaüstü",
                            ProcessName = "Desktop",
                            IconKey = "Desktop",
                            IsDesktop = true
                        };
                        ActiveWindowChanged?.Invoke(this, _lastInfo);
                    }
                    return _lastInfo;
                }

                // Shell windows
                if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                {
                    return _lastInfo; // Keep previous, taskbar isn't an app
                }

                // Get process info
                GetWindowThreadProcessId(hwnd, out uint processId);
                if (processId == 0)
                    return _lastInfo;

                var process = Process.GetProcessById((int)processId);
                string processName = process.ProcessName.ToLowerInvariant();

                // No change
                if (processName == _lastProcessName)
                    return _lastInfo;

                _lastProcessName = processName;

                // Get window title
                var titleBuilder = new StringBuilder(256);
                GetWindowText(hwnd, titleBuilder, 256);
                string windowTitle = titleBuilder.ToString();

                // Map to friendly name and icon
                var info = MapProcessToInfo(processName, windowTitle, cls);
                _lastInfo = info;

                ActiveWindowChanged?.Invoke(this, info);
                return info;
            }
            catch
            {
                return _lastInfo;
            }
        }

        private ActiveWindowInfo MapProcessToInfo(string processName, string windowTitle, string className)
        {
            var info = new ActiveWindowInfo
            {
                ProcessName = processName,
                WindowTitle = windowTitle
            };

            // Browser mapping
            switch (processName)
            {
                case "opera":
                    info.AppName = "Opera";
                    info.IconKey = "Browser";
                    break;
                case "chrome":
                    info.AppName = "Chrome";
                    info.IconKey = "Browser";
                    break;
                case "msedge":
                    info.AppName = "Edge";
                    info.IconKey = "Browser";
                    break;
                case "firefox":
                    info.AppName = "Firefox";
                    info.IconKey = "Browser";
                    break;
                case "brave":
                    info.AppName = "Brave";
                    info.IconKey = "Browser";
                    break;

                // File Explorer
                case "explorer":
                    if (className == "CabinetWClass" || className == "ExploreWClass")
                    {
                        info.AppName = "Klasör";
                        info.IconKey = "Folder";
                    }
                    else
                    {
                        info.AppName = "Explorer";
                        info.IconKey = "Folder";
                    }
                    break;

                // Development
                case "devenv":
                    info.AppName = "Visual Studio";
                    info.IconKey = "Code";
                    break;
                case "code":
                    info.AppName = "VS Code";
                    info.IconKey = "Code";
                    break;

                // Media
                case "spotify":
                    info.AppName = "Spotify";
                    info.IconKey = "Music";
                    break;
                case "vlc":
                    info.AppName = "VLC";
                    info.IconKey = "Media";
                    break;

                // Communication
                case "discord":
                    info.AppName = "Discord";
                    info.IconKey = "Chat";
                    break;
                case "telegram":
                    info.AppName = "Telegram";
                    info.IconKey = "Chat";
                    break;
                case "whatsapp":
                    info.AppName = "WhatsApp";
                    info.IconKey = "Chat";
                    break;
                case "teams":
                    info.AppName = "Teams";
                    info.IconKey = "Chat";
                    break;

                // Gaming
                case "steam":
                    info.AppName = "Steam";
                    info.IconKey = "Game";
                    break;

                // Office
                case "winword":
                    info.AppName = "Word";
                    info.IconKey = "Document";
                    break;
                case "excel":
                    info.AppName = "Excel";
                    info.IconKey = "Document";
                    break;
                case "powerpnt":
                    info.AppName = "PowerPoint";
                    info.IconKey = "Document";
                    break;

                // System
                case "taskmgr":
                    info.AppName = "Görev Yöneticisi";
                    info.IconKey = "System";
                    break;
                case "mmc":
                    info.AppName = "Yönetim Konsolu";
                    info.IconKey = "System";
                    break;
                case "regedit":
                    info.AppName = "Kayıt Defteri";
                    info.IconKey = "System";
                    break;

                // Default: use process name capitalized
                default:
                    info.AppName = char.ToUpper(processName[0]) + processName.Substring(1);
                    info.IconKey = "App";
                    break;
            }

            return info;
        }

        #region Win32

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        #endregion
    }
}
