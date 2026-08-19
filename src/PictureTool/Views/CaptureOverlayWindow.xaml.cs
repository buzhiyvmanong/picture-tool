using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PictureTool.Models;
using PictureTool.Services;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using IoFile = System.IO.File;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfClipboard = System.Windows.Clipboard;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace PictureTool.Views;

public partial class CaptureOverlayWindow : Window
{
    private const double MinSelectionSize = 8;
    private const double HandleSize = 12;
    private const double DefaultTextFontSize = 24;
    private const double MosaicBlockSize = 12;
    private const double OverlayEraserRadius = 12;
    private const double ScrollPreviewGap = 14;
    private const double ScrollPreviewMinWidth = 180;
    private const double ScrollPreviewMaxWidth = 360;
    private const int AutoScrollWheelDelta = ScrollCaptureSettings.AutoScrollWheelDelta;
    private const int AutoScrollFrameDelayMs = ScrollCaptureSettings.AutoScrollFrameDelayMs;
    private const int AutoScrollRetryDelayMs = ScrollCaptureSettings.AutoScrollRetryDelayMs;
    private const int AutoScrollMaxRetryCount = ScrollCaptureSettings.AutoScrollMaxRetryCount;
    private const int ManualCaptureSettleMs = ScrollCaptureSettings.ManualCaptureSettleMs;
    private const string ManualScrollTooFastWarning = ScrollCaptureSettings.ManualScrollTooFastWarning;
    private const string ManualScrollBusyWarning = ScrollCaptureSettings.ManualScrollBusyWarning;
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const int WmNchitTest = 0x0084;
    private const int HtTransparent = -1;
    private const uint WdaNone = 0x00000000;
    private const uint WdaExcludeFromCapture = 0x00000011;
    private const int DwmwaExcludedFromCapture = 34;

    private readonly ScreenshotFrame _frame;
    private readonly ScrollCaptureService _scrollCaptures = new();
    private BitmapSource? _sourceBitmap;
    private readonly double _scaleX;
    private readonly double _scaleY;
    private readonly Stack<AnnotationState> _undoStack = new();
    private readonly List<OverlayItem> _overlayItems = new();
    private readonly List<UIElement> _overlayElements = new();

    private WpfRect _selection = WpfRect.Empty;
    private WpfRect _interactionStartSelection = WpfRect.Empty;
    private WpfPoint _interactionStartPoint;
    private WpfPoint? _shapeStart;
    private Shape? _shapePreview;
    private AnnotationState? _editBaseline;
    private WpfTextBox? _activeTextBox;
    private WpfPoint _activeTextPoint;
    private WpfColor _activeTextColor;
    private double _activeTextFontSize = DefaultTextFontSize;
    private WpfColor _strokeColor = WpfColor.FromRgb(240, 68, 56);
    private double _strokeWidth = 4;
    private double _textFontSize = DefaultTextFontSize;
    private int _numberCounter;
    private ScrollCaptureService.CaptureSession? _scrollSession;
    private ScrollCaptureController? _scrollController;
    private HwndSource? _hwndSource;
    private bool _scrollWarningActive;
    private CancellationTokenSource? _autoScrollCancellation;
    private DrawingRectangle _scrollRegion;
    private CaptureMode _captureMode = CaptureMode.Screenshot;
    private InteractionMode _interaction = InteractionMode.None;
    private ResizeHandle _resizeHandle = ResizeHandle.None;
    private ToolMode _toolMode = ToolMode.Move;
    private bool _hasSelection;
    private bool _isScrollSessionActive;
    private bool _isScrollBusy;
    private bool _isAutoScrolling;
    private bool _isAutoScrollPaused;
    private bool _isCompleting;
    private bool _scrollPassThroughActive;

    public event EventHandler<string>? PinRequested;
    public event EventHandler<string>? ScrollCaptureCompleted;
    public event EventHandler<string>? CaptureCompleted;
    public event EventHandler? CaptureCanceled;

    private BitmapSource SourceBitmap =>
        _sourceBitmap ?? throw new InvalidOperationException("Screenshot image has already been released.");

    public CaptureOverlayWindow(ScreenshotFrame frame, bool startInScrollMode = false)
    {
        _frame = frame;
        _scaleX = frame.PixelBounds.Width / frame.DisplayBounds.Width;
        _scaleY = frame.PixelBounds.Height / frame.DisplayBounds.Height;

        InitializeComponent();

        Left = frame.DisplayBounds.X;
        Top = frame.DisplayBounds.Y;
        Width = frame.DisplayBounds.Width;
        Height = frame.DisplayBounds.Height;

        RootCanvas.Width = frame.DisplayBounds.Width;
        RootCanvas.Height = frame.DisplayBounds.Height;

        _sourceBitmap = BitmapLoader.LoadFrozen(frame.ImagePath);
        ScreenshotImage.Width = frame.DisplayBounds.Width;
        ScreenshotImage.Height = frame.DisplayBounds.Height;
        ScreenshotImage.Source = _sourceBitmap;

        AnnotationInkCanvas.Width = frame.DisplayBounds.Width;
        AnnotationInkCanvas.Height = frame.DisplayBounds.Height;
        AnnotationInkCanvas.DefaultDrawingAttributes = CreateDrawingAttributes();
        AnnotationInkCanvas.EraserShape = new EllipseStylusShape(18, 18);

        Loaded += (_, _) =>
        {
            PositionModeBar();
            RootCanvas.Focus();
            EnsureHwndSourceHook();
        };
        SizeChanged += (_, _) => PositionModeBar();
        SetTool(ToolMode.Move);
        SetCaptureMode(startInScrollMode ? CaptureMode.Scroll : CaptureMode.Screenshot);
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelActiveTextBox();
        StopAutoScroll();
        StopManualScrollCapture();
        SetOverlayExcludedFromCapture(false);
        _hwndSource?.RemoveHook(HwndSourceHook);
        _hwndSource = null;
        _scrollSession?.Dispose();
        _scrollSession = null;
        ScrollPreviewImage.Source = null;
        ScrollPreviewPanel.Visibility = Visibility.Collapsed;
        ScreenshotImage.Source = null;
        _sourceBitmap = null;
        AnnotationInkCanvas.Strokes.Clear();
        AnnotationInkCanvas.Children.Clear();
        _undoStack.Clear();
        _editBaseline = null;
        _shapePreview = null;
        _activeTextBox = null;
        _overlayElements.Clear();
        _overlayItems.Clear();
        MemoryPressureService.TrimSoon();

        base.OnClosed(e);
    }

    private void RootCanvas_PreviewMouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (IsToolbarSource(source) || IsSelectionChromeSource(source))
        {
            return;
        }

        if (_isScrollSessionActive)
        {
            e.Handled = true;
            return;
        }

        CommitActiveTextBox();

        var point = e.GetPosition(RootCanvas);
        if (_hasSelection && _selection.Contains(point) && _toolMode != ToolMode.Move)
        {
            return;
        }

