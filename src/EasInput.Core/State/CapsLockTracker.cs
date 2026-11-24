using EasInput.Core.Interop;

namespace EasInput.Core;

/// <summary>
/// Caps Lock is global per session, but <c>GetKeyState</c> reflects the calling thread's queue and
/// is stale in a background process. Seed once from the async state, then count edges from the
/// low-level keyboard hook (which sees every Caps Lock press on the desktop).
/// </summary>
public sealed class CapsLockTracker
{
    private bool _locked;
    private bool _seeded;

    public bool IsLocked
    {
        get
        {
            Seed();
            return _locked;
        }
    }

    public void Seed()
    {
        if (_seeded)
        {
            return;
        }

        _locked = ReadSystemLocked();
        _seeded = true;
    }

    /// <summary>
    /// The authoritative toggle state, read straight from the system without touching the tracked
    /// value. The two diverge when a Caps Lock edge is missed — the drift only a process restart
    /// cures. Must be called on a thread that pumps messages; other threads see a stale queue.
    /// </summary>
    public static bool ReadSystemLocked()
    {
        // GetKeyState's low bit is the documented toggle state. GetAsyncKeyState's low bit means
        // "pressed since the previous call", which is not a toggle state and produces false
        // positives in a fresh process, so it must not be OR-ed in here.
        return (NativeMethods.GetKeyState(NativeMethods.VkCapital) & 1) != 0;
    }

    public void OnKeyDown(int virtualKey)
    {
        Seed();
        if (virtualKey == NativeMethods.VkCapital)
        {
            _locked = !_locked;
        }
    }
}
