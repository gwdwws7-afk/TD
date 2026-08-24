# 扩充实施映射 v1（Expansion Implementation Mapping）

> 来源：v11 计划任务 3（代码会话，doc-only）。把四份执行级文档逐条映射到**现有代码的落点、单测与风险**——冻结结束即可热启动，实施期间零"读规格"成本。
> 输入：`expansion-tower-sheets-v1.md`（塔数值）、`rail-barricade-behavior-spec-v1.md`（轨障契约）、`boss-design-spec-v1.md`（Boss 相位）、`content-matrix-20-level-v2.md`（投放矩阵）、主规格 §1/§6（红线与顺序）。
> 代码锚点以 HEAD（`7453572` 谱系，19,800 行主文件）为准；拆分 S0-S7 完成后行号会漂移，**按符号名定位**。

---

## 0. 连带工程清单（批 1 先行半天的 12 塔扩容）

| 扩容点 | 现有落点（符号） | 新增内容 | 风险 |
|---|---|---|---|
| 塔种枚举 8→12 | `TDTowerKind`（TDTower.cs 顶部） | 追加 SlagBurner/SalvageDerrick/RailBarricade/LongRailCannon（**顺序必须是 9-12**，`(int)kind` 数组到处索引） | R-C1：所有 `(int)kind` 索引数组必须同步 12 长（见下） |
| 径向菜单数组 | `TDRadialTowerMenu.TowerColors[8]`、`TowerShortNames[8]` | 各 +4（颜色取 expansion-visual-identities-v1 的身份色） | 越界即 IndexOutOfRange，编译期不报 |
| 身份色表 | `TDUiVisualIdentity.GetTower(kind)` switch | +4 条目（iconResourcePath 先占位回退） | 美术图标未到时须走 fallback 色块（现有行为已如此） |
| 图鉴/本地化 | `TDTower` codex 观察枚举、`TDLocalization` 替换表、`GetFormationTowerRole` | +4 英文串 + 中文替换对 | 键名治理冻结期项——新串直接进 strings.json |
| autoplay 优先表 | `GetP124TowerPriority`（P124）、`ConfigureP124Formation` | 12 塔进优先序（轨障车排末位——autoplay 不依赖车体经济） | 新塔不进表 = autoplay 永不建造（零形成伤害式 bug，矩阵会暴露） |
| 平衡模拟器 | `TDBalanceSimulator`（自动化域）塔档案表 | +4 行基线（矩阵重校前先粗估） | 不加则矩阵对新塔零覆盖 |
| 平衡回归 | `TDTowerBalanceTests` / `TDEconomyTests` | 四塔基态钉值（费/射程/sps/伤） | 与 windup 锁表同模式 |

**R-C1 核对清单（编译不报的索引数组）**：`TDRadialTowerMenu.TowerColors/TowerShortNames`、`TDGameManager.GetEnemySortingOrder/GetEnemyVisualMaterial 等塔侧 switch`（switch 是安全的，危险的是**数组**）、`TDUiP132Art` 图标表、`TDBalanceSimulator` 内任何按 kind 索引的数组。全库 grep `(int)kind` / `(int)towerKind` 逐个核对。

---

## 1. 塔表逐行映射（expansion-tower-sheets-v1 → 代码落点 + 单测）

### 塔 9 · 炉渣喷灯（DoT 叠层）

