using System.Windows;
using System.Windows.Media;
using NI.Configuration;

namespace NI.Themes;

public static class ThemeManager
{
    public static void Initialize()
    {
        var resources = Application.Current.Resources.MergedDictionaries;
        resources.Add(new ResourceDictionary { Source = new Uri("/NI;component/Themes/Tokens.xaml", UriKind.Relative) });
        resources.Add(new ResourceDictionary { Source = new Uri("/NI;component/Styles/Controls.xaml", UriKind.Relative) });
    }
    public static void Apply(AppearanceSettings settings, bool nativeBackdrop)
    {
        var (r, g, b) = settings.Theme switch
        {
            GlassTheme.DarkerGlass => (12, 16, 23),
            GlassTheme.BlueGlass => (24, 42, 64),
            _ => (23, 31, 45)
        };
        var alpha = nativeBackdrop ? (byte)Math.Round(settings.SurfaceOpacity * 255) : (byte)255;
        SetBrush("SurfaceBrush", Color.FromArgb(alpha, (byte)r, (byte)g, (byte)b));
        // System high contrast overrides custom palette, transparency and native material.
        if (SystemParameters.HighContrast)
        {
            Application.Current.Resources["SurfaceBrush"] = SystemColors.WindowBrush;
            Application.Current.Resources["TextPrimary"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["TextSecondary"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["TextMuted"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["AccentBrush"] = SystemColors.HighlightBrush;
            Application.Current.Resources["InputSurface"] = SystemColors.WindowBrush;
            Application.Current.Resources["CardBrush"] = SystemColors.ControlBrush;
            Application.Current.Resources["StrokeBrush"] = SystemColors.ActiveBorderBrush;
        }
        else
        {
            foreach (var key in new[] { "InputSurface", "CardBrush", "StrokeBrush" }) Application.Current.Resources.Remove(key);
            SetBrush("TextPrimary", Color.FromRgb(240, 244, 250));
            SetBrush("TextSecondary", Color.FromRgb(164, 177, 196));
            SetBrush("TextMuted", Color.FromRgb(130, 145, 166));
            SetBrush("AccentBrush", Color.FromRgb(168, 199, 250));
        }
        Application.Current.Resources["PanelRadius"] = new CornerRadius(settings.CornerRadius);
    }
    private static void SetBrush(string name, Color color)
    {
        var brush = new SolidColorBrush(color); brush.Freeze(); Application.Current.Resources[name] = brush;
    }
}
