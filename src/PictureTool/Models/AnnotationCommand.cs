using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace PictureTool.Models;

public abstract record AnnotationCommand;

public sealed record PenCommand(IReadOnlyList<WpfPoint> Points, WpfColor Color, double Width) : AnnotationCommand;

public sealed record RectangleCommand(WpfRect Bounds, WpfColor Color, double Width) : AnnotationCommand;

public sealed record TextCommand(WpfPoint Position, string Text, WpfColor Color, double FontSize) : AnnotationCommand;

public sealed record MosaicCommand(WpfRect Bounds, double Strength) : AnnotationCommand;
