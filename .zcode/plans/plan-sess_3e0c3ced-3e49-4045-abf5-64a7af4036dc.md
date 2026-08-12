## 音频接入代码 — 实施计划

### 目标
让已入库的 62 个音频文件在游戏里真正响起来,替换当前 `CreateSfxClip` 程序合成占位,并补齐目前完全无声的事件(敌人死亡、敌人特性、UI 交互、场景机制、专精大招、音乐、环境音)。

### 核心策略:集中注册 + 最小改动调用点
所有现有 50+ 个 `PlaySfxTone`/`PlayCriticalSfxTone` 调用点**一行都不改**。只改两个播放方法,让它们先查注册表加载真实音频,找不到才回退到 `CreateSfxClip`。这样:
- 风险最小(不碰几十个调用点)
- 向后兼容(没对应文件的 key 仍用合成音)
- 可逐个验证

---

### 改动 1:TDGameManager.cs — SFX key→资源路径注册表 + 预加载

**新增字段**(字段区 ~line 682 附近):
```csharp
private AudioSource _musicSource;      // 循环音乐
private AudioSource _ambienceSource;   // 循环环境音
private AudioClip _musicClip;
private AudioClip _ambienceClip;
private const string AudioBasePath = "Audio";
```

**新增方法** `ResolveSfxResourcePath(string key)`:
集中映射表,把现有动态 key 归一化到真实文件路径:
- `tower_fire_*` / `feedback_hit_*` / `p121_feedback_hit` → `SFX/Hit/routine_hit`
- `tower_fire_*` 按塔类型 → `SFX/Tower/fire_<kind>`(如 `tower_fire_raillancer` → `SFX/Tower/fire_rail_lancer`)
- `tower_build` / `tower_upgrade_*` → `SFX/UI/tower_place` / `tower_upgrade`
- `feedback_armor_break` / `p121_armor_break` / `feedback_slow` → `SFX/Status/armor_break` / `slow_apply`
- `wave_start` / `wave_transition` / `wave_clear` → `SFX/UI/wave_start` / `wave_start` / `wave_clear`
- `run_victory` / `run_defeat` → `Music/victory_stinger` / `defeat_stinger`
- `resonance_ready` / `resonance_end` / `resonance_ember_surge` / `resonance_fracture_mark` / `matrix_convergence_*` → `SFX/Resonance/window_open` / `window_close` / `ember_surge` / `fracture_mark` / `matrix_convergence`
- `boss_phase` / `boss_warning` → `SFX/Enemy/boss_phase_shift` / `boss_spawn`
- `leak_*` / `critical_defense` → `SFX/Enemy/enemy_leak` / `Hit/boss_hit`
- `scenario_command` → 由改动 4 按 type 细分
- `danger_lane` / `wave_transition`(P134)等其余 → 保持合成回退

**改 `PlaySfxTone` (line 11270)** — 在 `CreateSfxClip` 之前插入:
```csharp
if (!_sfxClipCache.TryGetValue(key, out var clip) || clip == null)
{
    var resourcePath = ResolveSfxResourcePath(key);
    if (!string.IsNullOrEmpty(resourcePath))
        clip = Resources.Load<AudioClip>($"{AudioBasePath}/{resourcePath}");
    clip ??= CreateSfxClip(key, frequency, duration, rising);  // 回退
    if (clip == null) return;
    _sfxClipCache[key] = clip;
}
```
**同样改 `PlayCriticalSfxTone` (line 11292)**。

---

### 改动 2:TDGameManager.cs — 音乐与环境音系统

**`ConfigureSfx()` (line 11221) 末尾**新增:
- 创建 `_musicSource`、`_ambienceSource`(loop=true, spatialBlend=0)
- 应用 `_musicVolume` 滑块

**新增 `ResolveMapAmbiencePath(string mapId)`**:5 张地图 → 5 个环境音文件(`grayline_junction` 等)

**新增 `ResolveChapterMusicPath()`:**按 `_campaignRoute.level.levelIndex` 算 chapter A/B/C/D → `Music/combat_chapter_a..d`

**新增 `UpdateMusicState()`:**状态机
- `_missionBoardOpen` → `Music/menu_theme`
- 战斗中 + 共鸣窗口开 → `Music/resonance_window`
- 战斗中 → `Music/combat_chapter_*`
- `_gameOver && _victory` → `Music/victory_stinger`(一次)
- `_gameOver && !_victory` → `Music/defeat_stinger`(一次)

**调用点:**在 `Update()` 末尾调用 `UpdateMusicState()`(每帧检查状态切换,仅切换时才 reload clip)。
**环境音:**在 `BuildBoard()`(line 11408 拿到 mapId 后)调用一次设置 `_ambienceClip`,战斗期间持续循环。

---

