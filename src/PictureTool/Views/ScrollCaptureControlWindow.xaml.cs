using System.ComponentModel;
using System.Windows;

namespace PictureTool.Views;

public partial class ScrollCaptureControlWindow : Window
{
    private bool _handledClose;

    public event EventHandler? ManualCaptureRequested;
    public event EventHandler? AutoCaptureRequested;
    public event EventHandler? FinishRequested;
    public event EventHandler? CancelRequested;

    public ScrollCaptureControlWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionAtScreenTop();
    }

    public void SetFrameCount(int count)
    {
        StatusText.Text = $"手动滚动：已截取 {Math.Max(1, count)} 屏";
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    public void SetBusy(bool busy)
    {
        ManualButton.IsEnabled = !busy;
        AutoButton.IsEnabled = !busy;
        FinishButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
    }

    public void CloseSilently()
    {
        _handledClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_handledClose)
        {
            _handledClose = true;
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosing(e);
    }

    private void Manual_Click(object sender, RoutedEventArgs e)
    {
        ManualCaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Auto_Click(object sender, RoutedEventArgs e)
    {
        AutoCaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        FinishRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _handledClose = true;
        CancelRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void PositionAtScreenTop()
    {
        UpdateLayout();
        Left = SystemParameters.VirtualScreenLeft + (SystemParameters.VirtualScreenWidth - ActualWidth) / 2;
        Top = SystemParameters.VirtualScreenTop + 16;
    }
}
