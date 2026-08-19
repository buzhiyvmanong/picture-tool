using PictureTool.Services;
using Xunit;
using DrawingRectangle = System.Drawing.Rectangle;

namespace PictureTool.Tests;

public class ScrollCaptureLogicTests
{
    [Fact]
    public void GetContentCaptureRegion_TrimsEdges()
    {
        var region = new DrawingRectangle(100, 200, 800, 600);
        var result = ScrollCaptureService.GetContentCaptureRegion(region);

        Assert.Equal(103, result.Left);
        Assert.Equal(203, result.Top);
        Assert.Equal(794, result.Width);
        Assert.Equal(594, result.Height);
    }

    [Fact]
    public void GetContentCaptureRegion_SmallRegion_ClampsTrim()
    {
        var region = new DrawingRectangle(0, 0, 8, 8);
        var result = ScrollCaptureService.GetContentCaptureRegion(region);

        var maxTrim = Math.Min(8, 8) / 4; // 2
        Assert.Equal(maxTrim, result.Left);
        Assert.Equal(maxTrim, result.Top);
        Assert.Equal(8 - maxTrim * 2, result.Width);
    }

    [Fact]
    public void GetContentCaptureRegion_ZeroSize_ReturnsOriginal()
    {
        var region = new DrawingRectangle(50, 50, 0, 0);
        var result = ScrollCaptureService.GetContentCaptureRegion(region);

        Assert.Equal(region, result);
    }

    [Fact]
    public void OverlapMatch_IsSameFrame_WhenScoreVeryLowAndFullOverlap()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 500, Score: 1.0, AlternateScore: 50.0, MinimumOverlap: 48, FrameHeight: 510);

        Assert.True(match.IsSameFrame);
    }

    [Fact]
    public void OverlapMatch_NotSameFrame_WhenScoreHigh()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 500, Score: 10.0, AlternateScore: 50.0, MinimumOverlap: 48, FrameHeight: 510);

        Assert.False(match.IsSameFrame);
    }

    [Fact]
    public void OverlapMatch_NotSameFrame_WhenOverlapSmall()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 100, Score: 1.0, AlternateScore: 50.0, MinimumOverlap: 48, FrameHeight: 500);

        Assert.False(match.IsSameFrame);
    }

    [Fact]
    public void OverlapMatch_IsReliable_GoodScoreWithGap()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 100, Score: 5.0, AlternateScore: 20.0, MinimumOverlap: 48, FrameHeight: 500);

        Assert.True(match.IsReliable);
    }

    [Fact]
    public void OverlapMatch_NotReliable_WhenScoreTooHigh()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 100, Score: 20.0, AlternateScore: 50.0, MinimumOverlap: 48, FrameHeight: 500);

        Assert.False(match.IsReliable);
    }

    [Fact]
    public void OverlapMatch_NotReliable_WhenOverlapBelowMinimum()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 30, Score: 5.0, AlternateScore: 20.0, MinimumOverlap: 48, FrameHeight: 500);

        Assert.False(match.IsReliable);
    }

    [Fact]
    public void OverlapMatch_NotReliable_AmbiguousAlternate()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 100, Score: 14.0, AlternateScore: 15.0, MinimumOverlap: 48, FrameHeight: 500);

        Assert.False(match.IsReliable);
    }

    [Fact]
    public void OverlapMatch_IsReliable_StrongMatchHighOverlap()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 300, Score: 8.0, AlternateScore: 9.0, MinimumOverlap: 48, FrameHeight: 500);

        Assert.True(match.IsReliable);
    }

    [Fact]
    public void OverlapMatch_IsUsableForControlledScroll()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 100, Score: 20.0, AlternateScore: 22.0, MinimumOverlap: 48, FrameHeight: 500);

        Assert.True(match.IsUsableForControlledScroll);
    }

    [Fact]
    public void OverlapMatch_NotUsableForControlledScroll_ScoreTooHigh()
    {
        var match = new ScrollCaptureService.OverlapMatch(
            Overlap: 100, Score: 30.0, AlternateScore: 50.0, MinimumOverlap: 48, FrameHeight: 500);

        Assert.False(match.IsUsableForControlledScroll);
    }
}
