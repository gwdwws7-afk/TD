# QA 全量回归结果(任务 3,plan v3)—— 门禁全过 + 1 项平衡发现

日期:2026-08-17 · HEAD 基线:6985596(+ 运行时含 TDTower/TowerPresentationProfile 未提交改动,设计会话进行中)

## 结果总览

| 项 | 结果 |
|---|---|
| EditMode 全量 | ✅ 30/30(0.72s,基线保持) |
| 编辑器完整对局(L1,ts5×40s) | ✅ 全检查绿、0 控制台问题、3835 帧正常推进 |
| release 构建一局(standalone 探针) | ✅ 默认包(154MB/50.6s/0 错,automation=true)+ `--td-smoke-test` L1 **victory=True、0 运行时错误** |
| 25 局平衡回归 | ✅ **门禁通过:RailLancer 单塔种清关 = 0**(套利排除)· 但胜率 25/25 → **14/25**(见下) |

## 平衡发现(报总导演分派,非 QA 修复项)

- 分布:L01 4/5、L05 5/5、L09 5/5(首漏波 15-17,基线为 0)、**L13 0/5(两局首漏=第 1 波)**、**L20 0/5**。
- 排除项:全部 standard 难度(非存档偏好污染);singleKind=0 且败局非预算异常 → **拆塔套利排除**(autoplay 无出售行为)。
- 根因:**混合护甲模型**(583d33e 流引入:每点甲额外 4% 减伤、上限 60%,叠加固定减免)在 8/15 25/25 基线**之后**落地——高甲敌人(L13 husk_titan 9 甲、L20 matriarch 12 甲)对低单发塔(RailLancer 等)伤害大幅下降。TDCombatMath 抽取与原内联公式逐字一致(非回归)。
- 结论:这是**预期的设计改动使 autoplay 的固定布点/策略失配**,需要设计侧决策:L13/L20 布点变体与专精组合重标定,或护甲参数微调。8/15 的"25/25 全胜"难度基线已失效。

## 运维备注(编辑器卡死事故与恢复)

- 症状:EditMode 测试运行后进入 play,isPlaying=true、isPaused=false、isCompiling=false,但 `Time.frameCount` 永远 =1(玩家循环冻结);pause/Step/frame-debugger/focus/domain-reload/stop-play 全无效。
- **恢复法:再跑一次 EditMode 测试(完整周期)→ stop → play → 帧恢复推进**。建议 MCP manage_editor play 前置检测 frameCount 静止并自动执行此恢复(任务 6 候选)。
- 本次 3-2 的两次"startWave/screenshot 失败"均为此冻结的时序副作用,恢复后同参数全绿。

## 证据

- `output/playtest/regression_editor_L1d.json`(编辑器局)
- `builds/qa-regression-default.build.json` + `output/playtest/p1253_smoke_regression.json`(standalone 局)
- `output/playtest/balance_regression/`(25 局全套:status.json / progress.log / balance_regression_report.md / run_*.p124.json)
