using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Woobly.Models;

namespace Woobly.Services
{
    public class StorageService
    {
        private readonly string _dataFolder;
        private readonly string _settingsFile;
        private readonly string _tasksFile;
        private readonly string _appSettingsFile;

        public StorageService()
        {
            _dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Woobly"
            );
            _settingsFile = Path.Combine(_dataFolder, "settings.json");
            _tasksFile = Path.Combine(_dataFolder, "tasks.json");
            _appSettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);
        }

        public AppSettings LoadSettings()
        {
            var settings = new AppSettings();
            
            // Try to load OpenWeather API key from appsettings.json (developer config)
            try
            {
                if (File.Exists(_appSettingsFile))
                {
                    var appSettingsJson = File.ReadAllText(_appSettingsFile);
                    var appSettings = JsonConvert.DeserializeObject<Dictionary<string, string>>(appSettingsJson);
                    if (appSettings != null && appSettings.ContainsKey("OpenWeatherApiKey"))
                    {
                        settings.OpenWeatherApiKey = appSettings["OpenWeatherApiKey"];
                    }
                }
            }
            catch { }
            
            // Load user settings (overrides defaults)
            try
            {
                if (File.Exists(_settingsFile))
                {
                    var json = File.ReadAllText(_settingsFile);
                    var userSettings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (userSettings != null)
                    {
                        // Merge settings, user settings take precedence
                        if (!string.IsNullOrWhiteSpace(userSettings.AIProvider))
                            settings.AIProvider = userSettings.AIProvider;

                        if (!string.IsNullOrWhiteSpace(userSettings.AIApiKey))
                            settings.AIApiKey = userSettings.AIApiKey;
                        else if (!string.IsNullOrWhiteSpace(userSettings.OpenRouterApiKey))
                            settings.AIApiKey = userSettings.OpenRouterApiKey;

                        if (!string.IsNullOrWhiteSpace(userSettings.AIModel))
                            settings.AIModel = userSettings.AIModel;
                        else if (!string.IsNullOrWhiteSpace(userSettings.OpenRouterModel))
                            settings.AIModel = userSettings.OpenRouterModel;

                        if (!string.IsNullOrWhiteSpace(userSettings.OpenRouterApiKey))
                            settings.OpenRouterApiKey = userSettings.OpenRouterApiKey;
                        if (!string.IsNullOrWhiteSpace(userSettings.OpenRouterModel))
                            settings.OpenRouterModel = userSettings.OpenRouterModel;
                        if (!string.IsNullOrWhiteSpace(userSettings.City))
                            settings.City = userSettings.City;
                        // Keep weather API key from appsettings unless user overrides
                        if (!string.IsNullOrWhiteSpace(userSettings.OpenWeatherApiKey))
                            settings.OpenWeatherApiKey = userSettings.OpenWeatherApiKey;
                        
                        settings.ClipboardHistoryLimit = userSettings.ClipboardHistoryLimit > 0
                            ? userSettings.ClipboardHistoryLimit
                            : settings.ClipboardHistoryLimit;
                        settings.RunOnStartup = userSettings.RunOnStartup;
                        settings.IslandWidth = userSettings.IslandWidth;
                        settings.IslandHeight = userSettings.IslandHeight;
                        settings.ExpandedWidth = userSettings.ExpandedWidth;
                        settings.ExpandedHeight = userSettings.ExpandedHeight;
                        settings.AccentColor = userSettings.AccentColor;
                        settings.AnimationDuration = userSettings.AnimationDuration;
                        settings.IdleTimeout = userSettings.IdleTimeout;
                        settings.IgnorePointerWhenInactive = userSettings.IgnorePointerWhenInactive;
                    }
                }
            }
            catch { }
            
            return settings;
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsFile, json);
            }
            catch { }
        }

        public List<TaskItem> LoadTasks()
        {
            try
            {
                if (File.Exists(_tasksFile))
                {
                    var json = File.ReadAllText(_tasksFile);
                    return JsonConvert.DeserializeObject<List<TaskItem>>(json) ?? new List<TaskItem>();
                }
            }
            catch { }
            return new List<TaskItem>();
        }

        public void SaveTasks(IEnumerable<TaskItem> tasks)
        {
            try
            {
                var json = JsonConvert.SerializeObject(tasks, Formatting.Indented);
                File.WriteAllText(_tasksFile, json);
            }
            catch { }
        }
    }
}
