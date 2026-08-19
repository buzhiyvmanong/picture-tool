using System.Windows.Input;

namespace PictureTool.Models;

public sealed class HotkeySettings
{
    public HotkeyGesture CaptureArea { get; set; } = HotkeyGesture.Create(HotkeyModifiers.Control | HotkeyModifiers.Shift, Key.A);
    public HotkeyGesture PasteImage { get; set; } = HotkeyGesture.Create(HotkeyModifiers.Control | HotkeyModifiers.Shift, Key.V);

    public static HotkeySettings CreateDefault()
    {
        return new HotkeySettings();
    }

    public HotkeySettings Clone()
    {
        return new HotkeySettings
        {
            CaptureArea = CaptureArea.Clone(),
            PasteImage = PasteImage.Clone()
        };
    }
}

