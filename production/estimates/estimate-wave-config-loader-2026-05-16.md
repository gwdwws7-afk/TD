## Task Estimate: Wave config + loader

### Task Description
将当前硬编码波次逻辑改为数据驱动：使用外部配置定义每波敌人组成、生成间隔与奖励参数，并由运行时加载执行。

### Complexity Assessment
| Factor | Assessment | Notes |
|---|---|---|
| Systems affected | Wave Spawner, Enemy AI, Economy, UI/HUD | 波次与奖励联动 |
| Files likely modified | 4-7 | GameManager 拆分 + 数据定义 |
| New vs modification | 30/70 | 以改造现有逻辑为主 |
| Integration points | 4 | 敌人生成、波次推进、奖励、HUD |
| Test coverage | Medium | 需要波次顺序与边界验证 |
| Patterns available | Partial | 已有运行循环，可直接承接 |

### Effort Estimate
| Scenario | Days | Assumption |
|---|---|---|
| Optimistic | 1.0 | 数据模型简洁、无额外兼容问题 |
| Expected | 1.5 | 正常重构 + 基础校验与调试 |
| Pessimistic | 2.5 | 牵连 HUD 与结算逻辑返工 |

**Recommended budget: 1.5 days**

### Confidence: Medium
当前代码体量可控，但波次系统与经济奖励耦合，拆分时可能引入节奏回归问题。

### Risk Factors
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 配置字段不足导致二次改 schema | M | 0.5d | 先定义 MVP 字段 + 扩展字段预留 |
| 重构后波次节奏异常 | M | 0.5d | 加入波次日志与回放测试 |
| 配置错误导致运行时崩溃 | L | 0.5d | 加载校验 + 默认回退 |

### Dependencies
| Dependency | Status | Impact if Delayed |
|---|---|---|
| Enemy archetype 参数定义 | Partial | 敌人类型无法配置化 |
| HUD 波次显示接口 | Ready | 影响较小，可后补 |

### Suggested Breakdown
| # | Sub-task | Days |
|---|---|---|
| 1 | 设计 wave schema + 示例配置 | 0.25 |
| 2 | 实现配置加载与校验 | 0.5 |
| 3 | 替换现有刷怪循环 | 0.5 |
| 4 | 联调奖励与HUD | 0.15 |
| 5 | 回归测试与修正 | 0.1 |
| | **Total** | **1.5** |

### Notes
- 先支持单地图单难度，避免过早做多模式。
- 配置文件建议放在 `Assets/Resources/Data/waves/`，便于快速加载。
