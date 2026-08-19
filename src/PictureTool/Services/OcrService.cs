using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WpfBitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using WpfPngBitmapEncoder = System.Windows.Media.Imaging.PngBitmapEncoder;

namespace PictureTool.Services;

public sealed class OcrService
{
    public bool IsAvailable => OcrEngine.AvailableRecognizerLanguages.Count > 0;

    public async Task<OcrRecognitionResult> RecognizeAsync(BitmapSource bitmapSource, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return OcrRecognitionResult.Failed("当前系统不支持文字提取。");
        }

        var engine = CreateEngine();
        if (engine is null)
        {
            return OcrRecognitionResult.Failed("未找到可用的 OCR 语言包，请在 Windows 设置中安装 OCR 语言。");
        }

        using var softwareBitmap = await ConvertToSoftwareBitmapAsync(bitmapSource).ConfigureAwait(false);
        if (softwareBitmap is null)
        {
            return OcrRecognitionResult.Failed("无法读取图片。");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);
        var text = FormatOcrResult(result);
        return string.IsNullOrEmpty(text)
            ? OcrRecognitionResult.Failed("未提取到文字。")
            : OcrRecognitionResult.Succeeded(text);
    }

    public async Task<OcrRecognitionResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var bitmap = BitmapLoader.LoadFrozen(imagePath);
        return await RecognizeAsync(bitmap, cancellationToken).ConfigureAwait(false);
    }

    internal static string FormatOcrResult(OcrResult result)
    {
        var sb = new StringBuilder();
        foreach (var line in result.Lines)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            var lineText = string.Join(" ", line.Words.Select(w => w.Text));
            sb.Append(RemoveCjkSpaces(lineText));
        }

        return sb.ToString().Trim();
    }

    internal static string RemoveCjkSpaces(string text)
    {
        const string cjk = @"\u2E80-\u9FFF\uF900-\uFAFF\uFE30-\uFE4F\u3000-\u303F\uFF00-\uFFEF";
        string previous;
        do
        {
            previous = text;
            text = Regex.Replace(text, $@"([{cjk}])\s+([{cjk}])", "$1$2");
        } while (text != previous);

        return text;
    }

    private static OcrEngine? CreateEngine()
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is not null)
        {
            return engine;
        }

        var chinese = OcrEngine.AvailableRecognizerLanguages
            .FirstOrDefault(language => language.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
        if (chinese is not null)
        {
            return OcrEngine.TryCreateFromLanguage(chinese);
        }

        var fallback = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault();
        return fallback is null ? null : OcrEngine.TryCreateFromLanguage(fallback);
    }

    private static async Task<SoftwareBitmap?> ConvertToSoftwareBitmapAsync(BitmapSource bitmapSource)
    {
        using var memoryStream = new MemoryStream();
        var encoder = new WpfPngBitmapEncoder();
        encoder.Frames.Add(WpfBitmapFrame.Create(bitmapSource));
        encoder.Save(memoryStream);

        using var randomAccessStream = new InMemoryRandomAccessStream();
        await randomAccessStream.WriteAsync(memoryStream.ToArray().AsBuffer()).AsTask().ConfigureAwait(false);
        randomAccessStream.Seek(0);

        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream).AsTask().ConfigureAwait(false);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied).AsTask().ConfigureAwait(false);
    }
}
