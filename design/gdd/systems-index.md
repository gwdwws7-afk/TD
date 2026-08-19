## Systems Index: Emberline Defense (M2-Aligned)

> **Date**: 2026-05-19  
> **Status**: Active  
> **Source of Truth**: `design/gdd/full-project-plan-emberline-defense-v2.0.md`

### Enumeration
| System | Category | Description | Explicit? |
|---|---|---|---|
| Grid & Path | Foundation | 网格、路径、可建造区定义 | Yes |
| Level/Campaign Framework | Foundation | 章节、关卡、地图编排与解锁 | Yes |
| Wave Spawner | Core | 波次调度、敌人出场编排 | Yes |
| Enemy AI | Core | 路径移动、抗性、特性行为 | Yes |
| Tower Placement | Core | 放置合法性、占位、建造成本 | Yes |
| Tower Combat | Core | 索敌、攻击、投射物、伤害结算 | Yes |
| Resonance Combat | Core | 共振窗口与战术命令系统 | Yes |
| Economy | Core | 金币来源与消耗、奖励节奏 | Yes |
| Tower Upgrade | Feature | 塔成长分支与数值强化 | Yes |
| Status Effects | Feature | 减速、灼烧、破甲等效果 | Yes |
| Wave Grammar | Feature | 每波教学目标与敌群结构 | Yes |
| UI/HUD | Presentation | 预算、完整度、波次、共振、提示 | Yes |
| VFX/SFX | Presentation | 命中反馈、死亡反馈、警报 | Yes |
| Analytics/Telemetry | Production | 失败原因、波次统计、运行摘要 | Yes |
| Content Matrix & Pipeline | Production | 塔/敌/图资产与配置生产流水线 | Yes |
| Meta Progression | Post-Launch | 局外解锁与长期成长 | No |

### Dependency Map
| System | Depends On | Layer |
|---|---|---|
| Grid & Path | - | Foundation |
| Level/Campaign Framework | Grid & Path | Foundation |
| Economy | - | Foundation |
| Tower Placement | Grid & Path, Economy | Core |
| Wave Spawner | Grid & Path, Level/Campaign Framework | Core |
| Enemy AI | Grid & Path | Core |
| Tower Combat | Enemy AI, Tower Placement | Core |
| Resonance Combat | Tower Combat, Wave Grammar, UI/HUD | Core |
| Tower Upgrade | Economy, Tower Combat | Feature |
| Status Effects | Tower Combat, Enemy AI | Feature |
| Wave Grammar | Wave Spawner, Enemy AI, Tower Combat | Feature |
| UI/HUD | Economy, Wave Spawner, Enemy AI, Resonance Combat | Presentation |
| VFX/SFX | Tower Combat, Enemy AI, Resonance Combat | Presentation |
| Analytics/Telemetry | UI/HUD, Wave Grammar, Tower Combat | Production |
| Content Matrix & Pipeline | Level/Campaign Framework, Wave Grammar | Production |
| Meta Progression | Economy, Tower Upgrade | Post-Launch |

### Priority Tiers
| Tier | Systems |
|---|---|
| M1 Vertical Slice Core | Grid & Path, Economy, Tower Placement, Wave Spawner, Enemy AI, Tower Combat, UI/HUD |
| M2 Campaign Core | Level/Campaign Framework, Tower Upgrade, Wave Grammar, Resonance Combat, Analytics/Telemetry, Content Matrix & Pipeline |
| Post-M2 Expansion | Status Effects (advanced), VFX/SFX polish, Meta Progression |

### Progress Tracker
| System | Status | Notes | Doc Path |
|---|---|---|---|
| Grid & Path | Implemented | 运行时可用 | Pending |
| Level/Campaign Framework | Implemented (Phase2 Baseline) | Campaign schema + level routing + per-map path routing + waveSet mapping 已接入 | design/spec/campaign-schema-v1.md |
| Wave Spawner | Implemented | Config-driven，20-level x per-level waveSet baseline 已跑通 | design/spec/wave-schema-v1.md |
| Enemy AI | Implemented (Behavior Pass 1) | 12 敌接入 + 首轮行为差异（突袭冲刺/拟态变体/分裂/支援减伤/渗漏惩罚）已落地，待平衡 | design/gdd/enemy-ai-system.md |
| Tower Placement | Implemented | 可建造检测与占位可用 | Pending |
| Tower Combat | Implemented (M2 Feature Pass 1) | 8 塔战斗链路可用，新增5塔专属机制已接入（连锁/破甲/压制/标记/重力场），待平衡 | design/gdd/tower-combat-system.md |
| Resonance Combat | Implemented (MVP) | 共振充能/窗口/命令2选1已接入，待平衡迭代 | design/gdd/full-project-plan-emberline-defense-v2.0.md |
| Economy | Implemented | 基础预算与奖励可用 | Pending |
| Tower Upgrade | Implemented (M2 Baseline) | 8 塔均支持 3 阶升级与 2 分支 | design/gdd/tower-upgrade-system.md |
| Status Effects | Implemented | 减速/破甲/暴露/标记/压制/硬直全接入（2026-08-19 更正：原"破甲未接入"记录过时，P11-P13 已落地） | design/gdd/tower-combat-system.md |
| Wave Grammar | Implemented (Baseline) | 20-wave 教学节奏已配置，20关语法待展开 | design/gdd/wave-grammar-system.md |
| UI/HUD | Implemented (Baseline) | 基础HUD与结算摘要已接入 | Pending |
| VFX/SFX | Implemented | 62 条 WAV + 程序化合成兜底 + FX 预算 32 上限（P13.4 落地，2026-08-19 更正） | design/gdd/p13.4-audio-visual-input-feel.md |
| Analytics/Telemetry | Implemented (Baseline) | 失败标签与 run summary 已接入 | design/gdd/tower-combat-system.md |
| Content Matrix & Pipeline | Partial | 8塔/12敌/5图数据骨架已接入运行时，资产生产与精修待推进 | design/gdd/content-matrix-20-level-v1.md |
| Meta Progression | Partial | P10.1 战术协议 + 图鉴奖励已上线；长期成长线未启动（2026-08-19 更正） | design/gdd/p10-campaign-meta-loop.md |
