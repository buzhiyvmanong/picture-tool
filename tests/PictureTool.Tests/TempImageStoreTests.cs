using PictureTool.Services;
using Xunit;

namespace PictureTool.Tests;

public sealed class TempImageStoreTests
{
    [Fact]
    public void CreatePngPath_uses_managed_temp_directory()
    {
        var path = TempImageStore.CreatePngPath();

        Assert.StartsWith(TempImageStore.DirectoryPath, path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".png", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDelete_ignores_paths_outside_managed_directory()
    {
        TempImageStore.TryDelete(@"C:\Windows\Temp\outside.png");
    }
}
