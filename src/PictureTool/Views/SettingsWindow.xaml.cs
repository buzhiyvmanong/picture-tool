using System.Windows;
using System.Windows.Input;
using PictureTool.Models;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PictureTool.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(HotkeySettings hotkeys)
    {
        Hotkeys = hotkeys.Clone();
        InitializeComponent();
        UpdateHotkeyText();
    }

    public HotkeySettings Hotkeys { get; private set; }

    private void CaptureAreaHotkeyBox_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        e.Handled = true;
        TrySetHotkey(e, gesture => Hotkeys.CaptureArea = gesture);
    }

    private void PasteImageHotkeyBox_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        e.Handled = true;
        TrySetHotkey(e, gesture => Hotkeys.PasteImage = gesture);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Hotkeys = HotkeySettings.CreateDefault();
        StatusText.Text = "已恢复默认快捷键。";
        UpdateHotkeyText();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var error = ValidateHotkeys();
        if (error is not null)
        {
            StatusText.Text = error;
            return;
        }

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

    private string? ValidateHotkeys()
    {
        if (!Hotkeys.CaptureArea.IsValidGlobalHotkey)
        {
            return "区域截图快捷键无效。";
        }

        if (!Hotkeys.PasteImage.IsValidGlobalHotkey)
        {
            return "粘贴图片快捷键无效。";
        }

        if (Hotkeys.CaptureArea.Equals(Hotkeys.PasteImage))
        {
            return "两个功能不能使用同一个快捷键。";
        }

        return null;
    }

    private void UpdateHotkeyText()
    {
        CaptureAreaHotkeyBox.Text = Hotkeys.CaptureArea.ToString();
        PasteImageHotkeyBox.Text = Hotkeys.PasteImage.ToString();
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
