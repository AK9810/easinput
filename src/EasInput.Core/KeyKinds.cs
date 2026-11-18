namespace EasInput.Core;

public static class KeyKinds
{
    public static bool IsModifier(int virtualKey) =>
        virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C; // Shift, Ctrl, Alt, LWin, RWin
}
