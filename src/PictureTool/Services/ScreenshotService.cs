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
}
