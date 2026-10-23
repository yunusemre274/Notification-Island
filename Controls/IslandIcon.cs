using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NI.Controls;

// Windows' Fluent glyphs, with the Windows 10 MDL2 font as fallback. No emoji or bitmap scaling.
public sealed class IslandIcon : TextBlock
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(IslandIcon), new PropertyMetadata("Notification", OnKindChanged));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public IslandIcon()
    {
        FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        FontSize = 17; VerticalAlignment = VerticalAlignment.Center;
        TextAlignment = TextAlignment.Center; Text = Glyph("Notification");
        IsHitTestVisible = false;
    }
    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((IslandIcon)d).Text = Glyph((string)e.NewValue);
    private static string Glyph(string kind) => kind switch
    {
        "Wifi" => "\uE701", "Bluetooth" => "\uE702", "Airplane" => "\uE709",
        "Volume" => "\uE767", "Mute" => "\uE74F", "Brightness" => "\uE706",
        "Settings" => "\uE713", "Chevron" => "\uE70D", "Close" => "\uE711",
        "Play" => "\uE768", "Pause" => "\uE769", "Previous" => "\uE892", "Next" => "\uE893",
        "Music" => "\uE8D6", "Cpu" => "\uE950", "Gpu" => "\uE7F4", "Memory" => "\uE964",
        "Check" => "\uE73E", "Weather" => "\uE9BD", "Assistant" => "\uE99A",
        "External" => "\uE8A7", "Power" => "\uE7E8", _ => "\uE7F4"
    };
}
