using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NI.Models;
using NI.Services;
using NI.Services.Notifications;
using NI.Services.Windowing;
using NI.Themes;
using NI.ViewModels;

namespace NI;

public partial class MainWindow : Window
{
    private readonly IslandVM _vm;
    private readonly DispatcherTimer _hoverOpen, _hoverClose;
    private bool _ready;
    public MainWindow()
    {
        var settings = AppSettings.Load();
        _vm = new IslandVM(settings, new WindowsNotificationProvider(), new SystemMonitorService(),
            new MediaSessionService(), new NotificationActionHandler());
        DataContext = _vm;
        InitializeComponent();
        // Keep WPF's hardware acceleration. Do not force software rendering.
        _hoverOpen = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _hoverClose = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _hoverOpen.Tick += OnHoverOpen; _hoverClose.Tick += OnHoverClose;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded; Closed += OnClosed;
        SizeChanged += OnSizeChanged;
        DpiChanged += (_, _) => PositionWindow();
        PreviewKeyDown += OnKeyDown;
        MouseEnter += (_, _) => { _hoverClose.Stop(); if (_vm.Appearance.ExpandOnHover && !_vm.IsExpanded) _hoverOpen.Start(); };
        MouseLeave += (_, _) => { _hoverOpen.Stop(); if (_vm.Appearance.AutoCollapse) _hoverClose.Start(); };
        Deactivated += (_, _) => { if (_vm.Appearance.AutoCollapse && !_vm.IsSettingsOpen) _vm.IsExpanded = false; };
        Activated += OnActivated;
        _vm.PropertyChanged += OnViewModelChanged;
        _vm.Appearance.PropertyChanged += OnAppearanceChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _ready = true;
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowProc);
        ApplyAppearance(); PositionWindow();
    }
    private async void OnLoaded(object sender, RoutedEventArgs e) => await _vm.StartAsync();
    private async void OnActivated(object? sender, EventArgs e) => await _vm.RefreshAccessAsync();
    private void ApplyAppearance()
    {
        if (!_ready) return;
        var native = WindowMaterialService.Apply(this, _vm.Appearance);
        ThemeManager.Apply(_vm.Appearance, native);
        _vm.SetMaterialStatus(native ? $"{_vm.Appearance.Material} • sistem materyali etkin" : "Sistem materyali kapalı veya desteklenmiyor • opak glass yüzey");
        WindowMaterialService.ApplyCorners(this, _vm.Appearance.CornerRadius);
        PositionWindow();
    }
    private void PositionWindow()
    {
        var work = SystemParameters.WorkArea;
        MaxWidth = Math.Max(320, work.Width - 16);
        MaxHeight = Math.Max(200, work.Height - _vm.Appearance.TopOffset - 8);
        Left = Math.Clamp(work.Left + (work.Width - ActualWidth) / 2 + _vm.Appearance.HorizontalOffset,
            work.Left + 8, Math.Max(work.Left + 8, work.Right - ActualWidth - 8));
        Top = work.Top + _vm.Appearance.TopOffset;
    }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        PositionWindow();
        if (_ready) WindowMaterialService.ApplyCorners(this, _vm.Appearance.CornerRadius);
    }
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IslandVM.IsExpanded)) return;
        PositionWindow();
        if (_vm.IsExpanded) Island.AnimateContent();
        if (!_vm.Appearance.ReduceMotion && SystemParameters.ClientAreaAnimation)
            BeginAnimation(WidthProperty, new DoubleAnimation(ActualWidth, Math.Min(_vm.PanelWidth, MaxWidth), TimeSpan.FromMilliseconds(170))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
    private void OnAppearanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Theme" or "Material" or "SurfaceOpacity" or "CornerRadius") ApplyAppearance();
        else PositionWindow();
    }
    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(ApplyAppearance));
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (_vm.IsSettingsOpen) _vm.IsSettingsOpen = false;
        else if (_vm.IsAssistantOpen) _vm.IsAssistantOpen = false;
        else _vm.IsExpanded = false;
        e.Handled = true;
    }
    private void OnHoverOpen(object? sender, EventArgs e) { _hoverOpen.Stop(); if (IsMouseOver) _vm.IsExpanded = true; }
    private void OnHoverClose(object? sender, EventArgs e)
    {
        _hoverClose.Stop();
        if (!IsMouseOver && !_vm.IsSettingsOpen && !_vm.IsAssistantOpen) _vm.IsExpanded = false;
    }
    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x007E || message == 0x02E0) Dispatcher.BeginInvoke(new Action(PositionWindow)); // Display / DPI change
        return IntPtr.Zero;
    }
    private void OnClosed(object? sender, EventArgs e)
    {
        _hoverOpen.Stop(); _hoverClose.Stop();
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        _vm.Appearance.PropertyChanged -= OnAppearanceChanged;
        _vm.PropertyChanged -= OnViewModelChanged;
        _vm.Dispose();
    }
}
