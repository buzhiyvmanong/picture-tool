using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace PictureTool.Services;

public sealed class UpdateCheckService
{
    private const string Repository = "buzhiyvmanong/picture-tool";
    private static readonly HttpClient Http = CreateClient();

    public string CurrentVersion { get; } = GetCurrentVersion();

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync(
                $"https://api.github.com/repos/{Repository}/releases/latest",
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed($"HTTP {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var root = document.RootElement;
            var latestTag = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString()?.Trim()
                : null;

            if (string.IsNullOrWhiteSpace(latestTag))
            {
                return UpdateCheckResult.Failed("未找到版本号");
            }

            var latestVersion = NormalizeVersion(latestTag);
            if (!IsNewerVersion(latestVersion, CurrentVersion))
            {
                return UpdateCheckResult.UpToDate(CurrentVersion);
            }

            var downloadUrl = root.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;

            downloadUrl ??= $"https://github.com/{Repository}/releases/latest";
            return UpdateCheckResult.UpdateAvailable(CurrentVersion, latestVersion, downloadUrl);
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    public static void OpenDownloadPage(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    internal static bool IsNewerVersion(string latest, string current)
    {
        return Version.Parse(NormalizeVersion(latest)) > Version.Parse(NormalizeVersion(current));
    }

    internal static string NormalizeVersion(string version)
    {
        var trimmed = version.Trim();
        return trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? trimmed[1..]
            : trimmed;
    }

    private static string GetCurrentVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
            return plusIndex >= 0 ? informational[..plusIndex] : informational;
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PictureTool-UpdateChecker");
        return client;
    }
}

public sealed class UpdateCheckResult
{
    public UpdateCheckStatus Status { get; init; }

    public string CurrentVersion { get; init; } = string.Empty;

    public string? LatestVersion { get; init; }

    public string? DownloadUrl { get; init; }

    public string? ErrorMessage { get; init; }

    public static UpdateCheckResult UpToDate(string current) =>
        new() { Status = UpdateCheckStatus.UpToDate, CurrentVersion = current };

    public static UpdateCheckResult UpdateAvailable(string current, string latest, string downloadUrl) =>
        new()
        {
            Status = UpdateCheckStatus.UpdateAvailable,
            CurrentVersion = current,
            LatestVersion = latest,
            DownloadUrl = downloadUrl
        };

    public static UpdateCheckResult Failed(string message) =>
        new() { Status = UpdateCheckStatus.Failed, ErrorMessage = message };
}

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}
