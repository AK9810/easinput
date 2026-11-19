# 02: 预览入口整体删除

**What to build:** 依据本目录 spec.md：删除「预览效果」功能的一切痕迹——
托盘菜单项、命令行 `--preview` 参数、控制器中的预览路径（含 80ms×8 重试机制
与仅预览使用的窗口归属判断）。首次运行 balloon 与预览无关，保留。

**Blocked by:** None（can start immediately；与 01 无文件冲突，可并行）

**Status:** resolved

- [x] 托盘菜单不再出现「预览效果」项
- [x] 以 `--preview` 启动时参数被忽略，正常进入常驻运行（不预览、不崩溃、不退出）
- [x] 预览路径及其专属辅助删净，无死代码，构建 0 警告
- [x] README 不再提及 `--preview` 或「预览效果」
- [ ] 首次运行 balloon 行为不变（首启可见、二次双击自退）（留 ticket 03 人工）

## Comments

- 2026-11-19 实现完成：集成分支 `feat/caret-only-bubble`（ticket 合并 `52ac745`；review 修复 `11d344d`、`e394f12`）。
- 删除：托盘「预览效果」项及其回调参数/`_foregroundBeforeMenu`、`BubbleController.Preview/ShowPreview/RetryPreview/IsOwnWindow`、命令行 `--preview` 与 4 秒自退、README 的预览说明。
- 冒烟：用 `--preview` 启动后进程越过旧 4 秒退出窗口仍存活，确认参数被忽略；随后已终止，无残留进程。构建 0 警告；60 测试绿。
- 首次运行与已在运行的 balloon 文案不再提「预览」，展示时机/时长/一次性逻辑未改；文案与视觉确认留 ticket 03 人工。
