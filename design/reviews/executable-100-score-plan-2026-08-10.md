# Emberline Defense — 满分可执行方案

> 版本: 1.0 | 日期: 2026-08-10 | 基线提交: `52d8f64` | 当前评分: **72/100**
>
> 本文档是**行动手册**,不是分析报告。每个 Sprint 有明确的文件清单、代码骨架、验证标准。

---

## 总览

```
Sprint 0  已完成  平衡修复 + 音频接入 + 经济压缩          (72 分基线)
Sprint 1  7 天    对象池 + AudioMixer + UI 动画          (72 → 87)
Sprint 2  8 天    塔状态动画 + 本地化 JSON + 单元测试      (87 → 94)
Sprint 3  5 天    IL2CPP + Addressables + 考试美术        (94 → 97)
Sprint 4  3 天    平衡回归测试 + 终局验证 + 发布物料       (97 → 98)
                                            总计: ~23 天 → 98 分
```

---

## Sprint 1: 性能 + 音频 + 交互(7 天,72→87)

### 1.1 对象池系统(2.5 天,+6 分)

**目标**: 消除 Instantiate/Destroy 的 GC 尖刺,稳定 60FPS。

**改动的文件**:
- 新建 `Assets/Scripts/TowerDefense/TDObjectPool.cs`
- 修改 `Assets/Scripts/TowerDefense/TDProjectile.cs`(line 160, 169 的 `Destroy`)
- 修改 `Assets/Scripts/TowerDefense/TDTransientSpriteFx.cs`(line 24, 56 的 `Destroy`)
- 修改 `Assets/Scripts/TowerDefense/TDGameManager.cs`(line 13228 的 `new GameObject` enemy spawn)

**代码骨架** — `TDObjectPool.cs`:
```csharp
using UnityEngine;
using UnityEngine.Pool;

namespace TD
{
    /// <summary>
    /// Centralized pool manager for projectiles, FX, and enemy visuals.
    /// Replaces ad-hoc Instantiate/Destroy with pooled Get/Release.
    /// </summary>
    public sealed class TDObjectPool : MonoBehaviour
    {
        public static TDObjectPool Instance { get; private set; }

        private IObjectPool<TDProjectile> _projectilePool;
        private IObjectPool<TDTransientSpriteFx> _fxPool;

        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private GameObject _fxPrefab;
        [SerializeField] private int _defaultCapacity = 64;
        [SerializeField] private int _maxSize = 256;

        private void Awake()
        {
            Instance = this;
            _projectilePool = new ObjectPool<TDProjectile>(
                CreatePooledProjectile,
                OnGetProjectile,
                OnReleaseProjectile,
                OnDestroyPooled,
                collectionCheck: true,
                _defaultCapacity,
                _maxSize);
            _fxPool = new ObjectPool<TDTransientSpriteFx>(
                CreatePooledFx,
                OnGetFx,
                OnReleaseFx,
                OnDestroyPooled,
                collectionCheck: true,
                _defaultCapacity / 2,
                _maxSize / 2);
        }

        public TDProjectile GetProjectile(Vector3 pos, Quaternion rot)
        {
            var p = _projectilePool.Get();
            p.transform.SetPositionAndRotation(pos, rot);
            return p;
        }

        public void ReleaseProjectile(TDProjectile p) => _projectilePool.Release(p);
        public TDTransientSpriteFx GetFx() => _fxPool.Get();
        public void ReleaseFx(TDTransientSpriteFx fx) => _fxPool.Release(fx);

        // ... factory + callback implementations
    }
}
```

**迁移步骤**(逐文件):
1. `TDProjectile.cs`: 把 `Destroy(gameObject)` → `TDObjectPool.Instance.ReleaseProjectile(this)` + `gameObject.SetActive(false)` in OnRelease
2. `TDTransientSpriteFx.cs`: 同理,`Destroy` → `ReleaseFx` + deactivate
3. `TDGameManager.cs:13228`: enemy 对象暂不池化(`new GameObject` 有唯一命名,池化收益小),仅池化 projectile 和 FX

**验证标准**:
- [ ] L20 wave 10 密集战斗(38 敌人)下 GC.Alloc < 4KB/frame(用 Profiler 验证)
- [ ] 连续 20 分钟 soak 无内存增长
- [ ] 视觉无回退(projectile/FX 正常显示)

**回归风险**: 池化对象未正确重置 → 残留状态。在每个 `OnGet` 回调中重置所有字段。

