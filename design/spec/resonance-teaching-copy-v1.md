# 共振系统教学文案包 v1（Resonance Teaching Copy）

> 来源：共振系统详细分析（2026-08-23 会话）产出的教学动线。
> 用途：L16+ 教程位、波次 hint、图鉴/帮助页文案源。en 为源串（strings.json 键约定），zh 为译文。
> 接线：教程文案实体在 TDGameManager 教程位——按管线随代码批接线（与 FrostCoil 文案 C.4 同批）。
> 约束：浮动标签全部 ≤14 字符（P12.1 反馈预算）；一次一个新概念；每步绑定既有音画资产（resonance_ready / MATCH Critical / 收敛音效），零新增美术。

## 教学动线（introduce → reinforce → exam）

| 步 | 时机 | 概念 |
|---|---|---|
| 0 | L16 任务简报 | 世界观铺垫 |
| 1 | L16 首波 prep | 电荷条（纯观察） |
| 2 | 首次窗口 | 7 秒 + 二选一（首次决策） |
| 3 | L16 w3-5 | 威胁匹配 + 连击（reinforce，波次 hint 反复） |
| 4 | L17+ | 构筑共鸣 / 矩阵收敛（exam） |
| 5 | 水蛭同场 | 反制教学 |

## 双语文案（en 源串 → zh）

**0 · 简报插句**
- "The fire in your line never died. From this level on, every hit banks an ember — and when the gauge fills, you get to light it yourself."
  → 「防线的火没有熄。从这一关起，每一次命中都会积攒余烬——攒满的那一刻，你可以亲手点燃它。」

**1 · 电荷条**
- "The orange track at the top is your Ember Charge. It rises with every hit — the harder the hit, the faster it climbs. Resonance Beacons charge fastest of all."
  → 「屏幕顶部的橙色轨道，是余烬电荷。打中敌人它就会上涨——打得越疼，涨得越快。共振信标是这套系统的引擎，它充得比谁都快。」
- 浮动：`Embers Full` → `余烬满槽`

**2 · 窗口与选择**
- "Resonance window open: 7 seconds. All towers gain +10% damage while it lasts. Make one choice — one per window, no take-backs."
  → 「共振窗口开启，持续 7 秒——窗口内所有塔的伤害 +10%。做一个选择，一窗只此一次，选定不能反悔。」
- "[Z] Ember Surge — all towers gain a further +16% damage. Burn through them now."
  → 「[Z] 余烬涌动——所有塔再 +16% 伤害。想现在就烧穿它们，按 Z。」
- "[X] Fracture Mark — every enemy is pulsed with marks; Frost Coils and Siege Drills deal bonus damage to marked targets. Hold the line, then kill."
  → 「[X] 裂痕标记——全场敌人被反复打上标记，冷凝线圈与攻城钻机对被标记目标额外增伤。想控住场面再杀，按 X。」
- "You may skip it — but the window drains away, and your chain breaks."
  → 「也可以不选——但这一窗会白白流走，连击也会断。」

**3 · 匹配与连击**
- "Read the wave before you press. Armored, heavy or boss-heavy waves want Ember Surge. Fast, swarm or flanking waves want Fracture Mark."
  → 「先看这一波是什么敌人，再选：装甲、重装、BOSS 当道 → 余烬涌动（Z）是对的；快速、虫群、侧翼突袭 → 裂痕标记（X）是对的。」
- "Two matched windows in a row trigger the Resonance Chain: +10 budget on Surge; +6 budget and +1 line integrity on Mark. A miss — or a skipped window — resets the chain."
  → 「连续两个窗口都选对，触发共振连击：涌动 +10 预算；标记 +6 预算、+1 防线完整度。选错、或空窗不选，连击归零。」
- 状态：`MATCH 1/2` → `连击 1/2`；`NoMatch (streak reset)` → `选错·连击断`

**4 · 构筑共鸣（L17+）**
- "Every specialization has a resonance affinity — damage specs favor Surge, utility specs favor Mark. When the right spec hits the right enemies in the right window, Matrix Convergence triggers: Surge convergence extends the window and amplifies the whole line; Mark convergence pins every enemy in place. That is the highest reward for building the right towers."
  → 「每座塔的专精都有共鸣倾向：伤害系专精亲和涌动，功能系专精亲和标记。当专精在正确的窗口里反复命中正确的敌人，会触发矩阵收敛——涌动收敛延长窗口、全队增伤；标记收敛把全场敌人钉在原地。这是"塔建对了"的最高奖赏。」
- "Your formation doctrine amplifies its matching command by +10%. Out-of-run choices pay off inside the window."
  → 「编队时选的学说会放大对应命令（+10%）——你的局外选择，在窗口里兑现。」
- 浮动：`MATRIX!` → `矩阵收敛!`

**5 · 反制（水蛭同场）**
- "Ember Leeches drain your charge while they live. Kill them first — your windows are their food."
  → 「余烬水蛭活着的时候，会持续吸走你的电荷。看到它们，优先打死——你的窗口，就是它们的口粮。」

## HUD 速读卡（图鉴/帮助页）

| en | zh |
|---|---|
| Orange track rising | Charging (Beacons fastest) → 橙条上涨 = 正在充能（信标最快） |
| Track full + flash + chime | Window open, 7s, choose fast → 橙条满 + 闪烁 + 音效 = 窗口开启，7 秒，快选 |
| Z / X buttons lit | Awaiting your decision → Z / X 按钮亮起 = 等你决策 |
| MATCH 1/2 | Chain in progress → 连击进行中 |
| NoMatch (streak reset) | Wrong pick this window → 这窗选错了 |

## 失败归因话术（设计支柱"输得明白"）

| en | zh |
|---|---|
| Few windows opened → output too low, charging too slow | 窗口很少开 → 输出不足，充能太慢 |
| Windows opened, nothing pressed → rhythm forgotten | 窗口开了没按 → 忘了节奏，白窗 |
| Frequent NoMatch → pressed without reading the wave | 频繁 NoMatch → 没看敌人构成就按了 |
| Full windows, still leaking → build and command mismatched | 窗口全开仍漏怪 → 构筑与命令不匹配——该控的波用了爆发 |
