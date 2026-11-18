using EasInput.Core.Interop;

namespace EasInput.Core;

/// <summary>
/// DIP → physical-pixel conversion for the monitor that contains the anchor. The bubble is
/// positioned in physical screen pixels, so its size must use the DPI of the monitor under the
/// anchor rather than the DPI of the monitor the window currently occupies (the two differ when
/// the anchor is on a different monitor than the last show).
/// </summary>
public static class DpiScale
{
    public static double FromDpi(uint dpi) => dpi == 0 ? 1.0 : dpi / 96.0;

    public static int Round(double dip, double scale) =>
        (int)Math.Round(dip * scale, MidpointRounding.AwayFromZero);

    public static int Ceil(double dip, double scale) => (int)Math.Ceiling(dip * scale);

    /// <summary>Effective DPI scale of the monitor nearest to a physical screen point.</summary>
    public static double ForPoint(int x, int y)
    {
        var monitor = NativeMethods.MonitorFromPoint(
            new POINT { X = x, Y = y },
            NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return 1.0;
        }

        var hr = NativeMethods.GetDpiForMonitor(
            monitor,
            NativeMethods.MDT_EFFECTIVE_DPI,
            out var dpiX,
            out _);
        return hr == 0 ? FromDpi(dpiX) : 1.0;
    }
}
