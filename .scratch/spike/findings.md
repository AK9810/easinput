# Round 0 调研记录（未定稿，仅记录实测事实）

> 目的：为"光标旁输入模式气泡"确定可行性与技术底座。
> 所有"实测"结论均在本机（Win11 26H2 / zh-CN / 微信输入法 WeType 3.0.2.44）上跑出，
> 脚本见同目录 `ime_probe*.py`、`uia_*.ps1`、`icons.ps1`。这些脚本是探针，不是产品代码。

## 1. 状态源：中/英

| 路径 | 结果 |
|---|---|
| `ImmGetContext(hwnd)` | 经典 Win32 EDIT 窗口可用；**Chromium/Electron 顶层/子窗口/默认 IME 窗口全部返回 NULL** |
| `ImmGetConversionStatus` | 微信输入法在英文模式下 `IME_CMODE_NATIVE` 仍为 1（stale），**不能单独用** |
| `ImmGetOpenStatus` | 精确跟随 Shift 切换：中文=TRUE，英文=FALSE |
| `ImmGetDefaultIMEWnd(hwnd)` + `SendMessageTimeout(WM_IME_CONTROL, IMC_GETOPENSTATUS=5)` | **精确跟随切换（1→0→1），且 Chromium 跨进程有效** |
| `IMC_GETCONVERSIONMODE=1` | 微信输入法下恒为 1（与 open status 组合才有意义） |

实测输出（probe4）：
```
[initial]      ImmGetConversionStatus: conv=0x0001 中文=True open=True
               IMC_GETOPENSTATUS     : 1
[after shift]  ImmGetConversionStatus: conv=0x0001 中文=True open=False
               IMC_GETOPENSTATUS     : 0
[Chromium 前台] ImmGetContext: NULL
               IMC_GETOPENSTATUS     : 1   ← 跨进程可用
```

**结论**：`中文 = (open == 1) && (conv & IME_CMODE_NATIVE)`。
`open` 必须纳入判定，否则微信输入法的英文模式会误判成中文。
注意 ImTip 作者文档提到某些输入法返回 `opened 中文=2 / 英文=1`，解码要容忍非 0/1 值。

**没有状态变化通知**：`WM_IME_NOTIFY` 只发给目标应用（实测 Shift 切换时我们收不到任何通知）。
→ 必须 LL 键盘钩子触发 + 低频轮询兜底。

## 2. 锚点：插入符矩形

| 路径 | 结果 |
|---|---|
| `GetGUIThreadInfo(tid).rcCaret` | 真实 Win32 EDIT 上得到 `hwndCaret=0x000B084C rc=(37,1,38,17)`，**1x16**；**客户区坐标**（相对 hwndCaret），需 `ClientToScreen` |
| 同上，Chromium 前台 | `hwndCaret=NULL rc=(0,0,0,0)` — 无 Win32 caret |
| UIA `TextPattern.GetSelection().GetBoundingRectangles()`，Chromium | `ControlType.Document` 上返回 `(1052,957,1,26)` — **屏幕物理像素**，正是插入符 |

**结论**：两级锚点。UIA 退化 range 可能返回空数组，实现时需 `MoveEndpointByUnit(End, Character, 1)` 扩一个字符再取 `rect[0]`。

## 3. 输入法身份与图标

注册表链路（实测微信输入法）：
```
HKCU\Control Panel\International\User Profile\zh-Hans-CN
  → 0804:{86598FB9-...}{607FDF85-...}
HKLM\SOFTWARE\Microsoft\CTF\TIP\{86598FB9-...}\LanguageProfile\0x00000804\{607FDF85-...}
  Description         = WeType
  IconFile            = C:\Windows\system32\wetype_tip.dll
  IconIndex           = 0
  Display Description = @C:\Windows\system32\wetype_tip.dll,-102
```
`ExtractIconEx` 成功提取 32x32 品牌图标（微信输入法绿色 logo）。
`ImmGetDescription/ImmGetIMEFileName` 对 TSF 输入法返回空 → 不可用。

## 4. 覆盖矩阵（实测 + 文档）

| 应用类 | 插入符 | 中/英状态 |
|---|---|---|
| 经典 Win32（EDIT/RichEdit/对话框） | GetGUIThreadInfo ✅ | WM_IME_CONTROL ✅ |
| Chromium/Electron（Chrome/VS Code/ZCode/Slack） | UIA TextPattern ✅ | WM_IME_CONTROL ✅ |
| WinUI3/UWP、Terminal、Office | UIA TextPattern（文档：可用） | WM_IME_CONTROL（待验） |
| Qt / Java(Swing) / 画布 / 游戏 | ❌ | 视 IME 而定 |
| 提权窗口（UIPI） | ❌ | ❌（除非同权限） |
| UAC 安全桌面 / 锁屏 / 远程会话 | ❌ | ❌ |

## 5. 环境事实

- 本机：Win11 26H2（build 26300），zh-CN，激活输入法 = 微信输入法 WeType（微软拼音已禁用）。
- 工具链：**无 .NET SDK、无 MSVC/Windows SDK、无 Rust、无 AHK、无 cmake**；
  有 Node 25 / pnpm / bun / Python 3.12+3.14 / Java 17 / Git / gh（已登录）。
- → 任何原生方案都需先安装工具链；Python 可零安装做探针（已用）。

## 6. 参考实现（技法来源）

- **ImTip**（MIT，aardio）：`WM_IME_CONTROL` 读状态；caret 链 = winex.caret → MSAA(@类窗口) → JAB(JetBrains) → 鼠标兜底。
- **InputTip**（AGPL-3.0，AHK v2）：caret 链 = GetGUIThreadInfo → UIA TextPattern2 → 远程线程注入 → MSAA OBJID_CARET → WPF caret → ACC。
- **Weasel**（GPL-3.0，C++/TSF）：`ITfContextView::GetTextExt` 取插入符；`WS_EX_NOACTIVATE|TOPMOST|NOREDIRECTIONBITMAP` + Direct2D/DirectComposition 画状态窗，位置 = caret + 6px。
- **IME_Indicator**（Rust）：只做 CN/EN 彩色圆点。
