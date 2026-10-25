using System.IO;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace NI.Agent.Files;

public sealed record FileStamp(long Length, long ModifiedTicks, long CreatedTicks, string Hash, string? NativeId);
public static class PathGuard
{
    public static string Child(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Contains('\\') || relative.Contains('/')
            || relative is "." or ".." || relative.EndsWith('.') || relative.EndsWith(' ') || relative.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new AgentException("UNSAFE_PATH", "Dosya/klasör adı güvenli kapsam dışında.");
        var stem = relative.Split('.')[0].ToUpperInvariant();
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem))
            throw new AgentException("UNSAFE_PATH", "Windows aygıt adı reddedildi.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        var rel = Path.GetRelativePath(Path.GetFullPath(root), path);
        if (Path.IsPathRooted(rel) || rel.Split(Path.DirectorySeparatorChar).Any(x => x == ".."))
            throw new AgentException("PATH_ESCAPE", "Kapsam dışına çıkan yol reddedildi.");
        RejectLinks(path);
        return path;
    }
    public static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new AgentException("REPARSE_POINT", "Symlink, junction veya cloud placeholder otomatik işleme alınmaz.");
            }
            else
            {
                // A dangling symlink may not satisfy File.Exists.
                var info = new FileInfo(current);
                if (info.LinkTarget != null) throw new AgentException("REPARSE_POINT", "Bağlantı yolu reddedildi.");
            }
            if (Path.GetPathRoot(current) == current) break;
        }
    }
    public sealed class DirectoryLease : IDisposable
    {
        private readonly List<SafeFileHandle> _pins = new();
        internal DirectoryLease(string path)
        {
            try
            {
                RejectLinks(path);
                if (OperatingSystem.IsWindows()) foreach (var parent in Ancestors(path))
                {
                    var pin = CreateFileW(parent, 0x80, 0x1 | 0x2, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
                    if (pin.IsInvalid) { pin.Dispose(); throw new IOException("Kapsam klasörü sabitlenemedi."); }
                    _pins.Add(pin); RejectLinks(parent);
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var pin in _pins) pin.Dispose(); _pins.Clear(); }
    }
    public sealed class PinnedRead : IDisposable
    {
        private readonly DirectoryLease _lease;
        public FileStream Stream { get; }
        internal PinnedRead(string path)
        {
            _lease = new DirectoryLease(Path.GetDirectoryName(path)!);
            try
            {
                RejectLinks(path);
                if (OperatingSystem.IsWindows())
                {
                    var handle = CreateFileW(path, 0x80000000, 0x1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
                    if (handle.IsInvalid) { handle.Dispose(); throw new IOException("Dosya açılamadı veya kilitli."); }
                    try
                    {
                        if (!GetFileInformationByHandle(handle, out var information) || (information.Attributes & 0x410) != 0)
                            throw new AgentException("REPARSE_POINT", "Dosya türü/kimliği doğrulanamadı.");
                        var final = new StringBuilder(32768);
                        if (GetFinalPathNameByHandleW(handle, final, (uint)final.Capacity, 0) == 0) throw new IOException("Dosya yolu doğrulanamadı.");
                        var resolved = final.ToString();
                        if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) resolved = @"\\" + resolved[8..];
                        else if (resolved.StartsWith(@"\\?\", StringComparison.Ordinal)) resolved = resolved[4..];
                        if (!Path.GetFullPath(path).Equals(resolved, StringComparison.OrdinalIgnoreCase)) throw new AgentException("PATH_ESCAPE", "Açılan dosya izinli yolla eşleşmiyor.");
                        Stream = new FileStream(handle, FileAccess.Read, 65536, false);
                    }
                    catch { handle.Dispose(); throw; }
                }
                else Stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch { _lease.Dispose(); throw; }
        }
        public void Dispose() { Stream.Dispose(); _lease.Dispose(); }
    }
    public static DirectoryLease PinDirectory(string path) => new(path);
    public static PinnedRead OpenReadPinned(string path) => new(path);
    public static FileStamp Stamp(string path, CancellationToken token)
    {
        using var read = OpenReadPinned(path);
        return Stamp(read.Stream, path, token);
    }
    private static FileStamp Stamp(FileStream stream, string path, CancellationToken token)
    {
        stream.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
            var info = new FileInfo(path);
            string? native = null;
            if (OperatingSystem.IsWindows())
            {
                if (!GetFileInformationByHandle(stream.SafeFileHandle, out var data))
                    throw new AgentException("IDENTITY_UNAVAILABLE", "Windows dosya kimliği doğrulanamadı.");
                if ((data.Attributes & 0x410) != 0) throw new AgentException("REPARSE_POINT", "Reparse/dizin kaynağı reddedildi.");
                native = $"{data.Volume:X8}:{data.IndexHigh:X8}{data.IndexLow:X8}";
            }
            return new(stream.Length, info.LastWriteTimeUtc.Ticks, info.CreationTimeUtc.Ticks, Convert.ToHexString(hash.GetHashAndReset()), native);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
    public static void MoveVerified(string source, string destination, FileStamp expected, CancellationToken token)
    {
        RejectLinks(source); RejectLinks(destination);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new AgentException("DESTINATION_EXISTS", "Hedef zaten var; üzerine yazılmadı.");
        if (!OperatingSystem.IsWindows())
        {
            if (Stamp(source, token) != expected) throw new AgentException("SOURCE_CHANGED", "Plan sonrasında değişen kaynak atlandı.");
            token.ThrowIfCancellationRequested();
            RejectLinks(source); RejectLinks(destination);
            File.Move(source, destination, overwrite: false);
            // Unix fixture adapter is not claimed to prove Windows race resistance.
            return;
        }
        // Pin every ancestor with no delete-sharing, then rename the open source handle.
        // This avoids a path-based check/move race against junction/ancestor replacement.
        var pins = new List<SafeFileHandle>();
        try
        {
            foreach (var parent in Ancestors(Path.GetDirectoryName(source)!).Concat(Ancestors(Path.GetDirectoryName(destination)!)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var handle = CreateFileW(parent, 0x80, 0x1 | 0x2, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
                if (handle.IsInvalid) { handle.Dispose(); throw new IOException("Klasör kilitlenemedi."); }
                pins.Add(handle);
                RejectLinks(parent);
            }
            using var sourceHandle = CreateFileW(source, 0x80000000 | 0x00010000, 0x1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
            if (sourceHandle.IsInvalid) throw new IOException("Kaynak kilitli veya erişilemiyor.");
            using var stream = new FileStream(sourceHandle, FileAccess.Read, 65536, false);
            if (Stamp(stream, source, token) != expected) throw new AgentException("SOURCE_CHANGED", "Kaynağın kimliği/içeriği değişti; taşınmadı.");
            token.ThrowIfCancellationRequested();
            var pathBytes = Encoding.Unicode.GetBytes(Path.GetFullPath(destination));
            var rootOffset = IntPtr.Size == 8 ? 8 : 4;
            var lengthOffset = rootOffset + IntPtr.Size;
            var nameOffset = lengthOffset + 4;
            // Win32's rename normalization may read a terminating WCHAR even though
            // FileNameLength excludes it. An unterminated buffer produced random suffixes.
            var renameSize = nameOffset + pathBytes.Length + sizeof(char);
            var rename = Marshal.AllocHGlobal(renameSize);
            try
            {
                for (var i = 0; i < renameSize; i++) Marshal.WriteByte(rename, i, 0); // ReplaceIfExists=false; terminated filename
                Marshal.WriteIntPtr(rename, rootOffset, IntPtr.Zero);
                Marshal.WriteInt32(rename, lengthOffset, pathBytes.Length);
                Marshal.Copy(pathBytes, 0, rename + nameOffset, pathBytes.Length);
                if (!SetFileInformationByHandle(sourceHandle, 3, rename, (uint)renameSize))
                    throw new IOException("Aynı dosya sistemi içinde güvenli taşıma başarısız: " + Marshal.GetLastWin32Error());
                var final = new StringBuilder(32768);
                var count = GetFinalPathNameByHandleW(sourceHandle, final, (uint)final.Capacity, 0);
                var resolved = final.ToString();
                if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) resolved = @"\\" + resolved[8..];
                else if (resolved.StartsWith(@"\\?\", StringComparison.Ordinal)) resolved = resolved[4..];
                if (count == 0 || !Path.GetFullPath(destination).Equals(resolved, StringComparison.OrdinalIgnoreCase))
                {
                    var error = new IOException("Taşımanın hedefi doğrulanamadı; günlük uzlaştırması gerekli.");
                    error.Data["ExpectedPath"] = Path.GetFullPath(destination); error.Data["ResolvedPath"] = resolved;
                    throw error;
                }
            }
            finally { Marshal.FreeHGlobal(rename); }
        }
        finally { foreach (var pin in pins) pin.Dispose(); }
    }
    public static void CreateDirectoryUnder(string root, string name)
    {
        var pins = new List<SafeFileHandle>();
        try
        {
            if (OperatingSystem.IsWindows())
                foreach (var parent in Ancestors(root))
                {
                    var pin = CreateFileW(parent, 0x80, 0x1 | 0x2, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
                    if (pin.IsInvalid) { pin.Dispose(); throw new IOException("Klasör sabitlenemedi."); }
                    pins.Add(pin); RejectLinks(parent);
                }
            var target = Child(root, name);
            Directory.CreateDirectory(target); RejectLinks(target);
        }
        finally { foreach (var pin in pins) pin.Dispose(); }
    }
    private static IEnumerable<string> Ancestors(string path)
    {
        var stack = new Stack<string>();
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current)) stack.Push(current);
        return stack;
    }
    [StructLayout(LayoutKind.Sequential)] private struct HandleInfo
    {
        public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll")] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out HandleInfo information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint sharing, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, IntPtr information, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
}
