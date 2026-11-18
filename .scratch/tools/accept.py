#!/usr/bin/env python3
"""Acceptance-test helpers for easinput: window focus, synthetic keys, screenshots, pill bbox.

All screen coordinates are physical pixels (the process opts into PerMonitorV2 DPI awareness).
Set EASINPUT_DOTNET / EASINPUT_DLL / EASINPUT_LOG to override the defaults.
The shot/pill commands need pillow and numpy.
"""
import argparse
import ctypes
import ctypes.wintypes as wt
import json
import os
import re
import shutil
import subprocess
import sys
import time
from collections import deque
from pathlib import Path

for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

user32 = ctypes.WinDLL("user32", use_last_error=True)
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

try:
    user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))  # PER_MONITOR_AWARE_V2
except Exception:
    pass

ROOT = Path(__file__).resolve().parents[2]
DOTNET = (os.environ.get("EASINPUT_DOTNET")
          or shutil.which("dotnet")
          or r"C:\Users\QIZIP\AppData\Local\Microsoft\dotnet\dotnet.exe")
DLL = (os.environ.get("EASINPUT_DLL")
       or str(ROOT / "src" / "EasInput.App" / "bin" / "Debug" / "net8.0-windows" / "easinput.dll"))
LOG = (os.environ.get("EASINPUT_LOG")
       or str(Path(os.environ.get("APPDATA", str(Path.home()))) / "easinput" / "easinput.log"))

VK_SHIFT = 0x10
VK_LSHIFT = 0xA0
VK_CONTROL = 0x11
KEYEVENTF_KEYUP = 0x0002
KEYEVENTF_EXTENDEDKEY = 0x0001
MOUSEEVENTF_LEFTDOWN = 0x0002
MOUSEEVENTF_LEFTUP = 0x0004
SW_RESTORE = 9

user32.EnumWindows.argtypes = [ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM), wt.LPARAM]
user32.GetWindowTextLengthW.argtypes = [wt.HWND]
user32.GetWindowTextW.argtypes = [wt.HWND, wt.LPWSTR, ctypes.c_int]
user32.GetClassNameW.argtypes = [wt.HWND, wt.LPWSTR, ctypes.c_int]
user32.IsWindowVisible.argtypes = [wt.HWND]
user32.IsIconic.argtypes = [wt.HWND]
user32.GetWindowRect.argtypes = [wt.HWND, ctypes.POINTER(wt.RECT)]
user32.GetWindowThreadProcessId.argtypes = [wt.HWND, ctypes.POINTER(wt.DWORD)]
user32.GetWindowThreadProcessId.restype = wt.DWORD
user32.GetForegroundWindow.restype = wt.HWND
user32.AttachThreadInput.argtypes = [wt.DWORD, wt.DWORD, wt.BOOL]
user32.SetForegroundWindow.argtypes = [wt.HWND]
user32.BringWindowToTop.argtypes = [wt.HWND]
user32.SetFocus.argtypes = [wt.HWND]
user32.ShowWindow.argtypes = [wt.HWND, ctypes.c_int]
user32.SetCursorPos.argtypes = [ctypes.c_int, ctypes.c_int]
user32.GetCursorPos.argtypes = [ctypes.POINTER(wt.POINT)]
user32.mouse_event.argtypes = [wt.DWORD, wt.DWORD, wt.DWORD, wt.DWORD, ctypes.c_void_p]
user32.keybd_event.argtypes = [wt.BYTE, wt.BYTE, wt.DWORD, ctypes.c_size_t]


def list_windows():
    out = []

    @ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)
    def cb(hwnd, lparam):
        if not user32.IsWindowVisible(hwnd):
            return True
        length = user32.GetWindowTextLengthW(hwnd)
        title = ""
        if length:
            buf = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buf, length + 1)
            title = buf.value
        cls = ctypes.create_unicode_buffer(256)
        user32.GetClassNameW(hwnd, cls, 256)
        pid = wt.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
        rect = wt.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(rect))
        out.append({
            "hwnd": int(hwnd), "pid": pid.value, "class": cls.value, "title": title,
            "rect": [rect.left, rect.top, rect.right, rect.bottom],
            "minimized": bool(user32.IsIconic(hwnd)),
        })
        return True

    user32.EnumWindows(cb, 0)
    return out


