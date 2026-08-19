using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PictureTool.Models;
using PictureTool.Services;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace PictureTool.Views;

public partial class ScrollRegionOverlayWindow : Window
{
    private readonly ScreenshotFrame _frame;
    private WpfPoint? _start;

    public event EventHandler<DrawingRectangle>? SelectionCompleted;
    public event EventHandler? CaptureCanceled;

    public ScrollRegionOverlayWindow(ScreenshotFrame frame)
    {
        _frame = frame;
        InitializeComponent();

        Left = frame.DisplayBounds.X;
        Top = frame.DisplayBounds.Y;
        Width = frame.DisplayBounds.Width;
        Height = frame.DisplayBounds.Height;

        RootCanvas.Width = frame.DisplayBounds.Width;
        RootCanvas.Height = frame.DisplayBounds.Height;
        ScreenshotImage.Width = frame.DisplayBounds.Width;
        ScreenshotImage.Height = frame.DisplayBounds.Height;
        DimLayer.Width = frame.DisplayBounds.Width;
        DimLayer.Height = frame.DisplayBounds.Height;
        ScreenshotImage.Source = BitmapLoader.LoadFrozen(frame.ImagePath);

        Loaded += (_, _) => RootCanvas.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        ScreenshotImage.Source = null;
        MemoryPressureService.TrimSoon();
        base.OnClosed(e);
    }

    private void RootCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(RootCanvas);
        SelectionRect.Visibility = Visibility.Visible;
        SizeBadge.Visibility = Visibility.Visible;
        RootCanvas.CaptureMouse();
        UpdateSelection(_start.Value, _start.Value);
    }

    private void RootCanvas_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_start is null)
        {
            return;
        }

        UpdateSelection(_start.Value, e.GetPosition(RootCanvas));
    }

    private void RootCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is null)
        {
            return;
        }

        RootCanvas.ReleaseMouseCapture();
        var selection = CreatePixelRect(_start.Value, e.GetPosition(RootCanvas));

        if (selection.Width >= 32 && selection.Height >= 32)
        {
            SelectionCompleted?.Invoke(this, selection);
        }
        else
        {
            CaptureCanceled?.Invoke(this, EventArgs.Empty);
        }

        Close();
    }

    private void RootCanvas_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CaptureCanceled?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void UpdateSelection(WpfPoint start, WpfPoint current)
    {
        var rect = CreateLocalRect(start, current);
        Canvas.SetLeft(SelectionRect, rect.X);
        Canvas.SetTop(SelectionRect, rect.Y);
        SelectionRect.Width = rect.Width;
        SelectionRect.Height = rect.Height;

        var pixelWidth = Math.Max(1, (int)Math.Round(rect.Width * _frame.PixelBounds.Width / _frame.DisplayBounds.Width));
        var pixelHeight = Math.Max(1, (int)Math.Round(rect.Height * _frame.PixelBounds.Height / _frame.DisplayBounds.Height));
        SizeBadge.Text = $"{pixelWidth} * {pixelHeight}";
        SizeBadge.UpdateLayout();
        Canvas.SetLeft(SizeBadge, rect.X);
        Canvas.SetTop(SizeBadge, Math.Max(4, rect.Y - SizeBadge.ActualHeight - 6));
    }

    private DrawingRectangle CreatePixelRect(WpfPoint start, WpfPoint current)
    {
        var local = CreateLocalRect(start, current);
        var scaleX = _frame.PixelBounds.Width / _frame.DisplayBounds.Width;
        var scaleY = _frame.PixelBounds.Height / _frame.DisplayBounds.Height;

        var left = _frame.PixelBounds.Left + (int)Math.Round(local.X * scaleX);
        var top = _frame.PixelBounds.Top + (int)Math.Round(local.Y * scaleY);
        var width = Math.Max(1, (int)Math.Round(local.Width * scaleX));
        var height = Math.Max(1, (int)Math.Round(local.Height * scaleY));

        return new DrawingRectangle(left, top, width, height);
    }

    private static Rect CreateLocalRect(WpfPoint start, WpfPoint current)
    {
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);
        return new Rect(x, y, width, height);
    }
}
