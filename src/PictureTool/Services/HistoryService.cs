using System.Collections.ObjectModel;
using System.IO;

namespace PictureTool.Services;

public sealed class HistoryService
{
    private static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PictureTool", "history");

    private int _maxItems = 50;

    public ObservableCollection<HistoryItem> Items { get; } = new();

    public HistoryService(int maxItems = 50)
    {
        _maxItems = Math.Clamp(maxItems, 5, 200);
        Directory.CreateDirectory(HistoryDir);
        LoadExisting();
    }

    public void ConfigureMaxItems(int maxItems)
    {
        _maxItems = Math.Clamp(maxItems, 5, 200);
        TrimOverflow();
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
        TrimOverflow();
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
            .Take(_maxItems);

        foreach (var file in files)
        {
            Items.Add(new HistoryItem(file, File.GetCreationTime(file)));
        }
    }

    private void TrimOverflow()
    {
        while (Items.Count > _maxItems)
        {
            var removed = Items[^1];
            Items.RemoveAt(Items.Count - 1);
            TryDeleteFile(removed.FilePath);
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
        FileUri = new Uri(filePath);
        Timestamp = timestamp;
    }

    public string FilePath { get; }
    public Uri FileUri { get; }
    public DateTime Timestamp { get; }
    public string DisplayTime => Timestamp.ToString("MM/dd HH:mm:ss");
}
