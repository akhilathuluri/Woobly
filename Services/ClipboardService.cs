using System;
using System.Collections.Generic;
using WpfClipboard = System.Windows.Clipboard;
using System.Windows.Threading;
using Woobly.Models;

namespace Woobly.Services
{
    public class ClipboardService
    {
        private readonly List<ClipboardItem> _items = new List<ClipboardItem>();
        private string? _lastClipboardText;
        private DispatcherTimer _monitorTimer;
        private int _historyLimit;

        public event Action<List<ClipboardItem>>? ClipboardChanged;

        public ClipboardService(int historyLimit = 2)
        {
            _historyLimit = historyLimit < 1 ? 1 : historyLimit;
            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _monitorTimer.Tick += MonitorClipboard;
            _monitorTimer.Start();
        }

        private void MonitorClipboard(object? sender, EventArgs e)
        {
            try
            {
                if (WpfClipboard.ContainsText())
                {
                    var text = WpfClipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text) && text != _lastClipboardText)
                    {
                        _lastClipboardText = text;
                        AddClipboardItem(text);
                    }
                }
            }
            catch { }
        }

        private void AddClipboardItem(string text)
        {
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
                WpfClipboard.SetText(text);
                _lastClipboardText = text;
            }
            catch { }
        }
    }
}
