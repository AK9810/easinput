using EasInput.Core.Interop;

namespace EasInput.Core;

public interface IImeStateSource
{
    bool TryRead(IntPtr foregroundWindow, out ImeState state);
}

/// <summary>
/// Reads the foreground thread's IME state.
///
/// The primary path is <c>WM_IME_CONTROL</c> sent to the thread's default IME window. It works
/// across processes, and it is the only path that also works for TSF-only apps (Chromium/Electron)
/// where <c>ImmGetContext</c> returns NULL. See ADR-0002.
/// </summary>
public sealed class Win32ImeStateSource : IImeStateSource
{
    private const uint TimeoutMs = 20;

    public bool TryRead(IntPtr foregroundWindow, out ImeState state)
    {
        state = default;
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        var imeWindow = NativeMethods.ImmGetDefaultIMEWnd(foregroundWindow);
        if (imeWindow != IntPtr.Zero &&
            TryQuery(imeWindow, NativeMethods.IMC_GETOPENSTATUS, out var open) &&
            TryQuery(imeWindow, NativeMethods.IMC_GETCONVERSIONMODE, out var conversion))
        {
            // Some IMEs report open status as 2; only zero means closed.
            state = new ImeState(open != 0, (uint)conversion);
            return true;
        }

        var himc = NativeMethods.ImmGetContext(foregroundWindow);
        if (himc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!NativeMethods.ImmGetConversionStatus(himc, out var himcConversion, out _))
            {
                return false;
            }

            var himcOpen = NativeMethods.ImmGetOpenStatus(himc);
            state = new ImeState(himcOpen, himcConversion);
            return true;
        }
        finally
        {
            NativeMethods.ImmReleaseContext(foregroundWindow, himc);
        }
    }

    private static bool TryQuery(IntPtr imeWindow, int command, out ulong result)
    {
        result = 0;
        var sent = NativeMethods.SendMessageTimeoutW(
            imeWindow,
            NativeMethods.WM_IME_CONTROL,
            (IntPtr)command,
            IntPtr.Zero,
            NativeMethods.SMTO_BLOCK | NativeMethods.SMTO_ABORTIFHUNG,
            TimeoutMs,
            out var value);

        if (sent == IntPtr.Zero)
        {
            return false;
        }

        result = (ulong)value;
        return true;
    }
}
