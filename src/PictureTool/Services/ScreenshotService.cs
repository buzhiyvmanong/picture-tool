using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using PictureTool.Models;
using Forms = System.Windows.Forms;

namespace PictureTool.Services;

public sealed class ScreenshotService
{
    public ScreenshotFrame CaptureVirtualScreen()
    {
        var bounds = Forms.SystemInformation.VirtualScreen;
        var path = TempImageStore.CreatePngPath();

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        bitmap.Save(path, ImageFormat.Png);

        return new ScreenshotFrame(
            path,
            bounds,
            new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight));
    }

    public string Crop(ScreenshotFrame frame, Rectangle selection)
    {
        var sourceOffsetX = selection.X - frame.PixelBounds.X;
        var sourceOffsetY = selection.Y - frame.PixelBounds.Y;
        var width = Math.Max(1, selection.Width);
        var height = Math.Max(1, selection.Height);

        using var source = new Bitmap(frame.ImagePath);
        var cropRect = ClampCrop(source, new Rectangle(sourceOffsetX, sourceOffsetY, width, height));
        using var target = source.Clone(cropRect, PixelFormat.Format32bppPArgb);

        var path = TempImageStore.CreatePngPath();
        target.Save(path, ImageFormat.Png);
        return path;
    }

    private static Rectangle ClampCrop(Bitmap source, Rectangle crop)
    {
        var left = Math.Clamp(crop.Left, 0, source.Width - 1);
        var top = Math.Clamp(crop.Top, 0, source.Height - 1);
        var right = Math.Clamp(crop.Right, left + 1, source.Width);
        var bottom = Math.Clamp(crop.Bottom, top + 1, source.Height);

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

}
