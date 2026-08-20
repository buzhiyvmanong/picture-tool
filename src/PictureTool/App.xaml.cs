using System.Windows;
using PictureTool.Infrastructure;

namespace PictureTool;

public partial class App : System.Windows.Application
{
    private AppCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            RuntimeGuard.ShowFatal($"程序发生错误：{args.Exception.Message}");
            args.Handled = true;
            Shutdown(1);
        };

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _coordinator = new AppCoordinator(Dispatcher);
        _coordinator.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _coordinator?.Dispose();
        base.OnExit(e);
    }
}
