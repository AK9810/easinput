using EasInput.Core.Interop;

namespace EasInput.Core;

public static class KeyKinds
{
    public static bool IsModifier(int virtualKey) =>
        virtualKey is NativeMethods.VkShift
            or NativeMethods.VkControl
            or NativeMethods.VkMenu
            or NativeMethods.VkLWin
            or NativeMethods.VkRWin;
}