---

### 1.2 AudioMixer 混音系统(1.5 天,+5 分)

**目标**: 4 总线混音(Master/Music/SFX/Ambience),SFX ducking,快照转场。

**改动的文件**:
- 新建 `Assets/Audio/EmberlineMixer.mixer`(Unity Editor 中创建)
- 修改 `Assets/Scripts/TowerDefense/TDGameManager.cs`(`ConfigureSfx` line 11229, `ApplySfxVolumes` line 11245)
- 修改 `Assets/Scripts/TowerDefense/TDGameManager.cs`(`UpdateMusicState` ~line 11395)

**AudioMixer 结构**:
```
Master (0 dB)
├── Music    (-8 dB, volume exposed as "MusicVolume")
├── SFX      (-3 dB, volume exposed as "SfxVolume")
│   ├── Routine    (tower fire, hit — routed from _sfxSource)
│   ├── Tactical   (armor break, slow — from _tacticalSfxSource)
│   └── Critical   (boss, leak, defeat — from _criticalSfxSource)
└── Ambience  (-12 dB, volume exposed as "AmbienceVolume")
```

**Snapshots**:
- `Normal` — 默认各总线电平
- `BossPhase` — Music -4dB, SFX +2dB(Boss 出现时切入)
- `Resonance` — Music -2dB, SFX +1dB(共鸣窗口切入)
- `Victory` / `Defeat` — Music 淡入,stinger 播放

**代码改动** — `ConfigureSfx`:
```csharp
private AudioMixer _emberlineMixer;
private AudioMixerSnapshot _normalSnapshot;
private AudioMixerSnapshot _bossSnapshot;

private void ConfigureSfx()
{
    _emberlineMixer = Resources.Load<AudioMixer>("Audio/EmberlineMixer");
    // ... existing AudioSource setup ...
    // Route each AudioSource to its mixer group:
    if (_emberlineMixer != null)
    {
        var musicGroup = _emberlineMixer.FindMatchingGroups("Music")[0];
        var sfxGroup = _emberlineMixer.FindMatchingGroups("SFX")[0];
        _musicSource.outputAudioMixerGroup = musicGroup;
        _sfxSource.outputAudioMixerGroup = sfxGroup.FindMatchingGroups("Routine")[0];
        _tacticalSfxSource.outputAudioMixerGroup = sfxGroup.FindMatchingGroups("Tactical")[0];
        _criticalSfxSource.outputAudioMixerGroup = sfxGroup.FindMatchingGroups("Critical")[0];
        _ambienceSource.outputAudioMixerGroup = _emberlineMixer.FindMatchingGroups("Ambience")[0];
        _normalSnapshot = _emberlineMixer.FindSnapshot("Normal");
        _bossSnapshot = _emberlineMixer.FindSnapshot("BossPhase");
    }
}
```

**验证标准**:
- [ ] Boss 出现时音乐自动降低 4dB(`_bossSnapshot.TransitionTo(0.8f)`)
- [ ] 3 个音量滑块(Master/Music/SFX)独立控制各自总线
- [ ] 无叠音爆音(SFX 不超过 -3dB 峰值)

---

### 1.3 UI 动画 + Tooltip(3 天,+8 分)

**目标**: 面板 open/close 动画、hover tooltip、波次转场。

**依赖**: 安装 DOTween(Demigiant,免费 MIT)。通过 Unity Package Manager 或 Asset Store。

**改动的文件**:
- 新建 `Assets/Scripts/TowerDefense/TDUiAnimator.cs`
- 修改 `Assets/Scripts/TowerDefense/TDTowerTooltip.cs`(新建)
- 修改 `Assets/Scripts/TowerDefense/TDGameManager.cs`(`OpenMissionBoard` line 3525, `CloseMissionBoard` line 3551, `OpenFormationPanel` line 3940, 等)

