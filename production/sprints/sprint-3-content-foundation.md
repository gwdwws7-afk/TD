## Sprint 3: Content Foundation (Tower / Enemy / Map)
Generated: 2026-05-19
Milestone: M2 20-Level Campaign

### Objective
把“20关内容支撑”从规划推进到可生产状态，形成塔、怪、地图三条并行资产流水线。

### Input Docs
- `design/gdd/full-project-plan-emberline-defense-v2.0.md`
- `design/gdd/content-matrix-20-level-v1.md`

### Deliverables
| Deliverable | Domain | Priority | Points |
|---|---|---|---|
| 8塔数据模板（含升级分支字段） | Tower Content | P1 | 5 |
| 12敌数据模板（含行为标签字段） | Enemy Content | P1 | 5 |
| 5图关卡模板（每图4关骨架） | Map/Level Content | P1 | 8 |
| 关卡1-20骨架配置清单 | Wave/Level | P1 | 8 |
| 内容资产命名规范与导入清单 | Pipeline | P2 | 3 |

### Tasks
| # | Task | Owner | Days | Dependencies |
|---|---|---|---|---|
| 1 | 建立塔内容表（8塔、分支、解锁章） | User+Codex | 0.5 | None |
| 2 | 建立敌人内容表（12敌、行为、反制） | User+Codex | 0.5 | None |
| 3 | 建立地图内容表（5图、战术关键词） | User+Codex | 0.5 | None |
| 4 | 输出关卡1-20骨架（地图+新增内容+考试点） | Codex | 1.0 | 1,2,3 |
| 5 | 设计 JSON 字段规范并对接现有配置流 | Codex | 1.0 | 4 |
| 6 | 完成 Chapter A/B（1-10关）首版内容配置 | User+Codex | 1.5 | 5 |
| 7 | 完成 Chapter C/D（11-20关）首版内容配置 | User+Codex | 1.5 | 5 |
| 8 | 执行一次内容一致性审计（塔/怪/图覆盖率） | Codex | 0.5 | 6,7 |

### Exit Criteria
1. 20关都能映射到地图、塔、敌内容矩阵（无空洞关卡）。
2. 所有新塔/新敌均有明确首次登场关与反制定位。
3. 每个章节至少 1 个明确考试关，考点可解释。
4. 内容资产命名可直接用于批量导入与自动校验。
