namespace PictureTool.Models;

public sealed class HotkeyRegistrationResult
{
    public HotkeyRegistrationResult(IReadOnlyList<string> failures)
    {
        Failures = failures;
    }

    public IReadOnlyList<string> Failures { get; }
    public bool HasFailures => Failures.Count > 0;

    public static HotkeyRegistrationResult Success()
    {
        return new HotkeyRegistrationResult(Array.Empty<string>());
    }
}

