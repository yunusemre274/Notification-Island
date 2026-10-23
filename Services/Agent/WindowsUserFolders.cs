using System.IO;
using System.Runtime.InteropServices;

namespace NI.Services.Agent;

public static class WindowsUserFolders
{
    public static string Resolve(string scope)
    {
        // SHGetKnownFolderPath follows redirected/OneDrive desktop and localized installations.
        var id = scope switch
        {
            "desktop" => new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641"),
            "downloads" => new Guid("374DE290-123F-4565-9164-39C4925E467B"),
            _ => throw new ArgumentException("Desteklenmeyen kullanıcı klasörü.")
        };
        var result = SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var pointer);
        if (result != 0) Marshal.ThrowExceptionForHR(result);
        try { return Marshal.PtrToStringUni(pointer) ?? throw new IOException("Kullanıcı klasörü çözülemedi."); }
        finally { Marshal.FreeCoTaskMem(pointer); }
    }
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid folder, uint flags, IntPtr token, out IntPtr path);
}
