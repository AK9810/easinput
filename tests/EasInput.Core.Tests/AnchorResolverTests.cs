using EasInput.Core;

namespace EasInput.Core.Tests;

public class AnchorResolverTests
{
    private static readonly IntPtr Window = new(42);
    private static readonly PixelRect Win32Rect = PixelRect.FromLtrb(10, 10, 11, 26);
    private static readonly PixelRect MsaaRect = PixelRect.FromLtrb(15, 15, 16, 41);
    private static readonly PixelRect UiaRect = PixelRect.FromLtrb(20, 20, 21, 46);

    [Fact]
    public void Win32CaretWinsOverMsaaAndUia()
    {
        var resolver = new AnchorResolver(
            new FakeLocator(Win32Rect), new FakeLocator(MsaaRect), new FakeLocator(UiaRect));

        var anchor = resolver.Resolve(Window);

        Assert.Equal(AnchorTier.Caret, anchor.Tier);
        Assert.Equal(Win32Rect, anchor.Rect);
    }

    [Fact]
    public void FallsBackToMsaaWhenWin32CaretIsMissing()
    {
        // VS Code and ZCode expose no Win32 caret; MSAA OBJID_CARET has the real caret even
        // when the document is empty, while UIA answers with the whole line box.
        var resolver = new AnchorResolver(
            new FakeLocator(null), new FakeLocator(MsaaRect), new FakeLocator(UiaRect));

        var anchor = resolver.Resolve(Window);

        Assert.Equal(AnchorTier.Msaa, anchor.Tier);
        Assert.Equal(MsaaRect, anchor.Rect);
    }

    [Fact]
    public void FallsBackToUiaWhenBothCaretSourcesAreMissing()
    {
        var resolver = new AnchorResolver(
            new FakeLocator(null), new FakeLocator(null), new FakeLocator(UiaRect));

        var anchor = resolver.Resolve(Window);

        Assert.Equal(AnchorTier.Uia, anchor.Tier);
        Assert.Equal(UiaRect, anchor.Rect);
    }

    [Fact]
    public void HidesWhenAllCaretLocatorsAreMissing()
    {
        var resolver = new AnchorResolver(
            new FakeLocator(null), new FakeLocator(null), new FakeLocator(null));

        var anchor = resolver.Resolve(Window);

        Assert.Equal(AnchorTier.Hidden, anchor.Tier);
        Assert.Equal(default, anchor.Rect);
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
