"""Probe 3: for the current foreground window, try every plausible IMM32 entry point.

Answers: in a Chromium/Electron app (no IMM context on the top-level HWND), is there
ANY hwnd in the window chain for which ImmGetContext returns a usable context, and
does it report the Chinese/English state?

Read-only. Run with:  python -I ime_probe3.py
"""
import ctypes
import ctypes.wintypes as w

user32 = ctypes.WinDLL("user32", use_last_error=True)
imm32 = ctypes.WinDLL("imm32", use_last_error=True)

IME_CMODE_NATIVE = 0x0001
IME_CMODE_FULLSHAPE = 0x0008
IME_CMODE_ROMAN = 0x0010

for fn, args, res in [
    ("GetForegroundWindow", [], w.HWND),
    ("GetWindowThreadProcessId", [w.HWND, ctypes.POINTER(w.DWORD)], w.DWORD),
    ("GetGUIThreadInfo", [w.DWORD, ctypes.c_void_p], w.BOOL),
    ("GetClassNameW", [w.HWND, w.LPWSTR, ctypes.c_int], ctypes.c_int),
    ("GetWindowTextW", [w.HWND, w.LPWSTR, ctypes.c_int], ctypes.c_int),
    ("GetParent", [w.HWND], w.HWND),
    ("GetAncestor", [w.HWND, w.UINT], w.HWND),
    ("EnumChildWindows", [w.HWND, ctypes.c_void_p, w.LPARAM], w.BOOL),
    ("GetKeyboardLayout", [w.DWORD], w.HANDLE),
    ("GetKeyState", [ctypes.c_int], ctypes.c_short),
    ("IsWindowVisible", [w.HWND], w.BOOL),
    ("GetWindowLongPtrW", [w.HWND, ctypes.c_int], ctypes.c_ssize_t),
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


class RECT(ctypes.Structure):
    _fields_ = [("left", w.LONG), ("top", w.LONG), ("right", w.LONG), ("bottom", w.LONG)]


class GUITHREADINFO(ctypes.Structure):
    _fields_ = [
        ("cbSize", w.DWORD), ("flags", w.DWORD),
        ("hwndActive", w.HWND), ("hwndFocus", w.HWND), ("hwndCapture", w.HWND),
        ("hwndMenuOwner", w.HWND), ("hwndMoveSize", w.HWND), ("hwndCaret", w.HWND),
        ("rcCaret", RECT),
    ]


def info(h):
    cls = ctypes.create_unicode_buffer(200)
    txt = ctypes.create_unicode_buffer(200)
    user32.GetClassNameW(h, cls, 200)
    user32.GetWindowTextW(h, txt, 200)
    return cls.value, txt.value


def probe(h, label):
    if not h:
        print(f"  {label}: (null)")
        return
    cls, txt = info(h)
    himc = imm32.ImmGetContext(h)
    if not himc:
        print(f"  {label}: hwnd=0x{h:08X} class={cls!r} -> ImmGetContext NULL")
        return
    try:
        conv, sent = w.DWORD(0), w.DWORD(0)
        ok = imm32.ImmGetConversionStatus(himc, ctypes.byref(conv), ctypes.byref(sent))
        opened = imm32.ImmGetOpenStatus(himc)
        print(f"  {label}: hwnd=0x{h:08X} class={cls!r} -> conv=0x{conv.value:04X} "
              f"中文={bool(conv.value & IME_CMODE_NATIVE)} open={bool(opened)} ok={bool(ok)}")
    finally:
        imm32.ImmReleaseContext(h, himc)


def main():
    fg = user32.GetForegroundWindow()
    tid = user32.GetWindowThreadProcessId(fg, None)
    cls, txt = info(fg)
    print(f"foreground hwnd=0x{fg:08X} tid={tid} class={cls!r} title={txt!r}")
    print(f"GetKeyboardLayout(tid)=0x{(user32.GetKeyboardLayout(tid) or 0):08X}")
    print(f"CapsLock toggled={bool(user32.GetKeyState(0x14) & 1)}")

    gti = GUITHREADINFO()
    gti.cbSize = ctypes.sizeof(GUITHREADINFO)
    ok = user32.GetGUIThreadInfo(tid, ctypes.byref(gti))
    print(f"\nGetGUIThreadInfo ok={bool(ok)} flags=0x{gti.flags:X} "
          f"hwndActive=0x{(gti.hwndActive or 0):08X} hwndFocus=0x{(gti.hwndFocus or 0):08X} "
          f"hwndCaret=0x{(gti.hwndCaret or 0):08X}")

    print("\n-- IMM32 entry points --")
    probe(fg, "foreground")
    probe(gti.hwndFocus, "hwndFocus")
    probe(gti.hwndActive, "hwndActive")
    probe(gti.hwndCaret, "hwndCaret")
    probe(imm32.ImmGetDefaultIMEWnd(fg), "defaultIMEWnd")

    print("\n-- children of foreground (first 25) --")
    kids = []

    @ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    def cb(h, _):
        kids.append(h)
        return True if len(kids) < 25 else False

    user32.EnumChildWindows(fg, cb, 0)
    for h in kids:
        cls, _ = info(h)
        if cls in ("Chrome_RenderWidgetHostHWND", "Intermediate D3D Window",
                   "Chrome_WidgetWin_0", "Chrome_WidgetWin_1", "Edit", "RichEditD2DPT"):
            probe(h, f"child[{cls}]")

    print("\n-- conclusion hints --")
    print("If every probe above is NULL, Chromium-family apps give us no IMM32 state")
    print("and a different state source is required for them.")


if __name__ == "__main__":
    main()
