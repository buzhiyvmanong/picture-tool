using System.Drawing;
using System.Reflection;

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

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            var extracted = Icon.ExtractAssociatedIcon(assemblyPath);
            if (extracted is not null)
            {
                _cached = extracted;
                return _cached;
            }
        }

        _cached = SystemIcons.Application;
        return _cached;
    }

    public static Uri? GetWindowIconUri()
    {
        return new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
    }
}
