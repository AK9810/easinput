# easinput

Windows 上的输入模式提示器：在文本插入符下方显示一个瞬时药丸徽标，内容是当前输入模式的短代码。

| 输入模式 | 显示 |
|---|---|
| 中文 | 蓝底「拼」 |
| 英文小写 | 蓝底「a」 |
| 英文大写 | 蓝底「A」 |

视觉与位置按 macOS Sonoma 的光标配件（cursor accessory）1:1 复刻：23pt 高药丸、圆角 = 高度/2、
系统蓝 `#007AFF`、插入符正下方居中、约 3pt 间距、柔和阴影、淡入淡出。差异只有两处，且是刻意的：
macOS 的徽标只表达输入源身份（同一输入法内切中英不变），大小写靠独立的常驻 ⇪ 徽标；
本项目显示的是**输入模式**，并把 CapsLock 折进「A」。详见 [ADR-0004](docs/adr/0004-visual-language-and-semantics.md)。

## 构建与运行

需要 .NET 8 SDK。

```bash
dotnet build easinput.sln
dotnet run --project src/EasInput.App
```

发布单文件（自包含，目标机器无需安装 .NET）：

```bash
dotnet publish src/EasInput.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 调试

- `--probe`：打印当前前台窗口的 IME 状态、锚点层级与计算出的气泡位置。排查"某个应用里不显示"先用它。
- `--demo=拼|a|A [--demo-at=x,y | --demo-rect=x1,y1,x2,y2]`：在指定锚点显示 4 秒，用于视觉校准。
- 配置：`config.json` 放在 exe 同目录（便携模式），否则用 `%APPDATA%\easinput\config.json`。
- 日志：`%APPDATA%\easinput\easinput.log`，把 `logLevel` 改成 `debug` 可看到每次状态读取与锚点降级。

## 托盘

退出、暂停/恢复、开机自启、打开配置文件。v1 没有设置窗口，JSON 就是设置界面。

## 已知限制

- 提权窗口（UIPI）、UAC 安全桌面、独占全屏游戏、锁屏：不支持。
- Chromium/WinUI3/Office/Terminal 的插入符目前只走 Win32 caret 路径，可能不准；
  UIA `TextPattern` 兜底是下一个里程碑。
- 全角/半角、中英标点、输入法显示名：不显示。
- 混合 DPI 多屏下的定位只做了基本处理，尚未逐屏验证。

## 文档

- [CONTEXT.md](CONTEXT.md)：术语表
- [docs/adr](docs/adr)：技术决策
- [docs/reference/macos-input-hud.md](docs/reference/macos-input-hud.md)：macOS 参考规格（实测值）
- [.scratch/easinput/spec.md](.scratch/easinput/spec.md)：完整规格与验收标准