| 规格行 | 落点 | 说明 |
|---|---|---|
| 基态 50 费/2.2 射程/1.1sps/直击 8/弹速 8.0 | `TDTower.CreateBaseState` 新 case | 与现有 8 塔同构 |
| 蓄力 0.18s | `TowerState.windupDuration = 0.18f` + **TDTowerWindupTests 锁表 +1 行**（0.18 < 1/1.1 间隔 0.909 ✓ 不变量保持） | |
| 灼烧 6 层 ×2.0/s ×3.0s | **独立文件 `TDBurnSystem.cs`**（纯静态，红线）+ `TDEnemy` 新私有槽 `_burnLayers/_burnTimer/_burnTickAccumulator`（独立于 slow/expose——设计注记明示不复用） | 跳伤在 TDEnemy.Update 按 0.5s tick 结算 |
| 跳伤吃固定减伤不吃百分比 | `TDCombatMath.ResolveBurnTick(rawTick, enemyArmorFlat, armorBreakFlat)` 纯函数：`max(1, rawTick - max(0, armor - break))`，**不走 ResolveArmoredDamage** | **单测（新 TDBurnTests）**：`ResolveBurnTick(2, 9, 0) → 1`（高甲仍是墙）；`ResolveBurnTick(2, 9, 6) → 1→ max(1, 2-3)=1… 破甲后仍 1`——注意设计哲学是"吃固定减伤"，即 2 伤对 9 甲必 1；破甲价值在直击不在跳伤——**断言草稿见 §1.5** |
| D 线：直击 +18%/灼烧每层 +15% | `TDTower.ApplyDamageBranch` case SlagBurner | 每 tier 乘算，同现有格式 |
| U 线：时长 +0.4s/蔓延半径 +15% | `TDTower.ApplyUtilityBranch` case | 蔓延半径进 TowerState 新字段 `burnSpreadRadius`（默认 1.0 格） |
| 专精 Slag Sump（D2 满层爆发） | `TDTower.GetSpecializationDefinition` + 爆发结算进 `TDBurnSystem.DetonateFullStacks(enemy, perLayerDamage)`（纯函数算伤害，命中走 GetModifiedDamageForEnemy 管线） | 爆发 = 层数 × 每层伤害 × 2.0、清层 |
| 专精 Wildfire Drift（U2 击杀传染） | `TDEnemy.ResolveKill` 钩子：若 target 带灼烧且源塔有该专精 → `TDBurnSystem.SpreadOnDeath`（2 格 ≤3 目标各 2 层） | 复用 `GetEnemiesInRange`（**注意 P1 共享缓冲契约：传染目标列表必须立即消费**） |

### 塔 10 · 捞轨吊机（经济光环）

| 规格行 | 落点 |
|---|---|
| 基态 44 费/1.8/0.9sps/直击 5/光环 2.5 格 +18% | CreateBaseState + `TowerState` 新字段 `killBountyAuraRadius/bountyBonusPercent`（默认 2.5/0，本塔基线 18% 进 baseState） |
| 光环判定 | **不新增每帧扫描**——在 `NotifyEnemyKilled` 结算点反向查询：击杀坐标 2.5 格内是否存在持有光环的吊机（复用 `_towerStats`？否——用现有 `FindObjectsByType<TDTower>` 一次性？**更好：维护吊机注册表**（建塔/卖塔时增删的小 List，光环查询 O(吊机数)），P1 光环缓存同模式 |
| D 线保底打捞 +6/级 | `ApplyDamageBranch` + 波结算点（`WaveLoopFromConfig` 清波奖励处）追加 `TrackP125` 同款记账 |
| U 线光环 +12%/击杀返还 +1 | `ApplyUtilityBranch` |
| 专精 Scrap Protocol ×1.5 Boss 赏金 | `NotifyEnemyKilled` 的 reward 计算点（`TDEconomyTuning.ScaleCombatBounty` 之后乘） |
| 专精 Supply Drop 每波 +3 | 波开始点（`BeginWaveStat` 附近） |
| **平衡红线：单波增量 ≤45** | **单测（TDEconomyTests 扩展）**：`DerrickWaveIncomeCeiling`——构造最大配况（T3 全线 + 双专精 + 光环满编），断言 `perWave增量 <= 45`；公式纯函数化进 `TDEconomyTuning`（`ResolveDerrickWaveIncome(dLine, uLine, specialization, auraKills) → int`），测试直测函数。终局结余 ≤999 判据归 QA 矩阵（联算口径 p12.5.0 已有） |

### 塔 11 · 轨障车（拦截——批 1 最重项）

**独立文件 `TDBlockerWagon.cs`**（MonoBehaviour，meta 先例红线）+ `TDEnemy` 状态机扩展。契约 §5 五项单测验算草稿见 §2。

