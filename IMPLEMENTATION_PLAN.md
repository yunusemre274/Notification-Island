# Windows Dynamic Island - Feature Implementation Plan

## Executive Summary

This document outlines the modular implementation of three major features:
1. **Spotify Media Controls** - Previous, Play/Pause, Next buttons
2. **System Performance Panel** - Real-time CPU, RAM, GPU monitoring
3. **AI Assistant** - Local Ollama integration for system commands

All features are designed to integrate seamlessly with the existing architecture while maintaining full backward compatibility.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         MainWindow (WPF)                        │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                       IslandView (UI)                           │
│  ┌─────────────┬──────────────┬──────────────┬────────────────┐ │
│  │  Spotify    │   System     │  AI Assist   │   Existing     │ │
│  │  Card       │   Info Card  │  Card        │   Content      │ │
│  └─────────────┴──────────────┴──────────────┴────────────────┘ │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                    IslandVM (ViewModel)                         │
│              Consolidated Timer + State Management              │
└─────┬──────────────┬────────────────┬──────────────────────────┘
      │              │                │
┌─────▼─────┐  ┌────▼──────┐  ┌──────▼──────┐
│   Media   │  │  System   │  │  AI Command │
│  Session  │  │  Monitor  │  │  Service    │
│  Service  │  │  Service  │  │             │
└───────────┘  └───────────┘  └──────┬──────┘
                                     │
                              ┌──────▼──────┐
                              │   Ollama    │
                              │   Client    │
                              └─────────────┘
```

### Design Principles

1. **SOLID Compliance**
   - Single Responsibility: One service per feature domain
   - Open/Closed: Extensible via interfaces, closed for modification
   - Liskov Substitution: All services implement common interface patterns
   - Interface Segregation: Minimal, focused service contracts
   - Dependency Inversion: Services depend on abstractions

2. **Non-Breaking Integration**
   - New services are opt-in via feature flags
   - Existing timer pattern maintained
   - No modifications to existing service contracts
   - UI elements added as new XAML controls

3. **Performance First**
   - Async/await for all I/O operations
   - Background thread data collection
   - UI thread only for rendering
   - Debounced updates to prevent UI thrashing

---

## Feature 1: Spotify Media Controls

### Current State Analysis

**Existing Implementation:** [Services/SpotifyService.cs](Services/SpotifyService.cs)
- Uses process title parsing (`"Song - Artist"`)
- 3-second polling via IslandVM timer
- Fires `TrackChanged` event
- No actual playback control

### New Components

#### 1.1 MediaSessionService.cs

**Location:** `Services/MediaSessionService.cs`

**Purpose:** Interface with Windows Global System Media Transport Controls

**Technology Stack:**
- `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager`
- UWP API via .NET 8.0 Windows SDK

**Class Structure:**
```csharp
public class MediaSessionService : IDisposable
{
    // Properties
    public bool IsSpotifyPlaying { get; private set; }
    public TimelineProperties? PlaybackTimeline { get; private set; }

    // Events
    public event EventHandler<MediaPropertiesChangedEventArgs>? MediaPropertiesChanged;
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

    // Methods
    public async Task InitializeAsync()
    public async Task PlayAsync()
    public async Task PauseAsync()
    public async Task NextTrackAsync()
    public async Task PreviousTrackAsync()
    public async Task<MediaProperties?> GetCurrentMediaAsync()

    // Private
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private void OnSessionsChanged(...)
    private void OnMediaPropertiesChanged(...)
    private void OnPlaybackInfoChanged(...)
}
```

**Key Implementation Details:**

```csharp
// Initialization
public async Task InitializeAsync()
{
    _sessionManager = await GlobalSystemMediaTransportControlsSessionManager
        .RequestAsync();

    _sessionManager.SessionsChanged += OnSessionsChanged;

    // Get current Spotify session
    await UpdateCurrentSessionAsync();
}

// Session filtering
private async Task UpdateCurrentSessionAsync()
{
    var sessions = _sessionManager.GetSessions();

    // Priority: Spotify > other media apps
    _currentSession = sessions.FirstOrDefault(s =>
        s.SourceAppUserModelId.Contains("Spotify",
        StringComparison.OrdinalIgnoreCase));

    if (_currentSession != null)
    {
        _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
    }
}

// Playback control
public async Task PlayAsync()
{
    if (_currentSession == null) return;
    await _currentSession.TryPlayAsync();
}

public async Task PauseAsync()
{
    if (_currentSession == null) return;
    await _currentSession.TryPauseAsync();
}

public async Task NextTrackAsync()
{
    if (_currentSession == null) return;
    await _currentSession.TrySkipNextAsync();
}

