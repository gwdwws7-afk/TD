# Tower Combat System

> **Status**: In Design  
> **Author**: user + codex  
> **Last Updated**: 2026-05-16  
> **Implements Pillar**: Meaningful Trade-offs, Readable Threats  

## Overview
塔战斗系统负责“玩家布防决策如何转化为实际击杀效率”。玩家通过不同射程、攻速、伤害模型与效果类型的塔，建立覆盖区并削减敌群。系统目标是在可读前提下提供明确定位差异，避免单一最优解。

## Player Fantasy
- 我部署的阵型在运行，不同塔各司其职
- 我能通过理解敌人类型进行有效反制
- 我看到漏怪时知道哪里出了问题（输出不足、射程覆盖空洞、克制错误）

参考体验：`Kingdom Rush` 的定位差异、`Bloons TD` 的克制链清晰度。

## Detailed Design
### Core Rules
1. 塔只能放置于可建造格（非路径且未占位）。
2. 塔每 `fire_interval = 1 / attack_speed` 秒尝试开火一次。
3. 开火时遵循统一锁敌协议（默认“最近目标优先”）。
4. 命中后按伤害公式结算，敌人生命 <= 0 时死亡并掉落金币。
5. 每个塔类型必须拥有唯一战术身份（单体爆发、范围清杂、减速控制等）。
6. 每波结束后至少保留一次可调整窗口（防止经济锁死）。
7. 同一种敌人首次出现时，必须有明确可反制塔（避免“无解首遇”）。

### States and Transitions
- Tower State
  - `Idle` -> `AcquireTarget` -> `Fire` -> `Cooldown` -> `AcquireTarget`
  - 若无目标：`AcquireTarget` -> `Idle`
- Enemy State
  - `Moving` -> `HitReact` -> `Moving`
  - `Moving` -> `Dead`
  - `Moving` -> `Escaped`

### Targeting Protocol (Unified)（2026-08-18 按实现回写）
1. **Acquire**：加权评分制——`score = 路径进度×100 + 塔类型克制加分（最高 +24）+ (1−血量比)×5 − 距离惩罚（≤2）`，效果≈最接近终点优先；克制倾向按塔专属（控制塔偏 fast/flank、点杀塔偏 armored/heavy/boss、范围塔偏虫群）。
2. **重扫节流**：每塔 `0.15s + 实例 ID 错峰抖动（≤0.19s）` 全量重评分；两次重扫之间沿用缓存目标。
3. **Lock Window**：无显式锁定窗——蓄力期（0.14~0.40s，战斗数据 `TowerState.windupDuration`）目标硬锁定，等效防抖。
4. **Lose Target**：目标超出射程即置空缓存；目标死于蓄力期则该发落空但不返还冷却（防目标 churn 白嫖攻速）。
5. **Policy Consistency**：协议集中于 `TDGameManager.GetPriorityEnemy`，全部塔共用同一实现。

### Combat Pipeline (Per Attack)
1. 选择目标（锁敌协议）。
2. 生成攻击事件（瞬时命中或投射物命中）。
3. 计算伤害倍率（塔类型 -> 敌人类型克制）。
4. 计算护甲减伤并夹逼到最小伤害 1。
5. 结算状态效果（减速、灼烧等）。
6. 触发生命变化、死亡判定、赏金结算。
7. 上报埋点（tower_fire / enemy_killed / enemy_escaped）。

### Interactions with Other Systems
- 与 `Economy`：建造/升级消耗金币；击杀回金币
- 与 `Wave Spawner`：不同波次敌群要求不同塔组合
- 与 `UI/HUD`：显示塔范围（可选）、DPS摘要、漏怪反馈标签
- 与 `Status Effects`（后续）：命中附加减速/灼烧/破甲

## Formulas
### Base Damage
`final_damage = max(1, floor((tower_damage * damage_multiplier) - enemy_armor_flat))`

变量定义：
- `tower_damage`：塔基础伤害
- `damage_multiplier`：克制修正（默认 1.0）
- `enemy_armor_flat`：敌方固定减伤

范围建议：
- `tower_damage`: 1~120
- `damage_multiplier`: 0.5~1.8
- `enemy_armor_flat`: 0~20

### Attack Throughput
`theoretical_dps = final_damage * attack_speed`

### Time to Kill (single target estimate)
`ttk = enemy_hp / theoretical_dps`

### AOE Damage Model
`aoe_damage(target) = max(1, floor(final_damage * falloff_multiplier(distance_ratio)))`

`distance_ratio = target_distance_from_impact / aoe_radius`

`falloff_multiplier(r) = lerp(1.0, min_falloff, clamp01(r))`

建议参数：
- `aoe_radius`: 0.8~2.5 格
- `min_falloff`: 0.35~0.7
- `aoe_max_targets`: 3~12

### AOE Throughput
`aoe_effective_dps = sum(aoe_damage_i for i in hit_targets) * attack_speed`

其中 `hit_targets = min(targets_in_radius, aoe_max_targets)`。

### Economy Efficiency
`gold_efficiency = effective_dps / tower_cost`

其中：
- 单体塔：`effective_dps = theoretical_dps`
- 范围塔：`effective_dps = aoe_effective_dps`

用于比较塔的基线效率，避免某塔长期显著高于同阶塔（>25%）。

