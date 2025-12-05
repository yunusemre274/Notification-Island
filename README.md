# NI – Dynamic Island for Windows

Persistent, hover-expanding notification overlay inspired by Apple's Dynamic Island.

---

## Features

- **Persistent Overlay** – Always visible on desktop, never auto-closes
- **Hover Expansion** – Compact → Expanded on mouse hover
- **Desktop-Only** – Auto-hides when apps overlap, shows on desktop
- **Smooth Animations** – GPU-accelerated scale/width transitions
- **Ultra-Low Resource** – Event-driven, no heavy polling

---

## UI Modes

### Compact (Idle)
- Width: 350px, Height: 36px
- Shows: Clock, status dot, "Ready" text

### Expanded (Hover/Notification)
- Width: 680px, Height: 50px
- Shows: Clock, app icon, notification title/text, status icons

---

## Interactions

| Action | Result |
|--------|--------|
| Hover | Smooth expansion |
| Mouse Leave | Returns to compact |
| Click | Pulse animation |
| Right-click | Settings popup |
| Notification | Expand → show → auto-compact |

---

## Build & Run

### Requirements
- Windows 10/11
- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

### Build
```powershell
cd "c:\Users\yunus\Desktop\Projects\NI"
dotnet build
```

### Run
```powershell
dotnet run
```

---

## Project Structure

```
/NI
  /Views
    IslandView.xaml/.cs         # Main island UI with hover logic
  /ViewModels  
    IslandVM.cs                 # Lightweight state management
  /Services
    NotificationService.cs      # Notification handling
    DesktopVisibilityService.cs # Win32 desktop detection
  MainWindow.xaml/.cs           # Host window
  Program.cs                    # Entry point
```

---

## Performance

- Clock updates: Every 30 seconds
- Visibility check: Every 500ms
- Animations: GPU-accelerated transforms
- No continuous loops when idle

---

## Build Release

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

---

## License

MIT
