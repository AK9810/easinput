# 04: 增加 MSAA 插入符层级

**What to build:** 修复现场测试发现的两个 Chromium/Electron 锚点缺陷：
VS Code 里气泡落在整行正中间；ZCode 空输入框在输入第一个字之前不显示气泡。
在锚点降级链中加入 MSAA `OBJID_CARET`，并给所有候选矩形加插入符形状校验。

**Status:** resolved (2026-11-19)

## 缺陷与根因

1. **VS Code：气泡在整行正中间。**
   焦点元素是 Monaco 的 `native-edit-context`（`ControlType.Edit`）。
   UIA `TextPattern.GetSelection()` 返回折叠 range，但 `GetBoundingRectangles()`
   给出整行盒：实测 `(959,493)-(2047,519)`，宽 1088、高 26；真实插入符在 `x=1140`。
   `BubblePlacement` 以锚点水平居中，于是落在 `x≈1503` 的整行中点。

2. **ZCode 空输入框：第一条字符输入前不显示。**
   同一个 API 对空文档返回空文本、零矩形；扩一个字符也没有矩形。
   输入第一个字之后矩形才出现，所以空文档时锚点解析失败并隐藏。

## 修复

- 新增 `MsaaCaretLocator`：`AccessibleObjectFromWindow(hwnd, OBJID_CARET)` +
  `IAccessible.accLocation`，返回真实插入符。实测两种场景都有效：
  ZCode 空输入框 `1×26`、VS Code `1×27`，空文档亦然。
- 降级链：Win32 插入符 → MSAA 插入符 → UIA 插入符 → 隐藏（`AnchorTier.Msaa`）。
- MSAA 与 UIA 一样在工作线程上带超时（冷 300ms / 暖 100ms），并通过
  `CaretGeometry` 校验与前台窗口相交。
- 新增 `CaretGeometry`：零宽且高度 > 0 → 归一化为 1px；折叠矩形只接受细条
  （宽 ≤ max(4, 高/2)）；UIA 字符兜底只接受字符盒（宽 ≤ 2×高）。整行盒一律拒绝，
  宁可隐藏也不把气泡放到行中间。

## 测试

- `AnchorResolverTests`：三源优先级（Win32 > MSAA > UIA > 隐藏），含 MSAA 防回归。
- `CaretGeometryTests`：VS Code 实测整行盒 `1088×26` 拒绝、零宽 `0×41` 归一化、
  细条 `1×27` 保留、字符盒接受、空矩形/负宽拒绝。
- App 层沿用既有端到端缝（`--probe` + 真实应用），不新建测试基建。

## Comments

- 2026-11-19 自动验证（`--probe`，本机 2560×1440 @125%）：
  - VS Code → `Msaa (1140,493)-(1141,520) 1x27`（UIA 仍报整行盒 `1088×26`，已被拒绝）
  - ZCode 空输入框 → `Msaa (891,1039)-(892,1065) 1x26`
  - Edge 地址栏 → `Msaa 1x25`；Edge 只读页 → `Hidden`
  - Win32 EDIT harness → `Caret 1x16`（优先级未回退）
- 视觉验证：VSCode 与 ZCode 空输入框各一张截图，气泡均在插入符正下方居中
  （`.scratch/ref/fix-vscode.png`、`.scratch/ref/fix-zcode-empty3.png`）。
- 真人验收仍按 ticket 03 执行，验收矩阵中的层级预期已同步为 `Msaa`。
