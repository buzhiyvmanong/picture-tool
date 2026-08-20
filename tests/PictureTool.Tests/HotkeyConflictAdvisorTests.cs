using PictureTool.Models;
using PictureTool.Services;
using Xunit;

namespace PictureTool.Tests;

public class HotkeyConflictAdvisorTests
{
    [Fact]
    public void DescribeRegistrationFailure_IncludesSuggestion()
    {
        var gesture = HotkeyGesture.Create(HotkeyModifiers.Control | HotkeyModifiers.Shift, System.Windows.Input.Key.A);
        var message = HotkeyConflictAdvisor.DescribeRegistrationFailure("区域截图", gesture, 1409);

        Assert.Contains("已被其他程序占用", message);
        Assert.Contains("建议改用", message);
    }
}
