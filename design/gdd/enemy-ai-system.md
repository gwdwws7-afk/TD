# Enemy AI System

> **Status**: In Design  
> **Author**: user + codex  
> **Last Updated**: 2026-05-16  
> **Implements Pillar**: Readable Threats, Wave as Teaching  

## Overview
Enemy AI 系统定义敌人如何沿余烬铁道推进、如何施加不同类型压力、以及玩家如何从敌人行为中读出正确反制策略。目标是“行为清晰、威胁分层、可调可控”，避免敌人看起来只是不同血条。

## Player Fantasy
- 我能一眼看出这波敌人的主威胁是什么。
- 我通过观察敌人行为而不是猜规则来调整阵型。
- 我失败后能说出“是被快怪穿了，还是被重装顶穿了”。

## Detailed Design
### Core Rules
1. 所有敌人沿轨道路径从起点推进到终点，进入终点则造成线路完整度损失。
2. 每种敌人必须有明确行为签名（速度、耐久、抗性、群体密度或特殊机制）。
3. 敌人行为优先保证可读性，再追求复杂性；首发不引入不可见被动。
4. 敌人可被减速、受击、死亡；默认不具备硬控免疫（Boss 除外）。
5. 同波混编时，主威胁单位必须通过外观/移动节奏可识别。

### States and Transitions
- Global Enemy State
  - `Spawned` -> `Moving` -> `Escaped`
  - `Moving` -> `Debuffed` -> `Moving`
  - `Moving` -> `Dead`
- Shared Transition Rules
  - 生命值 <= 0 -> `Dead`
  - 到达终点 -> `Escaped`
  - 受减速效果 -> `Debuffed`（持续结束返回 `Moving`）

### Archetype Behaviors (MVP 4 Enemies)
1. **掠行虫（skitter_runner）**
   - 角色：突破覆盖缝隙的高速试探单位
   - 行为：恒定高速推进，不做停留
   - 设计意图：强迫玩家补齐射程断层

2. **甲壳重兽（carapace_brute）**
   - 角色：拖线与破阵单位
   - 行为：低速高血，承受火力并为后续单位创造穿线窗口
   - 设计意图：强迫玩家配置稳定单体输出

3. **灰潮虫群（ash_swarm）**
   - 角色：清杂压力单位
   - 行为：以小群打包刷新，单位间距小，形成区域压力
   - 设计意图：验证范围塔与控制协同

4. **护甲孢体（plated_spore）**
   - 角色：抗性检查单位
   - 行为：中速推进，拥有高固定护甲
   - 设计意图：惩罚错误塔型堆叠，推动反制决策

### Interactions with Other Systems
- 与 `Wave Spawner`：按波次语法决定刷新数量、间隔与组合。
- 与 `Tower Combat`：接受伤害、减速、击杀结算与克制修正。
- 与 `Economy`：不同敌人击杀赏金不同，影响跨波预算。
- 与 `UI/HUD`：输出“本波威胁”与“漏防原因”分类依据。

## Formulas
### Effective Move Speed
`effective_speed = base_speed * slow_multiplier_total`

其中：
- `slow_multiplier_total = max(min_speed_ratio, product(1 - slow_i))`
- 建议 `min_speed_ratio = 0.35`（避免完全停滞）

### Armor-Adjusted Damage Intake
`damage_taken = max(1, floor(incoming_damage - armor_flat))`

### Time on Track (Threat Persistence)
`time_on_track = path_length / effective_speed`

用于估算敌人在火力区停留时间，指导波次编排。

### Threat Weight (for Wave Composition)
`threat_weight = (hp * (1 + armor_flat * armor_factor) * speed_factor) / reward_gold`

建议：
- `armor_factor = 0.08~0.15`
- `speed_factor = lerp(0.9, 1.3, normalized_speed)`

## Edge Cases
1. **重叠刷怪**：同一生成点过密时，自动插入最小间隔避免视觉重叠。
2. **减速叠加失控**：多来源减速不得低于 `min_speed_ratio`。
3. **路径终点拥堵**：多单位同帧到达时逐个结算，避免漏扣线路完整度。
4. **超量AOE结算**：群怪同帧受击时保证死亡和赏金只结算一次。
5. **刷新公平性**：首次出现新敌种时不得同时叠加高压预算峰值。

## Dependencies
- Upstream
  - Grid & Path（路径长度、节点）
  - Worldview Naming（敌方命名与语义）
  - Wave Grammar（波次教学目标）
- Downstream
  - Tower Combat（克制矩阵）
  - Balance Check（通过率与失效构筑分析）
  - Telemetry（漏防原因判定）

## Tuning Knobs
| Knob | Safe Range | Too Low | Too High |
|---|---|---|---|
| `runner_speed` | 1.8~2.6 | 威胁不足 | 覆盖无解 |
| `brute_hp` | 90~180 | 无压线感 | 拖时严重 |
| `swarm_pack_size` | 4~12 | 群压不足 | 性能与可读性下降 |
| `spore_armor_flat` | 5~12 | 反制感弱 | 新手挫败 |
| `spawn_min_spacing` | 0.15~0.45s | 模型堆叠 | 波次节奏拖慢 |
| `min_speed_ratio` | 0.3~0.5 | 控制塔过强 | 控制塔失效 |
| `line_damage_on_escape` | 1~3 | 失败成本低 | 单次失误过罚 |

## Acceptance Criteria
1. 玩家在 Wave 1-5 能准确区分 4 类敌人的主要威胁特征。
2. 至少 3 种失败案例可稳定映射到既定原因标签（输出不足/覆盖断层/克制错误）。
3. 同一套塔配置不能稳定无损通过所有敌人组合（反制链有效）。
4. 群怪高压波下，敌人重叠与结算错误率为 0（功能正确性）。
5. 20 波全程中，敌人行为差异可读且无“纯血量换皮”波次。

## Open Questions
1. Vertical Slice 是否加入“突袭翼兽（空中单位）”作为第 5 敌种？
2. 护甲孢体是否需要“短时护盾激活”机制，还是先保持固定护甲？
3. 是否引入“路径分岔优先级”用于后续多路线地图？
