using EasInput.Core;

namespace EasInput.Core.Tests;

public class AnchorResolverTests
{
    private static readonly IntPtr Window = new(42);
    private static readonly PixelRect CaretRect = PixelRect.FromLtrb(10, 10, 11, 26);
    private static readonly PixelRect UiaRect = PixelRect.FromLtrb(20, 20, 21, 46);
    private static readonly PixelRect PointerRect = PixelRect.FromLtrb(30, 30, 31, 31);

    [Fact]
    public void Win32CaretWinsOverUiaAndPointer()
    {
        var resolver = new AnchorResolver(
            new FakeLocator(CaretRect), new FakeLocator(UiaRect), new FakeLocator(PointerRect));

        var anchor = resolver.Resolve(Window);

        Assert.Equal(AnchorTier.Caret, anchor.Tier);
        Assert.Equal(CaretRect, anchor.Rect);
    }

    [Fact]
    public void FallsBackToUiaWhenWin32CaretIsMissing()
    {
        var resolver = new AnchorResolver(
            new FakeLocator(null), new FakeLocator(UiaRect), new FakeLocator(PointerRect));

        var anchor = resolver.Resolve(Window);

        Assert.Equal(AnchorTier.Uia, anchor.Tier);
        Assert.Equal(UiaRect, anchor.Rect);
    }

    [Fact]
    public void FallsBackToPointerWhenBothCaretsAreMissing()
    {
        var resolver = new AnchorResolver(
            new FakeLocator(null), new FakeLocator(null), new FakeLocator(PointerRect));

        var anchor = resolver.Resolve(Window);

        Assert.Equal(AnchorTier.Pointer, anchor.Tier);
        Assert.Equal(PointerRect, anchor.Rect);
    }

    [Fact]
    public void HidesWhenNothingIsFound()
    {
        var resolver = new AnchorResolver(
            new FakeLocator(null), new FakeLocator(null), new FakeLocator(null));

        var anchor = resolver.Resolve(Window);

        Assert.Equal(AnchorTier.Hidden, anchor.Tier);
    }

    private sealed class FakeLocator : ICaretLocator
    {
        private readonly PixelRect? _rect;

        public FakeLocator(PixelRect? rect) => _rect = rect;

        public bool TryLocate(IntPtr foregroundWindow, out PixelRect caret)
        {
            caret = _rect ?? default;
            return _rect is not null;
        }
    }
}
