# 01: 托盘「重启」菜单项

**What to build:** 托盘右键菜单新增「重启」，效果等同于人工退出并重新打开。见 `spec.md`。

**Status:** resolved (2026-11-24)

## 实现

- `TrayIcon`：菜单变为 `启用提示 / 开机自启 / ─ / 打开配置文件 / 重启 / ─ / 退出`，
  新增 `restart` 回调；气泡文案「已重启」。
- `App.Restart()`：Warn 级写下 `BubbleController.DescribeState()` 快照 → 释放单实例 Mutex →
  拉起新进程（继承原参数，追加 `--restarted`）→ 报告两处钩子的卸载返回值 → `Shutdown()`。
  拉起失败则回收 Mutex、弹气泡报错、**保持运行**，并允许再次点击。
- `App.AcquireMutex(restarted)`：`--restarted` 的实例拿不到 Mutex 时最多重试 3 秒，
  避免把「旧进程还没退完」误判成「已经在运行」而退出。
- `KeyboardHook.Unhook()` / `ForegroundWatcher.Unhook()`：返回并在 Warn 级报告系统是否还认识该钩子，
  返回 false 即证明它早已被系统静默摘掉（漂移证据）。幂等，`Dispose` 复用。
- `CapsLockTracker.ReadSystemLocked()`：绕开计数状态直读 `GetKeyState`，供快照对比。
- `TriggerPolicy`：暴露 `CurrentMode` / `CurrentWindow` / `IsSeeded` 只读属性。

## 测试

- 79 个单测全绿（Core 纯逻辑未改判定，无新增用例）。
- App 层沿用既有端到端缝（Warn/Debug 日志 + 真实进程），用一次性 `--selftest-restart` 触发同一代码路径，
  验证后已移除该临时钩子。

## 现场验证（2026-11-24，Debug）

| 场景 | 日志证据 | 结果 |
|---|---|---|
| 触发重启 | `Restart: capsTracked=False capsSystem=False seeded=True mode=Chinese ... enabled=True` → `Keyboard hook: removed cleanly.` → `Foreground hooks: removed=True/True.` → `Restart handed off; exiting.` → `Exited.` | 通过 |
| 新进程接管 | `Started.` → `Restarted by the tray menu.`，日志时间差 433–441ms（两次实测），`tasklist` 只剩一个新 PID | 通过 |
| 旧进程未退时启动 `--restarted` | 3 秒后 `Another instance is already running;` → `Exited.`，不挂死、不残留 | 通过 |
| 托盘菜单可视化与点击 | 未自动化（菜单项由 `TrayIcon` 构造，与自测走同一 `Restart` 回调） | 未验证 |
