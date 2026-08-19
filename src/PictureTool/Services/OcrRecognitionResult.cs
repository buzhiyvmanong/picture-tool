namespace PictureTool.Services;

public sealed class OcrRecognitionResult
{
    public bool IsSuccess { get; init; }

    public string Text { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }

    public static OcrRecognitionResult Succeeded(string text) =>
        new() { IsSuccess = true, Text = text };

    public static OcrRecognitionResult Failed(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
