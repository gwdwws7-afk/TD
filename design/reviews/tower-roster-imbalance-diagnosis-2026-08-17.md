# Tower Roster Imbalance Diagnosis: SiegeDrill / GravSnare（2026-08-17）

> 任务来源：session-plan-design-2026-08-17.md 任务 3（P1 侦查项）
> 方法：p13.5 证据复算 + 波次 JSON 生态位密度分析 + autoplay AI 决策代码审查 + 拆塔返还新变量评估
> 结论性质：诊断 + 最小改法清单，**不含任何数值改动**

## 一、证据基线（复算自 output/playtest/p135_release_v1）

`p135_tower_usage.csv`（180 主样本 + 10 焦点局）派生指标：

| 塔 | 出场率 | 建塔数 | 伤害/塔 | 控制/塔 | 零贡献率 |
|---|---:|---:|---:|---:|---:|
| EmberFlak | 16.7% | 85 | 8,287 | 0 | 17.6% |
| CinderMortar | 71.7% | 261 | 5,116 | 0 | 21.5% |
| GravSnare | 8.3% | 45 | 5,220 | 375 | 31.1% |
| ArcWelder | 51.7% | 260 | 4,850 | 0 | 19.2% |
| ResonanceBeacon | 28.9% | 96 | 4,774 | 435 | 19.8% |
| RailLancer | 85% | 680 | 2,894 | 0 | 30.4% |
| FrostCoil | 75% | 463 | 2,359 | 248 | 10.8% |
| **SiegeDrill** | **20%** | **65** | **1,468** | **0** | **44.6%** |

零贡献口径（`tools/td_mcp_p135_campaign_experience_calibration.ps1:322`）：`damage==0 && controls==0`，即**整局一枪未中**。注意该口径不计破甲团队增益——但 0 伤害同时意味着 0 次破甲命中，所以 44.6% 是真实的"从未生效"，而非口径偏差放大。

## 二、新证据 1：反甲生态位不稀缺（推翻"niche 不存在"假设）

对 20 个波次 JSON 按 enemyCatalog 血量×tag 分解（脚本内联于会话记录，可用性可复现）：

| 章节段 | 装甲系 HP 占比 (armored/heavy/boss) | 快速系 (fast/flank) | 虫群系 (swarm/spawn/split) |
|---|---:|---:|---:|
| A (L01-05) | 0.0% | 63.7% | 36.3% |
| B (L06-10) | 46.4% | 26.4% | 27.2% |
| C (L11-15) | 47.8% | 26.8% | 25.4% |
| D (L16-20) | 42.1% | 23.0% | 35.0% |

**从 L06 起装甲系稳定占全部敌人血量的 42-53%**（L14 峰值 53.4%）。SiegeDrill 的专属生态位覆盖 15/20 关、半个游戏的血量。它的问题不是"没有敌人可打"。

## 三、新证据 2：出场由 AI 表格硬决定，非价值判断

用 compositionSignature 复算 180 局：

- **GravSnare：15 局出场 = L16-20 的 control_lattice 全部 15 局（15/15 吻合）**。出场 100% 由「L16 解锁 × control_lattice 优先表第 2 位」两个静态因素相乘决定，自适应路径零拾取。换算：可用局 45 局（L16-20 × 3 难度 × control_lattice），拾取率 33%——它的 8.3% 总出场率天花板是 25%（5/20 关），结构性锁死。
- **SiegeDrill：39 局出场中 36 局来自 focused_fire（优先表第 2 位），仅 3 局来自 adaptive_network**——尽管 adaptive_network 有专门的装甲压力分支，且这些关卡的装甲血量占比 ~47%。

## 四、根因定位（代码级）

