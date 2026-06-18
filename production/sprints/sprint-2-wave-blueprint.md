## Sprint 2 Wave Blueprint (20 Waves)
Date: 2026-05-19
Milestone: M1 Vertical Slice
Wave Set: `grayline_junction01_m1_v1`

### Design Mapping
1. Wave 1-3: 基础布防认知（Baseline）
2. Wave 4-6: 覆盖断层压力（Coverage Gap）
3. Wave 7-9: 单体与抗性识别（Single Target + Counter Intro）
4. Wave 10: 小考试（Mixed Exam）
5. Wave 11-14: 经济拉扯与重构窗口（Economy Tug）
6. Wave 15-17: 反制链考试（Counter Chain Exam）
7. Wave 18-19: 高压混编（High Pressure Mix）
8. Wave 20: 终局综合考试（Final Exam）

### Wave Plan
| Wave | Phase | goalTag | threatTags | Pressure Target | Budget Target | Budget Tol. | Hint |
|---|---|---|---|---:|---:|---:|---|
| 1 | introduce | baseline_path_read | light,basic | 0.78 | 8 | 1.10 | 试探波：先建立基础覆盖。 |
| 2 | reinforce | baseline_reinforce | light,swarm_intro | 0.86 | 11 | 1.10 | 压线波：开始出现小规模群体单位。 |
| 3 | exam | baseline_exam | light,swarm | 0.95 | 14 | 1.10 | 考试波：混编基础单位，检查覆盖完整度。 |
| 4 | introduce | coverage_gap | fast,gap_test | 0.98 | 17 | 1.08 | 试探波：高速单位测试火力空区。 |
| 5 | reinforce | coverage_reinforce | fast,swarm | 1.04 | 20 | 1.08 | 压线波：高速+群体叠加，要求范围区协同。 |
| 6 | exam | coverage_exam | fast,swarm,pressure | 1.14 | 24 | 1.07 | 考试波：高压快节奏，检验补塔与控速判断。 |
| 7 | introduce | single_target_intro | heavy_intro,durability | 1.00 | 27 | 1.07 | 试探波：首次重装单位，考验单体火力。 |
| 8 | reinforce | single_target_reinforce | heavy,mixed | 1.08 | 31 | 1.06 | 压线波：重装顶线+快怪穿线，要求分层火力。 |
| 9 | introduce | counter_check_intro | armored_intro,counter,heavy | 1.02 | 35 | 1.06 | 试探波：护甲单位登场，测试反制选择。 |
| 10 | exam | mixed_exam | heavy,swarm,armored,fast | 1.18 | 42 | 1.05 | 小考试波：四类压力混编，检验整体阵型与预算决策。 |
| 11 | introduce | economy_tug_intro | mixed,attrition,economy | 1.00 | 46 | 1.05 | 经济拉扯开始：保守存钱与补塔决策并存。 |
| 12 | reinforce | economy_tug_reinforce | heavy,armored,economy | 1.07 | 50 | 1.05 | 压线波：重装与护甲轮替推进，逼迫投资取舍。 |
| 13 | exam | economy_tug_exam | heavy,swarm,armored,economy | 1.17 | 56 | 1.05 | 经济考试波：若前序投资失衡将出现明显断层。 |
| 14 | reinforce | economy_rebuild_window | mixed,rebuild_window | 1.10 | 61 | 1.05 | 重构窗口波：高压但可读，鼓励纠偏与转型。 |
| 15 | introduce | counter_chain_intro | armored,heavy,counter | 1.12 | 66 | 1.05 | 反制链试探：错误塔型堆叠将明显掉血。 |
| 16 | reinforce | counter_chain_reinforce | armored,heavy,swarm | 1.22 | 72 | 1.05 | 压线波：抗性与群压叠加，要求多塔协同。 |
| 17 | exam | counter_chain_exam | armored,heavy,fast,swarm | 1.30 | 78 | 1.05 | 反制链考试波：检查升级分支与火力结构。 |
| 18 | reinforce | high_pressure_mix_a | heavy,armored,swarm,fast | 1.28 | 84 | 1.04 | 高压混编A：连续威胁，要求节奏稳定。 |
| 19 | reinforce | high_pressure_mix_b | heavy,armored,swarm,fast | 1.34 | 94 | 1.04 | 高压混编B：预算与覆盖双重拉扯。 |
| 20 | exam | final_exam | heavy,armored,swarm,fast,final | 1.40 | 104 | 1.05 | 终局考试波：综合验证构筑、升级和应变能力。 |

### Guardrails
1. 组内 `spawnInterval >= 0.18`，避免与 `spawnMinSpacing` 冲突。
2. 每波总威胁成本控制在 `budgetTarget * budgetTolerance` 以内。
3. Wave 10、13、17、20 作为关键考试波，保留更高可读性提示文本。
4. Wave 14 作为“高压重构窗口”，压强不降但节奏更可读。

### Next Actions
1. 接入失败原因标签与波次统计输出，支撑调参闭环。
2. 进入 20 局 playtest 回收通过率与失败标签分布。
3. 按 playtest 结果执行第一轮参数回调（优先 Wave 13/17/20）。