| 规格行 | 落点 |
|---|---|
| 塔放置生成车体 | `TDGameManager.HandleRadialTowerSelected`→`SpawnTower` 成功后：若 kind==RailBarricade 且格子邻接路段 → `TDBlockerWagon.SpawnFor(tower, nearestTrackPoint)`；**每路段至多一个**（路段键查重） |
| 车体 HP240/甲4、随塔升级 | 车体读 `tower.TowerState` 派生（armorUpgrades → 车甲）；塔升级时车体同步（监听或每帧拉取——**选每帧拉取**，避免事件接线） |
| Engaged 状态机 | `TDEnemy` 新增：`_engagedWagon` 引用 + 接战计时器；`Update` 行进分支前插 `if (_engagedWagon != null && _engagedWagon.IsAlive)` → 停止移动 + 每 1.2s `wagon.TakeHit(lineDamage)`；车体毁 → 引用清空恢复行进（寻路不改动 ✓） |
| 接战容量 ≤2 + 排队 | `TDBlockerWagon.TryEngage(enemy)`：`_engaged.Count < 2` → 入列；否则 enemy 标记 `_queuedWagon = wagon`（站住等待，**不改路径**） |
| 绕行者清单 | `TDBlockerWagon.CanBypass(enemy)` 硬编码：`burrow_sapper`（潜行）、`cinder_glider`（飞越）、`HasTag("boss")`（碾压路径）、`ash_swarm` 计数取模（同一车体每放行 4 只漏 1 → 车体持计数器） |
| Boss 碾压 | Boss 接触车体：`wagon.DestroyByCrush()`（一击毁）+ Boss `ApplyStagger`-like 停顿 3s（**复用 stagger 槽**，boss 免疫 stagger 的例外在此显式打破——契约点名） |
| 荆棘反伤 | 接战 tick 时反向 `enemy.TakeHit(thorns, 0, 0, sourceTower: 塔引用)`——**TakeHit 走护甲管线但契约说"不吃护甲减免（固定伤）"** → 走 `ResolveBurnTick` 同款固定减伤函数或直伤通道；对 Shield 免疫前 3 次的骑兵：`TakeHit` 现有护甲/伤害入口需检查 Shield 语义落点（批 2 骑兵实现时统一） |
| 25s 重建 / Holding Order 15s | 车体 Destroy 后协程计时重建（塔仍活着才重建） |
| U 线减速场 1.5 格 | 光环缓存模式（P1 `_supportEnemiesCache` 同款：脏标记列表 + 0.2s TTL）——**不每帧扫描** |
| 嘲讽脉冲 | Holding Order 专精：3s 一拍把 2.5 格敌人 `_engagedWagon` 指到车体（容量照限） |
| 出售=撤收 | `TrySellTower` 现有清理链 + `wagon.Retract()`（即时消失） |
| autoplay 出售守卫 | `TrySellP124IdleTowers` 的零贡献判定前加：`kind == RailBarricade → 车体._engaged.Count == 0 才可卖`；且车体**不入**塔 DPS 统计（`GetOrCreateTowerStat` 对车体不调用） |

### 塔 12 · 远程轨道炮（穿透狙击）

| 规格行 | 落点 |
|---|---|
| 基态 72 费/4.8/0.4sps/34 伤/蓄力 0.50s | CreateBaseState + windup 锁表 +1（0.50 < 1/0.4=2.5 ✓） |
| 直线穿透衰减 ×0.7 | **新投射物行为**：`TDProjectile` 不再锁定单目标——命中 `_target` 后沿方向向量继续飞行 `rangeRemaining`，逐敌人结算 `damage × 0.7^n`。实现选 **射线检测一次性结算**（发射瞬间沿方向 segment cast 全部敌人，逐个衰减）而非实体穿行——与现有弹道视觉解耦，`TDCombatMath.ResolvePierceDamageChain(baseDamage, falloff, targetCount)` 纯函数出整数序列（向下取整、地板 1） |
| **单测**：`ResolvePierceDamageChain(34, 0.7, 5)` 钉死序列 34/23/16/11/7（floor）；Full Bore 专精 falloff=1.0 且末位 ×1.3 | 进 TDCombatMath 测试 |
| 30% 快速敌闪避 | 现有 `EvadeableFastEnemyMissChance` 体系：0.4sps ≤ 1.0 → 基础曲线顶端 0.30 ——**无需新码**，`TDCombatMath.FastEnemyMissChance(0.4, 0)` 恰好 0.30（曲线 Lerp(0.18,0.30, InverseLerp(1.0,0.5,0.4))=0.30 ✓ 复核）→ **单测钉住这个巧合**：`FastEnemyMissChance(0.4f, 0f) == 0.30f`，防未来曲线改动破坏身份弱点 |
| Ballistic Lead 消闪避 | `TDEnemy.TakeHit` 闪避判定处：`sourceTower.HasSpecialization(BallisticLead)` → 跳过（或 TowerState 出 flag）；每波首发必中+标记：塔持 `_waveFirstShot` 状态 |
| 弹速 14 | CreateBaseState |

### 1.5 四塔单测断言草稿汇总（新增三个测试文件）

