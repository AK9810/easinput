using System.Windows.Threading;
using EasInput.Core;
using EasInput.Core.Interop;

namespace EasInput.App;

/// <summary>
/// Owns the state machine: read the foreground thread's mode off the UI thread, then apply the
/// result on the UI thread. The bubble is shown only on an effective state change.
/// </summary>
public sealed class BubbleController : IDisposable
{
    private readonly AppConfig _config;
    private readonly BubbleWindow _bubble;
    private readonly Dispatcher _dispatcher;
    private readonly AnchorResolver _anchors = new(new Win32CaretLocator());
    private readonly IImeStateSource _ime = new Win32ImeStateSource();
    private readonly CapsLockTracker _caps = new();
    private readonly System.Threading.Timer _poll;
    private readonly System.Threading.Timer _debounce;

    private int _reading;
    private int _disposed;
    private InputMode? _lastObserved;
    private IntPtr _shownFor;

    public BubbleController(AppConfig config, BubbleWindow bubble, Dispatcher dispatcher)
    {
        _config = config;
        _bubble = bubble;
        _dispatcher = dispatcher;
        _poll = new System.Threading.Timer(_ => ReadAndApply(), null, Timeout.Infinite, Timeout.Infinite);
        _debounce = new System.Threading.Timer(_ => ReadAndApply(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool IsBubbleVisible => _bubble.IsShowing;

    public void Start()
    {
        _caps.Seed();
        if (TryReadMode(NativeMethods.GetForegroundWindow(), out var mode))
        {
            _lastObserved = mode; // never pop a bubble at startup
        }

        _poll.Change(300, 300);
    }

    public void OnKey(int virtualKey, bool isDown)
    {
        if (virtualKey == NativeMethods.VkCapital && isDown)
        {
            _caps.OnKeyDown(virtualKey);
            Schedule(10);
            return;
        }

        if (KeyKinds.IsModifier(virtualKey))
        {
            if (!isDown)
            {
                // The IME toggle lands on key-up; give the IME a moment to update its state.
                Schedule(30);
            }

            return;
        }

        if (isDown)
        {
            HideBubble();
        }
    }

    public void OnForegroundChanged()
    {
        HideIfFocusLost(NativeMethods.GetForegroundWindow());
        Schedule(50);
    }

    public void OnMinimized(IntPtr hwnd)
    {
        if (hwnd == _shownFor)
        {
            HideBubble();
        }
    }

    public void OnEnabledChanged()
    {
        if (!_config.Enabled)
        {
            Post(HideBubble);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _poll.Dispose();
        _debounce.Dispose();
    }

    private void Schedule(int delayMs)
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        _debounce.Change(delayMs, Timeout.Infinite);
    }

    private void ReadAndApply()
    {
        if (Interlocked.Exchange(ref _reading, 1) == 1)
        {
            return;
        }

        try
        {
            if (Volatile.Read(ref _disposed) == 1)
            {
                return;
            }

            if (!_config.Enabled)
            {
                Post(HideBubble);
                return;
            }

            var foreground = NativeMethods.GetForegroundWindow();
            if (!TryReadMode(foreground, out var mode))
            {
                Post(HideBubble);
                return;
            }

            Post(() => Apply(mode, foreground));
        }
        finally
        {
            Interlocked.Exchange(ref _reading, 0);
        }
    }

    private void Post(Action action)
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        try
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
        }
        catch (TaskCanceledException)
        {
            // The dispatcher is shutting down.
        }
        catch (InvalidOperationException)
        {
            // The dispatcher is shutting down.
        }
    }

    private void Apply(InputMode mode, IntPtr foreground)
    {
        HideIfFocusLost(foreground);

        // A minimized window has no usable anchor; the poll may still read its IME state.
        if (NativeMethods.IsIconic(foreground))
        {
            HideBubble();
            return;
        }

        if (_lastObserved == mode)
        {
            return;
        }

        _lastObserved = mode;

        var anchor = _anchors.Resolve(foreground);
        if (anchor.Tier == AnchorTier.Hidden)
        {
            HideBubble();
            return;
        }

        var workArea = WorkArea.Get(anchor.Rect.Left, anchor.Rect.Top);
        _shownFor = foreground;
        _bubble.ShowAt(anchor.Rect, mode, _config.ShortCodes.For(mode), _config, workArea);
    }

    private void HideIfFocusLost(IntPtr foreground)
    {
        if (_bubble.IsShowing && foreground != _shownFor)
        {
            HideBubble();
        }
    }

    private void HideBubble()
    {
        _shownFor = IntPtr.Zero;
        _bubble.HideNow();
    }

    private bool TryReadMode(IntPtr foreground, out InputMode mode)
    {
        mode = default;
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        if (!_ime.TryRead(foreground, out var state))
        {
            return false;
        }

        mode = InputModeResolver.Resolve(state.IsOpen, state.ConversionMode, _caps.IsLocked);
        return true;
    }
}
