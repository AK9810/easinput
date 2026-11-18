"""Read-only probe: foreground window, caret rect, IME conversion state, keyboard layout.

Run with:  python -I ime_probe.py
No writes, no hooks, no injection. Just queries.
"""
import ctypes
import ctypes.wintypes as w
import sys

user32 = ctypes.WinDLL("user32", use_last_error=True)
imm32 = ctypes.WinDLL("imm32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

# --- IME conversion-mode flags (wingdi.h / imm.h) ---
IME_CMODE_ALPHANUMERIC = 0x0000
IME_CMODE_NATIVE = 0x0001
IME_CMODE_KATAKANA = 0x0002
IME_CMODE_LANGUAGE = 0x0003
IME_CMODE_FULLSHAPE = 0x0008
IME_CMODE_ROMAN = 0x0010
IME_CMODE_CHARCODE = 0x0020
IME_CMODE_HANJACONVERT = 0x0040
IME_CMODE_SOFTKBD = 0x0080
IME_CMODE_NOCONVERSION = 0x0100
IME_CMODE_EUDC = 0x0200
IME_CMODE_SYMBOL = 0x0400
IME_CMODE_FIXED = 0x0800


class RECT(ctypes.Structure):
    _fields_ = [("left", w.LONG), ("top", w.LONG), ("right", w.LONG), ("bottom", w.LONG)]


class GUITHREADINFO(ctypes.Structure):
    _fields_ = [
        ("cbSize", w.DWORD),
        ("flags", w.DWORD),
        ("hwndActive", w.HWND),
        ("hwndFocus", w.HWND),
        ("hwndCapture", w.HWND),
        ("hwndMenuOwner", w.HWND),
        ("hwndMoveSize", w.HWND),
        ("hwndCaret", w.HWND),
        ("rcCaret", RECT),
    ]


user32.GetGUIThreadInfo.argtypes = [w.DWORD, ctypes.POINTER(GUITHREADINFO)]
user32.GetGUIThreadInfo.restype = w.BOOL
user32.GetForegroundWindow.restype = w.HWND
user32.GetWindowThreadProcessId.argtypes = [w.HWND, ctypes.POINTER(w.DWORD)]
user32.GetWindowThreadProcessId.restype = w.DWORD
user32.GetClassNameW.argtypes = [w.HWND, w.LPWSTR, ctypes.c_int]
user32.GetWindowTextW.argtypes = [w.HWND, w.LPWSTR, ctypes.c_int]
user32.GetKeyboardLayout.argtypes = [w.DWORD]
user32.GetKeyboardLayout.restype = w.HANDLE
user32.GetKeyState.argtypes = [ctypes.c_int]
user32.GetKeyState.restype = ctypes.c_short
user32.GetAsyncKeyState.argtypes = [ctypes.c_int]
user32.GetAsyncKeyState.restype = ctypes.c_short

imm32.ImmGetContext.argtypes = [w.HWND]
imm32.ImmGetContext.restype = w.HANDLE
imm32.ImmReleaseContext.argtypes = [w.HWND, w.HANDLE]
imm32.ImmGetConversionStatus.argtypes = [w.HANDLE, ctypes.POINTER(w.DWORD), ctypes.POINTER(w.DWORD)]
imm32.ImmGetConversionStatus.restype = w.BOOL
imm32.ImmGetOpenStatus.argtypes = [w.HANDLE]
imm32.ImmGetOpenStatus.restype = w.BOOL
imm32.ImmGetDescriptionW.argtypes = [w.HANDLE, w.LPWSTR, w.UINT]
imm32.ImmGetDescriptionW.restype = w.UINT
imm32.ImmGetIMEFileNameW.argtypes = [w.HANDLE, w.LPWSTR, w.UINT]
imm32.ImmGetIMEFileNameW.restype = w.UINT
imm32.ImmIsIME.argtypes = [w.HANDLE]
imm32.ImmIsIME.restype = w.BOOL


def win_info(hwnd):
    cls = ctypes.create_unicode_buffer(256)
    txt = ctypes.create_unicode_buffer(256)
    user32.GetClassNameW(hwnd, cls, 256)
    user32.GetWindowTextW(hwnd, txt, 256)
    pid = w.DWORD(0)
    tid = user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    return tid, pid.value, cls.value, txt.value


def layout_name(hkl):
    buf = ctypes.create_unicode_buffer(256)
    n = imm32.ImmGetDescriptionW(hkl, buf, 256)
    desc = buf.value if n else ""
    buf2 = ctypes.create_unicode_buffer(260)
    n2 = imm32.ImmGetIMEFileNameW(hkl, buf2, 260)
    return desc, (buf2.value if n2 else "")


def report(hwnd, label):
    print(f"\n=== {label} ===")
    if not hwnd:
        print("  (no hwnd)")
        return
    tid, pid, cls, txt = win_info(hwnd)
    print(f"  hwnd=0x{hwnd:08X} tid={tid} pid={pid} class={cls!r} title={txt!r}")

    gti = GUITHREADINFO()
    gti.cbSize = ctypes.sizeof(GUITHREADINFO)
    ok = user32.GetGUIThreadInfo(tid, ctypes.byref(gti))
    if ok:
        r = gti.rcCaret
        print(f"  GetGUIThreadInfo OK: hwndCaret=0x{(gti.hwndCaret or 0):08X} "
              f"rcCaret=({r.left},{r.top},{r.right},{r.bottom}) "
              f"size={r.right - r.left}x{r.bottom - r.top}")
    else:
        print(f"  GetGUIThreadInfo FAILED err={ctypes.get_last_error()}")

    hkl = user32.GetKeyboardLayout(tid)
    desc, imefile = layout_name(hkl)
    print(f"  hkl=0x{(hkl or 0):08X} desc={desc!r} imefile={imefile!r} "
          f"ImmIsIME={bool(imm32.ImmIsIME(hkl))}")

    himc = imm32.ImmGetContext(hwnd)
    if not himc:
        print(f"  ImmGetContext -> NULL (err={ctypes.get_last_error()})  [no IME context for this window]")
        return
    try:
        conv = w.DWORD(0)
        sent = w.DWORD(0)
        ok = imm32.ImmGetConversionStatus(himc, ctypes.byref(conv), ctypes.byref(sent))
        opened = imm32.ImmGetOpenStatus(himc)
        print(f"  ImmGetConversionStatus ok={bool(ok)} conv=0x{conv.value:04X} sent=0x{sent.value:04X} "
              f"open={bool(opened)}")
        if ok:
            c = conv.value
            print(f"    NATIVE(中文)={bool(c & IME_CMODE_NATIVE)} "
                  f"ALPHANUMERIC={bool(c & IME_CMODE_ALPHANUMERIC)} "
                  f"FULLSHAPE(全角)={bool(c & IME_CMODE_FULLSHAPE)} "
                  f"ROMAN={bool(c & IME_CMODE_ROMAN)} "
                  f"SYMBOL={bool(c & IME_CMODE_SYMBOL)} "
                  f"KATAKANA={bool(c & IME_CMODE_KATAKANA)}")
    finally:
        imm32.ImmReleaseContext(hwnd, himc)


def main():
    print(f"python={sys.version.split()[0]}  arch={ctypes.sizeof(ctypes.c_void_p) * 8}-bit")
    caps = user32.GetKeyState(0x14)
    caps_async = user32.GetAsyncKeyState(0x14)
    print(f"CapsLock: GetKeyState=0x{caps & 0xFFFF:04X} toggled={bool(caps & 1)}  "
          f"GetAsyncKeyState toggled={bool(caps_async & 1)}")

    fg = user32.GetForegroundWindow()
    report(fg, "FOREGROUND")

    # Also probe a few well-known windows that likely have a real caret / text surface.
    targets = []

    @ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    def enum_cb(hwnd, _):
        if not user32.IsWindowVisible(hwnd):
            return True
        tid, pid, cls, txt = win_info(hwnd)
        if cls in ("Notepad", "Edit", "RichEditD2DPT", "Chrome_WidgetWin_1",
                   "CASCADIA_HOSTING_WINDOW_CLASS", "CabinetWClass", "ApplicationFrameWindow"):
            targets.append(hwnd)
            return False  # one is enough per class
        return True

    user32.EnumWindows(enum_cb, 0)
    for h in targets[:4]:
        report(h, "CANDIDATE")

    print("\nNote: rcCaret from GetGUIThreadInfo is in SCREEN coordinates per MSDN "
          "(it is derived from GetCaretPos + ClientToScreen).")


if __name__ == "__main__":
    main()
