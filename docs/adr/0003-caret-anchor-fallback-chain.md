# 锚点按 GetGUIThreadInfo → UIA → 指针 逐级降级

**Status**: accepted (2026-11-17)

没有单一 API 能覆盖所有应用的插入符。实测：经典 Win32 EDIT 上 `GetGUIThreadInfo` 的 `rcCaret` 给出 1×16 的矩形（`hwndCaret` 的客户区坐标）；Chromium/Electron 上 `hwndCaret` 为 NULL，但 UIA `TextPattern` 的 `GetBoundingRectangles` 能给出插入符矩形（屏幕物理像素，1×26）。因此锚点采用固定降级链：`GetGUIThreadInfo` → UIA `TextPattern` → 指针（仅当指针位于焦点窗口内）→ 隐藏。

**Consequences**:
- 两套坐标语义必须显式换算：`rcCaret` 是客户区逻辑坐标，需 `ClientToScreen` 并按目标窗口 DPI 处理；UIA 返回物理像素。
- UIA 是跨进程调用，可能被卡住的应用拖住：必须在工作线程上带超时调用，超时即视为"无插入符"。
- 退化 range 的 `GetBoundingRectangles` 可能返回空数组；取插入符需把 range 扩一个字符再读 `rect[0]`。
- 明确不做远程线程注入（InputTip 的做法）：它能多覆盖一部分 Qt/Java 应用，但会引入杀软误报与反作弊风险，收益不值得。
