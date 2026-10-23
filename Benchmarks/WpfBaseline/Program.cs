using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

internal static class Baseline
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Deliberately minimal framework/render baseline, not the product acceptance workload.
        if (args.Contains("--software")) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var application = new Application();
        application.Run(new Window { Title = "NI WPF framework baseline", Width = 460, Height = 48,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            Content = new Border { Background = new SolidColorBrush(Color.FromRgb(23, 31, 45)), CornerRadius = new CornerRadius(24),
                Child = new TextBlock { Text = "Notification Island • WPF baseline", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) } } });
    }
}
