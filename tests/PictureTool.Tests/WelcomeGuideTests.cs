using PictureTool.Services;
using Xunit;

namespace PictureTool.Tests;

public sealed class WelcomeGuideTests
{
    [Theory]
    [InlineData(null, "1.0.5", true)]
    [InlineData("", "1.0.5", true)]
    [InlineData("1.0.4", "1.0.5", true)]
    [InlineData("1.0.5", "1.0.5", false)]
    [InlineData("1.0.5", "1.0.6", true)]
    public void ShouldShow_respects_last_seen_version(string? lastSeen, string current, bool expected)
    {
        Assert.Equal(expected, WelcomeGuide.ShouldShow(lastSeen, current));
    }

    [Fact]
    public void ShouldShow_is_case_insensitive()
    {
        Assert.False(WelcomeGuide.ShouldShow("1.0.5", "1.0.5"));
    }
}
