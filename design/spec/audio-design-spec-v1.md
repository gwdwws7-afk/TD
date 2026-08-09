# Emberline Defense — 音乐与音效需求规格文档 v1

> **用途**:本文件是给 AI 音频生成工具(MiniMax Code)及人类作曲/音效师的唯一权威需求文档。所有条目均可直接对应到游戏代码中的触发事件,生成后按命名约定放入 `Assets/Resources/Audio/` 即可被运行时加载。
>
> **品质目标**:年度游戏级。每一个声音都必须服务于"余烬铁道"工业废土世界观,且具备足够的混音清晰度,使得玩家在密集战斗中仍能辨认出关键反馈。

---

## 0. 世界观与声音调色板(所有声音的统一约束)

### 世界观一句话
人类文明的余烬铁道在衰败。玩家是最后一位线务司令,在锈蚀的铁轨、冷却的熔炉、崩塌的终点站之间,用工业化战争机器抵御从灰烬中涌出的变异生物。基调是**"温暖的衰败"**——不是冰冷的科幻,也不是黑暗的恐怖,而是一种"夕阳下老机器还在轰鸣"的忧郁工业浪漫。

### 核心声音调色板(每个交付物必须落在其中)
| 类别 | 乐器/音色 | 用途 |
|---|---|---|
| **金属呼吸** | 打击铁轨、汽缸排气、钢板共鸣、低频钟鸣 | 塔、机制、UI 强调 |
| **余烬纹理** | 模拟合成器的暖噪、火舌喷气、煤渣碎裂、低频次低音 drone | 共振、余烬系统、危险 |
| **机械节奏** | 蒸汽脉冲、齿轮咬合、连杆往复、节拍器化的工业打击乐 | 音乐节律骨架 |
| **生物质感** | 甲壳碎裂、湿滑蠕动、低吼共鸣、群体嘶鸣(非人声,有机但不恶心) | 敌人 |
| **空间氛围** | 远处列车、风穿过废墟、空旷站台的混响尾音、磁带嘶声 | 环境层、菜单 |
| **人性温度** | 单把大提琴、孤立钢琴单音、女声无词哼鸣(极少,仅用于胜利/失败的情绪锚点) | 情绪高潮 |

### 全局混音原则
1. **瞬态优先**:战斗中所有 SFX 必须有清晰起音(attack),避免被音乐掩蔽。命中类 SFX 起音 < 5ms。
2. **频段分工**:音乐占低频(drone/鼓)与中频和声;SFX 命中占 2-5kHz 瞬态;人声哼鸣独占 200-400Hz 并配 sidechain。
3. **密度自适应**:音乐随波次阶段动态层数(见 §2),SFX 在战斗中不堆叠超过 8 个并发(代码已有 FX 预算 32 上限,音频应更保守)。
4. **响度基准**:母带 -14 LUFS 整体;单轨 SFX 峰值 ≤ -3 dBTP;音乐 -16 LUFS 供侧链衰减空间。

---

## 1. 技术接入规格(生成物如何接入游戏)

### 1.1 已有运行时基建(无需新建)
- `TDGameManager` 已持有 3 个分层 `AudioSource`:
  - `_sfxSource`(常规反馈层)
  - `_tacticalSfxSource`(战术层,独立音量)
  - `_criticalSfxSource`(关键层,Boss/共振/漏防,最高优先级)
- 已有 `_sfxClipCache` 字典 + `Resources.Load` 缓存路径
- 当前 SFX 是**程序化合成**的(`CreateSfxClip` / `AudioClip.Create`,11314 行起);本规格交付的音频将**替换**这些合成占位

### 1.2 文件格式与命名约定
- **格式**:`.wav`(44.1kHz / 16-bit / mono,除音乐和环境层为 stereo)
- **音乐**:`.ogg`(Vorbis,q5,循环切片必须首尾无缝,提供 2 小节 fade 尾)
- **目录**:
  ```
  Assets/Resources/Audio/
  ├── Music/        # 长循环音乐层
  ├── Sfx/          # 战斗反馈
  ├── Sfx/Enemy/    # 敌人专属
  ├── Sfx/Tower/    # 塔专属
  ├── Sfx/Ui/       # UI 与菜单
  └── Ambience/     # 环境氛围床
  ```
