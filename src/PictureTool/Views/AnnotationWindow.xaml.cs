using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PictureTool.Services;
using IoFile = System.IO.File;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfClipboard = System.Windows.Clipboard;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace PictureTool.Views;

public partial class AnnotationWindow : Window
{
    private readonly string _imagePath;
    private BitmapSource? _sourceBitmap;
    private readonly Stack<StrokeCollection> _undoStack = new();
    private readonly double _displayWidth;
    private readonly double _displayHeight;
    private readonly int _renderPixelWidth;
    private readonly int _renderPixelHeight;
    private readonly double _renderDpiX;
    private readonly double _renderDpiY;
    private StrokeCollection? _editBaseline;
    private ToolMode _mode = ToolMode.Pen;
    private WpfPoint? _start;
    private WpfRectangle? _rectanglePreview;

    public event EventHandler<string>? PinRequested;

    private BitmapSource SourceBitmap =>
        _sourceBitmap ?? throw new InvalidOperationException("Annotation image has already been released.");

    public AnnotationWindow(string imagePath)
    {
        _imagePath = imagePath;
        InitializeComponent();

        var bitmap = BitmapLoader.LoadFrozen(imagePath);
        _sourceBitmap = bitmap;
        _displayWidth = bitmap.Width;
        _displayHeight = bitmap.Height;
        _renderPixelWidth = Math.Max(1, bitmap.PixelWidth);
        _renderPixelHeight = Math.Max(1, bitmap.PixelHeight);
        _renderDpiX = bitmap.DpiX;
        _renderDpiY = bitmap.DpiY;

        SourceImage.Source = bitmap;
        SourceImage.Width = _displayWidth;
        SourceImage.Height = _displayHeight;
        AnnotationInkCanvas.Width = _displayWidth;
        AnnotationInkCanvas.Height = _displayHeight;
        ImageHost.Width = _displayWidth;
        ImageHost.Height = _displayHeight;

        ConfigureInkCanvas();
    }

    protected override void OnClosed(EventArgs e)
    {
        SourceImage.Source = null;
        _sourceBitmap = null;
        AnnotationInkCanvas.Strokes.Clear();
        AnnotationInkCanvas.Children.Clear();
        _undoStack.Clear();
        _editBaseline = null;
        TempImageStore.TryDelete(_imagePath);
        MemoryPressureService.TrimSoon();

        base.OnClosed(e);
    }

    private void ConfigureInkCanvas()
    {
        AnnotationInkCanvas.DefaultDrawingAttributes = CreateDrawingAttributes();
        AnnotationInkCanvas.EraserShape = new EllipseStylusShape(18, 18);
        SetMode(ToolMode.Pen);
    }

    private void Pen_Click(object sender, RoutedEventArgs e)
    {
        SetMode(ToolMode.Pen);
    }

    private void Rectangle_Click(object sender, RoutedEventArgs e)
    {
        SetMode(ToolMode.Rectangle);
    }

