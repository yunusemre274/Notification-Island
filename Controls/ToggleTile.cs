using System.Windows;
using System.Windows.Controls;

namespace NI.Controls;

public sealed class ToggleTile : Button
{
    static ToggleTile() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleTile), new FrameworkPropertyMetadata(typeof(ToggleTile)));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ToggleTile), new PropertyMetadata("Wifi"));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(ToggleTile), new PropertyMetadata(""));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(string), typeof(ToggleTile), new PropertyMetadata(""));
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ToggleTile), new PropertyMetadata(false));
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Status { get => (string)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
}
