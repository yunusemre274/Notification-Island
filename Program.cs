using System.Windows;

namespace NI;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
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
        using var instance = new System.Threading.Mutex(true, @"Local\NotificationIsland.Desktop", out var firstInstance);
        if (!firstInstance) return;
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        NI.Themes.ThemeManager.Initialize();
        app.Run(new MainWindow());
    }
}
