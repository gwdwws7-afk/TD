# Tower Upgrade System

> **Status**: In Design  
> **Author**: user + codex  
> **Last Updated**: 2026-05-16  
> **Implements Pillar**: Meaningful Trade-offs, Fast Iteration  

## Overview
Tower Upgrade 系统定义玩家如何在波次间用防卫预算强化既有塔，形成“短期救火 vs 长期收益”的经济抉择。目标是升级必须改变战术表现，而不是仅做数值堆叠。

## Player Fantasy
- 我的每次升级都能明显改变战线表现。
- 我可以把同一塔走成不同职责（伤害向/功能向）。
- 我在预算紧张时做出的升级选择会影响后续多波节奏。

## Detailed Design
### Core Rules
1. 每座塔最多可升级 3 阶（T0 -> T1 -> T2 -> T3）。
2. 每次升级提供二选一分支：`伤害向` 或 `功能向`。
3. 升级只能在建造窗口进行，战斗中不可升级（MVP）。
4. 升级价格递增，且同一分支连续强化有边际递减。
5. 升级必须影响至少一个关键战斗维度：伤害、攻速、射程、AOE、控制强度、克制倍率。

### States and Transitions
- Tower Upgrade State
  - `T0_Unupgraded` -> `T1_BranchA/B`
  - `T1_BranchA/B` -> `T2_BranchA/B`
  - `T2_BranchA/B` -> `T3_BranchA/B`
- Transition Rules
  - 预算足够且处于 Prep 状态时可升级
  - 升级后立即扣费并刷新塔参数快照

### Branch Design (MVP)
1. **轨道长枪塔（rail_lancer_tower）**
   - A 伤害向：提高单发伤害、对重装倍率
   - B 功能向：提高射程、降低目标切换阈值损耗

2. **煤爆迫击塔（cinder_mortar_tower）**
   - A 伤害向：提高爆心伤害、提高边缘最低伤害
   - B 功能向：扩大 AOE 半径、增加命中上限

3. **冷凝线圈塔（frost_coil_tower）**
   - A 伤害向：提升基础伤害与攻速
   - B 功能向：提高减速强度与持续时间

### Interactions with Other Systems
- 与 `Economy`：升级是中后期预算主要消耗渠道。
- 与 `Tower Combat`：升级直接改写伤害与克制表现。
- 与 `Wave Grammar`：考试波要求玩家做分支选择而非单纯加塔。
- 与 `UI/HUD`：展示升级收益预览与本波建议升级方向。

## Formulas
### Upgrade Cost
`upgrade_cost_tier_n = base_cost * tier_multiplier_n * branch_factor`

建议：
- `tier_multiplier`: T1=1.0, T2=1.6, T3=2.4
- `branch_factor`: 伤害向=1.0, 功能向=1.05（功能略贵）

### Stat Scaling
`upgraded_stat = base_stat * (1 + sum(branch_bonus_i) * diminishing_factor_n)`

建议：
- `diminishing_factor`: T1=1.0, T2=0.9, T3=0.8

### Upgrade Efficiency
`upgrade_efficiency = (effective_dps_after - effective_dps_before) / upgrade_cost`

用于限制某分支长期统治。

### Break-even Waves
`break_even_waves = upgrade_cost / expected_wave_gold_delta`

用于评估本波是否值得升级。

## Edge Cases
1. **预算误点**：预算不足时禁用按钮并显示缺口金额。
2. **升级后无感**：若升级后关键指标提升 <5%，判定为无效升级并需重调。
3. **分支锁死**：同塔分支一旦选择即锁定（MVP），后续可扩“重构券”。
4. **过度滚雪球**：连续升级同一塔导致通吃时，触发效率告警。
5. **批量升级卡顿**：同帧多塔升级时采用队列结算，避免 UI 卡顿。

## Dependencies
- Upstream
  - Tower Combat（可升级参数定义）
  - Economy（预算流）
  - Worldview Naming（升级命名语义）
- Downstream
  - UI/HUD 升级面板
  - Balance Check（分支胜率与效率分布）
  - Telemetry（升级选择率）

## Tuning Knobs
| Knob | Safe Range | Too Low | Too High |
|---|---|---|---|
| `tier_multiplier_t2` | 1.4~1.8 | 中盘过强 | 升级意愿低 |
| `tier_multiplier_t3` | 2.1~2.8 | 终盘滚雪球 | 终盘无成长感 |
| `branch_factor_functional` | 1.0~1.15 | 功能分支过优 | 功能分支无人选 |
| `min_upgrade_gain` | 5%~12% | 升级无感 | 数值跳变过大 |
| `same_tower_upgrade_penalty` | 0~20% | 单塔通吃 | 玩家挫败 |
| `respec_cost_ratio` (future) | 0.3~0.8 | 易刷最优 | 无法纠错 |

## Acceptance Criteria
1. 三塔均存在可用的 A/B 分支，不出现“伪分支”。
2. Wave 10 前玩家至少进行 1 次升级决策，且能感知效果差异。
3. 分支选择率在 20 局样本中不出现 90%+ 单分支垄断。
4. 单塔连续升级 3 次不能稳定覆盖所有敌人类型。
5. 预算紧张局中，升级与新建塔都应是可行选项（无单一正确答案）。

## Open Questions
1. Vertical Slice 是否允许“拆塔返还 60% 价值”以支持重构？
2. 是否在 T3 引入视觉外观升级（强化成就感）？
3. 首发是否加入一次性战术升级（本波生效）作为高阶策略？
