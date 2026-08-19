using System.Text.Json;
using System.Text.Json.Serialization;
using PictureTool.Models;
using IoDirectory = System.IO.Directory;
using IoFile = System.IO.File;
using IoPath = System.IO.Path;

namespace PictureTool.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SettingsPath { get; } = IoPath.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PictureTool",
        "settings.json");

    public AppSettings Load()
    {
        if (!IoFile.Exists(SettingsPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            using var stream = IoFile.OpenRead(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, _jsonOptions);
            return settings?.Clone() ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = IoPath.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            IoDirectory.CreateDirectory(directory);
        }

        using var stream = IoFile.Create(SettingsPath);
        JsonSerializer.Serialize(stream, settings, _jsonOptions);
    }
}
