using System.Windows.Threading;
using PictureTool.Models;
using PictureTool.Services;
using PictureTool.Views;

namespace PictureTool.Infrastructure;

public sealed class AppCoordinator : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ScreenshotService _screenshots = new();
    private readonly ClipboardImageService _clipboard = new();
    private readonly SettingsService _settingsService = new();
    private AppSettings _settings = new();
    private MainWindow? _mainWindow;
    private TrayService? _tray;
    private HotkeyService? _hotkeys;
    private readonly List<PinWindow> _pins = new();

    public AppCoordinator(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Start()
    {
        TempImageStore.CleanupStale(TimeSpan.FromHours(6));
        _settings = _settingsService.Load();
        _mainWindow = new MainWindow(this);
        _hotkeys = new HotkeyService(_mainWindow);
        _mainWindow.Show();
        _mainWindow.SetHotkeySummary(_settings.Hotkeys);

        _tray = new TrayService(
            showDashboard: ShowDashboard,
            captureArea: StartAreaCapture,
            scrollCapture: StartScrollCapture,
            pasteImage: OpenClipboardImage,
            openSettings: OpenSettings,
            showAllPins: ShowAllPins,
            closeAllPins: CloseAllPins,
            exit: Shutdown);

        ApplyHotkeys();
        _mainWindow.Hide();
        MemoryPressureService.TrimSoon(1000);
    }

    public void ShowDashboard()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
    }

    public void StartAreaCapture()
    {
        StartCaptureOverlay(startInScrollMode: false);
    }

    public void StartScrollCapture()
    {
        StartCaptureOverlay(startInScrollMode: true);
    }

    private void StartCaptureOverlay(bool startInScrollMode)
    {
        _mainWindow?.Hide();
        var frame = _screenshots.CaptureVirtualScreen();
        var overlay = new CaptureOverlayWindow(frame, startInScrollMode);

        overlay.PinRequested += (_, path) => OpenPin(path);
        overlay.ScrollCaptureCompleted += (_, path) => OpenAnnotation(path);
        overlay.Closed += (_, _) => TempImageStore.TryDelete(frame.ImagePath);
        overlay.Show();
    }

    public void OpenClipboardImage()
    {
        var imagePath = _clipboard.TrySaveImageFromClipboard();
        if (imagePath is null)
        {
            ShowDashboard();
            _mainWindow?.SetStatus("剪贴板里没有可用图片。");
            return;
        }

        OpenAnnotation(imagePath);
    }

    public void OpenSettings()
    {
        if (_mainWindow is null)
        {
            return;
        }

        ShowDashboard();

        var window = new SettingsWindow(_settings.Hotkeys)
        {
            Owner = _mainWindow
        };

        if (window.ShowDialog() != true)
        {
            return;
        }

        var previousHotkeys = _settings.Hotkeys.Clone();
        _settings.Hotkeys = window.Hotkeys.Clone();

        var result = ApplyHotkeys(updateStatus: false);
        if (result.HasFailures)
        {
            _settings.Hotkeys = previousHotkeys;
            _mainWindow.SetHotkeySummary(_settings.Hotkeys);
            ApplyHotkeys(updateStatus: false);
            _mainWindow.SetStatus($"快捷键未保存：{string.Join(" ", result.Failures)}");
            return;
        }

        _settingsService.Save(_settings);
        _mainWindow.SetHotkeySummary(_settings.Hotkeys);
        _mainWindow.SetStatus($"快捷键已保存：{_settings.Hotkeys.CaptureArea} 截图，{_settings.Hotkeys.PasteImage} 粘贴图片。");
    }

    private void OpenAnnotation(string imagePath)
    {
        var window = new AnnotationWindow(imagePath);
        window.PinRequested += (_, path) => OpenPin(path);
        window.Show();
        window.Activate();
    }

    private void OpenPin(string imagePath)
    {
        var pin = new PinWindow(imagePath);
        _pins.Add(pin);
        pin.Closed += (_, _) => _pins.Remove(pin);
        pin.Show();
    }

    private void ShowAllPins()
    {
        foreach (var pin in _pins.ToArray())
        {
            pin.Show();
            pin.Activate();
        }
    }

    private void CloseAllPins()
    {
        foreach (var pin in _pins.ToArray())
        {
            pin.Close();
        }
    }

    private HotkeyRegistrationResult ApplyHotkeys(bool updateStatus = true)
    {
        if (_hotkeys is null || _mainWindow is null)
        {
            return HotkeyRegistrationResult.Success();
        }

        var result = _hotkeys.ApplySettings(
            _settings.Hotkeys,
            () => _dispatcher.Invoke(StartAreaCapture),
            () => _dispatcher.Invoke(OpenClipboardImage));

        if (!updateStatus)
        {
            return result;
        }

        if (result.HasFailures)
        {
            _mainWindow.SetStatus(string.Join(" ", result.Failures));
            return result;
        }

        _mainWindow.SetStatus($"快捷键已启用：{_settings.Hotkeys.CaptureArea} 截图，{_settings.Hotkeys.PasteImage} 粘贴图片。");
        return result;
    }

    private void Shutdown()
    {
        CloseAllPins();
        _mainWindow?.AllowClose();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
    }
}
