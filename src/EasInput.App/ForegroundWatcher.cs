using System.Windows.Threading;
using EasInput.Core.Logging;

namespace EasInput.App;

/// <summary>Wakes the controller when the focus window changes or minimizes.</summary>
public sealed class ForegroundWatcher : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly BubbleController _controller;
    private readonly WindowInterop.WinEventDelegate _callback;
    private IntPtr _hook;
    private IntPtr _minimizeHook;

    public ForegroundWatcher(Dispatcher dispatcher, BubbleController controller)
    {
        _dispatcher = dispatcher;
        _controller = controller;
        _callback = OnWinEvent;
    }

    public bool Install()
    {
        _hook = WindowInterop.ForegroundHook.Install(_callback);
        _minimizeHook = WindowInterop.ForegroundHook.InstallMinimize(_callback);
        if (_minimizeHook == IntPtr.Zero)
        {
            Log.Warn("Minimize WinEvent hook failed to install.");
        }

        return _hook != IntPtr.Zero;
    }

    /// <summary>
    /// Removes both WinEvent hooks and reports whether Windows still knew about each. A false
    /// return means that hook was already gone, the same silent timeout that kills the keyboard
    /// hook. Idempotent.
    /// </summary>
    public bool Unhook(bool reportAtWarningLevel = false)
    {
        // Second call (the restart uninstalls explicitly, then Dispose runs): nothing left to say.
        if (_hook == IntPtr.Zero && _minimizeHook == IntPtr.Zero)
        {
            return true;
        }

        var foreground = UnhookOne(ref _hook);
        var minimize = UnhookOne(ref _minimizeHook);

        if (reportAtWarningLevel || !foreground || !minimize)
        {
            Log.Warn($"Foreground hooks: removed={foreground}/{minimize}.");
        }
        else
        {
            Log.Debug("Foreground hooks: removed cleanly.");
        }

        return foreground;
    }

    public void Dispose() => Unhook();

    private static bool UnhookOne(ref IntPtr hook)
    {
        if (hook == IntPtr.Zero)
        {
            return true;
        }

        var removed = WindowInterop.UnhookWinEvent(hook);
        hook = IntPtr.Zero;
        return removed;
    }

    private void OnWinEvent(
        IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            if (eventType == WindowInterop.ForegroundHook.EventSystemMinimizeStart)
            {
                // OBJID_WINDOW only: ignore minimize events for child objects.
                if (idObject != 0 || hwnd == IntPtr.Zero)
                {
                    return;
                }

                _dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    () => _controller.OnMinimized(hwnd));
                return;
            }

            _dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                () => _controller.OnForegroundChanged());
        }
        catch (TaskCanceledException)
        {
            // Shutting down.
        }
        catch (InvalidOperationException)
        {
            // Shutting down.
        }
    }
}