### 改动 3:补齐当前无声的事件(新增 PlaySfxTone 调用)

| 事件 | 文件:行 | 新增 key | 映射文件 |
|---|---|---|---|
| 敌人死亡(通用) | `NotifyEnemyKilled` :10710 | `enemy_death` | `SFX/Enemy/death_generic` |
| 孢子分裂 | `NotifyEnemyKilled` :10736 | `enemy_spore_split` | `SFX/Enemy/spore_split` |
| 模仿变形 | TDEnemy.cs `TryPlayMimicShiftFx` :991 | `enemy_mimic_shift` | `SFX/Enemy/mimic_shift` |
| 伏击突袭 | TDEnemy.cs `TryPlayBurrowAmbushFx` :936 | `enemy_burrow_ambush` | `SFX/Enemy/burrow_ambush` |
| 精英施压 | TDEnemy.cs `TryPlayElitePressureFx` :1115 | `enemy_elite_pressure` | `SFX/Enemy/elite_pressure` |
| 消耗虹吸 | TDEnemy.cs `TryUpdateAttritionSiphonFx` :1029 | `enemy_attrition` | `SFX/Enemy/attrition_siphon` |
| 支援链接 | TDEnemy.cs `TryUpdateSupportLinkFx` :1057 | `enemy_support_link` | `SFX/Enemy/support_link` |
| 暴露标记 | TDEnemy.cs `ApplyExposed` :637 | `status_expose` | `SFX/Status/expose_mark` |
| 专精大招 | `NotifyUltimateEffect` :6091(加轻量节流) | `specialization_ult` | `SFX/Status/specialization_ult` |

**TDEnemy.cs 改动说明:**TDEnemy 有 `_gameManager` 字段(:48),但 `PlaySfxTone` 是 private。改为在 TDEnemy 里调用已有的 `_gameManager.NotifyEnemyKilled` 类似的公开通知模式 —— 新增一个 `public void PlayEnemySfx(string key)` 转发方法到 TDGameManager,让 TDEnemy 的 FX 方法调用它。这样不暴露内部、保持架构一致。

---

### 改动 4:UI 交互 SFX + 场景机制细分

| 事件 | 文件:行 | 映射文件 |
|---|---|---|
| 悬停塔/建造点 | `UpdateBuildPreviewUnderCursor` :18222(加去抖字段 `_lastHoverSfxTower`) | `SFX/UI/hover` |
| 面板打开 | `OpenMissionBoard`:3515 / `OpenFormationPanel`:3930 / `OpenCampaignProfile`:3641 | `SFX/UI/panel_open` |
| 面板关闭 | `CloseMissionBoard`:3541 / `CloseFormationPanel`:3983 / `CloseCampaignProfile`:3653 | `SFX/UI/panel_close` |
| 关卡选择 | `SelectMissionBoardLevel` :3559 | `SFX/UI/level_select` |
| 部署确认 | `DeploySelectedMission` :5049 | `SFX/UI/deploy_confirm` |
| 早发调度 | `TryRequestWaveStart` :12616 分支内 | `SFX/UI/early_dispatch` |
| 教程推进 | `AdvanceTutorial` :1956 | `SFX/UI/tutorial_advance` |
| 教程完成 | `CompleteFirstRunTutorial` :1982 | `SFX/UI/tutorial_complete` |
| 章节奖励 | `TryClaimChapterReward` :3621 后 | `SFX/UI/chapter_reward` |
| 场景-route_switch | `TryActivateScenarioMechanic` :2678 分支 | `SFX/Scenario/route_switch` |
| 场景-reinforcement | :2682 分支 | `SFX/Scenario/reinforcement_train` |
| 场景-environment | :2686 分支 | `SFX/Scenario/kiln_purge` |
| 场景-boss_breaker | :2689 分支 | `SFX/Scenario/boss_breaker` |
| 场景-signal_gate | :2692 default | `SFX/Scenario/signal_gate` |

场景机制现有 :2700 的通用 `scenario_command` 保留,但各分支在其 case 内额外加一个更具体的音。

---

### 改动 5:验证

1. 确认编译通过(无语法错误)
2. 用 `td_mcp_playtest.ps1` 跑一局 L01 自动战斗(如果 MCP 可用),或至少确认无编译错误
3. 检查 Unity Console 无 Resources.Load 失败警告
4. Git 提交(单独 commit: "wire audio assets into playback code")

### 不做的事
- 不改 .wav 文件本身
- 不改 .meta(已入库)
- 不改音频压缩/平台设置(留后续)
- 不删 `CreateSfxClip`(保留作回退,无对应文件的 key 仍用合成音)
- 不动 P134/P121 审计断言(它们检查 `_sfxClipCache.ContainsKey`,现在 cache 里会有真实 clip,断言照样通过)