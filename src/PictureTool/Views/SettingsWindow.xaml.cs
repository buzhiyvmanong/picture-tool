using System.Globalization;
using System.Windows;
using System.Windows.Input;
using PictureTool.Models;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PictureTool.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        Settings = settings.Clone();
        InitializeComponent();
        UpdateHotkeyText();
        StartWithWindowsCheckBox.IsChecked = Settings.StartWithWindows;
        CheckUpdatesCheckBox.IsChecked = Settings.CheckUpdatesOnStartup;
        HistoryMaxItemsBox.Text = Settings.HistoryMaxItems.ToString(CultureInfo.InvariantCulture);
    }

    public AppSettings Settings { get; private set; }

    private void CaptureAreaHotkeyBox_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        e.Handled = true;
        TrySetHotkey(e, gesture => Settings.Hotkeys.CaptureArea = gesture);
    }

    private void PasteImageHotkeyBox_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        e.Handled = true;
        TrySetHotkey(e, gesture => Settings.Hotkeys.PasteImage = gesture);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Settings = new AppSettings();
        StatusText.Text = "已恢复默认设置。";
        UpdateHotkeyText();
        StartWithWindowsCheckBox.IsChecked = Settings.StartWithWindows;
        CheckUpdatesCheckBox.IsChecked = Settings.CheckUpdatesOnStartup;
        HistoryMaxItemsBox.Text = Settings.HistoryMaxItems.ToString(CultureInfo.InvariantCulture);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var error = ValidateSettings();
        if (error is not null)
        {
            StatusText.Text = error;
            return;
        }

        Settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        Settings.CheckUpdatesOnStartup = CheckUpdatesCheckBox.IsChecked == true;
        Settings.HistoryMaxItems = int.Parse(HistoryMaxItemsBox.Text.Trim(), CultureInfo.InvariantCulture);
        DialogResult = true;
        Close();
    }

    private void TrySetHotkey(WpfKeyEventArgs e, Action<HotkeyGesture> apply)
    {
        var key = ResolveKey(e);
        if (HotkeyGesture.IsModifierKey(key))
        {
            StatusText.Text = "还需要再按一个非修饰键。";
            return;
        }

        var gesture = HotkeyGesture.FromKeyboard(key, Keyboard.Modifiers);
        if (!gesture.IsValidGlobalHotkey)
        {
            StatusText.Text = "快捷键需要包含 Ctrl、Alt、Shift 或 Win。";
            return;
        }

        apply(gesture);
        StatusText.Text = $"已录入：{gesture}";
        UpdateHotkeyText();
    }

    private string? ValidateSettings()
    {
        if (!Settings.Hotkeys.CaptureArea.IsValidGlobalHotkey)
        {
            return "区域截图快捷键无效。";
        }

        if (!Settings.Hotkeys.PasteImage.IsValidGlobalHotkey)
        {
            return "粘贴图片快捷键无效。";
        }

        if (Settings.Hotkeys.CaptureArea.Equals(Settings.Hotkeys.PasteImage))
        {
            return "两个功能不能使用同一个快捷键。";
        }

        if (!int.TryParse(HistoryMaxItemsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxItems)
            || maxItems is < 5 or > 200)
        {
            return "历史记录上限需在 5 到 200 之间。";
        }

        return null;
    }

    private void UpdateHotkeyText()
    {
        CaptureAreaHotkeyBox.Text = Settings.Hotkeys.CaptureArea.ToString();
        PasteImageHotkeyBox.Text = Settings.Hotkeys.PasteImage.ToString();
    }

    private static Key ResolveKey(WpfKeyEventArgs e)
    {
        if (e.Key == Key.System)
        {
            return e.SystemKey;
        }

        if (e.Key == Key.ImeProcessed)
        {
            return e.ImeProcessedKey;
        }

        return e.Key;
    }
}
