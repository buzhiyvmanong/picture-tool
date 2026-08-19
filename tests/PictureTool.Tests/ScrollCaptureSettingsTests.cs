using PictureTool.Services;
using Xunit;

namespace PictureTool.Tests;

public sealed class ScrollCaptureSettingsTests
{
    [Fact]
    public void Manual_debounce_is_shorter_than_auto_frame_delay()
    {
        Assert.True(ScrollCaptureSettings.ManualWheelDebounceMs < ScrollCaptureSettings.AutoScrollFrameDelayMs);
    }

    [Fact]
    public void Warning_messages_are_not_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ScrollCaptureSettings.ManualScrollTooFastWarning));
        Assert.False(string.IsNullOrWhiteSpace(ScrollCaptureSettings.ManualScrollBusyWarning));
    }
}
