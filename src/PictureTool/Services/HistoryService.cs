using System.Collections.ObjectModel;
using System.IO;

namespace PictureTool.Services;

public sealed class HistoryService
{
    private static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PictureTool", "history");

    private const int MaxItems = 50;

    public ObservableCollection<HistoryItem> Items { get; } = new();

    public HistoryService()
    {
        Directory.CreateDirectory(HistoryDir);
        LoadExisting();
    }

    public void Add(string sourcePath)
    {
        var timestamp = DateTime.Now;
        var fileName = $"{timestamp:yyyyMMdd-HHmmss-fff}.png";
        var destPath = Path.Combine(HistoryDir, fileName);

        try
        {
            File.Copy(sourcePath, destPath, overwrite: true);
        }
        catch
        {
            return;
        }

        Items.Insert(0, new HistoryItem(destPath, timestamp));

        while (Items.Count > MaxItems)
        {
            var removed = Items[^1];
            Items.RemoveAt(Items.Count - 1);
            TryDeleteFile(removed.FilePath);
        }
    }

    public void Remove(HistoryItem item)
    {
        Items.Remove(item);
        TryDeleteFile(item.FilePath);
    }

    public void ClearAll()
    {
        foreach (var item in Items)
        {
            TryDeleteFile(item.FilePath);
        }

        Items.Clear();
    }

    private void LoadExisting()
    {
        var files = Directory.GetFiles(HistoryDir, "*.png")
            .OrderByDescending(f => File.GetCreationTime(f))
            .Take(MaxItems);

        foreach (var file in files)
        {
            Items.Add(new HistoryItem(file, File.GetCreationTime(file)));
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); } catch { }
    }
}

public sealed class HistoryItem
{
    public HistoryItem(string filePath, DateTime timestamp)
    {
        FilePath = filePath;
        Timestamp = timestamp;
    }

    public string FilePath { get; }
    public DateTime Timestamp { get; }
    public string DisplayTime => Timestamp.ToString("MM/dd HH:mm:ss");
}
