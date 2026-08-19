using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PictureTool.Models;

namespace PictureTool.Services;

public sealed class HotkeyService : IDisposable
{
    private const int CaptureAreaId = 1001;
    private const int PasteImageId = 1002;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly Window _host;
    private HwndSource? _source;
    private IntPtr _handle;
    private Action? _captureArea;
    private Action? _pasteImage;
    private HotkeySettings _settings = HotkeySettings.CreateDefault();

    public HotkeyService(Window host)
    {
        _host = host;
        _host.SourceInitialized += OnSourceInitialized;
    }

    public HotkeyRegistrationResult ApplySettings(HotkeySettings settings, Action captureArea, Action pasteImage)
    {
        _settings = settings.Clone();
        _captureArea = captureArea;
        _pasteImage = pasteImage;
        return RegisterCurrentHotkeys();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(_host).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);

        RegisterCurrentHotkeys();
    }

    private HotkeyRegistrationResult RegisterCurrentHotkeys()
    {
        if (_handle == IntPtr.Zero)
        {
            return HotkeyRegistrationResult.Success();
        }

        UnregisterHotKey(_handle, CaptureAreaId);
        UnregisterHotKey(_handle, PasteImageId);

        var failures = new List<string>();
        TryRegister(_handle, "区域截图", CaptureAreaId, _settings.CaptureArea, failures);

        if (_settings.CaptureArea.Equals(_settings.PasteImage))
        {
            failures.Add("粘贴图片快捷键和区域截图重复。");
        }
        else
        {
            TryRegister(_handle, "粘贴图片", PasteImageId, _settings.PasteImage, failures);
        }

        return new HotkeyRegistrationResult(failures);
    }

    private static void TryRegister(IntPtr handle, string name, int id, HotkeyGesture gesture, ICollection<string> failures)
    {
        if (!gesture.IsValidGlobalHotkey)
        {
            failures.Add($"{name}快捷键无效。");
            return;
        }

        if (RegisterHotKey(handle, id, ToNativeModifiers(gesture.Modifiers), (uint)KeyInterop.VirtualKeyFromKey(gesture.Key)))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        failures.Add($"{name}快捷键 {gesture} 注册失败，可能已被其他程序占用。错误码：{error}");
    }

    private static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        var result = ModNoRepeat;

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            result |= ModAlt;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            result |= ModControl;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            result |= ModShift;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Win))
        {
            result |= ModWin;
        }

        return result;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        handled = true;
        switch (wParam.ToInt32())
        {
            case CaptureAreaId:
                _captureArea?.Invoke();
                break;
            case PasteImageId:
                _pasteImage?.Invoke();
                break;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            UnregisterHotKey(_handle, CaptureAreaId);
            UnregisterHotKey(_handle, PasteImageId);
        }

        _source?.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
