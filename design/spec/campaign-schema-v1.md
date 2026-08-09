# Campaign Schema v1 (Emberline Defense)

> **Date**: 2026-05-19  
> **Status**: Implemented through P10.2
> **Scope**: 20-level campaign structure (5 maps / 4 chapters), compatible with `wave-schema-v1`

## 1. Purpose
将“单波次配置”扩展为“战役级内容编排”，用于表达：
1. 章节与关卡顺序
2. 关卡到地图的映射
3. 塔/敌解锁节奏
4. 每关引用的波次数据集
5. 每关可选契约与可见战场变体

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
  "difficultyTiers": [ ... ],
  "metaProgression": { ... },
  "chapters": [ ... ],
  "maps": [ ... ],
  "levels": [ ... ],
  "globalRules": { ... }
}
```

## 4. Field Definitions
### 4.1 Root

P8.5 adds a required root `difficultyTiers` array. Production data defines exactly three entries with unique contiguous tier indexes `0`, `1` and `2`.

| Field | Type | Required | Description |
|---|---|---|---|
| `difficultyTiers` | array | P8.5 Yes | Standard, Veteran and Ember Trial definitions. |
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

P8.4 adds a required `reward` object to each production chapter.
P8.5 also adds one required `challengeRemix` mutator object to each production chapter.

### 4.2.1 chapter reward
| Field | Type | Required | Description |
|---|---|---|---|
| `rewardId` | string | Yes | Campaign-unique portable reward ID. |
| `displayName` | string | Yes | Player-facing reward name. |
| `description` | string | Yes | Player-facing effect summary. |
| `startingBudgetBonus` | integer | No | Non-negative budget added to every deployment after claim. |
| `startingIntegrityBonus` | integer | No | Non-negative integrity added to every deployment after claim. |
| `resonanceGainMultiplier` | number | No | Positive resonance gain multiplier; 0 or 1 means neutral. |

### 4.2.2 difficulty tier
| Field | Type | Required | Description |
|---|---|---|---|
| `tier` | integer | Yes | Unique contiguous index: `0=Standard`, `1=Veteran`, `2=EmberTrial`. |
| `difficultyId` | string | Yes | Campaign-unique stable ID. |
| `displayName` | string | Yes | Player-facing tier name. |
| `description` | string | Yes | Player-facing unlock and pressure summary. |
| `modifiers` | mutator object | Tier 1-2 | Runtime modifier. Standard must have no effective modifier. |

### 4.3 maps item
| Field | Type | Required | Description |
|---|---|---|---|
| `mapId` | string | Yes | 地图ID（如 `grayline_junction`） |
| `displayName` | string | Yes | 地图名 |
| `sceneKey` | string | Yes | 运行时场景或地图资源键 |
| `tacticalHook` | string | No | 一句话战术关键词 |
| `mechanic` | object | P8.6 Yes | One unique decision-changing map mechanic. |

P8.6 `mechanic` fields are `mechanicId`, `displayName`, `description`, `commandLabel`, `mechanicType`, `maxCharges`, `budgetCost`, optional `reinforcementDelaySeconds`, optional `effectDurationSeconds`, and optional `bossPhaseThresholds`.

Supported `mechanicType` values are `signal_gate`, `timed_reinforcement`, `route_switch`, `environment_device`, and `boss_phase`. Production data uses each type exactly once across the five maps.

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
| `scenario` | object | P8.6 milestone exams | `milestoneExam`, `failureFocus`, and intensity 1-3. |
| `contract` | object | P8.2 Yes | 本关独立契约勋章目标 |
| `mutators` | array | P8.2 Yes | 本关可见规则变体，至少 1 个 |

### 4.5 contract
| Field | Type | Required | Description |
|---|---|---|---|
| `contractId` | string | Yes | 全战役唯一契约ID |
| `displayName` | string | Yes | 简报、HUD 与结算显示名 |
| `metric` | string | Yes | 契约读取的运行指标 |
| `comparison` | string | Yes | `at_least` 或 `at_most` |
| `target` | integer | Yes | 非负目标值 |

支持的 `metric`：`integrity`、`budget`、`escapes`、`tower_count`、`upgrades`、`tactical_score`、`counter_score`、`command_score`、`matrix_full_matches`、`convergence_triggers`。

### 4.6 mutators item
| Field | Type | Required | Description |
|---|---|---|---|
| `mutatorId` | string | Yes | 全战役唯一变体ID |
| `displayName` | string | Yes | 玩家可见名称 |
| `enemyHpMultiplier` | number | No | 敌人生命倍率，0 表示未配置/1.0 |
| `enemySpeedMultiplier` | number | No | 敌人速度倍率，0 表示未配置/1.0 |
| `enemyArmorBonus` | integer | No | 敌人平甲加值 |
| `startingBudgetDelta` | integer | No | 初始预算增减 |
| `startingIntegrityDelta` | integer | No | 初始防线完整度增减 |
| `rewardMultiplier` | number | No | 击杀赏金与波次奖励倍率 |
| `resonanceGainMultiplier` | number | No | 共鸣充能获取倍率 |

### 4.7 globalRules
| Field | Type | Required | Description |
|---|---|---|---|
| `maxFailureReasonsShown` | integer | No | 失败标签展示上限（默认3） |
| `resonanceEnabledFromLevel` | integer | No | 共振系统启用关卡（如16） |
| `startingBudgetPerLevel` | integer | No | L01 之后每关追加的初始预算（0..20） |
| `startingIntegrityPerChapter` | integer | No | 进入新章节时追加的防线完整度（0..10） |
| `towerPowerPerLevelPct` | number | No | L01 之后每关追加的塔伤百分比（0..3） |
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
9. 20 关正式战役每关必须有 1 个合法契约和至少 1 个非空变体效果。
10. `contractId` 与 `mutatorId` 必须全战役唯一。
11. 共鸣类契约不得早于 `resonanceEnabledFromLevel`。
12. 变体倍率和资源增减必须落在运行时校验范围内。

## 6. Runtime Mapping
1. 启动时加载 Campaign -> 构建 `levelIndex -> levelConfig` 快速索引。
2. 进入关卡时，根据 `level.waveSetId` 加载对应 `wave-schema-v1` 数据。
3. 通关后按 `newTowerUnlocks/newEnemyUnlocks` 更新局外解锁态。
4. 结算页根据 `goalTags` 与失败标签输出关卡复盘摘要。
5. 敌人变体作用于出生时克隆，不修改全局敌人图鉴配置。
6. 契约仅在胜利且达到目标时入档，之后重玩不能清除契约勋章。

## 6.1 P8.3 player progression extension

Formation choices are player progression data and are not authored inside a campaign level definition.
They are stored beside each `TDCampaignLevelProgress` record and are included in campaign snapshot export/import.

| Field | Type | Required | Description |
|---|---|---|---|
| `towerLoadout` | comma-separated tower IDs | No | One to four unique tower IDs valid for the mission's unlocked pool. Empty means runtime Auto Fit. |
| `resonanceDoctrine` | integer enum | No | `0=Adaptive`, `1=EmberSurge`, `2=FractureMark`; defaults to Adaptive. |

P8.3 persistence rules:

1. Normalize tower IDs to lowercase, reject unknown IDs and duplicates, and keep at most four.
2. Never allow a saved tower that is not unlocked for the target mission into the active build formation.
3. A missing or invalid loadout falls back to deterministic Auto Fit without writing PlayerPrefs until the player confirms deployment.
4. Doctrine remains Adaptive and has no score weight before `globalRules.resonanceEnabledFromLevel`.
5. Snapshot reset/import must clear and restore both `loadout` and `doctrine` keys with the rest of the level record.

## 6.2 P8.4 chapter and portable-save extension

Chapter reward claims are player progression data. The campaign snapshot adds:

| Field | Type | Required | Description |
|---|---|---|---|
| `claimedChapterRewards` | string[] | No | Normalized, unique reward IDs currently active on the profile. |

P8.4 persistence rules:

1. A chapter reward ID must be unique, use only letters, digits, `_` or `-`, and reference a reward in the loaded campaign.
2. Reward effects are applied after mission mutators and before starting budget and integrity are assigned to a run.
3. Snapshot reset clears reward claims with mission and formation records.
4. Portable saves emit `EMBERLINE-SAVE-2:<8-char FNV checksum>:<Base64 UTF-8 snapshot JSON>`.
5. Player import accepts save version 1 or 2, migrates version 1 to version 2, and requires exactly 20 unique mission records, bounded values, valid formations/doctrines and known reward IDs.
6. Campaign Profile import and reset require a second confirmation action. Codex discovery keys are outside this snapshot.

## 6.3 P8.5 difficulty and challenge extension

Each `TDCampaignLevelProgress` snapshot record adds:

| Field | Type | Required | Description |
|---|---|---|---|
| `difficultyPreference` | integer enum | No | Last confirmed tier for this mission, clamped to 0-2. |
| `highestDifficultyCleared` | integer enum | No | Highest victorious tier, clamped to 0-2 and never reduced by replay. |

P8.5 validation and runtime rules:

1. Production campaign data defines exactly three unique tier IDs and indexes 0, 1 and 2.
2. Veteran and Ember Trial modifiers must have at least one valid gameplay effect.
3. Every production chapter defines one non-empty `challengeRemix`; all mutator IDs are campaign-unique.
4. Veteran availability requires the selected chapter to be cleared. Ember Trial requires 20/20 campaign clears.
5. A locked saved preference falls back to the highest currently available tier without deleting the stored value.
6. Runtime composition order is mission mutator, difficulty modifier, chapter remix, then claimed chapter rewards.
7. Chapter remix applies only on Veteran and Ember Trial.
8. Snapshot reset/import clears and restores both difficulty fields with each mission record.

## 6.4 P8.6 slots, cloud envelope and scenario grammar

Persistence rules:

1. Exactly three save slots use independent PlayerPrefs prefixes. Slot switching cannot copy or erase another slot.
2. Legacy single-slot keys migrate once into slot 1. Legacy keys remain untouched for recovery.
3. Every slot tracks a monotonic revision and UTC modification ticks. Device ID is installation-wide.
4. Cloud envelopes use `EMBERLINE-CLOUD-1:<checksum>:<payload>` and include schema/save versions, target slot, revision, modification time, device ID and portable save.
5. Keep Local performs no mutation. Use Cloud validates then replaces the active slot. Merge keeps the maximum clear/mastery/contract/difficulty records and uses the newer formation/doctrine/preferences.
6. A cloud envelope can only resolve into its matching active slot; unknown chapter rewards are rejected by the campaign UI.

Scenario rules:

1. Every production map defines one valid, unique mechanic and mechanic type.
2. Every one of the 20 referenced wave sets contains at least one `introduce`, one `reinforce`, and one `exam` or `boss` phase.
3. `introduce` exposes the mechanic without enabling its command. `reinforce` enables the isolated decision. `exam` combines the mechanic with route and threat pressure.
4. L05, L09, L13, L17 and L20 are the five milestone exams. Each requires intensity 3 and a non-empty `failureFocus`.
5. Scenario use/opportunity conversion is included in run recap and can become a targeted replay recommendation.

## 7. Example (Truncated)
```json
{
  "schemaVersion": "campaign-schema-v1",
  "campaignId": "emberline_campaign_main_v1",
  "displayName": "Emberline Defense Campaign",
  "totalLevels": 20,
  "difficultyTiers": [
    { "tier": 0, "difficultyId": "standard", "displayName": "Standard", "description": "Authored baseline." },
    {
      "tier": 1,
      "difficultyId": "veteran",
      "displayName": "Veteran",
      "description": "Cleared chapter challenge.",
      "modifiers": { "mutatorId": "difficulty_veteran", "displayName": "Veteran Pressure", "enemyHpMultiplier": 1.15 }
    },
    {
      "tier": 2,
      "difficultyId": "ember_trial",
      "displayName": "Ember Trial",
      "description": "Full campaign challenge.",
      "modifiers": { "mutatorId": "difficulty_ember_trial", "displayName": "Ember Trial Pressure", "enemyHpMultiplier": 1.30 }
    }
  ],
  "chapters": [
    {
      "chapterId": "chapter_a",
      "displayName": "Chapter A",
      "startLevel": 1,
      "endLevel": 5,
      "challengeRemix": { "mutatorId": "chapter_a_rapid_escalation", "displayName": "Rapid Escalation", "enemySpeedMultiplier": 1.06 }
    },
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
      "newEnemyUnlocks": ["skitter_runner"],
      "contract": {
        "contractId": "l01_hold_the_line",
        "displayName": "Hold the Line",
        "metric": "integrity",
        "comparison": "at_least",
        "target": 16
      },
      "mutators": [
        { "mutatorId": "l01_reserve_grant", "displayName": "Reserve Grant", "startingBudgetDelta": 20 }
      ]
    }
  ],
  "globalRules": {
    "maxFailureReasonsShown": 3,
    "resonanceEnabledFromLevel": 16,
    "allowEarlyWaveDispatch": true
  }
}
```

## 7.1 P10.1 Meta Progression

`metaProgression` is required for the 20-level production campaign.

- `tacticalProtocols[]`: unique `protocolId`, player-facing name/description, unlock hint, and bounded runtime modifiers. Every non-baseline protocol must contain at least one benefit and one cost.
- `ratingRewards[]`: unique reward ID, `campaign_stars` source, threshold and an existing protocol destination.
- `codexRewards[]`: unique reward ID, `enemy_dossiers` or `tower_dossiers` source, threshold and an existing protocol destination.
- The runtime stores claimed reward IDs, unlocked protocol IDs, behavior observation flags and per-level protocol selections in save v2.
- Cloud merge uses set union for rewards/protocols, bitwise OR for observations and newer-per-level preference for protocol selections.

## 8. Compatibility Notes
P8.5 difficulty and chapter remix modifiers compose above the authored encounter pressure in each referenced wave set.

1. `campaign-schema-v1` 不替代 `wave-schema-v1`，而是上层编排。
2. 关卡级难度由 `waveSetId` 指向的波次文件具体定义。
3. 现有 20-wave 单地图配置可作为 `level_01` 到 `level_04` 的临时过渡数据源。
4. P8.2 字段保持 `campaign-schema-v1` 名称不变；旧数据可省略，新 20 关正式数据强制完整配置。
