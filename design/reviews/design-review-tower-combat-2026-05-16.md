## Design Review: tower-combat-system.md
Date: 2026-05-16

### Completeness: 8/8 sections
- Overview: Present
- Player Fantasy: Present
- Detailed Design: Present
- Formulas: Present
- Edge Cases: Present
- Dependencies: Present
- Tuning Knobs: Present
- Acceptance Criteria: Present

### Consistency Issues
1. 当前规则写明“最近目标优先”，但未定义切换成本与锁敌策略是否统一适用于所有塔类型，后续容易出现同类塔行为不一致。
2. `gold_efficiency` 只用理论 DPS 估算，尚未纳入射程覆盖率与溢出伤害，跨塔对比会偏差。

### Implementability Concerns
1. 未定义范围塔（AOE）的伤害衰减模型与命中上限，程序实现时会出现多个解释版本。
2. 命中反馈提到“漏怪原因标签”，但没有最小事件埋点定义（例如：死亡前最后命中塔、路径剩余距离），落地风险中等。

### Balance Concerns
1. 目前“同阶效率差异不超过 25%”作为目标合理，但缺少早中后期分层标准（开局/中盘/高压波）。
2. 敌方减伤上限 `20` 对低伤高攻塔有潜在硬克制风险，需在波次语法中同步补偿。

### Recommendations
1. 补一个“统一锁敌协议”小节：切换阈值、锁定最短持续时间、丢目标重选规则。
2. 增加 AOE 塔公式：半径、目标上限、边缘衰减和最低伤害。
3. 在 `Acceptance Criteria` 增加三段式验证：Wave 1-5、6-12、13-20。
4. 在后续 `Wave Grammar` GDD 明确每类塔的教学波次与失败信号。

### Verdict: NEEDS REVISION
文档结构完整，已可指导首版实现；但在“目标选择一致性”和“AOE伤害标准化”上仍需补齐，建议修订后进入正式实现阶段。
