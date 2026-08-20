# 美术需求 — 敌人死亡帧（per-enemy death reel）v1

> 来源：100 分路线图差距 4 遗留口（塔侧已关账，敌人侧此件补齐）；v7 任务 3 提前启动。
> 格式沿用 fire/T2 规格。**总量：12 敌 × 4 帧 = 48 张**。

## 文件命名规范

```
enemy_{kind}_death_00.png ... enemy_{kind}_death_03.png   (每敌 4 帧)
```

代码加载路径: `Art/anim/enemy_{kind}_death_{00..03}`

## 12 种敌人的 kind 标识

| 敌人 | kind | 死亡帧设计提示（与 idle 帧同视角） |
|---|---|---|
| 疾行爬虫 Skitter Runner | skitter_runner | 步足摊开、体壳塌折 |
| 灰烬虫群 Ash Swarm | ash_swarm | 个体离散崩解、灰化飘散 |
| 甲壳蛮兽 Carapace Brute | carapace_brute | 甲壳开裂崩落、躯干侧倾 |
| 覆甲孢子 Plated Spore | plated_spore | 甲片剥离、孢子液泄出 |
| 掘地工兵 Burrow Sapper | burrow_sapper | 钻头卡死、土石回填 |
| 余烬水蛭 Ember Leech | ember_leech | 躯体瘪塌、余烬熄灭 |
| 孢子载体 Spore Carrier | spore_carrier | 囊腔破裂、孢子雾散开（区别于存活期分裂预警 fx） |
| 铁轨守卫 Rail Warden | rail_warden | 护盾碎裂、装甲解体 |
| 烬火滑翔者 Cinder Glider | cinder_glider | 翼膜撕裂、螺旋坠地 |
| 空壳泰坦 Husk Titan | husk_titan | 大体量分段坍塌（重帧，2-3 帧可用同构图不同碎裂度） |
| 回声拟态体 Echo Mimic | echo_mimic | 拟态形态剥离、回声残影消散 |
| 熔炉母体 Furnace Matriarch | furnace_matriarch | 相位爆裂、炉核熄灭（Boss 仪式感，可与 70%/35% 相位呼应） |

## 生效机制（需要一次代码挂钩，规格明示——与 fire 帧不同、与 T2 类似）

动画器侧已就绪：`TDSpriteAnimator.ConfigureDeath(prefix, count, fps)` + `PlayDeath()`（播完定格末帧）**已实现但无人调用**——当前死亡表现 = 本体 0.22s 淡出 + 共享 `fx_enemy_death` 瞬态特效（8 帧 16fps）。

挂钩契约（`TDEnemy`，~15-20 行，代码会话实施）：
1. 死亡时探测 `enemy_{kind}_death_00` 是否存在（可复用 `FxPrefixAvailability` 式缓存）
2. 存在 → 本体换播 4 帧死亡卷（12fps ≈ 0.33s），本体保持可见至卷尾再淡出；共享 `fx_enemy_death` 特效**保留叠加**（打击感冗余）
3. 缺帧 → 完全回退现行表现（零副作用，美术可分批交付）

## 规格

- 尺寸: 1024×1024 PNG（与 idle 帧一致；Boss 可 1280 见方，导入后处理自适应）
- 帧数: 每敌 4 帧（00–03）
- 总量: 12 × 4 = **48 张**
- 帧率: 12 FPS（0.33s 完成，与死亡淡出节奏衔接）
- 风格: 余烬铁道工业风；第 0 帧应与 idle 姿态衔接（死亡瞬间姿态连续），第 3 帧为"残骸定格"
- 脚底锚点: 与该敌 idle 帧相同的地面接触位置（`foot_anchors.json` 锚点沿用，死亡帧不单独建锚）
- 可读性: 死亡帧不得引入大面积高亮闪烁（与战斗 FX 预算层冲突）；Boss 死亡帧允许更长（可 6 帧，总量按 6 计）

## 放置位置与导入

```
Assets/Resources/Art/anim/
  enemy_skitter_runner_death_00.png
  ... (共 48 张)
```

导入设置无需手工配置（`TDArtImporter` 自动规整，与 fire/T2 帧同流程）。

## 验证

1. 挂钩落地后：击杀任一敌应先播本体死亡卷再淡出（共享死亡特效叠加）
2. 缺帧敌（分批交付期间）表现与现状完全一致
3. QA 抽查：husk_titan / furnace_matriarch 死亡在 960×540 下可读、无大面积闪白
