using EasInput.Core.Interop;

namespace EasInput.Core;

public interface ICaretLocator
{
    bool TryLocate(IntPtr foregroundWindow, out PixelRect caret);
}

/// <summary>
/// Tier 1: the classic Win32 caret. <c>rcCaret</c> is in the client coordinates of
/// <c>hwndCaret</c>, so it must be mapped to the screen. A rect that falls outside its own window
/// is treated as stale (some apps never clear it).
/// </summary>
public sealed class Win32CaretLocator : ICaretLocator
{
    public bool TryLocate(IntPtr foregroundWindow, out PixelRect caret)
    {
        caret = default;
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        // GetGUIThreadInfo(0) queries the calling thread, which never owns the foreground caret.
        var threadId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);
        var info = GUITHREADINFO.Create();
        if (threadId == 0 ||
            !NativeMethods.GetGUIThreadInfo(threadId, ref info) ||
            info.hwndCaret == IntPtr.Zero)
        {
            return false;
        }

        var local = info.rcCaret;
        if (local.Right <= local.Left || local.Bottom <= local.Top)
        {
            return false;
        }

        var topLeft = new POINT { X = local.Left, Y = local.Top };
        var bottomRight = new POINT { X = local.Right, Y = local.Bottom };
        if (!NativeMethods.ClientToScreen(info.hwndCaret, ref topLeft) ||
            !NativeMethods.ClientToScreen(info.hwndCaret, ref bottomRight))
        {
            return false;
        }

        var rect = PixelRect.FromLtrb(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        if (!NativeMethods.GetWindowRect(info.hwndCaret, out var window))
        {
            return false;
        }

        var windowRect = PixelRect.FromRect(window);
        if (!rect.Intersects(windowRect))
        {
            return false;
        }

        caret = rect;
        return true;
    }
}

/// <summary>
/// Tiers in order: Win32 caret → MSAA caret → UIA text caret → hidden. The bubble only appears
/// at a real caret; without one it stays hidden. See ADR-0003.
/// </summary>
public sealed class AnchorResolver
{
    private readonly ICaretLocator _caret;
    private readonly ICaretLocator _msaa;
    private readonly ICaretLocator _uia;

    public AnchorResolver(ICaretLocator caret, ICaretLocator msaa, ICaretLocator uia)
    {
        _caret = caret;
        _msaa = msaa;
        _uia = uia;
    }

    public Anchor Resolve(IntPtr foregroundWindow)
    {
        if (_caret.TryLocate(foregroundWindow, out var caret))
        {
            return new Anchor(AnchorTier.Caret, caret);
        }

        if (_msaa.TryLocate(foregroundWindow, out var msaaCaret))
        {
            return new Anchor(AnchorTier.Msaa, msaaCaret);
        }

        if (_uia.TryLocate(foregroundWindow, out var uiaCaret))
        {
            return new Anchor(AnchorTier.Uia, uiaCaret);
        }

        return Anchor.Hidden;
    }
}
