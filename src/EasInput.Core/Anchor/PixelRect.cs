using EasInput.Core.Interop;

namespace EasInput.Core;

/// <summary>A rectangle in physical screen pixels.</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PixelRect FromLtrb(int left, int top, int right, int bottom) => new(left, top, right, bottom);

    internal static PixelRect FromRect(in RECT rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);
}

public enum AnchorTier
{
    Caret,
    Uia,
    Hidden,
}

public readonly record struct Anchor(AnchorTier Tier, PixelRect Rect)
{
    public static Anchor Hidden { get; } = new(AnchorTier.Hidden, default);
}
