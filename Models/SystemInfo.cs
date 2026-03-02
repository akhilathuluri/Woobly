using System;

namespace Woobly.Models
{
    public class SystemInfo
    {
        public DateTime CurrentTime { get; set; }
        public string FormattedTime => CurrentTime.ToString("h:mm tt");
        public string FormattedDate => CurrentTime.ToString("MMM dd, yyyy");
        public int BatteryPercentage { get; set; }
        public bool IsCharging { get; set; }
        public double Temperature { get; set; }
        public string? WeatherCondition { get; set; }
        public string? WeatherIcon { get; set; }
        public double? CpuUsage { get; set; }
        public bool IsNetworkAvailable { get; set; }
    }
}
