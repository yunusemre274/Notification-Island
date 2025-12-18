# Windows Dynamic Island - Architecture Diagram

## System Architecture Overview

```
┌───────────────────────────────────────────────────────────────────────┐
│                        MainWindow (WPF Container)                     │
│                     [Always-on-top, Transparent]                      │
└────────────────────────────────┬──────────────────────────────────────┘
                                 │
┌────────────────────────────────▼──────────────────────────────────────┐
│                          IslandView.xaml                              │
│                    [Main Dynamic Island UI Component]                 │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  COMPACT STATE (350x36)                                      │   │
│  │  ┌────────┬──────────────────────┬────────────────┐         │   │
│  │  │ Icon   │  Primary Text        │  Time / Status │         │   │
│  │  └────────┴──────────────────────┴────────────────┘         │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  EXPANDED STATE (680x50+)                                    │   │
│  │  ┌────────────────────────────────────────────────────────┐ │   │
│  │  │  CONTENT LAYERS (Priority-based visibility)            │ │   │
│  │  │  ┌──────────────────────────────────────────────────┐  │ │   │
│  │  │  │ 1. System Notification (Highest)                 │  │ │   │
│  │  │  ├──────────────────────────────────────────────────┤  │ │   │
│  │  │  │ 2. Headphone Connection Banner                   │  │ │   │
│  │  │  ├──────────────────────────────────────────────────┤  │ │   │
│  │  │  │ 3. Spotify Now Playing + Media Controls (NEW)    │  │ │   │
│  │  │  ├──────────────────────────────────────────────────┤  │ │   │
│  │  │  │ 4. System Performance Panel (NEW)                │  │ │   │
│  │  │  ├──────────────────────────────────────────────────┤  │ │   │
│  │  │  │ 5. AI Assistant Interface (NEW)                  │  │ │   │
│  │  │  ├──────────────────────────────────────────────────┤  │ │   │
│  │  │  │ 6. Active Window Info                            │  │ │   │
│  │  │  ├──────────────────────────────────────────────────┤  │ │   │
│  │  │  │ 7. Smart Events (Birthday, Holidays)             │  │ │   │
│  │  │  └──────────────────────────────────────────────────┘  │ │   │
│  │  └────────────────────────────────────────────────────────┘ │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  CONTROL CENTER PANEL (Appears on click)                     │   │
│  │  ┌───────────┬───────────┬───────────┬───────────┐          │   │
│  │  │ Volume    │ WiFi      │ Bluetooth │ Brightness│          │   │
│  │  │ Slider    │ Networks  │ Devices   │ Slider    │          │   │
│  │  └───────────┴───────────┴───────────┴───────────┘          │   │
│  └──────────────────────────────────────────────────────────────┘   │
└────────────────────────────────┬──────────────────────────────────────┘
                                 │
                                 │ Data Binding
                                 │
┌────────────────────────────────▼──────────────────────────────────────┐
│                        IslandVM (ViewModel)                           │
│                   [MVVM Pattern - INotifyPropertyChanged]             │
│                                                                        │
│  Properties:                                                          │
│  • CurrentContent (Spotify/System/AI/Window)                         │
│  • ShowMediaControls (bool)                                          │
│  • ShowSystemInfo (bool)                                             │
│  • ShowAiAssistant (bool)                                            │
│  • CpuUsage, RamUsage, GpuUsage                                      │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Consolidated Timer (3-second interval)                       │   │
│  │  ┌────────────────────────────────────────────────────────┐  │   │
│  │  │ Every 3s:  Check Spotify, Audio Devices                │  │   │
│  │  │ Every 10s: Update Clock                                │  │   │
│  │  │ Every 60s: Check Smart Events                          │  │   │
│  │  └────────────────────────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────────────┘   │
└─────┬──────────┬──────────┬──────────┬──────────────────────────────┘
      │          │          │          │
      │          │          │          │
┌─────▼───┐ ┌───▼─────┐ ┌──▼──────┐ ┌▼──────────┐
│ Existing│ │   NEW   │ │   NEW   │ │    NEW    │
│ Services│ │ Media   │ │ System  │ │ AI Assist │
└─────┬───┘ │ Session │ │ Monitor │ │  Service  │
      │     └────┬────┘ └────┬────┘ └─────┬─────┘
      │          │           │            │
┌─────▼──────────▼───────────▼────────────▼─────────────────────────┐
│                      SERVICE LAYER                                 │
│                                                                     │
│  EXISTING SERVICES:                                                │
│  ┌──────────────────┬──────────────────┬───────────────────────┐ │
│  │ SpotifyService   │ AudioService     │ WifiService           │ │
│  │ (Track Info)     │ (Volume, Devices)│ (Networks, Connect)   │ │
│  └──────────────────┴──────────────────┴───────────────────────┘ │
│  ┌──────────────────┬──────────────────┬───────────────────────┐ │
│  │ BluetoothService │ BrightnessService│ RadioService          │ │
│  │ (Pairing, Conn.) │ (Display Control)│ (WiFi/BT Toggle)      │ │
│  └──────────────────┴──────────────────┴───────────────────────┘ │
│  ┌──────────────────┬──────────────────┬───────────────────────┐ │
│  │ ActiveWindow     │ SmartEvent       │ Notification          │ │
│  │ Service          │ Service          │ Service               │ │
│  └──────────────────┴──────────────────┴───────────────────────┘ │
│                                                                     │
│  NEW SERVICES:                                                     │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ MediaSessionService                                        │  │
│  │ • Previous/Play/Pause/Next track control                   │  │
│  │ • Playback state monitoring                                │  │
│  │ • Timeline/progress tracking                               │  │
│  │ • UWP GlobalSystemMediaTransportControls API               │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ SystemMonitorService                                       │  │
│  │ • CPU usage (PerformanceCounter)                           │  │
│  │ • RAM usage (PerformanceCounter)                           │  │
│  │ • GPU usage (WMI - optional)                               │  │
│  │ • Background thread data collection (1s interval)          │  │
│  │ • Thread-safe property access                              │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ AiAssistantService                                         │  │
│  │ ┌────────────────────────────────────────────────────────┐ │  │
│  │ │ OllamaClient                                           │ │  │
│  │ │ • HTTP client for local Ollama API                    │ │  │
│  │ │ • Model: llama2 (configurable)                        │ │  │
│  │ │ • Endpoint: http://localhost:11434                    │ │  │
│  │ └────────────────────────────────────────────────────────┘ │  │
│  │ ┌────────────────────────────────────────────────────────┐ │  │
│  │ │ CommandParser                                          │ │  │
│  │ │ • Natural language → Structured command                │ │  │
│  │ │ • JSON response parsing                                │ │  │
│  │ │ • Intent classification                                │ │  │
│  │ └────────────────────────────────────────────────────────┘ │  │
│  │ ┌────────────────────────────────────────────────────────┐ │  │
│  │ │ CommandExecutor                                        │ │  │
│  │ │ • Sandboxed file operations                            │ │  │
│  │ │ • Folder search                                        │ │  │
│  │ │ • Path validation (user directory only)                │ │  │
│  │ └────────────────────────────────────────────────────────┘ │  │
│  └────────────────────────────────────────────────────────────┘  │
└─────┬──────────────────┬──────────────────┬───────────────────────┘
      │                  │                  │
┌─────▼──────┐  ┌────────▼────────┐  ┌──────▼──────────────────────┐
│  Windows   │  │  Windows        │  │  Ollama Local API           │
│  Media     │  │  Performance    │  │  (http://localhost:11434)   │
│  Session   │  │  Counters       │  │                             │
│  Manager   │  │  WMI            │  │  • LLaMA 2 Model            │
│  (UWP API) │  │                 │  │  • Fully Offline            │
└────────────┘  └─────────────────┘  └─────────────────────────────┘
```

