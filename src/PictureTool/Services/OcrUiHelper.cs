using System.Windows;
using System.Windows.Media.Imaging;
using PictureTool.Views;
using MessageBox = System.Windows.MessageBox;

namespace PictureTool.Services;

public static class OcrUiHelper
{
    private const string DialogTitle = "提取文字";
    private static readonly OcrService Service = new();

    public static async Task RunAsync(Window? owner, Func<BitmapSource> bitmapFactory)
    {
        if (!Service.IsAvailable)
        {
            ShowWarning(owner);
            return;
        }

        BitmapSource bitmap;
        try
        {
            bitmap = bitmapFactory();
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, ex.Message, DialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        await RunAsync(owner, bitmap).ConfigureAwait(true);
    }

    public static async Task RunAsync(Window? owner, BitmapSource bitmap)
    {
        if (!Service.IsAvailable)
        {
            ShowWarning(owner);
            return;
        }

        var progress = new OcrProgressWindow();
        var safeOwner = SafeOwner(owner);
        if (safeOwner is not null)
        {
            progress.Owner = safeOwner;
        }

        progress.Show();

        try
        {
            var result = await Service.RecognizeAsync(bitmap).ConfigureAwait(true);
            progress.Close();

            if (!result.IsSuccess)
            {
                MessageBox.Show(
                    SafeOwner(owner),
                    result.ErrorMessage ?? "提取失败。",
                    DialogTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var window = new OcrResultWindow(result.Text);
            if (SafeOwner(owner) is { } resultOwner)
            {
                window.Owner = resultOwner;
            }

            window.ShowDialog();
        }
        catch (Exception ex)
        {
            progress.Close();
            MessageBox.Show(
                SafeOwner(owner),
                $"提取文字失败：{ex.Message}",
                DialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public static Task RunForPathAsync(Window? owner, string imagePath) =>
        RunAsync(owner, () => BitmapLoader.LoadFrozen(imagePath));

    private static void ShowWarning(Window? owner)
    {
        MessageBox.Show(
            SafeOwner(owner),
            "当前系统不支持文字提取，请确认 Windows 版本支持 OCR 并已安装语言包。",
            DialogTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static Window? SafeOwner(Window? owner) =>
        owner is { IsLoaded: true } ? owner : null;
}
