namespace EasInput.Core;

/// <summary>
/// Validates candidate caret rectangles reported by text APIs. A caret is a thin vertical bar;
/// some UIA providers answer a collapsed selection with the whole line box, and taking that box
/// as the anchor centers the bubble in the middle of the line. See ADR-0003.
/// </summary>
public static class CaretGeometry
{
    /// <summary>
    /// Accepts a caret bar: a zero-width rect is normalized to one pixel, and a rect wider than
    /// a bar — a line or word box — is rejected.
    /// </summary>
    public static bool TryCaretBar(PixelRect candidate, out PixelRect caret)
    {
        caret = default;
        if (candidate.Height <= 0 || candidate.Width < 0)
        {
            return false;
        }

        var width = Math.Max(1, candidate.Width);
        if (width > MaxCaretWidth(candidate.Height))
        {
            return false;
        }

        caret = PixelRect.FromLtrb(candidate.Left, candidate.Top, candidate.Left + width, candidate.Bottom);
        return true;
    }

    /// <summary>
    /// Accepts a one-character box used when a collapsed range reports no rectangle: it may be
    /// as wide as the glyph, but not as wide as the line.
    /// </summary>
    public static bool TryCharacterBox(PixelRect candidate, out PixelRect caret)
    {
        caret = default;
        if (candidate.Height <= 0 || candidate.Width <= 0 || candidate.Width > candidate.Height * 2)
        {
            return false;
        }

        caret = candidate;
        return true;
    }

    // DPI rounding can make a one-pixel caret two or three pixels wide; half the line height
    // still leaves no room for a line box.
    private static int MaxCaretWidth(int height) => Math.Max(4, height / 2);
}
