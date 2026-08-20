using System.Windows;
using PictureTool.Infrastructure;
using PictureTool.Services;

namespace PictureTool;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            SquirrelLifecycleService.HandleStartupEvents();

            if (!RuntimeGuard.TryEnsureReady(out var error))
            {
                RuntimeGuard.ShowFatal(error);
                return;
            }

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            RuntimeGuard.ShowFatal($"启动失败：{ex.Message}");
        }
    }
}
