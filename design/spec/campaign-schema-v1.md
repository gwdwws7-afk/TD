# Campaign Schema v1 (Emberline Defense)

> **Date**: 2026-05-19  
> **Status**: Draft Ready for Implementation  
> **Scope**: 20-level campaign structure (5 maps / 4 chapters), compatible with `wave-schema-v1`

## 1. Purpose
将“单波次配置”扩展为“战役级内容编排”，用于表达：
1. 章节与关卡顺序
2. 关卡到地图的映射
3. 塔/敌解锁节奏
4. 每关引用的波次数据集

## 2. File Locations
- Schema spec: `design/spec/campaign-schema-v1.md`
- Suggested runtime path: `Assets/Resources/Data/campaign/campaign_main_v1.json`
- Referenced wave data: `Assets/Resources/Data/waves/*.json`

## 3. Top-Level Structure
```json
{
  "schemaVersion": "campaign-schema-v1",
  "campaignId": "emberline_campaign_main_v1",
  "displayName": "Emberline Defense Campaign",
  "totalLevels": 20,
  "chapters": [ ... ],
  "maps": [ ... ],
  "levels": [ ... ],
  "globalRules": { ... }
}
```

## 4. Field Definitions
### 4.1 Root
| Field | Type | Required | Description |
|---|---|---|---|
| `schemaVersion` | string | Yes | 固定为 `campaign-schema-v1` |
| `campaignId` | string | Yes | 战役唯一ID |
| `displayName` | string | Yes | 战役显示名 |
| `totalLevels` | integer | Yes | 关卡总数（当前固定 20） |
| `chapters` | array | Yes | 章节定义列表 |
| `maps` | array | Yes | 地图定义列表 |
| `levels` | array | Yes | 关卡列表（按 levelIndex 升序） |
| `globalRules` | object | No | 全局规则（可选） |

### 4.2 chapters item
| Field | Type | Required | Description |
|---|---|---|---|
| `chapterId` | string | Yes | 章节ID（如 `chapter_a`） |
| `displayName` | string | Yes | 章节名 |
| `startLevel` | integer | Yes | 起始关卡序号 |
| `endLevel` | integer | Yes | 结束关卡序号 |
| `themeTags` | string[] | No | 章节主题标签 |

### 4.3 maps item
| Field | Type | Required | Description |
|---|---|---|---|
| `mapId` | string | Yes | 地图ID（如 `grayline_junction`） |
| `displayName` | string | Yes | 地图名 |
| `sceneKey` | string | Yes | 运行时场景或地图资源键 |
| `tacticalHook` | string | No | 一句话战术关键词 |

### 4.4 levels item
| Field | Type | Required | Description |
|---|---|---|---|
| `levelIndex` | integer | Yes | 关卡序号（1..20） |
| `levelId` | string | Yes | 关卡ID |
| `chapterId` | string | Yes | 所属章节ID |
| `mapId` | string | Yes | 绑定地图ID |
| `waveSetId` | string | Yes | 引用的波次数据集ID |
| `goalTags` | string[] | Yes | 本关考点标签 |
| `newTowerUnlocks` | string[] | No | 本关新增塔ID |
| `newEnemyUnlocks` | string[] | No | 本关新增敌ID |
| `recommendedPower` | number | No | 调试/平衡参考值 |
| `bossLevel` | boolean | No | 是否Boss关 |

### 4.5 globalRules
| Field | Type | Required | Description |
|---|---|---|---|
| `maxFailureReasonsShown` | integer | No | 失败标签展示上限（默认3） |
| `resonanceEnabledFromLevel` | integer | No | 共振系统启用关卡（如16） |
| `allowEarlyWaveDispatch` | boolean | No | 是否允许手动提前开波 |

## 5. Validation Rules (Must)
1. `totalLevels` 必须等于 `levels.length`。
2. `levelIndex` 必须连续且唯一（1..`totalLevels`）。
3. 每个 `level.chapterId` 必须存在于 `chapters`。
4. 每个 `level.mapId` 必须存在于 `maps`。
5. 每个 `chapter` 的 `startLevel/endLevel` 区间不得重叠且必须覆盖全部关卡。
6. 每个 `mapId` 在 20 关版本中应至少出现 4 次（内容复用约束）。
7. `waveSetId` 必须存在对应 `wave-schema-v1` 数据文件。
8. `newTowerUnlocks/newEnemyUnlocks` 不得包含重复ID。

## 6. Runtime Mapping
1. 启动时加载 Campaign -> 构建 `levelIndex -> levelConfig` 快速索引。
2. 进入关卡时，根据 `level.waveSetId` 加载对应 `wave-schema-v1` 数据。
3. 通关后按 `newTowerUnlocks/newEnemyUnlocks` 更新局外解锁态。
4. 结算页根据 `goalTags` 与失败标签输出关卡复盘摘要。

## 7. Example (Truncated)
```json
{
  "schemaVersion": "campaign-schema-v1",
  "campaignId": "emberline_campaign_main_v1",
  "displayName": "Emberline Defense Campaign",
  "totalLevels": 20,
  "chapters": [
    { "chapterId": "chapter_a", "displayName": "Chapter A", "startLevel": 1, "endLevel": 5 },
    { "chapterId": "chapter_b", "displayName": "Chapter B", "startLevel": 6, "endLevel": 10 }
  ],
  "maps": [
    { "mapId": "grayline_junction", "displayName": "Grayline Junction", "sceneKey": "map_grayline_junction" },
    { "mapId": "ashfall_depot", "displayName": "Ashfall Depot", "sceneKey": "map_ashfall_depot" }
  ],
  "levels": [
    {
      "levelIndex": 1,
      "levelId": "level_01",
      "chapterId": "chapter_a",
      "mapId": "grayline_junction",
      "waveSetId": "grayline_junction_l01_v1",
      "goalTags": ["baseline_path_read"],
      "newTowerUnlocks": ["rail_lancer_tower"],
      "newEnemyUnlocks": ["skitter_runner"]
    }
  ],
  "globalRules": {
    "maxFailureReasonsShown": 3,
    "resonanceEnabledFromLevel": 16,
    "allowEarlyWaveDispatch": true
  }
}
```

## 8. Compatibility Notes
1. `campaign-schema-v1` 不替代 `wave-schema-v1`，而是上层编排。
2. 关卡级难度由 `waveSetId` 指向的波次文件具体定义。
3. 现有 20-wave 单地图配置可作为 `level_01` 到 `level_04` 的临时过渡数据源。