def focus(hwnd):
    target = int(hwnd)
    hwnd = wt.HWND(target)
    if user32.IsIconic(hwnd):
        user32.ShowWindow(hwnd, SW_RESTORE)
        time.sleep(0.15)
    fg = user32.GetForegroundWindow()
    fg_tid = user32.GetWindowThreadProcessId(fg, None) if fg else 0
    tgt_tid = user32.GetWindowThreadProcessId(hwnd, None)
    attached = False
    if fg_tid and tgt_tid and fg_tid != tgt_tid:
        attached = bool(user32.AttachThreadInput(fg_tid, tgt_tid, True))
    user32.BringWindowToTop(hwnd)
    ok = bool(user32.SetForegroundWindow(hwnd))
    if not ok or int(user32.GetForegroundWindow()) != target:
        # A synthetic Alt tap counts as user input and lifts the foreground lock.
        key_down(0x12)
        time.sleep(0.03)
        key_up(0x12)
        time.sleep(0.05)
        user32.BringWindowToTop(hwnd)
        ok = bool(user32.SetForegroundWindow(hwnd))
    user32.SetFocus(hwnd)
    if attached:
        user32.AttachThreadInput(fg_tid, tgt_tid, False)
    time.sleep(0.30)
    return {"set_fg": ok, "is_fg": int(user32.GetForegroundWindow()) == target,
            "attached": attached, "fg": int(user32.GetForegroundWindow())}


def click(x, y):
    old = wt.POINT()
    user32.GetCursorPos(ctypes.byref(old))
    user32.SetCursorPos(int(x), int(y))
    time.sleep(0.05)
    user32.mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, None)
    time.sleep(0.04)
    user32.mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, None)
    time.sleep(0.15)
    user32.SetCursorPos(old.x, old.y)
    return {"x": x, "y": y, "restored": [old.x, old.y]}


def key_down(vk, extended=False):
    user32.keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY if extended else 0, 0)


def key_up(vk, extended=False):
    user32.keybd_event(vk, 0, (KEYEVENTF_EXTENDEDKEY if extended else 0) | KEYEVENTF_KEYUP, 0)


def shift_toggle(gap_ms=30):
    key_down(VK_LSHIFT)
    time.sleep(gap_ms / 1000.0)
    key_up(VK_LSHIFT)


def ctrl_key(vk):
    key_down(VK_CONTROL)
    time.sleep(0.02)
    key_down(vk)
    time.sleep(0.03)
    key_up(vk)
    key_up(VK_CONTROL)


def probe():
    r = subprocess.run([DOTNET, DLL, "--probe"], capture_output=True, text=True,
                       encoding="utf-8", errors="replace", timeout=25)
    return r.stdout.strip().splitlines()


def parse_probe(lines):
    info = {}
    for line in lines:
        line = line.strip()
        m = re.match(r"foreground\s*: hwnd=0x([0-9A-Fa-f]+) pid=(\d+) tid=(\d+) class='(.*)' title='(.*)'", line)
        if m:
            info.update(hwnd=int(m.group(1), 16), pid=int(m.group(2)), tid=int(m.group(3)),
                        cls=m.group(4), title=m.group(5))
        m = re.match(r"ime\s*: open=(\d+) conv=0x([0-9A-Fa-f]+) chinese=(\w+) -> mode=(\w+)", line)
        if m:
            info.update(open=int(m.group(1)), conv=int(m.group(2), 16),
                        chinese=m.group(3) == "True", mode=m.group(4))
        m = re.match(r"anchor\s*: tier=(\w+) rect=(\((-?\d+),(-?\d+)\)-\((-?\d+),(-?\d+)\) (\d+)x(\d+)|\(empty\))", line)
        if m:
            info["tier"] = m.group(1)
            if m.group(3):
                info["anchor"] = [int(m.group(3)), int(m.group(4)), int(m.group(5)), int(m.group(6))]
        m = re.match(r"work area\s*: \((-?\d+),(-?\d+)\)-\((-?\d+),(-?\d+)\)", line)
        if m:
            info["work"] = [int(x) for x in m.groups()]
        m = re.match(r"bubble\s*: mode=(\w+) at \((-?\d+),(-?\d+)\)-\((-?\d+),(-?\d+)\)", line)
        if m:
            info.setdefault("bubbles", {})[m.group(1)] = [int(x) for x in m.groups()[1:]]
    return info


def grab(x, y, w, h):
    from PIL import ImageGrab
    return ImageGrab.grab(bbox=(x, y, x + w, y + h), all_screens=True).convert("RGB")