public async Task PreviousTrackAsync()
{
    if (_currentSession == null) return;
    await _currentSession.TrySkipPreviousAsync();
}
```

#### 1.2 UI Integration

**Location:** [Views/IslandView.xaml](Views/IslandView.xaml) (Spotify card section)

**Current Spotify Card Structure:**
```xml
<!-- Existing elements (PRESERVED) -->
<TextBlock x:Name="SpotifyTrackText" Text="Song Title" />
<TextBlock x:Name="SpotifyArtistText" Text="Artist Name" />
<Image Source="/Assets/spotify.png" />
<ProgressBar x:Name="SpotifyProgressBar" />
```

**New Media Controls (To Add):**
```xml
<!-- Media Control Buttons (NEW) -->
<StackPanel Orientation="Horizontal"
            HorizontalAlignment="Center"
            VerticalAlignment="Bottom"
            Margin="0,8,0,0"
            x:Name="MediaControlsPanel"
            Visibility="Collapsed">

    <!-- Previous Button -->
    <Button Style="{StaticResource MediaButtonStyle}"
            Click="OnPreviousClick"
            ToolTip="Previous Track">
        <Path Data="M6,18V6H8V18H6M9.5,12L18,6V18L9.5,12Z"
              Fill="White"
              Width="16"
              Height="16"/>
    </Button>

    <!-- Play/Pause Button -->
    <Button Style="{StaticResource MediaButtonStyle}"
            Click="OnPlayPauseClick"
            Margin="12,0"
            ToolTip="Play / Pause">
        <!-- Play Icon -->
        <Path x:Name="PlayIcon"
              Data="M8,5.14V19.14L19,12.14L8,5.14Z"
              Fill="White"
              Width="20"
              Height="20"
              Visibility="Collapsed"/>
        <!-- Pause Icon -->
        <Path x:Name="PauseIcon"
              Data="M14,19H18V5H14M6,19H10V5H6V19Z"
              Fill="White"
              Width="20"
              Height="20"
              Visibility="Visible"/>
    </Button>

    <!-- Next Button -->
    <Button Style="{StaticResource MediaButtonStyle}"
            Click="OnNextClick"
            ToolTip="Next Track">
        <Path Data="M16,18H18V6H16M6,18L14.5,12L6,6V18Z"
              Fill="White"
              Width="16"
              Height="16"/>
    </Button>
</StackPanel>
```

**Button Style (Add to Resources):**
```xml
<Style x:Key="MediaButtonStyle" TargetType="Button">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Width" Value="36"/>
    <Setter Property="Height" Value="36"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="Border"
                        Background="{TemplateBinding Background}"
                        CornerRadius="18"
                        BorderThickness="0">
                    <ContentPresenter HorizontalAlignment="Center"
                                    VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Border"
                                Property="Background"
                                Value="#30FFFFFF"/>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="Border"
                                Property="Background"
                                Value="#50FFFFFF"/>
                        <Setter TargetName="Border"
                                Property="RenderTransform">
                            <Setter.Value>
                                <ScaleTransform ScaleX="0.95" ScaleY="0.95"/>
                            </Setter.Value>
                        </Setter>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

#### 1.3 Code-Behind Integration

**Location:** [Views/IslandView.xaml.cs](Views/IslandView.xaml.cs)

```csharp
// Add field
private MediaSessionService? _mediaSessionService;

// Initialize in constructor
public IslandView()
{
    InitializeComponent();

    // Existing initialization...

    // NEW: Initialize media controls
    InitializeMediaSessionAsync();
}

private async void InitializeMediaSessionAsync()
{
    try
    {
        _mediaSessionService = new MediaSessionService();
        await _mediaSessionService.InitializeAsync();

        _mediaSessionService.PlaybackStateChanged += OnPlaybackStateChanged;
    }
    catch (Exception ex)
    {
        // Log error, feature gracefully disabled
        Debug.WriteLine($"Media controls unavailable: {ex.Message}");
    }
}

// Event handlers
private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
{
    Dispatcher.Invoke(() =>
    {
        // Update Play/Pause icon
        if (e.IsPlaying)
        {
            PlayIcon.Visibility = Visibility.Collapsed;
            PauseIcon.Visibility = Visibility.Visible;
        }
        else
        {
            PlayIcon.Visibility = Visibility.Visible;
            PauseIcon.Visibility = Visibility.Collapsed;
        }
    });
}

// Button click handlers
private async void OnPreviousClick(object sender, RoutedEventArgs e)
{
    await _mediaSessionService?.PreviousTrackAsync()!;
}

private async void OnPlayPauseClick(object sender, RoutedEventArgs e)
{
    if (_mediaSessionService?.IsSpotifyPlaying == true)
        await _mediaSessionService.PauseAsync();
    else
        await _mediaSessionService?.PlayAsync()!;
}

private async void OnNextClick(object sender, RoutedEventArgs e)
{
    await _mediaSessionService?.NextTrackAsync()!;
}
```

#### 1.4 ViewModel Integration

**Location:** [ViewModels/IslandVM.cs](ViewModels/IslandVM.cs)