```
TDBurnTests:
  BurnTick_FlatArmorOnly:       ResolveBurnTick(2, 9, 0) == 1   // 9 甲墙仍在
  BurnTick_ArmorBreakApplies:   ResolveBurnTick(2, 9, 6) == 1   // 破甲不吃百分比哲学下仍 1；文档化
  BurnTick_Floor:               ResolveBurnTick(1, 0, 0) == 1
  StackCap_Six:                 ClampStacks(7) == 6
  Detonate_ClearsStacks:        Detonate 后层数归零、伤害 = 层数×每层×2.0
  Spread_BoundedThree:          传染目标 ≤3、各 +2 层
TDDerrickTests（并入 TDEconomyTests 或独立）:
  WaveIncomeCeiling_T3DualSpec: ResolveDerrickWaveIncome(max) <= 45
  Aura_BountyPercent:           18% 基线 → +18%（构造击杀走纯函数）
TDPierceTests（并入 TDCombatMath 测试）:
  Chain_Falloff07:              [34,23,16,11,7]
  Chain_FullBore:               falloff 1.0 + 末位 ×1.3
  Cannon_EvadeIdentity:         FastEnemyMissChance(0.4,0)==0.30（身份弱点钉值）
TDWindup 锁表追加:               SlagBurner 0.18 / Derrick 0.24 / Cannon 0.50（轨障无 windup 不进表）
```

---

## 2. 轨障契约五项单测验算草稿（契约 §5）

单测在 EditMode 直测 `TDBlockerWagon` 纯逻辑（把接战判定/绕行/碾压拆成可无场景调用的静态或实例方法，MonoBehaviour 只做壳）。**这是实现的形状约束：逻辑必须可脱离场景测**。

```
1) 接战容量:
   wagon.EngageCapacity == 2
   TryEngage(e1)=true, TryEngage(e2)=true, TryEngage(e3)=false(入队)
   e3.IsQueuedOn(wagon) == true 且 e3 位移为零
   车体 Destroy → e1/e2/e3 全部恢复 Moving（engaged/queued 引用清空）

2) 绕行者清单:
   CanBypass(burrow_sapper)==true; CanBypass(cinder_glider)==true
   CanBypass(skitter_runner)==false; CanBypass(ash_swarm, passIndex:0..3)==false,false,false,true(第4只)
   其余新敌（echo_brood/forge_dragon/acid_blister…）==false（契约点名全接战）

3) Boss 碾压:
   CanBypass(any Boss)==true 但走 Crush 路径: wagon.HP=240 → CrushBy(boss) 后 IsAlive==false
   boss.StaggerTimer ≈ 3s（碾压停顿）; 重建计时启动

4) 波清空判定:
   WaveClearedCondition(enemies=[engagedA, engagedB]) == false   // 接战者不计清空
   wagon destroyed → 同列表 == true
   （落点：TDGameManager 波循环 while(_activeEnemies.Count>0) 处——接战者仍在 _activeEnemies ✓ 现有行为天然满足；单测钉的是"别在清空判定里排除接战者"这个未来改动的回归）

5) autoplay 出售守卫:
   TrySellP124IdleTowers: 轨障塔+车体 engaged=2 → 不卖
   车体毁（engaged=0）→ 按零贡献口径评估可卖
   车体不入 DPS 统计: GetOrCreateTowerStat(wagon) 不被调用（以统计字典无车体 id 断言）
```

---

## 3. Boss 状态机草图（boss-design-spec-v1）

共同基座（四 Boss 共用，独立文件 `TDBossPhases.cs` 纯逻辑 + TDEnemy 薄钩子）：

```
BossRuntime (per-boss, TDEnemy 持有):
  phaseIndex: int        // 0=基线, 1+=相位序号
  phaseTimer / nextActionTimer
  OnHealthRatioCrossed(threshold) -> 相位迁移事件（一次性）

迁移表（规格 → 状态机）:
装卸兽:   [100%..50%): P1 重装(甲10/速0.5) + throwContainerTimer 12s
              动作: 随机授权建造格封锁 10s（落点: TDGridMap 建造格掩码 + UI 提示；复用 IsRecommendedBuildCell 排除）
          [50%..0]:  P2 卸甲狂奔（armor 10→2, speed×1.6, lineDamage 3→5）——一次性迁移 + 卸甲 FX
暴君:     P1: rerouteTimer 15s → 切换到另一 lane 等价进度点
              （落点: _activeLanePaths 车道切换 + RouteProgress01 保持——TDEnemy 换 path 引用）
          35%: Split() —— 生成第二实体（同 enemyId 或 clone 体）各 50% HP/甲4/速0.85 分走两线;
              分裂瞬间 ClearDebuffs(both)（标记/暴露/破甲清空——防白嫖）
看守:     stackArmorTimer 10s → armorBonus+1 (上限 +8; 复用 armorBreak 反向槽: armorBreakFlat -= 1, 即规格注记的负破甲)
          70%/35%: Summon(6×ash_swarm + 2×plated_spore, 从后排入口)
          KilnPurge 命中: armorBonus -5 且 stackPause 8s（场景技交互——落点 TryActivateScenarioMechanic 的 kiln 分支）
先驱:     战斗开始: ReadMostBuiltTowerKind(_towerStats) → mimicProfile
          70%/35%: ClearAllDebuffs(self) + 改拟态第 2/3 多塔种
          拟态行为表: 每 6s 一拍自体 buff（速×1.8/塔减速场/免疫减速/甲+3/减伤盾/共振暂停）
              ——全部为 TDEnemy 自体效果，零"敌人攻击塔"新路径（除共振暂停走 TDGameManager 充能冻结 flag）
```

