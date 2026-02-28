using System;
using System.Management;
using System.Threading.Tasks;
using WinForms = System.Windows.Forms;
using Woobly.Models;

namespace Woobly.Services
{
    public class SystemMonitorService
    {
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

            return info;
        }
    }
}