1. **adaptive 装甲分支被 RailLancer 截流**（`TDGameManager.P124.cs:486-501`）：`FirstOrDefault(kind => RailLancer || SiegeDrill)` 恒返回 RailLancer（L01 起解锁、几乎总在编队），直到 railLimit（塔数 45%）饱和；溢出路径 `.ThenBy(TDTower.GetBuildCost)` **按成本升序**进一步压制 68 费的 SiegeDrill（全场第二贵）。结果：装甲压力响应 ≈ RailLancer 专卖。
2. **优先表席位**：adaptive_network 默认表 SiegeDrill 第 7、GravSnare 第 8（垫底，`GetP124TowerPriority`）；两者仅在 focused_fire（Siege #2）和 control_lattice（Grav #2）各有一个席位。
3. **零命中率 = 选址/目标获取失效**：单发慢速投射物（7.8 速、0.72/s）+ 对未减速快速敌 ~25% 闪避（L06-20 快速系仍占 23-36% 血量）+ 多线图（L09/L12 分轨峡谷）车道落错 = 整局无有效射界。RailLancer 85% 出场×45% 配额上限同样造成 30.4% 零贡献（过量建造摊薄）。
4. **GravSnare 本身不弱**：伤害/塔 5,220 高于 RailLancer，控制/塔 375 全场第二；问题纯粹是「能不能被选上」。

## 五、拆塔返还 60% 新变量评估（计划要求项）

- **自动化口径（p13.5 矩阵）**：autoplay 不执行出售，全部指标不受影响，原结论在自动化侧**原样成立**。
- **人类口径**：返还不改变零贡献率本身（那是选址结果），但把错配代价从 100% 损失降为 40% 损失——玩家试错成本下降，反而更可能自己发现 SiegeDrill 的真实价值。结论：**原判断（改摆放/价值预测，不改数值）继续成立**，返还属于缓解项而非修复项。
- **新观察项**：40 费 RailLancer 的"按波租用"净成本 16/波（战斗中禁售、仅备战窗口可操作），暂无滥用证据，列入遥测观察即可。

## 六、最小改法清单（按预期收益排序，全部为 AI/度量侧）

1. **adaptive 装甲分支混合拾取**（P124，~5 行）：armorPressure 显著（如 >0.4）且 siegeCount 未达装甲占比换算的份额时，直接返回 SiegeDrill，绕过 RailLancer 截流与成本排序。
2. **autoplay 学会拆塔**（P124，中等）：备战窗口出售"零贡献且 X 波未命中"的塔（复用 `TrySellTower` 60% 返还），既提高全塔效率度量，又规模化验证新拆塔路径——一石二鸟。
3. **度量口径补破甲**（matrix 脚本 + `DebugBuildP124RunReport`，小）：把破甲施加次数计入 controls 类，让 SiegeDrill 的团队价值可见，避免后续诊断再次失真。
4. **GravSnare 数据面**（矩阵配置，小）：如需更多样本，给 NG+/challengeRemix 局扩测而非移动 L16 解锁（终局控场定位是设计决策）；可选：adaptive 快压 fallback 表中 GravSnare 从 #3/#4 提一位。
5. **L09/L12 车道核验**（验证项，非改动）：确认选址评分的车道反制压已含装甲 tag（`CalculateP124SiteScore` 的车道权重 ×1.9 放大是否覆盖 armored/heavy）。

## 七、结论

p13.5 的原结论**成立且现在有了机制级解释**：SiegeDrill/GravSnare 是「AI 选择结构」问题（优先表席位 + RailLancer 截流 + L16 解锁天花板），叠加 SiegeDrill 自身的选址失效；二者数值身份（伤害/塔）并不弱于平均。按清单 1+2 改 autoplay 后重跑 190 局矩阵即可量化验证，全程无需动任何塔的数值。

## 附：证据文件

- `output/playtest/p135_release_v1/p135_tower_usage.csv`、`p135_real_runs.csv`
- 波次数据：`Assets/Resources/Data/waves/*_v1.json`（enemyCatalog 内嵌 hp/tags）
- 代码：`TDGameManager.P124.cs:475-554`（adaptive 分支）、`GetP124TowerPriority`、`TDGameManager.P135.cs:153-176`（engage 策略覆盖）

---

# 附录（2026-08-18 补充）：L13/L20 胜率塌陷处理方案（v4 任务 1.3）

## A.1 现象与时间线

QA 上报（v4 计划转述）：RailLancer 回归套件胜率 25/25 → 14/25，其中 **L13 0/5、L20 0/5**。时间线对齐：