**代码骨架** — `TDUiAnimator.cs`:
```csharp
using DG.Tweening;
using UnityEngine;

namespace TD
{
    public static class TDUiAnimator
    {
        private const float PanelDuration = 0.18f;
        private const float TooltipDelay = 0.4f;

        /// <summary>面板打开:从 0.85 缩放 + 0 透明度 → 1.0 + 1.0</summary>
        public static Tween PanelOpen(RectTransform rt)
        {
            rt.localScale = Vector3.one * 0.85f;
            var cg = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            return DOTween.Sequence()
                .Append(rt.DOScale(Vector3.one, PanelDuration).SetEase(Ease.OutCubic))
                .Join(cg.DOFade(1f, PanelDuration));
        }

        /// <summary>面板关闭:反向,完成后 SetActive(false)</summary>
        public static Tween PanelClose(RectTransform rt, System.Action onComplete = null)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            return DOTween.Sequence()
                .Append(rt.DOScale(Vector3.one * 0.85f, PanelDuration * 0.7f).SetEase(Ease.InCubic))
                .Join(cg?.DOFade(0f, PanelDuration * 0.7f) ?? null)
                .OnComplete(() => { onComplete?.Invoke(); });
        }

        /// <summary>波次转场:全屏暗化 + 文字淡入</summary>
        public static Tween WaveTransition(CanvasGroup overlay, string text)
        {
            return DOTween.Sequence()
                .Append(overlay.DOFade(0.7f, 0.3f))
                .AppendInterval(0.8f)
                .Append(overlay.DOFade(0f, 0.3f));
        }
    }
}
```

**Tooltip 系统** — `TDTowerTooltip.cs`:
```csharp
namespace TD
{
    public sealed class TDTowerTooltip : MonoBehaviour
    {
        // 悬停塔 0.4s 后显示:
        // Line 1: 塔名 + 费用
        // Line 2: 伤害 / 射程 / 射速
        // Line 3: 克制标签 (heavy/swarm/fast...)
        // Line 4: 当前升级等级 + 专精状态
        // 右下角跟随鼠标
    }
}
```

**接入点**:
- `OpenMissionBoard` → 在 `_missionBoardOpen = true;` 后加 `TDUiAnimator.PanelOpen(_uiMissionRoot)`
- `CloseMissionBoard` → 在 `_missionBoardOpen = false;` 前加 `TDUiAnimator.PanelClose(...)` async
- `UpdateBuildPreviewUnderCursor`(line 18222) → 悬停塔 0.4s 后 `TDTowerTooltip.Show(tower)`
- `TryRequestWaveStart`(line 12598) → 波次开始时 `TDUiAnimator.WaveTransition(...)`

**验证标准**:
- [ ] 所有面板 open/close 有 0.18s 缩放+淡入动画
- [ ] 悬停塔 0.4s 后显示 tooltip,移开后消失
- [ ] 波次开始有 0.6s 全屏暗化转场
- [ ] 960×540 分辨率下 tooltip 不溢出屏幕边缘

**Sprint 1 验收**: 87 分。对象池 + AudioMixer + UI 动画完成。

---

## Sprint 2: 视觉深度 + 国际化 + 质量(8 天,87→94)

### 2.1 塔状态动画(4 天,+7 分,需美术)

**现状**: 8 塔各有 6 帧 idle 循环(`tower_X_00..05.png`)+ 6 帧 t3 升级循环。无 fire/charge 状态。

**需要美术生产的新帧**:
```
每塔需要:
  tower_X_fire_00.png, tower_X_fire_01.png, tower_X_fire_02.png  (3 帧开火)
  tower_X_t3_fire_00.png, tower_X_t3_fire_01.png, tower_X_t3_fire_02.png

共: 8 塔 × 6 新帧 = 48 张 1024×1024 PNG
每敌人需要:
  enemy_X_death_00.png, enemy_X_death_01.png, enemy_X_death_02.png  (3 帧死亡)
共: 12 敌人 × 3 新帧 = 36 张 1024×1024 PNG
总计: 84 张新美术帧
```

**代码改动** — `TDTower.cs` 扩展 `TowerState`:
```csharp
public struct TowerState
{
    // 现有字段...
    public string fireAnimationPrefix;    // 新增: "Art/anim/tower_rail_lancer_fire"
    public int fireAnimationFrames;       // 新增: 3
    public float fireAnimationFps;        // 新增: 15f (快闪)
}
```

**代码改动** — `TDSpriteAnimator.cs`:
```csharp
// 新增状态机:
public enum TDAnimationState { Idle, Fire, Charge }

private TDAnimationState _state = TDAnimationState.Idle;
private float _fireAnimTimer;

public void PlayFire()
{
    _state = TDAnimationState.Fire;
    _fireAnimTimer = fireFrames / fireFps;  // 3帧/15fps = 0.2秒
}

// Update 中:
if (_state == TDAnimationState.Fire)
{
    _fireAnimTimer -= Time.deltaTime;
    // 播放 fire 帧序列
    if (_fireAnimTimer <= 0f) _state = TDAnimationState.Idle;
}
else { /* 播放 idle 循环 */ }
```