```csharp
// Add property
private bool _showMediaControls;
public bool ShowMediaControls
{
    get => _showMediaControls;
    set => SetProperty(ref _showMediaControls, value);
}

// Update timer tick to show controls when Spotify is active
private void OnMainTimerTick(object? sender, EventArgs e)
{
    // Existing code...

    // NEW: Show media controls when Spotify is playing
    ShowMediaControls = _spotifyService.IsPlaying;
}
```

**XAML Binding:**
```xml
<StackPanel x:Name="MediaControlsPanel"
            Visibility="{Binding ShowMediaControls,
                        Converter={StaticResource BoolToVisibilityConverter}}">
```

### Testing Strategy

1. **Spotify Running + Playing** → Controls visible, pause button active
2. **Spotify Paused** → Controls visible, play button active
3. **Spotify Not Running** → Controls hidden
4. **Background Playback** → Controls work even when Spotify minimized
5. **Track Change** → Progress bar and track info update correctly

---

## Feature 2: System Performance Panel

### Architecture

#### 2.1 SystemMonitorService.cs

**Location:** `Services/SystemMonitorService.cs`

**Purpose:** Real-time CPU, RAM, GPU monitoring with minimal overhead

**Technology Stack:**
- `System.Diagnostics.PerformanceCounter` (CPU, RAM)
- `System.Management` WMI (GPU - optional)
- Background thread data collection

**Class Structure:**
```csharp
public class SystemMonitorService : IDisposable
{
    // Properties
    public float CpuUsage { get; private set; }
    public float RamUsage { get; private set; }
    public float RamUsedGB { get; private set; }
    public float RamTotalGB { get; private set; }
    public float? GpuUsage { get; private set; } // Nullable if unavailable
    public float DiskUsage { get; private set; }

    // Events
    public event EventHandler<SystemMetricsEventArgs>? MetricsUpdated;

    // Methods
    public void Start()
    public void Stop()
    public void Dispose()

    // Private
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _ramCounter;
    private System.Threading.Timer? _updateTimer;
    private readonly object _lock = new();
}
```

**Implementation:**

```csharp
public class SystemMonitorService : IDisposable
{
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _ramAvailableCounter;
    private System.Threading.Timer? _updateTimer;
    private readonly object _lock = new();

    private float _cpuUsage;
    private float _ramUsage;
    private float _ramUsedGB;
    private float _ramTotalGB;
    private float? _gpuUsage;

    public float CpuUsage
    {
        get { lock (_lock) return _cpuUsage; }
        private set { lock (_lock) _cpuUsage = value; }
    }

    public float RamUsage
    {
        get { lock (_lock) return _ramUsage; }
        private set { lock (_lock) _ramUsage = value; }
    }

    public float RamUsedGB
    {
        get { lock (_lock) return _ramUsedGB; }
        private set { lock (_lock) _ramUsedGB = value; }
    }

    public float RamTotalGB
    {
        get { lock (_lock) return _ramTotalGB; }
        private set { lock (_lock) _ramTotalGB = value; }
    }

    public float? GpuUsage
    {
        get { lock (_lock) return _gpuUsage; }
        private set { lock (_lock) _gpuUsage = value; }
    }

    public event EventHandler<SystemMetricsEventArgs>? MetricsUpdated;

    public void Start()
    {
        try
        {
            // Initialize performance counters
            _cpuCounter = new PerformanceCounter(
                "Processor",
                "% Processor Time",
                "_Total",
                true);

            _ramAvailableCounter = new PerformanceCounter(
                "Memory",
                "Available MBytes",
                true);

            // Get total RAM
            _ramTotalGB = GetTotalPhysicalMemoryGB();

            // First read (always returns 0)
            _cpuCounter.NextValue();

            // Start background timer (1 second interval)
            _updateTimer = new System.Threading.Timer(
                UpdateMetrics,
                null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize system monitor: {ex.Message}");
        }
    }

    private void UpdateMetrics(object? state)
    {
        try
        {
            // CPU Usage
            CpuUsage = _cpuCounter?.NextValue() ?? 0;

            // RAM Usage
            float ramAvailableMB = _ramAvailableCounter?.NextValue() ?? 0;
            float ramUsedMB = (_ramTotalGB * 1024) - ramAvailableMB;
            RamUsedGB = ramUsedMB / 1024f;
            RamUsage = (ramUsedMB / (_ramTotalGB * 1024)) * 100f;

            // GPU Usage (optional, can be expensive)
            GpuUsage = GetGpuUsage();

            // Fire event on background thread
            MetricsUpdated?.Invoke(this, new SystemMetricsEventArgs
            {
                CpuUsage = CpuUsage,
                RamUsage = RamUsage,
                RamUsedGB = RamUsedGB,
                RamTotalGB = RamTotalGB,
                GpuUsage = GpuUsage
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating metrics: {ex.Message}");
        }
    }

    private float GetTotalPhysicalMemoryGB()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");

            foreach (ManagementObject obj in searcher.Get())
            {
                var totalBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                return totalBytes / (1024f * 1024f * 1024f);
            }
        }
        catch
        {
            return 16; // Fallback
        }
        return 16;
    }

    private float? GetGpuUsage()
    {
        // Optional: Implement GPU monitoring via WMI or LibreHardwareMonitor
        // For minimal overhead, return null initially
        return null;
    }

    public void Stop()
    {
        _updateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        Stop();
        _updateTimer?.Dispose();
        _cpuCounter?.Dispose();
        _ramAvailableCounter?.Dispose();
    }
}

public class SystemMetricsEventArgs : EventArgs
{
    public float CpuUsage { get; init; }
    public float RamUsage { get; init; }
    public float RamUsedGB { get; init; }
    public float RamTotalGB { get; init; }
    public float? GpuUsage { get; init; }
}
```

