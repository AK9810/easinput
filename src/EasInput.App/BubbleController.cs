using System.Windows.Threading;
using EasInput.Core;
using EasInput.Core.Interop;
using EasInput.Core.Logging;

namespace EasInput.App;

/// <summary>
/// Owns the state machine: read the foreground thread's mode off the UI thread, then apply the
/// result on the UI thread. The bubble is shown on an input-mode change and whenever focus moves
/// to a different window that has a real caret.
/// </summary>
public sealed class BubbleController : IDisposable
{
    // A focus switch can land before Chromium/Electron has created the caret for the new focused
    // element. Resolve, then retry a couple of times before consuming the trigger.
    private const int MaxAnchorAttempts = 3;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(150);

    private readonly AppConfig _config;
    private readonly BubbleWindow _bubble;
    private readonly Dispatcher _dispatcher;
    private readonly AnchorResolver _anchors = new(
        new Win32CaretLocator(), new MsaaCaretLocator(), new UiaCaretLocator());
    private readonly IImeStateSource _ime = new Win32ImeStateSource();
    private readonly CapsLockTracker _caps = new();
    private readonly TriggerPolicy _triggers = new();
    private readonly SemaphoreSlim _resolveGate = new(1, 1);
    private readonly System.Threading.Timer _poll;
    private readonly System.Threading.Timer _debounce;
    private readonly DispatcherTimer _retry;

    private int _reading;
    private int _disposed;
    private int _resolveGeneration;
    private bool _showPending;
    private IntPtr _shownFor;
    private long _keyTimestamp;

    private int _retryGeneration;
    private InputMode _retryMode;
    private IntPtr _retryForeground;
    private int _retryAttempt;

    private IntPtr _pendingForeground;

    public BubbleController(AppConfig config, BubbleWindow bubble, Dispatcher dispatcher)
    {
        _config = config;
        _bubble = bubble;
        _dispatcher = dispatcher;
        _poll = new System.Threading.Timer(_ => ReadAndApply(), null, Timeout.Infinite, Timeout.Infinite);
        _debounce = new System.Threading.Timer(_ => ReadAndApply(), null, Timeout.Infinite, Timeout.Infinite);
        _retry = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
        _retry.Tick += (_, _) => OnRetryTick();
    }

    public bool IsBubbleVisible => _bubble.IsShowing;

    /// <summary>True while an anchor resolve may still produce a bubble; a keypress must cancel it.</summary>
    public bool IsShowPending => _showPending;

    public void Start()
    {
        _caps.Seed();
        var foreground = NativeMethods.GetForegroundWindow();
        if (TryReadMode(foreground, out var mode))
        {
            _triggers.Seed(mode, foreground); // never pop a bubble at startup
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
        var foreground = NativeMethods.GetForegroundWindow();
        _keyTimestamp = 0;

        // Cancel an in-flight resolve only when the foreground actually moved: a spurious event
        // for the same window must not drop a pending retry.
        if (_showPending && foreground != _pendingForeground)
        {
            CancelPendingShow();
        }

        HideIfFocusLost(foreground);
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

        Interlocked.Increment(ref _resolveGeneration); // discard in-flight results
        _poll.Dispose();
        _debounce.Dispose();
        _retry.Stop();
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
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        // A newer foreground has its own read in flight; don't show a bubble for a stale window.
        if (NativeMethods.GetForegroundWindow() != foreground)
        {
            return;
        }

        HideIfFocusLost(foreground);

        // A minimized window has no usable anchor; the poll may still read its IME state.
        if (NativeMethods.IsIconic(foreground))
        {
            HideBubble();
            return;
        }

        var trigger = _triggers.Observe(mode, foreground);
        if (trigger == BubbleTrigger.None)
        {
            return;
        }

        Log.Debug($"Trigger: {trigger} hwnd=0x{foreground.ToInt64():X} mode={mode}");
        _retry.Stop();
        StartResolve(mode, foreground, attempt: 1);
    }

    private void StartResolve(InputMode mode, IntPtr foreground, int attempt)
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        var generation = ++_resolveGeneration;
        _showPending = true;
        _pendingForeground = foreground;

        // Resolving an anchor is a cross-process call that can take a few hundred milliseconds;
        // keep it off the UI thread. The gate serializes calls because the locators expose a
        // single-slot queue and concurrent callers would be rejected.
        _ = Task.Run(async () =>
        {
            await _resolveGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (generation != Volatile.Read(ref _resolveGeneration))
                {
                    return; // a newer trigger superseded this one
                }

                Anchor anchor;
                try
                {
                    anchor = _anchors.Resolve(foreground);
                }
                catch (Exception ex)
                {
                    Log.Warn($"Anchor resolve failed: {ex.GetType().Name}: {ex.Message}");
                    anchor = Anchor.Hidden;
                }

                Post(() => OnResolved(generation, mode, foreground, attempt, anchor));
            }
            finally
            {
                _resolveGate.Release();
            }
        });
    }

    private void OnResolved(int generation, InputMode mode, IntPtr foreground, int attempt, Anchor anchor)
    {
        if (Volatile.Read(ref _disposed) == 1 || generation != _resolveGeneration)
        {
            return;
        }

        if (NativeMethods.GetForegroundWindow() != foreground)
        {
            return; // the focus event for the new window drives the next attempt
        }

        if (NativeMethods.IsIconic(foreground))
        {
            HideBubble();
            return;
        }

        if (anchor.Tier == AnchorTier.Hidden)
        {
            Log.Debug($"Anchor: hidden hwnd=0x{foreground.ToInt64():X} attempt={attempt}");
            if (attempt < MaxAnchorAttempts)
            {
                ScheduleRetry(generation, mode, foreground, attempt + 1);
            }
            else
            {
                HideBubble();
            }

            return;
        }

        Log.Debug(
            $"Anchor: tier={anchor.Tier} rect={anchor.Rect.Left},{anchor.Rect.Top}," +
            $"{anchor.Rect.Right},{anchor.Rect.Bottom} attempt={attempt}");

        _retry.Stop();
        _showPending = false;
        var workArea = WorkArea.Get(anchor.Rect.Left, anchor.Rect.Top);
        _shownFor = foreground;
        _bubble.ShowAt(anchor.Rect, mode, _config.ShortCodes.For(mode), _config, workArea, _keyTimestamp);
        _keyTimestamp = 0;
    }

    private void ScheduleRetry(int generation, InputMode mode, IntPtr foreground, int attempt)
    {
        _retryGeneration = generation;
        _retryMode = mode;
        _retryForeground = foreground;
        _retryAttempt = attempt;
        _retry.Interval = RetryDelay;
        _retry.Start();
    }

    private void OnRetryTick()
    {
        _retry.Stop();

        if (Volatile.Read(ref _disposed) == 1 || _retryGeneration != _resolveGeneration)
        {
            return;
        }

        StartResolve(_retryMode, _retryForeground, _retryAttempt);
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
        CancelPendingShow();
        _shownFor = IntPtr.Zero;
        _bubble.HideNow();
    }

    private void CancelPendingShow()
    {
        _showPending = false;
        _pendingForeground = IntPtr.Zero;
        _retry.Stop();
        Interlocked.Increment(ref _resolveGeneration); // discard an in-flight resolve
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
