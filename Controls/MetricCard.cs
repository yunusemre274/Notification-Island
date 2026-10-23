using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NI.Controls;

public sealed class MetricCard : Control
{
    static MetricCard() => DefaultStyleKeyProperty.OverrideMetadata(typeof(MetricCard), new FrameworkPropertyMetadata(typeof(MetricCard)));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(MetricCard), new PropertyMetadata("CPU"));
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(MetricCard), new PropertyMetadata("—"));
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(nameof(Detail), typeof(string), typeof(MetricCard), new PropertyMetadata(""));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(MetricCard), new PropertyMetadata(0d, OnValueChanged));
    public static readonly DependencyProperty ReduceMotionProperty = DependencyProperty.Register(nameof(ReduceMotion), typeof(bool), typeof(MetricCard), new PropertyMetadata(false));
    public bool ReduceMotion { get => (bool)GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (MetricCard)d;
        if (card.GetTemplateChild("PART_Level") is not ProgressBar level) return;
        var current = level.Value;
        var target = Math.Clamp((double)e.NewValue, 0, 100);
        level.BeginAnimation(ProgressBar.ValueProperty, null);
        level.SetCurrentValue(ProgressBar.ValueProperty, target);
        if (!card.ReduceMotion && card.IsLoaded && SystemParameters.ClientAreaAnimation)
            level.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string ValueText { get => (string)GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
    public string Detail { get => (string)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}
