namespace PictureTool.Services;

public static class WelcomeGuide
{
    public static bool ShouldShow(string? lastSeenVersion, string currentVersion)
    {
        return !string.Equals(lastSeenVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
    }
}
