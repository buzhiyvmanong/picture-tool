using Squirrel;

namespace PictureTool.Services;

public sealed class AutoUpdateService
{
    public const string SquirrelFeedUrl =
        "https://github.com/buzhiyvmanong/picture-tool/releases/latest/download/";

    public bool IsSquirrelInstalled
    {
        get
        {
            try
            {
                using var updateManager = new UpdateManager(SquirrelFeedUrl);
                return updateManager.IsInstalledApp;
            }
            catch
            {
                return false;
            }
        }
    }

    public Task<AutoUpdateResult> TryUpdateAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => TryUpdateCore(progress), cancellationToken);
    }

    private static AutoUpdateResult TryUpdateCore(IProgress<int>? progress)
    {
        try
        {
            using var updateManager = new UpdateManager(SquirrelFeedUrl);
            if (!updateManager.IsInstalledApp)
            {
                return AutoUpdateResult.NotInstalledViaSquirrel;
            }

            var updateInfo = updateManager
                .CheckForUpdate(progress: p => progress?.Report(p))
                .GetAwaiter()
                .GetResult();

            if (updateInfo.ReleasesToApply.Count == 0)
            {
                return AutoUpdateResult.UpToDate;
            }

            updateManager.DownloadReleases(updateInfo.ReleasesToApply, p => progress?.Report(p));
            updateManager.ApplyReleases(updateInfo, p => progress?.Report(p));

            var version = updateInfo.FutureReleaseEntry?.Version.ToString() ?? string.Empty;
            return AutoUpdateResult.Updated(version);
        }
        catch (Exception ex)
        {
            return AutoUpdateResult.Failed(ex.Message);
        }
    }

    public static void RestartApplication()
    {
        UpdateManager.RestartApp();
    }
}

public sealed class AutoUpdateResult
{
    public AutoUpdateStatus Status { get; init; }

    public string? InstalledVersion { get; init; }

    public string? ErrorMessage { get; init; }

    public static AutoUpdateResult NotInstalledViaSquirrel { get; } =
        new() { Status = AutoUpdateStatus.NotInstalledViaSquirrel };

    public static AutoUpdateResult UpToDate { get; } =
        new() { Status = AutoUpdateStatus.UpToDate };

    public static AutoUpdateResult Updated(string version) =>
        new() { Status = AutoUpdateStatus.Updated, InstalledVersion = version };

    public static AutoUpdateResult Failed(string message) =>
        new() { Status = AutoUpdateStatus.Failed, ErrorMessage = message };
}

public enum AutoUpdateStatus
{
    NotInstalledViaSquirrel,
    UpToDate,
    Updated,
    Failed
}
