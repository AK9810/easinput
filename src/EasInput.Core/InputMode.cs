namespace EasInput.Core;

/// <summary>
/// What the user is about to type. Deliberately about the mode, not about which IME is installed:
/// a Chinese IME in English mode and a US keyboard both resolve to <see cref="LatinLower"/>.
/// </summary>
public enum InputMode
{
    Chinese,
    LatinLower,
    LatinUpper,
}
