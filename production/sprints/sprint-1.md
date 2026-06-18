## Sprint 1: Core Loop Stabilization
Generated: 2026-05-16
Milestone: M1 Vertical Slice

### Goals
1. 把当前原型升级为“可调参数驱动”的稳定核心循环
2. 从硬编码波次过渡到配置化波次
3. 完成 image2.0 第一套正式素材接入

### Deliverables
| Deliverable | System | Priority | Points |
|---|---|---|---|
| Wave config data + loader | Wave Spawner | P1 | 5 |
| 3 towers + 4 enemies baseline params | Tower Combat / Enemy AI | P1 | 8 |
| Basic HUD polish + leak reason hint | UI/HUD | P1 | 5 |
| Art pack v1 generated and imported | Art Pipeline | P2 | 3 |
| Balance sheet + first tuning pass | Economy/Combat | P2 | 3 |

### Tasks
| # | Task | Owner | Days | Dependencies |
|---|---|---|---|---|
| 1 | 定义 wave JSON schema | Codex | 0.5 | None |
| 2 | 实现 wave loader 与运行时生成 | Codex | 1.5 | 1 |
| 3 | 抽离塔/敌人参数为 data asset | Codex | 1.5 | 2 |
| 4 | 接入 3塔4敌并做首轮平衡 | User+Codex | 1.5 | 3 |
| 5 | 生成并导入 image2.0 资源 v1 | User+Codex | 0.5 | None |
| 6 | HUD增强与提示文本 | Codex | 1.0 | 2 |
| 7 | Playtest + bugfix + tuning | User+Codex | 1.0 | 4,6 |

### Capacity
- Available: 8.0 days
- Planned: 7.5 days
- Buffer: 0.5 days

### Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 参数拆分过慢影响节奏 | M | H | 先做最小字段集，二期再扩展 |
| 资源生成质量波动 | M | M | 小批次生成+快速回看 |
| 难度曲线前期过陡 | M | M | 首轮只锁 10 波，逐波调参 |

### Sprint Commitment
**Committed**: Wave config、3塔4敌、HUD可读性、美术v1接入  
**Stretch**: 结算页统计与简单排行榜占位