**触发点**: `TDTower.cs` 的 `NotifyTowerFired` / 开火逻辑处调用 `_animator.PlayFire()`。

**验证标准**:
- [ ] 8 塔开火时有 0.2s 独立开火动画(枪口闪光/后坐力)
- [ ] 12 敌人死亡时有 3 帧死亡动画(而非颜色淡出)
- [ ] t3 升级后的塔有独立的开火动画

---

### 2.2 本地化 JSON 化(2 天,+4 分)

**现状**: `TDLocalization.cs` 有 550 个硬编码 `new("English","中文")` 对。

**步骤**:

1. **导出** — 写脚本将 550 对导出为 `Assets/Resources/Localization/strings.json`:
```json
{
  "en": { "wave_start": "WAVE {0} START", "victory": "VICTORY", ... },
  "zh": { "wave_start": "第 {0} 波 开始", "victory": "胜利", ... },
  "ja": { "wave_start": "波 {0} 開始", "victory": "勝利", ... }
}
```

2. **重构** `TDLocalization.cs`:
```csharp
private static Dictionary<string, Dictionary<string, string>> _strings;
private static string _currentLang = "en";

public static void LoadLanguage(string lang)
{
    var json = Resources.Load<TextAsset>("Localization/strings").text;
    _strings = JsonUtility.Deserialize<...>(json); // 或 Newtonsoft.Json
    _currentLang = lang;
}

public static string Localize(string key, params object[] args)
{
    if (_strings.TryGetValue(_currentLang, out var lang)
        && lang.TryGetValue(key, out var s))
        return string.Format(s, args);
    return key; // fallback
}
```

3. **新增语言**: 日语(ja)、韩语(ko)。翻译外包(550 条 × 2 语言 ≈ $300-500)。

**验证标准**:
- [ ] 中/英/日/韩 4 语言切换无重启
- [ ] 德语/俄语等长文本不溢出(UI 使用 ContentSizeFitter)
- [ ] 添加新语言只需加 JSON key,不改代码

---

### 2.3 Unity 测试框架(2 天,+3 分)

**步骤**:

1. **创建测试 Assembly**:
```
Assets/Scripts/TD.Tests/TD.Tests.asmdef:
{
  "name": "TD.Tests",
  "references": ["TD", "UnityEngine.TestRunner", "nunit.framework"],
  "includePlatforms": ["Editor"],
  "defines": ["UNITY_INCLUDE_TESTS"]
}
```

2. **核心逻辑测试** — `Assets/Scripts/TD.Tests/TDBalanceTests.cs`:
```csharp
using NUnit.Framework;

namespace TD.Tests
{
    public class TDArmorTests
    {
        [Test]
        public void Armor_ReducesLowDamageHit_MoreThanHighDamage()
        {
            // RailLancer 18 dmg vs Husk Titan 9 armor:
            // percent = min(0.60, 9*0.04) = 0.36
            // after_percent = 18 * 0.64 = 11.52
            // damageTaken = max(1, round(11.52 - 9)) = 3
            // 验证: 低伤打高甲 = 大幅削弱
        }

        [Test]
        public void SiegeDrill_ArmorMultiplier_BypassesPercentReduction()
        {
            // SiegeDrill 对 armored 有 heavyMult * 1.08 = 1.30 * 1.08 = 1.404
            // 20 dmg * 1.404 = 28.08 → round = 28
            // after_percent = 28 * 0.64 = 17.92
            // damageTaken = max(1, round(17.92 - 9)) = 9
            // 验证: SiegeDrill 比 RailLancer 有效得多
        }
    }

    public class TDEconomyTests
    {
        [Test]
        public void LateWaveBounty_IsSignificantlyLower()
        {
            // Wave 18/20: progress = 17/19 = 0.89
            // lateProgress = InverseLerp(0.45, 1.0, 0.89) = 0.80
            // multiplier = 0.40 * Lerp(1, 0.06, 0.80) = 0.40 * 0.248 = 0.099
            // 验证: 后期赏金 < 10% 原始值
        }
    }

    public class TDSaveTests
    {
        [Test]
        public void CorruptedSnapshot_IsRejected()
        {
            // 验证 checksum mismatch → corruptionDetected = true
        }
    }
}
```

3. **CI 接入**: 在 build 脚本中加 `unity -batchmode -runTests -testPlatform editmode`。

