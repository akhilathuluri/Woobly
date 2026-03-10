using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Woobly.Services
{
    /// <summary>
    /// Watches for Bluetooth device connect/disconnect events using the Windows
    /// <see cref="DeviceWatcher"/> API (connection-status selector).
    /// <para>
    /// Unlike WMI creation/deletion events, this API fires correctly when an
    /// already-paired device physically connects or disconnects (e.g. turning
    /// headphones on/off). Covers both classic Bluetooth and BLE.
    /// </para>
    /// Call <see cref="Start"/> once after construction; call <see cref="Dispose"/> on shutdown.
    /// </summary>
    public sealed class BluetoothNotificationService : IDisposable
    {
        /// <summary>Fires with the friendly device name when a Bluetooth device connects.</summary>
        public event Action<string>? DeviceConnected;

        /// <summary>Fires with the friendly device name when a Bluetooth device disconnects.</summary>
        public event Action<string>? DeviceDisconnected;

        private DeviceWatcher? _classicWatcher;
        private DeviceWatcher? _bleWatcher;
        private readonly Dispatcher _dispatcher;

        // Id → name cache so Removed events (which carry no name) can still show readable text.
        private readonly ConcurrentDictionary<string, string> _knownDevices = new();

        public BluetoothNotificationService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>Starts watching. Safe to call once after construction.</summary>
        public void Start()
        {
            // Classic Bluetooth (headphones, speakers, phones, etc.)
            try
            {
                var selector = BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
                _classicWatcher = BuildWatcher(selector);
            }
            catch { }

            // BLE (fitness trackers, modern keyboards/mice, etc.)
            try
            {
                var bleSelector = BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
                _bleWatcher = BuildWatcher(bleSelector);
            }
            catch { }
        }

        private DeviceWatcher BuildWatcher(string selector)
        {
            var watcher = DeviceInformation.CreateWatcher(selector);

            // Gate: suppress Added events during the initial snapshot enumeration so we
            // don’t show “X Connected” for every device that was already connected at startup.
            int initialEnumDone = 0;

            watcher.EnumerationCompleted += (_, _) =>
                Interlocked.Exchange(ref initialEnumDone, 1);

            watcher.Added += (_, info) =>
            {
                var name = string.IsNullOrWhiteSpace(info.Name) ? "Bluetooth Device" : info.Name;
                _knownDevices[info.Id] = name;

                // Skip devices that were already connected when the app launched
                if (Volatile.Read(ref initialEnumDone) == 0) return;

                _dispatcher.BeginInvoke(() => DeviceConnected?.Invoke(name));
            };

            watcher.Removed += (_, info) =>
            {
                _knownDevices.TryRemove(info.Id, out var name);
                _dispatcher.BeginInvoke(() => DeviceDisconnected?.Invoke(name ?? "Bluetooth Device"));
            };

            watcher.Start();
            return watcher;
        }

        public void Dispose()
        {
            StopWatcher(_classicWatcher);
            StopWatcher(_bleWatcher);
        }

        private static void StopWatcher(DeviceWatcher? watcher)
        {
            if (watcher == null) return;
            try
            {
                if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                    watcher.Stop();
            }
            catch { }
        }
    }
}
