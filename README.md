# NI – Dynamic Island Style Notification Bar

Ultra-lightweight Windows overlay mimicking Apple's Dynamic Island.

---

## Features

- **Thin Floating Bar** – 700×42px compact, expands on notification
- **Dynamic Island Behavior** – Compact ↔ Expanded smooth transitions
- **Desktop-Only** – Auto-hides when apps overlap, shows on desktop
- **Pet Widget** – Tiny animated pet, responds to clicks
- **Ultra-Low Resource** – Target: <0.3% CPU idle, <40MB RAM

---

## Prerequisites

### Install .NET 8 SDK

Download: https://dotnet.microsoft.com/download/dotnet/8.0

Verify:
```powershell
dotnet --version
```

---

## Project Structure

```
/App
  /Views
    DynamicIslandView.xaml/.cs    # Main island UI
  /ViewModels
    IslandVM.cs                   # Lightweight island state
    PetVM.cs                      # Pet animation logic
  /Services
    NotificationService.cs        # Notification handling
    DesktopVisibilityService.cs   # Win32 visibility detection
    AppIconResolver.cs            # Icon loading
  MainWindow.xaml/.cs             # Host window
  Program.cs                      # Entry point
/Assets
  /Pet                            # Pet sprites (28x28 PNG)
  /Icons                          # UI icons
```

---

## Build & Run

```powershell
cd "c:\Users\yunus\Desktop\Projects\NI"
dotnet build
dotnet run
```

---

## Assets Required

| Path | Size | Description |
|------|------|-------------|
| `Assets/Pet/idle_1.png` | 28x28 | Pet idle frame 1 |
| `Assets/Pet/idle_2.png` | 28x28 | Pet idle frame 2 |
| `Assets/Pet/wave_1.png` | 28x28 | Pet wave frame 1 |
| `Assets/Pet/wave_2.png` | 28x28 | Pet wave frame 2 |
| `Assets/Pet/jump_1.png` | 28x28 | Pet jump frame 1 |
| `Assets/Pet/jump_2.png` | 28x28 | Pet jump frame 2 |

---

## Interactions

| Action | Result |
|--------|--------|
| Click | Island pulses, pet waves |
| Double-click | Pet jumps |
| Right-click | Settings popup |

---

## Desktop Visibility

The island auto-hides when:
- A maximized window is active
- Any window overlaps the island area

Shows again when:
- User returns to desktop (Win+D)
- Foreground window moves away

---

## Performance Optimizations

- Clock updates every 30s (not every second)
- Pet animation at 2.5 FPS idle
- Visibility check every 500ms
- Frozen brushes used throughout
- Animations stop when hidden
- Event-driven updates only

---

## Autostart

Toggle via right-click Settings, or manually:

```powershell
# Enable
$exe = (Get-Process -Id $PID).Path
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "NI" -Value $exe

# Disable
Remove-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "NI"
```

---

## Build Release

### Self-Contained EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output: `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\NI.exe`

---

## System Notifications (Requires MSIX)

Full system notification capture requires:
1. MSIX packaging
2. `<uap3:Capability Name="userNotificationListener"/>` in manifest
3. `UserNotificationListener.RequestAccessAsync()`

Current version uses simulated notifications for demo.

---

## License

MIT
