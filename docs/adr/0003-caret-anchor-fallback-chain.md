# 锚点按 GetGUIThreadInfo → UIA → 隐藏 逐级降级

**Status**: accepted (2026-11-17), revised (2026-11-19)

没有单一 API 能覆盖所有应用的插入符。实测：经典 Win32 EDIT 上 `GetGUIThreadInfo` 的 `rcCaret` 给出 1×16 的矩形（`hwndCaret` 的客户区坐标）；Chromium/Electron 上 `hwndCaret` 为 NULL，但 UIA `TextPattern` 的 `GetBoundingRectangles` 能给出插入符矩形（屏幕物理像素，1×26）。因此锚点采用固定降级链：`GetGUIThreadInfo` → UIA `TextPattern` → 隐藏。气泡只在解析到真实插入符时显示。

**Consequences**:
- 两套坐标语义必须显式换算：`rcCaret` 是客户区逻辑坐标，需 `ClientToScreen` 并按目标窗口 DPI 处理；UIA 返回物理像素。
- UIA 是跨进程调用，可能被卡住的应用拖住：必须在工作线程上带超时调用，超时即视为"无插入符"。
- 退化 range 的 `GetBoundingRectangles` 可能返回空数组；取插入符需把 range 扩一个字符再读 `rect[0]`。
- 明确不做远程线程注入（InputTip 的做法）：它能多覆盖一部分 Qt/Java 应用，但会引入杀软误报与反作弊风险，收益不值得。

**Implementation notes (2026-11-18)**:
- 只查 `AutomationElement.FocusedElement`，不回退到窗口根元素：避免把只读网页的选区当成插入符。
- `TextPattern.IsReadOnlyAttribute == true` 时视为无插入符——网页/PDF 有选区，但没有文本插入符。
- 选区先 `MoveEndpointByRange` 折叠到末端再取矩形；折叠后仍为空时扩一个字符取 `rect[0]`。
- 首次查询某个进程给 300ms（UIA provider 冷启动，VS Code 实测 >100ms），之后 100ms；超时即视为"无插入符"。
- 返回的矩形必须与前台窗口矩形相交，否则视为无效。

**Revision (2026-11-19)**:
- 降级链由四级收紧为三级：`GetGUIThreadInfo` → UIA `TextPattern` → 隐藏。指针级（仅要求指针位于焦点窗口内）已删除——在桌面、文件夹空白处等非文本区域，它会把气泡挂到与输入无关的位置，被用户当作误触弹窗。
- 参照物在此点是反面教材：macOS 在同样场景下也在光标处弹出，长期被用户抱怨且没有官方开关可关。本项目有意比它更严格——没有插入符就没有提示位置。
- 触发机制与「有效状态变化」语义不变，本次修订只改锚点层。