---

## Data Flow Diagrams

### 1. Spotify Media Control Flow

```
User Click (Previous/Play/Pause/Next)
        ↓
IslandView.xaml.cs Event Handler
        ↓
MediaSessionService.PlayAsync() / PauseAsync() / etc.
        ↓
GlobalSystemMediaTransportControlsSession API Call
        ↓
Spotify Application receives command
        ↓
Playback state changes
        ↓
MediaSessionService.PlaybackInfoChanged event fires
        ↓
IslandVM.OnPlaybackStateChanged()
        ↓
UI updates (Play/Pause icon toggle)
```

### 2. System Monitor Flow

```
SystemMonitorService.Start()
        ↓
Background Timer (1 second interval)
        ↓
PerformanceCounter.NextValue() for CPU/RAM
        ↓
(Optional) WMI Query for GPU
        ↓
Thread-safe property update
        ↓
MetricsUpdated event fires
        ↓
IslandView.Dispatcher.Invoke()
        ↓
UI thread updates progress bars
        ↓
Smooth animation via data binding
```

### 3. AI Assistant Flow

```
User types command in AI input box
        ↓
User presses Enter
        ↓
IslandView.ProcessAiCommandAsync()
        ↓
AiAssistantService.ProcessCommandAsync(input)
        ↓
┌────────────────────────────────────────┐
│ CommandParser.ParseAsync()             │
│   ↓                                    │
│ OllamaClient.GenerateAsync(prompt)     │
│   ↓                                    │
│ HTTP POST to Ollama API                │
│   ↓                                    │
│ JSON response parsing                  │
│   ↓                                    │
│ ParsedCommand object created           │
└────────────────────────────────────────┘
        ↓
┌────────────────────────────────────────┐
│ CommandExecutor.ExecuteAsync(command)  │
│   ↓                                    │
│ Validate command type                  │
│   ↓                                    │
│ Validate file paths (security)         │
│   ↓                                    │
│ Execute system operation               │
│   ↓                                    │
│ Return CommandExecutionResult          │
└────────────────────────────────────────┘
        ↓
UI displays result (success/error)
        ↓
Fade in result panel with color coding
```