    private void Eraser_Click(object sender, RoutedEventArgs e)
    {
        SetMode(ToolMode.Eraser);
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        UndoLast();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (AnnotationInkCanvas.Strokes.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        AnnotationInkCanvas.Strokes.Clear();
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        PinCurrent();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        CopyCurrent();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrent();
    }

    private async void Ocr_Click(object sender, RoutedEventArgs e)
    {
        await OcrUiHelper.RunForPathAsync(this, _imagePath).ConfigureAwait(true);
    }

    private void AnnotationWindow_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Z:
                UndoLast();
                e.Handled = true;
                break;
            case Key.C:
                CopyCurrent();
                e.Handled = true;
                break;
            case Key.S:
                SaveCurrent();
                e.Handled = true;
                break;
        }
    }

    private void UndoLast()
    {
        if (!_undoStack.TryPop(out var previous))
        {
            return;
        }

        AnnotationInkCanvas.Strokes = CloneStrokes(previous);
        _editBaseline = null;
    }

    private void PinCurrent()
    {
        PinRequested?.Invoke(this, SaveCurrentBitmapToTemp());
    }

    private void CopyCurrent()
    {
        WpfClipboard.SetImage(RenderCurrentBitmap());
        TrayNotificationService.Show("已复制", "图片已复制到剪贴板");
        MemoryPressureService.TrimSoon();
    }

    private void SaveCurrent()
    {
        var dialog = new SaveFileDialog
        {
            Filter = ImageExportService.SaveFilter,
            FileName = ImageExportService.DefaultFileName()
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ImageExportService.Save(dialog.FileName, RenderCurrentBitmap());
        TrayNotificationService.Show("已保存", System.IO.Path.GetFileName(dialog.FileName));
        MemoryPressureService.TrimSoon();
    }

    private void AnnotationInkCanvas_PreviewMouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        BeginEditSnapshot();

        if (_mode != ToolMode.Rectangle)
        {
            return;
        }

        _start = e.GetPosition(AnnotationInkCanvas);
        _rectanglePreview = new WpfRectangle
        {
            Stroke = WpfBrushes.Red,
            StrokeThickness = 3,
            Fill = WpfBrushes.Transparent
        };

        AnnotationInkCanvas.Children.Add(_rectanglePreview);
        AnnotationInkCanvas.CaptureMouse();
        UpdateRectanglePreview(_start.Value, _start.Value);
        e.Handled = true;
    }

    private void AnnotationInkCanvas_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_mode != ToolMode.Rectangle || _start is null || _rectanglePreview is null)
        {
            return;
        }

        UpdateRectanglePreview(_start.Value, e.GetPosition(AnnotationInkCanvas));
        e.Handled = true;
    }

    private void AnnotationInkCanvas_PreviewMouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
    {
        if (_mode == ToolMode.Rectangle)
        {
            CompleteRectangle(e.GetPosition(AnnotationInkCanvas));
            e.Handled = true;
            return;
        }

        Dispatcher.BeginInvoke((Action)CommitEditSnapshot, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void SetMode(ToolMode mode)
    {
        _mode = mode;

        AnnotationInkCanvas.EditingMode = mode switch
        {
            ToolMode.Pen => InkCanvasEditingMode.Ink,
            ToolMode.Eraser => InkCanvasEditingMode.EraseByPoint,
            _ => InkCanvasEditingMode.None
        };
    }

    private void CompleteRectangle(WpfPoint current)
    {
        if (_start is null || _rectanglePreview is null)
        {
            CommitEditSnapshot();
            return;
        }

        var bounds = CreateRect(_start.Value, current);
        AnnotationInkCanvas.Children.Remove(_rectanglePreview);
        AnnotationInkCanvas.ReleaseMouseCapture();

        if (bounds.Width >= 4 && bounds.Height >= 4)
        {
            AnnotationInkCanvas.Strokes.Add(CreateRectangleStroke(bounds));
        }

        _start = null;
        _rectanglePreview = null;
        CommitEditSnapshot();
    }

    private void UpdateRectanglePreview(WpfPoint start, WpfPoint current)
    {
        if (_rectanglePreview is null)
        {
            return;
        }

        var bounds = CreateRect(start, current);
        InkCanvas.SetLeft(_rectanglePreview, bounds.X);
        InkCanvas.SetTop(_rectanglePreview, bounds.Y);
        _rectanglePreview.Width = bounds.Width;
        _rectanglePreview.Height = bounds.Height;
    }

    private RenderTargetBitmap RenderCurrentBitmap()
    {
        var bitmap = new RenderTargetBitmap(
            _renderPixelWidth,
            _renderPixelHeight,
            _renderDpiX,
            _renderDpiY,
            PixelFormats.Pbgra32);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(SourceBitmap, new WpfRect(0, 0, _displayWidth, _displayHeight));
            AnnotationInkCanvas.Strokes.Draw(context);
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private void BeginEditSnapshot()
    {
        _editBaseline ??= CloneStrokes(AnnotationInkCanvas.Strokes);
    }

    private void CommitEditSnapshot()
    {
        if (_editBaseline is null)
        {
            return;
        }

        if (!AreStrokesEquivalent(_editBaseline, AnnotationInkCanvas.Strokes))
        {
            _undoStack.Push(_editBaseline);
        }

        _editBaseline = null;
    }

    private void PushUndoSnapshot()
    {
        _undoStack.Push(CloneStrokes(AnnotationInkCanvas.Strokes));
        _editBaseline = null;
    }

    private Stroke CreateRectangleStroke(WpfRect bounds)
    {
        var points = new StylusPointCollection
        {
            new StylusPoint(bounds.Left, bounds.Top),
            new StylusPoint(bounds.Right, bounds.Top),
            new StylusPoint(bounds.Right, bounds.Bottom),
            new StylusPoint(bounds.Left, bounds.Bottom),
            new StylusPoint(bounds.Left, bounds.Top)
        };

        return new Stroke(points, CreateDrawingAttributes());
    }

    private static DrawingAttributes CreateDrawingAttributes()
    {
        return new DrawingAttributes
        {
            Color = Colors.Red,
            Width = 3,
            Height = 3,
            FitToCurve = false,
            StylusTip = StylusTip.Ellipse
        };
    }

    private static StrokeCollection CloneStrokes(StrokeCollection strokes)
    {
        return new StrokeCollection(strokes.Select(stroke => stroke.Clone()));
    }

    private static bool AreStrokesEquivalent(StrokeCollection left, StrokeCollection right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (left[index].StylusPoints.Count != right[index].StylusPoints.Count)
            {
                return false;
            }

            if (!left[index].GetBounds().Equals(right[index].GetBounds()))
            {
                return false;
            }
        }

        return true;
    }

    private static WpfRect CreateRect(WpfPoint start, WpfPoint current)
    {
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);
        return new WpfRect(x, y, width, height);
    }

    private string SaveCurrentBitmapToTemp()
    {
        var path = TempImageStore.CreatePngPath();

        using var stream = IoFile.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(RenderCurrentBitmap()));
        encoder.Save(stream);

        return path;
    }

    private enum ToolMode
    {
        Pen,
        Rectangle,
        Eraser
    }
}
