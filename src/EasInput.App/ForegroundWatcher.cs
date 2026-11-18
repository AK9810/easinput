using System.Windows.Threading;

namespace EasInput.App;

/// <summary>Wakes the controller when the focus window changes, so per-window IME state is re-read.</summary>
public sealed class ForegroundWatcher : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly BubbleController _controller;
    private readonly WindowInterop.WinEventDelegate _callback;
    private IntPtr _hook;

    public ForegroundWatcher(Dispatcher dispatcher, BubbleController controller)
    {
        _dispatcher = dispatcher;
        _controller = controller;
        _callback = OnForeground;
    }

    public bool Install() => (_hook = WindowInterop.ForegroundHook.Install(_callback)) != IntPtr.Zero;

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            WindowInterop.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private void OnForeground(
        IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint thread, uint time)
    {
        try
        {
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
