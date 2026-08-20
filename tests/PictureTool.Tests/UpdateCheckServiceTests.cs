using PictureTool.Services;
using Xunit;

namespace PictureTool.Tests;

public class UpdateCheckServiceTests
{
    [Theory]
    [InlineData("1.0.2", "1.0.1", true)]
    [InlineData("v1.1.0", "1.0.9", true)]
    [InlineData("1.0.1", "1.0.1", false)]
    [InlineData("1.0.0", "1.0.1", false)]
    public void IsNewerVersion_ComparesCorrectly(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateCheckService.IsNewerVersion(latest, current));
    }

    [Theory]
    [InlineData("v1.0.1", "1.0.1")]
    [InlineData("V2.3.4", "2.3.4")]
    [InlineData("1.0.0", "1.0.0")]
    public void NormalizeVersion_StripsPrefix(string input, string expected)
    {
        Assert.Equal(expected, UpdateCheckService.NormalizeVersion(input));
    }
}