- **加载约定**:代码按 `Audio/{Category}/{filename}`(无扩展名)从 Resources 加载。生成时文件名即代码引用键,必须严格一致。

### 1.3 优先级与并发
- 代码侧 FX 预算:`Routine ≤18 / Tactical ≤12 / Accent ≤8`(总上限 32)。音频应匹配:**同一 AudioClip 的 PlayOneShot 间隔不少于其时长的 60%**,避免机枪式堆叠。
- 三个 AudioSource 分层独立,关键层可打断常规层(`_criticalSfxSource` 优先)。

---

## 2. 音乐(7 个交付物)

音乐采用**分层自适应**架构:每个关卡有 1 个音乐"种子",由 3-5 个独立 stem(和弦床/节奏/旋律/危险层)组成,代码按波次阶段(Prep→Combat→Exam/Boss)交叉淡入淡出各 stem。

### M1. 主菜单主题 — `Music/menu_theme`
- **用途**:游戏启动 / 主菜单循环
- **时长**:≥ 90 秒,无缝循环
- **BPM**:72(沉缓,呼吸感)
- **调性**:D 小调,自然小音阶
- **配器**:
  - 低频:次低音 drone(D1 持续音)+ 远处铁轨撞击采样(每 4 小节一次,带大量混响)
  - 中频:孤立钢琴单音(主题动机:D-F-A-D 上行,极慢,每音间隔 2-3 秒,带磁带嘶声)
  - 高频:风穿过废墟的滤波白噪(缓慢 pan)
  - 情绪锚点(第 32 小节起):女声无词哼鸣(单音 A4,8 小节长音,大量混响,sidechain 至 drone)
- **情绪**:庄严、忧郁、有"最后的岗位"仪式感。**不是**史诗英雄主义,**不是**恐怖紧张。
- **参考坐标**:`Disco Elysium` 主菜单的苍白庄严 + `Frostpunk` 主题曲的工业重量感
- **技术**:stereo,.ogg,首尾无缝,-16 LUFS

### M2. 战斗音乐床 — 按 4 个章节变体(各 1 个交付物)

每个变体共享同一节奏骨架(蒸汽脉冲 + 齿轮打击乐),但配器和危险度递进。

#### M2a. 章节A(Grayline/Ashfall,L1-L10)— `Music/combat_chapter_a`
- **BPM**:96(中速,稳定推进)
- **调性**:D 小调 → F 大调交替(大小调模糊,呼应"温暖衰败")
- **层数**:4 stem
  1. `combat_chapter_a_bed`(次低音 drone + 远处锅炉低频脉冲,贯穿全程)
  2. `combat_chapter_a_rhythm`(连杆往复打击乐,稳定四分音符,模拟蒸汽机)
  3. `combat_chapter_a_melody`(大提琴拉奏,简短下行动机,每 8 小节出现一次)
  4. `combat_chapter_a_danger`(滤波失谐金属刮擦,仅高压波次淡入)
- **循环**:每 stem ≥ 60 秒无缝循环

#### M2b. 章节B(Split Canyon,L9-L12)— `Music/combat_chapter_b`
- 相比 A:节奏加密至 BPM 104,加入"分路"双声道错位打击(左右耳错半拍),呼应分岔地图。大提琴改为更尖锐的滑音。

#### M2c. 章节C(Hollow Kiln,L13-L16)— `Music/combat_chapter_c`
- BPM 88(更慢但更重),加入窑炉低频共鸣(模拟大型空腔共鸣的 60-80Hz 脉动)。旋律转为钢琴,音符更稀疏、更孤立。**危险层加入次低音心跳**(每 2 小节一次,模拟窑炉"呼吸")。

#### M2d. 章节D(Last Ember,L17-L20)— `Music/combat_chapter_d`
- **终局音乐**,BPM 80,调性转 D 小调持续(最暗)。前段克制(只有 drone + 偶发铁轨撞击),Boss 波次(M2d 的 danger stem)全开:工业金属打击乐 + 失谐大提琴 + 远处汽笛长音。**胜利时(M3)由此 danger stem 自然延伸**。

