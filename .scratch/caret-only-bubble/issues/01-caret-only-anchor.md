# 01: 锚点链减为三级——纯插入符语义

**What to build:** 依据本目录 spec.md：气泡只在锚点解析到真实插入符时显示。
删掉指针级（指针定位器、锚点层级枚举中的 Pointer 值、解析器的第三注入点），
降级链变为 Win32 插入符 → UIA 插入符 → 隐藏。桌面、游戏、只读网页、
Qt/Java、远程桌面等探测不到插入符的场景彻底静默。触发机制与配置面均不动。

**Blocked by:** None（can start immediately）

**Status:** resolved

- [x] 降级链代码中不再存在指针级：定位器、枚举值、第三注入点删净，构建 0 警告
- [x] 防回归单测：两个插入符定位器都失败时，解析结果为 Hidden
- [x] 既有优先级用例改写为双源（Win32 胜 UIA；Win32 失败落 UIA），全测试套件绿
- [ ] `--probe` 实测：Win32 EDIT → Caret；VS Code / Edge 地址栏 → Uia；桌面 → hidden（VS Code 未运行，其余已实测）
- [ ] 实机：桌面按 CapsLock、点击非文本区域切换前台，均不再弹泡（留 ticket 03 人工）
- [x] CONTEXT.md：删「指针」词条，「锚点降级」改写为三级
- [x] ADR-0003 修订：降级链三级，记录 macOS 在同场景同样弹于鼠标指针处且被用户长期抱怨的理由
- [x] acceptance.md：ZCode / 微信自身输入框 / 只读网页各行预期更新为 hidden

## Comments

- 2026-11-19 实现完成：集成分支 `feat/caret-only-bubble`（ticket 合并 `bc30295`；review 修复 `11d344d`、`e394f12`）。
- 代码：`PointerLocator`、`AnchorTier.Pointer`、第三注入点、仅指针级使用的 `GetCursorPos` 与 `PixelRect.Contains` 全部删除；`AnchorResolver` 现在要求两个定位器。构建 0 警告；60 测试绿（双源优先级 + 双失败 → Hidden）。
- `--probe` 实测：Win32 EDIT harness → `Caret`；Edge 地址栏 → `Uia`；桌面（Progman）→ `Hidden`；ZCode 前台 → `Hidden`。
- 未验证：VS Code probe（当时未运行）；桌面 CapsLock / 点击非文本区域切前台的实机弹泡行为——均留 ticket 03 人工回归。
