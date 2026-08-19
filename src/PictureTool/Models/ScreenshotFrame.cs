using System.Windows;
using DrawingRectangle = System.Drawing.Rectangle;

namespace PictureTool.Models;

public sealed record ScreenshotFrame(string ImagePath, DrawingRectangle PixelBounds, Rect DisplayBounds);
