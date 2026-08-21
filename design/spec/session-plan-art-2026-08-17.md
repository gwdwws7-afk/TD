# 美术会话后续计划（2026-08-21 09:05 总导演更新，v8）

## 验收快照（相对 v7）

- ✅ 用户实机截图两个圈选问题修复（`139eb83`）：beacon 绿雾光晕参数化收紧（idle 6 + T2 6 帧）、death 消散雾清理（fx_enemy_death_04/05/06 的 95-99% 画布雾）
- ✅ 顺带 344 帧全目录清扫：约 50 帧参数化去外围淡雾（`tools/cleanup_fx_haze.py`，备份在 output/），特效本体未过度矫正
- ✅ T2 重建改进：`rebuild_tower_t2.py` 加连通域守卫（剔除灰膜/棋盘格背景残留，`6364d88`）
- 🔶 **T2 批次 44/48 帧**——4 帧缺失：`ember_flak_t2_02`、`grav_snare_t2_04`、`grav_snare_t2_05`、`rail_lancer_t2_03`；44 帧已重生成待入库
- v7 的本地任务（音频导入脚本、假 PNG）被截图反馈与 T2 插队，未做——顺延
- HEAD = f215c20

## 任务 1：补齐 T2 最后 4 帧（P0，当前主项——批次收口）

生图机 `git pull` 后 `python tools/generate_tower_t2.py`（断点续跑自动只补缺失帧，必要时 `--kinds <塔名> --force`）。然后：

1. 44 + 4 = 48 帧全量自验（连通域守卫 + 模块层阈值 + 目检）
2. 一笔提交整批（含重生成的 44 帧与 meta，后处理器自动规整）
3. 通知总导演 → QA 实机复验（三档读感 + beacon/death 观感跟进一次做完，其 v8 任务 2）

## 任务 2：敌人 death 帧批次——48 张（P1，T2 收口后接续）

规格已交付：`design/spec/enemy-death-frames-spec-v1.md`（12 敌 × 4 帧，逐敌死亡设计提示）。生效契约注意：共享死亡特效 `fx_enemy_death_*` 放入即播（你刚清理的那组）；**逐敌本体死亡卷轴需要代码挂钩**（TDEnemy 侧 ~15-20 行，代码会话 v8 已排）——挂钩落库前生成无副作用，与 T2 同模式。

## 任务 3：v7 遗留本地任务（P2，death 批次生成等待期做）

1. **音频导入脚本**（`design/spec/audio-design-spec-v1.md` 底稿，参考 `TDArtBatch103Import.cs` 模式）
2. **假 PNG logo 修复**（既定 backlog）
3. 各自独立提交，代码会话快验线评审

## 任务 4：Boss 立绘（P2，需求已就位排队）

需求定义已交付（`design/spec/boss-portrait-requirements-v1.md`：3 件产物/双相位可视编码/接入位）。排在 T2 + death 两批之后，产能允许时启动。

## 协作注意

- 生图环境走生图机（本机 DNS 污染未解），同步靠 git——开工前先 pull，提交后 push，避免两机漂移
- 工作区的 `EmberlineBootstrap.unity`（M）与 `ProjectSettings/SceneTemplateSettings.json`（??）是编辑器翻动痕迹——非你产出别入库，留给总导演统一清理

## 验收标准（更新）

- T2 48/48 入库 + QA 三档读感复验通过 → T2 批次关账
- death 帧批次按规格交付（挂钩就位后生效）
- 音频导入脚本 + 假 PNG 两笔本地提交落地
