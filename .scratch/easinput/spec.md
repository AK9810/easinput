# easinput — 规格

Windows 上的输入模式提示器：在文本插入符下方显示一个瞬时药丸徽标，
内容为当前输入模式的短代码——中文「拼」、英文小写「a」、英文大写「A」。

- 术语见 [CONTEXT.md](../../CONTEXT.md)
- 技术决策见 [docs/adr/](../../docs/adr/)
- macOS 参考规格见 [docs/reference/macos-input-hud.md](../../docs/reference/macos-input-hud.md)

## 目标

1. 在任何能拿到插入符的应用里，切换中/英或 CapsLock 时，插入符下方出现一个 23pt 高的药丸徽标。
2. 徽标的几何、位置、阴影、动效按 macOS Sonoma 的实测值 1:1 标定；字体与阴影的最终值以本机实测校准为准（见视觉规格表）。
3. 拿不到插入符时逐级降级，绝不出现"飘在无关位置"的徽标。
4. 单文件便携，零安装、零运行时依赖、零第三方库。

## 非目标

- 不做输入法本身，不接管按键，不改变输入行为。
- 不支持提权窗口（UIPI）、UAC 安全桌面、独占全屏游戏、锁屏。
- 不做全角/半角、中英标点、输入法显示名。
- 不做设置窗口、全局热键、按应用禁用名单、自动更新、代码签名。

## 状态模型

| 输入模式 | 判定 | 短代码 |
|---|---|---|
| 中文 | IME 打开 且 转换模式含 `IME_CMODE_NATIVE` | 拼 |
| 英文大写 | CapsLock 锁定 | A |
| 英文小写 | 其余情况 | a |

判定优先级：CapsLock > 中文 > 英文小写。
`中文 = open && (conv & IME_CMODE_NATIVE)`；`open` 必须参与判定（微信输入法英文模式只关 IME，
转换模式位不变）。解码需容忍非 0/1 的 open 值。

## 状态源

`ImmGetDefaultIMEWnd(fgHwnd)` + `SendMessageTimeout(WM_IME_CONTROL, IMC_GETOPENSTATUS / IMC_GETCONVERSIONMODE)`
带 `SMTO_ABORTIFHUNG`，超时 20ms。有 `ImmGetContext` 时用 `ImmGetConversionStatus` 补充。
CapsLock 用 `GetKeyState(VK_CAPITAL) & 1`，并以低级键盘钩子的边沿计数校正。

短代码 v1 默认取「拼」/「a」/「A」，映射表在配置里可覆盖。按输入法身份自动推导
（`HKLM\SOFTWARE\Microsoft\CTF\TIP\{CLSID}\LanguageProfile\<langid>\{profile}` 的
`Description` / `Display Description`；拼音类 → 「拼」，其他 → 本地化名称首字）排 v1.1。

## 锚点降级

> 本节自 2026-11-19 起作废：降级链已收紧为 Win32 插入符 → UIA 插入符 → 隐藏（无指针级）。
> 以 [.scratch/caret-only-bubble/spec.md](../caret-only-bubble/spec.md) 为准。

历史记录（非执行指令）：2026-11-19 之前的降级链为 Win32 插入符 → UIA 插入符 →
指针（仅当指针位于焦点窗口内）→ 隐藏；逐级校验细节见 git 历史。
当前语义由 [.scratch/caret-only-bubble/spec.md](../caret-only-bubble/spec.md) 定义。

## 触发与生命周期

- 触发：有效状态变化。来源 = 低级键盘钩子（修饰键抬起后延迟 30ms 重读）、
  `EVENT_SYSTEM_FOREGROUND`、300ms 轮询兜底。焦点进入另一个窗口且能解析到插入符，
  即使输入模式未变也视为有效状态变化。
