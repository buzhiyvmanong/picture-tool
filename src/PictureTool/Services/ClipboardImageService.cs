using System.Windows;
using System.Windows.Media.Imaging;
using IoFile = System.IO.File;
using WpfClipboard = System.Windows.Clipboard;

namespace PictureTool.Services;

public sealed class ClipboardImageService
{
    public string? TrySaveImageFromClipboard()
    {
        if (!WpfClipboard.ContainsImage())
        {
            return null;
        }

        var bitmap = WpfClipboard.GetImage();
        if (bitmap is null)
        {
            return null;
        }

        var path = TempImageStore.CreatePngPath();

        using var stream = IoFile.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);

        return path;
    }
}
