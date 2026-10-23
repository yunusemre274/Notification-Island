using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NI.Agent;

// Small versioned records; no resident database engine or long-lived file handles.
public static class DurableStore
{
    public static T Read<T>(string path, int maxBytes = 2_000_000)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > maxBytes) throw new InvalidDataException("Kalıcı kayıt boyut sınırını aşıyor.");
        return JsonSerializer.Deserialize<T>(stream, AgentJson.Options) ?? throw new InvalidDataException("Boş kalıcı kayıt.");
    }
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, value, AgentJson.Options); stream.Flush(flushToDisk: true); }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static FileStream Lock(string directory)
    {
        Directory.CreateDirectory(directory);
        for (var attempt = 0; ; attempt++)
        {
            try { return new FileStream(Path.Combine(directory, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 20) { Thread.Sleep(10); }
        }
    }
    public static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string IdPath(string directory, string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Geçersiz işlem kimliği.");
        return Path.Combine(directory, id);
    }
}
