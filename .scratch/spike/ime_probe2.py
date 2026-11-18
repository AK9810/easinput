"""Probe 2b: real Win32 EDIT window; verify (a) caret rect, (b) 中/英 flag flips.

A small topmost window appears for ~10s. It only sends synthetic Shift to ITSELF,
and only when it is verified to be the foreground window. Nothing is written.

Run with:  python -I ime_probe2.py
"""
import ctypes
import ctypes.wintypes as w
import time

user32 = ctypes.WinDLL("user32", use_last_error=True)
imm32 = ctypes.WinDLL("imm32", use_last_error=True)
gdi32 = ctypes.WinDLL("gdi32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

WS_OVERLAPPEDWINDOW = 0x00CF0000
WS_VISIBLE = 0x10000000
WS_CHILD = 0x40000000
WS_VSCROLL = 0x00200000
WS_EX_CLIENTEDGE = 0x00000200
WS_EX_TOPMOST = 0x00000008
ES_MULTILINE = 0x0004
ES_AUTOVSCROLL = 0x0040
ES_AUTOHSCROLL = 0x0080
SW_SHOW = 5
VK_CAPITAL = 0x14
VK_SHIFT = 0x10
KEYEVENTF_KEYUP = 0x0002
WM_TIMER = 0x0113
WM_DESTROY = 0x0002
WM_SETFOCUS = 0x0007
WM_IME_SETCONTEXT = 0x0281
WM_IME_NOTIFY = 0x0282
WM_IME_STARTCOMPOSITION = 0x010D
WM_IME_ENDCOMPOSITION = 0x010E

IME_CMODE_NATIVE = 0x0001
IME_CMODE_FULLSHAPE = 0x0008
IME_CMODE_ROMAN = 0x0010
IME_CMODE_SYMBOL = 0x0400

WNDPROC = ctypes.WINFUNCTYPE(ctypes.c_ssize_t, w.HWND, w.UINT, w.WPARAM, w.LPARAM)


class RECT(ctypes.Structure):
    _fields_ = [("left", w.LONG), ("top", w.LONG), ("right", w.LONG), ("bottom", w.LONG)]


class GUITHREADINFO(ctypes.Structure):
    _fields_ = [
        ("cbSize", w.DWORD), ("flags", w.DWORD),
        ("hwndActive", w.HWND), ("hwndFocus", w.HWND), ("hwndCapture", w.HWND),
        ("hwndMenuOwner", w.HWND), ("hwndMoveSize", w.HWND), ("hwndCaret", w.HWND),
        ("rcCaret", RECT),
    ]


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
    ("GetGUIThreadInfo", [w.DWORD, ctypes.POINTER(GUITHREADINFO)], w.BOOL),
    ("GetWindowThreadProcessId", [w.HWND, ctypes.POINTER(w.DWORD)], w.DWORD),
    ("SetForegroundWindow", [w.HWND], w.BOOL),
    ("GetForegroundWindow", [], w.HWND),
    ("SetFocus", [w.HWND], w.HWND),
    ("SetActiveWindow", [w.HWND], w.HWND),
    ("SendMessageW", [w.HWND, w.UINT, w.WPARAM, w.LPARAM], ctypes.c_ssize_t),
    ("SetTimer", [w.HWND, ctypes.c_size_t, w.UINT, w.HANDLE], ctypes.c_size_t),
    ("PeekMessageW", [ctypes.POINTER(w.MSG), w.HWND, w.UINT, w.UINT, w.UINT], w.BOOL),
    ("TranslateMessage", [ctypes.POINTER(w.MSG)], w.BOOL),
    ("DispatchMessageW", [ctypes.POINTER(w.MSG)], ctypes.c_ssize_t),
    ("GetKeyState", [ctypes.c_int], ctypes.c_short),
    ("GetKeyboardLayout", [w.DWORD], w.HANDLE),
    ("DestroyWindow", [w.HWND], w.BOOL),
    ("ShowWindow", [w.HWND, ctypes.c_int], w.BOOL),
    ("UpdateWindow", [w.HWND], w.BOOL),
    ("keybd_event", [w.BYTE, w.BYTE, w.DWORD, ctypes.c_size_t], None),
    ("LoadCursorW", [w.HINSTANCE, w.LPCWSTR], w.HANDLE),
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
gdi32.GetStockObject.argtypes = [ctypes.c_int]
gdi32.GetStockObject.restype = w.HANDLE
kernel32.GetModuleHandleW.argtypes = [w.LPCWSTR]
kernel32.GetModuleHandleW.restype = w.HINSTANCE

EM_SETSEL = 0x00B1
EM_REPLACESEL = 0x00C2

state = {"top": None, "edit": None, "t0": 0.0, "last": None, "rows": [], "seen_fg": False}


def ime_state():
    edit = state["edit"]
    tid = user32.GetWindowThreadProcessId(edit, None)
    himc = imm32.ImmGetContext(edit)
    if not himc:
        return ("nohimc",)
    try:
        conv, sent = w.DWORD(0), w.DWORD(0)
        ok = imm32.ImmGetConversionStatus(himc, ctypes.byref(conv), ctypes.byref(sent))
        opened = imm32.ImmGetOpenStatus(himc)
    finally:
        imm32.ImmReleaseContext(edit, himc)
    gti = GUITHREADINFO()
    gti.cbSize = ctypes.sizeof(GUITHREADINFO)
    gok = user32.GetGUIThreadInfo(tid, ctypes.byref(gti))
    r = gti.rcCaret
    return (bool(ok), conv.value, bool(opened), bool(gok), gti.hwndCaret,
            r.left, r.top, r.right, r.bottom, bool(user32.GetKeyState(VK_CAPITAL) & 1))


def fmt(s):
    if s[0] == "nohimc":
        return "ImmGetContext(NULL)"
    ok, conv, opened, gok, hcaret, l, t, rr, b, caps = s
    return (f"conv=0x{conv:04X} 中文={bool(conv & IME_CMODE_NATIVE)} 全角={bool(conv & IME_CMODE_FULLSHAPE)} "
            f"open={opened} caps={caps} | caret=0x{(hcaret or 0):08X} rc=({l},{t},{rr},{b}) "
            f"size={rr-l}x{b-t} gti_ok={gok}")


def sample(tag):
    s = ime_state()
    if s != state["last"]:
        print(f"[{time.time()-state['t0']:5.1f}s {tag:22s}] {fmt(s)}")
        state["last"] = s
        state["rows"].append((tag, s))


def wndproc(hwnd, msg, wp, lp):
    if msg == WM_TIMER:
        if user32.GetForegroundWindow() == state["top"]:
            if not state["seen_fg"]:
                state["seen_fg"] = True
                print(f"[{time.time()-state['t0']:5.1f}s] window IS foreground -> caret should exist")
        sample("timer")
    elif msg == WM_IME_NOTIFY:
        print(f"[{time.time()-state['t0']:5.1f}s] WM_IME_NOTIFY wParam=0x{wp:02X}")
        sample(f"ime-notify-{wp:02X}")
    elif msg == WM_IME_STARTCOMPOSITION:
        print(f"[{time.time()-state['t0']:5.1f}s] WM_IME_STARTCOMPOSITION")
    elif msg == WM_IME_ENDCOMPOSITION:
        print(f"[{time.time()-state['t0']:5.1f}s] WM_IME_ENDCOMPOSITION")
    elif msg == WM_DESTROY:
        return 0
    return user32.DefWindowProcW(hwnd, msg, wp, lp)


def pump(seconds):
    end = time.time() + seconds
    msg = w.MSG()
    while time.time() < end:
        if user32.PeekMessageW(ctypes.byref(msg), None, 0, 0, 1):
            if msg.message == 0x0012:
                return
            user32.TranslateMessage(ctypes.byref(msg))
            user32.DispatchMessageW(ctypes.byref(msg))
        else:
            time.sleep(0.005)


def main():
    hinst = kernel32.GetModuleHandleW(None)
    cb = WNDPROC(wndproc)
    wc = WNDCLASSW()
    wc.style = 0x0002 | 0x0020
    wc.lpfnWndProc = cb
    wc.hInstance = hinst
    wc.hCursor = user32.LoadCursorW(None, ctypes.cast(32512, w.LPCWSTR))
    wc.hbrBackground = gdi32.GetStockObject(0)
    wc.lpszClassName = "EasInputProbeEdit2"
    if not user32.RegisterClassW(ctypes.byref(wc)):
        print("RegisterClass failed", ctypes.get_last_error())
        return

    top = user32.CreateWindowExW(
        WS_EX_TOPMOST, "EasInputProbeEdit2",
        "easinput probe 2 — do not type here",
        WS_OVERLAPPEDWINDOW | WS_VISIBLE, 320, 320, 760, 170, None, None, hinst, None)
    edit = user32.CreateWindowExW(
        WS_EX_CLIENTEDGE, "EDIT", "hello ",
        WS_CHILD | WS_VISIBLE | ES_MULTILINE | ES_AUTOVSCROLL | ES_AUTOHSCROLL | WS_VSCROLL,
        12, 12, 720, 100, top, None, hinst, None)
    state["top"], state["edit"] = top, edit
    print(f"top=0x{top:08X} edit=0x{edit:08X}")

    user32.ShowWindow(top, SW_SHOW)
    user32.UpdateWindow(top)
    user32.SetForegroundWindow(top)
    user32.SetActiveWindow(top)
    user32.SetFocus(edit)
    user32.SendMessageW(edit, EM_SETSEL, 6, 6)
    state["t0"] = time.time()
    pump(0.8)

    print(f"foreground==top ? {user32.GetForegroundWindow() == top}")
    sample("after-focus")

    # Synthetic Shift toggle, ONLY if our window really is foreground.
    if user32.GetForegroundWindow() == top:
        print("\n--- sending Shift down/up to toggle 中/英 ---")
        user32.keybd_event(VK_SHIFT, 0, 0, 0)
        pump(0.15)
        user32.keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0)
        pump(1.2)
        sample("after-shift-1")
        print("--- toggling back ---")
        user32.keybd_event(VK_SHIFT, 0, 0, 0)
        pump(0.15)
        user32.keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0)
        pump(1.2)
        sample("after-shift-2")
    else:
        print("\n!! our window never became foreground; skipping synthetic keys")

    user32.SetTimer(top, 1, 250, None)
    pump(1.5)
    print("\n--- summary of distinct states ---")
    for tag, s in state["rows"]:
        print(f"  {tag:22s} {fmt(s)}")
    user32.DestroyWindow(top)


if __name__ == "__main__":
    main()
