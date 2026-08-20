# TDGameManager 拆分技术设计 v1

> 作者：代码会话（总导演 v7 计划任务 1）· 日期：2026-08-20 · 状态：待设计会话过排期相容性
> 性质：冻结期实施的技术蓝图。**本文档零代码改动**；QA 平衡矩阵闭环前不启动任何实施。

---

## 1. 盘点（现状边界）

### 1.1 规模与结构

| 项 | 数值 |
|---|---|
| 主文件 `TDGameManager.cs` | 19,742 行、620 个方法、约 330 个实例字段（对账表口径 336） |
| partial 家族合计 | 23,097 行（P124 自动游玩 1,847 / P135 决策遥测 558 / GamepadCursor 312 / P134 审计 250 / P1254 soak 187 / P125 经济遥测 127 / P1253 49 / P1252 25） |
| 主文件内自动化门控点 | 14 处 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD \|\| TD_AUTOMATION` |
| 连续调试区 | **15426–19530 行（约 4,100 行单块 `#if`）**，内嵌 4 个方法级门控（15728/15930/18980 等，嵌套合法） |

### 1.2 调试区可搬迁性（P3 门控后的现状）

**仍可整体搬迁 ✓**。依据：
- 区域边界干净：起点在 `SetStatus`（15426 前后）之后、终点在 `IsBuildWindowOpen`（19531+）之前，区域内全部为 `Debug*`/`Audit*` 命名方法（唯一例外 4 个辅助方法 `AccumulateExpectedRuntimeMutator`/`IsP133BuildSiteClearOfPersistentUi`/`SeedP121RunResultAnalytics`/`CollectTextOverflowNames`，调用方核查过全部在门控域内，可随迁）；
- 区域对外零泄漏：release 无符号构建 + 符号抽查两轮实证（探针/模拟器/审计符号在产物中为 0）；
- 搬迁方式：整块剪切至新文件（如 `TDAuditDirectors.cs`）保持 `#if` 包裹不动，或进一步移入独立 asmdef（`TD.Automation`，仅 Editor/Development/TD_AUTOMATION 平台）。**推荐后者**——隔离更彻底，且让"新增调试代码不得进主文件"成为编译期约束。

### 1.3 主文件职责聚类（方法数按签名前缀统计，有交叉）

| 职责簇 | 方法数（约） | 行数感估 | 现有先例 |
|---|---|---|---|
| 波次状态机（Wave/Spawn/Prep） | 124 | ~2,800 | `WaveLoopFromConfig`/`WaitForPrepStart`/`SpawnGroup` |
| 代码化 UI（Build*/Create*/Refresh*/*Ui） | 138 | ~3,500 | 战斗 HUD、任务板、编队面板、世界地图接线 |
| 战役/存档/进度（Campaign/Portable/Cloud） | 130 | ~2,400 | `LoadCampaignContext`、导入导出、云冲突 |
| 共鸣/矩阵（Resonance） | 55 | ~1,300 | `UpdateResonanceState`/矩阵收敛 |
| 调试/审计（Debug*/Audit*） | 55 | ~4,100（=调试区） | 见 1.2 |
| 音频（Sfx/Music/Mixer/Tone） | 45 | ~1,000 | 程序化波形合成 `CreateSfxClip`、BGM 状态机 |
| 场景机制（Scenario） | 24 | ~600 | 增援/装置/Boss 阶段 |
| 教程 | 15 | ~350 | |
| 图鉴（Codex） | 13 | ~250 | |
| 其余（输入/相机/生命周期/统计评分/通知） | ~100 | ~2,500 | 含 P1-P7 性能接缝（光环缓存、共享缓冲等） |

---

## 2. 接缝清单（目标模块）

原则：**沿用已验证的三种解耦模式**——①纯函数静态类（`TDCombatMath` 先例）；②静态门面 + 数据表（`TDCampaignProgression` 先例）；③不可变数据 + 重建（`TowerState`/windup 先例）。不引入接口爆炸：模块间以具体类 + 显式依赖传递，只有测试需要替身处才立接口。