- 08-03 p13.5 矩阵 179/180 胜（含 L13/L20 全过）——彼时还是旧护甲模型
- 08-10 R1 修复三件套上线：护甲混合模型（每点 −4% 上限 60% 叠固定值）、RailLancer heavyMult 1.25→1.0、快速敌闪避
- 08-17/18 回归套件塌陷，且 p13.5 时期已有先兆——L13 Ember Control Lattice 唯一败局 12 次漏怪全部归因 `counter_mismatch`、**armor-counter damage = 0%**（与本诊断第三节"adaptive 装甲响应被 RailLancer 截流"同源）

## A.2 机制：无破甲时全队被钉在伤害下限

`TDCombatMath.ResolveArmoredDamage`：`max(1, round(raw × (1−min(0.60, armor×0.04))) − effectiveArmor)`。基础每发伤害对高甲敌人（未破甲）：

| 塔基础每发 | plated_spore (8甲) | husk_titan (9甲) | Boss (12甲) |
|---|---:|---:|---:|
| RailLancer 18 | 4 | 3 | **1** |
| CinderMortar 16 | 3 | **1** | **1** |
| FrostCoil 8 | **1** | **1** | **1** |
| ArcWelder 10 | **1** | **1** | **1** |
| EmberFlak 10 | **1** | **1** | **1** |
| SiegeDrill 22(对甲) | 7 | 5 | **1** |
| *同上 + 破甲+5 后* | *16* | *14* | *9* |
| *RailLancer 破甲后* | *13 (3.2×)* | *11* | *6* |

**6 塔中 5 座对 9 甲以上敌人输出恒为 1**（下限），连 SiegeDrill 自己对 Boss 也是 1。唯一的钥匙是 SiegeDrill 命中附带的 +5 破甲（持续 3s）——全队伤害放大 3-6 倍。而本诊断已证明 autoplay 的装甲响应从不建 SiegeDrill：**护甲模型（08-10）× 截流 bug（一直存在）= L13（48% 装甲血量的窑炉考试关）与 L20（Boss 12 甲）精确塌陷**。两个关卡正好是全战役护甲密度最高点，与 QA 数据完全吻合。

## A.3 处理方案

**主方案（autoplay 重标定，已实施——本报告第六节改法 1+2）**：

1. 改法 1：adaptive 装甲分支 SiegeDrill 配额（`siegeQuota = clamp(塔数×0.2, 1..2)`，纯压力驱动，无关卡/地图硬编码）→ 装甲压力主导的波次必建破甲塔
2. 改法 2：备战窗口出售"站立 ≥3 波且零命中"的塔（每备战窗最多 1 座，保底 1 塔）→ 死置位损失回收 60%，预算转向有效塔

预期：L13/L20 的全队 1 点伤害墙被破甲解除，胜率回难度档；SiegeDrill 出场率从 20% 升至与装甲压力相称的水平。

**备选方案（护甲微调，未实施——仅当 QA 复跑后 L13/L20 仍不达标时启用）**：

固定减伤部分加帽：`effectiveArmorFlat = min(effectiveArmorFlat, ceil(postPercentDamage × 0.5))`，即固定减伤至多再砍掉百分比后伤害的一半（下限 1 不变）。效果：RailLancer 对 Boss 从 1 → 5；FrostCoil 对 husk_titan 从 1 → 2；破甲价值保留（8 甲 plated_spore 对 RailLancer 4→13 不变）。改动点：`TDCombatMath.ResolveArmoredDamage` 一处 + `TDArmorTests` 补 3 个边界用例。**先跑主方案回归再决定是否启用**——若主方案已达档，备选方案保持封存，护甲的"必须反制"教学价值优先。

## A.4 验证移交（QA，对应其计划任务 4）

1. RailLancer 回归 25 局：胜率应回 22+/25，L13/L20 各 ≥3/5（难度档目标）
2. p135 平衡矩阵复跑：SiegeDrill 出场率 20%→35%+、零贡献率 44.6%→30% 以下；GravSnare 指标不应恶化
3. 观察 autoplay 出售行为：日志应出现 `Sold ...` 战术事件且不出现同格反复建拆（每备战窗 1 座上限 + 零命中条件应自然抑制振荡）

---

# 附录 2（2026-08-19）：装甲配额零生效定位（v6 任务 1，三问三答）

## B.1 现象

v5 QA 复跑（`output/playtest/balance_regression/`，25 局）：L01/L05/L09 全回档 5/5，**L13 0/5、L20 0/5**；SiegeDrill 在 L13/L20 全部 10 局零出场。

