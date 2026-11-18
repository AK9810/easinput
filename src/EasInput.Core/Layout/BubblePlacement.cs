using EasInput.Core.Interop;

namespace EasInput.Core;

public static class BubblePlacement
{
    /// <summary>
    /// Centers the bubble on the anchor, below it when there is room and above it otherwise.
    /// Horizontal overflow is clamped to the work area; vertical overflow is clamped to the top.
    /// </summary>
    public static PixelRect Compute(PixelRect anchor, int width, int height, int gap, PixelRect workArea)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Bubble size must be positive.");
        }

        var left = anchor.Left + ((anchor.Width - width) / 2);
        var top = anchor.Bottom + gap;

        if (top + height > workArea.Bottom)
        {
            top = anchor.Top - gap - height;
        }

        if (top < workArea.Top)
        {
            top = workArea.Top;
        }

        var maxLeft = Math.Max(workArea.Left, workArea.Right - width);
        left = Math.Clamp(left, workArea.Left, maxLeft);

        return PixelRect.FromLtrb(left, top, left + width, top + height);
    }
}

public static class WorkArea
{
    public static PixelRect Get(int x, int y)
    {
        var point = new POINT { X = x, Y = y };
        var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = MONITORINFO.Create();
        if (monitor != IntPtr.Zero && NativeMethods.GetMonitorInfoW(monitor, ref info))
        {
            return PixelRect.FromRect(info.rcWork);
        }

        return PixelRect.FromLtrb(0, 0, 1920, 1080);
    }
}
