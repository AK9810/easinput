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
| ZCode | Pointer 1×1 | 无 caret/UIA，未做位置验收 | 需人工 |
| 微信输入法自身输入框 | — | 无可见输入框 | 需人工 |
| Win32 EDIT harness | Caret 1×16 | a 841,554 → 841,554-871,583（30×29），dx+0.5，65.8ms | 通过（对照） |

位置口径：所有自动项蓝框 top-left 偏差 0px，中心 dx ≤ 0.5px，间距 4px（3 DIP × 1.25），均在 ±4px 内。

## 生命周期 / 负向

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
3. ZCode 输入框（仅 Pointer 降级）。
4. 候选窗遮挡（本轮未输入中文）。
5. 连续快速切换的闪烁。
6. 托盘菜单逐项点击。
7. 混合 DPI 多屏（单屏无法验证）。
8. Word 视觉比对（功能/位置已自动通过）。