def pill_bbox(img, tol=20, min_area=40):
    """Largest connected #007AFF component; returns bbox in image coordinates."""
    import numpy as np
    a = np.asarray(img).astype(int)
    mask = (np.abs(a[:, :, 0] - 0) <= tol) & (np.abs(a[:, :, 1] - 122) <= tol) & (np.abs(a[:, :, 2] - 255) <= tol)
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    best = None
    for yy in range(h):
        for xx in range(w):
            if not mask[yy, xx] or seen[yy, xx]:
                continue
            q = deque([(yy, xx)])
            seen[yy, xx] = True
            minx = maxx = xx
            miny = maxy = yy
            area = 0
            while q:
                cy, cx = q.popleft()
                area += 1
                if cx < minx: minx = cx
                if cx > maxx: maxx = cx
                if cy < miny: miny = cy
                if cy > maxy: maxy = cy
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
            if best is None or area > best[4]:
                best = (minx, miny, maxx + 1, maxy + 1, area)
    if best is None or best[4] < min_area:
        return None
    return {"bbox": list(best[:4]), "area": best[4]}


def read_log_lines():
    with open(LOG, "r", encoding="utf-8-sig", errors="replace") as f:
        return f.read().splitlines()


def parse_show(lines, start=0):
    shows = []
    for line in lines[start:]:
        m = re.search(
            r"Show: code=(\S+) anchor=(-?\d+),(-?\d+),(-?\d+),(-?\d+) scale=([\d.]+) "
            r"pill=(\d+)x(\d+) target=(-?\d+),(-?\d+) window=(-?\d+),(-?\d+)", line)
        if m:
            shows.append({
                "code": m.group(1),
                "anchor": [int(m.group(i)) for i in range(2, 6)],
                "scale": float(m.group(6)),
                "pill": [int(m.group(7)), int(m.group(8))],
                "target": [int(m.group(9)), int(m.group(10))],
                "window": [int(m.group(11)), int(m.group(12))],
                "line": line,
            })
    return shows


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)

    sub.add_parser("list")
    p = sub.add_parser("focus"); p.add_argument("hwnd", type=lambda s: int(s, 0))
    p = sub.add_parser("click"); p.add_argument("x", type=int); p.add_argument("y", type=int)
    sub.add_parser("probe")
    p = sub.add_parser("shift"); p.add_argument("n", type=int, nargs="?", default=1)
    p.add_argument("--gap", type=int, default=30)
    p = sub.add_parser("ctrl"); p.add_argument("letter")
    p = sub.add_parser("shot"); p.add_argument("x", type=int); p.add_argument("y", type=int)
    p.add_argument("w", type=int); p.add_argument("h", type=int); p.add_argument("out")
    p = sub.add_parser("pill"); p.add_argument("x", type=int); p.add_argument("y", type=int)
    p.add_argument("w", type=int); p.add_argument("h", type=int)
    p = sub.add_parser("shows"); p.add_argument("start", type=int, nargs="?", default=0)
    p = sub.add_parser("loglines")

    args = ap.parse_args()
    if args.cmd == "list":
        print(json.dumps(list_windows(), ensure_ascii=False, indent=1))
    elif args.cmd == "focus":
        print(json.dumps(focus(args.hwnd), ensure_ascii=False))
    elif args.cmd == "click":
        print(json.dumps(click(args.x, args.y), ensure_ascii=False))
    elif args.cmd == "probe":
        lines = probe()
        print("\n".join(lines))
        print("PARSED " + json.dumps(parse_probe(lines), ensure_ascii=False))
    elif args.cmd == "shift":
        for _ in range(args.n):
            shift_toggle(args.gap)
            time.sleep(0.08)
        print(f"shift x{args.n}")
    elif args.cmd == "ctrl":
        vk = ord(args.letter.upper())
        ctrl_key(vk)
        print(f"ctrl+{args.letter}")
    elif args.cmd == "shot":
        img = grab(args.x, args.y, args.w, args.h)
        img.save(args.out)
        print(json.dumps({"out": args.out, "size": img.size, "origin": [args.x, args.y]}))
    elif args.cmd == "pill":
        img = grab(args.x, args.y, args.w, args.h)
        hit = pill_bbox(img)
        if hit:
            hit["screen"] = [args.x + hit["bbox"][0], args.y + hit["bbox"][1],
                             args.x + hit["bbox"][2], args.y + hit["bbox"][3]]
        print(json.dumps(hit, ensure_ascii=False))
    elif args.cmd == "shows":
        print(json.dumps(parse_show(read_log_lines(), args.start), ensure_ascii=False, indent=1))
    elif args.cmd == "loglines":
        print(len(read_log_lines()))


if __name__ == "__main__":
    main()