#### 2.2 UI Implementation

**Location:** [Views/IslandView.xaml](Views/IslandView.xaml)

**New System Info Card:**
```xml
<!-- System Info Card (NEW) -->
<Border x:Name="SystemInfoCard"
        Background="#1E1E1E"
        CornerRadius="20"
        Padding="16,12"
        Visibility="Collapsed"
        Width="320"
        Height="Auto">

    <StackPanel>
        <!-- Header -->
        <TextBlock Text="SYSTEM INFO"
                   FontSize="10"
                   FontWeight="SemiBold"
                   Foreground="#888"
                   Margin="0,0,0,12"/>

        <!-- CPU Usage -->
        <Grid Margin="0,0,0,10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Column="0"
                       Text="CPU"
                       FontSize="13"
                       Foreground="White"
                       VerticalAlignment="Center"/>

            <ProgressBar Grid.Column="1"
                         x:Name="CpuProgressBar"
                         Height="6"
                         Margin="12,0"
                         Minimum="0"
                         Maximum="100"
                         Value="0"
                         Background="#333"
                         Foreground="#007AFF"
                         BorderThickness="0"/>

            <TextBlock Grid.Column="2"
                       x:Name="CpuPercentText"
                       Text="0%"
                       FontSize="13"
                       FontWeight="Medium"
                       Foreground="White"
                       VerticalAlignment="Center"/>
        </Grid>

        <!-- RAM Usage -->
        <Grid Margin="0,0,0,10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Column="0"
                       Text="RAM"
                       FontSize="13"
                       Foreground="White"
                       VerticalAlignment="Center"/>

            <ProgressBar Grid.Column="1"
                         x:Name="RamProgressBar"
                         Height="6"
                         Margin="12,0"
                         Minimum="0"
                         Maximum="100"
                         Value="0"
                         Background="#333"
                         Foreground="#FF9500"
                         BorderThickness="0"/>

            <TextBlock Grid.Column="2"
                       x:Name="RamPercentText"
                       Text="0%"
                       FontSize="13"
                       FontWeight="Medium"
                       Foreground="White"
                       VerticalAlignment="Center"/>
        </Grid>

        <!-- GPU Usage (Optional) -->
        <Grid x:Name="GpuPanel"
              Margin="0,0,0,10"
              Visibility="Collapsed">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Column="0"
                       Text="GPU"
                       FontSize="13"
                       Foreground="White"
                       VerticalAlignment="Center"/>

            <ProgressBar Grid.Column="1"
                         x:Name="GpuProgressBar"
                         Height="6"
                         Margin="12,0"
                         Minimum="0"
                         Maximum="100"
                         Value="0"
                         Background="#333"
                         Foreground="#30D158"
                         BorderThickness="0"/>

            <TextBlock Grid.Column="2"
                       x:Name="GpuPercentText"
                       Text="0%"
                       FontSize="13"
                       FontWeight="Medium"
                       Foreground="White"
                       VerticalAlignment="Center"/>
        </Grid>

        <!-- Detailed Info (On Hover) -->
        <Border x:Name="DetailedInfoPanel"
                Background="#252525"
                CornerRadius="10"
                Padding="10"
                Margin="0,8,0,0"
                Visibility="Collapsed">
            <StackPanel>
                <TextBlock x:Name="DetailedCpuText"
                           Text="CPU: 8 cores @ 2.4 GHz"
                           FontSize="11"
                           Foreground="#AAA"
                           Margin="0,0,0,4"/>
                <TextBlock x:Name="DetailedRamText"
                           Text="RAM: 12.5 GB / 16.0 GB"
                           FontSize="11"
                           Foreground="#AAA"/>
            </StackPanel>
        </Border>
    </StackPanel>
</Border>
```

#### 2.3 Code-Behind Integration

**Location:** [Views/IslandView.xaml.cs](Views/IslandView.xaml.cs)

