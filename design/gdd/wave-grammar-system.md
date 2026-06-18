# Wave Grammar System

> **Status**: In Design  
> **Author**: user + codex  
> **Last Updated**: 2026-05-16  
> **Implements Pillar**: Wave as Teaching, Readable Threats  

## Overview
波次语法系统定义“每一波在教什么、考什么、如何升压”。它不是简单提高敌人血量，而是通过敌人组合、出场节奏与抗性配置，系统性测试玩家对塔定位与反制链的理解。

## Player Fantasy
- 我不是被数值碾压，而是在和一套可理解的战术题对抗
- 每次失败都知道“为什么输了”，下次能有明确调整方向
- 后期高压波是“可读但紧张”的，而不是随机混乱

## Detailed Design
### Core Rules
1. 每一波必须有一个主要教学目标（例如：单点火力检查、范围清杂检查、减速协同检查）。
2. 每三波形成一个小节奏：`Introduce -> Reinforce -> Exam`。
3. 新敌人类型首次出现的波次必须降低其他变量，保证可识别性。
4. 不能连续 2 波以上用同一解法通吃（通过组合变化打破单解）。
5. 每一波结束后，系统记录失败/漏怪信号供调参与提示。

### States and Transitions
- Wave State
  - `Prep`（建造窗口）
  - `Spawning`（持续出怪）
  - `Active`（有残留敌人）
  - `Resolved`（全灭或漏怪结算）
  - `Reward`（发放奖励并进入下一波）
- Transition Rules
  - `Prep -> Spawning`：倒计时结束或手动开始
  - `Spawning -> Active`：本波全部单位已生成
  - `Active -> Resolved`：敌人清空或生命归零
  - `Resolved -> Reward`：结算完成
  - `Reward -> Prep`：下一波初始化完成

### Interactions with Other Systems
- 与 `Wave Spawner`：语法输出转成实际敌群序列和时间轴
- 与 `Enemy AI`：依赖敌人类型标签（轻甲、重甲、快单位、群体）
- 与 `Tower Combat`：验证塔组合是否形成反制链
- 与 `Economy`：决定波次奖励和建造窗口压力
- 与 `UI/HUD`：展示本波威胁摘要与失败原因标签

## Formulas
### Wave Budget
`wave_budget = base_budget + (wave_index * growth_linear) + growth_curve(wave_index)`

建议：
- `base_budget`: 10~25
- `growth_linear`: 2~8
- `growth_curve`: 可用分段函数在中后期抬升

### Enemy Cost Composition
`sum(enemy_cost_i * enemy_count_i) <= wave_budget * budget_tolerance`

建议：
- `budget_tolerance`: 0.9~1.1

### Pressure Score
`pressure_score = (incoming_hp_per_second * speed_factor) / expected_player_dps`

目标区间：
- 新手教学波：0.7~0.95
- 常规挑战波：0.95~1.15
- 高压考试波：1.15~1.35

### Variety Constraint
`variety_score = unique_enemy_types / total_enemy_groups`

目标：
- 早期 0.25~0.4（易读）
- 中后期 0.4~0.7（多维压力）

## Edge Cases
1. **教学过载**：同一波引入超过 1 个新敌人机制时，拆成两波。
2. **经济锁死**：连续高压导致玩家无调整窗口，必须插入缓冲波或提高奖励。
3. **单塔通关**：出现“单一塔配置连过3波”即触发语法警报。
4. **不可读混编**：同时出现高血+高速+群怪且无明显优先威胁时，降低复杂度。
5. **后期拖时**：残局只剩高血慢怪导致节奏拖沓，需设置软超时机制（加速或斩杀阈值）。

## Dependencies
- Upstream
  - Enemy archetype 数据（类型、标签、成本）
  - Tower combat 基线效率数据
  - Economy 奖励模型
- Downstream
  - Wave config 文件生成
  - HUD 波次提示文本
  - Balance Check（波次通过率、失败分布）

## Tuning Knobs
| Knob | Safe Range | Too Low | Too High |
|---|---|---|---|
| `base_budget` | 10~25 | 前期空洞 | 新手劝退 |
| `growth_linear` | 2~8 | 中期无压力 | 中期断崖 |
| `budget_tolerance` | 0.9~1.1 | 波次同质 | 波次失控 |
| `prep_time` | 4~15s | 无决策时间 | 节奏拖慢 |
| `reward_per_wave` | 15~120 | 经济锁死 | 滚雪球 |
| `boss_interval` | 5~10 waves | 缺乏高潮 | 压力过密 |
| `max_new_mechanics_per_wave` | 1 | 学习过载 | - |

## Acceptance Criteria
1. Wave 1-5：每波目标清晰，玩家能说出“这波在考什么”。
2. Wave 6-12：至少出现三种不同压力模式（单体突破、群体堆压、混合反制）。
3. Wave 13-20：平均通关率与失败率分布可解释，失败原因标签不超过 3 大类。
4. 任意 5 波窗口内，不出现完全同构敌群（避免重复体感）。
5. 通过数据回放，能定位每个难度尖峰来源（预算、速度、抗性或节奏）。

## Open Questions
1. 首发是否加入“精英波预警”，提前显示关键威胁？
2. 是否支持玩家手动提早开始下一波并获得额外奖励？
3. Boss 波放在 10/20 还是 8/16/24 的节奏更贴合时长目标？
