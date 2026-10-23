using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NI.Configuration;
using NI.Agent;

namespace NI.Models;

public sealed class AppSettings
{
    public AgentOptions Agent { get; set; } = new();
    public int SchemaVersion { get; set; } = 2;
    public AppearanceSettings Appearance { get; set; } = new();
    public string WeatherLatitude { get; set; } = "";
    public string WeatherLongitude { get; set; } = "";
    // Preserve legacy preferences when upgrading an existing settings file.
    public string Birthday { get; set; } = "2004-12-06";
    public bool ShowNationalEvents { get; set; } = true;
    public bool ShowGlobalEvents { get; set; } = true;
    public bool IdleMessagesEnabled { get; set; } = true;
    public bool AutostartEnabled { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool EnableMediaControls { get; set; } = true;
    public bool EnableSystemMonitor { get; set; } = true;
    public bool EnableAiAssistant { get; set; } = true;
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama2";
    [JsonIgnore] public string? LoadWarning { get; private set; }
    private string? _storagePath;
    private bool _canSave = true;
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotificationIsland", "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public AppSettings() { }
    // Alternate storage is useful for isolated tests and portable hosts.
    public AppSettings(string storagePath) => _storagePath = Path.GetFullPath(storagePath);

    public static AppSettings Load(string? storagePath = null)
    {
        var path = Path.GetFullPath(storagePath ?? DefaultPath);
        try
        {
            if (!File.Exists(path)) return new AppSettings(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
            settings.Appearance ??= new AppearanceSettings();
            settings.Agent ??= new AgentOptions();
            settings.SchemaVersion = 2;
            settings._storagePath = path;
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Settings load: {ex.Message}");
            var fallback = new AppSettings(path) { LoadWarning = "Ayarlar okunamadı; varsayılan görünüm kullanılıyor." };
            // Preserve the original before allowing a later save to replace malformed JSON.
            try { File.Copy(path, path + $".invalid-{DateTime.UtcNow.Ticks}.json"); }
            catch { fallback._canSave = false; }
            return fallback;
        }
    }
    public bool Save()
    {
        if (!_canSave) return false;
        try
        {
            var path = _storagePath ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Settings save: {ex.Message}");
            return false;
        }
    }
    public DateTime? GetBirthdayDate() => DateTime.TryParse(Birthday, out var date) ? date : null;
}
