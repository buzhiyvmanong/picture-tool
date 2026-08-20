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
    private HistoryService _history = null!;
    public HistoryService History => _history;
    private MainWindow? _mainWindow;
    private TrayService? _tray;
    private HotkeyService? _hotkeys;
    private readonly List<PinWindow> _pins = new();
    private readonly UpdateCheckService _updateChecker = new();

    public AppCoordinator(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Start()
    {
        TempImageStore.CleanupStale(TimeSpan.FromHours(6));
        _settings = _settingsService.Load();
        _history = new HistoryService(_settings.HistoryMaxItems);
        ApplyStartupSetting();
        _mainWindow = new MainWindow(this);
        _mainWindow.ApplyPlacement(_settings.MainWindowPlacement);
        _mainWindow.Title = $"Picture Tool v{_updateChecker.CurrentVersion}";
        _hotkeys = new HotkeyService(_mainWindow);
        _mainWindow.Show();
        _mainWindow.SetHotkeySummary(_settings.Hotkeys);

        _tray = new TrayService(
            showDashboard: ShowDashboard,
            captureArea: StartAreaCapture,
            scrollCapture: StartScrollCapture,
            pasteImage: OpenClipboardImage,
            extractClipboardText: ExtractClipboardText,
            openSettings: OpenSettings,
            showUsageGuide: ShowUsageGuide,
            showAllPins: ShowAllPins,
            closeAllPins: CloseAllPins,
            exit: Shutdown);

        TrayNotificationService.Initialize((title, message) => _tray.ShowBalloon(title, message));
        ApplyHotkeys();
        ShowWelcomeIfNeeded();
        _mainWindow.Hide();
        MemoryPressureService.TrimSoon(1000);
        _ = CheckForUpdatesAsync();
    }

    private void ShowWelcomeIfNeeded()
    {
        if (_settings.HasSeenWelcome || _mainWindow is null)
        {
            return;
        }

        var welcome = new WelcomeWindow(_settings.Hotkeys)
        {
            Owner = _mainWindow
        };
        if (welcome.ShowDialog() == true && welcome.DontShowAgain)
        {
            _settings.HasSeenWelcome = true;
            _settingsService.Save(_settings);
        }
    }

    public void ShowUsageGuide()
    {
        if (_mainWindow is null)
        {
            return;
        }

        ShowDashboard();
        var guide = new WelcomeWindow(_settings.Hotkeys, isReplayMode: true)
        {
            Owner = _mainWindow
        };
        guide.ShowDialog();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (!_settings.CheckUpdatesOnStartup)
        {
            return;
        }

        var result = await _updateChecker.CheckAsync().ConfigureAwait(false);
        if (result.Status != UpdateCheckStatus.UpdateAvailable || result.LatestVersion is null)
        {
            return;
        }

        if (string.Equals(_settings.LastDismissedUpdateVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _dispatcher.InvokeAsync(() =>
        {
            if (_mainWindow is null)
            {
                return;
            }

            var answer = System.Windows.MessageBox.Show(
                _mainWindow,
                $"发现新版本 v{result.LatestVersion}（当前 v{result.CurrentVersion}）。\n是否打开下载页面？",
                "检查更新",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Information);

            if (answer == System.Windows.MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(result.DownloadUrl))
            {
                UpdateCheckService.OpenDownloadPage(result.DownloadUrl);
            }

            if (answer is System.Windows.MessageBoxResult.Yes or System.Windows.MessageBoxResult.No)
            {
                _settings.LastDismissedUpdateVersion = result.LatestVersion;
                _settingsService.Save(_settings);
            }
        });
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
        overlay.CopyCompleted += (_, _) => TrayNotificationService.Show("已复制", "截图已复制到剪贴板");
        overlay.ScrollCaptureCompleted += (_, path) =>
        {
            History.Add(path);
            TrayNotificationService.Show("滚动截图完成", "长图已保存到历史记录");
            OpenAnnotation(path);
        };
        overlay.CaptureCompleted += (_, path) => History.Add(path);
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

    public async void ExtractClipboardText()
    {
        var bitmap = System.Windows.Clipboard.GetImage();
        if (bitmap is null)
        {
            ShowDashboard();
            _mainWindow?.SetStatus("剪贴板里没有可用图片。");
            return;
        }

        await OcrUiHelper.RunAsync(null, bitmap).ConfigureAwait(true);
    }

    public void OpenSettings()
    {
        if (_mainWindow is null)
        {
            return;
        }

        ShowDashboard();

        var window = new SettingsWindow(_settings)
        {
            Owner = _mainWindow
        };

        if (window.ShowDialog() != true)
        {
            return;
        }

        var previousHotkeys = _settings.Hotkeys.Clone();
        var previousStartup = _settings.StartWithWindows;
        _settings = window.Settings.Clone();

        var result = ApplyHotkeys(updateStatus: false);
        if (result.HasFailures)
        {
            _settings.Hotkeys = previousHotkeys;
            _mainWindow.SetHotkeySummary(_settings.Hotkeys);
            ApplyHotkeys(updateStatus: false);
            _mainWindow.SetStatus(string.Join("\n", result.Failures));
            return;
        }

        try
        {
            ApplyStartupSetting();
        }
        catch (Exception ex)
        {
            _settings.StartWithWindows = previousStartup;
            _mainWindow.SetStatus($"开机自启设置失败：{ex.Message}");
            return;
        }

        History.ConfigureMaxItems(_settings.HistoryMaxItems);
        _settingsService.Save(_settings);
        _mainWindow.SetHotkeySummary(_settings.Hotkeys);
        _mainWindow.SetStatus("设置已保存。");
    }

    private void ApplyStartupSetting()
    {
        StartupService.SetEnabled(_settings.StartWithWindows);
    }

    public void OpenAnnotationFromHistory(string imagePath)
    {
        OpenAnnotation(imagePath);
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
        SaveWindowPlacement();
        CloseAllPins();
        _mainWindow?.AllowClose();
        System.Windows.Application.Current.Shutdown();
    }

    private void SaveWindowPlacement()
    {
        if (_mainWindow is null) return;
        _settings.MainWindowPlacement = _mainWindow.GetPlacement();
        _settingsService.Save(_settings);
    }

    public void Dispose()
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
    }
}
