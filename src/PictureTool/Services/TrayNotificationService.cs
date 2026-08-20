namespace PictureTool.Services;

public static class TrayNotificationService
{
    private static Action<string, string>? _showBalloon;

    public static void Initialize(Action<string, string> showBalloon)
    {
        _showBalloon = showBalloon;
    }

    public static void Show(string title, string message)
    {
        _showBalloon?.Invoke(title, message);
    }
}
