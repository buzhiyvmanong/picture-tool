using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PictureTool.Infrastructure;
using PictureTool.Models;
using PictureTool.Services;

namespace PictureTool.Views;

public partial class MainWindow : Window
{
    private readonly AppCoordinator _coordinator;
    private bool _allowClose;

    public MainWindow(AppCoordinator coordinator)
    {
        _coordinator = coordinator;
        InitializeComponent();
        AppIconHelper.ApplyWindowIcon(this);
        HistoryList.ItemsSource = coordinator.History.Items;
    }

    public void ApplyPlacement(WindowPlacement? placement)
    {
        if (placement is null) return;
        if (placement.Width > 0) Width = placement.Width;
        if (placement.Height > 0) Height = placement.Height;
        Left = placement.Left;
        Top = placement.Top;
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    public WindowPlacement GetPlacement() => new()
    {
        Left = Left, Top = Top, Width = Width, Height = Height
    };

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

    private void ScrollCapture_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.StartScrollCapture();
    }

    private void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.OpenClipboardImage();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.OpenSettings();
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.History.ClearAll();
    }

    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItem item)
        {
            _coordinator.OpenAnnotationFromHistory(item.FilePath);
        }
    }
}