```csharp
private SystemMonitorService? _systemMonitor;
private bool _isSystemInfoVisible;

// Initialize
private void InitializeSystemMonitor()
{
    _systemMonitor = new SystemMonitorService();
    _systemMonitor.MetricsUpdated += OnSystemMetricsUpdated;
    _systemMonitor.Start();
}

// Event handler
private void OnSystemMetricsUpdated(object? sender, SystemMetricsEventArgs e)
{
    Dispatcher.Invoke(() =>
    {
        // Update progress bars
        CpuProgressBar.Value = e.CpuUsage;
        CpuPercentText.Text = $"{e.CpuUsage:F0}%";

        RamProgressBar.Value = e.RamUsage;
        RamPercentText.Text = $"{e.RamUsage:F0}%";

        // Update detailed text
        DetailedRamText.Text = $"RAM: {e.RamUsedGB:F1} GB / {e.RamTotalGB:F1} GB";

        // GPU (if available)
        if (e.GpuUsage.HasValue)
        {
            GpuPanel.Visibility = Visibility.Visible;
            GpuProgressBar.Value = e.GpuUsage.Value;
            GpuPercentText.Text = $"{e.GpuUsage.Value:F0}%";
        }
    });
}

// Toggle visibility (via keyboard shortcut or click)
public void ToggleSystemInfo()
{
    _isSystemInfoVisible = !_isSystemInfoVisible;

    if (_isSystemInfoVisible)
    {
        SystemInfoCard.Visibility = Visibility.Visible;
        AnimateSystemInfoIn();
    }
    else
    {
        AnimateSystemInfoOut();
    }
}

private void AnimateSystemInfoIn()
{
    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
    SystemInfoCard.BeginAnimation(OpacityProperty, fadeIn);
}

private void AnimateSystemInfoOut()
{
    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
    fadeOut.Completed += (s, e) => SystemInfoCard.Visibility = Visibility.Collapsed;
    SystemInfoCard.BeginAnimation(OpacityProperty, fadeOut);
}

// Hover event for detailed info
private void SystemInfoCard_MouseEnter(object sender, MouseEventArgs e)
{
    DetailedInfoPanel.Visibility = Visibility.Visible;
}

private void SystemInfoCard_MouseLeave(object sender, MouseEventArgs e)
{
    DetailedInfoPanel.Visibility = Visibility.Collapsed;
}
```

### Performance Optimization

1. **Background Thread Updates** - Metrics collected on timer thread, not UI thread
2. **1-Second Interval** - Balanced between real-time and overhead
3. **Lock-Free Reads** - Properties use locks only for thread safety
4. **Optional GPU** - Expensive GPU monitoring disabled by default
5. **Stop When Hidden** - Service pauses when System Info card not visible

---

## Feature 3: AI Assistant (Ollama Integration)

### Architecture

```
User Input → AiAssistantService → OllamaClient → Ollama API
                   ↓
            CommandParser (Intent Detection)
                   ↓
            CommandExecutor (System Operations)
                   ↓
            Result → UI Feedback
```

#### 3.1 OllamaClient.cs

**Location:** `Services/AI/OllamaClient.cs`

**Purpose:** HTTP client for local Ollama API

```csharp
public class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaClient(string baseUrl = "http://localhost:11434",
                        string model = "llama2")
    {
        _baseUrl = baseUrl;
        _model = model;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string> GenerateAsync(string prompt,
                                            CancellationToken ct = default)
    {
        var request = new
        {
            model = _model,
            prompt = prompt,
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/generate",
            request,
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(ct);
        return result?.Response ?? string.Empty;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

internal class OllamaResponse
{
    public string? Response { get; set; }
    public bool Done { get; set; }
}
```

#### 3.2 CommandParser.cs

**Location:** `Services/AI/CommandParser.cs`

**Purpose:** Parse AI response into structured commands

```csharp
public enum CommandType
{
    CreateFile,
    FindFolder,
    GetFolderPath,
    ListFiles,
    OpenApplication,
    Unknown
}

public class ParsedCommand
{
    public CommandType Type { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string OriginalInput { get; set; } = string.Empty;
}

public class CommandParser
{
    // System prompt for Ollama
    private const string SYSTEM_PROMPT = @"
You are a Windows command interpreter. Parse user requests into structured commands.

Available commands:
1. CREATE_FILE: {""filename"": ""name.ext"", ""location"": ""path""}
2. FIND_FOLDER: {""name"": ""folder_name""}
3. GET_PATH: {""location"": ""Desktop|Downloads|Documents""}
4. LIST_FILES: {""location"": ""path"", ""extension"": "".txt""}

Respond ONLY with JSON. Example:
User: ""Create file text.txt on Desktop""
Response: {""command"": ""CREATE_FILE"", ""filename"": ""text.txt"", ""location"": ""Desktop""}

User: ""Find Projects folder""
Response: {""command"": ""FIND_FOLDER"", ""name"": ""Projects""}
";

    private readonly OllamaClient _ollama;

    public CommandParser(OllamaClient ollama)
    {
        _ollama = ollama;
    }

    public async Task<ParsedCommand> ParseAsync(string userInput,
                                                CancellationToken ct = default)
    {
        var prompt = $"{SYSTEM_PROMPT}\n\nUser: \"{userInput}\"\nResponse:";

        var response = await _ollama.GenerateAsync(prompt, ct);

        // Parse JSON response
        return ParseJsonResponse(response, userInput);
    }

    private ParsedCommand ParseJsonResponse(string json, string originalInput)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var command = root.GetProperty("command").GetString();
            var type = command switch
            {
                "CREATE_FILE" => CommandType.CreateFile,
                "FIND_FOLDER" => CommandType.FindFolder,
                "GET_PATH" => CommandType.GetFolderPath,
                "LIST_FILES" => CommandType.ListFiles,
                _ => CommandType.Unknown
            };

            var parameters = new Dictionary<string, string>();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name != "command")
                    parameters[prop.Name] = prop.Value.GetString() ?? "";
            }

            return new ParsedCommand
            {
                Type = type,
                Parameters = parameters,
                OriginalInput = originalInput
            };
        }
        catch
        {
            return new ParsedCommand
            {
                Type = CommandType.Unknown,
                OriginalInput = originalInput
            };
        }
    }
}
```