### M3. 胜利结算 — `Music/victory_stinger`
- **用途**:关卡胜利时播放一次(不循环),与结算界面同步
- **时长**:8-12 秒(带 2 秒 tail)
- **内容**:基于主菜单主题动机(D-F-A-D),但转为**大调明亮呈现**:钢琴柱式和弦 + 大提琴持续音 + 一次温暖的金属钟鸣(D4,带丰富泛音)。情绪是"机器终于可以休息了"的释然,**不是**凯旋狂欢。
- **接入**:代码在 `_victory = true` 时触发(对应 RunSummary result=victory)

### M4. 失败结算 — `Music/defeat_stinger`
- **用途**:防线崩溃时播放一次
- **时长**:6-10 秒
- **内容**:单个下行大提琴滑音(D2→A1)+ 蒸汽泄压的长尾(滤波下降的嘶声)+ 一次失真的金属坍塌声。情绪是"炉火熄灭"的寂灭,**不是**戏剧化悲剧。
- **接入**:`_lineIntegrity <= 0` / RunSummary result=defeat

### M5. 共振窗口主题 — `Music/resonance_window`
- **用途**:余烬共振 7 秒窗口开启时循环播放(窗口结束自动淡出)
- **时长**:≥ 10 秒无缝循环
- **BPM**:与当前战斗音乐同步(代码会在窗口开启时ducking战斗乐)
- **内容**:这是本作**唯一允许的"魔幻超验"声音**——余烬电荷是一种神秘力量。用上升滤波的暖噪垫层 + 高频闪烁的金属泛音(模拟"余烬即将点燃")+ 隐约的女声哼鸣(A4,带颤音)。情绪:危险中的神圣机会窗口。
- **接入**:`_resonanceWindowTimer > 0`(代码已在 UpdateResonanceState 处理)

---

## 3. 战斗音效 SFX(按代码事件锚点)

每个 SFX 列明:**代码触发点 → 文件名 → 声音描述 → 技术规格**。代码事件已核对真实存在。

### 3.1 命中与伤害反馈(对应 `EmitFeedback` / `NotifyEnemyDamaged`)

| ID | 触发事件 | 文件名 | 描述 | 规格 |
|---|---|---|---|---|
| S1 | `TDBattleFeedbackKind.Hit`(常规命中) | `Sfx/Hit/routine_hit` | 金属弹击中甲壳:短促"叮"+ 轻微碎屑声。每组塔有不同的音色变体(见 §3.3) | 80-120ms,mono,-18 LUFS,起音 <3ms |
| S2 | `TDBattleFeedbackKind.CriticalHit`(暴击) | `Sfx/Hit/critical_hit` | 更亮的金属穿透声 + 一次额外的谐波"鸣响"。明显比 routine 更"贵" | 150-200ms,mono,-12 LUFS |
| S3 | `TDBattleFeedbackKind.BossDamage`(对 Boss 命中) | `Sfx/Hit/boss_hit` | 沉重的金属撞钟 + 低频冲击。让玩家感到"我在伤害一个庞然大物" | 250-350ms,mono,-9 LUFS,有 80Hz 冲击 |

### 3.2 状态效果反馈(对应 `EmitFeedback` 状态类)

| ID | 触发事件 | 文件名 | 描述 | 规格 |
|---|---|---|---|---|
| S4 | `TDBattleFeedbackKind.ArmorBreak`(破甲) | `Sfx/Status/armor_break` | 甲壳/金属碎裂声:从密实到松散的"喀喇—哗啦"。强调"防御被瓦解" | 200-300ms,mono,-15 LUFS |
| S5 | `TDBattleFeedbackKind.Slow`(减速命中) | `Sfx/Status/slow_apply` | 凝滞感:短促的湿冰声 + 下行音高弯曲。冷而黏 | 180-250ms,mono,-18 LUFS |
| S6 | `TDBattleFeedbackKind.Exposed`*(若代码触发)* | `Sfx/Status/expose_mark` | 标记声:一次清脆的金属"叮"+ 持续的低频嗡鸣起音(标记存在感) | 150ms 撞击 + 300ms 尾,mono,-18 LUFS |

