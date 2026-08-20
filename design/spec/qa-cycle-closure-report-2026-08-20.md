# 项目级关账报告 — QA 周期收口(2026-08-20,计划 v7 任务 3)

供总导演出项目级结论。本协作周期(QA v3–v7)四支柱终态:

## 四支柱终态

### 1. 美术缺口 ✅ 清零(已关账)
B103 PPU/考试设备/贴花尺寸与观感/48 开火帧(t3+回退)/稀疏 prop 重跑全部实机验证通过;hollow_kiln 贴花修正复看 PASS(`4edf196`)。唯一遗留:假 PNG logo(JPG 内容)——既定 backlog,非本周期口径。

### 2. 手柄 P12.3 终态 ✅
- Phase A 19/19 全交互(含 TD-GP-001 主动开波、002/003 修复复验)
- **纯手柄 L01 20/20 通关工件 × 2**:倒计时开波(08-17)+ **主动开波**(08-19,P124 决策轨迹 × 纯 InputSystem 事件,零调试 API,`57af610`)
- TD-GP-004 复验 PASS(f959401:面板图形退出射线;(8,1) 强制面板下可建;合成环境对跳过按钮连动性的限定已注)
- → **设计可回填 P12.3 手柄矩阵终态**

### 3. 平衡回档 ❌ 未达门(进展真实,多种子证实)
**本轮多种子矩阵(3 种子 × L13/L20 × 5 局 + 25 门禁同批,种子间结果完全一致——确定性已由节拍修复保证)**:

| 指标 | L13 | L20 |
|---|---|---|
| 胜率 | **0/15**(波 5-9) | **0/15**(波 7-**18**) |
| SiegeDrill 入 adaptive/control 编队 | 1/3(仅 control) | **3/3(配额修复完全生效)** |
| GravSnare 出场 | 0 | 1/种子(control 选用——"从不被选"在 L20 已解) |
| focused 停滞(B.4) | 每种子 2 局(3 塔+预算闲置) | 同左 |

- 门禁底线:RailLancer 单塔种清关 = 0 ✅(25/25 局)
- **进展**:L20 adaptive 10-16 波 → 稳定 18 波;SiegeDrill/GravSnare 已上场
- **未达标** → 按判据走总导演决策线:**护甲帽 `min(flat, postPercent×0.5)` 解封评审**(设计已备)
- 附注:L13 的 adaptive 不纳 SiegeDrill(装甲优势检测未覆盖该图?)+ focused 建塔循环停滞(B.4 插桩线)为两个独立的次级问题

### 4. 发布 checklist ✅
商店/CI 双出包路径验证落档(纯净包 PURE PASS、默认包探针可用);复验工具与 runbook 全部入库(`tools/qa_*`)。

## 对账表闭环("等 QA 2 项")

| 项 | 状态 |
|---|---|
| P12.3 手柄矩阵回填(设计侧动作) | **QA 侧就绪**,终态数据+工件已交(`qa-gamepad-terminal-validation-2026-08-19.md`) |
| 失衡诊断关账(设计侧动作) | **QA 侧数据齐**:诊断 A.4 度量已复测(SiegeDrill L20 全入编、GravSnare control 选用);L13/L20 未回档 → 决策线开 |

## 遗留清单(移交下周期)

1. **平衡决策**:护甲帽解封评审(总导演)——L13 adaptive 配额盲区 + focused B.4 停滞插桩一并处理
2. GravSnare 在 L13/adaptive 的选择策略(非数值问题,如实记录)
3. TD-GP-004 已修复;假 PNG logo(美术 backlog)
4. 编辑器运维项:EditMode 测试后偶发玩家循环冻结(恢复法已记录于 checklist)

## 周期 QA 产物索引

`tools/`:qa_gamepad_acceptance.py(InputSystem 手柄驱动/适配器)、qa_pure_package_audit.py、qa_balance_multiseed.py、qa-runbook-b103-fireframes.md、_windup_ab.py(TD-WINDUP-001 判决工具)
`design/spec/`:qa-findings-gamepad / qa-b103-fireframes-result / qa-regression-2026-08-17 / qa-session-v4/v5 / qa-art-gaps-closed / qa-gamepad-terminal-validation / release-build-checklist / 本报告
数据:`output/playtest/`(平衡矩阵多种子 balance_multiseed/ + 门禁 balance_regression/、手柄工件、B103/开火帧证据)