### M0 `TD.Automation`（调试区搬迁）
- 输入：主文件私有状态（经现有 `Debug*ForTest` 方法体访问）→ 搬迁后改走 `internal` 访问器或保留在 partial（见风险 R1）
- 输出：无（只被探针/MCP 调用）
- 接口：**无需新接口**——partial 语义保留（同程序集内 partial 跨文件），先整体剪切到独立文件；asmdef 化是第二步，届时用 `internal` + `InternalsVisibleTo`

### M1 `TDRunStats`（运行统计/评分/遥测）——纯数据簇
- 输入：`Notify*` 事件流（击杀/逃脱/伤害/升级/共振…）→ 方法签名照搬
- 输出：`CalculateRunScore()`/`CalculateRunCounterScore()`/报表 DTO（`TDRunScoreReport` 等随迁）
- 依赖：只读 `_activeEnemies` 数量、波次号 → 以参数传入
- 接口：无（具体类）；P125 遥测字段随迁

### M2 `TDAudioService`（音频）
- 输入：`PlaySfxTone/PlayCriticalSfxTone/PlayEnemySfx` 调用点（全库 ~200 处调用，签名不变、改静态转发）；音乐状态输入（gameOver/boardOpen/resonance）
- 输出：AudioSource 播放、快照切换；状态查询（供审计）
- 依赖：`_emberlineMixer`/`_sfxSource`/`_musicSource` 的创建与生命周期随迁；`Resources.Load` 音频路径表随迁
- 接口：静态门面（先例：`TDLocalization`），内部持有自建 GameObject

### M3 `TDBattleUiFactory`（代码化 UI）
- 输入：`CreateUiButton/CreateUiText/CreateUiPanel/CreateUiRect/AddUiPanelChrome/AddUiButtonIcon` 等 ~20 个工厂方法 + `_battleCanvas`/`_uiFont`
- 输出：RectTransform/Text/Button 组件树
- 依赖：`TDLocalization.ResolveFont`、`TDUiVisualIdentity`（已独立，直接引用）
- 接口：静态类；面板的**业务**刷新逻辑（`UpdateXxxUi` 138 个）**不随迁**——它们属于各子系统的表现层，随各自模块走或留主文件做编排

### M4 `TDWaveDirector`（波次状态机）
- 输入：`_waveSet`（数值外置后为配置资产）、`_enemyCatalog`、部署确认/开波请求/暂停信号
- 输出：波次推进事件（BeginWaveStat/FinalizeCurrentWaveStat 回调）、出怪（经 M5 敌人服务）、奖励发放（回调主文件经济）
- 依赖：协程宿主（保持 MonoBehaviour，挂 manager 子对象）
- 接口：`IWavesRunner { event Action<int> WaveCleared; … }`——**本模块与 M5 是仅有的两处建议立接口的地方**（波次↔经济↔战斗三方互调，需测试替身）

### M5 `TDCombatServices`（战斗服务聚合：敌人注册表 + 目标搜索 + 光环 + 伤害修正）
- 输入：`_activeEnemies` 注册表（spawn/RemoveAll/death 三口）、塔的查询请求
- 输出：`GetPriorityEnemy`/`GetEnemiesInRange`（共享缓冲语义文档化！调用方不得持有返回列表——P1 已核实现状满足）/`HasSupportAuraNearby`/`GetModifiedDamageForEnemy`
- 依赖：P1 性能接缝全部在此（目标节流的塔侧缓存除外，留在 TDTower）
- 接口：`IEnemyRegistry`（供 M4 与未来敌人池化使用）

### 不拆的部分
- 战役进度/存档（已是静态门面，主文件只是接线）；`TDCombatMath`（已纯）；输入兼容（已独立）；各表现类（已独立）

---

## 3. 迁移顺序（解耦模式复制：先纯/数据，后行为）

每步统一验证协议：**EditMode 全量 + 编辑器编译 0 错/0 新警告 + `_build_verify.py`（无符号构建 + 符号抽查）+ autoplay 种子基线（种子 42，`adaptive_network`，比对波次/完整度轨迹）+ QA 回归 runbook 抽样**。每步一笔提交、独立可回退。

