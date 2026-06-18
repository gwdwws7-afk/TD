---
name: fadx-tool
description: "让 AI 使用 FADT 的抽象词汇做跨品类分析，而不是被具体题材或镜头绑死。"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

# FADT 抽象设计工具

## 核心锚点资料
- **Formal Abstract Design Tools** — https://www.gamedeveloper.com/design/formal-abstract-design-tools
- **MIT CMS study materials listing FADT** — https://opencw.aprende.org/courses/comparative-media-studies/cms-608-game-design-fall-2010/study-materials/
- **Game Design Tools slides** — https://media.gdcvault.com/gdc2017/Presentations/Neil_Katharine_Game%20Design%20Tools.pdf

## FADT本质
用 intention、perceivable consequence、story 三个抽象维度建立共同语言。

## 核心概念
- **Intention**：玩家是否能形成明确目标与行动意图
- **Perceivable Consequence**：玩家是否能看见自己行为的结果并归因
- **Story**：既包括作者叙事，也包括系统中生成的玩家故事

## 分析流程
1. 先描述玩家最常见的意图链条
2. 检查系统是否让玩家知道：我能做什么、为什么做、做完会发生什么
3. 分析系统是否自然生成可讲述经历
4. 找出意图被打断、后果不可感知、故事无法成立的环节
5. 给出修复建议：是目标表达、反馈表达，还是系统耦合问题

## 输出模板
1. 核心目标/体验
2. 核心循环或核心对抗
3. 关键系统与资源
4. 主要决策点
5. 反馈与可读性
6. 学习曲线/难度结构
7. 设计支柱
8. 主要问题与建议

## FADT三维分析（框架增强）

### Intention（意图清晰度）
| 玩家常见意图 | 系统支持度 | 断裂点 |
|------------|----------|--------|
| [意图1] | [高/中/低] | [断裂描述] |

### Perceivable Consequence（结果可感知度）
| 玩家行为 | 系统反馈 | 归因清晰度 |
|---------|---------|-----------|
| [行为1] | [反馈1] | [清晰/模糊] |

### Story（可叙述性）
| 玩家经历 | 故事元素 | 可叙述性 |
|---------|---------|---------|
| [经历1] | [元素1] | [高/中/低] |

## 检查清单
- 玩家是否常常不知道下一步做什么？
- 玩家是否做了正确行为却看不到结果？
- 玩家是否能形成个人故事？

## 常见误区
- 把 FADT 当纯叙事分析工具
- 只谈作者叙事，不谈玩家故事
- 把 intention 误写成任务文本

## 一句话记忆
FADT 是检查玩家意图、结果感知和可叙述经历是否完整。

## 框架输出位置
`design/gdd/fadx-[analysis-target].md`
