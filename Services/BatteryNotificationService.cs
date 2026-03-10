using System;
using System.Collections.Generic;

namespace Woobly.Services
{
    /// <summary>
    /// Monitors battery state changes and fires notifications for:
    ///  - Charger connected or disconnected
    ///  - Battery falling below 30%, 20%, or 10% thresholds (once per discharge cycle)
    /// Call <see cref="Check"/> every second from the UI timer.
    /// </summary>
    public class BatteryNotificationService
    {
        /// <summary>Fires with (icon, message) when a battery event occurs.</summary>
        public event Action<string, string>? NotificationTriggered;

        private bool _wasCharging;
        private bool _initialized;
        private readonly HashSet<int> _alertedThresholds = new();

        private static readonly int[] Thresholds = { 30, 20, 10 };

        public void Check(int batteryPercent, bool isCharging)
        {
            // Skip the very first call — just capture the baseline state
            if (!_initialized)
            {
                _wasCharging = isCharging;
                _initialized = true;
                return;
            }

            // Detect charger plug/unplug transition
            if (isCharging != _wasCharging)
            {
                _wasCharging = isCharging;

                if (isCharging)
                {
                    NotificationTriggered?.Invoke("\uE83E", $"Charging \u00B7 {batteryPercent}%");
                    // Clear thresholds so user gets fresh low-battery alerts next discharge
                    _alertedThresholds.Clear();
                }
                else
                {
                    NotificationTriggered?.Invoke("\uE996", $"Unplugged \u00B7 {batteryPercent}%");
                    // Pre-mark thresholds already crossed so we don't spam them on next tick
                    foreach (var t in Thresholds)
                        if (batteryPercent <= t)
                            _alertedThresholds.Add(t);
                }

                // Only one notification per tick
                return;
            }

            // Low battery alerts (only while discharging)
            if (!isCharging)
            {
                foreach (var threshold in Thresholds)
                {
                    if (batteryPercent <= threshold && !_alertedThresholds.Contains(threshold))
                    {
                        _alertedThresholds.Add(threshold);
                        NotificationTriggered?.Invoke("🔋", $"Low Battery · {batteryPercent}%");
                        break; // Fire one threshold alert per tick; others fire on subsequent ticks
                    }
                }
            }
        }
    }
}
