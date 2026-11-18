using EasInput.Core;

namespace EasInput.Core.Tests;

public class BubblePlacementTests
{
    private static readonly PixelRect WorkArea = PixelRect.FromLtrb(0, 0, 1920, 1080);

    [Fact]
    public void CentersBelowTheAnchor()
    {
        var anchor = PixelRect.FromLtrb(100, 200, 102, 220);

        var placed = BubblePlacement.Compute(anchor, width: 30, height: 23, gap: 4, workArea: WorkArea);

        Assert.Equal(PixelRect.FromLtrb(86, 224, 116, 247), placed);
    }

    [Fact]
    public void FlipsAboveWhenThereIsNoRoomBelow()
    {
        var anchor = PixelRect.FromLtrb(100, 200, 102, 220);
        var shortScreen = PixelRect.FromLtrb(0, 0, 1920, 240);

        var placed = BubblePlacement.Compute(anchor, width: 30, height: 23, gap: 4, workArea: shortScreen);

        Assert.Equal(PixelRect.FromLtrb(86, 173, 116, 196), placed);
    }

    [Fact]
    public void ClampsToTheLeftEdge()
    {
        var anchor = PixelRect.FromLtrb(0, 200, 2, 220);

        var placed = BubblePlacement.Compute(anchor, width: 30, height: 23, gap: 4, workArea: WorkArea);

        Assert.Equal(0, placed.Left);
    }

    [Fact]
    public void ClampsToTheRightEdge()
    {
        var anchor = PixelRect.FromLtrb(1919, 200, 1920, 220);

        var placed = BubblePlacement.Compute(anchor, width: 30, height: 23, gap: 4, workArea: WorkArea);

        Assert.Equal(1920 - 30, placed.Left);
        Assert.Equal(1920, placed.Right);
    }

    [Fact]
    public void ClampsToTheTopEdge()
    {
        var anchor = PixelRect.FromLtrb(100, -50, 102, -30);

        var placed = BubblePlacement.Compute(anchor, width: 30, height: 23, gap: 4, workArea: WorkArea);

        Assert.Equal(0, placed.Top);
    }

    [Fact]
    public void PreservesRequestedSize()
    {
        var anchor = PixelRect.FromLtrb(500, 500, 501, 516);

        var placed = BubblePlacement.Compute(anchor, width: 37, height: 29, gap: 4, workArea: WorkArea);

        Assert.Equal(37, placed.Width);
        Assert.Equal(29, placed.Height);
    }

    [Fact]
    public void RejectsNonPositiveSize()
    {
        var anchor = PixelRect.FromLtrb(500, 500, 501, 516);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => BubblePlacement.Compute(anchor, width: 0, height: 23, gap: 4, workArea: WorkArea));
    }
}
