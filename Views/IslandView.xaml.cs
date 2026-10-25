using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using NI.ViewModels;

namespace NI.Views;

public partial class IslandView : UserControl
{
    private IslandVM? _observed;
    public IslandView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Observe(DataContext as IslandVM);
        Loaded += (_, _) => Observe(DataContext as IslandVM);
        Unloaded += (_, _) => Observe(null);
    }
    private void Observe(IslandVM? vm)
    {
        if (_observed != null) _observed.PropertyChanged -= OnViewModelChanged;
        _observed = vm;
        if (_observed != null) _observed.PropertyChanged += OnViewModelChanged;
    }
    private bool MotionEnabled => _observed != null && !_observed.Appearance.ReduceMotion && SystemParameters.ClientAreaAnimation;
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IslandVM.IsTaskInputOpen) && _observed?.IsTaskInputOpen == true)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (_observed?.IsTaskInputOpen != true) return;
                if (Window.GetWindow(this) is { } window) FocusManager.SetFocusedElement(window, TaskInputBox);
                TaskInputBox.Focus(); Keyboard.Focus(TaskInputBox);
                TaskInputBox.CaretIndex = TaskInputBox.Text.Length;
            }));
        if (e.PropertyName == nameof(IslandVM.SummaryTitle) && MotionEnabled)
            SummaryContent.BeginAnimation(OpacityProperty, new DoubleAnimation(.55, 1, TimeSpan.FromMilliseconds(130)));
    }
    public void AnimateContent()
    {
        if (!MotionEnabled) return;
        var duration = TimeSpan.FromMilliseconds(160);
        ExpandedBody.BeginAnimation(OpacityProperty, new DoubleAnimation(.45, 1, duration));
        ((TranslateTransform)ExpandedBody.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-4, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}
