using System.Windows;
using PictureTool.Infrastructure;

namespace PictureTool;

public partial class App : System.Windows.Application
{
    private AppCoordinator? _coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
