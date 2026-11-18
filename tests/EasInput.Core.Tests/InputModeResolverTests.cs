using EasInput.Core;

namespace EasInput.Core.Tests;

public class InputModeResolverTests
{
    [Fact]
    public void CapsLockWinsOverChinese()
    {
        Assert.Equal(InputMode.LatinUpper, InputModeResolver.Resolve(imeOpen: true, conversionMode: 0x0001, capsLock: true));
    }

    [Fact]
    public void OpenImeWithNativeBitIsChinese()
    {
        Assert.Equal(InputMode.Chinese, InputModeResolver.Resolve(imeOpen: true, conversionMode: 0x0001, capsLock: false));
    }

    [Fact]
    public void NativeBitWithOtherModesIsStillChinese()
    {
        // Native | FullShape | Roman: still Chinese, full-width punctuation does not change the mode.
        Assert.Equal(InputMode.Chinese, InputModeResolver.Resolve(imeOpen: true, conversionMode: 0x0019, capsLock: false));
    }

    [Fact]
    public void ClosedImeIsEnglishEvenWhenNativeBitIsStale()
    {
        // This is the WeType case: switching to English only closes the IME, leaving IME_CMODE_NATIVE set.
        Assert.Equal(InputMode.LatinLower, InputModeResolver.Resolve(imeOpen: false, conversionMode: 0x0001, capsLock: false));
    }

    [Fact]
    public void FullShapeAloneIsNotChinese()
    {
        Assert.Equal(InputMode.LatinLower, InputModeResolver.Resolve(imeOpen: true, conversionMode: 0x0008, capsLock: false));
    }

    [Fact]
    public void CapsLockOffAndImeClosedIsLowercase()
    {
        Assert.Equal(InputMode.LatinLower, InputModeResolver.Resolve(imeOpen: false, conversionMode: 0, capsLock: false));
    }
}
