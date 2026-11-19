# 锚点按 Win32 → MSAA → UIA → 隐藏 逐级降级

**Status**: accepted (2026-11-17), revised (2026-11-19)

没有单一 API 能覆盖所有应用的插入符。实测：经典 Win32 EDIT 上 `GetGUIThreadInfo` 的 `rcCaret` 给出 1×16 的矩形（`hwndCaret` 的客户区坐标）；Chromium/Electron 上 `hwndCaret` 为 NULL。Chromium 的 UIA `TextPattern` 有时能给出插入符矩形（1×26），但对折叠 selection 也可能返回整行盒或空矩形；同一批应用上 MSAA `OBJID_CARET` 给出的就是真实插入符。因此锚点采用固定降级链：`GetGUIThreadInfo` → MSAA `OBJID_CARET` → UIA `TextPattern` → 隐藏。气泡只在解析到真实插入符时显示。

**Consequences**：
- 坐标语义必须显式换算：`rcCaret` 是客户区逻辑坐标，需 `ClientToScreen` 并按目标窗口 DPI 处理；MSAA 与 UIA 返回屏幕物理像素。
- MSAA 与 UIA 都是跨进程调用，可能被卡住的应用拖住：必须在工作线程上带超时调用，超时即视为"无插入符"。
- 插入符候选矩形必须过形状校验：折叠 range 只接受细条（宽 ≤ max(4, 高/2)），整行盒与空矩形都拒绝；UIA 的字符兜底只接受字符盒（宽 ≤ 2×高）。
- 明确不做远程线程注入（InputTip 的做法）：它能多覆盖一部分 Qt/Java 应用，但会引入杀软误报与反作弊风险，收益不值得。

**Implementation notes (2026-11-18)**:
- 只查 `AutomationElement.FocusedElement`，不回退到窗口根元素：避免把只读网页的选区当成插入符。
- `TextPattern.IsReadOnlyAttribute == true` 时视为无插入符——网页/PDF 有选区，但没有文本插入符。
- 选区先 `MoveEndpointByRange` 折叠到末端再取矩形；折叠矩形只接受细条形状，整行盒拒绝。
  折叠后无矩形时扩一个字符取字符盒；字符盒宽度超过 2×行高视为整行盒并拒绝。
- 首次查询某个进程给 300ms（UIA provider 冷启动，VS Code 实测 >100ms），之后 100ms；超时即视为"无插入符"。
- 返回的矩形必须与前台窗口矩形相交，否则视为无效。

## Revision (2026-11-19): 删除指针级
- 当时的降级链由四级收紧为三级：`GetGUIThreadInfo` → UIA `TextPattern` → 隐藏。指针级（仅要求指针位于焦点窗口内）已删除——在桌面、文件夹空白处等非文本区域，它会把气泡挂到与输入无关的位置，被用户当作误触弹窗。
- 参照物在此点是反面教材：macOS 在同样场景下也在鼠标指针处弹出，长期被用户抱怨且没有官方开关可关。本项目有意比它更严格——没有插入符就没有提示位置。
- 触发机制与「有效状态变化」语义不变，本次修订只改锚点层。

## Revision (2026-11-19): 增加 MSAA 插入符

现场两个缺陷都出在 Chromium/Electron 的 UIA 几何上：

- VS Code：UIA `TextPattern.GetSelection()` 返回折叠 range，但 `GetBoundingRectangles()`
  给出整行盒（实测 1088×26），真实插入符在行内 x=1140；气泡按锚点居中，于是落在整行正中间。
- ZCode 空输入框：同一个 API 返回空文本、没有任何矩形，扩一个字符也没有；输入第一个字之后才出现矩形。
  空文档因此解析不到锚点，气泡不显示。

MSAA `AccessibleObjectFromWindow(hwnd, OBJID_CARET)` + `accLocation` 在两个应用里都直接给出
真实插入符（实测 ZCode 空输入框 1×26、VS Code 1×27，空文档同样有效）。降级链增加一级：

Win32 插入符 → MSAA 插入符 → UIA 插入符 → 隐藏。

- MSAA 也是跨进程调用，照 UIA 的做法放在工作线程上带超时；`0×0` 视为无插入符，
  零宽但高度 > 0 的矩形归一化为 1px 宽。
- 两个来源都过 `CaretGeometry` 校验：折叠矩形只接受细条（宽 ≤ max(4, 高/2)），
  整行盒（VS Code 的 1088×26）拒绝——宁可隐藏，也不把气泡放到行中间；
  UIA 的字符兜底只接受字符盒（宽 ≤ 2×高）。
- MSAA 返回的矩形仍须与前台窗口相交；只读网页实测 `OBJID_CARET` 返回 0×0，
  不会把页面选区当成插入符。
- 验收预期更新：VS Code / Edge 地址栏 / ZCode 输入框 → `Msaa`；Win32 EDIT → `Caret`；
  只读网页 / 桌面 → `Hidden`。

## Revision (2026-11-19): 焦点切换触发、锚点重试与微信边界

触发层修订（锚点层仍保持三级链）：

- 焦点进入另一个窗口且解析到插入符，即视为有效状态变化，即使输入模式未变也弹。
  旧语义「状态未变的焦点切换不显示」作废；同一窗口、同一模式的 300ms 轮询仍不重复弹。
- 锚点解析移到后台线程；隐藏结果按 150ms 间隔重试，共 3 次。切换瞬间新窗口的插入符
  可能尚未就绪，旧的「一次失败就消费」在现场表现为漏报。重试只服务这一次有效状态变化，
  新窗口/新模式会作废旧重试。

微信 4.x 边界（2026-11-19 实测）：

- `Weixin.exe` 主窗口：`Qt51514QWindowIcon`，子窗口 `MMUIRenderSubWindowHW`，IME 走 TSF
  （线程有 `MSCTFIME UI` 与 `Default IME` 窗口，但 `ImmGetContext` 返回 NULL）。
- `GetGUIThreadInfo` 的 `hwndCaret` 为 NULL（输入框已点击聚焦、IME open=1 时同样）；
  `AccessibleObjectFromWindow(OBJID_CARET)` 返回 0×0；UIA `FocusedElement` 是顶层 Window，
  只有 Window/Transform pattern；MSAA `OBJID_CLIENT` 只有两个不可枚举子节点。
- 三条路径都拿不到插入符。决定：**先不兼容微信**，不引入按窗口推算的固定锚点，也不恢复
  指针兜底；注入方案仍按本文档开头的立场否决。若未来必须支持，需单独立项决策。
