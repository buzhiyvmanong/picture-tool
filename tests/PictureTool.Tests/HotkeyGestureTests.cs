using System.Windows.Input;
using PictureTool.Models;
using Xunit;

namespace PictureTool.Tests;

public sealed class HotkeyGestureTests
{
    [Fact]
    public void IsValidGlobalHotkey_requires_modifier_and_non_modifier_key()
    {
        var valid = HotkeyGesture.Create(HotkeyModifiers.Control | HotkeyModifiers.Shift, Key.A);
        var missingModifier = HotkeyGesture.Create(HotkeyModifiers.None, Key.A);
        var modifierOnly = HotkeyGesture.Create(HotkeyModifiers.Control, Key.LeftCtrl);

        Assert.True(valid.IsValidGlobalHotkey);
        Assert.False(missingModifier.IsValidGlobalHotkey);
        Assert.False(modifierOnly.IsValidGlobalHotkey);
    }

    [Fact]
    public void ToString_formats_common_shortcut()
    {
        var gesture = HotkeyGesture.Create(HotkeyModifiers.Control | HotkeyModifiers.Shift, Key.A);

        Assert.Equal("Ctrl + Shift + A", gesture.ToString());
    }
}
