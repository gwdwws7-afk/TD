# Emberline Defense

> **Status**: Superseded (Historical Reference)  
> **Date**: 2026-05-16  
> **Genre**: 2D Tower Defense  
> **Platform**: PC (first), Web build optional  
> **Superseded By**: `design/gdd/full-project-plan-emberline-defense-v2.0.md` (2026-05-19)

> [!WARNING]
> 本文档为早期概念稿。当前范围、内容规模与排期请以 `v2.0` 主计划为准。

## Creative Brief
一款快节奏 2D 塔防游戏。玩家在有限金币下布局防御塔，针对不同敌人和波次做反制，核心体验是“看懂威胁 -> 快速决策 -> 阵地迭代 -> 险胜翻盘”。

## Elevator Pitch
《Emberline Defense》是一款强调波次语法与反制链的 2D 塔防：你不是“堆塔”，而是在每一波前做资源下注，赌你的阵型能撑到下一次升级窗口。

## Core Verb
- Place
- Upgrade
- Re-route (future map mechanic)
- Counter

## Core Fantasy
- 我是战场指挥官，在资源紧张中建立“杀伤走廊”
- 我能读懂敌方编制并用更聪明的布防反制

## Unique Hook
- 波次不是只涨数值，而是按“教学目标”设计（测试单点火力、范围清杂、减速协同、抗性反制）
- 每波结算给出“失败原因标签”（漏怪原因可读）

## Primary MDA Aesthetic
- Challenge
- Expression
- Discovery（对组合与路径价值的发现）

## Scope
- **MVP**: 单地图、3 类塔、4 类敌人、20 波、可通关
- **Vertical Slice**: MVP + 一套完整美术风格（image2.0）+ 结算与重开流程
- **Full Vision**: 多地图、分支科技、特殊事件波、Boss

## Core Loop (4 Scales)
### 30-Second Loop
观察敌群 -> 放塔/升级 -> 看战线是否击穿

### 5-Minute Loop
完成 2-3 波 -> 获得金币与改造窗口 -> 重新平衡阵型

### Session Loop
从开局到失败/通关，形成一次完整“构筑尝试”

### Progression Loop
解锁新塔与被动强化，逐步提升可解的战术空间

## Pillars
1. **Readable Threats**: 玩家必须能看懂为什么漏怪
2. **Meaningful Trade-offs**: 每次花费都要有机会成本
3. **Wave as Teaching**: 波次要测试机制理解，不只抬数值
4. **Fast Iteration**: 单局失败后可快速重开验证新策略

## Anti-Pillars
1. 不做“纯挂机”塔防
2. 不做“塔外观多但机制同质化”
3. 不做“靠隐藏规则制造失败”

## Player Type
- Primary: Achiever / Strategist
- Secondary: Explorer（尝试不同塔组合法）
- Not For: 只想无脑堆数值、低操作低决策的玩家

## Feasibility
- Engine: Unity 6 (URP 2D)
- Art Pipeline: image2.0 批量生成 + Unity Sprite 导入
- Current State: 已有可玩原型（放塔/刷怪/子弹/金钱/生命）
- Biggest Risks:
  - 数值曲线失衡导致“单一最优塔”
  - 敌人与塔克制关系不清晰
  - 美术风格统一性不足

## MVP Definition (Ship-if-time-runs-out)
1. 一张地图可完整游玩到结算
2. 至少 3 种有明确定位差异的塔
3. 至少 4 种行为差异敌人
4. 20 波难度曲线可通关且不崩坏
5. image2.0 首套统一风格素材可用
