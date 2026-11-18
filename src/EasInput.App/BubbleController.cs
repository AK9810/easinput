using System.Windows.Threading;
using EasInput.Core;
using EasInput.Core.Interop;
using EasInput.Core.Logging;

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
    private readonly AnchorResolver _anchors = new(new Win32CaretLocator(), new UiaCaretLocator());
    private readonly IImeStateSource _ime = new Win32ImeStateSource();
    private readonly CapsLockTracker _caps = new();
    private readonly System.Threading.Timer _poll;
    private readonly System.Threading.Timer _debounce;

    private int _reading;
    private int _disposed;
    private InputMode? _lastObserved;
    private IntPtr _shownFor;
    private long _keyTimestamp;

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

    public void OnKey(int virtualKey, bool isDown, long timestamp)
    {
        if (virtualKey == NativeMethods.VkCapital && isDown)
        {
            _caps.OnKeyDown(virtualKey);
            _keyTimestamp = timestamp;
            Schedule(10);
            return;
        }

        if (KeyKinds.IsModifier(virtualKey))
        {
            if (!isDown)
            {
                // The IME toggle lands on key-up; give the IME a moment to update its state.
                _keyTimestamp = timestamp;
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
        // A pending key sample belongs to the old foreground; don't attribute it to this show.
        _keyTimestamp = 0;
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

    /// <summary>
    /// Shows the bubble once for the given window (the one that was foreground when the tray
    /// menu opened), reusing the same anchor fallback and hold/fade timing as a real key.
    /// </summary>
    public void Preview(IntPtr foreground)
    {
        Post(() => ShowPreview(foreground, 0));
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
        _bubble.ShowAt(anchor.Rect, mode, _config.ShortCodes.For(mode), _config, workArea, _keyTimestamp);
        _keyTimestamp = 0;
    }

    private void ShowPreview(IntPtr preferred, int attempt)
    {
        var foreground = preferred;

        // While the tray menu is closing the foreground can still be ours (or briefly zero).
        // The window the user was working in comes back a few hundred milliseconds later.
        if (IsOwnWindow(foreground))
        {
            foreground = NativeMethods.GetForegroundWindow();
        }

        if (IsOwnWindow(foreground) ||
            NativeMethods.IsIconic(foreground) ||
            !TryReadMode(foreground, out var mode))
        {
            RetryPreview(attempt);
            return;
        }

        var anchor = _anchors.Resolve(foreground);
        if (anchor.Tier == AnchorTier.Hidden)
        {
            RetryPreview(attempt);
            return;
        }

        // ShowAt repositions and restarts the hold timer when a bubble is already up.
        _lastObserved = mode;
        _shownFor = foreground;
        _bubble.ShowAt(
            anchor.Rect,
            mode,
            _config.ShortCodes.For(mode),
            _config,
            WorkArea.Get(anchor.Rect.Left, anchor.Rect.Top));
    }

    private void RetryPreview(int attempt)
    {
        const int MaxAttempts = 8;

        if (attempt >= MaxAttempts)
        {
            Log.Warn("Preview: no usable foreground window.");
            return;
        }

        var timer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(80),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ShowPreview(IntPtr.Zero, attempt + 1);
        };
        timer.Start();
    }

    private static bool IsOwnWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return true;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == (uint)Environment.ProcessId;
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
