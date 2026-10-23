using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using NI.Agent.Alarms;

namespace NI.Services.Alarms;

public sealed class AlarmHost : IDisposable
{
    public static string StorageDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotificationIsland", "alarms");
    private static string PipeName { get { using var identity = WindowsIdentity.GetCurrent(); return "NotificationIsland.Alarm." + identity.User!.Value; } }
    private readonly CancellationTokenSource _stop = new();
    private Task? _listener;
    public AlarmService Service { get; } = new(StorageDirectory, new WindowsAlarmScheduler());
    public event EventHandler<AlarmDelivery>? Due;
    public void Start()
    {
        Service.Due += OnDue;
        _listener = ListenAsync();
        _ = Task.Run(() => { try { Service.Reconcile(); } catch (Exception ex) { System.Diagnostics.Trace.TraceWarning($"Alarm reconciliation: {ex.GetType().Name}"); } });
    }
    private void OnDue(object? sender, AlarmDelivery delivery) => Due?.Invoke(this, delivery);
    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 128, 128);
                await pipe.WaitForConnectionAsync(_stop.Token);
                using var readLimit = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); readLimit.CancelAfter(TimeSpan.FromSeconds(2));
                var packet = new byte[24];
                await pipe.ReadExactlyAsync(packet, readLimit.Token);
                var id = new Guid(packet.AsSpan(0, 16)).ToString("N");
                var ticks = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(packet.AsSpan(16));
                var delivery = Service.Fire(id, ticks);
                await pipe.WriteAsync(new byte[] { 1 }, readLimit.Token);
                if (delivery != null) Service.ConfirmPresented(delivery.Id);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { System.Diagnostics.Trace.TraceWarning($"Alarm IPC: {ex.GetType().Name}"); }
        }
    }
    // Called before constructing Application/MainWindow: no WPF surface in the helper path.
    public static int FireStandalone(string id, long ticks)
    {
        if (!Guid.TryParseExact(id, "N", out _) || ticks <= 0) return 2;
        try
        {
            if (SendToRunningAsync(id, ticks).GetAwaiter().GetResult()) return 0;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException) { }

        var service = new AlarmService(StorageDirectory, new WindowsAlarmScheduler());
        var delivery = service.Fire(id, ticks);
        if (delivery == null) return 0;
        MessageBeep(0x40);
        // Interactive-session fallback: no promise about lock-screen sound/waking a sleeping PC.
        MessageBoxW(IntPtr.Zero, (delivery.Missed ? "Kaçırılmış alarm: " : "") + delivery.Title,
            "Notification Island • Alarm", 0x40 | 0x10000);
        service.ConfirmPresented(id);
        return 0;
    }
    private static async Task<bool> SendToRunningAsync(string id, long ticks)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(1000, timeout.Token);
        var packet = new byte[24]; Guid.ParseExact(id, "N").TryWriteBytes(packet);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(packet.AsSpan(16), ticks);
        await pipe.WriteAsync(packet, timeout.Token);
        var ack = new byte[1]; await pipe.ReadExactlyAsync(ack, timeout.Token);
        return ack[0] == 1;
    }
    public static void SignalSound() => MessageBeep(0x40);
    public void Dispose()
    {
        _stop.Cancel(); Service.Due -= OnDue;
        if (_listener != null) _ = _listener.ContinueWith(_ => _stop.Dispose(), TaskScheduler.Default);
        else _stop.Dispose();
    }
    [DllImport("user32.dll")] private static extern bool MessageBeep(uint type);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
