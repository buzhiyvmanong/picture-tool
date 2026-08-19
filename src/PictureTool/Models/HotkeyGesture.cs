using System.Windows.Input;

namespace PictureTool.Models;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public sealed class HotkeyGesture : IEquatable<HotkeyGesture>
{
    public HotkeyModifiers Modifiers { get; set; }
    public Key Key { get; set; }

    public bool IsValidGlobalHotkey => Modifiers != HotkeyModifiers.None && Key != Key.None && !IsModifierKey(Key);

    public static HotkeyGesture Create(HotkeyModifiers modifiers, Key key)
    {
        return new HotkeyGesture
        {
            Modifiers = modifiers,
            Key = key
        };
    }

    public static HotkeyGesture FromKeyboard(Key key, ModifierKeys modifiers)
    {
        return Create(FromKeyboardModifiers(modifiers), key);
    }

    public static HotkeyModifiers FromKeyboardModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= HotkeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= HotkeyModifiers.Win;
        }

        return result;
    }

    public HotkeyGesture Clone()
    {
        return Create(Modifiers, Key);
    }

    public override string ToString()
    {
        if (Key == Key.None)
        {
            return "未设置";
        }

        var parts = new List<string>();

        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(FormatKey(Key));
        return string.Join(" + ", parts);
    }

    public bool Equals(HotkeyGesture? other)
    {
        return other is not null && Modifiers == other.Modifiers && Key == other.Key;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as HotkeyGesture);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Modifiers, Key);
    }

    public static bool IsModifierKey(Key key)
    {
        return key is Key.LeftAlt
            or Key.RightAlt
            or Key.LeftCtrl
            or Key.RightCtrl
            or Key.LeftShift
            or Key.RightShift
            or Key.LWin
            or Key.RWin;
    }

    private static string FormatKey(Key key)
    {
        var text = key.ToString();

        if (text.Length == 2 && text[0] == 'D' && char.IsDigit(text[1]))
        {
            return text[1].ToString();
        }

        if (text.StartsWith("NumPad", StringComparison.Ordinal))
        {
            return text.Replace("NumPad", "Num ", StringComparison.Ordinal);
        }

        return text;
    }
}

