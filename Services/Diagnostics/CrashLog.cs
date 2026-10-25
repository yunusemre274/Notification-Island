using System.IO;

namespace NI.Services.Diagnostics;

public static class CrashLog
{
    private static readonly object Gate = new();
    public static string DirectoryPath => Environment.GetEnvironmentVariable("NI_LOG_DIRECTORY") is { Length: > 0 } configured
        ? Path.GetFullPath(configured)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotificationIsland", "logs");
    public static void Write(string context, Exception exception)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                var path = Path.Combine(DirectoryPath, "crash.log");
                if (File.Exists(path) && new FileInfo(path).Length > 262144) File.Move(path, path + ".previous", overwrite: true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} [{context}] {exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException or ArgumentException)
        {
            System.Diagnostics.Trace.TraceError($"Crash logging failed: {logError.GetType().Name}; original: {exception}");
        }
    }
}
