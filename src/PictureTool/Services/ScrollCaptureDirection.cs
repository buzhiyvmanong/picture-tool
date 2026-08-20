namespace PictureTool.Services;

public enum ScrollCaptureDirection
{
    VerticalDown,
    VerticalUp,
    HorizontalRight,
    HorizontalLeft
}

public static class ScrollCaptureDirectionExtensions
{
    public static int GetAutoWheelDelta(this ScrollCaptureDirection direction) =>
        direction switch
        {
            ScrollCaptureDirection.VerticalDown => -120,
            ScrollCaptureDirection.VerticalUp => 120,
            ScrollCaptureDirection.HorizontalRight => 120,
            ScrollCaptureDirection.HorizontalLeft => -120,
            _ => -120
        };

    public static bool IsHorizontal(this ScrollCaptureDirection direction) =>
        direction is ScrollCaptureDirection.HorizontalRight or ScrollCaptureDirection.HorizontalLeft;

    public static string GetDisplayName(this ScrollCaptureDirection direction) =>
        direction switch
        {
            ScrollCaptureDirection.VerticalDown => "向下",
            ScrollCaptureDirection.VerticalUp => "向上",
            ScrollCaptureDirection.HorizontalRight => "向右",
            ScrollCaptureDirection.HorizontalLeft => "向左",
            _ => "向下"
        };
}
