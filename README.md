# NI – Floating Notification Center + Pet Widget

A simple WPF .NET 8 app that shows a floating notification bar with optional animated pet.

---

## Features

- **Floating Notification Bar** – 900×70px, rounded corners, translucent black background.
- **Clock & Status Dot** – live clock on left, green status dot.
- **Notification Queue** – incoming notifications queue up and auto-hide after 3 seconds.
- **Pet Widget** – simple sprite animation, responds to click/feed/jump (extendable).
- **Topmost Overlay** – stays on top, does not steal focus.

---

## Prerequisites

### Install .NET 8 SDK

Download and install from: https://dotnet.microsoft.com/download/dotnet/8.0

After installation, verify:

```powershell
dotnet --version
# Should show 8.x.x
```

---

## Project Structure

```
/App
  /Views
    NotificationView.xaml / .cs
    PetView.xaml / .cs
  /ViewModels
    NotificationViewModel.cs
    PetViewModel.cs
  /Services
    NotificationListener.cs
    AppIconResolver.cs
    PetStateMachine.cs
  Program.cs
/Assets
  /Icons
    sound.png
    wifi.png
  /PetSprites
    idle_1.png .. idle_4.png
MainWindow.xaml / .cs
NI.csproj
README.md
```

---

## Build & Run

### Requirements

- Windows 10/11 (version 1903+)
- .NET 8 SDK
- Visual Studio 2022 (optional) or VS Code + C# Dev Kit

### Build

```powershell
dotnet build
```

### Run

```powershell
dotnet run
```

The window will appear at the top-center of your primary monitor.

---

## Assets (Required)

Place your own PNG files in the `Assets` folder (or download free icons):

| Path                        | Description              |
|-----------------------------|--------------------------|
| `Assets/Icons/sound.png`    | Speaker/volume icon      |
| `Assets/Icons/wifi.png`     | WiFi icon                |
| `Assets/PetSprites/idle_1.png` – `idle_4.png` | Pet animation frames |

Without these files the app will run but show blank images.

---

## Autostart on Boot

Toggle in code or add manually:

```powershell
# Enable
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "NI" -Value "$(Resolve-Path .\bin\Release\net8.0-windows\NI.exe)"

# Disable
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "NI"
```

---

## System Notification Listening – Limitations

**Important:** Capturing *all* Windows toast notifications from any app requires:

1. The app to be **packaged (MSIX)** and declare the `userNotificationListener` capability.
2. Or use a system-level hook which requires elevated permissions and is fragile.

This sample provides a **simulated notification** for demo purposes. To enable real listening:

1. Package the app via MSIX.
2. Add `<uap3:Capability Name="userNotificationListener" />` to Package.appxmanifest.
3. Request `Windows.UI.Notifications.Management.UserNotificationListener.RequestAccessAsync()`.

---

## Creating an Installer

### Option 1 – Self-Contained EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output: `bin\Release\net8.0-windows\win-x64\publish\NI.exe`

### Option 2 – MSIX Package (for store or sideload)

Use Visual Studio → Add → Windows Application Packaging Project, then publish.

---

## Optional Features (Toggle in Code)

| Feature           | Location                 |
|-------------------|--------------------------|
| Dark/Light Mode   | Change Border Background |
| Blur Effect       | Set `BlurEffectResource` |
| Pet Size          | Adjust Width/Height      |
| Disable Pet       | Remove PetView from Grid |
| Notification Sound| Add `SoundPlayer.Play()` |

---

## License

MIT – do whatever you want.
