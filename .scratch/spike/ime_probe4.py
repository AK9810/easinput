"""Probe 4: can we query IME state with WM_IME_CONTROL instead of ImmGetContext?

Two parts:
  A) own Win32 EDIT window -> toggle 中/英 with synthetic Shift, compare
     ImmGetConversionStatus / ImmGetOpenStatus against
     SendMessage(ImmGetDefaultIMEWnd, WM_IME_CONTROL, IMC_GETOPENSTATUS/IMC_GETCONVERSIONMODE)
  B) cross-process: same WM_IME_CONTROL query against the current foreground window
     (e.g. an Electron app) where ImmGetContext is known to return NULL.

Read-only except for a short-lived test window. Run with:  python -I ime_probe4.py
"""
import ctypes
import ctypes.wintypes as w
import time

user32 = ctypes.WinDLL("user32", use_last_error=True)
imm32 = ctypes.WinDLL("imm32", use_last_error=True)
gdi32 = ctypes.WinDLL("gdi32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

WM_IME_CONTROL = 0x0283
IMC_GETCONVERSIONMODE = 0x0001
IMC_GETSENTENCEMODE = 0x0003
IMC_GETOPENSTATUS = 0x0005
IMC_GETCANDIDATEPOS = 0x0007
SMTO_ABORTIFHUNG = 0x0002
SMTO_BLOCK = 0x0001
VK_SHIFT = 0x10
KEYEVENTF_KEYUP = 0x0002
IME_CMODE_NATIVE = 0x0001

WNDPROC = ctypes.WINFUNCTYPE(ctypes.c_ssize_t, w.HWND, w.UINT, w.WPARAM, w.LPARAM)


class WNDCLASSW(ctypes.Structure):
    _fields_ = [
        ("style", w.UINT), ("lpfnWndProc", WNDPROC), ("cbClsExtra", ctypes.c_int),
        ("cbWndExtra", ctypes.c_int), ("hInstance", w.HINSTANCE), ("hIcon", w.HICON),
        ("hCursor", w.HANDLE), ("hbrBackground", w.HBRUSH), ("lpszMenuName", w.LPCWSTR),
        ("lpszClassName", w.LPCWSTR),
    ]


for fn, args, res in [
    ("DefWindowProcW", [w.HWND, w.UINT, w.WPARAM, w.LPARAM], ctypes.c_ssize_t),
    ("RegisterClassW", [ctypes.POINTER(WNDCLASSW)], w.ATOM),
    ("CreateWindowExW", [w.DWORD, w.LPCWSTR, w.LPCWSTR, w.DWORD, ctypes.c_int, ctypes.c_int,
                         ctypes.c_int, ctypes.c_int, w.HWND, w.HMENU, w.HINSTANCE, w.LPVOID], w.HWND),
    ("GetForegroundWindow", [], w.HWND),
    ("GetWindowThreadProcessId", [w.HWND, ctypes.POINTER(w.DWORD)], w.DWORD),
    ("SetForegroundWindow", [w.HWND], w.BOOL),
    ("SetFocus", [w.HWND], w.HWND),
    ("ShowWindow", [w.HWND, ctypes.c_int], w.BOOL),
    ("UpdateWindow", [w.HWND], w.BOOL),
    ("PeekMessageW", [ctypes.POINTER(w.MSG), w.HWND, w.UINT, w.UINT, w.UINT], w.BOOL),
    ("TranslateMessage", [ctypes.POINTER(w.MSG)], w.BOOL),
    ("DispatchMessageW", [ctypes.POINTER(w.MSG)], ctypes.c_ssize_t),
    ("SendMessageW", [w.HWND, w.UINT, w.WPARAM, w.LPARAM], ctypes.c_ssize_t),
    ("SendMessageTimeoutW", [w.HWND, w.UINT, w.WPARAM, w.LPARAM, w.UINT, w.UINT, ctypes.POINTER(ctypes.c_size_t)], w.LPARAM),
    ("DestroyWindow", [w.HWND], w.BOOL),
    ("keybd_event", [w.BYTE, w.BYTE, w.DWORD, ctypes.c_size_t], None),
    ("LoadCursorW", [w.HINSTANCE, w.LPCWSTR], w.HANDLE),
    ("GetClassNameW", [w.HWND, w.LPWSTR, ctypes.c_int], ctypes.c_int),
]:
    f = getattr(user32, fn)
    f.argtypes = args
    f.restype = res

imm32.ImmGetContext.argtypes = [w.HWND]
imm32.ImmGetContext.restype = w.HANDLE
imm32.ImmReleaseContext.argtypes = [w.HWND, w.HANDLE]
imm32.ImmGetConversionStatus.argtypes = [w.HANDLE, ctypes.POINTER(w.DWORD), ctypes.POINTER(w.DWORD)]
imm32.ImmGetConversionStatus.restype = w.BOOL
imm32.ImmGetOpenStatus.argtypes = [w.HANDLE]
imm32.ImmGetOpenStatus.restype = w.BOOL
imm32.ImmGetDefaultIMEWnd.argtypes = [w.HWND]
imm32.ImmGetDefaultIMEWnd.restype = w.HWND
gdi32.GetStockObject.argtypes = [ctypes.c_int]
gdi32.GetStockObject.restype = w.HANDLE
kernel32.GetModuleHandleW.argtypes = [w.LPCWSTR]
kernel32.GetModuleHandleW.restype = w.HINSTANCE

EM_SETSEL = 0x00B1


def cls_of(h):
    b = ctypes.create_unicode_buffer(128)
    user32.GetClassNameW(h, b, 128)
    return b.value


def ime_window_of(hwnd):
    return imm32.ImmGetDefaultIMEWnd(hwnd)


def via_message(hwnd, code, timeout_ms=400):
    """Ask the default IME window with WM_IME_CONTROL."""
    iw = ime_window_of(hwnd)
    if not iw:
        return ("no-ime-window", None, None)
    out = ctypes.c_size_t(0)
    ok = user32.SendMessageTimeoutW(iw, WM_IME_CONTROL, code, 0,
                                    SMTO_ABORTIFHUNG | SMTO_BLOCK, timeout_ms, ctypes.byref(out))
    return (f"imeWnd=0x{iw:08X}({cls_of(iw)})", bool(ok), out.value)


def via_imm(hwnd):
    himc = imm32.ImmGetContext(hwnd)
    if not himc:
        return None
    try:
        conv, sent = w.DWORD(0), w.DWORD(0)
        ok = imm32.ImmGetConversionStatus(himc, ctypes.byref(conv), ctypes.byref(sent))
        opened = imm32.ImmGetOpenStatus(himc)
        return (bool(ok), conv.value, bool(opened))
    finally:
        imm32.ImmReleaseContext(hwnd, himc)


def snapshot(hwnd, tag):
    imm = via_imm(hwnd)
    om = via_message(hwnd, IMC_GETOPENSTATUS)
    cm = via_message(hwnd, IMC_GETCONVERSIONMODE)
    print(f"  [{tag}]")
    if imm is None:
        print("     ImmGetContext          : NULL")
    else:
        ok, conv, opened = imm
        print(f"     ImmGetConversionStatus : conv=0x{conv:04X} 中文={bool(conv & IME_CMODE_NATIVE)} open={opened} ok={ok}")
    print(f"     IMC_GETOPENSTATUS      : {om}")
    print(f"     IMC_GETCONVERSIONMODE  : {cm}")


state = {"top": None, "edit": None}


def wndproc(hwnd, msg, wp, lp):
    return user32.DefWindowProcW(hwnd, msg, wp, lp)


def pump(sec):
    end = time.time() + sec
    msg = w.MSG()
    while time.time() < end:
        if user32.PeekMessageW(ctypes.byref(msg), None, 0, 0, 1):
            user32.TranslateMessage(ctypes.byref(msg))
            user32.DispatchMessageW(ctypes.byref(msg))
        else:
            time.sleep(0.005)


def part_a():
    print("=== A) own Win32 EDIT window: does WM_IME_CONTROL track state? ===")
    hinst = kernel32.GetModuleHandleW(None)
    cb = WNDPROC(wndproc)
    wc = WNDCLASSW()
    wc.style = 0x0002 | 0x0020
    wc.lpfnWndProc = cb
    wc.hInstance = hinst
    wc.hCursor = user32.LoadCursorW(None, ctypes.cast(32512, w.LPCWSTR))
    wc.hbrBackground = gdi32.GetStockObject(0)
    wc.lpszClassName = "EasInputProbe4"
    user32.RegisterClassW(ctypes.byref(wc))
    top = user32.CreateWindowExW(0x00000008, "EasInputProbe4", "easinput probe 4",
                                 0x00CF0000 | 0x10000000, 360, 360, 760, 160, None, None, hinst, None)
    edit = user32.CreateWindowExW(0x00000200, "EDIT", "hello",
                                 0x40000000 | 0x10000000 | 0x0004 | 0x0040 | 0x0080 | 0x00200000,
                                 12, 12, 720, 90, top, None, hinst, None)
    user32.ShowWindow(top, 5)
    user32.UpdateWindow(top)
    user32.SetForegroundWindow(top)
    user32.SetFocus(edit)
    user32.SendMessageW(edit, EM_SETSEL, 5, 5)
    pump(0.6)
    fg_ok = user32.GetForegroundWindow() == top
    print(f"  foreground==ours: {fg_ok}")
    snapshot(edit, "initial")

    if fg_ok:
        for i in (1, 2):
            user32.keybd_event(VK_SHIFT, 0, 0, 0)
            pump(0.12)
            user32.keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0)
            pump(0.9)
            snapshot(edit, f"after shift #{i}")
    user32.DestroyWindow(top)
    print()


def part_b():
    print("=== B) cross-process: WM_IME_CONTROL on the real foreground window ===")
    fg = user32.GetForegroundWindow()
    print(f"  foreground: hwnd=0x{fg:08X} class={cls_of(fg)!r}")
    snapshot(fg, "foreground")
    print("\n  (If IMC_GETOPENSTATUS returns a value while ImmGetContext is NULL,")
    print("   WM_IME_CONTROL is a viable state source for TSF/Chromium apps.)")


if __name__ == "__main__":
    part_a()
    part_b()
