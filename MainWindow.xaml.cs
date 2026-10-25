using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
    private readonly DispatcherTimer _hoverOpen;
    private bool _ready, _geometryQueued;
    private IntPtr _windowHandle;
    public MainWindow() : this(CreateViewModel()) { }
    private static IslandVM CreateViewModel()
    {
        var settings = AppSettings.Load();
        return new IslandVM(settings, new WindowsNotificationProvider(), new SystemMonitorService(),
            new MediaSessionService(), new NotificationActionHandler());
    }
    public MainWindow(IslandVM viewModel, bool startServices = true)
    {
        _vm = viewModel;
        DataContext = _vm;
        InitializeComponent();
        SetCurrentValue(WidthProperty, _vm.PanelWidth);
        // Keep WPF's hardware acceleration. Do not force software rendering.
        _hoverOpen = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _hoverOpen.Tick += OnHoverOpen;
        SourceInitialized += OnSourceInitialized;
        if (startServices) Loaded += OnLoaded;
        Closed += OnClosed;
        SizeChanged += OnSizeChanged;
        DpiChanged += (_, _) => PositionWindow();
        PreviewKeyDown += OnKeyDown;
        MouseEnter += (_, _) => { if (_vm.Appearance.ExpandOnHover && !_vm.IsExpanded && !_vm.IsTaskInputOpen) _hoverOpen.Start(); };
        MouseLeave += (_, _) => _hoverOpen.Stop();
        // No close timer or Deactivated handler. Arrow, Escape and notification activation
        // are the complete closing policy; slider/focus/popup interactions keep the panel open.
        if (startServices) Activated += OnActivated;
        _vm.PropertyChanged += OnViewModelChanged;
        _vm.Appearance.PropertyChanged += OnAppearanceChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _ready = true;
        var handle = new WindowInteropHelper(this).Handle;
        _windowHandle = handle;
        DesktopInstance.RegisterWindow(handle);
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
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
        WindowMaterialService.ApplyCorners(this, _vm.WindowCornerRadius);
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
        QueueWindowGeometry();
    }
    private void QueueWindowGeometry()
    {
        if (_geometryQueued) return;
        _geometryQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _geometryQueued = false;
            if (!_ready) return;
            PositionWindow();
            WindowMaterialService.ApplyCorners(this, _vm.WindowCornerRadius);
        }));
    }
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        // This adapter applies geometry from the VM, with one owner for the native region.
        // Position and clipping wait for layout so native resize messages cannot restore an old size.
        if (e.PropertyName == nameof(IslandVM.PanelWidth))
        {
            SetCurrentValue(WidthProperty, Math.Min(_vm.PanelWidth, MaxWidth));
            QueueWindowGeometry(); return;
        }
        if (e.PropertyName != nameof(IslandVM.Mode)) return;
        _hoverOpen.Stop();
        if (_vm.IsExpanded) Island.AnimateContent();
        if (_vm.IsTaskInputOpen) Activate();
        QueueWindowGeometry();
    }
    private void OnAppearanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Theme" or "Material" or "SurfaceOpacity" or "CornerRadius") ApplyAppearance();
        else QueueWindowGeometry();
    }
    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(ApplyAppearance));
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (_vm.IsSettingsOpen) _vm.IsSettingsOpen = false;
        else if (_vm.IsTaskInputOpen) _vm.IsTaskInputOpen = false;
        else _vm.IsExpanded = false;
        e.Handled = true;
    }
    private void OnHoverOpen(object? sender, EventArgs e) { _hoverOpen.Stop(); if (IsMouseOver && !_vm.IsTaskInputOpen) _vm.IsExpanded = true; }
    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (DesktopInstance.ActivationMessage != 0 && message == DesktopInstance.ActivationMessage)
        {
            handled = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_ready) return;
                Show(); WindowState = WindowState.Normal; PositionWindow(); Activate();
            }));
            return IntPtr.Zero;
        }
        if (message == 0x007E || message == 0x02E0) Dispatcher.BeginInvoke(new Action(PositionWindow)); // Display / DPI change
        return IntPtr.Zero;
    }
    private void OnClosed(object? sender, EventArgs e)
    {
        if (_windowHandle != IntPtr.Zero) DesktopInstance.UnregisterWindow(_windowHandle);
        _windowHandle = IntPtr.Zero;
        _ready = false;
        _hoverOpen.Stop();
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        _vm.Appearance.PropertyChanged -= OnAppearanceChanged;
        _vm.PropertyChanged -= OnViewModelChanged;
        _vm.Dispose();
    }
}