## B.2 三问三答

**Q1：这些局的 autoplay 路径经过配额代码吗？**
配额只存在于 `ResolveP124BuildKind` 的 `_p124StrategyId == "adaptive_network"` 装甲子分支。回归配置每关 5 局（adaptive×2 / focused×2 / control×1）：

- **adaptive×2**：经过该分支，但配额的前提 `buildableKinds.Contains(SiegeDrill)` 恒为 **false**——`buildableKinds = 优先表 ∩ _unlockedTowerKinds(4 塔编队) ∩ 预算`（`TryBuildP124Tower` :419-421），而 `ConfigureP124Formation` 从静态优先表取前 4 填编队，adaptive 表中 SiegeDrill 排第 7，**根本进不了编队**。实锤：10 局 JSON 的 towers 数组显示 adaptive 编成恒为 {RailLancer, CinderMortar, FrostCoil, ArcWelder}。
- **focused×2**：不经过（配额在 adaptive 分支内）。focused 编队含 SiegeDrill（优先表第 2），但见 B.4 停滞异常，轮转没轮到它。
- **control×1**：不经过，且 SiegeDrill 是其优先表第 8。

**结论：10 局中 0 局存在"配额代码 + SiegeDrill 在集合内"的组合。**

**Q2：装甲压力谓词在这些局成立吗？**
成立，谓词不是瓶颈。threatCost 加权核算（与 `CalculateP124WaveTagPressure` 同式）：L13 全局压力 armor=302 / swarm=198 / fast=154（装甲主导），逐波 9/20 装甲主导；L20 armor=322 / swarm=248 / fast=191（主导），逐波 7/20。装甲子分支被进入且条件满足——它忠实地返回了 RailLancer（SiegeDrill 不在集合，FirstOrDefault 兜底）。

**Q3：定性结论 = 条件缺陷（挂对了分支，但分支的输入集合在上游被编队截断清空）。**
非实现 bug（配额逻辑本身正确，给它含 SiegeDrill 的集合即可开火）；非机制不足（从未获得开火机会，无从谈不足）。

## B.3 修复（随本附录同批，待代码评审）

**装甲感知编队**（`ConfigureP124Formation` + 新增 `IsP124ArmorDominantLevel`/`CalculateP124LevelTagPressure`）：编队填充完成后，若本关波次全集装甲压力主导（与逐波谓词同权重）且 SiegeDrill 已解锁但不在编队 → 替换编队末位。效果：L13 adaptive 编队 {Rail,Cinder,Frost,Arc} → {Rail,Cinder,Frost,**Siege**}；control 末位 Cinder → Siege（其"最少持有优先"逻辑会尽早建它）。L01-05 装甲压力为 0，自动不触发，教学关不受影响。护甲加帽按导演指令**继续封存**。

## B.4 次级异常（独立于配额，需一次插桩局定位）：建塔循环停滞

全部 L13/L20 局在仅建 3-5 塔后停止扩张，死亡时未花预算 348-1254：focused L13 三塔（Rail×1+Flak×2）即停滞，coverage 40-52 远低于 72 阈值、`shouldBuild` 恒真——即 `TryBuildP124Tower` 在预算充足时持续返回 false。静态排查已排除：编队过滤（集合非空）、预算过滤（余量远超塔价）、`strategyBuildLimit`（=12）、选址硬禁令（x≤4 禁后仍有 5-9 个合法格）、P135 覆盖（policy 为空）、fix-2 出售（towersBuilt=存活数，零出售）。**修复编队后此异常可能仍压制 focused 局**——建议 QA 复跑时若 focused 仍失守，下一步给 `TryBuildP124Tower` 的每个 early-return 加计数器插桩定位。另注：run_22 赛后分析自己给出"补充轨枪/钻机"建议，与诊断方向互证。

## B.5 QA 复跑判据补充（对应 A.4，追加）

- L13/L20 adaptive/control 局编队应出现 SiegeDrill（p124.json towers 数组）
- focused 局若仍 3-4 塔停滞 → 触发 B.4 插桩流程，勿直接归因配额
- 建议多种子取样（≥3 种子）——帧计时下单种子判定天然脆弱（代码会话交接提醒）