**落点风险**：分裂双体（暴君）是唯一"生成新敌人实体"的 Boss 行为——复用 `SpawnSplitChildren` 协程模式（L 现有分裂敌先例）；拟态读取 `_towerStats`（规格注记点名现成）。

---

## 4. 批间接缝（§6.4 四批的实施顺序依赖）

```
批 0 连带扩容（半天）
  └─ 12 枚举/数组/图鉴/优先表 —— 四塔实现的编译前提
批 1 塔 ×4
  ├─ 灼烧系统（塔9）──────────┐
  ├─ 光环经济（塔10）          ├─ 批 2 依赖: 骑兵 Shield 与灼烧跳伤的免疫交互、
  ├─ 轨障车（塔11）←最重       │   傀儡余烬堆与吊机光环的击杀位经济
  └─ 穿透弹道（塔12）          ┘   先驱拟态行为表引用塔 9/12 的身份分类
批 2 敌 ×6 + Boss ×4
  └─ 行为全部挂在批 1 的系统上（灼烧/光环/标记/穿透）；Boss 相位基座独立
批 3 波次重织（纯数据）
  └─ 依赖批 2 的 enemyId 进目录；语法工具链现成；新敌首现波纯净规则
批 4 全矩阵重校（QA 主导）
  └─ 依赖 1-3 全落；保留判据含 meta-0 自洽；吊机经济门以"含吊机局"复跑
```

**关键顺序约束**：
1. 轨障车必须在批 1 内**最后**做（其敌人 Engaged 状态是 TDEnemy 侵入式改动，先落三塔再动车体减冲突面）；
2. 骑兵 Shield（批 2）与轨障荆棘的免疫语义在批 1 实现荆棘时就要预留（TakeHit 的 shield 检查点先留 hook）；
3. 波次重织前跑一次现有矩阵存基线（主规格 §4 要求的对照档）。

---

## 5. 风险登记（扩充实施新增）

| # | 风险 | 缓解 |
|---|---|---|
| R-E1 | `(int)kind` 数组 12 扩容漏改（编译不报） | §0 的 R-C1 全库核对清单 + 单测遍历 12 kind 钉数组长度 |
| R-E2 | 灼烧跳伤误走百分比护甲 → 高甲下喷灯变超模 | TDBurnTests 钉死固定减伤通道 + 矩阵焦点局 |
| R-E3 | 吊机光环每帧扫描回潮（P1 修过的模式） | 吊机注册表 + 击杀点反向查询，实现注记已写进映射 |
| R-E4 | 轨障车与敌人状态机的死亡竞态（车毁同帧敌死） | Engaged 引用双向清理在两处 Update 各自防御；契约单测 1 覆盖 |
| R-E5 | Boss 分裂实体逃逸路径（_activeEnemies 之外的生成） | 复用 SpawnSplitChildren 通道（已在 _activeEnemies 内注册） |
| R-E6 | 12 塔优先表缺新塔 → autoplay 零出场（Siege 配额同款"成员为零"复发） | 连带清单点名 GetP124TowerPriority 必改 + 矩阵首跑即暴露 |
| R-E7 | 冻结拆分（S5 CombatServices 敌人注册表改造）与 Engaged 状态的接缝 | **轨障车在拆分后才实施**（§6 约束本就如此）——Engaged 落在拆分后的注册表边界内，R4 排期已含 |
| R-E8 | 单波 ≤45 红线在 T3 双专精 + 光环满编下被穿透 | 纯函数化 + T3 满配单测；矩阵"含吊机局"复跑是第二道闸 |