### 3.3 塔开火音(按 8 种塔,对应 `NotifyTowerFired`)
每种塔有独特的"机械签名",玩家应能**听声辨塔**。

| 塔 | 文件名 | 声音签名 | 规格 |
|---|---|---|---|
| **Rail Lancer**(电磁轨道单发) | `Sfx/Tower/fire_rail_lancer` | 蓄电容充电嗡鸣(150ms)→ 电磁"嗖-啪"释放 + 金属鞭击。锐利、线性、高科技 | 400ms(含蓄电),mono,-12 LUFS |
| **Cinder Mortar**(迫击炮 AOE) | `Sfx/Tower/fire_cinder_mortar` | 沉闷的"嗵"+ 炮弹离开炮管的金属摩擦尾。有重量感 | 350ms,mono,-12 LUFS |
| **Frost Coil**(冰冻控制) | `Sfx/Tower/fire_frost_coil` | 冷冻剂喷射:持续的"嘶——"+ 结晶"咔"。冷感、持续 | 600ms,mono,-15 LUFS |
| **Arc Welder**(连锁闪电) | `Sfx/Tower/fire_arc_welder` | 电弧:高频"滋啦"放电 + 跳火的噼啪。链接到次目标时有递减的二次"啪" | 500ms(含连锁),mono,-12 LUFS |
| **Siege Drill**(破甲钻) | `Sfx/Tower/fire_siege_drill` | 重型机械启动:电机加速 + 钻头咬合的金属研磨。最有"工业机器"感 | 700ms,mono,-12 LUFS |
| **Ember Flak**(防空高炮) | `Sfx/Tower/fire_ember_flak` | 快速连发的"嗒嗒嗒"+ 每发的火药爆燃。节奏感强 | 100ms×连发,mono,-12 LUFS |
| **Resonance Beacon**(共振信标) | `Sfx/Tower/fire_resonance_beacon` | 调谐音叉 + 脉冲扩散:纯净的金属共振音 + 环形扩散的"嗡"。非杀伤,是"信号" | 800ms,mono,-15 LUFS |
| **Grav Snare**(引力井) | `Sfx/Tower/fire_grav_snare` | 空间扭曲:低频下行的"咆——"+ 真空抽吸感。神秘、危险 | 600ms,mono,-12 LUFS |

### 3.4 敌人事件(对应 `NotifyEnemyKilled` / `NotifyEnemyEscaped`)

| ID | 触发事件 | 文件名 | 描述 | 规格 |
|---|---|---|---|---|
| S7 | `NotifyEnemyKilled`(敌人死亡) | `Sfx/Enemy/death_generic` | 甲壳/生物质塌陷的"扑"+ 灰烬飘散的尾音。不是血腥,是"解体为尘" | 250ms,mono,-15 LUFS |
| S8 | `NotifyEnemyEscaped`(敌人漏防,防线扣血) | `Sfx/Enemy/enemy_leak` | **关键警报**:刺耳但短促的金属啸叫 + 一次沉重的铁门撞击。让玩家"心头一紧" | 400ms,mono,-9 LUFS(关键层) |
| S9 | Boss(furnace_matriarch)出场 | `Sfx/Enemy/boss_spawn` | 远处汽笛长鸣(2 秒)+ 地面震动的低频轰鸣 + 金属应力嘎吱声。建立"庞然大物降临"的恐惧 | 3 秒,mono,-6 LUFS(关键层,不可被掩蔽) |
| S10 | Boss 阶段切换(`ShowCinematic(BossPhase)`) | `Sfx/Enemy/boss_phase_shift` | 炉火爆燃的"轰"+ 机械过载的金属扭曲 + 一次下行的失真滑音。标志 Boss 进入新阶段 | 1.2 秒,mono,-6 LUFS(关键层) |

### 3.5 共振系统(对应 `_activeResonanceCommand`)

