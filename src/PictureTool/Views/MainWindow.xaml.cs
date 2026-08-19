using System.ComponentModel;
using System.Windows;
using PictureTool.Infrastructure;
using PictureTool.Models;

namespace PictureTool.Views;

public partial class MainWindow : Window
{
    private readonly AppCoordinator _coordinator;
    private bool _allowClose;

    public MainWindow(AppCoordinator coordinator)
    {
        _coordinator = coordinator;
        InitializeComponent();
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    public void SetHotkeySummary(HotkeySettings hotkeys)
    {
        HotkeySummaryText.Text = $"快捷键：{hotkeys.CaptureArea} 截图，{hotkeys.PasteImage} 贴图";
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void CaptureArea_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.StartAreaCapture();
    }

    private void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.OpenClipboardImage();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.OpenSettings();
    }
}
