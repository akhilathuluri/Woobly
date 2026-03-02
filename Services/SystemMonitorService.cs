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
            catch
            {
                _cpuCounter = null;
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
            catch
            {
                info.BatteryPercentage = 100;
                info.IsCharging = false;
            }

            try
            {
                info.IsNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                info.IsNetworkAvailable = false;
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
            catch
            {
                info.CpuUsage = null;
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
            catch
            {
                info.BatteryPercentage = 100;
                info.IsCharging = false;
            }

            try
            {
                info.IsNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                info.IsNetworkAvailable = false;
            }

            try
            {
                if (_cpuCounter != null)
                {
                    var value = _cpuCounter.NextValue();
                    info.CpuUsage = Math.Round(value, 0);
                }
            }
            catch
            {
                info.CpuUsage = null;
            }

            return info;
        }
    }
}