| 步 | 内容 | 模式 | 预估 | 风险 |
|---|---|---|---|---|
| S0 | 基线固化：autoplay 种子轨迹归档（当前 HEAD 三局）、QA 矩阵绿档 | — | 0.5d | 无 |
| S1 | M0 调试区整体剪切至 `TD.Automation` 独立 asmdef | 整块搬迁 | 1d | R1 |
| S2 | M1 RunStats（纯数据 + `CalculateRunScore` 族） | ①/② | 1d | 低 |
| S3 | M2 AudioService（静态转发，调用点零改动） | ② | 1d | 低 |
| S4 | M3 UiFactory（工厂下沉；`UpdateXxxUi` 不动） | ② | 1.5d | 中（坐标字面量随迁） |
| S5 | M5 CombatServices（注册表 + 查询 + 光环；P1 接缝随迁） | ③+接口 | 2d | R2 |
| S6 | M4 WaveDirector（协程 + 状态机；最后拆） | ③+接口 | 2d | R3 |
| S7 | 收尾：主文件 ~19.7k → 目标 ≤6k（编排 + 接线 + 教程/图鉴/场景机制待定去留） | — | 1d | — |

顺序依据：S1 零行为风险先拿走 4,100 行；S2-S3 无状态或自持状态；S4 只动创建不动逻辑；S5 是 S6 的前置（波次要经注册表出怪）；S6 依赖前述全部就位。S5 之前完成敌人池化评估（见 R4）将显著降低 S5 返工。

---

## 4. 风险登记

| # | 风险 | 缓解 |
|---|---|---|
| R1 | 调试区 partial 搬 asmdef 后 `Debug*ForTest` 访问主文件私有成员失败 | 第一步只剪切文件保 partial（零风险）；asmdef 化第二步单独提交，用 `InternalsVisibleTo` + 私有→internal 最小改动清单 |
| R2 | `GetEnemiesInRange` 共享缓冲语义在搬迁中被无意破坏（调用方开始持有返回值） | 接口 XML 注明契约 + 加 Debug 断言（域内深度计数）；P1 的调用方清单（7 处）入迁移核对表 |
| R3 | 波次协程与 `EnterLevelInPlace` 生命周期（P0 修复的软锁族）回归 | S6 必须复用 P0 的 `ResetRunState` 统一入口；autoplay 基线 + Retry/切关/Back 三路径手测 |
| R4 | 敌人池化（冻结期三项之一）与 S5 注册表改造互踩 | **顺序建议：拆分 S1-S4 → 敌人池化 → S5-S7**（池化的四条销毁路径需要稳定的注册表边界；反向则池化返工） |
| R5 | 数值外置（campaign schema）与 S4/S6 的参数字面量随迁冲突 | **数值外置先行于 S4/S6**（外置后工厂/波次读配置资产，搬迁时不再搬字面量）；与对账表"先小批清障后拆分"排序一致 |
| R6 | 多会话并发编辑（本项目常态）下大搬迁冲突 | 每步 ≤1 天粒度提交；搬迁文件加入会话间"认领"约定（本文件谁在动，其他人不动） |
| R7 | 帧时序敏感（TD-WINDUP-001 教训）：搬迁即重编译，autoplay 单种子轨迹可能漂移 | 基线比对用 3 种子×2 局中位数；单波次差异不判失败（时序噪声），胜负/完整度大梯度才回查 |
| R8 | 平衡热修（护甲帽类）在拆分窗口插入 | 冻结期红线：平衡改动优先，拆分暂停让行（对账表已排"平衡闭环后启动"） |

### 冻结期三项执行顺序（对账表排期建议的落地版）

**小批清障（已完成）→ 数值外置（S-1，~2d）→ 拆分 S1-S4 → 敌人池化（~2d）→ 拆分 S5-S7**

---

## 附：验证资产清单（现有可复用）

- `tools/_build_verify.py`（单会话重试 + 全新目录 + 状态轮询）
- `tools/_windup_ab.py`（autoplay 种子 A/B，S0 基线直接复用）
- `tools/qa_gamepad_acceptance.py`（QA 终验驱动器，纯手柄回归）
- EditMode 全量（当前 44 用例：护甲 9 + windup 6 + 经济/塔/闪避 29）+ release 符号抽查清单（P3 报告）
