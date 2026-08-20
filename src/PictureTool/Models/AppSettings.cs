namespace PictureTool.Models;

public sealed class AppSettings
{
    public HotkeySettings Hotkeys { get; set; } = HotkeySettings.CreateDefault();

    public WindowPlacement? MainWindowPlacement { get; set; }

    public bool HasSeenWelcome { get; set; }

    /// <summary>用户已确认欢迎页的应用版本；与当前版本不一致时再次显示欢迎页。</summary>
    public string? LastSeenWelcomeVersion { get; set; }

    public string? LastDismissedUpdateVersion { get; set; }

    public bool StartWithWindows { get; set; }

    public bool CheckUpdatesOnStartup { get; set; } = true;

    public int HistoryMaxItems { get; set; } = 50;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Hotkeys = Hotkeys.Clone(),
            MainWindowPlacement = MainWindowPlacement?.Clone(),
            HasSeenWelcome = HasSeenWelcome,
            LastSeenWelcomeVersion = LastSeenWelcomeVersion,
            LastDismissedUpdateVersion = LastDismissedUpdateVersion,
            StartWithWindows = StartWithWindows,
            CheckUpdatesOnStartup = CheckUpdatesOnStartup,
            HistoryMaxItems = HistoryMaxItems
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

