using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NI.Controls;

// Border.CornerRadius only rounds its paint; it does not clip child visuals or their hit area.
public sealed class GlassSurface : Border
{
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo); UpdateClip();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == CornerRadiusProperty) UpdateClip();
    }
    private void UpdateClip()
    {
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(ActualWidth, ActualHeight) / 2);
        var clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), radius, radius);
        clip.Freeze(); Clip = clip;
    }
}
