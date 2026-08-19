namespace PictureTool.Models;

public sealed class AppSettings
{
    public HotkeySettings Hotkeys { get; set; } = HotkeySettings.CreateDefault();

    public WindowPlacement? MainWindowPlacement { get; set; }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Hotkeys = Hotkeys.Clone(),
            MainWindowPlacement = MainWindowPlacement?.Clone()
        };
    }
}

public sealed class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public WindowPlacement Clone() => new()
    {
        Left = Left, Top = Top, Width = Width, Height = Height
    };
}

