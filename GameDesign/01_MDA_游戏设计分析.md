---
name: mda-analysis
description: "让 AI 用 MDA 框架把游戏拆成机制、运行中的玩家行为动态、以及最终体验，避免把层级混淆。"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

# MDA 游戏设计分析

## 核心锚点资料
- **MDA: A Formal Approach to Game Design and Game Research** — https://www.cs.northwestern.edu/~hunicke/MDA.pdf
- **AAAI archive for MDA** — https://aaai.org/papers/ws04-04-001-mda-a-formal-approach-to-game-design-and-game-research/

## MDA本质
强迫分析者区分：规则是什么、规则在玩家输入下会生成什么行为模式、这些模式会带来什么体验。

## 核心概念
- **Mechanics**：规则、对象、数值、输入映射、资源、关卡约束
- **Dynamics**：玩家与系统运行后出现的行为模式（滚雪球、拉扯、试错、构筑、风险管理）
- **Aesthetics**：玩家主观体验（sensation、challenge、discovery、expression等）
- **层间传导**：改一个 mechanic 会改变 dynamics，再间接改变 aesthetics

## 分析流程
1. 列出最重要的 5~10 条规则与资源约束
2. 描述这些规则在正常游玩中最常诱发的行为模式
3. 把这些行为模式转译成玩家体验词
4. 检查 mechanic 与体验目标的失配
5. 总结哪些体验来自系统结构，哪些只是演出强化

## 输出模板
1. 核心目标/体验
2. 核心循环或核心对抗
3. 关键系统与资源
4. 主要决策点
5. 反馈与可读性
6. 学习曲线/难度结构
7. 设计支柱
8. 主要问题与建议

## 因果链图谱（框架增强）
| Mechanic | → Dynamics | → Aesthetics |
|----------|-----------|--------------|
| [规则1] | [行为1] | [体验1] |

## 检查清单
- 有没有把"高压感"误写成 mechanic？
- 有没有解释 dynamics 是如何从规则跑出来的？
- 有没有指出最负载体验的关键机制？

## 常见误区
- 把 MDA 当三列名词堆砌
- 把演出包装误认为核心动态
- 只说"这个游戏爽"，不解释爽从哪条规则来

## 一句话记忆
规则生成行为，行为生成体验。

## 框架输出位置
`design/gdd/mda-[game-name].md`
