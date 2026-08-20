using System.Windows;

namespace PictureTool.Infrastructure;

internal static class RuntimeGuard
{
    private const string DotNetDownloadUrl = "https://dotnet.microsoft.com/download/dotnet/10.0";

    public static bool TryEnsureReady(out string message)
    {
        if (!OperatingSystem.IsWindows())
        {
            message = "PictureTool 仅支持 Windows。";
            return false;
        }

        if (Environment.OSVersion.Version.Build < 19_041)
        {
            message = "需要 Windows 10 版本 2004（内部版本 19041）或更高版本。";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static void ShowFatal(string message)
    {
        var text = message.TrimEnd();
        if (!text.Contains("dotnet.microsoft.com", StringComparison.OrdinalIgnoreCase))
        {
            text += $"\n\n如提示缺少 .NET 运行时，请安装 .NET 10 桌面运行时：\n{DotNetDownloadUrl}";
        }

        System.Windows.MessageBox.Show(
            text,
            "PictureTool 无法启动",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
