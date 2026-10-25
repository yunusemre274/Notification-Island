using System.Windows;
using NI.Services.Diagnostics;
using NI.Services.Windowing;

namespace NI;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception) CrashLog.Write("Unhandled background exception", exception);
        };
        TaskScheduler.UnobservedTaskException += (_, e) => { CrashLog.Write("Unobserved task", e.Exception); e.SetObserved(); };
        try { Run(args); }
        catch (Exception ex)
        {
            CrashLog.Write("Startup", ex);
            Environment.ExitCode = 1;
        }
    }
    private static void Run(string[] args)
    {
        // Dispatch before any Application/MainWindow construction; alarm helper is counted in the budget.
        if (args.Length == 3 && args[0] == "--alarm-fire")
        {
            if (Guid.TryParseExact(args[1], "N", out _) && long.TryParse(args[2], out var ticks))
                Environment.ExitCode = NI.Services.Alarms.AlarmHost.FireStandalone(args[1], ticks);
            else Environment.ExitCode = 2;
            return;
        }
        if (args.Length != 0) { Environment.ExitCode = 2; return; }
        RunInteractive();
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RunInteractive()
    {
        using var instance = new System.Threading.Mutex(true, DesktopInstance.MutexName, out var firstInstance);
        if (!firstInstance)
        {
            if (!DesktopInstance.TryActivateExisting())
                throw new InvalidOperationException("Açık Notification Island penceresine ulaşılamadı. Çalışan örneği kapatıp tekrar başlatın.");
            return;
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) =>
        {
            CrashLog.Write("WPF dispatcher", e.Exception);
            e.Handled = true;
            // Do not continue in an unknown UI state or repeatedly open modal error dialogs.
            app.Shutdown(1);
        };
        NI.Themes.ThemeManager.Initialize();
        app.Run(new MainWindow());
    }
}