#### 3.3 CommandExecutor.cs

**Location:** `Services/AI/CommandExecutor.cs`

**Purpose:** Execute validated system commands

```csharp
public class CommandExecutionResult
{
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
}

public class CommandExecutor
{
    private readonly string _userProfile;

    public CommandExecutor()
    {
        _userProfile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
    }

    public async Task<CommandExecutionResult> ExecuteAsync(ParsedCommand command)
    {
        try
        {
            return command.Type switch
            {
                CommandType.CreateFile => await CreateFileAsync(command.Parameters),
                CommandType.FindFolder => await FindFolderAsync(command.Parameters),
                CommandType.GetFolderPath => GetFolderPath(command.Parameters),
                CommandType.ListFiles => await ListFilesAsync(command.Parameters),
                _ => new CommandExecutionResult
                {
                    Success = false,
                    Error = "Unknown command"
                }
            };
        }
        catch (Exception ex)
        {
            return new CommandExecutionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<CommandExecutionResult> CreateFileAsync(
        Dictionary<string, string> parameters)
    {
        var filename = parameters.GetValueOrDefault("filename", "file.txt");
        var location = parameters.GetValueOrDefault("location", "Desktop");

        var basePath = GetSpecialFolderPath(location);
        var fullPath = Path.Combine(basePath, filename);

        // Validate path (security)
        if (!fullPath.StartsWith(_userProfile))
            return new CommandExecutionResult
            {
                Success = false,
                Error = "Access denied: Path outside user directory"
            };

        await File.WriteAllTextAsync(fullPath, string.Empty);

        return new CommandExecutionResult
        {
            Success = true,
            Result = $"Created: {fullPath}"
        };
    }

    private async Task<CommandExecutionResult> FindFolderAsync(
        Dictionary<string, string> parameters)
    {
        var name = parameters.GetValueOrDefault("name", "");

        // Search in common locations
        var searchPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(_userProfile, "Downloads"),
            _userProfile
        };

        foreach (var basePath in searchPaths)
        {
            var dirs = Directory.GetDirectories(basePath, name,
                SearchOption.TopDirectoryOnly);

            if (dirs.Length > 0)
            {
                return new CommandExecutionResult
                {
                    Success = true,
                    Result = dirs[0]
                };
            }
        }

        return new CommandExecutionResult
        {
            Success = false,
            Error = $"Folder '{name}' not found"
        };
    }

    private CommandExecutionResult GetFolderPath(
        Dictionary<string, string> parameters)
    {
        var location = parameters.GetValueOrDefault("location", "Desktop");
        var path = GetSpecialFolderPath(location);

        return new CommandExecutionResult
        {
            Success = true,
            Result = path
        };
    }

    private async Task<CommandExecutionResult> ListFilesAsync(
        Dictionary<string, string> parameters)
    {
        var location = parameters.GetValueOrDefault("location", "Desktop");
        var extension = parameters.GetValueOrDefault("extension", "*");

        var basePath = GetSpecialFolderPath(location);
        var pattern = extension == "*" ? "*.*" : $"*{extension}";

        var files = Directory.GetFiles(basePath, pattern);
        var fileList = string.Join("\n", files.Select(Path.GetFileName));

        return new CommandExecutionResult
        {
            Success = true,
            Result = fileList
        };
    }

    private string GetSpecialFolderPath(string location)
    {
        return location.ToLower() switch
        {
            "desktop" => Environment.GetFolderPath(
                Environment.SpecialFolder.Desktop),
            "downloads" => Path.Combine(_userProfile, "Downloads"),
            "documents" => Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
            "pictures" => Environment.GetFolderPath(
                Environment.SpecialFolder.MyPictures),
            _ => Environment.GetFolderPath(
                Environment.SpecialFolder.Desktop)
        };
    }
}
```

#### 3.4 AiAssistantService.cs

**Location:** `Services/AiAssistantService.cs`

**Purpose:** Orchestrate AI → Command → Execution flow

