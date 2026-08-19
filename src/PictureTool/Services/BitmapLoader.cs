using System.IO;
using System.Windows.Media.Imaging;

namespace PictureTool.Services;

public static class BitmapLoader
{
    public static BitmapImage LoadFrozen(string path, int? decodePixelWidth = null, int? decodePixelHeight = null)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        if (decodePixelWidth is > 0)
        {
            bitmap.DecodePixelWidth = decodePixelWidth.Value;
        }
        else if (decodePixelHeight is > 0)
        {
            bitmap.DecodePixelHeight = decodePixelHeight.Value;
        }

        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public static BitmapImage LoadFrozenForDisplay(string path, int maxPixelWidth, int maxPixelHeight)
    {
        var info = ReadInfo(path);
        var scale = Math.Min(1.0, Math.Min(
            maxPixelWidth / (double)Math.Max(1, info.PixelWidth),
            maxPixelHeight / (double)Math.Max(1, info.PixelHeight)));

        if (scale >= 1)
        {
            return LoadFrozen(path);
        }

        var decodeWidth = Math.Max(1, (int)Math.Round(info.PixelWidth * scale));
        return LoadFrozen(path, decodePixelWidth: decodeWidth);
    }

    public static ImageInfo ReadInfo(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.IgnoreImageCache,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        return new ImageInfo(
            frame.PixelWidth,
            frame.PixelHeight,
            frame.DpiX <= 0 ? 96 : frame.DpiX,
            frame.DpiY <= 0 ? 96 : frame.DpiY);
    }

    public sealed record ImageInfo(int PixelWidth, int PixelHeight, double DpiX, double DpiY)
    {
        public double Width => PixelWidth * 96.0 / DpiX;

        public double Height => PixelHeight * 96.0 / DpiY;
    }
}
