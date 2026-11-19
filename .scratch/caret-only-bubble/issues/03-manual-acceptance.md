# 03: 纯插入符语义人工回归验收

**What to build:** 依据本目录 spec.md：在 01+02 落地后，对完整验收矩阵做一次
真人复测，并把**结果**记入验收记录。两条线：既有插入符应用不回退；
无插入符场景确认静默。需真人操作——合成输入会被托盘 tooltip 挡住，
ZCode / 微信自身输入框只能真人验证。

**Blocked by:** 01, 02

**Status:** ready-for-human

- [ ] 既有插入符应用逐项通过且气泡位置正常：记事本、资源管理器重命名、Edge 地址栏、VS Code、Terminal、Word、ZCode 输入框（含空输入框）、经典 Win32 EDIT
- [ ] 无插入符场景逐项确认静默：桌面、只读网页、微信 4.x（Weixin.exe）、微信输入法自身输入框
- [ ] 同模式切到另一个有插入符的窗口（如 VS Code ↔ ZCode / Terminal）会弹；同一窗口内模式未变的轮询不重复弹
- [ ] 托盘菜单逐项点击正常（少了「预览效果」后的完整菜单）；首次运行 balloon 仍可见
- [ ] 全部结果（含日期）记入 `.scratch/easinput/acceptance.md`

## Comments

- 2026-11-19 已解除阻塞：ticket 01/02 均已在 `feat/caret-only-bubble` 落地（合并 `bc30295`、`52ac745`；review 修复 `11d344d`、`e394f12`）。
- 自动侧已覆盖：`--probe` 的 Win32 EDIT → `Caret`、Edge 地址栏 → `Uia`、桌面 → `Hidden`、ZCode → `Hidden`；`--preview` 已被忽略。验收矩阵中的隐藏预期已写入 `.scratch/easinput/acceptance.md`。
- 本票全部条目仍需真人操作与记录，Status 保持 `ready-for-human`。
- 2026-11-19 修订（见 ticket 04）：VS Code / Edge 地址栏 / ZCode 输入框现在走 `Msaa`；ZCode 空输入框也能解析插入符，
  已从「无插入符场景」移到「既有插入符应用」。`--probe` 自动证据：
  VS Code `Msaa (1140,493)-(1141,520) 1x27`、ZCode `Msaa (891,1039)-(892,1065) 1x26`、
  Win32 EDIT `Caret 1x16`、Edge 只读页 `Hidden`。
- 2026-11-19 修订（见 ticket 05）：触发语义扩展为「焦点进入另一个有插入符的窗口也弹」，
  同模式切窗不再静默；锚点隐藏时重试 3 次。微信 4.x 实测三条插入符路径全空，
  按用户决定不兼容，验收预期改为静默。
