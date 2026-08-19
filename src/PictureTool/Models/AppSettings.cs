namespace PictureTool.Models;

public sealed class AppSettings
{
    public HotkeySettings Hotkeys { get; set; } = HotkeySettings.CreateDefault();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Hotkeys = Hotkeys.Clone()
        };
    }
}

