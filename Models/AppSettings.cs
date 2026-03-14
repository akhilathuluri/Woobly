using System;

namespace Woobly.Models
{
    public class AppSettings
    {
        public string AIProvider { get; set; } = "OpenRouter";
        public string? AIApiKey { get; set; }
        public string AIModel { get; set; } = "openai/gpt-3.5-turbo";

        // Legacy fields retained for backward compatibility with existing settings files.
        public string? OpenRouterApiKey { get; set; }
        public string OpenRouterModel { get; set; } = "openai/gpt-3.5-turbo";
        public string? OpenWeatherApiKey { get; set; }
        public string City { get; set; } = "London";
        public int ClipboardHistoryLimit { get; set; } = 2;
        public bool RunOnStartup { get; set; } = false;
        public bool HasCompletedPrivacyConsent { get; set; } = false;
        public bool EnableClipboardMonitoring { get; set; } = false;
        public bool EnableCallMonitoring { get; set; } = false;
        
        public double IslandWidth { get; set; } = 150;
        public double IslandHeight { get; set; } = 40;
        public double ExpandedWidth { get; set; } = 400;
        public double ExpandedHeight { get; set; } = 200;
        
        public string AccentColor { get; set; } = "#FF1E1E1E";
        public double AnimationDuration { get; set; } = 0.3;
        public double IdleTimeout { get; set; } = 3.0;
        public bool IgnorePointerWhenInactive { get; set; } = false;
    }
}
