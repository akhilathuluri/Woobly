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

        public event Action<List<ClipboardItem>>? ClipboardChanged;

        public ClipboardService()
        {
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

            // Keep only last 2 items
            if (_items.Count > 2)
                _items.RemoveRange(2, _items.Count - 2);

            ClipboardChanged?.Invoke(new List<ClipboardItem>(_items));
        }

        public List<ClipboardItem> GetItems()
        {
            return new List<ClipboardItem>(_items);
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