```csharp
public class AiAssistantService : IDisposable
{
    private readonly OllamaClient _ollama;
    private readonly CommandParser _parser;
    private readonly CommandExecutor _executor;
    private bool _isAvailable;

    public bool IsAvailable => _isAvailable;

    public event EventHandler<CommandResultEventArgs>? CommandCompleted;

    public AiAssistantService()
    {
        _ollama = new OllamaClient();
        _parser = new CommandParser(_ollama);
        _executor = new CommandExecutor();
    }

    public async Task InitializeAsync()
    {
        _isAvailable = await _ollama.IsAvailableAsync();
    }

    public async Task<CommandExecutionResult> ProcessCommandAsync(
        string userInput,
        CancellationToken ct = default)
    {
        if (!_isAvailable)
        {
            return new CommandExecutionResult
            {
                Success = false,
                Error = "Ollama is not available. Please start Ollama."
            };
        }

        // Step 1: Parse command using AI
        var parsedCommand = await _parser.ParseAsync(userInput, ct);

        // Step 2: Execute validated command
        var result = await _executor.ExecuteAsync(parsedCommand);

        // Step 3: Fire event
        CommandCompleted?.Invoke(this, new CommandResultEventArgs
        {
            Input = userInput,
            Result = result
        });

        return result;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

public class CommandResultEventArgs : EventArgs
{
    public string Input { get; init; } = string.Empty;
    public CommandExecutionResult Result { get; init; } = null!;
}
```

#### 3.5 UI Implementation

**Location:** [Views/IslandView.xaml](Views/IslandView.xaml)

**AI Assistant Card:**
```xml
<!-- AI Assistant Card (NEW) -->
<Border x:Name="AiAssistantCard"
        Background="#2D2D2D"
        CornerRadius="20"
        Padding="16,12"
        Visibility="Collapsed"
        Width="400"
        Height="Auto">

    <StackPanel>
        <!-- Header -->
        <Grid Margin="0,0,0,12">
            <TextBlock Text="AI ASSISTANT"
                       FontSize="10"
                       FontWeight="SemiBold"
                       Foreground="#888"
                       HorizontalAlignment="Left"/>

            <Ellipse Width="8"
                     Height="8"
                     x:Name="OllamaStatusIndicator"
                     Fill="#30D158"
                     HorizontalAlignment="Right"
                     VerticalAlignment="Center"
                     ToolTip="Ollama Online"/>
        </Grid>

        <!-- Input Field -->
        <Border Background="#1E1E1E"
                CornerRadius="12"
                Padding="12,8"
                Margin="0,0,0,12">
            <TextBox x:Name="AiInputBox"
                     Background="Transparent"
                     BorderThickness="0"
                     Foreground="White"
                     FontSize="13"
                     Text="Try: 'Find the Projects folder'"
                     GotFocus="OnAiInputFocus"
                     KeyDown="OnAiInputKeyDown"
                     CaretBrush="White"/>
        </Border>

        <!-- Result Display -->
        <Border x:Name="AiResultPanel"
                Background="#1A1A1A"
                CornerRadius="10"
                Padding="12"
                Visibility="Collapsed">
            <TextBlock x:Name="AiResultText"
                       FontSize="12"
                       Foreground="#DDD"
                       TextWrapping="Wrap"
                       FontFamily="Consolas"/>
        </Border>

        <!-- Loading Indicator -->
        <StackPanel x:Name="AiLoadingPanel"
                    Orientation="Horizontal"
                    HorizontalAlignment="Center"
                    Visibility="Collapsed"
                    Margin="0,8,0,0">
            <TextBlock Text="Processing..."
                       FontSize="11"
                       Foreground="#888"
                       Margin="0,0,8,0"/>
            <ProgressBar IsIndeterminate="True"
                         Width="100"
                         Height="3"
                         Background="Transparent"
                         Foreground="#007AFF"/>
        </StackPanel>
    </StackPanel>
</Border>
```

#### 3.6 Code-Behind Integration

**Location:** [Views/IslandView.xaml.cs](Views/IslandView.xaml.cs)

