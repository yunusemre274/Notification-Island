using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NI.Controls;

// Decorative playback indicator, not an audio spectrum. Nine retained visuals animate only
// render transforms at 20 fps. No frame callbacks, polling, layout animation or audio capture.
public sealed class MediaWaveform : FrameworkElement
{
    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(MediaWaveform), new PropertyMetadata(false, OnAnimationChanged));
    public static readonly DependencyProperty ReduceMotionProperty = DependencyProperty.Register(nameof(ReduceMotion), typeof(bool), typeof(MediaWaveform), new PropertyMetadata(false, OnAnimationChanged));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(MediaWaveform), new PropertyMetadata(Brushes.LightSteelBlue, OnBrushChanged));
    private readonly VisualCollection _visuals;
    private readonly List<ScaleTransform> _scales = new();
    private bool _animating;
    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public bool ReduceMotion { get => (bool)GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public int ActiveAnimationCount => _scales.Count(scale => scale.HasAnimatedProperties);
    public MediaWaveform()
    {
        _visuals = new VisualCollection(this);
        Width = 32; Height = 20; IsHitTestVisible = false;
        Loaded += (_, _) => { SystemParameters.StaticPropertyChanged += OnSystemSettingsChanged; UpdateAnimation(); };
        Unloaded += (_, _) => { SystemParameters.StaticPropertyChanged -= OnSystemSettingsChanged; Stop(); };
        IsVisibleChanged += (_, _) => UpdateAnimation();
    }
    protected override int VisualChildrenCount => _visuals?.Count ?? 0;
    protected override Visual GetVisualChild(int index) => _visuals[index];
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); BuildVisuals(); }
    private static void OnAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MediaWaveform)d).UpdateAnimation();
    private static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MediaWaveform)d).BuildVisuals();
    private void OnSystemSettingsChanged(object? sender, PropertyChangedEventArgs e) => UpdateAnimation();
    private void BuildVisuals()
    {
        Stop(); _visuals.Clear(); _scales.Clear();
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        for (var i = 0; i < 9; i++)
        {
            var visual = new DrawingVisual();
            var transform = new ScaleTransform(1, .2 + (i % 3) * .09, 0, ActualHeight / 2);
            visual.Transform = transform;
            using (var dc = visual.RenderOpen())
                dc.DrawRoundedRectangle(Foreground, null, new Rect(i * ActualWidth / 9, 0, 2, ActualHeight), 1, 1);
            _visuals.Add(visual); _scales.Add(transform);
        }
        UpdateAnimation();
    }
    private void UpdateAnimation()
    {
        var animate = IsLoaded && IsVisible && IsPlaying && !ReduceMotion && SystemParameters.ClientAreaAnimation;
        if (!animate) { Stop(); return; }
        if (_animating || _scales.Count == 0) return;
        _animating = true;
        for (var i = 0; i < _scales.Count; i++)
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1050 + (i % 3) * 170), RepeatBehavior = RepeatBehavior.Forever };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(.18 + i % 3 * .08, KeyTime.FromPercent(0)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(.65 + i % 4 * .10, KeyTime.FromPercent(.28)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(.24 + i % 2 * .12, KeyTime.FromPercent(.55)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(.55 + i % 3 * .15, KeyTime.FromPercent(.78)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(.18 + i % 3 * .08, KeyTime.FromPercent(1)));
            Timeline.SetDesiredFrameRate(animation, 20);
            _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
    }
    private void Stop()
    {
        foreach (var scale in _scales) scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _animating = false;
    }
}
