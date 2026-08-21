using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace PictureTool.Services;

public sealed class ScrollCaptureService
{
    private const int MaxFrames = 24;
    private const int CaptureEdgeTrim = 3;
    private const int ScrollDelayMs = 520;
    private const int MinOverlap = 48;
    private const int OverlapSearchStep = 8;
    private const int MinMeaningfulAdvancePx = 16;
    private const double SameFrameScore = 2.0;
    private const double MatchScoreLimit = 16.0;
    private const double ControlledScrollMatchScoreLimit = 24.0;
    private const double StrongMatchScoreLimit = 10.0;
    private const double MinContinuousOverlapRatio = 0.24;
    private const double HighConfidenceOverlapRatio = 0.50;
    private const double AmbiguousScoreGap = 2.5;
    private const double OverlapScoreTieEpsilon = 0.75;
    private const double FullFrameUnchangedScore = 6.0;
    private const double SuspiciousLargeAdvanceScore = 12.0;

    public CaptureSession StartSession(DrawingRectangle region, ScrollCaptureDirection direction = ScrollCaptureDirection.VerticalDown)
    {
        var session = new CaptureSession(region, direction);
        try
        {
            session.CaptureCurrent(createPreview: false);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private static string CaptureRegion(DrawingRectangle region)
    {
        var path = TempImageStore.CreatePngPath();
        var contentRegion = GetContentCaptureRegion(region);
        using var bitmap = new Bitmap(contentRegion.Width, contentRegion.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            contentRegion.Left,
            contentRegion.Top,
            0,
            0,
            contentRegion.Size,
            CopyPixelOperation.SourceCopy);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    internal static DrawingRectangle GetContentCaptureRegion(DrawingRectangle region)
    {
        var maxTrim = Math.Max(0, Math.Min(region.Width, region.Height) / 4);
        var trim = Math.Min(CaptureEdgeTrim, maxTrim);
        if (trim <= 0)
        {
            return region;
        }

        return new DrawingRectangle(
            region.Left + trim,
            region.Top + trim,
            region.Width - trim * 2,
            region.Height - trim * 2);
    }

    private static string Stitch(IReadOnlyList<FramePart> parts, bool deleteParts = true)
    {
        if (parts.Count == 0)
        {
            throw new InvalidOperationException("No frames to stitch.");
        }

        return parts[0].Direction.IsHorizontal()
            ? StitchHorizontal(parts, deleteParts)
            : StitchVertical(parts, deleteParts);
    }

    private static string StitchVertical(IReadOnlyList<FramePart> parts, bool deleteParts)
    {
        var direction = parts[0].Direction;
        var outputPath = TempImageStore.CreatePngPath();
        try
        {
            var totalHeight = 0;
            var width = 0;
            foreach (var part in parts)
            {
                using var frame = new Bitmap(part.Path);
                width = Math.Max(width, frame.Width);
                totalHeight += GetVerticalDrawableHeight(frame, part, direction);
            }

            using var output = new Bitmap(width, totalHeight, PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(output);
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            if (direction == ScrollCaptureDirection.VerticalUp)
            {
                var y = totalHeight;
                foreach (var part in parts)
                {
                    using var frame = new Bitmap(part.Path);
                    var source = GetVerticalUpSourceRect(frame, part);
                    y -= source.Height;
                    var target = new DrawingRectangle(0, y, frame.Width, source.Height);
                    graphics.DrawImage(frame, target, source, GraphicsUnit.Pixel);
                }
            }
            else
            {
                var y = 0;
                foreach (var part in parts)
                {
                    using var frame = new Bitmap(part.Path);
                    var source = GetVerticalDownSourceRect(frame, part);
                    var target = new DrawingRectangle(0, y, frame.Width, source.Height);
                    graphics.DrawImage(frame, target, source, GraphicsUnit.Pixel);
                    y += source.Height;
                }
            }

            output.Save(outputPath, ImageFormat.Png);
            return outputPath;
        }
        finally
        {
            if (deleteParts)
            {
                foreach (var part in parts)
                {
                    TempImageStore.TryDelete(part.Path);
                }
            }
        }
    }

    private static string StitchHorizontal(IReadOnlyList<FramePart> parts, bool deleteParts)
    {
        var direction = parts[0].Direction;
        var outputPath = TempImageStore.CreatePngPath();
        try
        {
            var totalWidth = 0;
            var height = 0;
            foreach (var part in parts)
            {
                using var frame = new Bitmap(part.Path);
                height = Math.Max(height, frame.Height);
                totalWidth += GetHorizontalDrawableWidth(frame, part, direction);
            }

            using var output = new Bitmap(totalWidth, height, PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(output);
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            if (direction == ScrollCaptureDirection.HorizontalLeft)
            {
                var x = totalWidth;
                foreach (var part in parts)
                {
                    using var frame = new Bitmap(part.Path);
                    var source = GetHorizontalLeftSourceRect(frame, part);
                    x -= source.Width;
                    var target = new DrawingRectangle(x, 0, source.Width, frame.Height);
                    graphics.DrawImage(frame, target, source, GraphicsUnit.Pixel);
                }
            }
            else
            {
                var x = 0;
                foreach (var part in parts)
                {
                    using var frame = new Bitmap(part.Path);
                    var source = GetHorizontalRightSourceRect(frame, part);
                    var target = new DrawingRectangle(x, 0, source.Width, frame.Height);
                    graphics.DrawImage(frame, target, source, GraphicsUnit.Pixel);
                    x += source.Width;
                }
            }

            output.Save(outputPath, ImageFormat.Png);
            return outputPath;
        }
        finally
        {
            if (deleteParts)
            {
                foreach (var part in parts)
                {
                    TempImageStore.TryDelete(part.Path);
                }
            }
        }
    }

    private static int GetVerticalDrawableHeight(Bitmap frame, FramePart part, ScrollCaptureDirection direction)
    {
        return direction == ScrollCaptureDirection.VerticalUp
            ? GetVerticalUpSourceRect(frame, part).Height
            : GetVerticalDownSourceRect(frame, part).Height;
    }

    private static DrawingRectangle GetVerticalDownSourceRect(Bitmap frame, FramePart part)
    {
        var skipTop = Math.Clamp(part.OverlapSkip, 0, frame.Height - 1);
        return new DrawingRectangle(0, skipTop, frame.Width, frame.Height - skipTop);
    }

    private static DrawingRectangle GetVerticalUpSourceRect(Bitmap frame, FramePart part)
    {
        var skipBottom = Math.Clamp(part.OverlapSkip, 0, frame.Height - 1);
        return new DrawingRectangle(0, 0, frame.Width, frame.Height - skipBottom);
    }

    private static int GetHorizontalDrawableWidth(Bitmap frame, FramePart part, ScrollCaptureDirection direction)
    {
        return direction == ScrollCaptureDirection.HorizontalLeft
            ? GetHorizontalLeftSourceRect(frame, part).Width
            : GetHorizontalRightSourceRect(frame, part).Width;
    }

    private static DrawingRectangle GetHorizontalRightSourceRect(Bitmap frame, FramePart part)
    {
        var skipLeft = Math.Clamp(part.OverlapSkip, 0, frame.Width - 1);
        return new DrawingRectangle(skipLeft, 0, frame.Width - skipLeft, frame.Height);
    }

    private static DrawingRectangle GetHorizontalLeftSourceRect(Bitmap frame, FramePart part)
    {
        var skipRight = Math.Clamp(part.OverlapSkip, 0, frame.Width - 1);
        return new DrawingRectangle(0, 0, frame.Width - skipRight, frame.Height);
    }

    private static OverlapMatch FindBestOverlap(Bitmap previous, Bitmap current, ScrollCaptureDirection direction) =>
        direction switch
        {
            ScrollCaptureDirection.VerticalUp => FindBestVerticalOverlapUp(previous, current),
            ScrollCaptureDirection.HorizontalRight => FindBestHorizontalOverlapRight(previous, current),
            ScrollCaptureDirection.HorizontalLeft => FindBestHorizontalOverlapLeft(previous, current),
            _ => FindBestVerticalOverlap(previous, current)
        };

    private static OverlapMatch FindBestVerticalOverlap(Bitmap previous, Bitmap current) =>
        FindBestAxisOverlap(
            Math.Min(previous.Height, current.Height),
            overlap => CompareOverlap(previous, current, overlap));

    internal static OverlapMatch FindBestOverlapForTests(
        Bitmap previous,
        Bitmap current,
        ScrollCaptureDirection direction = ScrollCaptureDirection.VerticalDown) =>
        FindBestOverlap(previous, current, direction);

    private static unsafe double CompareOverlap(Bitmap previous, Bitmap current, int overlap)
    {
        var width = Math.Min(previous.Width, current.Width);
        var rowSamples = Math.Clamp(overlap / 12, 12, 72);
        var colSamples = Math.Clamp(width / 48, 16, 72);
        long diff = 0;
        var samples = 0;

        var prevRect = new DrawingRectangle(0, 0, previous.Width, previous.Height);
        var currRect = new DrawingRectangle(0, 0, current.Width, current.Height);
        var prevData = previous.LockBits(prevRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        var currData = current.LockBits(currRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);

        try
        {
            var prevStride = prevData.Stride;
            var currStride = currData.Stride;
            var prevScan = (byte*)prevData.Scan0;
            var currScan = (byte*)currData.Scan0;

            for (var row = 0; row < rowSamples; row++)
            {
                var sampleY = (row + 0.5) * overlap / rowSamples;
                var previousY = previous.Height - overlap + (int)Math.Floor(sampleY);
                var currentY = (int)Math.Floor(sampleY);

                if (previousY < 0 || previousY >= previous.Height || currentY < 0 || currentY >= current.Height)
                {
                    continue;
                }

                var prevRow = prevScan + previousY * prevStride;
                var currRow = currScan + currentY * currStride;

                for (var col = 0; col < colSamples; col++)
                {
                    var x = Math.Clamp((int)Math.Floor((col + 0.5) * width / colSamples), 0, width - 1);
                    var prevOffset = x * 4;
                    var currOffset = x * 4;

                    diff += Math.Abs(prevRow[prevOffset] - currRow[currOffset]);
                    diff += Math.Abs(prevRow[prevOffset + 1] - currRow[currOffset + 1]);
                    diff += Math.Abs(prevRow[prevOffset + 2] - currRow[currOffset + 2]);
                    samples++;
                }
            }
        }
        finally
        {
            previous.UnlockBits(prevData);
            current.UnlockBits(currData);
        }

        return samples == 0 ? double.MaxValue : diff / (samples * 3.0);
    }

    /// <summary>
    /// Average per-channel difference across the whole frame. Used to detect "scroll did nothing"
    /// even when overlap search falsely prefers a small overlap on near-identical images.
    /// </summary>
    internal static unsafe double CompareFullFrame(Bitmap previous, Bitmap current)
    {
        var width = Math.Min(previous.Width, current.Width);
        var height = Math.Min(previous.Height, current.Height);
        if (width <= 0 || height <= 0)
        {
            return double.MaxValue;
        }

        var rowSamples = Math.Clamp(height / 8, 24, 96);
        var colSamples = Math.Clamp(width / 8, 24, 96);
        long diff = 0;
        var samples = 0;

        var prevRect = new DrawingRectangle(0, 0, previous.Width, previous.Height);
        var currRect = new DrawingRectangle(0, 0, current.Width, current.Height);
        var prevData = previous.LockBits(prevRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        var currData = current.LockBits(currRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);

        try
        {
            var prevStride = prevData.Stride;
            var currStride = currData.Stride;
            var prevScan = (byte*)prevData.Scan0;
            var currScan = (byte*)currData.Scan0;

            for (var row = 0; row < rowSamples; row++)
            {
                var y = Math.Clamp((int)Math.Floor((row + 0.5) * height / rowSamples), 0, height - 1);
                var prevRow = prevScan + y * prevStride;
                var currRow = currScan + y * currStride;

                for (var col = 0; col < colSamples; col++)
                {
                    var x = Math.Clamp((int)Math.Floor((col + 0.5) * width / colSamples), 0, width - 1);
                    var offset = x * 4;
                    diff += Math.Abs(prevRow[offset] - currRow[offset]);
                    diff += Math.Abs(prevRow[offset + 1] - currRow[offset + 1]);
                    diff += Math.Abs(prevRow[offset + 2] - currRow[offset + 2]);
                    samples++;
                }
            }
        }
        finally
        {
            previous.UnlockBits(prevData);
            current.UnlockBits(currData);
        }

        return samples == 0 ? double.MaxValue : diff / (samples * 3.0);
    }

    private static OverlapMatch FindBestVerticalOverlapUp(Bitmap previous, Bitmap current) =>
        FindBestAxisOverlap(
            Math.Min(previous.Height, current.Height),
            overlap => CompareOverlapUp(previous, current, overlap));

    private static OverlapMatch FindBestHorizontalOverlapRight(Bitmap previous, Bitmap current) =>
        FindBestAxisOverlap(
            Math.Min(previous.Width, current.Width),
            overlap => CompareOverlapHorizontalRight(previous, current, overlap));

    private static OverlapMatch FindBestHorizontalOverlapLeft(Bitmap previous, Bitmap current) =>
        FindBestAxisOverlap(
            Math.Min(previous.Width, current.Width),
            overlap => CompareOverlapHorizontalLeft(previous, current, overlap));

    private static OverlapMatch FindBestAxisOverlap(int span, Func<int, double> scoreForOverlap)
    {
        var maxOverlap = Math.Max(1, span - 8);
        var minOverlap = Math.Min(
            Math.Max(MinOverlap, (int)Math.Round(span * MinContinuousOverlapRatio)),
            maxOverlap);
        var bestOverlap = 0;
        var bestScore = double.MaxValue;
        var candidates = new List<(int Overlap, double Score)>();

        for (var overlap = minOverlap; overlap <= maxOverlap; overlap += OverlapSearchStep)
        {
            var score = scoreForOverlap(overlap);
            candidates.Add((overlap, score));
            ConsiderOverlapCandidate(overlap, score, ref bestOverlap, ref bestScore);
        }

        if (bestOverlap > 0)
        {
            var refineStart = Math.Max(minOverlap, bestOverlap - OverlapSearchStep);
            var refineEnd = Math.Min(maxOverlap, bestOverlap + OverlapSearchStep);
            for (var overlap = refineStart; overlap <= refineEnd; overlap++)
            {
                var score = scoreForOverlap(overlap);
                ConsiderOverlapCandidate(overlap, score, ref bestOverlap, ref bestScore);
            }

            // Identical frames score ~0 at every overlap; prefer the largest usable overlap
            // so IsSameFrame can recognize "no movement" instead of treating minOverlap as new content.
            if (bestScore <= SameFrameScore)
            {
                var nearFull = scoreForOverlap(maxOverlap);
                ConsiderOverlapCandidate(maxOverlap, nearFull, ref bestOverlap, ref bestScore);
            }
        }

        var distinctDistance = Math.Max(OverlapSearchStep * 2, (int)Math.Round(span * 0.10));
        var alternateScore = candidates
            .Where(candidate => Math.Abs(candidate.Overlap - bestOverlap) >= distinctDistance)
            .Select(candidate => candidate.Score)
            .DefaultIfEmpty(double.MaxValue)
            .Min();

        return new OverlapMatch(bestOverlap, bestScore, alternateScore, minOverlap, span);
    }

    private static void ConsiderOverlapCandidate(
        int overlap,
        double score,
        ref int bestOverlap,
        ref double bestScore)
    {
        if (score < bestScore - OverlapScoreTieEpsilon)
        {
            bestScore = score;
            bestOverlap = overlap;
            return;
        }

        if (score <= bestScore + OverlapScoreTieEpsilon && overlap > bestOverlap)
        {
            bestOverlap = overlap;
            bestScore = Math.Min(bestScore, score);
        }
    }

    private static unsafe double CompareOverlapUp(Bitmap previous, Bitmap current, int overlap)
    {
        var width = Math.Min(previous.Width, current.Width);
        var rowSamples = Math.Clamp(overlap / 12, 12, 72);
        var colSamples = Math.Clamp(width / 48, 16, 72);
        long diff = 0;
        var samples = 0;

        var prevRect = new DrawingRectangle(0, 0, previous.Width, previous.Height);
        var currRect = new DrawingRectangle(0, 0, current.Width, current.Height);
        var prevData = previous.LockBits(prevRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        var currData = current.LockBits(currRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);

        try
        {
            var prevStride = prevData.Stride;
            var currStride = currData.Stride;
            var prevScan = (byte*)prevData.Scan0;
            var currScan = (byte*)currData.Scan0;

            for (var row = 0; row < rowSamples; row++)
            {
                var sampleY = (row + 0.5) * overlap / rowSamples;
                var previousY = (int)Math.Floor(sampleY);
                var currentY = current.Height - overlap + (int)Math.Floor(sampleY);

                if (previousY < 0 || previousY >= previous.Height || currentY < 0 || currentY >= current.Height)
                {
                    continue;
                }

                var prevRow = prevScan + previousY * prevStride;
                var currRow = currScan + currentY * currStride;

                for (var col = 0; col < colSamples; col++)
                {
                    var x = Math.Clamp((int)Math.Floor((col + 0.5) * width / colSamples), 0, width - 1);
                    var prevOffset = x * 4;
                    var currOffset = x * 4;

                    diff += Math.Abs(prevRow[prevOffset] - currRow[currOffset]);
                    diff += Math.Abs(prevRow[prevOffset + 1] - currRow[currOffset + 1]);
                    diff += Math.Abs(prevRow[prevOffset + 2] - currRow[currOffset + 2]);
                    samples++;
                }
            }
        }
        finally
        {
            previous.UnlockBits(prevData);
            current.UnlockBits(currData);
        }

        return samples == 0 ? double.MaxValue : diff / (samples * 3.0);
    }

    private static unsafe double CompareOverlapHorizontalRight(Bitmap previous, Bitmap current, int overlap)
    {
        var height = Math.Min(previous.Height, current.Height);
        var colSamples = Math.Clamp(overlap / 12, 12, 72);
        var rowSamples = Math.Clamp(height / 48, 16, 72);
        long diff = 0;
        var samples = 0;

        var prevRect = new DrawingRectangle(0, 0, previous.Width, previous.Height);
        var currRect = new DrawingRectangle(0, 0, current.Width, current.Height);
        var prevData = previous.LockBits(prevRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        var currData = current.LockBits(currRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);

        try
        {
            var prevStride = prevData.Stride;
            var currStride = currData.Stride;
            var prevScan = (byte*)prevData.Scan0;
            var currScan = (byte*)currData.Scan0;

            for (var col = 0; col < colSamples; col++)
            {
                var sampleX = (col + 0.5) * overlap / colSamples;
                var previousX = previous.Width - overlap + (int)Math.Floor(sampleX);
                var currentX = (int)Math.Floor(sampleX);

                for (var row = 0; row < rowSamples; row++)
                {
                    var y = Math.Clamp((int)Math.Floor((row + 0.5) * height / rowSamples), 0, height - 1);
                    var prevOffset = previousX * 4;
                    var currOffset = currentX * 4;
                    var prevRow = prevScan + y * prevStride;
                    var currRow = currScan + y * currStride;

                    diff += Math.Abs(prevRow[prevOffset] - currRow[currOffset]);
                    diff += Math.Abs(prevRow[prevOffset + 1] - currRow[currOffset + 1]);
                    diff += Math.Abs(prevRow[prevOffset + 2] - currRow[currOffset + 2]);
                    samples++;
                }
            }
        }
        finally
        {
            previous.UnlockBits(prevData);
            current.UnlockBits(currData);
        }

        return samples == 0 ? double.MaxValue : diff / (samples * 3.0);
    }

    private static unsafe double CompareOverlapHorizontalLeft(Bitmap previous, Bitmap current, int overlap)
    {
        var height = Math.Min(previous.Height, current.Height);
        var colSamples = Math.Clamp(overlap / 12, 12, 72);
        var rowSamples = Math.Clamp(height / 48, 16, 72);
        long diff = 0;
        var samples = 0;

        var prevRect = new DrawingRectangle(0, 0, previous.Width, previous.Height);
        var currRect = new DrawingRectangle(0, 0, current.Width, current.Height);
        var prevData = previous.LockBits(prevRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        var currData = current.LockBits(currRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);

        try
        {
            var prevStride = prevData.Stride;
            var currStride = currData.Stride;
            var prevScan = (byte*)prevData.Scan0;
            var currScan = (byte*)currData.Scan0;

            for (var col = 0; col < colSamples; col++)
            {
                var sampleX = (col + 0.5) * overlap / colSamples;
                var previousX = (int)Math.Floor(sampleX);
                var currentX = current.Width - overlap + (int)Math.Floor(sampleX);

                for (var row = 0; row < rowSamples; row++)
                {
                    var y = Math.Clamp((int)Math.Floor((row + 0.5) * height / rowSamples), 0, height - 1);
                    var prevOffset = previousX * 4;
                    var currOffset = currentX * 4;
                    var prevRow = prevScan + y * prevStride;
                    var currRow = currScan + y * currStride;

                    diff += Math.Abs(prevRow[prevOffset] - currRow[currOffset]);
                    diff += Math.Abs(prevRow[prevOffset + 1] - currRow[currOffset + 1]);
                    diff += Math.Abs(prevRow[prevOffset + 2] - currRow[currOffset + 2]);
                    samples++;
                }
            }
        }
        finally
        {
            previous.UnlockBits(prevData);
            current.UnlockBits(currData);
        }

        return samples == 0 ? double.MaxValue : diff / (samples * 3.0);
    }

    private static void ScrollWheel(
        DrawingRectangle region,
        int wheelDelta,
        ScrollCaptureDirection direction,
        DrawingPoint? scrollPoint = null,
        bool forceRealInput = false)
    {
        if (direction.IsHorizontal())
        {
            ScrollHorizontalWheel(region, wheelDelta, scrollPoint, forceRealInput);
            return;
        }

        ScrollVerticalWheel(region, wheelDelta, scrollPoint, forceRealInput);
    }

    private static void ScrollVerticalWheel(
        DrawingRectangle region,
        int wheelDelta,
        DrawingPoint? scrollPoint = null,
        bool forceRealInput = false)
    {
        var x = scrollPoint?.X ?? region.Left + region.Width / 2;
        var y = scrollPoint?.Y ?? region.Top + region.Height / 2;
        
        x = Math.Clamp(x, region.Left, region.Right - 1);
        y = Math.Clamp(y, region.Top, region.Bottom - 1);
        SetCursorPos(x, y);

        if (forceRealInput)
        {
            Thread.Sleep(60);
            mouse_event(MouseEventWheel, 0, 0, wheelDelta, UIntPtr.Zero);
            return;
        }

        var targets = FindWheelTargets(x, y);
        if (targets.Count > 0)
        {
            foreach (var target in targets)
            {
                PostMessage(target, WmMouseWheel, MakeWheelWParam(wheelDelta), MakeLParam(x, y));
            }

            return;
        }

        Thread.Sleep(60);
        mouse_event(MouseEventWheel, 0, 0, wheelDelta, UIntPtr.Zero);
    }

    private static void ScrollHorizontalWheel(
        DrawingRectangle region,
        int wheelDelta,
        DrawingPoint? scrollPoint = null,
        bool forceRealInput = false)
    {
        var x = scrollPoint?.X ?? region.Left + region.Width / 2;
        var y = scrollPoint?.Y ?? region.Top + region.Height / 2;
        x = Math.Clamp(x, region.Left, region.Right - 1);
        y = Math.Clamp(y, region.Top, region.Bottom - 1);
        SetCursorPos(x, y);

        if (forceRealInput)
        {
            Thread.Sleep(60);
            mouse_event(MouseEventHWheel, 0, 0, wheelDelta, UIntPtr.Zero);
            return;
        }

        var targets = FindWheelTargets(x, y);
        if (targets.Count > 0)
        {
            foreach (var target in targets)
            {
                PostMessage(target, WmMouseHWheel, MakeWheelWParam(wheelDelta), MakeLParam(x, y));
            }

            return;
        }

        Thread.Sleep(60);
        mouse_event(MouseEventHWheel, 0, 0, wheelDelta, UIntPtr.Zero);
    }

    private static IntPtr MakeWheelWParam(int wheelDelta)
    {
        return new IntPtr((wheelDelta << 16) & unchecked((int)0xFFFF0000));
    }

    private static IntPtr MakeLParam(int x, int y)
    {
        return new IntPtr((y << 16) | (x & 0xFFFF));
    }

    private static IReadOnlyList<IntPtr> FindWheelTargets(int x, int y)
    {
        var currentProcessId = Environment.ProcessId;
        var point = new Point(x, y);

        for (var hwnd = GetTopWindow(IntPtr.Zero);
             hwnd != IntPtr.Zero;
             hwnd = GetWindow(hwnd, GwHwndNext))
        {
            if (!IsWindowVisible(hwnd) || !GetWindowRect(hwnd, out var rect))
            {
                continue;
            }

            if (x < rect.Left || x >= rect.Right || y < rect.Top || y >= rect.Bottom)
            {
                continue;
            }

            GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == currentProcessId)
            {
                continue;
            }

            var clientPoint = point;
            ScreenToClient(hwnd, ref clientPoint);
            var child = FindDeepestChildWindow(hwnd, clientPoint);
            var targets = new List<IntPtr>();
            AddTarget(targets, child == IntPtr.Zero ? hwnd : child);
            AddTarget(targets, hwnd);

            var parent = child;
            while (parent != IntPtr.Zero)
            {
                parent = GetParent(parent);
                AddTarget(targets, parent);
                if (parent == hwnd)
                {
                    break;
                }
            }

            return targets;
        }

        return [];
    }

    private static IntPtr FindDeepestChildWindow(IntPtr hwnd, Point clientPoint)
    {
        var current = hwnd;
        var point = clientPoint;

        while (true)
        {
            var child = ChildWindowFromPointEx(
                current,
                point,
                CwpSkipInvisible | CwpSkipDisabled | CwpSkipTransparent);

            if (child == IntPtr.Zero || child == current)
            {
                return current == hwnd ? IntPtr.Zero : current;
            }

            var parent = current;
            current = child;
            var screenPoint = point;
            ClientToScreen(parent, ref screenPoint);
            point = screenPoint;
            ScreenToClient(current, ref point);
        }
    }

    private static void AddTarget(List<IntPtr> targets, IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero && !targets.Contains(hwnd))
        {
            targets.Add(hwnd);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, int dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetTopWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref Point point);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr ChildWindowFromPointEx(IntPtr hWndParent, Point point, uint flags);

    private const uint MouseEventWheel = 0x0800;
    private const uint MouseEventHWheel = 0x01000;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmMouseHWheel = 0x020E;
    private const uint GwHwndNext = 2;
    private const uint CwpSkipInvisible = 0x0001;
    private const uint CwpSkipDisabled = 0x0002;
    private const uint CwpSkipTransparent = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public readonly int X;

        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;

        public int Top;

        public int Right;

        public int Bottom;

    }

    public sealed class CaptureSession : IDisposable
    {
        private readonly DrawingRectangle _region;
        private readonly ScrollCaptureDirection _direction;
        private readonly List<FramePart> _parts = new();
        private BitmapHolder? _previousHolder;
        private string? _cachedPreviewPath;
        private int _cachedPreviewParts;
        private int _unmatchedSteps;
        private bool _finished;

        public CaptureSession(DrawingRectangle region, ScrollCaptureDirection direction)
        {
            _region = region;
            _direction = direction;
        }

        public ScrollCaptureDirection Direction => _direction;

        public int FrameCount => _parts.Count;

        public CaptureStepResult CaptureCurrent(bool createPreview = true)
        {
            return CaptureCurrent(createPreview, useControlledScrollMatching: false, countUnmatchedStep: true);
        }

        public CaptureStepResult CaptureCurrentForAuto(bool createPreview = true)
        {
            // Auto mode must not use the loose ControlledScroll "Added" path — that is what
            // turned stalled near-identical frames into duplicated strips.
            return CaptureCurrent(createPreview, useControlledScrollMatching: false, countUnmatchedStep: false);
        }

        private CaptureStepResult CaptureCurrent(
            bool createPreview,
            bool useControlledScrollMatching,
            bool countUnmatchedStep)
        {
            ThrowIfFinished();

            var currentPath = CaptureRegion(_region);
            try
            {
                if (_previousHolder is null)
                {
                    _parts.Add(new FramePart(currentPath, 0, _direction));
                    _previousHolder = new BitmapHolder(currentPath);
                    _unmatchedSteps = 0;
                    InvalidatePreviewCache();
                    return CaptureStepResult.Added(createPreview ? CreatePreview() : null, FrameCount);
                }

                int overlap;
                int currentSpan;
                using (var current = new Bitmap(currentPath))
                {
                    var fullFrameScore = CompareFullFrame(_previousHolder.Bitmap, current);
                    if (fullFrameScore <= FullFrameUnchangedScore)
                    {
                        TempImageStore.TryDelete(currentPath);
                        _unmatchedSteps = 0;
                        return CaptureStepResult.Unchanged(FrameCount);
                    }

                    var match = FindBestOverlap(_previousHolder.Bitmap, current, _direction);
                    overlap = match.Overlap;
                    currentSpan = _direction.IsHorizontal() ? current.Width : current.Height;
                    var advance = currentSpan - overlap;

                    if (match.IsSameFrame)
                    {
                        TempImageStore.TryDelete(currentPath);
                        _unmatchedSteps = 0;
                        return CaptureStepResult.Unchanged(FrameCount);
                    }

                    // Claiming a large advance while the whole viewport still looks similar means
                    // overlap search latched onto a false small overlap (common on chat UIs).
                    if (advance >= currentSpan * 0.35 && fullFrameScore <= SuspiciousLargeAdvanceScore)
                    {
                        TempImageStore.TryDelete(currentPath);
                        _unmatchedSteps = 0;
                        return CaptureStepResult.Unchanged(FrameCount);
                    }

                    if (!match.IsReliable && !(useControlledScrollMatching && match.IsUsableForControlledScroll))
                    {
                        return RejectCurrentFrame(currentPath, countUnmatchedStep);
                    }
                }

                if (overlap >= currentSpan - MinMeaningfulAdvancePx)
                {
                    TempImageStore.TryDelete(currentPath);
                    _unmatchedSteps = 0;
                    return CaptureStepResult.Unchanged(FrameCount);
                }

                if (overlap <= 0)
                {
                    return RejectCurrentFrame(currentPath, countUnmatchedStep);
                }

                _parts.Add(new FramePart(currentPath, overlap, _direction));
                _previousHolder.Replace(currentPath);
                _unmatchedSteps = 0;
                InvalidatePreviewCache();
                return CaptureStepResult.Added(createPreview ? CreatePreview() : null, FrameCount);
            }
            catch
            {
                TempImageStore.TryDelete(currentPath);
                throw;
            }
        }

        public CaptureStepResult CaptureAutoStep(int wheelDelta, int delayMs, bool createPreview = true)
        {
            ThrowIfFinished();

            var delta = wheelDelta == 0 ? _direction.GetAutoWheelDelta() : wheelDelta;

            // 1) Synthetic PostMessage scroll (works for many Win32 targets)
            ScrollWheel(_region, delta, _direction, forceRealInput: false);
            Thread.Sleep(Math.Max(0, delayMs));
            var first = CaptureCurrentForAuto(createPreview: false);
            if (first.Status == CaptureStepStatus.Added)
            {
                return createPreview
                    ? CaptureStepResult.Added(CreatePreview(), FrameCount)
                    : first;
            }

            // 2) Unchanged/Indeterminate after synthetic scroll: try a real mouse wheel once
            //    before deciding we truly cannot advance (otherwise nested scrollers never reach bottom).
            ScrollWheel(_region, delta, _direction, forceRealInput: true);
            Thread.Sleep(Math.Max(delayMs / 2, 500));
            var second = CaptureCurrentForAuto(createPreview);
            return second;
        }

        public CaptureStepResult CaptureManualStep(bool createPreview = true)
        {
            ThrowIfFinished();
            return CaptureCurrent(createPreview, useControlledScrollMatching: true, countUnmatchedStep: true);
        }

        public CaptureStepResult ScrollAndCapture(int wheelDelta, DrawingPoint? scrollPoint = null, bool createPreview = true)
        {
            ThrowIfFinished();

            var delta = wheelDelta == 0 ? _direction.GetAutoWheelDelta() : wheelDelta;
            ScrollWheel(_region, delta, _direction, scrollPoint);
            Thread.Sleep(ScrollDelayMs);
            return CaptureCurrentForAuto(createPreview);
        }

        private CaptureStepResult RejectCurrentFrame(string currentPath, bool countUnmatchedStep)
        {
            TempImageStore.TryDelete(currentPath);
            if (!countUnmatchedStep)
            {
                return CaptureStepResult.Indeterminate(FrameCount);
            }

            var isConfirmedBreak = _parts.Count > 1 || _unmatchedSteps > 0;
            _unmatchedSteps++;
            return isConfirmedBreak
                ? CaptureStepResult.Discontinuous(FrameCount)
                : CaptureStepResult.Indeterminate(FrameCount);
        }

        public bool CanCaptureMore => _parts.Count < MaxFrames;

        public string Finish()
        {
            ThrowIfFinished();

            if (_parts.Count == 0)
            {
                CaptureCurrent(createPreview: false);
            }

            _previousHolder?.Dispose();
            _previousHolder = null;

            var outputPath = Stitch(_parts);
            _parts.Clear();
            _finished = true;
            return outputPath;
        }

        public string CreatePreview()
        {
            ThrowIfFinished();

            if (_parts.Count == 0)
            {
                CaptureCurrent();
            }

            if (_cachedPreviewPath is not null && _cachedPreviewParts == _parts.Count)
            {
                return _cachedPreviewPath;
            }

            InvalidatePreviewCache();
            _cachedPreviewPath = Stitch(_parts, deleteParts: false);
            _cachedPreviewParts = _parts.Count;
            return _cachedPreviewPath;
        }

        private void InvalidatePreviewCache()
        {
            if (_cachedPreviewPath is null)
            {
                return;
            }

            TempImageStore.TryDelete(_cachedPreviewPath);
            _cachedPreviewPath = null;
            _cachedPreviewParts = 0;
        }

        public void Dispose()
        {
            _previousHolder?.Dispose();
            _previousHolder = null;
            InvalidatePreviewCache();

            if (_finished)
            {
                return;
            }

            foreach (var part in _parts)
            {
                TempImageStore.TryDelete(part.Path);
            }

            _parts.Clear();
            _finished = true;
        }

        private void ThrowIfFinished()
        {
            if (_finished)
            {
                throw new ObjectDisposedException(nameof(CaptureSession));
            }
        }
    }

    internal sealed record OverlapMatch(int Overlap, double Score, double AlternateScore, int MinimumOverlap, int FrameHeight)
    {
        public bool IsSameFrame =>
            Score <= SameFrameScore
            && (Overlap >= FrameHeight - MinMeaningfulAdvancePx
                || AlternateScore <= SameFrameScore + OverlapScoreTieEpsilon);

        public bool IsReliable =>
            Overlap >= MinimumOverlap
            && Score <= MatchScoreLimit
            && (AlternateScore - Score >= AmbiguousScoreGap
                || (Score <= StrongMatchScoreLimit && Overlap >= FrameHeight * HighConfidenceOverlapRatio));

        public bool IsUsableForControlledScroll =>
            Overlap >= MinimumOverlap
            && Overlap < FrameHeight - MinMeaningfulAdvancePx
            && Score <= ControlledScrollMatchScoreLimit
            && !IsSameFrame;
    }

    public sealed record CaptureStepResult(CaptureStepStatus Status, string? PreviewPath, int FrameCount)
    {
        public static bool operator true(CaptureStepResult result)
        {
            return result.Status == CaptureStepStatus.Added;
        }

        public static bool operator false(CaptureStepResult result)
        {
            return result.Status != CaptureStepStatus.Added;
        }

        public static CaptureStepResult Added(string? previewPath, int frameCount)
        {
            return new CaptureStepResult(CaptureStepStatus.Added, previewPath, frameCount);
        }

        public static CaptureStepResult Unchanged(int frameCount)
        {
            return new CaptureStepResult(CaptureStepStatus.Unchanged, null, frameCount);
        }

        public static CaptureStepResult Indeterminate(int frameCount)
        {
            return new CaptureStepResult(CaptureStepStatus.Indeterminate, null, frameCount);
        }

        public static CaptureStepResult Discontinuous(int frameCount)
        {
            return new CaptureStepResult(CaptureStepStatus.Discontinuous, null, frameCount);
        }
    }

    public enum CaptureStepStatus
    {
        Added,
        Unchanged,
        Indeterminate,
        Discontinuous
    }

    private sealed record FramePart(string Path, int OverlapSkip, ScrollCaptureDirection Direction);

    private sealed class BitmapHolder : IDisposable
    {
        public BitmapHolder(string path)
        {
            using var source = new Bitmap(path);
            Bitmap = new Bitmap(source);
        }

        public Bitmap Bitmap { get; private set; }

        public void Replace(string path)
        {
            Bitmap.Dispose();
            using var source = new Bitmap(path);
            Bitmap = new Bitmap(source);
        }

        public void Dispose()
        {
            Bitmap.Dispose();
        }
    }
}
