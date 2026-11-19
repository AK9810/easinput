# 05: 焦点切换触发与锚点重试

**What to build:** 修复现场反馈「重复切换窗口、目标窗口明明有插入符却不弹气泡」。
把「焦点进入另一个窗口且能解析到插入符」视为有效状态变化（即使模式未变），
并把锚点解析移出 UI 线程、失败后短暂重试。

**Status:** resolved (2026-11-19)

## 缺陷与根因

1. **同模式切窗静默。** 旧实现只在 `InputMode` 变化时显示；从中文窗口切到另一个中文窗口
   不会弹。用户感知为「有冷却时间」。
2. **切换瞬间锚点未就绪。** 新窗口获得焦点后的头几百毫秒里，Chromium/Electron 的
   MSAA/UIA 插入符可能还没建好；旧实现一次解析失败就把这次状态消费掉（「锚点失败不重试」），
   之后模式不再变化，就永远不补弹。

## 修复

- 新增 Core 纯逻辑 `TriggerPolicy`：按 `(InputMode, foreground hwnd)` 观察；
  窗口变化优先于模式变化；观察即消费，重复轮询保持安静；`Seed` 用于启动时播种。
- `BubbleController` 改为：`Apply` 只做触发判断；锚点解析在后台线程执行
  （`SemaphoreSlim` 串行化，generation 令牌作废旧请求）；隐藏结果按 150ms
  重试，共 3 次，仍失败才静默。
- 锚点解析不再阻塞 UI 线程；键→显示路径保持后台读取 + UI 派发。
- 微信 4.x 不兼容（用户决定）：Qt/TSF 自绘，Win32/MSAA/UIA 均无插入符。焦点切到微信时
  会做 3 次重试后静默，不弹到固定位置。

## 测试

- 新增 `TriggerPolicyTests`（7 例）：首次观察播种、同窗口同模式安静、同模式切窗触发、
  同窗口模式变化、窗口+模式同时变化时窗口优先、观察消费、回到旧窗口再次触发。
- App 层沿用既有端到端缝（debug 日志 + 真实窗口），未新建测试基建。

## 现场验证（2026-11-19，Debug / 2560×1440 @125%）

| 场景 | 日志证据 | 结果 |
|---|---|---|
| 启动后切到 ZCode（模式未变 LatinLower） | `Trigger: FocusChange ... Anchor: tier=Msaa ... Show: code=a` | 通过 |
| VS Code（Chinese）→ Terminal（Chinese）同模式切窗 | `Trigger: FocusChange ... Show: code=拼` | 通过 |
| 切到桌面 | `Trigger: FocusChange hwnd=0x101CA` → 3× `Anchor: hidden` | 通过（静默） |
| 切到微信 4.x | 3× `Anchor: hidden` | 通过（按决定不兼容） |
| 同窗口 CapsLock | `Trigger: ModeChange ... Latency: 29.9–41.8ms` | 通过（无回退） |
