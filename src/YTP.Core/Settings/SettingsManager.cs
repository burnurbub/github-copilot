using System;
using System.IO;
using Newtonsoft.Json;

namespace YTP.Core.Settings
{
    public class SettingsManager
    {
        private readonly string _configPath;
        public AppSettings Settings { get; private set; }

        public SettingsManager(string? configPath = null)
        {
            _configPath = configPath ?? GetDefaultConfigPath();
            Settings = LoadSettings();
        }

        private static string GetDefaultConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "YTPDownloader");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "config.json");
        }

        private AppSettings LoadSettings()
        {
            if (!File.Exists(_configPath))
                return new AppSettings();

            try
            {
                var json = File.ReadAllText(_configPath);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }

        public void Update(Action<AppSettings> apply)
        {
            apply(Settings);
            Save();
        }
    }
}
