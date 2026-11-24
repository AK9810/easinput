using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using EasInput.Core;
using EasInput.Core.Interop;
using EasInput.Core.Logging;

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

    /// <summary>
    /// Removes the hook and reports whether Windows still knew about it. A false return means the
    /// hook was already gone — the OS silently drops a low-level hook whose callback times out —
    /// which is one of the drifts a restart cures. Idempotent.
    /// </summary>
    public bool Unhook(bool reportAtWarningLevel = false)
    {
        if (_hook == IntPtr.Zero)
        {
            return true;
        }

        var removed = WindowInterop.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;

        if (!removed)
        {
            Log.Warn("Keyboard hook: Windows no longer knew it (silently removed earlier).");
        }
        else if (reportAtWarningLevel)
        {
            Log.Warn("Keyboard hook: removed cleanly.");
        }
        else
        {
            Log.Debug("Keyboard hook: removed cleanly.");
        }

        return removed;
    }

    public void Dispose() => Unhook();

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
                    // Timestamp before the dispatcher hop so the measurement covers the whole path.
                    var timestamp = Stopwatch.GetTimestamp();
                    _dispatcher.BeginInvoke(
                        DispatcherPriority.Input,
                        () => _controller.OnKey(virtualKey, isDown, timestamp));
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

        // A plain keystroke matters while the bubble is visible or a show is still being
        // resolved: in both cases it must dismiss or cancel the pending bubble.
        return isDown && (_controller.IsBubbleVisible || _controller.IsShowPending);
    }
}
