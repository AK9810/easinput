# easinput

[![协议](https://img.shields.io/github/license/AK9810/easinput?label=%E5%8D%8F%E8%AE%AE&logo=gnu)](https://github.com/AK9810/easinput/blob/main/LICENSE)
[![版本](https://img.shields.io/github/v/release/AK9810/easinput?include_prereleases&label=%E7%89%88%E6%9C%AC)](https://github.com/AK9810/easinput/releases/latest)
![语言](https://img.shields.io/badge/%E8%AF%AD%E8%A8%80-C%23%20.NET%208-512BD4?logo=dotnet&logoColor=white)
![单文件](https://img.shields.io/badge/%E5%8D%95%E6%96%87%E4%BB%B6-66%20MB-007AFF)

Windows 上的输入模式提示器：在文本插入符下方显示一个瞬时药丸徽标，内容是当前输入模式的短代码。

| 输入模式 | 显示 |
|---|---|
| 中文 | 蓝底「拼」 |
| 英文小写 | 蓝底「a」 |
| 英文大写 | 蓝底「A」 |

视觉与位置按 macOS Sonoma 的插入符配件（cursor accessory）1:1 复刻：23pt 高药丸、圆角 = 高度/2、
系统蓝 `#007AFF`、插入符正下方居中、约 3pt 间距、柔和阴影、淡入淡出。差异只有两处，且是刻意的：
macOS 的徽标只表达输入源身份（同一输入法内切中英不变），大小写靠独立的常驻 ⇪ 徽标；
本项目显示的是**输入模式**，并把 CapsLock 折进「A」。详见 [ADR-0004](docs/adr/0004-visual-language-and-semantics.md)。

## 为什么用它

- 单文件 66MB，零安装、零运行时依赖、零第三方库。
- 零网络请求（源码里没有任何 HTTP/Socket 调用），纯本地，不联网。
- 空闲总 CPU 0.039%、私有内存 71.9MB（Debug / 12 逻辑核实测）。
- 插入符下方显示，不挡正文；锚点三级降级：Win32 插入符 → UIA TextPattern → 隐藏。
- 三种状态「拼 / a / A」，CapsLock 折进「A」——macOS 原生没有这个语义。
- 视觉按 macOS Sonoma 插入符配件 1:1 复刻：23pt 高、圆角 = 高度/2、`#007AFF`、3pt 间距、柔和阴影。

## 使用方法

### 下载与运行

从 [Releases](https://github.com/AK9810/easinput/releases/latest) 下载 `easinput-v0.1.1-win-x64.exe`
（单文件约 66MB，自包含，Windows 10/11 x64，只在 Windows 11 上实测过，无需安装 .NET），双击运行。

程序没有主窗口，图标在任务栏右下角的托盘溢出区（`^`）里。首次运行会被 SmartScreen 拦：
点「更多信息」→「仍要运行」。

首次启动会弹一次系统通知，说明程序在托盘运行；之后不再弹。

### 托盘菜单

右键托盘图标，菜单有四项：

- **启用提示**：勾选或取消，立即暂停或恢复气泡，并把 `enabled` 写回配置。
- **开机自启**：勾选或取消，同步写入或删除注册表启动项。
- **打开配置文件**：用默认程序打开当前生效的 `config.json`；双击托盘图标同效。
- **退出**：结束程序，释放键盘钩子与单实例互斥体。

### 开机自启

默认关闭。在托盘菜单里勾选后，程序写入
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 的 `easinput` 值；取消则删除。
启动时以配置里的 `autostart` 为准：程序每次启动都会按它同步注册表，手动改配置后重启也生效。

## 配置

- 便携优先：`config.json` 放在 exe 同目录时使用该文件，否则使用 `%APPDATA%\easinput\config.json`。
- 首次运行会生成一份默认配置。手动改完保存后，重启程序生效；托盘菜单里的两个开关会立即写入配置。
- 日志：`%APPDATA%\easinput\easinput.log`。把 `logLevel` 改成 `debug` 并重启，可看到每次状态读取与锚点降级。
- v1 没有设置窗口，JSON 就是设置界面。

| 字段 | 默认值 | 说明 |
|---|---|---|
| `enabled` | `true` | 总开关 |
| `holdMs` | `1000` | 100–10000，气泡停留毫秒 |
| `fadeInMs` | `120` | 0–2000 |
| `fadeOutMs` | `200` | 0–2000 |
| `gapDip` | `3` | 0–32，气泡与插入符的间距（DIP） |
| `pillHeight` | `23` | 12–64，药丸高度（DIP） |
| `pillColor` | `"#007AFF"` | `#RRGGBB` 或 `#AARRGGBB` |
| `fontFamily` | `"Segoe UI Variable Display"` | 字体 |
| `fontSize` | `12` | 6–48 |
| `shortCodes.chinese` / `.latinLower` / `.latinUpper` | `"拼"` / `"a"` / `"A"` | 三种模式的短代码 |
| `autostart` | `false` | 开机自启；启动时以它为准写注册表 |
| `logLevel` | `"warn"` | `error` / `warn` / `info` / `debug` |
| `firstRunShown` | `false` | 首次运行提示是否已展示；改回 `false` 可重看 |

## 排查

- 某个应用里不显示：先在终端里跑 `easinput.exe --probe`，看 anchor 的 tier。
- 提权窗口 / UAC 安全桌面 / 独占全屏 / 锁屏：本来就不支持。
- 只读网页、PDF：没有插入符，气泡不显示。
- 找不到托盘图标：Win11 默认收进 `^` 溢出区。
- 再次双击 exe 没反应：它已经在运行，会有一次「easinput 已在运行」提示。
- 日志：`%APPDATA%\easinput\easinput.log`，`logLevel` 改 `debug` 并重启。

## 升级

先在托盘菜单里退出，再用新 exe 覆盖旧文件。程序运行时会占用 exe，直接覆盖会失败。

## 已知限制

- 提权窗口（UIPI）、UAC 安全桌面、独占全屏游戏、锁屏：不支持。
- Chromium/WinUI3/Office/Terminal 的插入符走 UIA `TextPattern` 兜底（Chromium 系已验证；
  首次查询某个应用有约 300ms 的 UIA 冷启动）。只读网页/PDF 会被判定为无插入符，气泡隐藏。
- 全角/半角、中英标点、输入法显示名：不显示。
- 混合 DPI 多屏下的定位只做了基本处理，尚未逐屏验证。
- 按键→显示实测 avg 57.7ms（Debug / 12 逻辑核），高于 50ms 预算；空闲总 CPU 0.039%、私有内存 71.9MB。
- exe 未签名，首次运行需通过 SmartScreen。

## 开发者

需要 .NET 8 SDK。

### 构建与运行

```bash
dotnet build easinput.sln
dotnet run --project src/EasInput.App
```

### 调试

- `--probe`：打印当前前台窗口的 IME 状态、锚点层级与计算出的气泡位置。排查"某个应用里不显示"先用它。
- `--demo=拼|a|A [--demo-at=x,y | --demo-rect=x1,y1,x2,y2]`：在指定锚点显示 4 秒，用于视觉校准。
- `--calibrate`：在当前目录渲染中文「拼」的徽标 PNG（`calibrate-100.png` / `calibrate-150.png` /
  `calibrate-200.png`），打印每个文件的绝对路径与像素尺寸，用于人眼比对 100%/150%/200% DPI。

### 发布单文件

自包含，目标机器无需安装 .NET：

```bash
dotnet publish src/EasInput.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -o publish
```

产物是单个 `publish/easinput.exe`（约 66MB）。只写 `PublishSingleFile=true` 是不够的：WPF 的 5 个原生
DLL（`wpfgfx_cor3`、`PresentationNative_cor3` 等）默认留在 exe 旁边，一共 6 个文件约 140MB。
`IncludeNativeLibrariesForSelfExtract` 把它们也塞进 exe，首次启动解压到 `%TEMP%\.net\easinput\`。
exe 未签名，首次下载运行会被 SmartScreen 拦一下。

## 文档

- [CONTEXT.md](CONTEXT.md)：术语表
- [docs/adr](docs/adr)：技术决策
- [docs/reference/macos-input-hud.md](docs/reference/macos-input-hud.md)：macOS 参考规格（实测值）
- [.scratch/easinput/spec.md](.scratch/easinput/spec.md)：完整规格（「锚点降级」一节已作废，现行语义见下）
- [.scratch/caret-only-bubble/spec.md](.scratch/caret-only-bubble/spec.md)：现行的纯插入符锚点规格

## 许可证

- 本项目以 **GPL-3.0-only** 发布，见 [LICENSE](LICENSE)。
- Copyright (C) 2026 AnKenK
- v0.1.0 及更早版本以 MIT 发布，那份授权对已获得副本的人继续有效。
- 发布的二进制对应源码即本仓库同 tag 的代码。