---

## UI State Machine

```
┌─────────────────┐
│  COMPACT MODE   │
│   (350x36)      │
└────────┬────────┘
         │
         │ Hover / Notification / Spotify Track Change
         │
         ▼
┌─────────────────┐
│  EXPANDED MODE  │
│   (680x50+)     │
└────────┬────────┘
         │
         │ Content Priority Evaluation
         │
    ┌────┴────┬────────┬──────────┬──────────┬───────────┐
    ▼         ▼        ▼          ▼          ▼           ▼
┌────────┐ ┌─────┐ ┌──────┐ ┌──────────┐ ┌──────┐ ┌────────┐
│ System │ │Head-│ │Spotify│ │ System   │ │  AI  │ │ Active │
│ Notif. │ │phone│ │ Card  │ │ Info     │ │Assist│ │ Window │
│(High)  │ │     │ │+ Media│ │ Panel    │ │      │ │ (Low)  │
└────────┘ └─────┘ └──────┘ └──────────┘ └──────┘ └────────┘
    │         │        │          │          │         │
    └─────────┴────────┴──────────┴──────────┴─────────┘
                        │
                        │ Auto-collapse after 4-6s
                        │ OR Manual collapse
                        ▼
              ┌─────────────────┐
              │  COMPACT MODE   │
              └─────────────────┘
```

---

## Threading Model