```csharp
private AiAssistantService? _aiAssistant;
private CancellationTokenSource? _aiCancellationTokenSource;

private async void InitializeAiAssistant()
{
    _aiAssistant = new AiAssistantService();
    await _aiAssistant.InitializeAsync();

    // Update status indicator
    OllamaStatusIndicator.Fill = _aiAssistant.IsAvailable
        ? new SolidColorBrush(Color.FromRgb(48, 209, 88))  // Green
        : new SolidColorBrush(Color.FromRgb(255, 69, 58)); // Red

    OllamaStatusIndicator.ToolTip = _aiAssistant.IsAvailable
        ? "Ollama Online"
        : "Ollama Offline";

    _aiAssistant.CommandCompleted += OnAiCommandCompleted;
}

private void OnAiInputFocus(object sender, RoutedEventArgs e)
{
    if (AiInputBox.Text == "Try: 'Find the Projects folder'")
        AiInputBox.Text = string.Empty;
}

private async void OnAiInputKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Enter)
    {
        await ProcessAiCommandAsync();
    }
}

private async Task ProcessAiCommandAsync()
{
    var input = AiInputBox.Text.Trim();
    if (string.IsNullOrEmpty(input)) return;

    // Show loading
    AiLoadingPanel.Visibility = Visibility.Visible;
    AiResultPanel.Visibility = Visibility.Collapsed;

    _aiCancellationTokenSource = new CancellationTokenSource();

    try
    {
        var result = await _aiAssistant!.ProcessCommandAsync(
            input,
            _aiCancellationTokenSource.Token);

        // Hide loading
        AiLoadingPanel.Visibility = Visibility.Collapsed;

        // Show result
        AiResultPanel.Visibility = Visibility.Visible;
        AiResultText.Text = result.Success
            ? $"✓ {result.Result}"
            : $"✗ {result.Error}";
        AiResultText.Foreground = result.Success
            ? new SolidColorBrush(Color.FromRgb(48, 209, 88))
            : new SolidColorBrush(Color.FromRgb(255, 69, 58));

        // Clear input
        AiInputBox.Text = string.Empty;
    }
    catch (Exception ex)
    {
        AiLoadingPanel.Visibility = Visibility.Collapsed;
        AiResultPanel.Visibility = Visibility.Visible;
        AiResultText.Text = $"✗ Error: {ex.Message}";
        AiResultText.Foreground = new SolidColorBrush(
            Color.FromRgb(255, 69, 58));
    }
}

private void OnAiCommandCompleted(object? sender, CommandResultEventArgs e)
{
    // Already handled in ProcessAiCommandAsync
}

// Toggle visibility
public void ToggleAiAssistant()
{
    if (AiAssistantCard.Visibility == Visibility.Visible)
    {
        AnimateAiAssistantOut();
    }
    else
    {
        AiAssistantCard.Visibility = Visibility.Visible;
        AnimateAiAssistantIn();
        AiInputBox.Focus();
    }
}
```

---

## Feature Flags & Configuration

**Location:** [Models/AppSettings.cs](Models/AppSettings.cs)

```csharp
public class AppSettings
{
    // Existing settings...

    // NEW: Feature flags
    public bool EnableMediaControls { get; set; } = true;
    public bool EnableSystemMonitor { get; set; } = true;
    public bool EnableAiAssistant { get; set; } = true;

    // NEW: AI settings
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama2";
}
```

---

## Project Dependencies

**Update:** [NI.csproj](NI.csproj)

```xml
<ItemGroup>
  <!-- Existing -->
  <PackageReference Include="NAudio" Version="2.2.1" />
  <PackageReference Include="System.Management" Version="10.0.0" />

  <!-- NEW: For JSON handling -->
  <PackageReference Include="System.Text.Json" Version="8.0.0" />
</ItemGroup>
```

---

## Testing Strategy

### 1. Spotify Media Controls
- [ ] Controls appear when Spotify is playing
- [ ] Play/Pause toggles correctly
- [ ] Previous/Next skip tracks
- [ ] Controls work with Spotify in background
- [ ] No controls when Spotify is closed

### 2. System Monitor
- [ ] CPU usage updates every second
- [ ] RAM usage matches Task Manager
- [ ] Progress bars animate smoothly
- [ ] Detailed info shows on hover
- [ ] Service stops when card hidden

### 3. AI Assistant
- [ ] Ollama availability detected correctly
- [ ] File creation works on Desktop
- [ ] Folder search finds existing folders
- [ ] Path queries return correct paths
- [ ] Invalid commands show error messages
- [ ] Paths outside user directory are blocked

---

## Deployment Checklist

- [ ] All existing features tested and working
- [ ] New features behind feature flags
- [ ] No breaking changes to existing code
- [ ] Error handling for all async operations
- [ ] UI animations match existing style
- [ ] Performance overhead < 5% CPU when idle
- [ ] Works on Windows 10 and Windows 11
- [ ] No third-party cloud dependencies

---

## File Structure Summary

```
NI/
├── Services/
│   ├── MediaSessionService.cs         (NEW)
│   ├── SystemMonitorService.cs        (NEW)
│   ├── AiAssistantService.cs          (NEW)
│   └── AI/                            (NEW)
│       ├── OllamaClient.cs
│       ├── CommandParser.cs
│       └── CommandExecutor.cs
├── Models/
│   └── AppSettings.cs                 (MODIFIED)
├── ViewModels/
│   └── IslandVM.cs                    (MODIFIED)
└── Views/
    ├── IslandView.xaml                (MODIFIED)
    └── IslandView.xaml.cs             (MODIFIED)
```

---

## Next Steps

1. Review and approve this implementation plan
2. Implement features in order:
   - Spotify Media Controls (lowest risk)
   - System Monitor (medium complexity)
   - AI Assistant (highest complexity)
3. Test each feature independently
4. Integration testing
5. Performance profiling
6. User acceptance testing

---

**End of Implementation Plan**
