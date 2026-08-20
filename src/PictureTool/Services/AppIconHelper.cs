using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PictureTool.Services;

public static class AppIconHelper
{
    private static Icon? _cached;

    public static Icon GetTrayIcon()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            var extracted = Icon.ExtractAssociatedIcon(exePath);
            if (extracted is not null)
            {
                _cached = extracted;
                return _cached;
            }
        }

        _cached = SystemIcons.Application;
        return _cached;
    }

    public static Uri WindowIconUri { get; } =
        new("pack://application:,,,/Assets/app.ico", UriKind.Absolute);

    public static void ApplyWindowIcon(Window window)
    {
        window.Icon = BitmapFrame.Create(WindowIconUri);
    }
}
