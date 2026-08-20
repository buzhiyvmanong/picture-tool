using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace PictureTool.Services;

public sealed class ScrollCaptureController : IDisposable
{
    private GlobalWheelHook? _hook;
    private CancellationTokenSource? _wheelDebounce;
    private int _wheelEventsInWindow;
    private long _wheelWindowStartTick;
    private bool _disposed;

    public event Action? CaptureRequested;
    public event Action<string, string>? WarningRequested;

    public bool IsInstalled => _hook is not null;

    public void Install()
    {
        if (_hook is not null)
        {
            return;
        }

        _hook = new GlobalWheelHook();
        _hook.WheelScrolled += OnWheelScrolled;
        _hook.Install();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _wheelDebounce?.Cancel();
        _wheelDebounce = null;

        if (_hook is not null)
        {
            _hook.WheelScrolled -= OnWheelScrolled;
            _hook.Dispose();
            _hook = null;
        }
    }

    public bool TryHandleWheel(
        int wheelDelta,
        DrawingPoint screenPoint,
        DrawingRectangle scrollRegion,
        ScrollCaptureDirection direction,
        bool isHorizontalWheel,
        bool isBusy,
        bool isAutoScrolling,
        bool isAutoScrollPaused,
        Func<DrawingPoint, bool> isInsideChrome)
    {
        if (wheelDelta == 0 || direction.IsHorizontal() != isHorizontalWheel)
        {
            return false;
        }

        if (!ContainsScreenPoint(scrollRegion, screenPoint))
        {
            return false;
        }

        if (isInsideChrome(screenPoint))
        {
            return false;
        }

        if (isAutoScrolling && !isAutoScrollPaused)
        {
            return false;
        }

        if (isBusy)
        {
            WarningRequested?.Invoke(
                ScrollCaptureSettings.ManualScrollBusyWarning,
                "正在拼接，请稍候");
            return true;
        }

        TrackWheelPace();
        _wheelDebounce?.Cancel();
        _wheelDebounce = new CancellationTokenSource();
        var token = _wheelDebounce.Token;
        _ = DebouncedCaptureAsync(token);
        return true;
    }

    public void ResetPace()
    {
        _wheelEventsInWindow = 0;
    }

    private async Task DebouncedCaptureAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(ScrollCaptureSettings.ManualWheelDebounceMs, token);
            CaptureRequested?.Invoke();
        }
        catch (OperationCanceledException)
        {
            WarningRequested?.Invoke(
                ScrollCaptureSettings.ManualScrollTooFastWarning,
                "滚动过快，请放慢");
        }
    }

    private void TrackWheelPace()
    {
        var now = Environment.TickCount64;
        if (now - _wheelWindowStartTick > ScrollCaptureSettings.ManualWheelRapidWindowMs)
        {
            _wheelEventsInWindow = 0;
            _wheelWindowStartTick = now;
        }

        _wheelEventsInWindow++;
        if (_wheelEventsInWindow >= ScrollCaptureSettings.ManualWheelRapidThreshold)
        {
            WarningRequested?.Invoke(
                ScrollCaptureSettings.ManualScrollTooFastWarning,
                "滚动过快，请放慢");
        }
    }

    private void OnWheelScrolled(int wheelDelta, DrawingPoint screenPoint, bool isHorizontal)
    {
        WheelScrolled?.Invoke(wheelDelta, screenPoint, isHorizontal);
    }

    public event Action<int, DrawingPoint, bool>? WheelScrolled;

    private static bool ContainsScreenPoint(DrawingRectangle region, DrawingPoint point)
    {
        return point.X >= region.Left
            && point.X < region.Right
            && point.Y >= region.Top
            && point.Y < region.Bottom;
    }
}
