"""End-to-end harness: a real Win32 EDIT window that toggles the IME with synthetic Shift.

Place it at a known spot, focus the edit, then toggle 中/英 every 2 seconds for ~16s.
The running easinput app should pop its bubble just below the caret.
"""
import ctypes
import ctypes.wintypes as w
import time
import sys

user32 = ctypes.WinDLL("user32", use_last_error=True)
gdi32 = ctypes.WinDLL("gdi32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

VK_SHIFT = 0x10
KEYEVENTF_KEYUP = 0x0002

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
    ("ShowWindow", [w.HWND, ctypes.c_int], w.BOOL),
    ("UpdateWindow", [w.HWND], w.BOOL),
    ("SetForegroundWindow", [w.HWND], w.BOOL),
    ("SetActiveWindow", [w.HWND], w.HWND),
    ("BringWindowToTop", [w.HWND], w.BOOL),
    ("SetFocus", [w.HWND], w.HWND),
    ("PeekMessageW", [ctypes.POINTER(w.MSG), w.HWND, w.UINT, w.UINT, w.UINT], w.BOOL),
    ("TranslateMessage", [ctypes.POINTER(w.MSG)], w.BOOL),
    ("DispatchMessageW", [ctypes.POINTER(w.MSG)], ctypes.c_ssize_t),
    ("SendMessageW", [w.HWND, w.UINT, w.WPARAM, w.LPARAM], ctypes.c_ssize_t),
    ("DestroyWindow", [w.HWND], w.BOOL),
    ("keybd_event", [w.BYTE, w.BYTE, w.DWORD, ctypes.c_size_t], None),
    ("LoadCursorW", [w.HINSTANCE, w.LPCWSTR], w.HANDLE),
    ("GetForegroundWindow", [], w.HWND),
    ("GetWindowThreadProcessId", [w.HWND, ctypes.POINTER(w.DWORD)], w.DWORD),
    ("GetGUIThreadInfo", [w.DWORD, ctypes.c_void_p], w.BOOL),
    ("ClientToScreen", [w.HWND, ctypes.c_void_p], w.BOOL),
]:
    f = getattr(user32, fn)
    f.argtypes = args
    f.restype = res

gdi32.GetStockObject.argtypes = [ctypes.c_int]
gdi32.GetStockObject.restype = w.HANDLE
kernel32.GetModuleHandleW.argtypes = [w.LPCWSTR]
kernel32.GetModuleHandleW.restype = w.HINSTANCE

EM_SETSEL = 0x00B1
EM_REPLACESEL = 0x00C2

WIN_X, WIN_Y, WIN_W, WIN_H = 600, 380, 560, 150
EDIT_X, EDIT_Y, EDIT_W, EDIT_H = 14, 14, 520, 100


class RECT(ctypes.Structure):
    _fields_ = [("left", w.LONG), ("top", w.LONG), ("right", w.LONG), ("bottom", w.LONG)]


class GUITHREADINFO(ctypes.Structure):
    _fields_ = [
        ("cbSize", w.DWORD), ("flags", w.DWORD),
        ("hwndActive", w.HWND), ("hwndFocus", w.HWND), ("hwndCapture", w.HWND),
        ("hwndMenuOwner", w.HWND), ("hwndMoveSize", w.HWND), ("hwndCaret", w.HWND),
        ("rcCaret", RECT),
    ]


def pump(seconds):
    end = time.time() + seconds
    msg = w.MSG()
    while time.time() < end:
        if user32.PeekMessageW(ctypes.byref(msg), None, 0, 0, 1):
            user32.TranslateMessage(ctypes.byref(msg))
            user32.DispatchMessageW(ctypes.byref(msg))
        else:
            time.sleep(0.005)


def wndproc(hwnd, msg, wp, lp):
    return user32.DefWindowProcW(hwnd, msg, wp, lp)


def caret_rect(edit):
    tid = user32.GetWindowThreadProcessId(edit, None)
    info = GUITHREADINFO()
    info.cbSize = ctypes.sizeof(GUITHREADINFO)
    if not user32.GetGUIThreadInfo(tid, ctypes.byref(info)) or not info.hwndCaret:
        return None
    r = info.rcCaret
    tl = (ctypes.c_int * 2)(r.left, r.top)
    br = (ctypes.c_int * 2)(r.right, r.bottom)
    user32.ClientToScreen(info.hwndCaret, tl)
    user32.ClientToScreen(info.hwndCaret, br)
    return tl[0], tl[1], br[0], br[1]


def main():
    hinst = kernel32.GetModuleHandleW(None)
    cb = WNDPROC(wndproc)
    wc = WNDCLASSW()
    wc.style = 0x0002 | 0x0020
    wc.lpfnWndProc = cb
    wc.hInstance = hinst
    wc.hCursor = user32.LoadCursorW(None, ctypes.cast(32512, w.LPCWSTR))
    wc.hbrBackground = gdi32.GetStockObject(0)
    wc.lpszClassName = "EasInputHarness"
    user32.RegisterClassW(ctypes.byref(wc))

    top = user32.CreateWindowExW(0x00000008, "EasInputHarness",
                                 "easinput harness - do not type here",
                                 0x00CF0000 | 0x10000000,
                                 WIN_X, WIN_Y, WIN_W, WIN_H, None, None, hinst, None)
    edit = user32.CreateWindowExW(0x00000200, "EDIT", "hello world ",
                                  0x40000000 | 0x10000000 | 0x0004 | 0x0040 | 0x0080 | 0x00200000,
                                  EDIT_X, EDIT_Y, EDIT_W, EDIT_H, top, None, hinst, None)
    user32.ShowWindow(top, 5)
    user32.UpdateWindow(top)
    user32.BringWindowToTop(top)
    user32.SetForegroundWindow(top)
    user32.SetActiveWindow(top)
    user32.SetFocus(edit)
    user32.SendMessageW(edit, EM_SETSEL, 12, 12)
    pump(0.8)

    rect = caret_rect(edit)
    print(f"foreground==ours: {user32.GetForegroundWindow() == top}")
    print(f"caret screen rect = {rect}", flush=True)
    if rect:
        cx = (rect[0] + rect[2]) // 2
        print(f"expected bubble: centered x={cx}, top y={rect[3] + 4}")

    for i in range(4 if "--static" not in sys.argv else 0):
        print(f"toggle {i + 1} at t={time.time():.1f}")
        user32.keybd_event(VK_SHIFT, 0, 0, 0)
        pump(0.12)
        user32.keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0)
        pump(1.8)

    if "--static" in sys.argv:
        pump(10)
    user32.DestroyWindow(top)


if __name__ == "__main__":
    main()
