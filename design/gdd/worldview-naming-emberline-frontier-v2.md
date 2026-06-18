# Emberline Frontier 命名总表 v2

> **Date**: 2026-05-19  
> **Status**: Locked for Campaign Production (20-level scope)  
> **Supersedes**: `design/gdd/worldview-naming-emberline-frontier-v1.md`

## 1. 势力与组织
| 类别 | 中文名 | 英文名 | 用途 |
|---|---|---|---|
| 阵营 | 铁道防卫局 | Emberline Defense Bureau | 玩家阵营主称 |
| 阵营 | 荒原异种群 | Wasteland Brood | 敌对总称 |
| 组织 | 枢纽调度署 | Junction Dispatch Office | 任务播报 |
| 组织 | 轨道工务队 | Railworks Corps | 教学文本 |
| 组织 | 灰烬后勤团 | Ash Logistics Unit | 经济与补给说明 |

## 2. 战役塔命名（8塔）
| 中文名 | 英文显示名 | 英文ID | 定位 |
|---|---|---|---|
| 轨道长枪塔 | Rail Lancer Tower | `rail_lancer_tower` | 单体点杀 |
| 煤爆迫击塔 | Cinder Mortar Tower | `cinder_mortar_tower` | 范围清杂 |
| 冷凝线圈塔 | Frost Coil Tower | `frost_coil_tower` | 减速控制 |
| 电弧焊枪塔 | Arc Welder Tower | `arc_welder_tower` | 连锁清线 |
| 破甲钻机塔 | Siege Drill Tower | `siege_drill_tower` | 抗甲破防 |
| 灰烬高炮塔 | Ember Flak Tower | `ember_flak_tower` | 反快怪突发 |
| 共振信标塔 | Resonance Beacon Tower | `resonance_beacon_tower` | 战术辅助 |
| 重力缠缚塔 | Grav Snare Tower | `grav_snare_tower` | 终局控场 |

## 3. 战役敌人命名（12敌）
| 中文名 | 英文显示名 | 英文ID | 定位 |
|---|---|---|---|
| 掠行虫 | Skitter Runner | `skitter_runner` | 低血高速 |
| 灰潮虫群 | Ash Swarm | `ash_swarm` | 低血群体 |
| 甲壳重兽 | Carapace Brute | `carapace_brute` | 高血低速 |
| 护甲孢体 | Plated Spore | `plated_spore` | 减伤推进 |
| 潜轨爆破体 | Burrow Sapper | `burrow_sapper` | 突袭单位 |
| 灰烬渗漏体 | Ember Leech | `ember_leech` | 资源惩罚 |
| 孢囊载体 | Spore Carrier | `spore_carrier` | 分裂群压 |
| 轨道卫士 | Rail Warden | `rail_warden` | 支援增益 |
| 炉渣滑翔体 | Cinder Glider | `cinder_glider` | 侧袭快怪 |
| 燃核巨壳 | Husk Titan | `husk_titan` | 精英重压 |
| 回响拟态体 | Echo Mimic | `echo_mimic` | 形态复制 |
| 终炉母体 | Furnace Matriarch | `furnace_matriarch` | 终局Boss |

## 4. 战役地图命名（5图）
| 中文名 | 英文显示名 | 地图ID | 用途 |
|---|---|---|---|
| 灰线枢纽 | Grayline Junction | `grayline_junction` | 关卡 1-4 |
| 灰烬货场 | Ashfall Depot | `ashfall_depot` | 关卡 5-8 |
| 裂轨峡谷 | Split-Switch Canyon | `split_switch_canyon` | 关卡 9-12 |
| 空窑盆地 | Hollow Kiln Basin | `hollow_kiln_basin` | 关卡 13-16 |
| 终焰终点 | Last Ember Terminus | `last_ember_terminus` | 关卡 17-20 |

## 5. 波次与事件术语
| 中文术语 | 英文术语 | 说明 |
|---|---|---|
| 试探波 | Probe Wave | 教学引导波 |
| 压线波 | Pressure Wave | 常规挑战波 |
| 考试波 | Exam Wave | 机制综合考核 |
| 高压波 | Overload Wave | 难度峰值 |
| 缓冲波 | Breather Wave | 调整窗口 |
| 裂轨警报 | Railbreak Alert | 高威胁预警 |
| 共振窗口 | Resonance Window | 记忆点战斗窗口 |
| 过载指令 | Overdrive Command | 共振爆发命令 |
| 重调指令 | Retune Command | 共振稳线命令 |

## 6. UI/系统文案关键名词
| 中文 | 英文Key | 用途 |
|---|---|---|
| 线路完整度 | `line_integrity` | 生命值语义替换 |
| 防卫预算 | `defense_budget` | 金币语义替换 |
| 本波威胁 | `wave_threat` | 波次提示 |
| 漏防原因 | `breach_reason` | 失败标签 |
| 立即发车 | `dispatch_now` | 提前开波按钮 |
| 共振充能 | `resonance_charge` | 共振条文本 |
| 共振命令 | `resonance_command` | 共振指令按钮 |

## 7. Naming Rules
1. ID 统一 snake_case，不包含空格与连字符。
2. 显示名可本地化，ID 不可本地化。
3. 新增塔/敌/图必须先登记本表，再进入配置与美术流程。
