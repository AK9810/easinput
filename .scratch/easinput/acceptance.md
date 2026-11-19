# 验收记录 — 2026-11-18

环境：Windows 11 / 2560×1440 @125% / 单屏 / 12 逻辑核 / Debug 构建。
便携 `config.json`（`logLevel=debug`、`holdMs=4000`）用于截图，测试后已删除。

## 应用矩阵

| 应用 | 锚点层级 | 真实触发 + 位置（target → #007AFF 蓝框） | 结果 |
|---|---|---|---|
| Win11 记事本 | Caret 1×24 | CapsLock → A：501,430 → 501,430-533,459（32×29），dx+0.5，gap 4，39.1ms | 位置通过；Shift 中/英需人工 |
| 资源管理器重命名框 | Caret 1×20 | Shift → a：1229,588 → 1229,588-1259,617（30×29），dx+0.5，45.4ms | 通过 |
| Edge 地址栏 | Uia 2×20 | 拼 152,89 → 152,89-189,118（37×29），dx+0.5；a 155,89 → 155,89-185,118，dx0；25.2 / 63.3ms | 通过；Chrome 未安装 |
| VS Code | Uia 1×26 | 拼 1400,748 → 1400,748-1437,777（37×29），dx0，47.0ms | 通过 |
| Windows Terminal | Uia 12×23 | a 485,367 → 485,367-515,396（30×29），dx0，60.6ms | 通过（附加） |
| Word | Caret 1×23 | a 944,383 → 944,383-974,412（30×29），dx+0.5，46.9ms | 功能/位置通过；视觉比对需人工 |
| ZCode | Hidden（预期） | 无 caret/UIA，气泡应隐藏（原 Pointer 1×1 行为已废弃） | 需人工 |
| 微信输入法自身输入框 | Hidden（预期） | 无可见输入框，无插入符，气泡应隐藏 | 需人工 |
| 只读网页 | Hidden（预期） | 无插入符，气泡应隐藏 | 需人工 |
| Win32 EDIT harness | Caret 1×16 | a 841,554 → 841,554-871,583（30×29），dx+0.5，65.8ms | 通过（对照） |

位置口径：所有自动项蓝框 top-left 偏差 0px，中心 dx ≤ 0.5px，间距 4px（3 DIP × 1.25），均在 ±4px 内。

## 生命周期 / 负向

> 历史记录说明：本节记录于 2026-11-18；涉及「指针」的条目反映当时的四级降级链
> （含指针兜底），仅作历史比对，不代表当前语义。当前语义以
> [caret-only-bubble/spec.md](../caret-only-bubble/spec.md) 为准。

- 无 caret + 指针在焦点窗外（资源管理器）：Shift 后 mode 由 LatinLower→Chinese，`anchor=Hidden`，日志无 `Show`。通过。
- 气泡可见时切焦点（Edge→记事本）：`Show` 22:34:03.161 → `Hide` 22:34:03.362；聚焦调用后 11ms 内 Hide。最小化 Edge 同样触发 Hide。通过。
- 10 次快速 Shift：10 条 `Show` 交替「拼」/「a」，进程正常，无崩溃。闪烁视觉需人工。
- 启动后未主动弹泡；焦点切换后 `--probe` 的 mode 与各应用状态一致。

## 性能（同一环境）

- key→visible：25.2–75.7ms，avg 57.7ms（**未达 <50ms**；30ms IME 稳定延迟 + 15.6ms 定时器粒度）。
- 空闲总 CPU：0.039%（<0.1% 达标）；单核口径 0.469%。
- 私有内存：71.9–72.1MB（<100MB 达标）；WorkingSet 约 132MB（含共享页）。

## 托盘

仅代码核对，均需人工点击：启用提示→写 config + `OnEnabledChanged`；开机自启→`HKCU\...\Run\easinput`；打开配置→ShellExecute；退出→Shutdown/OnExit 释放钩子与互斥体。
2026-11-18 修正：`dotnet easinput.dll` 开发运行时，autostart 现在写 `"dotnet.exe" "easinput.dll"`，不再只写 dotnet.exe。

## 需人工

1. 记事本 Shift 中/英（合成 Shift 前后 `open` 均为 0；CapsLock 已通过）。
2. 微信输入法自身输入框（无可见输入框）。
3. ZCode 输入框（预期 Hidden，无气泡）。
4. 候选窗遮挡（本轮未输入中文）。
5. 连续快速切换的闪烁。
6. 托盘菜单逐项点击。
7. 混合 DPI 多屏（单屏无法验证）。
8. Word 视觉比对（功能/位置已自动通过）。

## 2026-11-19 修订：MSAA 插入符（ticket 04）

自动证据（`--probe`，Debug 构建，`feat/caret-only-bubble`）：

| 应用 | 锚点层级 | 实测矩形 | 结果 |
|---|---|---|---|
| VS Code（编辑器内） | Msaa | 1140,493-1141,520 1×27 | 通过；UIA 仍报整行盒 1088×26，已被拒绝 |
| ZCode 空输入框 | Msaa | 891,1039-892,1065 1×26 | 通过；修复「首字符输入前不显示」 |
| Edge 地址栏 | Msaa | 1×25 | 通过 |
| Edge 只读网页 | Hidden | — | 通过 |
| Win32 EDIT harness | Caret | 1×16 | 通过（Win32 优先级未回退） |

视觉：VS Code 与 ZCode 空输入框的气泡均在插入符正下方居中
（`.scratch/ref/fix-vscode.png`、`.scratch/ref/fix-zcode-empty3.png`）。
本表是当天的自动复测；真人与托盘项仍按 ticket 03 执行。

## 2026-11-19 修订：焦点切换触发与锚点重试（ticket 05）

自动验证（Debug 构建，`feat/caret-only-bubble`，125% 缩放）：

| 场景 | 日志证据 | 结果 |
|---|---|---|
| 启动后切到 ZCode（模式未变 LatinLower） | `Trigger: FocusChange` → `Anchor: tier=Msaa` → `Show: code=a` | 通过 |
| VS Code（Chinese）→ Windows Terminal（Chinese），同模式切窗 | `Trigger: FocusChange` → `Show: code=拼` | 通过 |
| 切到桌面 `Progman` | `Trigger: FocusChange hwnd=0x101CA` → 3× `Anchor: hidden` | 通过（静默） |
| 切到微信 4.x（Weixin.exe） | 3× `Anchor: hidden` | 通过（按决定不兼容） |
| 同窗口 CapsLock 切换 | `Trigger: ModeChange` → `Latency: 29.9–41.8ms` | 通过（无回退） |

语义变化：旧口径「状态未变的焦点切换不显示」作废；现在切到另一个能解析插入符的窗口会弹，
同一窗口、同一模式的 300ms 轮询仍不重复弹。锚点隐藏时按 150ms 间隔重试 3 次，
仍失败才静默消费。微信 4.x 实测 Win32/MSAA/UIA 均拿不到插入符，保持静默。
