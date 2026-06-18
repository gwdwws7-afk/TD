---
name: machinations
description: "让 AI 把复杂系统、资源循环、经济平衡和成长曲线用节点—流—状态变化分析。"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

# Machinations 系统与经济设计

## 核心锚点资料
- **Machinations official platform** — https://machinations.io/
- **Game Design Tools slides** — https://media.gdcvault.com/gdc2017/Presentations/Neil_Katharine_Game%20Design%20Tools.pdf

## Machinations本质
把设计变成可模拟的资源系统。只要一个系统可以描述为输入、转化、存量、消耗、概率与反馈，它就能被结构化分析。

## 核心概念
- **Source / Drain / Pool**：来源、消耗点、存量池
- **Converter / Gate**：转化器和条件门
- **Feedback Loop**：正反馈和负反馈
- **Bottleneck**：真正卡玩家的节点
- **Simulation mindset**：关注均值、波动与极端结果

## 分析流程
1. 列出资源、货币、行动点、时间、概率产物
2. 画出输入—转化—消耗—回报闭环
3. 标出正反馈、负反馈与瓶颈位置
4. 估算新手、中期、后期的主要失衡风险
5. 输出：哪些地方该模拟，哪些地方靠手工调参

## 输出模板
1. 核心目标/体验
2. 核心循环或核心对抗
3. 关键系统与资源
4. 主要决策点
5. 反馈与可读性
6. 学习曲线/难度结构
7. 设计支柱
8. 主要问题与建议

## 资源流图谱（框架增强）
```
[Source] --> [Converter] --> [Pool] --> [Drain]
                ^
                |
            [Gate]
```

## 节点定义
| 节点名称 | 类型 | 描述 | 数值范围 |
|---------|------|-----|---------|
| [名称] | Source/Drain/Pool/Converter/Gate | [描述] | [范围] |

## 检查清单
- 是否识别出至少一个正反馈和一个负反馈？
- 是否说明系统失控会怎样表现？
- 是否分清主货币和次级货币？

## 常见误区
- 只谈产出，不谈消耗
- 只画大循环，不找瓶颈
- 把平衡问题都归结为数值

## 一句话记忆
Machinations 的重点是把系统从"想法"变成"可模拟的流"。

## 框架输出位置
`design/gdd/machinations-[system-name].md`
