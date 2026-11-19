using System.Collections.Concurrent;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using EasInput.Core;
using EasInput.Core.Interop;

namespace EasInput.App;

/// <summary>
/// Tier 2: the UIA text caret. Chromium/Electron expose no Win32 caret, but their editable
/// controls expose one through <see cref="TextPattern"/>. The call is cross-process and can
/// hang, so it runs on one background MTA thread with a hard timeout. See ADR-0003.
/// </summary>
public sealed class UiaCaretLocator : ICaretLocator
{
    // The first UIA query to a process pays for provider activation (VS Code alone can take
    // >100 ms); later queries are fast. Budget the cold call separately.
    private static readonly TimeSpan ColdTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan WarmTimeout = TimeSpan.FromMilliseconds(100);

    private readonly BlockingCollection<Request> _queue = new(boundedCapacity: 1);
    private readonly Thread _worker;
    private uint _warmProcessId;

    public UiaCaretLocator()
    {
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "easinput-uia-caret",
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
        var focused = AutomationElement.FocusedElement;
        if (focused is null || focused.Current.IsOffscreen)
        {
            return null;
        }

        if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject) ||
            patternObject is not TextPattern textPattern)
        {
            return null;
        }

        var selection = textPattern.GetSelection();
        if (selection.Length == 0)
        {
            return null;
        }

        var range = selection[0];

        // A read-only document (web page, PDF) also exposes a selection, but it has no insertion
        // caret. Without this guard the bubble would anchor to the page text.
        if (range.GetAttributeValue(TextPattern.IsReadOnlyAttribute) is true)
        {
            return null;
        }

        // A selection is not a caret: collapse it to its active end before reading rectangles.
        var caretRange = CollapseToCaret(range);
        var rect = FirstCaretBar(caretRange);
        if (rect is null)
        {
            // A degenerate caret range can return no rectangles, and some providers answer it
            // with the whole line box (the "middle of the line" bug). Expand to the enclosing
            // character and use its box; the geometry check rejects a line-wide box (ADR-0003).
            var expanded = caretRange.Clone();
            expanded.ExpandToEnclosingUnit(TextUnit.Character);
            rect = FirstCharacterBox(expanded);
        }

        if (rect is not { } caret || !IsInside(foregroundWindow, caret))
        {
            return null;
        }

        return caret;
    }

    private static TextPatternRange CollapseToCaret(TextPatternRange range)
    {
        try
        {
            var collapsed = range.Clone();
            collapsed.MoveEndpointByRange(
                TextPatternRangeEndpoint.Start, collapsed, TextPatternRangeEndpoint.End);
            return collapsed;
        }
        catch
        {
            return range;
        }
    }

    private static PixelRect? FirstCaretBar(TextPatternRange range)
    {
        foreach (var bounds in range.GetBoundingRectangles())
        {
            if (CaretGeometry.TryCaretBar(ToPixelRect(bounds), out var caret))
            {
                return caret;
            }
        }

        return null;
    }

    private static PixelRect? FirstCharacterBox(TextPatternRange range)
    {
        foreach (var bounds in range.GetBoundingRectangles())
        {
            if (CaretGeometry.TryCharacterBox(ToPixelRect(bounds), out var box))
            {
                return box;
            }
        }

        return null;
    }

    private static PixelRect ToPixelRect(System.Windows.Rect bounds)
    {
        var left = (int)Math.Round(bounds.Left);
        var top = (int)Math.Round(bounds.Top);
        var right = (int)Math.Round(bounds.Right);
        var bottom = (int)Math.Round(bounds.Bottom);
        return PixelRect.FromLtrb(left, top, right, bottom);
    }

    private static bool IsInside(IntPtr foregroundWindow, PixelRect caret)
    {
        if (!NativeMethods.GetWindowRect(foregroundWindow, out var window))
        {
            return false;
        }

        var rect = PixelRect.FromRect(window);
        return caret.Intersects(rect);
    }

    private sealed class Request
    {
        public Request(IntPtr foreground) => Foreground = foreground;

        public IntPtr Foreground { get; }

        public TaskCompletionSource<PixelRect?> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
