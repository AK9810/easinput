using System.IO;
using System.Text;
using EasInput.Core;
using EasInput.Core.Interop;

namespace EasInput.App;

/// <summary>
/// <c>easinput --probe</c>: prints what the app sees for the current foreground window.
/// This is the field-debugging tool for "it does not show up in app X".
/// </summary>
internal static class ProbeMode
{
    public static void Run()
    {
        NativeMethods.AttachConsole(-1);
        var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(output);

        var report = new StringBuilder();
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            report.AppendLine("foreground: (none)");
            Console.Write(report.ToString());
            return;
        }

        var threadId = NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        var hkl = NativeMethods.GetKeyboardLayout(threadId);

        report.AppendLine($"foreground : hwnd=0x{foreground.ToInt64():X} pid={processId} tid={threadId} " +
                          $"class='{ClassName(foreground)}' title='{WindowText(foreground)}'");
        report.AppendLine($"layout     : hkl=0x{hkl.ToInt64():X} lang=0x{(hkl.ToInt64() & 0xFFFF):X4}");

        var caps = new CapsLockTracker();
        report.AppendLine($"caps lock  : {(caps.IsLocked ? "on" : "off")}");

        var stateSource = new Win32ImeStateSource();
        if (stateSource.TryRead(foreground, out var state))
        {
            var mode = InputModeResolver.Resolve(state.IsOpen, state.ConversionMode, caps.IsLocked);
            report.AppendLine($"ime        : open={(state.IsOpen ? 1 : 0)} conv=0x{state.ConversionMode:X4} " +
                              $"chinese={state.IsChinese} -> mode={mode}");
        }
        else
        {
            report.AppendLine("ime        : unreadable (no IME window and no HIMC)");
        }

        var anchors = new AnchorResolver(new Win32CaretLocator(), new UiaCaretLocator());
        var anchor = anchors.Resolve(foreground);
        report.AppendLine($"anchor     : tier={anchor.Tier} rect={Describe(anchor.Rect)}");

        if (anchor.Tier != AnchorTier.Hidden)
        {
            var work = WorkArea.Get(anchor.Rect.Left, anchor.Rect.Top);
            report.AppendLine($"work area  : {Describe(work)}");
            foreach (var mode in new[] { InputMode.Chinese, InputMode.LatinLower, InputMode.LatinUpper })
            {
                var placed = BubblePlacement.Compute(anchor.Rect, width: 37, height: 29, gap: 4, workArea: work);
                report.AppendLine($"bubble     : mode={mode} at {Describe(placed)}");
            }
        }

        Console.Write(report.ToString());
        NativeMethods.FreeConsole();
    }

    private static string Describe(PixelRect rect) =>
        rect.IsEmpty ? "(empty)" : $"({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom}) {rect.Width}x{rect.Height}";

    private static string ClassName(IntPtr hwnd)
    {
        var buffer = new char[256];
        var length = NativeMethods.GetClassNameW(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    private static string WindowText(IntPtr hwnd)
    {
        var buffer = new char[256];
        var length = NativeMethods.GetWindowTextW(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }
}