### Baseline Archetype Data (MVP)
| 单位 | 成本/生命 | 射程/速度 | 频率 | 伤害/护甲 | 备注 |
|---|---:|---:|---:|---:|---|
| 轨道长枪塔 | 40 | 3.0格 | 1.0/s | 18 | 重装倍率 1.25 |
| 煤爆迫击塔 | 55 | 2.8格 | 0.55/s | 22 | AOE 1.2格，最多6目标 |
| 冷凝线圈塔 | 45 | 2.6格 | 0.8/s | 8 | 30%减速，1.5s |
| 掠行虫 | 26HP | 2.2格/秒 | - | 0甲 | 高速低血 |
| 甲壳重兽 | 120HP | 0.8格/秒 | - | 4甲 | 高血低速 |
| 灰潮虫群 | 16HP | 1.5格/秒 | - | 0甲 | 群体单位 |
| 护甲孢体 | 70HP | 1.1格/秒 | - | 8甲 | 高减伤 |

### Counter Matrix (MVP)
| 塔 \\ 敌 | 掠行虫 | 甲壳重兽 | 灰潮虫群 | 护甲孢体 |
|---|---|---|---|---|
| 轨道长枪塔 | 中 | 高 | 低 | 中高 |
| 煤爆迫击塔 | 中 | 低 | 高 | 中 |
| 冷凝线圈塔 | 高（控速） | 中 | 高（控群） | 中 |

## Edge Cases
1. **边界目标**：敌人位于射程边缘抖动时，目标锁定至少持续 0.2s 避免抖动换目标。
2. **无目标帧**：冷却结束但无目标时，不应重置额外冷却。
3. **超高攻速**：`attack_speed` 设上限（例如 10/s）避免性能尖刺。
4. **过量伤害**：伤害溢出不传递给其他敌人（除非范围塔特例）。
5. **同帧死亡**：同帧多塔命中同一敌人时，奖励只结算一次。
6. **AOE同帧重复命中**：同一发 AOE 对同一敌人只结算一次。
7. **目标切换抖动**：锁定窗口内禁止因微小位移切目标。
8. **经济锁死回合**：若连续 2 波无有效布防预算，强制触发缓冲奖励机制。

## Dependencies
- Upstream
  - Grid & Path（位置与路径）
  - Enemy AI（移动与生命状态）
  - Economy（金币）
- Downstream
  - Wave Grammar（用于验证波次是否可解）
  - Balance Check（公式与效率审查）
  - VFX/SFX（命中与击杀反馈）

## Tuning Knobs
| Knob | Safe Range | Too Low | Too High |
|---|---|---|---|
| `tower_cost` | 20~250 | 决策无成本 | 早期无法布防 |
| `tower_damage` | 1~120 | 战线拖沓 | 秒杀破坏节奏 |
| `attack_speed` | 0.4~10/s | 手感迟缓 | 性能风险 |
| `range` | 1.2~6.0格 | 覆盖太窄 | 布局意义下降 |
| `enemy_armor_flat` | 0~20 | 克制无意义 | 新手挫败感高 |
| `reward_gold` | 1~80 | 经济崩坏偏紧 | 滚雪球失控 |
| `target_switch_ratio` | 0.7~0.9 | 目标过于僵硬 | 抖动换目标 |
| `aoe_radius` | 0.8~2.5格 | 清杂不足 | 范围塔通吃 |
| `aoe_max_targets` | 3~12 | AOE价值过低 | 群怪关卡失衡 |
| `min_falloff` | 0.35~0.7 | 边缘伤害太差 | 无差别全额AOE |

## Telemetry (Minimal Event Set)
用于“漏怪原因标签”最小化落地：
1. `enemy_escaped`：敌人类型、剩余HP、路径剩余距离、最近3秒受击来源。
2. `tower_fire`：塔类型、目标类型、命中/未命中、期望伤害。
3. `enemy_killed`：击杀塔类型、波次、击杀位置。
4. `wave_end_summary`：总伤害占比、漏怪来源、金币结余。

### Breach Reason Tags (玩家可见)
1. `dps_insufficient`：重装单位漏防，单体火力不足。
2. `coverage_gap`：高速单位穿越覆盖空区。
3. `counter_mismatch`：塔组合与敌人抗性不匹配。

## Acceptance Criteria
1. **Wave 1-5**：玩家能识别每种塔定位，且无“第一塔通吃”策略。
2. **Wave 6-12**：至少出现 2 次必须更换塔组合才能稳定过关的波次挑战。
3. **Wave 13-20**：单塔效率优势不长期超过同阶 25%，且 AOE 不垄断群怪解法。
4. 30 分钟压力测试下，无异常卡顿或对象泄漏。
5. 漏怪时可定位原因（输出不足/射程空洞/克制错误）并能通过调整改善。
6. 单波平均战斗时长维持在 35~90 秒区间，超出则进入平衡告警。
7. 同屏敌人 80 上限压力下，帧时间无持续性劣化。

## Open Questions
1. 首发是否加入“穿透弹”塔，还是放到 Vertical Slice？
2. 首发敌人抗性做几类（护甲、飞行、群体）更利于教学？
3. 是否启用“塔朝向影响攻速”这一高阶机制？
