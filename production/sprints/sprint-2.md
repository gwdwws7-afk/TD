## Sprint 2: Vertical Slice Completion
Generated: 2026-05-19
Milestone: M1 Vertical Slice

### Related Design Docs
- `design/gdd/full-project-plan-emberline-defense-v1.1.md`
- `design/gdd/wave-grammar-system.md`
- `design/gdd/tower-combat-system.md`
- `design/gdd/enemy-ai-system.md`
- `design/gdd/tower-upgrade-system.md`
- `design/spec/wave-schema-v1.md`

### Planning Baseline (2026-05-19)
1. 10波配置化流程已跑通（JSON加载、波次循环、奖励结算、基础HUD）。
2. 三塔四敌、双分支升级、AOE与减速已具备可玩原型。
3. 主要缺口是：20波扩展、可观测指标（失败标签/通过率）、系统与GDD规则对齐、首轮平衡闭环。

### Goals
1. 从10波扩展到20波，完整落地 `Introduce -> Reinforce -> Exam` 教学节奏。
2. 建立最小可用观测层：波次通过率、失败原因标签、构筑失效信号。
3. 对齐关键设计规则，减少“实现偏离文档”的风险。
4. 完成 M1 冻结前的首轮平衡与稳定性验收。

### Deliverables
| Deliverable | System | Priority | Points |
|---|---|---|---|
| 20-wave config v1.1 + schema check | Wave Spawner / Wave Grammar | P1 | 8 |
| 失败原因标签 + 波次统计输出 | Analytics/Telemetry / UI-HUD | P1 | 5 |
| 建造/升级窗口规则对齐与提示 | Tower Upgrade / Core Loop | P1 | 3 |
| 3塔4敌首轮平衡报告（含反制链验证） | Tower Combat / Enemy AI / Economy | P1 | 5 |
| 高压波反馈增强（VFX微调 + SFX挂点） | Presentation | P2 | 3 |
| 30分钟稳定性与性能回归清单 | Runtime Core | P1 | 3 |

### Tasks
| # | Task | Owner | Days | Dependencies |
|---|---|---|---|---|
| 1 | 定义20波蓝图（每波 goalTag / threatTags / pressure target） | User+Codex | 1.0 | None |
| 2 | 扩展 `grayline_junction01_m1_v1` 到20波并通过schema校验 | Codex | 1.0 | 1 |
| 3 | 增加运行时波次统计（到达波次、通过/失败、关键预算节点） | Codex | 1.0 | 2 |
| 4 | 实现失败原因标签规则（输出不足/覆盖断层/克制错误）与HUD展示 | Codex | 1.0 | 3 |
| 5 | 对齐升级规则（默认仅Prep可升级）并保留调试开关 | Codex | 0.5 | 2 |
| 6 | 首轮平衡：三塔基础参数、升级效率、敌人压力曲线调参 | User+Codex | 1.5 | 2,5 |
| 7 | Playtest矩阵（至少20局）并汇总通过率与失败分布 | User+Codex | 1.0 | 4,6 |
| 8 | 高压波可读性增强（命中/漏防反馈、警报挂点） | Codex | 0.5 | 4 |
| 9 | 30分钟稳定性回归（帧率、GC、阻断Bug） | User+Codex | 0.5 | 6,8 |

### Capacity
- Available: 8.5 days
- Planned: 8.0 days
- Buffer: 0.5 days

### Exit Criteria (Sprint 2)
1. 单地图20波可完整结算（胜负均可闭环）。
2. Wave 1-5 目标可解释，Wave 10/20 有明确考试波体验。
3. 失败后可输出不超过3类主因标签，且可映射到调参动作。
4. 不出现长期单塔统治（单塔伤害占比不稳定超过60%）。
5. 无S1/S2阻断问题，连续30分钟回归可完成。

### Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 20波后半段难度尖刺 | M | H | 每3波做一次压力审计，限制预算跃迁 |
| 标签判定失真导致误导 | M | M | 保留原始事件日志，标签规则迭代两轮 |
| 升级限制改变手感 | M | M | 提供调试开关，A/B测试两种节奏 |
| 平衡周期超时 | M | H | 先锁P1指标，P2视觉反馈延后 |

### Sprint Commitment
**Committed**: 20波扩展、失败标签与统计、规则对齐、首轮平衡闭环  
**Stretch**: 简版结算页（按波次展示失败主因Top3）
