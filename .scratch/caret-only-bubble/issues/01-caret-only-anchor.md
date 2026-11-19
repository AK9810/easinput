# 01: 锚点链减为三级——纯插入符语义

**What to build:** 依据本目录 spec.md：气泡只在锚点解析到真实插入符时显示。
删掉指针级（指针定位器、锚点层级枚举中的 Pointer 值、解析器的第三注入点），
降级链变为 Win32 插入符 → UIA 插入符 → 隐藏。桌面、游戏、只读网页、
Qt/Java、远程桌面等探测不到插入符的场景彻底静默。触发机制与配置面均不动。

**Blocked by:** None（can start immediately）

**Status:** ready-for-agent

- [ ] 降级链代码中不再存在指针级：定位器、枚举值、第三注入点删净，构建 0 警告
- [ ] 防回归单测：两个插入符定位器都失败时，解析结果为 Hidden
- [ ] 既有优先级用例改写为双源（Win32 胜 UIA；Win32 失败落 UIA），全测试套件绿
- [ ] `--probe` 实测：Win32 EDIT → Caret；VS Code / Edge 地址栏 → Uia；桌面 → hidden
- [ ] 实机：桌面按 CapsLock、点击非文本区域切换前台，均不再弹泡
- [ ] CONTEXT.md：删「指针」词条，「锚点降级」改写为三级
- [ ] ADR-0003 修订：降级链三级，记录 macOS 在同场景同样弹于光标处且被用户长期抱怨的理由
- [ ] acceptance.md：ZCode / 微信自身输入框 / 只读网页各行预期更新为 hidden
