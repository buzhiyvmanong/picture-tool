using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WpfBitmapEncoder = System.Windows.Media.Imaging.BitmapEncoder;
using WpfBitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using WinBitmapEncoder = Windows.Graphics.Imaging.BitmapEncoder;

namespace PictureTool.Services;

public static class ImageExportService
{
    private static readonly Guid WebpEncoderId = new("0AF4D220-0C41-4A87-BFC0-AE851AF65936");

    public const string SaveFilter =
        "PNG 图片|*.png|JPEG 图片|*.jpg;*.jpeg|WebP 图片|*.webp|所有支持的格式|*.png;*.jpg;*.jpeg;*.webp";

    public static string DefaultFileName(string prefix = "picture-tool")
    {
        return $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.png";
    }

    public static void Save(string path, BitmapSource bitmap)
    {
        var extension = Path.GetExtension(path);
        switch (extension.ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                SaveJpeg(path, bitmap);
                break;
            case ".webp":
                SaveWebp(path, bitmap);
                break;
            default:
                SavePng(path, bitmap);
                break;
        }
    }

    public static void SavePng(string path, BitmapSource bitmap)
    {
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(WpfBitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    public static void SaveJpeg(string path, BitmapSource bitmap, int quality = 90)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(WpfBitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    public static void SaveWebp(string path, BitmapSource bitmap)
    {
        var frame = WpfBitmapFrame.Create(bitmap);
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        using var randomAccessStream = new InMemoryRandomAccessStream();
        var encoder = WinBitmapEncoder.CreateAsync(WebpEncoderId, randomAccessStream).AsTask().GetAwaiter().GetResult();
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)width,
            (uint)height,
            96,
            96,
            pixels);
        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();

        randomAccessStream.Seek(0);
        using var output = File.Create(path);
        randomAccessStream.AsStreamForRead().CopyTo(output);
    }
}
