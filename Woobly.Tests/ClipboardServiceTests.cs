using Woobly.Services;
using Xunit;

namespace Woobly.Tests;

public class ClipboardServiceTests
{
    [Fact]
    public void CaptureText_RespectsHistoryLimitAndOrder()
    {
        var fake = new FakeClipboardAdapter();
        using var service = new ClipboardService(historyLimit: 2, clipboard: fake, startMonitoring: false);

        service.CaptureText("first");
        service.CaptureText("second");
        service.CaptureText("third");

        var items = service.GetItems();
        Assert.Equal(2, items.Count);
        Assert.Equal("third", items[0].Content);
        Assert.Equal("second", items[1].Content);
    }

    [Fact]
    public void RestoreToClipboard_WritesBackToClipboardAdapter()
    {
        var fake = new FakeClipboardAdapter();
        using var service = new ClipboardService(historyLimit: 2, clipboard: fake, startMonitoring: false);

        service.RestoreToClipboard("restored");
        Assert.Equal("restored", fake.Text);
    }

    private sealed class FakeClipboardAdapter : IClipboardAdapter
    {
        public string Text { get; private set; } = string.Empty;

        public bool ContainsText() => !string.IsNullOrWhiteSpace(Text);

        public string GetText() => Text;

        public void SetText(string text) => Text = text;
    }
}
