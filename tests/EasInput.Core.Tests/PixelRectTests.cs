using EasInput.Core;

namespace EasInput.Core.Tests;

public class PixelRectTests
{
    [Fact]
    public void IntersectsReturnsTrueWhenRectanglesOverlap()
    {
        var rect = PixelRect.FromLtrb(10, 10, 30, 30);
        var other = PixelRect.FromLtrb(20, 20, 40, 40);

        Assert.True(rect.Intersects(other));
    }

    [Fact]
    public void IntersectsReturnsTrueWhenOneRectangleContainsTheOther()
    {
        var outer = PixelRect.FromLtrb(0, 0, 100, 100);
        var inner = PixelRect.FromLtrb(20, 20, 40, 40);

        Assert.True(outer.Intersects(inner));
        Assert.True(inner.Intersects(outer));
    }

    [Fact]
    public void IntersectsReturnsFalseWhenEdgesOnlyTouch()
    {
        var rect = PixelRect.FromLtrb(10, 10, 30, 30);
        var right = PixelRect.FromLtrb(30, 10, 50, 30);
        var below = PixelRect.FromLtrb(10, 30, 30, 50);

        Assert.False(rect.Intersects(right));
        Assert.False(rect.Intersects(below));
    }

    [Fact]
    public void IntersectsReturnsFalseWhenSeparated()
    {
        var rect = PixelRect.FromLtrb(10, 10, 30, 30);
        var other = PixelRect.FromLtrb(100, 100, 120, 120);

        Assert.False(rect.Intersects(other));
    }
}
