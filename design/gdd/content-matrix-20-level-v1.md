# Emberline Defense 内容矩阵 v1（20关支撑版）

> **Version**: v1  
> **Date**: 2026-05-19  
> **Status**: Draft for Production  
> **Parent Plan**: `design/gdd/full-project-plan-emberline-defense-v2.0.md`
> **Naming Source**: `design/gdd/worldview-naming-emberline-frontier-v2.md`
> **Art Prompt Source**: `design/gdd/image2-master-prompt-emberline-frontier-v2.md`

## 1. 内容最低门槛（首发）
1. 战役关卡：20 关（4 章节，每章 5 关）。
2. 可用塔：8 座（全部可升级，3阶+2分支）。
3. 敌人类型：12 种（含 2 个章节级精英/Boss 单位）。
4. 地图数量：5 张（每图承载 4 关，形成明确地形记忆点）。

## 2. 塔内容池（8塔，全部可升级）
| 英文ID | 塔名 | 职责 | 解锁章节 | 分支A（Damage） | 分支B（Utility） |
|---|---|---|---|---|---|
| `rail_lancer_tower` | 轨道长枪塔 Rail Lancer | 单体点杀 | Chapter A | 高倍率重击 | 射程与锁敌稳定 |
| `cinder_mortar_tower` | 煤爆迫击塔 Cinder Mortar | 范围清杂 | Chapter A | 爆心伤害与边缘衰减优化 | AOE半径与命中上限 |
| `frost_coil_tower` | 冷凝线圈塔 Frost Coil | 控制减速 | Chapter A | 伤害+攻速 | 减速强度+持续时间 |
| `arc_welder_tower` | 电弧焊枪塔 Arc Welder | 连锁清线 | Chapter B | 连锁伤害叠加 | 连锁跳数与导电标记 |
| `siege_drill_tower` | 破甲钻机塔 Siege Drill | 抗甲破防 | Chapter B | 破甲穿透与斩杀阈值 | 护甲削减持续与范围 |
| `ember_flak_tower` | 灰烬高炮塔 Ember Flak | 反快怪突发 | Chapter C | 短时爆发弹幕 | 拦截优先级与击退 |
| `resonance_beacon_tower` | 共振信标塔 Resonance Beacon | 战术辅助 | Chapter C | 共振窗口爆发增幅 | 共振充能效率与稳定 |
| `grav_snare_tower` | 重力缠缚塔 Grav Snare | 终局控场 | Chapter D | 区域坍缩伤害 | 区域束缚与减速链 |

### 塔设计约束
1. 每塔必须具备“可被替代但不可被无脑替代”的定位。
2. 任意章节至少需要 3 种塔组合可通关，避免单解。
3. 所有塔在对应章节解锁后 2 关内必须有一次“高价值登场”。

## 3. 敌人内容池（12敌）
| 英文ID | 敌人 | 类型标签 | 首次出现 | 主要压力 | 反制提示 |
|---|---|---|---|---|---|
| `skitter_runner` | 掠行虫 Skitter Runner | fast,light | 1 | 穿越覆盖空区 | 提升覆盖与减速 |
| `ash_swarm` | 灰潮虫群 Ash Swarm | swarm,light | 2 | 群体堆压 | AOE清杂 |
| `carapace_brute` | 甲壳重兽 Carapace Brute | heavy,armored | 6 | 高血拖线 | 单体持续输出 |
| `plated_spore` | 护甲孢体 Plated Spore | armored,mid | 7 | 固定减伤 | 破甲与高倍率 |
| `burrow_sapper` | 潜轨爆破体 Burrow Sapper | fast,special | 8 | 中途突袭 | 提前拦截与控制 |
| `ember_leech` | 灰烬渗漏体 Ember Leech | attrition,special | 9 | 漏防额外资源惩罚 | 优先点杀 |
| `spore_carrier` | 孢囊载体 Spore Carrier | swarm,spawn | 10 | 死亡分裂 | 连锁与范围压制 |
| `rail_warden` | 轨道卫士 Rail Warden | support,armored | 11 | 邻近护盾增益 | 先杀支援单位 |
| `cinder_glider` | 炉渣滑翔体 Cinder Glider | fast,flank | 13 | 节奏错位突入 | 反快怪火力与控场 |
| `husk_titan` | 燃核巨壳 Husk Titan | elite,heavy | 15 | 单体高压峰值 | 破甲+爆发窗口 |
| `echo_mimic` | 回响拟态体 Echo Mimic | special,mixed | 17 | 复制威胁形态 | 读波与动态切塔 |
| `furnace_matriarch` | 终炉母体 Furnace Matriarch | boss,final | 20 | 综合终局压制 | 全体系协同 |

