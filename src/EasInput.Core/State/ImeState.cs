using EasInput.Core.Interop;

namespace EasInput.Core;

/// <summary>
/// A reading of the foreground thread's IME. <see cref="ConversionMode"/> can be stale while the
/// IME is closed, which is why <see cref="IsChinese"/> requires both signals.
/// </summary>
public readonly record struct ImeState(bool IsOpen, uint ConversionMode)
{
    public bool IsChinese => IsOpen && (ConversionMode & ImeConversionMode.Native) != 0;
}

public static class InputModeResolver
{
    /// <summary>
    /// Caps Lock wins over Chinese: with a Chinese IME open, a locked Caps Lock still types
    /// uppercase Latin letters.
    /// </summary>
    public static InputMode Resolve(bool imeOpen, uint conversionMode, bool capsLock)
    {
        if (capsLock)
        {
            return InputMode.LatinUpper;
        }

        var chinese = imeOpen && (conversionMode & ImeConversionMode.Native) != 0;
        return chinese ? InputMode.Chinese : InputMode.LatinLower;
    }
}
