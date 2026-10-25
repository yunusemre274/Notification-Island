using NI.Core;

namespace NI.Configuration;

public enum GlassTheme { DarkGlass, DarkerGlass, BlueGlass }
public enum GlassMaterial { None, Mica, Acrylic }

public sealed class AppearanceSettings : ObservableObject
{
    private GlassTheme _theme;
    public GlassTheme Theme { get => _theme; set => Set(ref _theme, Enum.IsDefined(value) ? value : GlassTheme.DarkGlass); }
    private GlassMaterial _material = GlassMaterial.Acrylic;
    public GlassMaterial Material { get => _material; set => Set(ref _material, Enum.IsDefined(value) ? value : GlassMaterial.Acrylic); }
    private double _opacity = .62;
    public double SurfaceOpacity { get => _opacity; set => Set(ref _opacity, Clamp(value, .45, 1, .62)); }
    private double _radius = 24;
    public double CornerRadius { get => _radius; set => Set(ref _radius, Clamp(value, 8, 32, 24)); }
    private double _compactWidth = 460;
    public double CompactWidth { get => _compactWidth; set => Set(ref _compactWidth, Clamp(value, 360, 640, 460)); }
    private double _panelWidth = 760;
    public double PanelWidth { get => _panelWidth; set => Set(ref _panelWidth, Clamp(value, 680, 1000, 760)); }
    private double _compactHeight = 46;
    public double CompactHeight { get => _compactHeight; set => Set(ref _compactHeight, Clamp(value, 42, 64, 46)); }
    private double _expandedMaxHeight = 460;
    public double ExpandedMaxHeight { get => _expandedMaxHeight; set => Set(ref _expandedMaxHeight, Clamp(value, 420, 720, 460)); }
    private double _horizontalOffset;
    public double HorizontalOffset { get => _horizontalOffset; set => Set(ref _horizontalOffset, Clamp(value, -2000, 2000, 0)); }
    private double _topOffset = 12;
    public double TopOffset { get => _topOffset; set => Set(ref _topOffset, Clamp(value, 0, 160, 12)); }
    private bool _alwaysOnTop = true;
    public bool AlwaysOnTop { get => _alwaysOnTop; set => Set(ref _alwaysOnTop, value); }
    private bool _expandOnHover;
    public bool ExpandOnHover { get => _expandOnHover; set => Set(ref _expandOnHover, value); }
    // Retained for JSON compatibility. Closing is now an explicit user action, never hover/focus loss.
    private bool _autoCollapse;
    public bool AutoCollapse { get => _autoCollapse; set => Set(ref _autoCollapse, value); }
    private bool _showClock = true;
    public bool ShowClock { get => _showClock; set => Set(ref _showClock, value); }
    private bool _showWeather;
    public bool ShowWeather { get => _showWeather; set => Set(ref _showWeather, value); }
    private bool _showMediaSummary = true;
    public bool ShowMediaSummary { get => _showMediaSummary; set => Set(ref _showMediaSummary, value); }
    private bool _showNotificationSummary = true;
    public bool ShowNotificationSummary { get => _showNotificationSummary; set => Set(ref _showNotificationSummary, value); }
    private bool _showNotifications = true;
    public bool ShowNotifications { get => _showNotifications; set => Set(ref _showNotifications, value); }
    private bool _showSystemStatus = true;
    public bool ShowSystemStatus { get => _showSystemStatus; set => Set(ref _showSystemStatus, value); }
    private bool _showMedia = true;
    public bool ShowMedia { get => _showMedia; set => Set(ref _showMedia, value); }
    private bool _showConnections = true;
    public bool ShowConnections { get => _showConnections; set => Set(ref _showConnections, value); }
    private bool _reduceMotion;
    public bool ReduceMotion { get => _reduceMotion; set => Set(ref _reduceMotion, value); }
    public static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
