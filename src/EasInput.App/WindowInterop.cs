using System.Runtime.InteropServices;

namespace EasInput.App;

internal static class WindowInterop
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint WineventOutofcontext = 0x0000;

    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    internal delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    internal static class KeyboardHook
    {
        public static IntPtr Install(LowLevelKeyboardProc proc)
        {
            return SetWindowsHookExW(WhKeyboardLl, proc, GetModuleHandleW(null), 0);
        }

        public static bool IsKeyDownMessage(int message) => message is WmKeyDown or WmSysKeyDown;

        public static bool IsKeyUpMessage(int message) => message is WmKeyUp or WmSysKeyUp;
    }

    internal static class ForegroundHook
    {
        private const uint EventSystemForeground = 0x0003;
        public const uint EventSystemMinimizeStart = 0x0016;

        public static IntPtr Install(WinEventDelegate callback) =>
            InstallFor(EventSystemForeground, callback);

        public static IntPtr InstallMinimize(WinEventDelegate callback) =>
            InstallFor(EventSystemMinimizeStart, callback);

        private static IntPtr InstallFor(uint eventType, WinEventDelegate callback)
        {
            return SetWinEventHook(
                eventType, eventType, IntPtr.Zero, callback, 0, 0, WineventOutofcontext);
        }
    }
}
