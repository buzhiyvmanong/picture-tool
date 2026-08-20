using PictureTool.Models;

namespace PictureTool.Services;

public static class HotkeyConflictAdvisor
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    public static string DescribeRegistrationFailure(string featureName, HotkeyGesture gesture, int win32Error)
    {
        var baseMessage = win32Error switch
        {
            ErrorHotkeyAlreadyRegistered =>
                $"{featureName}快捷键 {gesture} 已被其他程序占用。",
            _ => $"{featureName}快捷键 {gesture} 注册失败（错误码 {win32Error}）。"
        };

        var suggestion = SuggestAlternative(gesture);
        return $"{baseMessage}\n建议改用：{suggestion}";
    }

    public static string SuggestAlternative(HotkeyGesture gesture)
    {
        var modifiers = gesture.Modifiers;
        var key = gesture.Key;

        if (modifiers == HotkeyModifiers.None)
        {
            modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift;
        }

        var candidates = new List<HotkeyGesture>
        {
            HotkeyGesture.Create(modifiers, NextKey(key)),
            HotkeyGesture.Create(ToggleModifier(modifiers), key),
            HotkeyGesture.Create(modifiers | HotkeyModifiers.Shift, key),
            HotkeyGesture.Create(HotkeyModifiers.Control | HotkeyModifiers.Alt, key),
            HotkeyGesture.Create(HotkeyModifiers.Control | HotkeyModifiers.Shift, NextKey(key))
        };

        foreach (var candidate in candidates)
        {
            if (candidate.IsValidGlobalHotkey && !candidate.Equals(gesture))
            {
                return candidate.ToString();
            }
        }

        return "Ctrl + Shift + B";
    }

    private static HotkeyModifiers ToggleModifier(HotkeyModifiers modifiers)
    {
        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            return (modifiers & ~HotkeyModifiers.Control) | HotkeyModifiers.Alt;
        }

        return (modifiers & ~HotkeyModifiers.Alt) | HotkeyModifiers.Control;
    }

    private static System.Windows.Input.Key NextKey(System.Windows.Input.Key key)
    {
        return key switch
        {
            System.Windows.Input.Key.A => System.Windows.Input.Key.B,
            System.Windows.Input.Key.B => System.Windows.Input.Key.C,
            System.Windows.Input.Key.C => System.Windows.Input.Key.D,
            System.Windows.Input.Key.V => System.Windows.Input.Key.W,
            _ => System.Windows.Input.Key.Q
        };
    }
}