| ID | 触发事件 | 文件名 | 描述 | 规格 |
|---|---|---|---|---|
| S11 | 共振窗口开启(`_resonanceWindowTimer` 从 0 转 >0) | `Sfx/Resonance/window_open` | 余烬点燃:上升的暖噪 + 一次纯净的金属泛音"鸣"(像音叉被敲响)+ 高频闪烁。这是"机会来了"的信号 | 1 秒,mono,-9 LUFS(关键层) |
| S12 | Ember Surge 命令激活(`TrySelectResonanceCommand(EmberSurge)`) | `Sfx/Resonance/ember_surge` | 火舌爆发的"呼——"+ 煤渣碎裂的密集噼啪 + 上升音高。热烈、危险、有攻击性 | 800ms,stereo,-9 LUFS(关键层) |
| S13 | Fracture Mark 命令激活(`TrySelectResonanceCommand(FractureMark)`) | `Sfx/Resonance/fracture_mark` | 玻璃/晶体断裂的清脆"喀喇喇"+ 下行音高 + 冷感的高频闪烁。冷冽、精准、控制感 | 800ms,stereo,-9 LUFS(关键层) |

---

## 4. UI 与系统音效(对应按钮点击、面板开关、波次切换)

| ID | 触发 | 文件名 | 描述 | 规格 |
|---|---|---|---|---|
| U1 | 按钮悬停 | `Sfx/Ui/hover` | 极轻的金属"叮",几乎像静电。不打扰 | 60ms,mono,-24 LUFS |
| U2 | 按钮点击确认 | `Sfx/Ui/click_confirm` | 坚实的机械"喀"+ 轻微的弹簧回弹。有"啮合"感 | 100ms,mono,-15 LUFS |
| U3 | 面板打开 | `Sfx/Ui/panel_open` | 蒸汽阀门短促排气 + 金属滑轨声。"舱门开启" | 250ms,mono,-18 LUFS |
| U4 | 面板关闭 | `Sfx/Ui/panel_close` | 上述的逆过程:滑轨收回 + 闷的"嗒" | 200ms,mono,-18 LUFS |
| U5 | 塔放置成功(`SpawnTower`) | `Sfx/Ui/tower_place` | 沉重的"哐当"+ 蒸汽泄压 + 底座啮合的"咔嚓"。**最有满足感的 UI 音**——让建塔像"部署一台战争机器" | 500ms,mono,-12 LUFS |
| U6 | 塔升级成功(`ApplyUpgrade`) | `Sfx/Ui/tower_upgrade` | 机械升级:电机加速 + 齿轮升档 + 一次更亮的金属共鸣。比放置音更"精炼" | 400ms,mono,-12 LUFS |
| U7 | 波次开始(`ShowCinematic(WaveTransition)`) | `Sfx/Ui/wave_start` | 远处汽笛短鸣(1 秒)+ 铁轨震动渐起。标志战斗开始 | 1.5 秒,mono,-12 LUFS |
| U8 | 波次清剿完成 | `Sfx/Ui/wave_clear` | 蒸汽长泄 + 一次温暖的金属钟鸣 + 机械减速的"呼"。**释然感**,标志喘息窗口 | 1.2 秒,mono,-12 LUFS |
| U9 | 教程步骤推进 | `Sfx/Ui/tutorial_advance` | 轻柔的"叮"+ 短暂的上行音。引导性、温和 | 200ms,mono,-18 LUFS |

---

## 5. 环境氛围床(按 5 张地图,各 1 个)

环境层是**低音量、长循环、立体声**的"场所感"背景。每个地图播放对应床,贯穿整关。

