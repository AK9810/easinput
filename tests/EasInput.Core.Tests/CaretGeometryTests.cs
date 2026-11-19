using EasInput.Core;

namespace EasInput.Core.Tests;

/// <summary>
/// Candidate rectangles measured from real applications: a collapsed UIA selection in VS Code
/// answers with the whole line box, while MSAA reports the caret itself, sometimes zero-width.
/// </summary>
public class CaretGeometryTests
{
    [Fact]
    public void KeepsThinCaretRect()
    {
        // MSAA OBJID_CARET in VS Code's editor: 1x27 at the caret.
        var candidate = PixelRect.FromLtrb(1140, 493, 1141, 520);

        Assert.True(CaretGeometry.TryCaretBar(candidate, out var caret));
        Assert.Equal(candidate, caret);
    }

    [Fact]
    public void NormalizesZeroWidthCaretToOnePixel()
    {
        // MSAA returns a zero-width caret in VS Code's SCM input.
        var candidate = PixelRect.FromLtrb(506, 234, 506, 275);

        Assert.True(CaretGeometry.TryCaretBar(candidate, out var caret));
        Assert.Equal(PixelRect.FromLtrb(506, 234, 507, 275), caret);
    }

    [Fact]
    public void RejectsWholeLineReportedForCollapsedSelection()
    {
        // UIA TextPattern in VS Code: the collapsed range rect spans the whole line.
        var candidate = PixelRect.FromLtrb(959, 493, 2047, 519);

        Assert.False(CaretGeometry.TryCaretBar(candidate, out var caret));
        Assert.Equal(default, caret);
    }

    [Fact]
    public void RejectsEmptyRect()
    {
        var candidate = PixelRect.FromLtrb(0, 0, 0, 0);

        Assert.False(CaretGeometry.TryCaretBar(candidate, out _));
    }

    [Fact]
    public void RejectsNegativeWidth()
    {
        var candidate = PixelRect.FromLtrb(500, 100, 400, 120);

        Assert.False(CaretGeometry.TryCaretBar(candidate, out _));
    }

    [Fact]
    public void CharacterBoxKeepsGlyphSizedRect()
    {
        // A one-character UIA range may be as wide as the glyph, never as wide as the line.
        var candidate = PixelRect.FromLtrb(959, 493, 973, 517);

        Assert.True(CaretGeometry.TryCharacterBox(candidate, out var caret));
        Assert.Equal(candidate, caret);
    }

    [Fact]
    public void CharacterBoxRejectsWholeLineRect()
    {
        var candidate = PixelRect.FromLtrb(959, 493, 2047, 519);

        Assert.False(CaretGeometry.TryCharacterBox(candidate, out _));
    }
}
