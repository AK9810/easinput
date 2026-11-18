# 输入模式从 WM_IME_CONTROL 读取，而不是 IMM32 上下文

**Status**: accepted (2026-11-17)

读取中/英有两条直觉路径，都不可用：`ImmGetContext` + `ImmGetConversionStatus` 在本机实测中对 Chromium/Electron 返回 NULL；TSF compartment 是 per-thread 的，跨进程拿不到。改用 `ImmGetDefaultIMEWnd(hwnd)` + `SendMessageTimeout(WM_IME_CONTROL, IMC_GETOPENSTATUS / IMC_GETCONVERSIONMODE)`：实测在微信输入法上精确跟随 Shift 切换（1→0→1），且在 Chromium 前台、`ImmGetContext` 为 NULL 时仍然有效。

判定式为 `中文 = open && (conversionMode & IME_CMODE_NATIVE)`。`open` 必须参与判定——微信输入法的英文模式只关闭 IME，`IME_CMODE_NATIVE` 位保持不变，单看转换模式会把英文误判成中文。解码还要容忍非 0/1 的 open 值（ImTip 作者记录过某些输入法返回 2/1）。

**Consequences**:
- 每次读取都是跨进程同步消息，必须带超时（`SMTO_ABORTIFHUNG`），不能高频轮询。
- 没有状态变化通知：`WM_IME_NOTIFY` 只发给目标应用。只能靠低级键盘钩子触发 + 低频轮询兜底。
- 输入模式是 per-window/per-thread 的，焦点切换后必须重新查询，不能跨窗口缓存。
- 提权窗口受 UIPI 限制读不到状态，这是明确不支持的范围。
