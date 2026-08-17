# 发布出包路径 Checklist(2026-08-17 QA 建立)

## 结论(任务 4 验证,commit 590ec01 状态)

| 出包路径 | 用途 | 验证状态 |
|---|---|---|
| `TDReleaseBuilder` 默认(automation=true,TD_AUTOMATION 开) | **CI 冒烟门禁** | 代码会话已验证(符号在包内、门禁可用);QA 无需复跑 |
| `TDReleaseBuilder` automation=false | **商店纯包** | ✅ QA 2026-08-17 实测通过(见下) |

## 纯包验证记录(builds/qa-pure-20260817)

- 构建:`TD.Editor.TDReleaseBuilder.BuildWindowsForMcp(..., automation: false)`,Windows x64 / Mono / Medium stripping / 非开发;45s、161MB、0 错误(`builds/qa-pure-20260817.build.json`,buildGuid 26312ee8)
- 符号抽查(`tools/qa_pure_package_audit.py`,51 个程序集字节扫描):
  - 自动化符号 8/8 **全部缺失**(Debug* 探针 API、P1254 soak、standalone smoke probe)✓
  - 玩法符号 7/7 **全部在包**(GamepadCursor、TrySellTower、SellRefundValue、TDCombatMath、ResolveArmoredDamage、TDRadialTowerMenu)✓
- 复验命令:`python tools/qa_pure_package_audit.py builds/<buildName>`

## 出包操作

- 商店包:菜单 `TD/Build/Windows x64 Baseline` 的批处理等价 `-tdAutomation false -tdOutput builds/<name>/EmberlineDefense.exe`
- CI 包:同路径 `-tdAutomation true`(默认);CI 冒烟需要包内探针,勿关
- MCP 通道:反射调 `TD.Editor.TDReleaseBuilder.BuildWindowsForMcp`(execute_code 编译域引用不到 Editor 程序集,须反射)

## 复用资产(任务 6 收尾,2026-08-17)

- 手柄验收:`python tools/qa_gamepad_acceptance.py [--skip-phase-b|--phase-b-only]`(InputSystem 原生模拟;发现单见 design/spec/qa-findings-gamepad-2026-08-17.md)
- B103/开火帧 runbook:`tools/qa-runbook-b103-fireframes.md`,结果 `design/spec/qa-b103-fireframes-result.md`
- 全量回归三件套:EditMode(MCP run_tests)→ 编辑器局(td_mcp_playtest.ps1 L1 ts5×40s)→ standalone 局(默认包构建 + `--td-smoke-test` 启动)→ 25 局平衡(td_raillancer_balance_regression.ps1 -Force 分离进程);汇总 `design/spec/qa-regression-2026-08-17.md`
- 编辑器玩家循环冻结(测试后 frameCount=1)恢复法:重跑一次 EditMode → stop → play
