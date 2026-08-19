using System.Windows.Threading;
using PictureTool.Models;
using PictureTool.Services;
using PictureTool.Views;
using DrawingRectangle = System.Drawing.Rectangle;

namespace PictureTool.Infrastructure;

public sealed class AppCoordinator : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ScreenshotService _screenshots = new();
    private readonly ScrollCaptureService _scrollCaptures = new();
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

    private async Task BeginScrollCaptureSession(DrawingRectangle region)
    {
        _mainWindow?.SetStatus("滚动截图准备中...");
        await Task.Delay(180);

        ScrollCaptureService.CaptureSession? session;
        try
        {
            session = await Task.Run(() => _scrollCaptures.StartSession(region));
        }
        catch (Exception ex)
        {
            ShowDashboard();
            _mainWindow?.SetStatus($"滚动截图启动失败：{ex.Message}");
            return;
        }

        var controller = new ScrollCaptureControlWindow();
        controller.SetFrameCount(session.FrameCount);

        controller.ManualCaptureRequested += async (_, _) =>
        {
            if (session is null)
            {
                return;
            }

            controller.SetBusy(true);
            controller.SetStatus("截取中...");
            controller.Hide();
            await Task.Delay(140);

            try
            {
                var added = await Task.Run(() => session.CaptureCurrent());
                controller.Show();
                controller.Activate();

                if (added)
                {
                    controller.SetFrameCount(session.FrameCount);
                }
                else
                {
                    controller.SetStatus($"没有检测到新内容，已截取 {session.FrameCount} 屏");
                }
            }
            catch (Exception ex)
            {
                controller.Show();
                controller.Activate();
                controller.SetStatus($"截取失败：{ex.Message}");
            }
            finally
            {
                if (session is not null)
                {
                    controller.SetBusy(false);
                }
            }
        };

        controller.AutoCaptureRequested += async (_, _) =>
        {
            if (session is null)
            {
                return;
            }

            controller.SetBusy(true);
            controller.SetStatus("自动滚动中...");
            controller.Hide();
            await Task.Delay(140);

            try
            {
                await Task.Run(() => session.CaptureAuto());
                var outputPath = await Task.Run(() => session.Finish());
                session = null;
                controller.CloseSilently();
                _mainWindow?.SetStatus("滚动截图完成。");
                OpenAnnotation(outputPath);
            }
            catch (Exception ex)
            {
                controller.Show();
                controller.Activate();
                controller.SetStatus($"自动滚动失败：{ex.Message}");
                controller.SetBusy(false);
            }
        };

        controller.FinishRequested += async (_, _) =>
        {
            if (session is null)
            {
                return;
            }

            controller.SetBusy(true);
            controller.SetStatus("拼接中...");

            try
            {
                var outputPath = await Task.Run(() => session.Finish());
                session = null;
                controller.CloseSilently();
                _mainWindow?.SetStatus("滚动截图完成。");
                OpenAnnotation(outputPath);
            }
            catch (Exception ex)
            {
                controller.SetStatus($"拼接失败：{ex.Message}");
                controller.SetBusy(false);
            }
        };

        controller.CancelRequested += (_, _) =>
        {
            session?.Dispose();
            session = null;
            _mainWindow?.SetStatus("滚动截图已取消。");
        };

        controller.Show();
        controller.Activate();
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