- 显示：立即出现在锚点下方，水平居中，间距 3pt。
- 消失：任何**非修饰键**按下立即消失；否则停留 1000ms 后淡出。
- 已显示时状态再变：只换内容并重置计时，不重播入场动画。
- 焦点窗口失焦/最小化、锚点降级到隐藏：立即消失。
- 应用启动、登录、鼠标移动、同一窗口内状态未变的轮询：不显示。

## 视觉规格

> 字体与阴影为本机实测标定值；与 macOS 原始测量值的差异及理由见 ADR-0004。

| 项 | 值 |
|---|---|
| 高度 | 23 DIP |
| 圆角 | 11.5 DIP（= 高度/2） |
| 填充 | `#007AFF` |
| 短代码 | 白色，Segoe UI Variable Display，12 DIP；中文 Normal、拉丁 SemiBold（CJK 回退字重偏重，按实测校准） |
| 水平内边距 | 0.39 × 高度（23 DIP 高时 ≈ 8.97 DIP），校准到「拼」总宽 ≈ 30 DIP |
| 锚点 | 插入符矩形下方 3 DIP，水平居中于插入符中心 |
| 阴影 | 向下 2 DIP，模糊 9 DIP，黑 15%（本机 125% 缩放下实测校准） |
| 入场 | 120ms 淡入 |
| 停留 | 1000ms |
| 出场 | 200ms 淡出 |
| 越界 | 底部越界翻到插入符上方；左右越界夹紧到工作区内 |

窗口：`WS_POPUP` + `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`，
`ShowActivated=false`，不进任务栏、不进 Alt+Tab、全窗口点击穿透。

## 配置

`config.json`，便携优先：exe 同目录存在则用之，否则 `%APPDATA%\easinput\config.json`。

```json
{
  "enabled": true,
  "holdMs": 1000,
  "fadeInMs": 120,
  "fadeOutMs": 200,
  "gapDip": 3,
  "pillHeight": 23,
  "pillColor": "#007AFF",
  "fontFamily": "Segoe UI Variable Display",
  "fontSize": 12,
  "shortCodes": { "chinese": "拼", "latinLower": "a", "latinUpper": "A" },
  "autostart": false,
  "logLevel": "warn"
}
```

## 验收

**应用矩阵**：Win11 记事本、资源管理器重命名框、Chrome 地址栏、VS Code、Windows Terminal、
Word、ZCode、微信输入法自身输入框。

**检查项**：位置居中与间距、不遮挡候选窗、切换延迟、切焦点后状态正确、无插入符时隐藏、
多屏与 150%/200% DPI、连续快速切换不闪烁。

**性能预算**：按键→显示 < 50ms；空闲 CPU < 0.1%；内存 < 100MB。

口径：CPU 按整机总 CPU，内存按私有内存。2026-11-18 实测（Debug / 12 逻辑核 / 125%）：
key→visible 42.6–68.9ms，avg 57.7ms（**未达标**；主因是 30ms IME 稳定延迟 + 15.6ms 定时器粒度）；
空闲总 CPU 0.039%（达标）；私有内存 71.9MB（达标）。

**自动化**：逻辑层 xUnit（状态解码、锚点层级选择、DPI 换算、配置解析）；
`easinput --probe` 打印当前前台窗口的状态/锚点/层级；
`easinput --calibrate` 渲染 100/150/200% DPI 下的徽标 PNG 供人眼比对。

最近一次验收记录见 [acceptance.md](acceptance.md)（2026-11-18）。

## 里程碑

- **M1 垂直切片**：经典 Win32 锚点 + 状态源 + 徽标渲染 + 键盘钩子触发。
- **M2 外壳**：托盘、配置、单实例、开机自启、日志、`--probe`。
- **M3 长尾**：UIA 锚点（已落地，Chromium 系验证）、`--calibrate`（已落地，渲染 100%/150%/200% DPI 的 PNG）、验收矩阵跑通（待完成）。
- **M4 打磨**：视觉校准、性能测量、README 与截图。
