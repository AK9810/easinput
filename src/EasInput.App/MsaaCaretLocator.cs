using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Accessibility;
using EasInput.Core;
using EasInput.Core.Interop;

namespace EasInput.App;

/// <summary>
/// Tier 2: the MSAA caret object. Chromium/Electron expose no Win32 caret, and UIA TextPattern
/// answers a collapsed selection with the whole line box (VS Code) or with nothing at all when
/// the document is empty (ZCode). <c>OBJID_CARET</c> reports the real caret in both cases, and
/// the call is cheap once the app's accessibility provider is up. It is still cross-process and
/// can hang, so it runs on one background MTA thread with a hard timeout. See ADR-0003.
/// </summary>
public sealed class MsaaCaretLocator : ICaretLocator
{
    private static readonly TimeSpan ColdTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan WarmTimeout = TimeSpan.FromMilliseconds(100);

    private readonly BlockingCollection<Request> _queue = new(boundedCapacity: 1);
    private readonly Thread _worker;
    private uint _warmProcessId;

    public MsaaCaretLocator()
    {
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "easinput-msaa-caret",
        };
        _worker.SetApartmentState(ApartmentState.MTA);
        _worker.Start();
    }

    public bool TryLocate(IntPtr foregroundWindow, out PixelRect caret)
    {
        caret = default;
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        var request = new Request(foregroundWindow);
        if (!_queue.TryAdd(request))
        {
            return false; // A query is already queued or running.
        }

        NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var processId);
        var timeout = processId == _warmProcessId ? WarmTimeout : ColdTimeout;
        if (!request.Completion.Task.Wait(timeout))
        {
            return false;
        }

        _warmProcessId = processId;
        if (request.Completion.Task.Result is not { } rect)
        {
            return false;
        }

        caret = rect;
        return true;
    }

    private void WorkerLoop()
    {
        foreach (var request in _queue.GetConsumingEnumerable())
        {
            try
            {
                request.Completion.TrySetResult(Query(request.Foreground));
            }
            catch
            {
                request.Completion.TrySetResult(null);
            }
        }
    }

    private static PixelRect? Query(IntPtr foregroundWindow)
    {
        var iid = NativeMethods.IID_IAccessible;
        if (NativeMethods.AccessibleObjectFromWindow(
                foregroundWindow, NativeMethods.OBJID_CARET, ref iid, out var accessible) != 0 ||
            accessible is null)
        {
            return null;
        }

        try
        {
            if (accessible is not IAccessible caret)
            {
                return null;
            }

            caret.accLocation(out var left, out var top, out var width, out var height, 0);
            if (!CaretGeometry.TryCaretBar(
                    PixelRect.FromLtrb(left, top, left + width, top + height), out var rect))
            {
                return null;
            }

            // The caret object can be stale while another window is in the foreground.
            if (!IsInside(foregroundWindow, rect))
            {
                return null;
            }

            return rect;
        }
        finally
        {
            Marshal.ReleaseComObject(accessible);
        }
    }

    private static bool IsInside(IntPtr foregroundWindow, PixelRect caret)
    {
        if (!NativeMethods.GetWindowRect(foregroundWindow, out var window))
        {
            return false;
        }

        return caret.Intersects(PixelRect.FromRect(window));
    }

    private sealed class Request
    {
        public Request(IntPtr foreground) => Foreground = foreground;

        public IntPtr Foreground { get; }

        public TaskCompletionSource<PixelRect?> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
