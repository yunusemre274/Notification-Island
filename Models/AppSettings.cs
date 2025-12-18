using System;
using System.IO;
using System.Text.Json;

namespace NI.Models
{
    public class AppSettings
    {
        public string Birthday { get; set; } = "2004-12-06";
        public bool ShowNationalEvents { get; set; } = true;
        public bool ShowGlobalEvents { get; set; } = true;
        public bool IdleMessagesEnabled { get; set; } = true;
        public bool AutostartEnabled { get; set; } = false;
        public bool SoundEnabled { get; set; } = true;

        // NEW: Feature flags
        public bool EnableMediaControls { get; set; } = true;
        public bool EnableSystemMonitor { get; set; } = true;
        public bool EnableAiAssistant { get; set; } = true;

        // NEW: AI settings
        public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "llama2";


        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NotificationIsland");
        
        private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                    Directory.CreateDirectory(SettingsFolder);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }

        public DateTime? GetBirthdayDate()
        {
            if (DateTime.TryParse(Birthday, out var date))
                return date;
            return null;
        }
    }
}