```
┌──────────────────────────────────────────────────────────────┐
│                       UI THREAD (Main)                       │
│                                                              │
│  • WPF Rendering                                            │
│  • Event handlers                                           │
│  • Data binding updates                                     │
│  • Animation playback                                       │
│                                                              │
│  IslandView.xaml.cs                                         │
│  IslandVM (PropertyChanged notifications)                   │
└───────┬────────────────────────────────────────────────┬────┘
        │                                                │
        │ Async/Await                                    │
        │                                                │
┌───────▼────────────────────┐              ┌────────────▼─────────┐
│  TASK THREAD POOL          │              │  BACKGROUND TIMER    │
│                            │              │  THREAD              │
│  • MediaSessionService     │              │                      │
│    async operations        │              │ SystemMonitorService │
│  • AiAssistantService      │              │  • 1-second timer    │
│    HTTP calls to Ollama    │              │  • Performance       │
│  • File I/O operations     │              │    counter reads     │
│                            │              │  • WMI queries       │
└────────────────────────────┘              └──────────────────────┘
        │                                                │
        │ Dispatcher.Invoke()                            │
        │ (Marshal to UI thread)                         │
        └─────────────────┬──────────────────────────────┘
                          │
                    ┌─────▼─────┐
                    │ UI UPDATE │
                    └───────────┘
```

---

## Security Architecture (AI Assistant)

```
User Input: "Create file secret.txt on Desktop"
        ↓
┌───────────────────────────────────────────────────┐
│ LAYER 1: Input Validation                         │
│ • Non-empty string check                          │
│ • Length limit (prevent prompt injection)         │
└─────────────────┬─────────────────────────────────┘
                  ↓
┌───────────────────────────────────────────────────┐
│ LAYER 2: AI Parsing (Ollama)                      │
│ • Structured JSON output only                     │
│ • Command type classification                     │
│ • Parameter extraction                            │
└─────────────────┬─────────────────────────────────┘
                  ↓
┌───────────────────────────────────────────────────┐
│ LAYER 3: Command Validation                       │
│ • Whitelist of allowed commands                   │
│ • Parameter type checking                         │
│ • No raw AI output execution                      │
└─────────────────┬─────────────────────────────────┘
                  ↓
┌───────────────────────────────────────────────────┐
│ LAYER 4: Path Sandboxing                          │
│ • Resolve absolute path                           │
│ • Check: path.StartsWith(UserProfileDirectory)    │
│ • DENY if outside user directory                  │
│ • DENY system folders (C:\Windows, etc.)          │
└─────────────────┬─────────────────────────────────┘
                  ↓
┌───────────────────────────────────────────────────┐
│ LAYER 5: Execution                                │
│ • Deterministic function call                     │
│ • Try-catch error handling                        │
│ • Return structured result                        │
└─────────────────┬─────────────────────────────────┘
                  ↓
        CommandExecutionResult
        (Success + Result) OR (Failure + Error)
```

**Blocked Examples:**
- ❌ `C:\Windows\System32\config.txt` → Access denied: Path outside user directory
- ❌ `C:\ProgramFiles\malware.exe` → Access denied: Path outside user directory
- ❌ `../../AppData/Roaming/passwords.txt` → Resolved path blocked
- ✅ `C:\Users\Yunus\Desktop\file.txt` → Allowed
- ✅ `C:\Users\Yunus\Documents\Projects\file.txt` → Allowed

---

## Performance Budget

| Component            | CPU Usage (Idle) | CPU Usage (Active) | Memory  |
|---------------------|------------------|-------------------|---------|
| Base Island UI      | < 1%            | < 2%              | ~50 MB  |
| MediaSessionService | < 0.1%          | < 0.5%            | +5 MB   |
| SystemMonitorService| < 1%            | < 2%              | +10 MB  |
| AiAssistantService  | 0% (on-demand)  | < 5% (processing) | +20 MB  |
| **TOTAL TARGET**    | **< 3%**        | **< 10%**         | **< 100MB** |

---

## Module Independence Matrix

|                  | Can work without Media | Can work without System Monitor | Can work without AI |
|-----------------|------------------------|--------------------------------|-------------------|
| Base Island     | ✅ Yes                 | ✅ Yes                         | ✅ Yes            |
| Media Controls  | N/A                    | ✅ Yes                         | ✅ Yes            |
| System Monitor  | ✅ Yes                 | N/A                            | ✅ Yes            |
| AI Assistant    | ✅ Yes                 | ✅ Yes                         | N/A               |

