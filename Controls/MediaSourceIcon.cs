using System.Windows;
using System.Windows.Media;
using NI.Services.Media;

namespace NI.Controls;

// Resolution-independent brand marks; no downloads, font fallback, or per-frame bitmap work.
public sealed class MediaSourceIcon : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(MediaSourceKind), typeof(MediaSourceIcon), new FrameworkPropertyMetadata(MediaSourceKind.Generic, FrameworkPropertyMetadataOptions.AffectsRender));
    public MediaSourceKind Source { get => (MediaSourceKind)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    private static readonly Brush SpotifyGreen = Frozen(Color.FromRgb(30, 215, 96));
    private static readonly Brush YouTubeRed = Frozen(Color.FromRgb(255, 45, 61));
    private static readonly Geometry Play = Geometry.Parse("M 10,7.5 L 17,12 L 10,16.5 Z");
    private static readonly Geometry Note = Geometry.Parse("M 10,5 L 19,3 L 19,16 C 19,20 12,21 12,17 C 12,14 15,13 17,14 L 17,7 L 10,9 L 10,18 C 10,22 3,23 3,19 C 3,16 6,15 8,16 L 8,6 Z");
    public MediaSourceIcon() { Width = Height = 24; IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        if (Source == MediaSourceKind.Spotify)
        {
            dc.DrawEllipse(SpotifyGreen, null, new Point(12, 12), 11.5, 11.5);
            Arc(6, 9, 12, 6, 19, 10, 1.8);
            Arc(6.7, 12, 12, 9.5, 18, 13, 1.6);
            Arc(7.5, 15, 12, 13, 17, 16, 1.4);
        }
        else if (Source == MediaSourceKind.YouTube)
        {
            dc.DrawRoundedRectangle(YouTubeRed, null, new Rect(0, 4, 24, 16), 5, 5);
            dc.DrawGeometry(Brushes.White, null, Play);
        }
        else dc.DrawGeometry(SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brushes.LightSteelBlue, null, Note);
        dc.Pop();

        void Arc(double x, double y, double cx, double cy, double endX, double endY, double thickness)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open()) { context.BeginFigure(new Point(x, y), false, false); context.QuadraticBezierTo(new Point(cx, cy), new Point(endX, endY), true, false); }
            geometry.Freeze();
            var pen = new Pen(Brushes.Black, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; pen.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }
    }
    private static Brush Frozen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }
}
