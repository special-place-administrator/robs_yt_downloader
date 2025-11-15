using System;
using System.IO;
using Newtonsoft.Json;
using RobsYTDownloader.Models;

namespace RobsYTDownloader.Services
{
    public class ConfigManager
    {
        private readonly string _configFolder;
        private readonly string _configFilePath;

        public ConfigManager()
        {
            _configFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobsYTDownloader");
            _configFilePath = Path.Combine(_configFolder, "config.json");

            EnsureConfigFolderExists();
        }

        public string ConfigFolder => _configFolder;

        private void EnsureConfigFolderExists()
        {
            if (!Directory.Exists(_configFolder))
            {
                Directory.CreateDirectory(_configFolder);
            }
        }

        public AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
            }

            return new AppConfig();
        }

        public void SaveConfig(AppConfig config)
        {
            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save configuration: {ex.Message}", ex);
            }
        }

        public string GetToolsFolder()
        {
            var toolsFolder = Path.Combine(_configFolder, "tools");
            if (!Directory.Exists(toolsFolder))
            {
                Directory.CreateDirectory(toolsFolder);
            }
            return toolsFolder;
        }

        public string GetCookiesFilePath()
        {
            return Path.Combine(_configFolder, "cookies.txt");
        }
    }
}
