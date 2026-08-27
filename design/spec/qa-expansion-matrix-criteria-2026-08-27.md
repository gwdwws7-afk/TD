# 扩充批 4 矩阵工具与判据档(计划 v14 任务 2,2026-08-27)

工具:`tools/qa_expansion_matrix.py` · 判据源:`design/spec/balance-reweave-input-v1.md` 三档口径 + §4 验收规则

## 跑法

- **B 段验收跑(扩充落地后)**:`python tools/qa_expansion_matrix.py --tier B --difficulty Standard --seeds 1337 42 2024`(180 局;判据只认终局跑,DurationSeconds 200 无截断)
- A 段门禁:`--tier A --difficulty Standard`(锚:冻结基线 15/15)
- C 段记录:`--tier C`(预期态,不追踪缺陷;12 塔+Boss 批次后转正式验收)
- 玩家难度档:Standard = meta-0 全 20 关可通关(对齐 meta-0 红线);Veteran/EmberTrial 允许局外加成——**批 4 落档前需补全 20 关 Veteran 探针**(现仅 5 关)
- focused 局:B.4 停滞自动打标(`<B.4>`),段 B 判定不计为策略缺陷(建造循环属代码侧输入)

## 判据(三档)

| 档 | 关段 | 判定 |
|---|---|---|
| A 校准段 | L01-09 | 全胜=GATE;任何败局=REGRESSION(曲线不许动) |
| B 平台段 | L10-15 | Standard meta-0 三策略终局全胜=ACCEPT;否则 BELOW-TIER(重织待验) |
| C 考试段 | L16-20 | 败=EXPECTED(记录);胜=ABOVE EXPECTATION(顺带报告) |

## 增量报告格式(对照冻结基线)

每关输出:`medianWaves`(本批中位)、`baselineWaves_s1337`(冻结基线 1337 种子)、`deltaVsBaseline`、胜率、B.4 计数。扩充后全矩阵重校报告按此格式对照 `baseline_pre_expansion_20260825/`,改进幅度以中位波差与胜率差双列呈现。

## §4 前置项提醒(重织动数据前核清)

1. P124 逐波谓词度量 vs 装甲 HP 份额对齐(代码侧待核)
2. B.4 插桩若先到,建造循环修复并入批 3,波次重织不预留补偿余量
3. 时限探针仅作速度信号(judge_matrix/pass1 模式),验收一律终局跑

## 冻结期裁判流水(随 S 阶段追加)

| 阶段 | 符号 | EditMode | 六局中位(L13/L20) | 判定 |
|---|---|---|---|---|
| S0+S1 | PURE PASS | 51/51 | 7 / 19(+1 正向漂移,新锚点) | ✅ |
| S2+S3 | PURE PASS | 51/51 | 7 / 19(**零漂移**) | ✅ |