### 敌人设计约束
1. 相邻两关不允许只做“同敌人加数量”升级。
2. 每 5 关至少引入 1 个新行为机制（非纯数值）。
3. 精英/Boss 关必须给出明确可见的威胁提示与反制窗口。

## 4. 地图内容池（5图 / 20关）
| Map ID | 地图名 | 承载关卡 | 地形记忆点 | 战术特征 |
|---|---|---|---|---|
| `grayline_junction` | 灰线枢纽 Grayline Junction | 1-4 | 单主轨+短弯道 | 基础覆盖教学 |
| `ashfall_depot` | 灰烬货场 Ashfall Depot | 5-8 | 双长直段+高压拐角 | 单体与AOE分区 |
| `split_switch_canyon` | 裂轨峡谷 Split-Switch Canyon | 9-12 | 分合流节点 | 反制与资源调度 |
| `hollow_kiln_basin` | 空窑盆地 Hollow Kiln Basin | 13-16 | 环形中庭+回折路径 | 控场与窗口爆发 |
| `last_ember_terminus` | 终焰终点 Last Ember Terminus | 17-20 | 多段推进终端区 | 终局综合考试 |

### 地图设计约束
1. 每张地图必须有“1句可记住”的战术关键词（例：分合流、环形中庭）。
2. 同章节 5 关内，至少 2 关使用同图不同波次结构（提升内容复用效率）。
3. 关卡18-20地图必须支持高压混编可读性（视觉与路径分层）。

## 5. 20关内容投放节奏（塔/怪/图）
| 关卡 | 地图ID | 新增塔ID | 新增敌ID | 考点 |
|---|---|---|---|---|
| 1 | `grayline_junction` | `rail_lancer_tower` | `skitter_runner` | 覆盖基础 |
| 2 | `grayline_junction` | `cinder_mortar_tower` | `ash_swarm` | AOE识别 |
| 3 | `grayline_junction` | `frost_coil_tower` | - | 三塔协同 |
| 4 | `grayline_junction` | - | - | 章节A考试 |
| 5 | `ashfall_depot` | - | - | 过渡高压 |
| 6 | `ashfall_depot` | `arc_welder_tower` | `carapace_brute` | 重装引入 |
| 7 | `ashfall_depot` | `siege_drill_tower` | `plated_spore` | 抗甲反制 |
| 8 | `ashfall_depot` | - | `burrow_sapper` | 突袭应对 |
| 9 | `split_switch_canyon` | - | `ember_leech` | 经济惩罚识别 |
| 10 | `split_switch_canyon` | - | `spore_carrier` | 中盘考试 |
| 11 | `split_switch_canyon` | `ember_flak_tower` | `rail_warden` | 支援单位优先级 |
| 12 | `split_switch_canyon` | `resonance_beacon_tower` | - | 分支策略形成 |
| 13 | `hollow_kiln_basin` | - | `cinder_glider` | 反快怪压力 |
| 14 | `hollow_kiln_basin` | - | - | 连续考试 |
| 15 | `hollow_kiln_basin` | - | `husk_titan` | 精英波决策 |
| 16 | `hollow_kiln_basin` | `grav_snare_tower` | - | 控场体系成型 |
| 17 | `last_ember_terminus` | - | `echo_mimic` | 动态读波 |
| 18 | `last_ember_terminus` | - | - | 高压混编A |
| 19 | `last_ember_terminus` | - | - | 高压混编B |
| 20 | `last_ember_terminus` | - | `furnace_matriarch` | 终局Boss考试 |

## 6. 生产拆解（内容资产量）
### 6.1 塔资产
1. 8 塔基础立绘/图标/放置态。
2. 每塔至少 1 套升级视觉变化（T0 与 T3可辨识）。
3. 每塔2个分支的文案与UI收益描述。

### 6.2 敌人资产
1. 12 敌基础动画（移动、受击、死亡）。
2. 精英/Boss 增加专属警示反馈。
3. 特殊机制敌（E05/E07/E11）补充行为提示特效。

### 6.3 地图资产
1. 5 图基础地表+路径+氛围层。
2. 每图 1 组独特环境识别物（视觉记忆锚点）。
3. 每图 4 关波次配置模板（共20关）。

## 7. 验收口径（内容充分性）
1. 玩家在第 10 关前可明确区分至少 5 种敌人威胁。
2. 玩家在第 15 关前完成至少 4 次有效升级分支决策。
3. 玩家在第 20 关前实际使用过至少 6 座不同塔。
4. 测试样本中，至少 70% 玩家能说出 3 张以上地图的战术差异。

## 8. 与现有系统衔接
1. 当前已实现的 3塔4敌作为 Chapter A/B 基础池继续沿用。
2. 新增内容按章节逐步并入，不一次性灌入，降低调参风险。
3. 波次数据继续采用 JSON 配置驱动，保持现有工具链兼容。
