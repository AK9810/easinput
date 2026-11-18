# C#/.NET 8 + WPF，逻辑层与渲染层分离

**Status**: accepted (2026-11-17)

候选是 C#/WPF（单文件自包含约 35–70MB）、C++/Direct2D（约 1MB）、Rust（约 2MB）。选 C#/WPF：本项目的风险在视觉打磨与长尾应用兼容，不在性能或体积；P/Invoke 面只有状态读取、锚点、窗口样式三组；WPF 的矢量渲染、圆角、动画、Per-Monitor DPI 足够做 macOS 质感；.NET SDK 的安装成本远低于 VS Build Tools。

**Consequences**:
- 产物体积 35–70MB，对一个托盘小工具偏大，这是明确接受的代价。
- 逻辑层（状态解码、锚点降级、DPI 换算、配置）必须独立成库、不依赖 WPF，这样将来若为体积重写渲染层，核心不用重写。
- WPF 的 `AllowsTransparency` 走软件渲染；对 30×23 的窗口没有影响。
- 零第三方运行时依赖，测试只用 xUnit。
