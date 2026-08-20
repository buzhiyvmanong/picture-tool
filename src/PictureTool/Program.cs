using System.IO;
using System.Windows;
using PictureTool.Infrastructure;

namespace PictureTool;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            if (!RuntimeGuard.TryEnsureReady(out var error))
            {
                RuntimeGuard.ShowFatal(error);
                return;
            }

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or BadImageFormatException)
        {
            RuntimeGuard.ShowFatal(
                "缺少必要的运行环境，无法加载程序组件。\n请安装 .NET 10 桌面运行时后重试。");
        }
    }
}
