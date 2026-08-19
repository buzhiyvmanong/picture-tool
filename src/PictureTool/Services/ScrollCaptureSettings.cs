namespace PictureTool.Services;

public static class ScrollCaptureSettings
{
    public const int AutoScrollWheelDelta = -120;
    public const int AutoScrollFrameDelayMs = 1400;
    public const int AutoScrollRetryDelayMs = 700;
    public const int AutoScrollMaxRetryCount = 5;
    public const int ManualCaptureSettleMs = 80;
    public const int ManualWheelDebounceMs = 420;
    public const int ManualWheelRapidWindowMs = 700;
    public const int ManualWheelRapidThreshold = 2;

    public const string ManualScrollTooFastWarning =
        "滚动速度过快，请重新回到截图位置，缓慢滑动";

    public const string ManualScrollBusyWarning =
        "正在拼接上一屏，请稍候再滚动";
}
