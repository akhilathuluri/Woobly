using System;
using System.Collections.Generic;
using WpfClipboard = System.Windows.Clipboard;
using System.Windows.Threading;
using Woobly.Models;

namespace Woobly.Services
{
    public interface IClipboardAdapter
    {
        bool ContainsText();
        string GetText();
        void SetText(string text);
    }

    public sealed class SystemClipboardAdapter : IClipboardAdapter
    {
        public bool ContainsText() => WpfClipboard.ContainsText();
        public string GetText() => WpfClipboard.GetText();
        public void SetText(string text) => WpfClipboard.SetText(text);
    }

    public class ClipboardService : IDisposable
    {
        private readonly List<ClipboardItem> _items = new List<ClipboardItem>();
        private readonly IClipboardAdapter _clipboard;
        private string? _lastClipboardText;
        private readonly DispatcherTimer _monitorTimer;
        private int _historyLimit;
        private bool _isMonitoring;

        public event Action<List<ClipboardItem>>? ClipboardChanged;

        public ClipboardService(int historyLimit = 2, IClipboardAdapter? clipboard = null, bool startMonitoring = true)
        {
            _clipboard = clipboard ?? new SystemClipboardAdapter();
            _historyLimit = historyLimit < 1 ? 1 : historyLimit;
            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _monitorTimer.Tick += MonitorClipboard;

            if (startMonitoring)
            {
                Start();
            }
        }

        public void Start()
        {
            if (_isMonitoring) return;
            _monitorTimer.Start();
            _isMonitoring = true;
        }

        public void Stop()
        {
            if (!_isMonitoring) return;
            _monitorTimer.Stop();
            _isMonitoring = false;
        }

        private void MonitorClipboard(object? sender, EventArgs e)
        {
            try
            {
                if (_clipboard.ContainsText())
                {
                    var text = _clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text) && text != _lastClipboardText)
                    {
                        CaptureText(text);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Clipboard monitor tick failed: {ex.Message}");
            }
        }

        public void CaptureText(string text)
        {
            _lastClipboardText = text;
            var item = new ClipboardItem { Content = text };
            _items.Insert(0, item);

            // Keep only last N items based on history limit
            if (_items.Count > _historyLimit)
                _items.RemoveRange(_historyLimit, _items.Count - _historyLimit);

            ClipboardChanged?.Invoke(new List<ClipboardItem>(_items));
        }

        public List<ClipboardItem> GetItems()
        {
            return new List<ClipboardItem>(_items);
        }

        public void UpdateHistoryLimit(int historyLimit)
        {
            _historyLimit = historyLimit < 1 ? 1 : historyLimit;

            if (_items.Count > _historyLimit)
            {
                _items.RemoveRange(_historyLimit, _items.Count - _historyLimit);
                ClipboardChanged?.Invoke(new List<ClipboardItem>(_items));
            }
        }

        public void RestoreToClipboard(string text)
        {
            try
            {
                _clipboard.SetText(text);
                _lastClipboardText = text;
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Clipboard restore failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _monitorTimer.Tick -= MonitorClipboard;
        }
    }
}