**Independence Achieved Through:**
1. Feature flags in AppSettings
2. Try-catch blocks for service initialization
3. Null-conditional operators (`?.`)
4. Graceful degradation (service unavailable → feature hidden)

---

## Animation Timing Chart

```
┌────────────────────────────────────────────────────────────┐
│ COMPACT → EXPANDED                                         │
│                                                            │
│ 0ms   ─────────────────────────────────────────────────→  │
│       Start expansion                                      │
│                                                            │
│ 0-130ms  Width: 350 → 680 (CubicEase.EaseOut)            │
│          Height: 36 → 50                                   │
│          Corner Radius: 18 → 25                            │
│                                                            │
│ 0-50ms   Compact content: Opacity 1 → 0                   │
│                                                            │
│ 40-120ms Expanded content: Opacity 0 → 1                  │
│                                                            │
│ 130ms  ─────────────────────────────────────────────────→ │
│        Animation complete                                  │
└────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────┐
│ EXPANDED → COMPACT                                         │
│                                                            │
│ 0ms   ─────────────────────────────────────────────────→  │
│       Start collapse                                       │
│                                                            │
│ 0-110ms  Width: 680 → 350 (CubicEase.EaseOut)            │
│          Height: 50 → 36                                   │
│          Corner Radius: 25 → 18                            │
│                                                            │
│ 0-40ms   Expanded content: Opacity 1 → 0                  │
│                                                            │
│ 30-80ms  Compact content: Opacity 0 → 1                   │
│                                                            │
│ 110ms  ─────────────────────────────────────────────────→ │
│        Animation complete                                  │
└────────────────────────────────────────────────────────────┘
```

**Media Button Interaction:**
- Hover: Background opacity 0 → 0.19 (instant)
- Press: Scale 1.0 → 0.95, Background opacity 0.19 → 0.31 (60ms)
- Release: Scale 0.95 → 1.0, Background opacity 0.31 → 0.19 (80ms)

---

## Error Handling Strategy

### MediaSessionService
```csharp
try {
    await InitializeAsync();
} catch (UnauthorizedAccessException) {
    // User denied media control permission
    // → Feature silently disabled
} catch (Exception) {
    // Windows API unavailable
    // → Feature silently disabled
}
```

### SystemMonitorService
```csharp
try {
    _cpuCounter = new PerformanceCounter(...);
} catch (UnauthorizedAccessException) {
    // Performance counters require admin on some systems
    // → Show static "N/A" values
} catch (Exception) {
    // Counter unavailable
    // → Disable specific metric
}
```

### AiAssistantService
```csharp
if (!await _ollama.IsAvailableAsync()) {
    // Ollama not running
    // → Show red indicator
    // → Input disabled with helpful message
}

try {
    await ProcessCommandAsync(input);
} catch (HttpRequestException) {
    // Ollama crashed mid-request
    // → Show error: "Ollama connection lost"
} catch (TaskCanceledException) {
    // Request timeout
    // → Show error: "Request timed out"
}
```

---

## Backward Compatibility

### Existing Code Preservation
✅ No modifications to existing service contracts
✅ No changes to existing event signatures
✅ No breaking changes to SpotifyService
✅ IslandVM timer pattern maintained
✅ All existing XAML controls preserved

### Safe Integration Points
1. **New services added as optional dependencies**
   - Constructor: `_mediaSession = new MediaSessionService();`
   - Initialization wrapped in try-catch
   - Null checks before use

2. **New XAML elements added with Visibility="Collapsed"**
   - Existing layout unaffected
   - Shown only when feature active

3. **New properties added to IslandVM**
   - Existing properties unchanged
   - New bindings independent

---

**End of Architecture Documentation**
