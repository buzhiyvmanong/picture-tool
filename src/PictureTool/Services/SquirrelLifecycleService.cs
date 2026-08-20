using Squirrel;

namespace PictureTool.Services;

public static class SquirrelLifecycleService
{
    public static void HandleStartupEvents()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            SquirrelAwareApp.HandleEvents(
                onInitialInstall: (_, tools) => CreateShortcuts(tools),
                onAppUpdate: (_, tools) => CreateShortcuts(tools),
                onAppUninstall: (_, tools) => RemoveShortcuts(tools));
        }
        catch
        {
            // Portable builds or non-Squirrel launches do not require lifecycle hooks.
        }
    }

    private static void CreateShortcuts(IAppTools tools)
    {
        tools.CreateShortcutForThisExe(ShortcutLocation.StartMenu);
        tools.CreateShortcutForThisExe(ShortcutLocation.Desktop);
    }

    private static void RemoveShortcuts(IAppTools tools)
    {
        tools.RemoveShortcutForThisExe(ShortcutLocation.StartMenu);
        tools.RemoveShortcutForThisExe(ShortcutLocation.Desktop);
    }
}
