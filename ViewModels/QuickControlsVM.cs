using System.Windows.Input;
using System.Windows.Threading;
using NI.Core;
using NI.Services;
using Windows.System;

namespace NI.ViewModels;

public sealed class QuickControlsVM : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly AudioEndpointService _audio = new();
    private readonly Action<string> _report;
    private readonly DispatcherTimer _brightnessDebounce;
    private bool _disposed, _refreshing, _writingBrightness, _initializing, _initialized;
    private Task? _initialization;
    private bool _wanted;
    private int _volume, _brightness;
    private bool _brightnessAvailable, _audioAvailable, _muted;
    public bool WifiOn => RadioService.IsWifiOn;
    public bool BluetoothOn => RadioService.IsBluetoothOn;
    public bool WifiAvailable => RadioService.IsWifiAvailable;
    public bool BluetoothAvailable => RadioService.IsBluetoothAvailable;
    public string WifiLabel => !WifiAvailable ? "Yok" : WifiOn ? "Açık" : "Kapalı";
    public string BluetoothLabel => !BluetoothAvailable ? "Yok" : BluetoothOn ? "Açık" : "Kapalı";
    public bool AudioAvailable { get => _audioAvailable; private set => Set(ref _audioAvailable, value); }
    public bool BrightnessAvailable { get => _brightnessAvailable; private set { if (Set(ref _brightnessAvailable, value)) Raise(nameof(BrightnessLabel)); } }
    public string BrightnessLabel => BrightnessAvailable ? $"{Brightness}%" : "Desteklenmiyor";
    public string VolumeIcon => _muted ? "Mute" : "Volume";
    public string VolumeLabel => _muted ? "Sessiz" : $"{Volume}%";
    public int Volume
    {
        get => _volume;
        set { if (Set(ref _volume, value)) { if (!_initializing) _audio.Volume = value; Raise(nameof(VolumeLabel)); } }
    }
    public int Brightness
    {
        get => _brightness;
        set
        {
            if (!Set(ref _brightness, value)) return;
            Raise(nameof(BrightnessLabel));
            if (!_initializing) { _brightnessDebounce.Stop(); _brightnessDebounce.Start(); }
        }
    }
    public ICommand WifiCommand { get; }
    public ICommand BluetoothCommand { get; }
    public ICommand AirplaneCommand { get; }
    public ICommand MuteCommand { get; }
    public ICommand SoundSettingsCommand { get; }
    public QuickControlsVM(Action<string> report)
    {
        _report = report;
        _brightnessDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _brightnessDebounce.Tick += OnBrightness;
        WifiCommand = new AsyncCommand(async _ =>
        {
            if (!await RadioService.ToggleWifiAsync()) await Launcher.LaunchUriAsync(new Uri("ms-settings:network-wifi"));
            RadioChanged(null, EventArgs.Empty);
        }, ex => _report(ex.Message));
        BluetoothCommand = new AsyncCommand(async _ =>
        {
            if (!await RadioService.ToggleBluetoothAsync()) await Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"));
            RadioChanged(null, EventArgs.Empty);
        }, ex => _report(ex.Message));
        // Airplane mode has no supported public desktop toggle API. Open the real system control.
        AirplaneCommand = new AsyncCommand(async _ => await Launcher.LaunchUriAsync(new Uri("ms-settings:network-airplanemode")), ex => _report(ex.Message));
        SoundSettingsCommand = new AsyncCommand(async _ => await Launcher.LaunchUriAsync(new Uri("ms-settings:sound")), ex => _report(ex.Message));
        MuteCommand = new RelayCommand(_ => { _audio.IsMuted = !_audio.IsMuted; RefreshAudio(); });
    }
    public Task InitializeAsync()
    {
        _wanted = true;
        return _initialization ??= InitializeCoreAsync();
    }
    private async Task InitializeCoreAsync()
    {
        if (_disposed) return;
        _audio.Changed += AudioChanged;
        _audio.Initialize(); RefreshAudio();
        RadioService.WifiStateChanged += RadioChanged; RadioService.BluetoothStateChanged += RadioChanged;
        await RadioService.InitializeAsync();
        if (!_disposed && _wanted) { _initialized = true; RadioChanged(null, EventArgs.Empty); }
        else ReleaseResources();
    }
    public async Task RefreshAsync()
    {
        if (_refreshing || _disposed || !_initialized) return; _refreshing = true;
        try
        {
            var result = await Task.Run(() => { var supported = BrightnessService.TryGetBrightness(out var value); return (supported, value); });
            if (_disposed) return;
            _initializing = true;
            BrightnessAvailable = result.supported; Brightness = result.value;
            _initializing = false;
            RefreshAudio();
        }
        finally { _refreshing = false; }
    }
    private async void OnBrightness(object? sender, EventArgs e)
    {
        _brightnessDebounce.Stop();
        if (_writingBrightness || _disposed) return;
        _writingBrightness = true;
        var requested = Brightness;
        try
        {
            if (!await Task.Run(() => BrightnessService.TrySetBrightness(requested)) && !_disposed)
            { BrightnessAvailable = false; _report("Ekran parlaklığı değiştirilemedi. DDC/CI veya dahili ekran desteğini kontrol edin."); }
        }
        finally
        {
            _writingBrightness = false;
            if (!_disposed && requested != Brightness) _brightnessDebounce.Start();
        }
    }
    private void AudioChanged(object? sender, EventArgs e) => OnUi(RefreshAudio);
    private void RefreshAudio()
    {
        _initializing = true;
        Volume = _audio.Volume; AudioAvailable = _audio.IsAvailable; _muted = _audio.IsMuted;
        _initializing = false; Raise(nameof(VolumeIcon)); Raise(nameof(VolumeLabel));
    }
    private void RadioChanged(object? sender, EventArgs e) => OnUi(() =>
    {
        foreach (var name in new[] { nameof(WifiOn), nameof(BluetoothOn), nameof(WifiAvailable), nameof(BluetoothAvailable), nameof(WifiLabel), nameof(BluetoothLabel) }) Raise(name);
    });
    private void OnUi(Action action)
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        if (_dispatcher.CheckAccess()) action(); else _dispatcher.BeginInvoke(new Action(() => { if (!_disposed) action(); }));
    }
    public bool SetVolumeVerified(int value)
    {
        if (_disposed || value is < 0 or > 100) return false;
        var transient = !_initialized;
        try
        {
            if (transient) _audio.Initialize();
            if (!_audio.IsAvailable) return false;
            _audio.Volume = value; RefreshAudio();
            return Math.Abs(_audio.Volume - value) <= 1;
        }
        finally { if (transient) _audio.Dispose(); }
    }
    public void Suspend()
    {
        _wanted = false;
        _brightnessDebounce.Stop();
        if (_initialization?.IsCompleted == false) return;
        ReleaseResources();
    }
    private void ReleaseResources()
    {
        RadioService.WifiStateChanged -= RadioChanged; RadioService.BluetoothStateChanged -= RadioChanged;
        _audio.Changed -= AudioChanged; _audio.Dispose(); RadioService.Release();
        _initialized = false; _initialization = null;
    }
    public void Dispose()
    {
        _disposed = true; _brightnessDebounce.Stop();
        _wanted = false; ReleaseResources();
    }
}
