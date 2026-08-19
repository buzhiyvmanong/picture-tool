using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PictureTool.Services;
using IoFile = System.IO.File;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfClipboard = System.Windows.Clipboard;

namespace PictureTool.Views;

public partial class PinWindow : Window
{
    private readonly string _imagePath;

    public PinWindow(string imagePath)
    {
        _imagePath = imagePath;
        InitializeComponent();

        var imageInfo = BitmapLoader.ReadInfo(imagePath);
        var displayWidth = imageInfo.Width;
        var displayHeight = imageInfo.Height;
        var maxWidth = SystemParameters.WorkArea.Width * 0.9;
        var maxHeight = SystemParameters.WorkArea.Height * 0.9;
        var scale = Math.Min(1, Math.Min(maxWidth / displayWidth, maxHeight / displayHeight));
        var initialWidth = Math.Max(96, displayWidth * scale + 2);
        var initialHeight = Math.Max(72, displayHeight * scale + 2);
        var imageWidth = Math.Max(1, initialWidth - 2);
        var imageHeight = Math.Max(1, initialHeight - 2);
        var bitmap = BitmapLoader.LoadFrozenForDisplay(
            imagePath,
            Math.Max(1, (int)Math.Round(imageInfo.PixelWidth * scale)),
            Math.Max(1, (int)Math.Round(imageInfo.PixelHeight * scale)));

        PinnedImage.Source = bitmap;
        PinnedImage.Width = imageWidth;
        PinnedImage.Height = imageHeight;
        PinContent.Width = imageWidth;
        PinContent.Height = imageHeight;
        Width = initialWidth;
        Height = initialHeight;

        Left = SystemParameters.WorkArea.Left + Math.Max(0, (SystemParameters.WorkArea.Width - Width) / 2);
        Top = SystemParameters.WorkArea.Top + Math.Max(0, (SystemParameters.WorkArea.Height - Height) / 2);
    }

    protected override void OnClosed(EventArgs e)
    {
        PinnedImage.Source = null;
        TempImageStore.TryDelete(_imagePath);
        MemoryPressureService.TrimSoon();

        base.OnClosed(e);
    }

    private void PinFrame_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsCloseButtonClick(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            Close();
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if the mouse button state changes mid-drag.
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PinContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        TopmostMenuItem.Header = Topmost ? "取消置顶" : "保持置顶";
    }

    private void CopyPin_Click(object sender, RoutedEventArgs e)
    {
        WpfClipboard.SetImage(BitmapLoader.LoadFrozen(_imagePath));
        MemoryPressureService.TrimSoon();
    }

    private void SavePin_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片|*.png",
            FileName = $"picture-tool-pin-{DateTime.Now:yyyyMMdd-HHmmss}.png"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        using var stream = IoFile.Create(dialog.FileName);
        var bitmap = BitmapLoader.LoadFrozen(_imagePath);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
        MemoryPressureService.TrimSoon();
    }

    private async void ExtractText_Click(object sender, RoutedEventArgs e)
    {
        await OcrUiHelper.RunForPathAsync(this, _imagePath).ConfigureAwait(true);
    }

    private void ToggleTopmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        TopmostMenuItem.Header = Topmost ? "取消置顶" : "保持置顶";
    }

    private void CloseMenu_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static bool IsCloseButtonClick(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
