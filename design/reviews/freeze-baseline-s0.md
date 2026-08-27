# 冻结期 S0 基线档（2026-08-27）

> 采集协议：autoplay `adaptive_network`、每种子 2 局、L01、`tools/_s0_baseline.py`（种子注入 + 逐波采样）。
> 用途：S1-S7 每步对照（R7 判据：单波次差异 = 帧时序噪声不判失败；胜负/完整度大梯度才回查）。
> 原始轨迹：`output/playtest/freeze_baseline_s0.json`

## 基线结果（HEAD = 088a2b1 谱系）

| 种子 | 局 | 终局 | 一致性 |
|---|---|---|---|
| 42 | A / B | wave 4, integrity 0, DEFEAT | 两局完全一致 |
| 7 | A / B | wave 4, integrity 0, DEFEAT | 两局完全一致 |
| 2024 | A / B | wave 3, integrity 0, DEFEAT | 两局完全一致 |

**中位数终局**：wave 4 完整度 0 败。种子间离散 ±1 波（帧时序敏感的正常带宽，TD-WINDUP-001 教训在案）。

## 对照判据（每 S 步跑同协议）

- **通过**：三种子终局 (wave, integrity, state) 与本表逐一相同，或差异仅在种子固有离散带宽内（同种子两局仍一致）。
- **回查**：任一种子的终局波次变化 ≥2 或出现 WIN/DEFEAT 翻转。
- 附带符号抽查清单（P3 报告沿用）：探针/模拟器/Debug API/审计符号在 release 无符号构建产物中为 0。

## 备注

autoplay 基线为"败局轨迹"是预期（autoplay 策略非平衡基线；本档只用于**代码行为不变性**对照，不用于平衡判定——平衡基线是 QA 帽后矩阵）。
