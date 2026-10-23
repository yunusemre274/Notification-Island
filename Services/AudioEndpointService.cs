using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace NI.Services;

// Event-driven Core Audio adapter, including volume changes made outside the island.
public sealed class AudioEndpointService : IMMNotificationClient, IDisposable
{
    private readonly object _sync = new();
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    public event EventHandler? Changed;
    public bool IsAvailable { get { lock (_sync) return _device != null; } }
    public int Volume
    {
        get { lock (_sync) { try { return (int)Math.Round((_device?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0) * 100); } catch { return 0; } } }
        set { lock (_sync) { try { if (_device != null) _device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0, 100) / 100f; } catch (Exception ex) { Debug.WriteLine(ex.Message); } } }
    }
    public bool IsMuted
    {
        get { lock (_sync) { try { return _device?.AudioEndpointVolume.Mute ?? false; } catch { return false; } } }
        set { lock (_sync) { try { if (_device != null) _device.AudioEndpointVolume.Mute = value; } catch (Exception ex) { Debug.WriteLine(ex.Message); } } }
    }
    public void Initialize()
    {
        lock (_sync)
        {
            try
            {
                _enumerator = new MMDeviceEnumerator();
                _enumerator.RegisterEndpointNotificationCallback(this);
                Refresh();
            }
            catch (Exception ex) { Debug.WriteLine($"Audio unavailable: {ex.Message}"); }
        }
    }
    private void Refresh()
    {
        lock (_sync)
        {
            if (_device != null) { _device.AudioEndpointVolume.OnVolumeNotification -= OnVolume; _device.Dispose(); _device = null; }
            try
            {
                _device = _enumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (_device != null) _device.AudioEndpointVolume.OnVolumeNotification += OnVolume;
            }
            catch { /* No render endpoint is a valid state. */ }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private void OnVolume(AudioVolumeNotificationData data) => Changed?.Invoke(this, EventArgs.Empty);
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) { if (flow == DataFlow.Render && role == Role.Multimedia) Refresh(); }
    public void OnDeviceStateChanged(string deviceId, DeviceState state) => Refresh();
    public void OnDeviceAdded(string deviceId) => Refresh();
    public void OnDeviceRemoved(string deviceId) => Refresh();
    public void OnPropertyValueChanged(string deviceId, PropertyKey key) { }
    public void Dispose()
    {
        lock (_sync)
        {
            _enumerator?.UnregisterEndpointNotificationCallback(this);
            if (_device != null) { _device.AudioEndpointVolume.OnVolumeNotification -= OnVolume; _device.Dispose(); _device = null; }
            _enumerator?.Dispose(); _enumerator = null;
        }
    }
}
