using System;
using WinForms = System.Windows.Forms;
using Woobly.Models;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace Woobly.Services
{
    public class SystemMonitorService
    {
        private readonly PerformanceCounter? _cpuCounter;

        public SystemMonitorService()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ = _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _cpuCounter = null;
                AppLog.Warn($"CPU counter initialization failed: {ex.Message}");
            }
        }

        public void UpdateSystemInfo(SystemInfo info)
        {
            info.CurrentTime = DateTime.Now;

            try
            {
                var powerStatus = WinForms.SystemInformation.PowerStatus;
                info.BatteryPercentage = (int)(powerStatus.BatteryLifePercent * 100);
                info.IsCharging = powerStatus.PowerLineStatus == WinForms.PowerLineStatus.Online;
            }
            catch (Exception ex)
            {
                info.BatteryPercentage = 100;
                info.IsCharging = false;
                AppLog.Warn($"Battery status update failed: {ex.Message}");
            }

            try
            {
                info.IsNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
            }
            catch (Exception ex)
            {
                info.IsNetworkAvailable = false;
                AppLog.Warn($"Network status update failed: {ex.Message}");
            }

            try
            {
                if (_cpuCounter != null)
                {
                    var value = _cpuCounter.NextValue();
                    info.CpuUsage = Math.Round(value, 0);
                }
                else
                {
                    info.CpuUsage = null;
                }
            }
            catch (Exception ex)
            {
                info.CpuUsage = null;
                AppLog.Warn($"CPU usage update failed: {ex.Message}");
            }
        }

        public SystemInfo GetSystemInfo()
        {
            var info = new SystemInfo
            {
                CurrentTime = DateTime.Now
            };

            try
            {
                var powerStatus = WinForms.SystemInformation.PowerStatus;
                info.BatteryPercentage = (int)(powerStatus.BatteryLifePercent * 100);
                info.IsCharging = powerStatus.PowerLineStatus == WinForms.PowerLineStatus.Online;
            }
            catch (Exception ex)
            {
                info.BatteryPercentage = 100;
                info.IsCharging = false;
                AppLog.Warn($"Battery status read failed: {ex.Message}");
            }

            try
            {
                info.IsNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
            }
            catch (Exception ex)
            {
                info.IsNetworkAvailable = false;
                AppLog.Warn($"Network status read failed: {ex.Message}");
            }

            try
            {
                if (_cpuCounter != null)
                {
                    var value = _cpuCounter.NextValue();
                    info.CpuUsage = Math.Round(value, 0);
                }
            }
            catch (Exception ex)
            {
                info.CpuUsage = null;
                AppLog.Warn($"CPU usage read failed: {ex.Message}");
            }

            return info;
        }
    }
}
