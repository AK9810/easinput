using System.Runtime.InteropServices;
using System.Windows.Threading;
using EasInput.Core;
using EasInput.Core.Interop;

namespace EasInput.App;

/// <summary>
/// Global low-level keyboard hook. The callback must return fast, so it only decides whether an
/// event matters and posts it to the dispatcher; all real work happens after the hook returns.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly BubbleController _controller;
    private readonly WindowInterop.LowLevelKeyboardProc _callback;
    private IntPtr _hook;

    public KeyboardHook(Dispatcher dispatcher, BubbleController controller)
    {
        _dispatcher = dispatcher;
        _controller = controller;
        _callback = OnKey; // keep a managed reference alive for the lifetime of the hook
    }

    public bool Install() => (_hook = WindowInterop.KeyboardHook.Install(_callback)) != IntPtr.Zero;

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            WindowInterop.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr OnKey(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var isDown = WindowInterop.KeyboardHook.IsKeyDownMessage(message);
            var isUp = WindowInterop.KeyboardHook.IsKeyUpMessage(message);

            if (isDown || isUp)
            {
                var virtualKey = Marshal.ReadInt32(lParam);
                if (ShouldNotify(virtualKey, isDown))
                {
                    _dispatcher.BeginInvoke(
                        DispatcherPriority.Input,
                        () => _controller.OnKey(virtualKey, isDown));
                }
            }
        }

        return WindowInterop.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool ShouldNotify(int virtualKey, bool isDown)
    {
        if (virtualKey == NativeMethods.VkCapital)
        {
            return true;
        }

        if (KeyKinds.IsModifier(virtualKey))
        {
            return !isDown;
        }

        // A plain keystroke only matters while the bubble is visible: it dismisses it.
        return isDown && _controller.IsBubbleVisible;
    }
}
