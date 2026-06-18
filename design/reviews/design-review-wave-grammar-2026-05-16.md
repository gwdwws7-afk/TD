## Design Review: wave-grammar-system.md
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
1. `pressure_score` 依赖 `expected_player_dps`，但该值来源尚未明确（实时统计或设计基线），实现前需固定口径。
2. `variety_score` 用于多样性控制，但未定义“enemy_group”的最小分组单位（按批次还是按敌人类型）。

### Implementability Concerns
1. “单塔通关触发语法警报”需要明确检测窗口和阈值（例如三波窗口中塔建造占比/伤害占比）。
2. 当前 GDD 定义了语法目标，但还缺“语法到配置文件字段”的映射表，建议补一张 schema 对照。

### Balance Concerns
1. 若 `budget_tolerance` 长期放到 1.1，后期波次波动可能导致难度尖刺。
2. `prep_time` 在后期如果不动态缩短，会稀释高压节奏。

### Recommendations
1. 增加“指标口径”小节：`expected_player_dps` 与 `enemy_group` 的正式定义。
2. 补 `WaveTemplate` 数据结构草案（波次目标、敌群批次、奖励、预警文本）。
3. 为 1-20 波先做一张教学目标时间线，避免后续实现阶段走偏。
4. 先以 10 波做封闭测试，再扩到 20 波。

### Verdict: APPROVED WITH SUGGESTIONS
文档可直接驱动第一版配置化实现；建议先补指标口径与 schema 对照，降低联调歧义。
