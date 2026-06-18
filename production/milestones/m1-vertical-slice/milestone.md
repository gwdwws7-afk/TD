# Milestone: M1 Vertical Slice

> **Status**: Active  
> **Target Date**: 2026-06-06  
> **Owner**: user + codex  

## Goal
交付一个可完整游玩的 2D 塔防垂直切片，包含核心玩法闭环、首套统一美术与基础可调平衡参数。

## Success Criteria
1. 单地图可完整游玩（开始 -> 波次推进 -> 胜负结算 -> 重开）。
2. 至少 3 类塔与 4 类敌人，存在清晰反制关系。
3. 波次具教学结构，不只是线性抬数值。
4. image2.0 生成的首套美术资源已接入并可用。
5. 基础 HUD 与关键反馈可读（金币、生命、波次、漏怪反馈）。

## Scope
### In
- Grid & Path
- Tower Placement
- Tower Combat
- Wave Spawner (config-driven)
- Economy
- Basic HUD
- Art generation pipeline (image2.0)

### Out
- 局外成长
- 多地图章节
- 复杂剧情系统

## Risks
| Risk | Severity | Mitigation |
|---|---|---|
| 单塔最优解 | High | 每周执行 balance-check，维护克制链 |
| 波次体验断层 | Medium | 波次按教学目标重写，不按纯血量递增 |
| 美术风格不统一 | Medium | 固定 prompt 模板，分批评审后再扩产 |

## Exit Checklist
- [ ] Core loop 稳定
- [ ] 波次可跑完且难度曲线合理
- [ ] 美术资源可替换与回退
- [ ] 无 S1/S2 阻断问题