| 地图 | 文件名 | 描述 | 规格 |
|---|---|---|---|
| **Grayline Junction**(单铁轨枢纽,L1-L4) | `Ambience/grayline_junction` | 远处缓慢驶过的列车(铁轨接缝的规律"咔哒咔哒")+ 风吹过空旷站台的呼啸 + 偶发的信号钟(很远,带混响)。**宁静的衰败** | ≥ 60s 循环,stereo,-30 LUFS |
| **Ashfall Depot**(灰烬货场,L5-L8) | `Ambience/ashfall_depot` | 灰烬飘落的细腻沙沙声 + 远处锅炉的低频脉动 + 偶发的金属应力嘎吱。比 Grayline 更"热"、更不安 | ≥ 60s,stereo,-28 LUFS |
| **Split Switch Canyon**(分岔峡谷,L9-L12) | `Ambience/split_switch_canyon` | 峡谷回声的风 + 远处两列方向相反的列车(左右声道错位)+ 碎石滚落。**双线感** | ≥ 60s,stereo,-28 LUFS |
| **Hollow Kiln Basin**(环状窑炉盆地,L13-L16) | `Ambience/hollow_kiln_basin` | 大型空腔的持续低频共鸣(60-80Hz"呼吸")+ 炉火低语 + 金属热胀冷缩的"叮——"。**最压抑、最沉重** | ≥ 60s,stereo,-26 LUFS |
| **Last Ember Terminus**(终局终点站,L17-L20) | `Ambience/last_ember_terminus` | 接近寂灭:几乎只有风穿过巨大废墟的空洞呼啸 + 极低频的地鸣 + 偶发的一次遥远汽笛(像最后的告别)。**最荒凉** | ≥ 60s,stereo,-28 LUFS |

---

## 6. 交付清单与优先级

### P0 — 必须有(无此无法称"有音频的游戏")
| 数量 | 内容 |
|---|---|
| 1 | M1 主菜单主题 |
| 4 | M2a-d 四章战斗音乐床 |
| 1 | M3 胜利 stinger |
| 1 | M4 失败 stinger |
| 5 | §3.4 S7-S10 敌人死亡/漏防/Boss 出场/Boss 阶段 |
| 3 | §3.5 S11-S13 共振系统三音 |
| 8 | §3.3 八塔开火音 |
| 2 | §4 U5 塔放置 + U7 波次开始 |

**P0 合计:25 个交付物**

### P1 — 强烈建议(显著提升手感)
| 数量 | 内容 |
|---|---|
| 3 | §3.1 S1-S3 命中音(常规/暴击/Boss 伤) |
| 3 | §3.2 S4-S6 状态音(破甲/减速/暴露) |
| 1 | M5 共振窗口循环 |
| 5 | §5 五张地图环境床 |
| 4 | §4 U2/U6/U8/U9 点击/升级/清剿/教程 |

**P1 合计:16 个交付物**

### P2 — 锦上添花
| 数量 | 内容 |
|---|---|
| 2 | §4 U1 悬停 + U3 面板打开 |
| 2 | §4 U4 面板关闭 + (备用) |

**P2 合计:4 个交付物**

### **总计:45 个交付物**(P0 优先完成)

---

## 7. 验收标准(每个交付物必须满足)

1. **世界观一致**:可通过盲听判断属于"工业废土"调色板,无明显的现成 loop 库廉价感
2. **触发对应**:文件名与本文档命名约定 100% 一致(代码按字符串加载)
3. **混音达标**:单轨峰值 ≤ -3 dBTP;整体母带 -14 LUFS;起音清晰的命中音不被音乐掩蔽
4. **无缝循环**:所有标"循环"的音乐/环境床首尾接缝处无明显点击声或音高跳变
5. **时长合规**:在规格表列明的时长范围内(过短会显得廉价,过长会占用预算)
6. **格式正确**:.wav(44.1kHz/16-bit)用于 SFX,.ogg(q5)用于音乐/环境
7. **多样性**:塔开火音和命中音建议各提供 2-3 个变体(代码支持随机变体选择),避免高频重复导致的听觉疲劳

---

## 8. 给 MiniMax Code 的提示词建议(按交付物)

生成每个声音时,建议在 MiniMax 中使用如下结构的提示词模板:

```
[风格] Industrial dark ambient, post-apocalyptic, warm decay aesthetic.
       Reference: Frostpunk soundtrack meets Disco Elysium palette.
[乐器/音源] {此交付物的配器列表}
[情绪] {此交付物的情绪关键词}
[时长] {时长}, {是否循环}
[技术] 44.1kHz mono, -{X} LUFS, fast attack <5ms, no clipping, seamless loop.
[世界观约束] Must evoke "the last railway of a dying ember civilization".
             NO: epic orchestral, EDM drops, horror stingers, cartoonish SFX.
```

每个交付物的具体{占位符}从本文档对应表格行直接取值。
