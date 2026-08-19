using System.IO;

namespace PictureTool.Services;

public static class TempImageStore
{
    private const string DirectoryName = "PictureTool";

    public static string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), DirectoryName);

    public static string CreatePngPath()
    {
        Directory.CreateDirectory(DirectoryPath);
        return Path.Combine(DirectoryPath, $"{Guid.NewGuid():N}.png");
    }

    public static void TryDelete(string? path)
    {
        if (!IsManagedPngPath(path))
        {
            return;
        }

        TryDeleteFile(path!);
    }

    public static void CleanupStale(TimeSpan maxAge)
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
            {
                return;
            }

            var cutoff = DateTime.UtcNow - maxAge;
            foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.png", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // Cleanup is best effort; active images may still be in use.
                }
            }
        }
        catch
        {
            // The temp folder is optional and should never block the app from starting.
        }
    }

    private static bool IsManagedPngPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var rootPath = Path.GetFullPath(DirectoryPath);
            if (!rootPath.EndsWith(Path.DirectorySeparatorChar))
            {
                rootPath += Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary images are cache files; failure to delete is non-fatal.
        }
    }
}
