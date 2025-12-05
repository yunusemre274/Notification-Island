# 🏝️ Notification Island for Windows

A sleek, macOS/iOS-inspired **Dynamic Island** desktop overlay for Windows. Built with C#, .NET 8, and WPF — bringing Apple's innovative notification experience to your Windows desktop.

![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?style=for-the-badge&logo=windows)
![Framework](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Desktop-68217A?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

---

## ✨ Features

### 🎯 Dynamic Island UI
| Feature | Description |
|---------|-------------|
| **Compact Mode** | Minimal pill-shaped bar (350×36px) showing clock and status |
| **Expanded Mode** | Full content display (680×50px) with rich information |
| **Smooth Animations** | GPU-accelerated 200ms transitions with easing |
| **Hover Expansion** | Automatically reveals full content on mouse hover |
| **Pulse Feedback** | Subtle scale animation on click interaction |

### 🎵 Spotify Integration
- **Live Now Playing**: Real-time song detection from Spotify window title
- **Artist & Track Display**: Shows "Artist - Song" in compact, full details in expanded
- **Glowing Logo**: Spotify green (#1DB954) glow effect when music is playing
- **Progress Bar**: Visual track progress indicator
- **Click to Open**: Click the center to launch Spotify

### 🎧 Bluetooth Headphone Detection
- **Connection Banner**: Animated notification when headphones connect
- **Device Name Display**: Shows the connected device name (AirPods, Sony WH-1000XM, etc.)
- **Cyan Glow Effect**: Beautiful glow animation on the headphone icon
- **Auto-Dismiss**: Banner disappears after 4 seconds

### 🖥️ Active Window Tracking
- **Smart App Detection**: Identifies the current foreground application
- **App Icons**: Custom icons for browsers, code editors, games, chat apps, etc.
- **Friendly Names**: Maps process names to readable app names
- **Replaces Idle**: Shows active app instead of generic idle messages

### 📅 Smart Event Cards
- **National Days (TR)**: Turkish national holidays and observances
- **Global Days**: International awareness days and celebrations
- **Birthday Reminders**: Configurable personal reminders
- **Priority System**: Smart events yield to higher-priority content

### 🎛️ iOS-Style Control Center
- **WiFi Panel**: Network status and quick settings
- **Sound Panel**: Volume control with NAudio integration
- **Bluetooth Panel**: Device connection status
- **Blur Background**: Frosted glass aesthetic

### 🖥️ Desktop Integration
- **Always on Top**: Persistent overlay that stays visible
- **Fullscreen Detection**: Auto-hides during games and videos
- **Screen Recording Friendly**: Software rendering for capture compatibility
- **Non-Intrusive**: Doesn't steal focus or appear in Alt+Tab

---

## 📸 Preview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│   COMPACT MODE (Idle/Active Window)                                     │
│   ┌─────────────────────────────────────────┐                           │
│   │  14:32 ●  │  🌐 Opera Browser  │  ●    │                           │
│   └─────────────────────────────────────────┘                           │
│                                                                         │
│   COMPACT MODE (Spotify Playing)                                        │
│   ┌─────────────────────────────────────────┐                           │
│   │  14:32 ●  │  🎵 Artist - Song  │  ●    │                           │
│   └─────────────────────────────────────────┘                           │
│                                                                         │
│   COMPACT MODE (Headphone Connected)                                    │
│   ┌─────────────────────────────────────────┐                           │
│   │  14:32 ●  │  🎧 AirPods Pro    │  ●    │                           │
│   └─────────────────────────────────────────┘                           │
│                                                                         │
│   EXPANDED MODE (Spotify)                                               │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  14:32 ●  │  🎵  Daft Punk                          │  🔊  📶  │   │
│   │           │      Get Lucky                          │          │   │
│   │           │      ━━━━━━━━━━━━━●━━━━━━━━━            │          │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🚀 Quick Start

### Prerequisites

- **Windows 10/11** (version 1903 or later, build 19041+)
- **.NET 8 SDK**: [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Spotify** (optional): For music integration

Verify installation:
```powershell
dotnet --version
# Should output: 8.x.x
```

### Build & Run

```powershell
# Clone the repository
git clone https://github.com/yunusemre274/Notification-Island.git
cd Notification-Island

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

The island will appear at the **top-center** of your primary screen, ~20px from the top edge.

---

## 🎮 Interactions

| Action | Behavior |
|--------|----------|
| **Hover** | Smoothly expands to show full content |
| **Mouse Leave** | Returns to compact mode after 150ms delay |
| **Left Click** | Pulse animation feedback |
| **Right Click** | Opens settings popup |
| **Click Center (Spotify)** | Opens Spotify application |
| **Click WiFi/Sound Icons** | Opens Control Center panel |

---

## ⚙️ Settings

Right-click the island to access settings:

| Option | Description |
|--------|-------------|
| **Start with Windows** | Auto-launch on system boot |
| **Sound Effects** | Enable/disable UI sounds |
| **National Days (TR)** | Show Turkish national holidays |
| **Global Days** | Show international awareness days |
| **Idle Messages** | Show random tips when idle |
| **Exit App** | Close the application |

---

## 📁 Project Structure

```
NI/
├── 📄 Program.cs                          # Application entry point
├── 📄 MainWindow.xaml/.cs                 # Host window (transparent, borderless)
├── 📄 NI.csproj                           # Project configuration
├── 📄 app.manifest                        # DPI awareness, Windows compatibility
│
├── 📂 Models/
│   ├── 📄 AppSettings.cs                  # Persisted user settings
│   ├── 📄 SmartEvent.cs                   # Smart card event model
│   └── 📄 SpotifyTrack.cs                 # Spotify track information
│
├── 📂 Views/
│   ├── 📄 IslandView.xaml/.cs             # Main Dynamic Island UI
│   └── 📂 Controls/
│       ├── 📄 ControlCenterPanel.xaml/.cs # iOS-style control center
│       ├── 📄 SoundPanel.xaml/.cs         # Volume control panel
│       └── 📄 WifiPanel.xaml/.cs          # WiFi settings panel
│
├── 📂 ViewModels/
│   └── 📄 IslandVM.cs                     # Main ViewModel with state management
│
├── 📂 Services/
│   ├── 📄 ActiveWindowService.cs          # Foreground window detection
│   ├── 📄 AudioService.cs                 # NAudio volume control & device detection
│   ├── 📄 BluetoothService.cs             # Bluetooth device management
│   ├── 📄 DesktopVisibilityService.cs     # Fullscreen app detection
│   ├── 📄 NotificationService.cs          # System notification hooks
│   ├── 📄 SmartEventService.cs            # Calendar events & special days
│   ├── 📄 SpotifyService.cs               # Spotify window title parsing
│   └── 📄 WifiService.cs                  # Network status monitoring
│
└── 📂 Assets/
    └── 📂 Icons/                          # Application icons
```

---

## 🏗️ Architecture

### Design Principles

| Principle | Implementation |
|-----------|----------------|
| **KISS** | Simple, readable code without over-engineering |
| **Event-Driven** | Updates only when state changes occur |
| **GPU-First** | Animations use transforms, not layout recalculations |
| **Priority System** | Content display follows strict priority order |

### Priority Order

```
1. System Notifications (highest)
2. Headphone Connection Banner
3. Spotify Now Playing
4. Active Window Card
5. Smart Events (holidays, reminders)
6. Idle Messages (lowest)
```

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           MainWindow                                    │
│  (Transparent, Borderless, Topmost, Desktop Integration)               │
├─────────────────────────────────────────────────────────────────────────┤
│                           IslandView                                    │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────┐  ┌────────────────┐  │
│  │ CompactMode │  │ ExpandedMode │  │  Settings │  │ ControlCenter  │  │
│  └─────────────┘  └──────────────┘  └───────────┘  └────────────────┘  │
├─────────────────────────────────────────────────────────────────────────┤
│                           IslandVM                                      │
│  (State: Clock, Spotify, Headphones, ActiveWindow, SmartEvents)        │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐   │
│  │SpotifyService│ │ AudioService │ │ActiveWindow  │ │SmartEvent    │   │
│  │(Title Parse) │ │(NAudio)      │ │Service       │ │Service       │   │
│  └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘   │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐                    │
│  │DesktopVisib. │ │BluetoothSvc  │ │  WifiService │                    │
│  │(Fullscreen)  │ │(WinRT)       │ │(WinRT)       │                    │
│  └──────────────┘ └──────────────┘ └──────────────┘                    │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## ⚡ Performance

### Optimization Strategies

| Area | Technique | Impact |
|------|-----------|--------|
| **Timer Consolidation** | Single 1-second timer for all checks | Reduced CPU wake-ups |
| **Tick Modulo** | Clock every 30s, Spotify every 2s, etc. | Minimized processing |
| **Frozen Brushes** | Static `SolidColorBrush` resources | No GC pressure |
| **GPU Animations** | `ScaleTransform`, `DoubleAnimation` | Smooth 60fps |
| **Event-Driven** | No continuous loops, only state changes | Near-zero idle CPU |
| **Software Rendering** | `RenderMode.SoftwareOnly` for screen recording | Capture compatibility |

### Resource Targets

| Metric | Target | Actual |
|--------|--------|--------|
| **CPU (Idle)** | < 0.5% | ~0.1-0.3% |
| **RAM** | < 50 MB | ~35-45 MB |
| **GPU** | Minimal | Only during animations |

---

## 📦 Building for Release

### Self-Contained Executable (Recommended)

Creates a single .exe with embedded .NET runtime (~150MB):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\NI.exe`

### Framework-Dependent (Smaller, ~5MB)

Requires .NET 8 runtime installed on target machine:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

### Trimmed Build (Experimental)

Reduces size by removing unused code:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true
```

---

## 🔧 Configuration

### Settings File Location

Settings are stored in:
```
%APPDATA%\NI\settings.json
```

### Manual Autostart

```powershell
# Enable autostart
$exePath = "C:\Path\To\NI.exe"
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "NotificationIsland" -Value $exePath

# Disable autostart
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "NotificationIsland"
```

### DPI Scaling

The app supports Per-Monitor V2 DPI awareness via `app.manifest` for crisp rendering on high-DPI displays.

---

## 🎵 Spotify Integration Details

### How It Works

1. **Window Detection**: Finds Spotify's main window by class name
2. **Title Parsing**: Extracts "Artist - Song" from window title
3. **State Detection**: Distinguishes between playing, paused, and closed states
4. **Progress Estimation**: Simulates progress bar based on elapsed time

### Supported Formats

| Window Title | Parsed As |
|-------------|-----------|
| `Daft Punk - Get Lucky` | Artist: Daft Punk, Song: Get Lucky |
| `Spotify Premium` | Spotify is open but paused |
| `Spotify` | Spotify is open but no track |

### Limitations

- Requires Spotify desktop app (not web player)
- Title parsing may fail on unusual track names with " - " in them
- Progress bar is estimated, not actual playback position

---

## 🎧 Headphone Detection Details

### Supported Devices

The app detects Bluetooth and wired headphones through:

| Brand | Models Detected |
|-------|-----------------|
| **Apple** | AirPods, AirPods Pro, AirPods Max |
| **Sony** | WH-1000XM series, WF series |
| **Bose** | QuietComfort, SoundLink |
| **Beats** | Solo, Studio, Powerbeats |
| **Jabra** | Elite series |
| **Generic** | Any Bluetooth audio device |

### Detection Method

1. **NAudio Core Audio API**: Monitors default audio endpoint changes
2. **Device Name Parsing**: Identifies headphone-type devices
3. **Connection Events**: Fires when a new headphone device becomes default

---

## 🐛 Troubleshooting

### Island Not Visible

| Issue | Solution |
|-------|----------|
| Hidden by fullscreen app | Exit the fullscreen app or press `Win + D` |
| Behind other windows | Check if topmost is disabled |
| Off-screen | Reset window position in settings |

### Spotify Not Detected

| Issue | Solution |
|-------|----------|
| Using web player | Switch to desktop app |
| Spotify minimized to tray | Restore Spotify window |
| Title format changed | Report issue on GitHub |

### High CPU Usage

| Issue | Solution |
|-------|----------|
| Timer running too fast | This shouldn't happen - report as bug |
| Service stuck | Restart the application |
| Memory leak | Check for updates, restart app |

### Build Errors

```powershell
# Clean build artifacts
dotnet clean

# Restore packages
dotnet restore

# Rebuild
dotnet build
```

---

## 🗺️ Roadmap

### Planned Features

- [ ] **Notification Listener**: Real Windows toast notification capture
- [ ] **Theme Support**: Light, dark, and accent color themes
- [ ] **Multi-Monitor**: Position island on any connected display
- [ ] **Media Controls**: Play/pause/skip buttons for Spotify
- [ ] **Weather Widget**: Current weather in expanded view
- [ ] **Calendar Integration**: Upcoming events from Outlook/Google
- [ ] **Custom Widgets**: Plugin system for user-created content
- [ ] **Position Lock**: Drag to reposition and save location

### Known Issues

- [ ] Progress bar is estimated, not synced with Spotify
- [ ] Some Bluetooth devices may not be detected
- [ ] Screen recording requires software rendering mode

---

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/amazing-feature`
3. **Commit** your changes: `git commit -m 'Add amazing feature'`
4. **Push** to branch: `git push origin feature/amazing-feature`
5. **Open** a Pull Request

### Code Style

- Use C# 12 features where appropriate
- Follow Microsoft naming conventions
- Keep methods small and focused
- Add XML documentation for public APIs

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- Inspired by **Apple's Dynamic Island** on iPhone 14 Pro/15 Pro
- Built with [.NET 8](https://dotnet.microsoft.com/) and [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- Audio powered by [NAudio](https://github.com/naudio/NAudio)
- Icons from [Material Design Icons](https://materialdesignicons.com/)

---

## 📊 Tech Stack

| Component | Technology |
|-----------|------------|
| **Runtime** | .NET 8.0 |
| **UI Framework** | WPF (Windows Presentation Foundation) |
| **Audio API** | NAudio 2.2.1 + Windows Core Audio |
| **Bluetooth** | Windows.Devices.Bluetooth (WinRT) |
| **WiFi** | Windows.Devices.WiFi (WinRT) |
| **Interop** | Win32 API (User32, Shell32) |

---

<p align="center">
  <img src="https://img.shields.io/github/stars/yunusemre274/Notification-Island?style=social" alt="Stars">
  <img src="https://img.shields.io/github/forks/yunusemre274/Notification-Island?style=social" alt="Forks">
  <img src="https://img.shields.io/github/watchers/yunusemre274/Notification-Island?style=social" alt="Watchers">
</p>

<p align="center">
  Made with ❤️ by <a href="https://github.com/yunusemre274">yunusemre274</a>
</p>

<p align="center">
  <sub>If you find this project useful, please consider giving it a ⭐!</sub>
</p>
