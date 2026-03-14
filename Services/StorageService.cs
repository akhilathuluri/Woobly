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
        private readonly ISecretStore _secretStore;
        private const string AISecretKeyPrefix = "ai-provider-key::";

        public StorageService(string? dataFolderOverride = null, ISecretStore? secretStore = null)
        {
            _dataFolder = dataFolderOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Woobly"
            );
            _settingsFile = Path.Combine(_dataFolder, "settings.json");
            _tasksFile = Path.Combine(_dataFolder, "tasks.json");
            _appSettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            _secretStore = secretStore ?? new DpapiSecretStore(Path.Combine(_dataFolder, "secrets"));

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
            catch (Exception ex)
            {
                AppLog.Warn($"Failed to load developer appsettings.json: {ex.Message}");
            }
            
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
                        settings.HasCompletedPrivacyConsent = userSettings.HasCompletedPrivacyConsent;
                        settings.EnableClipboardMonitoring = userSettings.EnableClipboardMonitoring;
                        settings.EnableCallMonitoring = userSettings.EnableCallMonitoring;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Failed to load user settings: {ex.Message}");
            }

            // Load AI API key from DPAPI secure store first; migrate plaintext fallback if present.
            try
            {
                var secretKey = BuildAISecretKey(settings.AIProvider);
                var secureValue = _secretStore.GetSecret(secretKey);
                if (!string.IsNullOrWhiteSpace(secureValue))
                {
                    settings.AIApiKey = secureValue;
                    settings.OpenRouterApiKey = secureValue;
                }
                else if (!string.IsNullOrWhiteSpace(settings.AIApiKey))
                {
                    _secretStore.SetSecret(secretKey, settings.AIApiKey);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Failed to load secure AI key: {ex.Message}");
            }
            
            return settings;
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                // Persist sensitive keys via DPAPI store, not plain JSON.
                var secretKey = BuildAISecretKey(settings.AIProvider);
                if (!string.IsNullOrWhiteSpace(settings.AIApiKey))
                {
                    _secretStore.SetSecret(secretKey, settings.AIApiKey);
                }
                else
                {
                    _secretStore.DeleteSecret(secretKey);
                }

                var safeSettings = CloneWithoutSecrets(settings);
                var json = JsonConvert.SerializeObject(safeSettings, Formatting.Indented);
                File.WriteAllText(_settingsFile, json);
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to save settings", ex);
            }
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
            catch (Exception ex)
            {
                AppLog.Warn($"Failed to load tasks: {ex.Message}");
            }
            return new List<TaskItem>();
        }

        public void SaveTasks(IEnumerable<TaskItem> tasks)
        {
            try
            {
                var json = JsonConvert.SerializeObject(tasks, Formatting.Indented);
                File.WriteAllText(_tasksFile, json);
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to save tasks", ex);
            }
        }

        private static string BuildAISecretKey(string provider)
        {
            var normalized = string.IsNullOrWhiteSpace(provider) ? "OpenRouter" : provider.Trim();
            return AISecretKeyPrefix + normalized;
        }

        private static AppSettings CloneWithoutSecrets(AppSettings settings)
        {
            return new AppSettings
            {
                AIProvider = settings.AIProvider,
                AIApiKey = null,
                AIModel = settings.AIModel,
                OpenRouterApiKey = null,
                OpenRouterModel = settings.OpenRouterModel,
                OpenWeatherApiKey = settings.OpenWeatherApiKey,
                City = settings.City,
                ClipboardHistoryLimit = settings.ClipboardHistoryLimit,
                RunOnStartup = settings.RunOnStartup,
                HasCompletedPrivacyConsent = settings.HasCompletedPrivacyConsent,
                EnableClipboardMonitoring = settings.EnableClipboardMonitoring,
                EnableCallMonitoring = settings.EnableCallMonitoring,
                IslandWidth = settings.IslandWidth,
                IslandHeight = settings.IslandHeight,
                ExpandedWidth = settings.ExpandedWidth,
                ExpandedHeight = settings.ExpandedHeight,
                AccentColor = settings.AccentColor,
                AnimationDuration = settings.AnimationDuration,
                IdleTimeout = settings.IdleTimeout,
                IgnorePointerWhenInactive = settings.IgnorePointerWhenInactive
            };
        }
    }
}
