# Wave Schema v1 (Emberline Defense)

> **Date**: 2026-05-16  
> **Status**: Draft Ready for Implementation  
> **Scope**: M1 Vertical Slice (Grayline Junction-01, 10-wave closed test -> 20-wave expansion)

## 1. Purpose
将波次设计从硬编码迁移为数据驱动配置，确保：
1. 波次教学目标可追踪
2. 敌群组合可快速调参
3. 程序读取与校验有统一字段

## 2. File Locations
- Schema spec: `design/spec/wave-schema-v1.md`
- Runtime sample config: `Assets/Resources/Data/waves/grayline_junction01_m1_v1.json`

## 3. Top-Level Structure
```json
{
  "schemaVersion": "wave-schema-v1",
  "waveSetId": "grayline_junction01_m1_v1",
  "mapId": "grayline_junction01",
  "displayName": "Grayline Junction-01 (M1 10-wave test)",
  "globalDefaults": { ... },
  "enemyCatalog": [ ... ],
  "waves": [ ... ]
}
```

## 4. Field Definitions
### 4.1 Root
| Field | Type | Required | Description |
|---|---|---|---|
| `schemaVersion` | string | Yes | 固定为 `wave-schema-v1` |
| `waveSetId` | string | Yes | 波次配置唯一ID |
| `mapId` | string | Yes | 地图ID |
| `displayName` | string | Yes | 设计/调试可读名称 |
| `globalDefaults` | object | Yes | 波次默认参数 |
| `enemyCatalog` | array | Yes | 敌人参数与成本索引 |
| `waves` | array | Yes | 波次列表（按 waveIndex 升序） |

### 4.2 globalDefaults
| Field | Type | Required | Description |
|---|---|---|---|
| `prepSeconds` | number | Yes | 默认建造窗口时长 |
| `baseRewardGold` | number | Yes | 默认结算奖励 |
| `spawnMinSpacing` | number | Yes | 组内最小刷新间隔 |
| `lineDamageDefault` | integer | Yes | 默认漏防伤害 |
| `maxConcurrentEnemiesHint` | integer | No | 设计目标并发上限（调优提示） |

### 4.3 enemyCatalog item
| Field | Type | Required | Description |
|---|---|---|---|
| `enemyId` | string | Yes | 敌人ID（需与程序敌人原型映射） |
| `displayName` | string | Yes | 可读名称 |
| `hp` | integer | Yes | 生命 |
| `speed` | number | Yes | 移速（格/秒） |
| `armorFlat` | integer | Yes | 固定护甲 |
| `rewardGold` | integer | Yes | 击杀奖励 |
| `lineDamage` | integer | No | 漏防伤害（缺省用 globalDefaults） |
| `threatCost` | number | Yes | 波次预算成本 |
| `tags` | string[] | Yes | 分类标签（fast/heavy/swarm/armored） |

### 4.4 wave item
| Field | Type | Required | Description |
|---|---|---|---|
| `waveIndex` | integer | Yes | 波次序号（从1开始） |
| `phase` | string | Yes | `introduce` / `reinforce` / `exam` / `boss` |
| `goalTag` | string | Yes | 教学目标标签（coverage_gap等） |
| `threatTags` | string[] | Yes | 本波主要威胁标签 |
| `prepSeconds` | number | No | 覆盖默认建造时长 |
| `rewardGold` | integer | No | 覆盖默认结算奖励 |
| `budgetTarget` | number | Yes | 波次预算目标 |
| `budgetTolerance` | number | Yes | 预算容差（0.9~1.1） |
| `hint` | string | No | UI提示文案（可本地化键） |
| `groups` | array | Yes | 刷怪组列表 |

### 4.5 wave.groups item
| Field | Type | Required | Description |
|---|---|---|---|
| `enemyId` | string | Yes | 敌人ID |
| `count` | integer | Yes | 数量 |
| `startDelay` | number | Yes | 波开始后延迟生成时间 |
| `spawnInterval` | number | Yes | 该组生成间隔 |
| `formation` | string | No | `stream` / `burst` / `pack` |
| `lane` | string | No | 路线标识（多路线预留） |

## 5. Validation Rules (Must)
1. `waveIndex` 连续且唯一。
2. 所有 `enemyId` 必须存在于 `enemyCatalog`。
3. `spawnInterval >= globalDefaults.spawnMinSpacing`。
4. 单波仅允许一个“新机制引入敌”首次出现（对应教学原则）。
5. `sum(groupCount * threatCost)` 必须满足：
   - `<= budgetTarget * budgetTolerance`
   - `>= budgetTarget * (2 - budgetTolerance)`（等价下界）
6. `rewardGold >= 0` 且建议随波次缓增。
7. `groups` 不能为空。

## 6. Runtime Mapping
1. 加载 `waveSet` -> 构建 `enemyCatalog` 字典。
2. 每波按 `groups.startDelay` 排序生成计划。
3. 每组按 `spawnInterval` 逐个生成。
4. 波次结束判定：所有计划生成完成 + 场上敌人清空。
5. 结算：发放 `rewardGold`，记录 `goalTag` 与失败标签。

## 7. Closed-Test Scope
v1 先用于 10 波封闭测试，验证：
1. 配置可读可改
2. 波次目标可被玩家识别
3. 失败标签与波次目标有对应关系

20 波扩展在 v1.1 执行，不变更 schema，仅补充内容数据。