**验证标准**:
- [ ] 20+ edit-mode 测试覆盖护甲/经济/存档核心逻辑
- [ ] `unity -runTests` 在 CI 中全绿
- [ ] 改平衡后测试能捕捉回归

**Sprint 2 验收**: 94 分。

---

## Sprint 3: 发布就绪(5 天,94→97)

### 3.1 IL2CPP + Addressables(3 天,+3 分)

**步骤**:

1. **默认 IL2CPP** — `TDReleaseBuilder.cs`:
```csharp
// 改默认 backend
var backend = cliBackend ?? ScriptingImplementation.IL2CPP;
PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
```

2. **Addressables 迁移** — 将大资源从 `Resources/` 迁移到 Addressables:
```
优先迁移(按大小):
  Assets/Resources/Art/anim/        (252 张 1024×1024 PNG ≈ 最大块)
  Assets/Resources/Audio/           (62 个 WAV)
  Assets/Resources/Art/Combat/P11/  (冲击/投射图)

保留在 Resources/:
  Assets/Resources/Data/            (JSON 配置,小文件,需同步加载)
  Assets/Resources/Localization/    (小文件)
```

3. **按需加载** — `TDSpriteAnimator.cs`:
```csharp
// 改 Resources.Load → Addressables.LoadAssetAsync
var handle = Addressables.LoadAssetAsync<Sprite>(prefix + "_00");
await handle.Task;  // 异步加载
```

4. **多平台**: 加 macOS/Linux build target。

**验证标准**:
- [ ] IL2CPP 构建通过(P12.5.2 已验证)
- [ ] 首次场景加载内存 < 150MB(当前 ~190MB)
- [ ] Addressables 分组加载无卡顿

---

### 3.2 考试装置美术(1 天,+2 分)

**现状**: `TDExamPresentationCatalog` 引用 4 个不存在的美术路径:
- `Art/Exam/P12/device_reserve_train`
- `Art/Exam/P12/device_canyon_switch`
- `Art/Exam/P12/device_kiln_purge`
- `Art/Exam/P12/device_phase_breaker`

**步骤**: 用 MiniMax/AI 图像生成 4 张 512×512 装置图(余烬铁道工业风),放入 `Assets/Resources/Art/Exam/P12/`。

**验证标准**:
- [ ] L05/L09/L13/L17/L20 考试波次装置正常显示
- [ ] 无 fallback 日志

---

### 3.3 降低运行时 GC(1 天)

补充 Sprint 1 对象池未覆盖的:
- `TDGameManager.cs` 中的 `new List<>()` 热路径 → 复用缓存列表
- `string.Format` / 字符串拼接热路径 → `StringBuilder` 复用
- LINQ `.Where().ToArray()` 热路径 → 手动遍历

**验证标准**:
- [ ] Profiler 中零 GC.Alloc > 1KB/frame(战斗中)

**Sprint 3 验收**: 97 分。

---

## Sprint 4: 终局验证 + 发布(3 天,97→98)

### 4.1 终局完整通关验证(1 天)

**手动游玩**(无法自动化,因为 P124 在时限内打不完 20 波):
- [ ] L16-L20 手动通关,确认共鸣系统触发
- [ ] L20 击败 Furnace Matriarch(620HP),确认 Boss 战流程
- [ ] 矩阵汇聚(6 全匹配 + 2 不同专精)正常触发
- [ ] 胜利/失败结算画面 5 维度评分正常

### 4.2 平衡回归测试(1 天)

用 Sprint 2 的测试框架 + 5 关自动播放:
- [ ] RailLancer-only 策略在 L13/L20 显著变弱(更多漏怪或无法通关)
- [ ] 混合塔策略(RailLancer + SiegeDrill + FrostCoil)表现优于单塔
- [ ] 经济不再饱和(终局预算 < 150)

### 4.3 音频人工验证(0.5 天)

- [ ] 在 Unity 编辑器中实听 10 分钟 L09 战斗
- [ ] 确认: 塔开火、敌人死亡、UI 点击、波次开始/结束、音乐切换
- [ ] 确认无叠音/爆音/缺失

### 4.4 发布物料(0.5 天)

- [ ] 5 张 Steam 商店截图(1920×1080,展示各阶段)
- [ ] 30 秒预告片(展示 Boss 战 + 共鸣 + 结算)
- [ ] 商店描述文案(中英双语)
- [ ] 隐私政策/第三方致谢

**Sprint 4 验收**: 98 分。

---

