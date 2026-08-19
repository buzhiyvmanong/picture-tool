using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace PictureTool.Services;

public sealed class ScrollCaptureService
{
    public const int ManualScrollDelta = -360;

    private const int MaxFrames = 24;
    private const int CaptureEdgeTrim = 3;
    private const int AutoScrollDelta = -120;
    private const int ScrollDelayMs = 520;
    private const int AutoScrollDelayMs = 1400;
    private const int MinOverlap = 48;
    private const int OverlapSearchStep = 8;
    private const double SameFrameScore = 2.0;
    private const double MatchScoreLimit = 16.0;
    private const double ControlledScrollMatchScoreLimit = 24.0;
    private const double StrongMatchScoreLimit = 10.0;
    private const double MinContinuousOverlapRatio = 0.24;
    private const double HighConfidenceOverlapRatio = 0.50;
    private const double AmbiguousScoreGap = 2.5;

    public string Capture(DrawingRectangle region)
    {
        using var session = StartSession(region);
        session.CaptureAuto();
        return session.Finish();
    }

    public CaptureSession StartSession(DrawingRectangle region)
    {
        var session = new CaptureSession(region);
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

    private static DrawingRectangle GetContentCaptureRegion(DrawingRectangle region)
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
        var outputPath = TempImageStore.CreatePngPath();
        try
        {
            var totalHeight = 0;
            var width = 0;
            foreach (var part in parts)
            {
                using var frame = new Bitmap(part.Path);
                width = Math.Max(width, frame.Width);
                totalHeight += Math.Max(1, frame.Height - part.SkipTop);
            }

            using var output = new Bitmap(width, totalHeight, PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(output);
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            var y = 0;
            foreach (var part in parts)
            {
                using var frame = new Bitmap(part.Path);
                var skipTop = Math.Clamp(part.SkipTop, 0, frame.Height - 1);
                var source = new DrawingRectangle(0, skipTop, frame.Width, frame.Height - skipTop);
                var target = new DrawingRectangle(0, y, frame.Width, source.Height);
                graphics.DrawImage(frame, target, source, GraphicsUnit.Pixel);
                y += source.Height;
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

    private static OverlapMatch FindBestVerticalOverlap(Bitmap previous, Bitmap current)
    {
        var height = Math.Min(previous.Height, current.Height);
        var maxOverlap = Math.Max(1, height - 8);
        var minOverlap = Math.Min(
            Math.Max(MinOverlap, (int)Math.Round(height * MinContinuousOverlapRatio)),
            maxOverlap);
        var bestOverlap = 0;
        var bestScore = double.MaxValue;
        var candidates = new List<(int Overlap, double Score)>();

        for (var overlap = minOverlap; overlap <= maxOverlap; overlap += OverlapSearchStep)
        {
            var score = CompareOverlap(previous, current, overlap);
            candidates.Add((overlap, score));
            if (score < bestScore)
            {
                bestScore = score;
                bestOverlap = overlap;
            }
        }

        if (bestOverlap > 0)
        {
            var refineStart = Math.Max(minOverlap, bestOverlap - OverlapSearchStep);
            var refineEnd = Math.Min(maxOverlap, bestOverlap + OverlapSearchStep);
            for (var overlap = refineStart; overlap <= refineEnd; overlap++)
            {
                var score = CompareOverlap(previous, current, overlap);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestOverlap = overlap;
                }
            }
        }

        var distinctDistance = Math.Max(OverlapSearchStep * 2, (int)Math.Round(height * 0.10));
        var alternateScore = candidates
            .Where(candidate => Math.Abs(candidate.Overlap - bestOverlap) >= distinctDistance)
            .Select(candidate => candidate.Score)
            .DefaultIfEmpty(double.MaxValue)
            .Min();

        return new OverlapMatch(bestOverlap, bestScore, alternateScore, minOverlap, height);
    }

    private static double CompareOverlap(Bitmap previous, Bitmap current, int overlap)
    {
        var width = Math.Min(previous.Width, current.Width);
        var rowSamples = Math.Clamp(overlap / 12, 12, 72);
        var colSamples = Math.Clamp(width / 48, 16, 72);
        long diff = 0;
        var samples = 0;

        for (var row = 0; row < rowSamples; row++)
        {
            var sampleY = (row + 0.5) * overlap / rowSamples;
            var previousY = previous.Height - overlap + (int)Math.Floor(sampleY);
            var currentY = (int)Math.Floor(sampleY);

            if (previousY < 0 || previousY >= previous.Height || currentY < 0 || currentY >= current.Height)
            {
                continue;
            }

            for (var col = 0; col < colSamples; col++)
            {
                var x = Math.Clamp((int)Math.Floor((col + 0.5) * width / colSamples), 0, width - 1);
                var left = previous.GetPixel(x, previousY);
                var right = current.GetPixel(x, currentY);

                diff += Math.Abs(left.R - right.R);
                diff += Math.Abs(left.G - right.G);
                diff += Math.Abs(left.B - right.B);
                samples++;
            }
        }

        return samples == 0 ? double.MaxValue : diff / (samples * 3.0);
    }

    private static void ScrollWheel(
        DrawingRectangle region,
        int wheelDelta,
        DrawingPoint? scrollPoint = null)
    {
        var x = scrollPoint?.X ?? region.Left + region.Width / 2;
        var y = scrollPoint?.Y ?? region.Top + region.Height / 2;
        x = Math.Clamp(x, region.Left, region.Right - 1);
        y = Math.Clamp(y, region.Top, region.Bottom - 1);
        SetCursorPos(x, y);

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
    private const uint WmMouseWheel = 0x020A;
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
        private readonly List<FramePart> _parts = new();
        private BitmapHolder? _previousHolder;
        private int _unmatchedSteps;
        private bool _finished;

        public CaptureSession(DrawingRectangle region)
        {
            _region = region;
        }

        public int FrameCount => _parts.Count;

        public string? LatestFramePath => _parts.Count == 0 ? null : _parts[^1].Path;

        public CaptureStepResult CaptureCurrent(bool createPreview = true)
        {
            return CaptureCurrent(createPreview, useControlledScrollMatching: false, countUnmatchedStep: true);
        }

        public CaptureStepResult CaptureCurrentForAuto(bool createPreview = true)
        {
            return CaptureCurrent(createPreview, useControlledScrollMatching: true, countUnmatchedStep: false);
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
                    _parts.Add(new FramePart(currentPath, 0));
                    _previousHolder = new BitmapHolder(currentPath);
                    _unmatchedSteps = 0;
                    return CaptureStepResult.Added(createPreview ? CreatePreview() : null, FrameCount);
                }

                int overlap;
                int currentHeight;
                using (var current = new Bitmap(currentPath))
                {
                    var match = FindBestVerticalOverlap(_previousHolder.Bitmap, current);
                    overlap = match.Overlap;
                    currentHeight = current.Height;

                    if (match.IsSameFrame)
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

                if (overlap >= currentHeight - 8)
                {
                    TempImageStore.TryDelete(currentPath);
                    _unmatchedSteps = 0;
                    return CaptureStepResult.Unchanged(FrameCount);
                }

                if (overlap <= 0)
                {
                    return RejectCurrentFrame(currentPath, countUnmatchedStep);
                }

                _parts.Add(new FramePart(currentPath, overlap));
                _previousHolder.Replace(currentPath);
                _unmatchedSteps = 0;
                return CaptureStepResult.Added(createPreview ? CreatePreview() : null, FrameCount);
            }
            catch
            {
                TempImageStore.TryDelete(currentPath);
                throw;
            }
        }

        public CaptureStepResult CaptureAuto()
        {
            ThrowIfFinished();

            CaptureStepResult lastResult = CaptureStepResult.Unchanged(FrameCount);
            while (_parts.Count < MaxFrames)
            {
                lastResult = CaptureAutoStep(createPreview: false);

                if (lastResult.Status == CaptureStepStatus.Indeterminate)
                {
                    continue;
                }

                if (lastResult.Status != CaptureStepStatus.Added)
                {
                    return lastResult;
                }
            }

            return lastResult;
        }

        public CaptureStepResult CaptureAutoStep(bool createPreview = true)
        {
            return CaptureAutoStep(AutoScrollDelta, AutoScrollDelayMs, createPreview);
        }

        public CaptureStepResult CaptureAutoStep(int wheelDelta, int delayMs, bool createPreview = true)
        {
            ThrowIfFinished();

            ScrollWheel(_region, wheelDelta);
            Thread.Sleep(Math.Max(0, delayMs));
            return CaptureCurrentForAuto(createPreview);
        }

        public CaptureStepResult CaptureManualStep(bool createPreview = true)
        {
            ThrowIfFinished();
            return CaptureCurrent(createPreview, useControlledScrollMatching: true, countUnmatchedStep: true);
        }

        public CaptureStepResult ScrollAndCapture(int wheelDelta, DrawingPoint? scrollPoint = null, bool createPreview = true)
        {
            ThrowIfFinished();

            ScrollWheel(_region, NormalizeWheelDelta(wheelDelta), scrollPoint);
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

            return Stitch(_parts, deleteParts: false);
        }

        public void Dispose()
        {
            _previousHolder?.Dispose();
            _previousHolder = null;

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

    private sealed record OverlapMatch(int Overlap, double Score, double AlternateScore, int MinimumOverlap, int FrameHeight)
    {
        public bool IsSameFrame => Score <= SameFrameScore && Overlap >= FrameHeight - 12;

        public bool IsReliable =>
            Overlap >= MinimumOverlap
            && Score <= MatchScoreLimit
            && (AlternateScore - Score >= AmbiguousScoreGap
                || (Score <= StrongMatchScoreLimit && Overlap >= FrameHeight * HighConfidenceOverlapRatio));

        public bool IsUsableForControlledScroll =>
            Overlap >= MinimumOverlap
            && Score <= ControlledScrollMatchScoreLimit;
    }

    private static int NormalizeWheelDelta(int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return -120;
        }

        return wheelDelta;
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

    private sealed record FramePart(string Path, int SkipTop);

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
