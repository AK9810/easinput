# 自研核心，不基于现有输入法提示工具

**Status**: accepted (2026-11-17)

市面上已有 ImTip（MIT，aardio）与 InputTip（AGPL-3.0，AutoHotkey v2）实现了"插入符旁显示中/英"，但都不作为本项目的底座，只作为技法参考。

ImTip 最接近需求，但 aardio 工具链小众、IDE 专有，长期维护与二次开发被生态锁死。InputTip 的 caret 链最完整，但 AGPL-3.0 会传染整个衍生项目；且其 GitHub 仓库与作者账号在 2026-11-16 查证时已 404，只剩 Gitee 镜像，供应链风险不可接受。两者都无法满足"MIT、单文件便携、可长期维护"的定位。

**Consequences**: 长尾应用（Office、Terminal、JetBrains、UWP）的兼容问题由我们自己承担，Phase 1 的验收范围因此限定在 Chromium 系与经典 Win32。我们直接采用已在本机验证过的技法（见 ADR-0002、ADR-0003），不重新发明。