## 验收检查清单(全 Sprint 汇总)

### 性能
- [ ] 对象池覆盖 projectile + FX
- [ ] GC.Alloc < 4KB/frame(战斗中)
- [ ] 20 分钟 soak 无内存增长
- [ ] 60FPS @ 1920×1080(P95 < 16ms)

### 音频
- [ ] AudioMixer 4 总线 + 3 快照
- [ ] Boss 出现自动 ducking
- [ ] 62 文件全部在正确时机播放(人工验证)
- [ ] 音量滑块独立控制各总线

### UI/UX
- [ ] 所有面板 open/close 动画(0.18s)
- [ ] 塔悬停 tooltip(0.4s 延迟)
- [ ] 波次转场动画
- [ ] 960×540 可读性达标

### 视觉
- [ ] 8 塔有 fire 动画(3 帧)
- [ ] 12 敌人有 death 动画(3 帧)
- [ ] 考试装置美术完整

### 平衡
- [ ] RailLancer-only 在 L13+ 不再轻松通关
- [ ] SiegeDrill 对高甲敌人必需
- [ ] FrostCoil 对快速敌人必需
- [ ] 终局预算 < 150

### 国际化
- [ ] 4 语言(中/英/日/韩)
- [ ] JSON 驱动,加语言不改代码
- [ ] 长文本不溢出

### 质量
- [ ] 20+ edit-mode 测试
- [ ] CI 测试全绿
- [ ] 存档校验测试通过

### 发布
- [ ] IL2CPP 构建通过
- [ ] macOS/Linux 构建通过
- [ ] Addressables 分组
- [ ] 商店物料完整

---

## 分数追踪

| Sprint | 完成后分数 | 关键交付 |
|---|---|---|
| Sprint 0(已完成) | 72 | 平衡修复 + 音频接入 + 经济压缩 |
| Sprint 1 | **87** (+15) | 对象池 + AudioMixer + UI 动画 |
| Sprint 2 | **94** (+7) | 塔状态动画 + 本地化 JSON + 测试 |
| Sprint 3 | **97** (+3) | IL2CPP + Addressables + 考试美术 |
| Sprint 4 | **98** (+1) | 终局验证 + 平衡回归 + 发布物料 |

**98 → 100 的最后 2 分**: 预告片品质、社区口碑、媒体评测、发售时机。这些不是工程能解决的,需要市场和运营投入。

---

## 附录:文件改动清单

### 新建文件(14 个)
```
Assets/Scripts/TowerDefense/TDObjectPool.cs
Assets/Scripts/TowerDefense/TDUiAnimator.cs
Assets/Scripts/TowerDefense/TDTowerTooltip.cs
Assets/Audio/EmberlineMixer.mixer
Assets/Scripts/TD.Tests/TD.Tests.asmdef
Assets/Scripts/TD.Tests/TDBalanceTests.cs
Assets/Scripts/TD.Tests/TDEconomyTests.cs
Assets/Scripts/TD.Tests/TDSaveTests.cs
Assets/Resources/Localization/strings.json
Assets/Resources/Art/Exam/P12/device_reserve_train.png
Assets/Resources/Art/Exam/P12/device_canyon_switch.png
Assets/Resources/Art/Exam/P12/device_kiln_purge.png
Assets/Resources/Art/Exam/P12/device_phase_breaker.png
+ 84 张塔/敌人状态动画帧(美术生产)
```

### 修改文件(10 个)
```
Assets/Scripts/TowerDefense/TDProjectile.cs          (Destroy → pool release)
Assets/Scripts/TowerDefense/TDTransientSpriteFx.cs   (Destroy → pool release)
Assets/Scripts/TowerDefense/TDSpriteAnimator.cs      (状态机: idle/fire/charge)
Assets/Scripts/TowerDefense/TDTower.cs               (fire animation prefix + PlayFire trigger)
Assets/Scripts/TowerDefense/TDEnemy.cs               (death animation trigger)
Assets/Scripts/TowerDefense/TDGameManager.cs         (UI animator 接入 + mixer + tooltip hook)
Assets/Scripts/TowerDefense/TDLocalization.cs        (JSON 驱动重构)
Assets/Scripts/TowerDefense/TDP123SettingsPanel.cs   (mixer volume 滑块)
Assets/Editor/TDReleaseBuilder.cs                    (IL2CPP 默认 + Addressables)
Assets/Scripts/TowerDefense/TDCampaignLoader.cs      (locale 初始化)
```