        BeginSelection(point);
        e.Handled = true;
    }

    private void RootCanvas_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_interaction == InteractionMode.None)
        {
            return;
        }

        var point = e.GetPosition(RootCanvas);
        switch (_interaction)
        {
            case InteractionMode.Selecting:
                SetSelection(CreateRect(_interactionStartPoint, point));
                break;
            case InteractionMode.Moving:
                MoveSelection(point);
                break;
            case InteractionMode.Resizing:
                ResizeSelection(point);
                break;
        }

        e.Handled = true;
    }

    private void RootCanvas_PreviewMouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
    {
        if (_interaction == InteractionMode.None)
        {
            return;
        }

        if (RootCanvas.IsMouseCaptured)
        {
            RootCanvas.ReleaseMouseCapture();
        }

        if (_interaction == InteractionMode.Selecting)
        {
            if (_selection.Width < MinSelectionSize || _selection.Height < MinSelectionSize)
            {
                HideSelection();
            }
            else
            {
                if (_captureMode == CaptureMode.Scroll)
                {
                    _ = EnterScrollCaptureModeAsync();
                }
                else
                {
                    EnterEditMode();
                }
            }
        }

        _interaction = InteractionMode.None;
        _resizeHandle = ResizeHandle.None;
        e.Handled = true;
    }

    private async void RootCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_isScrollSessionActive || _scrollController is null || _scrollSession is null)
        {
            return;
        }

        var wheelPoint = e.GetPosition(RootCanvas);
        if (!_selection.Contains(wheelPoint))
        {
            return;
        }

        if (IsToolbarSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        e.Handled = true;
        if (_isScrollBusy || (_isAutoScrolling && !_isAutoScrollPaused))
        {
            return;
        }

        HideScrollWarning();
        await RunManualScrollCaptureAsync(e.Delta, ToAbsolutePixelPoint(wheelPoint));
    }

    private void RootCanvas_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelCapture();
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
                CopyAndClose();
                e.Handled = true;
                break;
            case Key.S:
                SaveSelected();
                e.Handled = true;
                break;
        }
    }

    private void MoveSurface_MouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        if (!_hasSelection || _toolMode != ToolMode.Move)
        {
            return;
        }

        _interactionStartPoint = e.GetPosition(RootCanvas);
        _interactionStartSelection = _selection;
        _interaction = InteractionMode.Moving;
        RootCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeHandle_MouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        if (!_hasSelection || _toolMode != ToolMode.Move)
        {
            return;
        }

        _resizeHandle = ParseResizeHandle((sender as FrameworkElement)?.Tag?.ToString());
        if (_resizeHandle == ResizeHandle.None)
        {
            return;
        }

        _interactionStartPoint = e.GetPosition(RootCanvas);
        _interactionStartSelection = _selection;
        _interaction = InteractionMode.Resizing;
        RootCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void AnnotationInkCanvas_PreviewMouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        if (!_hasSelection)
        {
            return;
        }

        var point = e.GetPosition(RootCanvas);
        if (!_selection.Contains(point))
        {
            e.Handled = true;
            return;
        }

        CommitActiveTextBox();

        if (_toolMode == ToolMode.Eraser)
        {
            BeginEditSnapshot();
            EraseOverlayItemsAt(point);
            return;
        }

        if (_toolMode == ToolMode.Text)
        {
            ShowTextEditor(point);
            e.Handled = true;
            return;
        }

        if (_toolMode == ToolMode.NumberMarker)
        {
            BeginEditSnapshot();
            _numberCounter++;
            _overlayItems.Add(new NumberMarkerOverlayItem(point, _numberCounter, _strokeColor));
            RebuildOverlayElements();
            CommitEditSnapshot();
            e.Handled = true;
            return;
        }

        BeginEditSnapshot();

        if (!IsDragOverlayTool(_toolMode))
        {
            return;
        }

        _shapeStart = point;
        _shapePreview = CreateShapePreview(point);
        AnnotationInkCanvas.Children.Add(_shapePreview);
        AnnotationInkCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void AnnotationInkCanvas_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_toolMode == ToolMode.Eraser && e.LeftButton == MouseButtonState.Pressed)
        {
            EraseOverlayItemsAt(e.GetPosition(RootCanvas));
        }

        if (_shapeStart is null || _shapePreview is null)
        {
            return;
        }

        UpdateShapePreview(_shapeStart.Value, e.GetPosition(RootCanvas));
        e.Handled = true;
    }

    private void AnnotationInkCanvas_PreviewMouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
    {
        if (IsDragOverlayTool(_toolMode))
        {
            CompleteShape(e.GetPosition(RootCanvas));
            e.Handled = true;
            return;
        }

        Dispatcher.BeginInvoke((Action)CommitEditSnapshot, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void Move_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Move);

    private void Rectangle_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Rectangle);

    private void Ellipse_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Ellipse);

    private void Line_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Line);

    private void Arrow_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Arrow);

    private void Highlight_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Highlight);

    private void NumberMarker_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.NumberMarker);

    private void Pen_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Pen);

    private void Text_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Text);

    private void Mosaic_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Mosaic);

    private void Eraser_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Eraser);

    private async void ScrollCapture_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasSelection || _isScrollSessionActive)
        {
            return;
        }

        await EnterScrollCaptureModeAsync();
    }

    private async void AutoScroll_Click(object sender, RoutedEventArgs e)
    {
        if (_isAutoScrolling && !_isAutoScrollPaused)
        {
            _isAutoScrollPaused = true;
            EnableScrollPassThrough();
            UpdateScrollControls();
            return;
        }

        await RunAutoScrollCaptureAsync();
    }

    private async void CancelScroll_Click(object sender, RoutedEventArgs e)
    {
        StopManualScrollCapture();
        StopAutoScroll();
        await WaitForScrollIdleAsync();
        CancelCapture();
    }

    private async void SaveScroll_Click(object sender, RoutedEventArgs e)
    {
        StopAutoScroll();
        await WaitForScrollIdleAsync();
        await SaveScrollCaptureAsync();
    }

    private async void AnnotateScroll_Click(object sender, RoutedEventArgs e)
    {
        StopAutoScroll();
        await WaitForScrollIdleAsync();
        await FinishScrollCaptureAsync(openAnnotation: true);
    }

    private async void FinishScroll_Click(object sender, RoutedEventArgs e)
    {
        StopManualScrollCapture();
        StopAutoScroll();
        await WaitForScrollIdleAsync();
        await FinishScrollCaptureAsync(openAnnotation: false, copyToClipboard: true);
    }

    private void ScreenshotMode_Click(object sender, RoutedEventArgs e) => SetCaptureMode(CaptureMode.Screenshot);

    private void ScrollMode_Click(object sender, RoutedEventArgs e) => SetCaptureMode(CaptureMode.Scroll);

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string value })
        {
            return;
        }

        try
        {
            _strokeColor = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(value)!;
            if (_toolMode == ToolMode.Text)
            {
                _activeTextColor = _strokeColor;
                if (_activeTextBox is not null)
                {
                    _activeTextBox.Foreground = new SolidColorBrush(_activeTextColor);
                }
            }

            ApplyDrawingAttributes();
            UpdateToolbarState();
        }
        catch
        {
            // Ignore invalid color tags in the lightweight toolbar.
        }
    }

    private void StrokeWidth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string value } || !double.TryParse(value, out var width))
        {
            return;
        }

        _strokeWidth = Math.Clamp(width, 1, 18);
        ApplyDrawingAttributes();
        UpdateToolbarState();
    }

    private void TextSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TextSizeComboBox?.SelectedItem is not ComboBoxItem { Tag: string value }
            || !double.TryParse(value, out var size))
        {
            return;
        }

        _textFontSize = Math.Clamp(size, 12, 72);
        if (_toolMode == ToolMode.Text)
        {
            _activeTextFontSize = _textFontSize;
            if (_activeTextBox is not null)
            {
                _activeTextBox.FontSize = _activeTextFontSize;
            }
        }
    }

    private void SyncTextSizeComboBox()
    {
        if (TextSizeComboBox is null)
        {
            return;
        }

        foreach (var item in TextSizeComboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string value
                && double.TryParse(value, out var size)
                && Math.Abs(size - _textFontSize) < 0.1)
            {
                TextSizeComboBox.SelectedItem = item;
                return;
            }
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => UndoLast();

    private void Save_Click(object sender, RoutedEventArgs e) => SaveSelected();

    private void Copy_Click(object sender, RoutedEventArgs e) => CopyAndClose();

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasSelection)
        {
            return;
        }

        PinRequested?.Invoke(this, SaveSelectedToTemp());
        CompleteAndClose();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelCapture();

    private void Accept_Click(object sender, RoutedEventArgs e) => CopyAndClose();

    private async void Ocr_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasSelection) return;
        var bitmap = RenderSelectedBitmap();
        if (bitmap is null) return;
        await OcrUiHelper.RunAsync(this, bitmap).ConfigureAwait(true);
    }

    private void BeginSelection(WpfPoint point)
    {
        ScreenshotImage.Visibility = Visibility.Visible;
        SelectionRect.IsHitTestVisible = false;
        AnnotationInkCanvas.Strokes.Clear();
        AnnotationInkCanvas.Children.Clear();
        _overlayItems.Clear();
        _overlayElements.Clear();
        _undoStack.Clear();
        _editBaseline = null;
        _activeTextBox = null;
        _shapeStart = null;
        _shapePreview = null;
        _hasSelection = false;
        _isScrollSessionActive = false;
        StopAutoScroll();
        _scrollSession?.Dispose();
        _scrollSession = null;
        ScrollPreviewImage.Source = null;
        ScrollPreviewPanel.Visibility = Visibility.Collapsed;
        ScrollWarningText.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Collapsed;
        ScrollToolbar.Visibility = Visibility.Collapsed;
        ToolOptionsBar.Visibility = Visibility.Collapsed;
        AnnotationInkCanvas.Visibility = Visibility.Collapsed;
        ModeBar.Visibility = Visibility.Collapsed;

        _interactionStartPoint = ClampPoint(point);
        _selection = new WpfRect(_interactionStartPoint, _interactionStartPoint);
        _interaction = InteractionMode.Selecting;
        RootCanvas.CaptureMouse();
        ShowSelectionVisuals();
        UpdateSelectionVisuals();
    }

    private void EnterEditMode()
    {
        _hasSelection = true;
        ScreenshotImage.Visibility = Visibility.Visible;
        SelectionRect.IsHitTestVisible = false;
        AnnotationInkCanvas.Visibility = Visibility.Visible;
        SetTool(ToolMode.Move);
        UpdateSelectionVisuals();
        ModeBar.Visibility = Visibility.Collapsed;
        ScrollPreviewPanel.Visibility = Visibility.Collapsed;
        ScrollWarningText.Visibility = Visibility.Collapsed;
        ScrollToolbar.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke((Action)PositionToolbar, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void HideSelection()
    {
        _hasSelection = false;
        _selection = WpfRect.Empty;
        ScreenshotImage.Visibility = Visibility.Visible;
        SelectionRect.IsHitTestVisible = false;
        AnnotationInkCanvas.Visibility = Visibility.Collapsed;
        SelectionChrome.Visibility = Visibility.Collapsed;
        SelectionRect.Visibility = Visibility.Collapsed;
        SizeBadge.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Collapsed;
        ScrollToolbar.Visibility = Visibility.Collapsed;
        ToolOptionsBar.Visibility = Visibility.Collapsed;
        ScrollPreviewImage.Source = null;
        ScrollPreviewPanel.Visibility = Visibility.Collapsed;
        ScrollWarningText.Visibility = Visibility.Collapsed;
        ModeBar.Visibility = Visibility.Collapsed;
        SetDimVisibility(Visibility.Collapsed);
    }

    private void SetSelection(WpfRect rect)
    {
        var bounds = new WpfRect(0, 0, RootCanvas.Width, RootCanvas.Height);
        rect.Intersect(bounds);
        _selection = rect;
        UpdateSelectionVisuals();
    }

    private void MoveSelection(WpfPoint current)
    {
        var delta = current - _interactionStartPoint;
        var x = Math.Clamp(_interactionStartSelection.X + delta.X, 0, RootCanvas.Width - _interactionStartSelection.Width);
        var y = Math.Clamp(_interactionStartSelection.Y + delta.Y, 0, RootCanvas.Height - _interactionStartSelection.Height);
        _selection = new WpfRect(x, y, _interactionStartSelection.Width, _interactionStartSelection.Height);
        UpdateSelectionVisuals();
    }

    private void ResizeSelection(WpfPoint current)
    {
        var left = _interactionStartSelection.Left;
        var top = _interactionStartSelection.Top;
        var right = _interactionStartSelection.Right;
        var bottom = _interactionStartSelection.Bottom;
        var point = ClampPoint(current);

        if (_resizeHandle is ResizeHandle.TopLeft or ResizeHandle.Left or ResizeHandle.BottomLeft)
        {
            left = Math.Min(point.X, right - MinSelectionSize);
        }

        if (_resizeHandle is ResizeHandle.TopLeft or ResizeHandle.Top or ResizeHandle.TopRight)
        {
            top = Math.Min(point.Y, bottom - MinSelectionSize);
        }

        if (_resizeHandle is ResizeHandle.TopRight or ResizeHandle.Right or ResizeHandle.BottomRight)
        {
            right = Math.Max(point.X, left + MinSelectionSize);
        }

        if (_resizeHandle is ResizeHandle.BottomLeft or ResizeHandle.Bottom or ResizeHandle.BottomRight)
        {
            bottom = Math.Max(point.Y, top + MinSelectionSize);
        }

        left = Math.Clamp(left, 0, RootCanvas.Width - MinSelectionSize);
        top = Math.Clamp(top, 0, RootCanvas.Height - MinSelectionSize);
        right = Math.Clamp(right, left + MinSelectionSize, RootCanvas.Width);
        bottom = Math.Clamp(bottom, top + MinSelectionSize, RootCanvas.Height);

        _selection = new WpfRect(left, top, right - left, bottom - top);
        UpdateSelectionVisuals();
    }

    private void ShowSelectionVisuals()
    {
        SetDimVisibility(Visibility.Visible);
        SelectionRect.Visibility = Visibility.Visible;
        SizeBadge.Visibility = Visibility.Visible;
        SelectionChrome.Visibility = Visibility.Visible;
    }

    private void UpdateSelectionVisuals()
    {
        if (_selection.IsEmpty)
        {
            return;
        }

        PositionRect(SelectionRect, _selection);
        PositionDimRects();
        PositionSelectionChrome();
        PositionSizeBadge();
        PositionScrollPreview();
        PositionScrollWarning();
        PositionToolbar();
        PositionScrollToolbar();
        AnnotationInkCanvas.Clip = new RectangleGeometry(_selection);
    }

    private void PositionDimRects()
    {
        var width = RootCanvas.Width;
        var height = RootCanvas.Height;

        PositionRect(DimTop, new WpfRect(0, 0, width, _selection.Top));
        PositionRect(DimLeft, new WpfRect(0, _selection.Top, _selection.Left, _selection.Height));
        PositionRect(DimRight, new WpfRect(_selection.Right, _selection.Top, Math.Max(0, width - _selection.Right), _selection.Height));
        PositionRect(DimBottom, new WpfRect(0, _selection.Bottom, width, Math.Max(0, height - _selection.Bottom)));
    }

    private void PositionSelectionChrome()
    {
        Canvas.SetLeft(SelectionChrome, _selection.Left);
        Canvas.SetTop(SelectionChrome, _selection.Top);
        SelectionChrome.Width = _selection.Width;
        SelectionChrome.Height = _selection.Height;

        MoveSurface.Width = _selection.Width;
        MoveSurface.Height = _selection.Height;

        PositionHandle(HandleTopLeft, 0, 0);
        PositionHandle(HandleTop, _selection.Width / 2, 0);
        PositionHandle(HandleTopRight, _selection.Width, 0);
        PositionHandle(HandleRight, _selection.Width, _selection.Height / 2);
        PositionHandle(HandleBottomRight, _selection.Width, _selection.Height);
        PositionHandle(HandleBottom, _selection.Width / 2, _selection.Height);
        PositionHandle(HandleBottomLeft, 0, _selection.Height);
        PositionHandle(HandleLeft, 0, _selection.Height / 2);
    }

    private void PositionSizeBadge()
    {
        SizeBadge.Text = $"{Math.Max(1, (int)Math.Round(_selection.Width * _scaleX))} * {Math.Max(1, (int)Math.Round(_selection.Height * _scaleY))}";
        SizeBadge.UpdateLayout();

        var x = Math.Clamp(_selection.Left, 0, Math.Max(0, RootCanvas.Width - SizeBadge.ActualWidth));
        var y = _selection.Top - SizeBadge.ActualHeight - 6;
        if (y < 0)
        {
            y = _selection.Top + 6;
        }

        Canvas.SetLeft(SizeBadge, x);
        Canvas.SetTop(SizeBadge, y);
    }

    private void PositionScrollPreview()
    {
        if (ScrollPreviewPanel.Visibility != Visibility.Visible || _selection.IsEmpty)
        {
            return;
        }

        PositionRect(ScrollPreviewPanel, GetScrollPreviewRect());
    }

    private WpfRect GetScrollPreviewRect()
    {
        var edge = 8.0;
        var desiredWidth = Math.Clamp(_selection.Width * 0.45, ScrollPreviewMinWidth, ScrollPreviewMaxWidth);
        var desiredHeight = Math.Clamp(_selection.Height, 120, Math.Max(120, RootCanvas.Height - edge * 2));
        var top = Math.Clamp(_selection.Top, edge, Math.Max(edge, RootCanvas.Height - desiredHeight - edge));

        var rightSpace = RootCanvas.Width - _selection.Right - ScrollPreviewGap - edge;
        if (rightSpace >= ScrollPreviewMinWidth)
        {
            var width = Math.Min(desiredWidth, rightSpace);
            return new WpfRect(_selection.Right + ScrollPreviewGap, top, width, desiredHeight);
        }

        var leftSpace = _selection.Left - ScrollPreviewGap - edge;
        if (leftSpace >= ScrollPreviewMinWidth)
        {
            var width = Math.Min(desiredWidth, leftSpace);
            return new WpfRect(_selection.Left - ScrollPreviewGap - width, top, width, desiredHeight);
        }

        var bottomSpace = RootCanvas.Height - _selection.Bottom - ScrollPreviewGap - edge;
        if (bottomSpace >= 120)
        {
            var width = Math.Min(Math.Max(ScrollPreviewMinWidth, _selection.Width), RootCanvas.Width - edge * 2);
            var x = Math.Clamp(_selection.Left, edge, Math.Max(edge, RootCanvas.Width - width - edge));
            var height = Math.Min(240, bottomSpace);
            return new WpfRect(x, _selection.Bottom + ScrollPreviewGap, width, height);
        }

        var topSpace = _selection.Top - ScrollPreviewGap - edge;
        var fallbackHeight = Math.Max(80, Math.Min(200, topSpace));
        var fallbackWidth = Math.Min(Math.Max(ScrollPreviewMinWidth, _selection.Width), RootCanvas.Width - edge * 2);
        var fallbackX = Math.Clamp(_selection.Left, edge, Math.Max(edge, RootCanvas.Width - fallbackWidth - edge));
        var fallbackY = Math.Max(edge, _selection.Top - ScrollPreviewGap - fallbackHeight);
        return new WpfRect(fallbackX, fallbackY, fallbackWidth, fallbackHeight);
    }

    private void PositionScrollWarning()
    {
        if (ScrollWarningText.Visibility != Visibility.Visible || _selection.IsEmpty)
        {
            return;
        }

        ScrollWarningText.MaxWidth = Math.Max(220, Math.Min(480, RootCanvas.Width - 16));
        ScrollWarningText.UpdateLayout();

        var width = Math.Max(220, ScrollWarningText.ActualWidth);
        var x = _selection.Left + (_selection.Width - width) / 2;
        x = Math.Clamp(x, 8, Math.Max(8, RootCanvas.Width - width - 8));

        var y = _selection.Top - ScrollWarningText.ActualHeight - 10;
        if (y < 8)
        {
            y = Math.Min(_selection.Bottom - ScrollWarningText.ActualHeight - 10, _selection.Top + 12);
        }

        if (y + ScrollWarningText.ActualHeight > RootCanvas.Height - 8)
        {
            y = Math.Max(8, RootCanvas.Height - ScrollWarningText.ActualHeight - 8);
        }

        Canvas.SetLeft(ScrollWarningText, x);
        Canvas.SetTop(ScrollWarningText, y);
        System.Windows.Controls.Panel.SetZIndex(ScrollWarningText, 2000);
    }

    private void PositionModeBar()
    {
        if (ModeBar is null)
        {
            return;
        }

        ModeBar.UpdateLayout();
        var width = ModeBar.ActualWidth > 0 ? ModeBar.ActualWidth : 220;
        var x = System.Math.Max(8, (RootCanvas.Width - width) / 2);
        Canvas.SetLeft(ModeBar, x);
    }

    private void PositionToolbar()
    {
        if (Toolbar.Visibility != Visibility.Visible || _selection.IsEmpty)
        {
            return;
        }

        Toolbar.UpdateLayout();
        var toolbarWidth = Toolbar.ActualWidth;
        var toolbarHeight = Toolbar.ActualHeight;
        var x = _selection.Left + (_selection.Width - toolbarWidth) / 2;
        x = Math.Clamp(x, 8, Math.Max(8, RootCanvas.Width - toolbarWidth - 8));

        var y = _selection.Bottom + 10;
        if (y + toolbarHeight > RootCanvas.Height - 8)
        {
            y = _selection.Top - toolbarHeight - 10;
        }

        if (y < 8)
        {
            y = Math.Max(8, _selection.Bottom - toolbarHeight - 10);
        }

        Canvas.SetLeft(Toolbar, x);
        Canvas.SetTop(Toolbar, y);
        PositionToolOptionsBar(x, y, toolbarWidth, toolbarHeight);
    }

    private void PositionScrollToolbar()
    {
        if (ScrollToolbar.Visibility != Visibility.Visible || _selection.IsEmpty)
        {
            return;
        }

        ScrollToolbar.UpdateLayout();
        var toolbarWidth = ScrollToolbar.ActualWidth;
        var toolbarHeight = ScrollToolbar.ActualHeight;
        var x = _selection.Right - toolbarWidth;
        x = Math.Clamp(x, 8, Math.Max(8, RootCanvas.Width - toolbarWidth - 8));

        var y = _selection.Bottom + 10;
        if (y + toolbarHeight > RootCanvas.Height - 8)
        {
            y = _selection.Top - toolbarHeight - 10;
        }

        if (y < 8)
        {
            y = Math.Max(8, _selection.Bottom - toolbarHeight - 10);
        }

        Canvas.SetLeft(ScrollToolbar, x);
        Canvas.SetTop(ScrollToolbar, y);
    }

    private void PositionToolOptionsBar(double toolbarX, double toolbarY, double toolbarWidth, double toolbarHeight)
    {
        if (ToolOptionsBar.Visibility != Visibility.Visible)
        {
            return;
        }

        ToolOptionsBar.UpdateLayout();
        var optionsWidth = ToolOptionsBar.ActualWidth;
        var optionsHeight = ToolOptionsBar.ActualHeight;
        var x = toolbarX + (toolbarWidth - optionsWidth) / 2;
        x = Math.Clamp(x, 8, Math.Max(8, RootCanvas.Width - optionsWidth - 8));

        var y = toolbarY - optionsHeight - 8;
        if (y < 8)
        {
            y = toolbarY + toolbarHeight + 8;
        }

        if (y + optionsHeight > RootCanvas.Height - 8)
        {
            y = Math.Max(8, toolbarY - optionsHeight - 8);
        }

        Canvas.SetLeft(ToolOptionsBar, x);
        Canvas.SetTop(ToolOptionsBar, y);
    }

    private void SetTool(ToolMode mode)
    {
        CommitActiveTextBox();
        _toolMode = mode;
        CancelShapePreview();

        AnnotationInkCanvas.IsHitTestVisible = _hasSelection && mode != ToolMode.Move;
        SelectionChrome.IsHitTestVisible = _hasSelection && mode == ToolMode.Move;
        Cursor = mode switch
        {
            ToolMode.Move => WpfCursors.Arrow,
            ToolMode.Text => WpfCursors.IBeam,
            _ => WpfCursors.Cross
        };

        AnnotationInkCanvas.EditingMode = mode switch
        {
            ToolMode.Pen => InkCanvasEditingMode.Ink,
            ToolMode.Eraser => InkCanvasEditingMode.EraseByPoint,
            _ => InkCanvasEditingMode.None
        };

        UpdateToolOptionsVisibility();
        UpdateToolbarState();
        PositionToolbar();
    }

    private void SetCaptureMode(CaptureMode mode)
    {
        CommitActiveTextBox();
        _captureMode = mode;
        _scrollSession?.Dispose();
        _scrollSession = null;
        _isScrollSessionActive = false;

        if (RootCanvas.IsMouseCaptured)
        {
            RootCanvas.ReleaseMouseCapture();
        }

        _interaction = InteractionMode.None;
        _resizeHandle = ResizeHandle.None;
        CancelShapePreview();
        HideSelection();
        Cursor = WpfCursors.Cross;
        UpdateCaptureModeState();
        PositionModeBar();
    }

    private async Task EnterScrollCaptureModeAsync()
    {
        if (_isScrollBusy)
        {
            return;
        }

        _hasSelection = true;
        _isScrollSessionActive = true;
        StopAutoScroll();
        _scrollRegion = ToAbsolutePixelRect(_selection);

        ScreenshotImage.Visibility = Visibility.Collapsed;
        ReleaseFullscreenBitmap();
        SelectionRect.IsHitTestVisible = true;
        AnnotationInkCanvas.Visibility = Visibility.Collapsed;
        Toolbar.Visibility = Visibility.Collapsed;
        ToolOptionsBar.Visibility = Visibility.Collapsed;
        ModeBar.Visibility = Visibility.Collapsed;
        SelectionChrome.Visibility = Visibility.Collapsed;
        SizeBadge.Visibility = Visibility.Collapsed;
        ScrollToolbar.Visibility = Visibility.Visible;
        UpdateSelectionVisuals();
        SetOverlayExcludedFromCapture(true);

        SetScrollBusy(true, "准备中...");
        try
        {
            _scrollSession = await RunWithOverlayHiddenAsync(() => _scrollCaptures.StartSession(_scrollRegion));
            var previewPath = await Task.Run(() => _scrollSession.CreatePreview());
            UpdateScrollPreview(previewPath);

            StartManualScrollCapture();
            SetScrollBusy(false);
            UpdateScrollControls();
        }
        catch (Exception ex)
        {
            StopManualScrollCapture();
            SetScrollBusy(false);
            AutoScrollButton.Content = $"启动失败：{ex.Message}";
        }
    }

    private void StartManualScrollCapture()
    {
        _scrollController?.Dispose();
        _scrollController = new ScrollCaptureController();
        _scrollController.WheelScrolled += OnControllerWheelScrolled;
        _scrollController.CaptureRequested += () => _ = RunManualCaptureOnlyAsync();
        _scrollController.WarningRequested += (banner, toolbar) =>
            Dispatcher.BeginInvoke(() => ShowManualScrollWarning(banner, toolbar));
        try
        {
            _scrollController.Install();
        }
        catch (Exception ex)
        {
            AutoScrollButton.Content = $"滚轮监听失败：{ex.Message}";
        }

        EnableScrollPassThrough();
    }

    private void StopManualScrollCapture()
    {
        if (_scrollController is not null)
        {
            _scrollController.WheelScrolled -= OnControllerWheelScrolled;
            _scrollController.Dispose();
            _scrollController = null;
        }

        DisableScrollPassThrough();
        SetOverlayExcludedFromCapture(false);
        _scrollWarningActive = false;
    }

    private void OnControllerWheelScrolled(int wheelDelta, DrawingPoint screenPoint)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_isScrollSessionActive || _scrollSession is null || _scrollController is null)
            {
                return;
            }

            _scrollController.TryHandleWheel(
                wheelDelta,
                screenPoint,
                _scrollRegion,
                _isScrollBusy,
                _isAutoScrolling,
                _isAutoScrollPaused,
                IsPointInsideScrollChrome);
        });
    }

    private void ReleaseFullscreenBitmap()
    {
        ScreenshotImage.Source = null;
        _sourceBitmap = null;
        MemoryPressureService.TrimSoon();
    }

    private async Task RunManualCaptureOnlyAsync()
    {
        var session = _scrollSession;
        if (!_isScrollSessionActive || session is null || _isScrollBusy)
        {
            return;
        }

        if (_isAutoScrolling && !_isAutoScrollPaused)
        {
            return;
        }

        SetScrollBusy(true, "截取中...");

        try
        {
            await Task.Delay(ManualCaptureSettleMs);
            var result = session.CaptureManualStep(createPreview: true);
            if (result.Status is ScrollCaptureService.CaptureStepStatus.Indeterminate
                or ScrollCaptureService.CaptureStepStatus.Discontinuous)
            {
                result = await RetryManualContinuityAsync(session);
            }

            ApplyScrollCaptureResult(result);
        }
        catch (Exception ex)
        {
            ApplyScrollCaptureFailure($"截取失败：{ex.Message}");
        }
        finally
        {
            if (!_isCompleting)
            {
                SetScrollBusy(false);
            }
        }
    }

    private async Task RunManualScrollCaptureAsync(int wheelDelta, DrawingPoint scrollPoint)
    {
        var session = _scrollSession;
        if (session is null)
        {
            return;
        }

        SetScrollBusy(true, "滚动中...");
        try
        {
            await Task.Delay(ManualCaptureSettleMs);
            var result = session.ScrollAndCapture(wheelDelta, scrollPoint);
            if (result.Status is ScrollCaptureService.CaptureStepStatus.Indeterminate
                or ScrollCaptureService.CaptureStepStatus.Discontinuous)
            {
                result = await RetryManualContinuityAsync(session);
            }

            ApplyScrollCaptureResult(result);
        }
        catch (Exception ex)
        {
            ApplyScrollCaptureFailure($"截取失败：{ex.Message}");
        }
        finally
        {
            if (!_isCompleting)
            {
                SetScrollBusy(false);
            }
        }
    }

    private async Task<ScrollCaptureService.CaptureStepResult> RetryManualContinuityAsync(
        ScrollCaptureService.CaptureSession session)
    {
        for (var attempt = 0; attempt < AutoScrollMaxRetryCount; attempt++)
        {
            await Task.Delay(AutoScrollRetryDelayMs);

            var retry = session.CaptureManualStep(createPreview: true);
            if (retry.Status is ScrollCaptureService.CaptureStepStatus.Added
                or ScrollCaptureService.CaptureStepStatus.Unchanged)
            {
                return retry;
            }
        }

        return ScrollCaptureService.CaptureStepResult.Discontinuous(session.FrameCount);
    }

    private async Task RunAutoScrollCaptureAsync()
    {
        var session = _scrollSession;
        if (!_isScrollSessionActive || session is null)
        {
            return;
        }

        if (_isAutoScrolling)
        {
            _isAutoScrollPaused = false;
            DisableScrollPassThrough();
            UpdateScrollControls();
            return;
        }

        _autoScrollCancellation?.Dispose();
        _autoScrollCancellation = new CancellationTokenSource();
        var token = _autoScrollCancellation.Token;
        _isAutoScrolling = true;
        _isAutoScrollPaused = false;
        DisableScrollPassThrough();
        UpdateScrollControls();

        try
        {
            while (session.CanCaptureMore)
            {
                while (_isAutoScrollPaused)
                {
                    await Task.Delay(120, token);
                }

                token.ThrowIfCancellationRequested();
                var result = await CaptureAutoStepAsync(session, token);

                if (result.Status is ScrollCaptureService.CaptureStepStatus.Indeterminate
                    or ScrollCaptureService.CaptureStepStatus.Discontinuous)
                {
                    result = await RetryAutoContinuityAsync(session, token);
                }

                switch (result.Status)
                {
                    case ScrollCaptureService.CaptureStepStatus.Added:
                        ApplyScrollCaptureResult(result);
                        break;
                    case ScrollCaptureService.CaptureStepStatus.Unchanged:
                        HideScrollWarning();
                        StopAutoScroll();
                        AutoScrollButton.Content = "已停止";
                        return;
                    case ScrollCaptureService.CaptureStepStatus.Indeterminate:
                    case ScrollCaptureService.CaptureStepStatus.Discontinuous:
                        StopAutoAtSafeBoundary();
                        return;
                }

                await Task.Delay(180, token);
            }

            StopAutoScroll();
            AutoScrollButton.Content = "已停止";
        }
        catch (OperationCanceledException)
        {
            _isScrollBusy = false;
            UpdateScrollControls();
        }
        catch (Exception ex)
        {
            StopAutoScroll();
            AutoScrollButton.Content = $"自动失败：{ex.Message}";
        }
    }

    private async Task<ScrollCaptureService.CaptureStepResult> CaptureAutoStepAsync(
        ScrollCaptureService.CaptureSession session,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _isScrollBusy = true;
        UpdateScrollControls();
        try
        {
            return await RunWithoutOverlayInputTransparentAsync(() =>
                session.CaptureAutoStep(AutoScrollWheelDelta, AutoScrollFrameDelayMs, createPreview: true));
        }
        finally
        {
            _isScrollBusy = false;
        }
    }

    private async Task<ScrollCaptureService.CaptureStepResult> RetryAutoContinuityAsync(
        ScrollCaptureService.CaptureSession session,
        CancellationToken token)
    {
        HideScrollWarning();

        for (var attempt = 0; attempt < AutoScrollMaxRetryCount; attempt++)
        {
            UpdateScrollControls();
            await Task.Delay(AutoScrollRetryDelayMs, token);
            token.ThrowIfCancellationRequested();

            _isScrollBusy = true;
            UpdateScrollControls();
            try
            {
                var retry = await RunWithoutOverlayInputTransparentAsync(() => session.CaptureCurrentForAuto(createPreview: true));
                if (retry.Status is ScrollCaptureService.CaptureStepStatus.Added
                    or ScrollCaptureService.CaptureStepStatus.Unchanged)
                {
                    return retry;
                }
            }
            finally
            {
                _isScrollBusy = false;
            }
        }

        return ScrollCaptureService.CaptureStepResult.Indeterminate(session.FrameCount);
    }

    private async Task SaveScrollCaptureAsync()
    {
        if (_scrollSession is null || _isScrollBusy)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片|*.png",
            FileName = $"picture-tool-scroll-{DateTime.Now:yyyyMMdd-HHmmss}.png"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await FinishScrollCaptureAsync(openAnnotation: false, savePath: dialog.FileName);
    }

    private async Task FinishScrollCaptureAsync(bool openAnnotation, string? savePath = null, bool copyToClipboard = false)
    {
        var session = _scrollSession;
        if (session is null)
        {
            return;
        }

        StopManualScrollCapture();
        SetScrollBusy(true, "拼接中...");
        try
        {
            var outputPath = await Task.Run(() => session.Finish());
            _scrollSession = null;
            _isCompleting = true;

            if (savePath is not null)
            {
                IoFile.Copy(outputPath, savePath, overwrite: true);
                TempImageStore.TryDelete(outputPath);
            }
            else if (openAnnotation)
            {
                ScrollCaptureCompleted?.Invoke(this, outputPath);
            }
            else if (copyToClipboard)
            {
                WpfClipboard.SetImage(BitmapLoader.LoadFrozen(outputPath));
                TempImageStore.TryDelete(outputPath);
            }

            Close();
        }
        catch (Exception ex)
        {
            SetScrollBusy(false);
            AutoScrollButton.Content = $"拼接失败：{ex.Message}";
        }
    }

    private async Task<T> RunWithOverlayHiddenAsync<T>(Func<T> action)
    {
        SetOverlayInputTransparent(true);
        await Task.Delay(120);

        try
        {
            return await Task.Run(action);
        }
        finally
        {
            SetOverlayInputTransparent(false);
            if (!_isCompleting)
            {
                Activate();
                RootCanvas.Focus();
            }
        }
    }

    private static async Task<T> RunWithoutOverlayInputTransparentAsync<T>(Func<T> action)
    {
        return await Task.Run(action);
    }

    private async Task WaitForScrollIdleAsync()
    {
        while (_isScrollBusy)
        {
            await Task.Delay(50);
        }
    }

    private void SetScrollBusy(bool busy, string? text = null)
    {
        _isScrollBusy = busy;
        if (text is not null)
        {
            AutoScrollButton.Content = text;
        }
        else if (!busy && !_scrollWarningActive)
        {
            UpdateScrollControls();
        }

        UpdateScrollButtonStates();
        PositionScrollToolbar();
    }

    private void UpdateScrollToolbarText()
    {
        if (!_scrollWarningActive)
        {
            UpdateScrollControls();
        }
        else
        {
            PositionScrollToolbar();
        }
    }

    private void UpdateScrollControls(string? startText = null)
    {
        AutoScrollButton.Content = startText ?? (_isAutoScrolling
            ? (_isAutoScrollPaused ? "继续滚动" : "暂停")
            : "自动滚动");
        AutoScrollButton.ToolTip = _isAutoScrolling
            ? (_isAutoScrollPaused ? "继续自动滚动" : "暂停自动滚动")
            : "自动滚动";
        UpdateScrollButtonStates();
        PositionScrollToolbar();
    }

    private void UpdateScrollButtonStates()
    {
        var hasSession = _scrollSession is not null;
        var canInteract = hasSession && (!_isScrollBusy || _isAutoScrolling);
        AutoScrollButton.IsEnabled = canInteract;
        SaveScrollButton.IsEnabled = canInteract;
        AnnotateScrollButton.IsEnabled = canInteract;
        CancelScrollButton.IsEnabled = !_isScrollBusy || _isAutoScrolling;
        FinishScrollButton.IsEnabled = canInteract;
    }

    private void StopAutoScroll()
    {
        _autoScrollCancellation?.Cancel();
        _autoScrollCancellation = null;
        _isAutoScrolling = false;
        _isAutoScrollPaused = false;
        if (_isScrollSessionActive && _scrollController is not null)
        {
            EnableScrollPassThrough();
        }

        if (ScrollToolbar.Visibility == Visibility.Visible)
        {
            UpdateScrollControls();
        }
    }

    private void StopAutoAtSafeBoundary()
    {
        StopAutoScroll();
        HideScrollWarning();
        AutoScrollButton.Content = "已停止";
        PositionScrollToolbar();
    }

    private void ApplyScrollCaptureResult(ScrollCaptureService.CaptureStepResult result)
    {
        switch (result.Status)
        {
            case ScrollCaptureService.CaptureStepStatus.Added:
                if (result.PreviewPath is not null)
                {
                    UpdateScrollPreview(result.PreviewPath);
                }

                _scrollController?.ResetPace();
                HideScrollWarning();
                UpdateScrollControls();
                break;
            case ScrollCaptureService.CaptureStepStatus.Unchanged:
                ShowManualScrollWarning(ManualScrollTooFastWarning, "没有检测到新内容");
                break;
            case ScrollCaptureService.CaptureStepStatus.Indeterminate:
                ShowManualScrollWarning(ManualScrollTooFastWarning, "滚动过快，请放慢");
                break;
            case ScrollCaptureService.CaptureStepStatus.Discontinuous:
                ShowManualScrollWarning(ManualScrollTooFastWarning, "滚动不连贯，请回退");
                break;
        }
    }

    private void ApplyScrollCaptureFailure(string message)
    {
        AutoScrollButton.Content = message;
        PositionScrollToolbar();
    }

    private void UpdateScrollPreview(string imagePath)
    {
        try
        {
            ScrollPreviewImage.Source = null;
            var previewRect = GetScrollPreviewRect();
            var maxPixelWidth = Math.Max(1, (int)Math.Round(previewRect.Width * _scaleX));
            var maxPixelHeight = Math.Max(1, (int)Math.Round(previewRect.Height * _scaleY));
            ScrollPreviewImage.Source = BitmapLoader.LoadFrozenForDisplay(imagePath, maxPixelWidth, maxPixelHeight);
        }
        finally
        {
            TempImageStore.TryDelete(imagePath);
        }

        ScrollPreviewPanel.Visibility = Visibility.Visible;
        PositionScrollPreview();
    }

    private void ShowManualScrollWarning(string bannerMessage, string toolbarMessage)
    {
        _scrollWarningActive = true;
        ScrollWarningText.Text = bannerMessage;
        ScrollWarningText.Visibility = Visibility.Visible;
        AutoScrollButton.Content = toolbarMessage;
        PositionScrollWarning();
        PositionScrollToolbar();
        System.Windows.Controls.Panel.SetZIndex(ScrollToolbar, 1999);
    }

    private void ShowScrollWarning(string message)
    {
        ShowManualScrollWarning(message, message);
    }

    private void HideScrollWarning()
    {
        _scrollWarningActive = false;
        ScrollWarningText.Visibility = Visibility.Collapsed;
    }

    private void UpdateCaptureModeState()
    {
        ApplyModeButtonState(ScreenshotModeButton, _captureMode == CaptureMode.Screenshot);
        ApplyModeButtonState(ScrollModeButton, _captureMode == CaptureMode.Scroll);
    }

    private static void ApplyModeButtonState(WpfButton button, bool active)
    {
        button.Background = new SolidColorBrush(active
            ? WpfColor.FromRgb(37, 99, 235)
            : WpfColor.FromRgb(248, 250, 252));
        button.Foreground = new SolidColorBrush(active
            ? WpfColor.FromRgb(255, 255, 255)
            : WpfColor.FromRgb(52, 64, 84));
        button.BorderBrush = new SolidColorBrush(active
            ? WpfColor.FromRgb(37, 99, 235)
            : WpfColor.FromRgb(208, 213, 221));
    }

    private void ApplyDrawingAttributes()
    {
        AnnotationInkCanvas.DefaultDrawingAttributes = CreateDrawingAttributes();
    }

    private void UpdateToolbarState()
    {
        var buttons = new[]
        {
            MoveButton,
            RectangleButton,
            EllipseButton,
            LineButton,
            ArrowButton,
            PenButton,
            TextButton,
            HighlightButton,
            NumberMarkerButton,
            MosaicButton,
            EraserButton
        };

        foreach (var button in buttons)
        {
            button.Background = WpfBrushes.WhiteSmoke;
            button.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(208, 213, 221));
        }

        var active = _toolMode switch
        {
            ToolMode.Move => MoveButton,
            ToolMode.Rectangle => RectangleButton,
            ToolMode.Ellipse => EllipseButton,
            ToolMode.Line => LineButton,
            ToolMode.Arrow => ArrowButton,
            ToolMode.Pen => PenButton,
            ToolMode.Text => TextButton,
            ToolMode.Highlight => HighlightButton,
            ToolMode.NumberMarker => NumberMarkerButton,
            ToolMode.Mosaic => MosaicButton,
            ToolMode.Eraser => EraserButton,
            _ => MoveButton
        };

        active.Background = new SolidColorBrush(WpfColor.FromRgb(221, 247, 255));
        active.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(105, 199, 255));

        var colorButtons = new[]
        {
            ColorRedButton,
            ColorYellowButton,
            ColorGreenButton,
            ColorBlueButton,
            ColorWhiteButton,
            ColorBlackButton
        };

        foreach (var button in colorButtons)
        {
            button.BorderThickness = new Thickness(1);
            button.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(208, 213, 221));
        }

        foreach (var button in colorButtons)
        {
            if (button.Tag is not string value)
            {
                continue;
            }

            try
            {
                var color = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(value)!;
                if (color == _strokeColor)
                {
                    button.BorderThickness = new Thickness(2);
                    button.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(16, 24, 40));
                }
            }
            catch
            {
                // Keep the toolbar usable even if a swatch tag is malformed.
            }
        }

        var widthButtons = new[] { StrokeThinButton, StrokeMediumButton, StrokeBoldButton };
        foreach (var button in widthButtons)
        {
            button.Background = WpfBrushes.WhiteSmoke;
            button.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(208, 213, 221));
        }

        foreach (var button in widthButtons)
        {
            if (button.Tag is string value
                && double.TryParse(value, out var width)
                && Math.Abs(width - _strokeWidth) < 0.1)
            {
                button.Background = new SolidColorBrush(WpfColor.FromRgb(221, 247, 255));
                button.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(105, 199, 255));
            }
        }

        SyncTextSizeComboBox();
    }

    private void UpdateToolOptionsVisibility()
    {
        var showTextOptions = _hasSelection && _toolMode == ToolMode.Text;
        var showStrokeOptions = _hasSelection && UsesStrokeOptions(_toolMode);
        var showColorOptions = showTextOptions || showStrokeOptions;

        ToolOptionsBar.Visibility = showColorOptions ? Visibility.Visible : Visibility.Collapsed;
        ColorOptionsPanel.Visibility = showColorOptions ? Visibility.Visible : Visibility.Collapsed;
        StrokeOptionsDivider.Visibility = showStrokeOptions ? Visibility.Visible : Visibility.Collapsed;
        StrokeOptionsPanel.Visibility = showStrokeOptions ? Visibility.Visible : Visibility.Collapsed;
        TextSizeOptionsPanel.Visibility = showTextOptions ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool UsesStrokeOptions(ToolMode mode)
    {
        return mode is ToolMode.Rectangle or ToolMode.Ellipse or ToolMode.Line or ToolMode.Arrow or ToolMode.Pen or ToolMode.Highlight;
    }

    private void BeginEditSnapshot()
    {
        _editBaseline ??= CaptureState();
    }

    private void CommitEditSnapshot()
    {
        if (_editBaseline is null)
        {
            return;
        }

        if (!AreStatesEquivalent(_editBaseline, CaptureState()))
        {
            _undoStack.Push(_editBaseline);
        }

        _editBaseline = null;
    }

    private void UndoLast()
    {
        CancelShapePreview();
        CommitActiveTextBox();
        if (!_undoStack.TryPop(out var previous))
        {
            return;
        }

        RestoreState(previous);
        _editBaseline = null;
    }

    private Shape CreateShapePreview(WpfPoint point)
    {
        Shape shape = _toolMode switch
        {
            ToolMode.Ellipse => new Ellipse(),
            ToolMode.Line or ToolMode.Arrow => new Line { X1 = point.X, Y1 = point.Y, X2 = point.X, Y2 = point.Y },
            _ => new WpfRectangle()
        };

        shape.Stroke = _toolMode == ToolMode.Highlight
            ? WpfBrushes.Transparent
            : new SolidColorBrush(_strokeColor);
        shape.StrokeThickness = _toolMode == ToolMode.Highlight ? 0 : _strokeWidth;
        shape.Fill = _toolMode switch
        {
            ToolMode.Mosaic => new SolidColorBrush(WpfColor.FromArgb(150, 152, 162, 179)),
            ToolMode.Highlight => new SolidColorBrush(WpfColor.FromArgb(80, _strokeColor.R, _strokeColor.G, _strokeColor.B)),
            _ => WpfBrushes.Transparent
        };
        shape.IsHitTestVisible = false;

        if (shape is not Line)
        {
            InkCanvas.SetLeft(shape, point.X);
            InkCanvas.SetTop(shape, point.Y);
        }

        return shape;
    }

    private void UpdateShapePreview(WpfPoint start, WpfPoint current)
    {
        if (_shapePreview is null)
        {
            return;
        }

        if (_shapePreview is Line line)
        {
            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = current.X;
            line.Y2 = current.Y;
            return;
        }

        var rect = CreateRect(start, current);
        InkCanvas.SetLeft(_shapePreview, rect.X);
        InkCanvas.SetTop(_shapePreview, rect.Y);
        _shapePreview.Width = rect.Width;
        _shapePreview.Height = rect.Height;
    }

    private void CompleteShape(WpfPoint current)
    {
        if (_shapeStart is null)
        {
            CommitEditSnapshot();
            return;
        }

        var start = _shapeStart.Value;
        CancelShapePreview();

        if ((current - start).Length < 4)
        {
            CommitEditSnapshot();
            return;
        }

        switch (_toolMode)
        {
            case ToolMode.Rectangle:
                AnnotationInkCanvas.Strokes.Add(CreateRectangleStroke(CreateRect(start, current)));
                break;
            case ToolMode.Ellipse:
                AnnotationInkCanvas.Strokes.Add(CreateEllipseStroke(CreateRect(start, current)));
                break;
            case ToolMode.Line:
                AnnotationInkCanvas.Strokes.Add(CreateLineStroke(start, current));
                break;
            case ToolMode.Arrow:
                foreach (var stroke in CreateArrowStrokes(start, current))
                {
                    AnnotationInkCanvas.Strokes.Add(stroke);
                }

                break;
            case ToolMode.Highlight:
                _overlayItems.Add(new HighlightOverlayItem(CreateRect(start, current), _strokeColor));
                RebuildOverlayElements();
                break;
            case ToolMode.Mosaic:
                _overlayItems.Add(new MosaicOverlayItem(CreateRect(start, current)));
                RebuildOverlayElements();
                break;
        }

        CommitEditSnapshot();
    }

    private void ShowTextEditor(WpfPoint point)
    {
        BeginEditSnapshot();

        var initialText = string.Empty;
        _activeTextColor = _strokeColor;
        _activeTextFontSize = _textFontSize;

        var existingIndex = FindTextOverlayIndexAt(point);
        if (existingIndex >= 0 && _overlayItems[existingIndex] is TextOverlayItem existingText)
        {
            initialText = existingText.Text;
            _activeTextPoint = existingText.Position;
            _activeTextColor = existingText.Color;
            _activeTextFontSize = existingText.FontSize;
            _strokeColor = existingText.Color;
            _textFontSize = existingText.FontSize;
            _overlayItems.RemoveAt(existingIndex);
            RebuildOverlayElements();
            ApplyDrawingAttributes();
            UpdateToolbarState();
        }
        else
        {
            var x = Math.Clamp(point.X, _selection.Left, Math.Max(_selection.Left, _selection.Right - 120));
            var y = Math.Clamp(point.Y, _selection.Top, Math.Max(_selection.Top, _selection.Bottom - 34));
            _activeTextPoint = new WpfPoint(x, y);
        }

        var textBox = new WpfTextBox
        {
            Text = initialText,
            MinWidth = 120,
            MinHeight = 32,
            FontFamily = new WpfFontFamily("Microsoft YaHei UI"),
            FontSize = _activeTextFontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(_activeTextColor),
            Background = new SolidColorBrush(WpfColor.FromArgb(230, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(105, 199, 255)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 0, 4, 2),
            AcceptsReturn = false
        };

        textBox.KeyDown += ActiveTextBox_KeyDown;
        textBox.LostFocus += ActiveTextBox_LostFocus;
        _activeTextBox = textBox;
        InkCanvas.SetLeft(textBox, _activeTextPoint.X);
        InkCanvas.SetTop(textBox, _activeTextPoint.Y);
        AnnotationInkCanvas.Children.Add(textBox);
        Dispatcher.BeginInvoke((Action)(() =>
        {
            textBox.Focus();
            Keyboard.Focus(textBox);
            if (initialText.Length > 0)
            {
                textBox.SelectAll();
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void ActiveTextBox_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitActiveTextBox();
            e.Handled = true;
            RootCanvas.Focus();
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelActiveTextBox();
            e.Handled = true;
            RootCanvas.Focus();
        }
    }

    private void ActiveTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, _activeTextBox))
        {
            CommitActiveTextBox();
        }
    }

    private void CommitActiveTextBox()
    {
        var textBox = _activeTextBox;
        if (textBox is null)
        {
            return;
        }

        textBox.KeyDown -= ActiveTextBox_KeyDown;
        textBox.LostFocus -= ActiveTextBox_LostFocus;
        AnnotationInkCanvas.Children.Remove(textBox);
        _activeTextBox = null;

        var text = textBox.Text.Trim();
        if (text.Length > 0)
        {
            _overlayItems.Add(new TextOverlayItem(text, _activeTextPoint, _activeTextColor, _activeTextFontSize));
            RebuildOverlayElements();
            CommitEditSnapshot();
            return;
        }

        RebuildOverlayElements();
        CommitEditSnapshot();
    }

    private void CancelActiveTextBox()
    {
        var textBox = _activeTextBox;
        if (textBox is null)
        {
            return;
        }

        textBox.KeyDown -= ActiveTextBox_KeyDown;
        textBox.LostFocus -= ActiveTextBox_LostFocus;
        AnnotationInkCanvas.Children.Remove(textBox);
        _activeTextBox = null;
        if (_editBaseline is not null)
        {
            RestoreState(_editBaseline);
        }

        _editBaseline = null;
    }

    private void EraseOverlayItemsAt(WpfPoint point)
    {
        if (_overlayItems.Count == 0)
        {
            return;
        }

        var removed = false;
        for (var index = _overlayItems.Count - 1; index >= 0; index--)
        {
            if (!IsOverlayItemHit(_overlayItems[index], point))
            {
                continue;
            }

            BeginEditSnapshot();
            _overlayItems.RemoveAt(index);
            removed = true;
        }

        if (removed)
        {
            RebuildOverlayElements();
        }
    }

    private bool IsOverlayItemHit(OverlayItem item, WpfPoint point)
    {
        var bounds = GetOverlayItemBounds(item);
        bounds.Inflate(OverlayEraserRadius, OverlayEraserRadius);
        return bounds.Contains(point);
    }

    private int FindTextOverlayIndexAt(WpfPoint point)
    {
        for (var index = _overlayItems.Count - 1; index >= 0; index--)
        {
            if (_overlayItems[index] is TextOverlayItem text && GetTextOverlayBounds(text).Contains(point))
            {
                return index;
            }
        }

        return -1;
    }

    private WpfRect GetOverlayItemBounds(OverlayItem item)
    {
        const double markerSize = 28;
        return item switch
        {
            MosaicOverlayItem mosaic => mosaic.Bounds,
            HighlightOverlayItem highlight => highlight.Bounds,
            NumberMarkerOverlayItem marker => new WpfRect(
                marker.Position.X - markerSize / 2,
                marker.Position.Y - markerSize / 2,
                markerSize, markerSize),
            _ => WpfRect.Empty
        };
    }

    private WpfRect GetTextOverlayBounds(TextOverlayItem item)
    {
        var typeface = new Typeface(
            new WpfFontFamily("Microsoft YaHei UI"),
            FontStyles.Normal,
            FontWeights.SemiBold,
            FontStretches.Normal);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var formatted = new FormattedText(
            item.Text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            item.FontSize,
            WpfBrushes.Black,
            pixelsPerDip);

        return new WpfRect(item.Position.X, item.Position.Y, formatted.Width, formatted.Height);
    }

    private AnnotationState CaptureState()
    {
        return new AnnotationState(
            CloneStrokes(AnnotationInkCanvas.Strokes),
            _overlayItems.Select(item => item.Clone()).ToList());
    }

    private void RestoreState(AnnotationState state)
    {
        AnnotationInkCanvas.Strokes = CloneStrokes(state.Strokes);
        _overlayItems.Clear();
        _overlayItems.AddRange(state.Items.Select(item => item.Clone()));
        RebuildOverlayElements();
    }

    private void RebuildOverlayElements()
    {
        foreach (var element in _overlayElements)
        {
            AnnotationInkCanvas.Children.Remove(element);
        }

        _overlayElements.Clear();

        foreach (var item in _overlayItems)
        {
            var element = CreateOverlayElement(item);
            _overlayElements.Add(element);
            AnnotationInkCanvas.Children.Add(element);
        }
    }

    private UIElement CreateOverlayElement(OverlayItem item)
    {
        switch (item)
        {
            case TextOverlayItem text:
            {
                var block = new TextBlock
                {
                    Text = text.Text,
                    FontFamily = new WpfFontFamily("Microsoft YaHei UI"),
                    FontSize = text.FontSize,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(text.Color),
                    IsHitTestVisible = false
                };

                InkCanvas.SetLeft(block, text.Position.X);
                InkCanvas.SetTop(block, text.Position.Y);
                return block;
            }
            case MosaicOverlayItem mosaic:
            {
                var rect = new WpfRectangle
                {
                    Fill = new SolidColorBrush(WpfColor.FromArgb(150, 152, 162, 179)),
                    Stroke = new SolidColorBrush(WpfColor.FromArgb(210, 255, 255, 255)),
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };

                PositionRect(rect, mosaic.Bounds);
                return rect;
            }
            case HighlightOverlayItem highlight:
            {
                var rect = new WpfRectangle
                {
                    Fill = new SolidColorBrush(WpfColor.FromArgb(80, highlight.Color.R, highlight.Color.G, highlight.Color.B)),
                    IsHitTestVisible = false
                };

                PositionRect(rect, highlight.Bounds);
                return rect;
            }
            case NumberMarkerOverlayItem marker:
            {
                const double markerSize = 28;
                var grid = new Grid
                {
                    Width = markerSize,
                    Height = markerSize,
                    IsHitTestVisible = false
                };

                grid.Children.Add(new Ellipse
                {
                    Fill = new SolidColorBrush(marker.Color),
                    Width = markerSize,
                    Height = markerSize
                });

                grid.Children.Add(new TextBlock
                {
                    Text = marker.Number.ToString(),
                    Foreground = WpfBrushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                });

                InkCanvas.SetLeft(grid, marker.Position.X - markerSize / 2);
                InkCanvas.SetTop(grid, marker.Position.Y - markerSize / 2);
                return grid;
            }
            default:
                return new Canvas();
        }
    }

    private void DrawMosaicItems(DrawingContext context)
    {
        foreach (var item in _overlayItems.OfType<MosaicOverlayItem>())
        {
            DrawMosaicItem(context, item.Bounds);
        }
    }

    private void DrawTextItems(DrawingContext context)
    {
        foreach (var item in _overlayItems.OfType<TextOverlayItem>())
        {
            DrawTextItem(context, item);
        }
    }

    private void DrawTextItem(DrawingContext context, TextOverlayItem item)
    {
        var foreground = new SolidColorBrush(item.Color);
        var shadow = new SolidColorBrush(WpfColor.FromArgb(180, 0, 0, 0));
        var typeface = new Typeface(
            new WpfFontFamily("Microsoft YaHei UI"),
            FontStyles.Normal,
            FontWeights.SemiBold,
            FontStretches.Normal);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var shadowText = new FormattedText(
            item.Text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            item.FontSize,
            shadow,
            pixelsPerDip);
        var foregroundText = new FormattedText(
            item.Text,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            item.FontSize,
            foreground,
            pixelsPerDip);

        context.DrawText(shadowText, new WpfPoint(item.Position.X + 1, item.Position.Y + 1));
        context.DrawText(foregroundText, item.Position);
    }

    private void DrawHighlightItems(DrawingContext context)
    {
        foreach (var item in _overlayItems.OfType<HighlightOverlayItem>())
        {
            var brush = new SolidColorBrush(WpfColor.FromArgb(80, item.Color.R, item.Color.G, item.Color.B));
            context.DrawRectangle(brush, null, item.Bounds);
        }
    }

    private void DrawNumberMarkerItems(DrawingContext context)
    {
        const double markerSize = 28;
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(
            new WpfFontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);

        foreach (var item in _overlayItems.OfType<NumberMarkerOverlayItem>())
        {
            var center = item.Position;
            context.DrawEllipse(
                new SolidColorBrush(item.Color),
                null,
                center,
                markerSize / 2,
                markerSize / 2);

            var text = new FormattedText(
                item.Number.ToString(),
                CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                typeface,
                14,
                WpfBrushes.White,
                pixelsPerDip);

            context.DrawText(text, new WpfPoint(
                center.X - text.Width / 2,
                center.Y - text.Height / 2));
        }
    }

    private void DrawMosaicItem(DrawingContext context, WpfRect bounds)
    {
        var pixelRect = ToSourcePixelRect(bounds);
        if (pixelRect.Width <= 1 || pixelRect.Height <= 1)
        {
            return;
        }

        try
        {
            var cropped = new CroppedBitmap(SourceBitmap, pixelRect);
            cropped.Freeze();

            var block = Math.Max(4, MosaicBlockSize * Math.Max(_scaleX, _scaleY));
            var pixelated = new TransformedBitmap(cropped, new ScaleTransform(1 / block, 1 / block));
            pixelated.Freeze();
            context.DrawImage(pixelated, bounds);
        }
        catch
        {
            context.DrawRectangle(
                new SolidColorBrush(WpfColor.FromArgb(180, 152, 162, 179)),
                null,
                bounds);
        }
    }

    private Int32Rect ToSourcePixelRect(WpfRect bounds)
    {
        var sourceBitmap = SourceBitmap;
        var left = Math.Clamp((int)Math.Floor(bounds.Left * _scaleX), 0, sourceBitmap.PixelWidth - 1);
        var top = Math.Clamp((int)Math.Floor(bounds.Top * _scaleY), 0, sourceBitmap.PixelHeight - 1);
        var right = Math.Clamp((int)Math.Ceiling(bounds.Right * _scaleX), left + 1, sourceBitmap.PixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(bounds.Bottom * _scaleY), top + 1, sourceBitmap.PixelHeight);

        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private void CancelShapePreview()
    {
        if (_shapePreview is not null)
        {
            AnnotationInkCanvas.Children.Remove(_shapePreview);
        }

        if (AnnotationInkCanvas.IsMouseCaptured)
        {
            AnnotationInkCanvas.ReleaseMouseCapture();
        }

        _shapeStart = null;
        _shapePreview = null;
    }

    private void SaveSelected()
    {
        if (!_hasSelection)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片|*.png",
            FileName = $"picture-tool-{DateTime.Now:yyyyMMdd-HHmmss}.png"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SaveBitmap(dialog.FileName, RenderSelectedBitmap());
        CompleteAndClose();
    }

    private void CopyAndClose()
    {
        if (!_hasSelection)
        {
            return;
        }

        var tempPath = SaveSelectedToTemp();
        WpfClipboard.SetImage(BitmapLoader.LoadFrozen(tempPath));
        CaptureCompleted?.Invoke(this, tempPath);
        CompleteAndClose();
    }

    private string SaveSelectedToTemp()
    {
        CommitActiveTextBox();
        var path = TempImageStore.CreatePngPath();
        SaveBitmap(path, RenderSelectedBitmap());
        return path;
    }

    private RenderTargetBitmap RenderSelectedBitmap()
    {
        CommitActiveTextBox();
        var pixelWidth = Math.Max(1, (int)Math.Round(_selection.Width * _scaleX));
        var pixelHeight = Math.Max(1, (int)Math.Round(_selection.Height * _scaleY));
        var dpiX = 96 * _scaleX;
        var dpiY = 96 * _scaleY;
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var context = visual.RenderOpen())
        {
            context.PushClip(new RectangleGeometry(new WpfRect(0, 0, _selection.Width, _selection.Height)));
            context.PushTransform(new TranslateTransform(-_selection.Left, -_selection.Top));
            context.DrawImage(SourceBitmap, new WpfRect(0, 0, RootCanvas.Width, RootCanvas.Height));
            DrawHighlightItems(context);
            DrawMosaicItems(context);
            AnnotationInkCanvas.Strokes.Draw(context);
            DrawTextItems(context);
            DrawNumberMarkerItems(context);
            context.Pop();
            context.Pop();
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private DrawingRectangle ToAbsolutePixelRect(WpfRect bounds)
    {
        var left = _frame.PixelBounds.Left + (int)Math.Round(bounds.Left * _scaleX);
        var top = _frame.PixelBounds.Top + (int)Math.Round(bounds.Top * _scaleY);
        var right = _frame.PixelBounds.Left + (int)Math.Round(bounds.Right * _scaleX);
        var bottom = _frame.PixelBounds.Top + (int)Math.Round(bounds.Bottom * _scaleY);

        left = Math.Clamp(left, _frame.PixelBounds.Left, _frame.PixelBounds.Right - 1);
        top = Math.Clamp(top, _frame.PixelBounds.Top, _frame.PixelBounds.Bottom - 1);
        right = Math.Clamp(right, left + 1, _frame.PixelBounds.Right);
        bottom = Math.Clamp(bottom, top + 1, _frame.PixelBounds.Bottom);

        return DrawingRectangle.FromLTRB(left, top, right, bottom);
    }

    private DrawingPoint ToAbsolutePixelPoint(WpfPoint point)
    {
        var x = _frame.PixelBounds.Left + (int)Math.Round(point.X * _scaleX);
        var y = _frame.PixelBounds.Top + (int)Math.Round(point.Y * _scaleY);

        x = Math.Clamp(x, _scrollRegion.Left, _scrollRegion.Right - 1);
        y = Math.Clamp(y, _scrollRegion.Top, _scrollRegion.Bottom - 1);

        return new DrawingPoint(x, y);
    }

    private void CompleteAndClose()
    {
        _isCompleting = true;
        Close();
    }

    private void CancelCapture()
    {
        StopManualScrollCapture();
        if (!_isCompleting)
        {
            CaptureCanceled?.Invoke(this, EventArgs.Empty);
        }

        Close();
    }

    private bool IsPointInsideScrollChrome(DrawingPoint screenPoint)
    {
        var canvasPoint = RootCanvas.PointFromScreen(new WpfPoint(screenPoint.X, screenPoint.Y));
        return IsPointInScrollToolbar(canvasPoint)
            || IsPointInScrollPreview(canvasPoint)
            || IsPointInScrollWarning(canvasPoint);
    }

    private bool IsPointInScrollToolbar(WpfPoint canvasPoint)
    {
        if (ScrollToolbar.Visibility != Visibility.Visible)
        {
            return false;
        }

        ScrollToolbar.UpdateLayout();
        var rect = new WpfRect(
            Canvas.GetLeft(ScrollToolbar),
            Canvas.GetTop(ScrollToolbar),
            Math.Max(1, ScrollToolbar.ActualWidth),
            Math.Max(1, ScrollToolbar.ActualHeight));
        return rect.Contains(canvasPoint);
    }

    private bool IsPointInScrollPreview(WpfPoint canvasPoint)
    {
        if (ScrollPreviewPanel.Visibility != Visibility.Visible)
        {
            return false;
        }

        ScrollPreviewPanel.UpdateLayout();
        var rect = new WpfRect(
            Canvas.GetLeft(ScrollPreviewPanel),
            Canvas.GetTop(ScrollPreviewPanel),
            Math.Max(1, ScrollPreviewPanel.ActualWidth),
            Math.Max(1, ScrollPreviewPanel.ActualHeight));
        return rect.Contains(canvasPoint);
    }

    private bool IsPointInScrollWarning(WpfPoint canvasPoint)
    {
        if (ScrollWarningText.Visibility != Visibility.Visible)
        {
            return false;
        }

        ScrollWarningText.UpdateLayout();
        var rect = new WpfRect(
            Canvas.GetLeft(ScrollWarningText),
            Canvas.GetTop(ScrollWarningText),
            Math.Max(1, ScrollWarningText.ActualWidth),
            Math.Max(1, ScrollWarningText.ActualHeight));
        return rect.Contains(canvasPoint);
    }

    private void EnsureHwndSourceHook()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        _hwndSource?.RemoveHook(HwndSourceHook);
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(HwndSourceHook);
    }

    private void EnableScrollPassThrough()
    {
        EnsureHwndSourceHook();
        _scrollPassThroughActive = true;
    }

    private void DisableScrollPassThrough()
    {
        _scrollPassThroughActive = false;
    }

    private void SetOverlayExcludedFromCapture(bool exclude)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowDisplayAffinity(hwnd, exclude ? WdaExcludeFromCapture : WdaNone);
        var value = exclude ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaExcludedFromCapture, ref value, sizeof(int));
    }

    private IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNchitTest || !_scrollPassThroughActive || !_isScrollSessionActive)
        {
            return IntPtr.Zero;
        }

        var screenPoint = new WpfPoint(
            (short)(lParam.ToInt64() & 0xFFFF),
            (short)((lParam.ToInt64() >> 16) & 0xFFFF));
        var canvasPoint = RootCanvas.PointFromScreen(screenPoint);
        if (_selection.Contains(canvasPoint)
            && !IsPointInScrollToolbar(canvasPoint)
            && !IsPointInScrollPreview(canvasPoint)
            && !IsPointInScrollWarning(canvasPoint))
        {
            handled = true;
            return new IntPtr(HtTransparent);
        }

        return IntPtr.Zero;
    }

    private static void SaveBitmap(string path, BitmapSource bitmap)
    {
        using var stream = IoFile.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
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

    private Stroke CreateEllipseStroke(WpfRect bounds)
    {
        var points = new StylusPointCollection();
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;
        var radiusX = bounds.Width / 2;
        var radiusY = bounds.Height / 2;

        for (var index = 0; index <= 64; index++)
        {
            var angle = Math.PI * 2 * index / 64;
            points.Add(new StylusPoint(centerX + Math.Cos(angle) * radiusX, centerY + Math.Sin(angle) * radiusY));
        }

        return new Stroke(points, CreateDrawingAttributes());
    }

    private Stroke CreateLineStroke(WpfPoint start, WpfPoint end)
    {
        return new Stroke(new StylusPointCollection
        {
            new StylusPoint(start.X, start.Y),
            new StylusPoint(end.X, end.Y)
        }, CreateDrawingAttributes());
    }

    private IEnumerable<Stroke> CreateArrowStrokes(WpfPoint start, WpfPoint end)
    {
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        var length = 18;
        var sideA = new WpfPoint(
            end.X - length * Math.Cos(angle - Math.PI / 7),
            end.Y - length * Math.Sin(angle - Math.PI / 7));
        var sideB = new WpfPoint(
            end.X - length * Math.Cos(angle + Math.PI / 7),
            end.Y - length * Math.Sin(angle + Math.PI / 7));

        return new[]
        {
            CreateLineStroke(start, end),
            CreateLineStroke(end, sideA),
            CreateLineStroke(end, sideB)
        };
    }

    private DrawingAttributes CreateDrawingAttributes()
    {
        return new DrawingAttributes
        {
            Color = _strokeColor,
            Width = _strokeWidth,
            Height = _strokeWidth,
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

    private static bool AreStatesEquivalent(AnnotationState left, AnnotationState right)
    {
        if (!AreStrokesEquivalent(left.Strokes, right.Strokes) || left.Items.Count != right.Items.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Items.Count; index++)
        {
            if (!left.Items[index].IsEquivalent(right.Items[index]))
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

    private WpfPoint ClampPoint(WpfPoint point)
    {
        return new WpfPoint(
            Math.Clamp(point.X, 0, RootCanvas.Width),
            Math.Clamp(point.Y, 0, RootCanvas.Height));
    }

    private static void PositionRect(FrameworkElement element, WpfRect rect)
    {
        Canvas.SetLeft(element, rect.X);
        Canvas.SetTop(element, rect.Y);
        element.Width = Math.Max(0, rect.Width);
        element.Height = Math.Max(0, rect.Height);
    }

    private static void PositionHandle(FrameworkElement handle, double x, double y)
    {
        Canvas.SetLeft(handle, x - HandleSize / 2);
        Canvas.SetTop(handle, y - HandleSize / 2);
    }

    private void SetDimVisibility(Visibility visibility)
    {
        DimTop.Visibility = visibility;
        DimLeft.Visibility = visibility;
        DimRight.Visibility = visibility;
        DimBottom.Visibility = visibility;
    }

    private void SetOverlayInputTransparent(bool transparent)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        var nextStyle = transparent
            ? style | WsExTransparent
            : style & ~WsExTransparent;

        if (nextStyle != style)
        {
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(nextStyle));
        }
    }

    private static bool IsDragOverlayTool(ToolMode mode)
    {
        return mode is ToolMode.Rectangle or ToolMode.Ellipse or ToolMode.Line or ToolMode.Arrow or ToolMode.Mosaic or ToolMode.Highlight;
    }

    private static bool IsToolbarSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Border border && (border.Name == "Toolbar" || border.Name == "ToolOptionsBar" || border.Name == "ModeBar" || border.Name == "ScrollToolbar"))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static bool IsSelectionChromeSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Canvas canvas && canvas.Name == "SelectionChrome")
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static ResizeHandle ParseResizeHandle(string? value)
    {
        return Enum.TryParse<ResizeHandle>(value, out var handle) ? handle : ResizeHandle.None;
    }

    private enum CaptureMode
    {
        Screenshot,
        Scroll
    }

    private enum InteractionMode
    {
        None,
        Selecting,
        Moving,
        Resizing
    }

    private enum ToolMode
    {
        Move,
        Rectangle,
        Ellipse,
        Line,
        Arrow,
        Pen,
        Text,
        Mosaic,
        Highlight,
        NumberMarker,
        Eraser
    }

    private enum ResizeHandle
    {
        None,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left
    }

    private sealed record AnnotationState(StrokeCollection Strokes, List<OverlayItem> Items);

    private abstract class OverlayItem
    {
        public abstract OverlayItem Clone();

        public abstract bool IsEquivalent(OverlayItem other);
    }

    private sealed class TextOverlayItem : OverlayItem
    {
        public TextOverlayItem(string text, WpfPoint position, WpfColor color, double fontSize)
        {
            Text = text;
            Position = position;
            Color = color;
            FontSize = fontSize;
        }

        public string Text { get; }

        public WpfPoint Position { get; }

        public WpfColor Color { get; }

        public double FontSize { get; }

        public override OverlayItem Clone()
        {
            return new TextOverlayItem(Text, Position, Color, FontSize);
        }

        public override bool IsEquivalent(OverlayItem other)
        {
            return other is TextOverlayItem text
                && text.Text == Text
                && text.Position.Equals(Position)
                && text.Color == Color
                && Math.Abs(text.FontSize - FontSize) < 0.1;
        }
    }

    private sealed class MosaicOverlayItem : OverlayItem
    {
        public MosaicOverlayItem(WpfRect bounds)
        {
            Bounds = bounds;
        }

        public WpfRect Bounds { get; }

        public override OverlayItem Clone()
        {
            return new MosaicOverlayItem(Bounds);
        }

        public override bool IsEquivalent(OverlayItem other)
        {
            return other is MosaicOverlayItem mosaic && mosaic.Bounds.Equals(Bounds);
        }
    }

    private sealed class HighlightOverlayItem : OverlayItem
    {
        public HighlightOverlayItem(WpfRect bounds, WpfColor color)
        {
            Bounds = bounds;
            Color = color;
        }

        public WpfRect Bounds { get; }
        public WpfColor Color { get; }

        public override OverlayItem Clone() => new HighlightOverlayItem(Bounds, Color);

        public override bool IsEquivalent(OverlayItem other)
        {
            return other is HighlightOverlayItem h && h.Bounds.Equals(Bounds) && h.Color == Color;
        }
    }

    private sealed class NumberMarkerOverlayItem : OverlayItem
    {
        public NumberMarkerOverlayItem(WpfPoint position, int number, WpfColor color)
        {
            Position = position;
            Number = number;
            Color = color;
        }

        public WpfPoint Position { get; }
        public int Number { get; }
        public WpfColor Color { get; }

        public override OverlayItem Clone() => new NumberMarkerOverlayItem(Position, Number, Color);

        public override bool IsEquivalent(OverlayItem other)
        {
            return other is NumberMarkerOverlayItem n && n.Position.Equals(Position) && n.Number == Number && n.Color == Color;
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
