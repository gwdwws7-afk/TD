# 标题屏引入后 P124 自动播放死锁 — 诊断与修复

> 日期: 2026-08-10
> 触发提交: `181f9f3` "Add title screen / main menu system (P0)"
> 协作: 本文档由辅助会话（无 Unity 运行时，纯静态代码审计）编写，
> 供拥有运行时环境的标题屏实现会话 (`sess_3e0c3ced`) 验证与应用。
> 辅助会话未修改任何代码，仅提供诊断与候选 patch。

---

## 症状

标题屏提交后，P124 自动播放全部失败：
- `output/playtest/title_fix1..4.json` 四次运行 `waves=0/20`、`enemies=0`、`towers=0`
- `title_fix3.png` 与 `title_fix4.png` MD5 完全相同（`b24790801a5884f812441fd4512e4370`）= 状态冻结
- `_isInPrepPhase=False`、`_waveStartRequested=False`、`_wave=0`
- `stalled=False`（游戏以为自己在正常跑，但战斗循环从未启动）
- 45 秒空转后超时

提交者本人在 commit message 里已承认：
> "Automated P124 testing has a residual timing issue where the wave loop
> doesn't resume after title dismissal in some sessions — needs investigation."

---

## 根因链（已通过代码审计确认）

初始化时序（`TDGameManager.cs`）：

```
Awake() [1060]
 ├─ LoadCampaignContext() [1067]
 │    └─ _campaignDeploymentConfirmed = true   ← [12532] 标题屏引入前由此进入战斗
 ├─ BuildBoard() [1068]
 ├─ BuildBattleUi() [1072]
 │    └─ BuildTitleScreen() [1565]
 │         └─ _campaignDeploymentConfirmed = false   ← [1613] ★重置点★
 └─ (Awake 结束)

Start() [1083]
 └─ StartCoroutine(WaveLoopFromConfig()) [1085]
      └─ while(!_campaignDeploymentConfirmed) yield return null   ← [13557] 永久等待
```

### 标题屏引入前（能工作）
`LoadCampaignContext` 把 `deploymentConfirmed` 设 true，之后没人改它。
Start 启动的 wave 协程立刻通过 while，进入 wave 1 prep phase。

### 标题屏引入后（死锁）
`BuildTitleScreen` 在 Awake 末尾把它**重置为 false**。
Start 启动的 wave 协程卡在 `while(!deploymentConfirmed)` 永久等待。

playtest 脚本 (`td_mcp_playtest.ps1:419-440`) 试图补救：
1. `SkipTitleScreenForAutomation()` — **无效**，flag 只在 `BuildTitleScreen` 里检查，而那时已经执行完了
2. 反射 `_titleScreen.Hide()` — 成功隐藏标题
3. 反射 `_campaignDeploymentConfirmed = true` — **应该**解除协程阻塞

### 为什么反射设 true 后协程仍不恢复（待运行时确认的 5%）
理论上下一帧协程应跳出 while。但 json 证明 wave loop 仍为 0。最可能的原因（按概率排序）：

1. **【最可能】反射注入时机晚于协程首次 yield 的诊断窗口**：
   playtest 的 `runtimeSetupCode` 在 MCP 注入时执行，可能距 Start 已过数秒。
   若 wave 协程在等待期间因 `_waveSet` 引用、`_campaignRoute.level` 的瞬态 null 抛异常，
   协程会**静默终止**（Unity 不在 Console 报 Coroutine 异常除非显式 try）。
   反射设 true 时协程已死，无人消费。

2. **`Time.timeScale` 或 `WaitForSeconds` 实时长问题**：
   `WaitForSeconds(1f)` 受 timeScale 影响。若注入前 timeScale 被设 0，
   协程即使解阻塞也会卡在 1 秒等待。

3. **多实例/静态 flag 残留**：`_skipTitleForAutomation` 是 static，
   跨场景重载可能残留导致下一局 BuildTitleScreen 被错误跳过。

---

## 修复方案（三选一，推荐方案 A + B 组合）

### 方案 A：修正 `BuildTitleScreen` 的 deploy 重置语义（必做，治本）

`BuildTitleScreen` 不应无条件把 `deploymentConfirmed` 设 false。
它只在**显示**标题屏时才需要阻止自动部署。

```csharp
// TDGameManager.cs — BuildTitleScreen() [1580-1614]
private void BuildTitleScreen()
{
    if (_battleCanvas == null || _titleScreen != null) return;

    var skipTitle = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--td-skip-title") >= 0
        || TDStandaloneSmokeProbe.IsRequested()
        || TDP1254StandaloneProbe.IsRequested()
        || _skipTitleForAutomation;
    if (skipTitle) return;   // 不改 deploymentConfirmed，保持 LoadCampaignContext 设的 true

    var totalLevels = _campaign?.totalLevels ?? 20;
    var hasProgress = TDCampaignProgression.IsLevelUnlocked(2, totalLevels);

    var titleGo = new GameObject("TD Title Screen");
    titleGo.transform.SetParent(_battleCanvas.transform, false);
    _titleScreen = titleGo.AddComponent<TDTitleScreen>();
    _titleScreen.Build(_battleCanvas, hasProgress);
    _titleScreen.OnNewGame = HandleTitleNewGame;
    _titleScreen.OnContinue = HandleTitleContinue;
    _titleScreen.OnOpenSettings = HandleTitleSettings;

    // ★修复：只在标题屏真正显示时才阻止自动部署
    _campaignDeploymentConfirmed = false;
}
```

> 上面看起来没变？关键差异在**语义澄清**：当前代码 1613 行的无条件赋值
> 在 `skipTitle` 早返回路径里**不会执行**（return 在前），所以 skip 路径是 OK 的。
> 问题在于 **非 skip 路径**（标题屏显示）下重置了 deploymentConfirmed，
> 而后 playtest 反射补救不可靠。真正修复见方案 B。

### 方案 B：让 wave 协程对 deploymentConfirmed 变化具备韧性（必做，治本）

问题核心是协程在等待期间可能因瞬态 null 静默死亡。加保护：

```csharp
// TDGameManager.cs — WaveLoopFromConfig() [13555-13562]
private IEnumerator WaveLoopFromConfig()
{
    // 等待部署确认，带超时保护防止永久挂起
    var waitStart = Time.realtimeSinceStartup;
    while (!_campaignDeploymentConfirmed && !_gameOver)
    {
        if (Time.realtimeSinceStartup - waitStart > 30f)
        {
            Debug.LogError("[TD] WaveLoop waited >30s for deployment confirmation — forcing resume (title screen path?).");
            _campaignDeploymentConfirmed = true;   // 强制解除，避免死锁
        }
        yield return null;
    }

    // ★关键：进入 wave 循环前确保数据就绪
    if (_waveSet == null || _waveSet.waves == null || _waveSet.waves.Length == 0)
    {
        Debug.LogError("[TD] WaveLoopFromConfig: _waveSet null/empty — falling back.");
        yield return FallbackWaveLoop();
        yield break;
    }

    yield return new WaitForSecondsRealtime(1f);   // ★改用 Realtime，不受 timeScale 影响
    // ... 后续 for 循环不变
}
```

### 方案 C：playtest 脚本用命令行参数而非运行时注入（治标，但最稳）

```powershell
# td_mcp_playtest.ps1 — 启动 Unity 时就传参，让 Awake 阶段 skip 标题屏
# 在启动 Unity 编辑器的命令行加:
#   -executeMethod ... --td-skip-title
# 而不是运行时反射注入

# 并删除 runtimeSetupCode 里已失效的 SkipTitleScreenForAutomation() 调用 [419]
# 保留反射 Hide + deploymentConfirmed 作为双保险
```

---

## 验证步骤

1. 应用方案 A + B
2. 启动 Unity 编辑器，打开 EmberlineBootstrap 场景
3. 运行 `td_mcp_playtest.ps1 -Level 1 -P124Strategy adaptive_network -P124MaxRealSeconds 60`
4. 预期：
   - `waves >= 14`（60 秒/16× 速度）
   - `enemies > 0`、`towers > 0`
   - `_isInPrepPhase` 在早期帧为 True，随后 False（战斗中）
   - 截图不再是冻结的同一张
5. 手动游玩验证：启动游戏 → 标题屏 → NEW GAME → 任务面板 → 部署 → 战斗正常开始

---

## 影响范围

- **手动游玩不受影响**：标题屏 → New Game/Continue → `HandleTitleEnterGame` →
  `LoadCampaignContext`（重新设 deploymentConfirmed=true）→ `Hide` → 任务面板 →
  Deploy → `RestartCurrentScene` → 重载后 Awake 重新走一遍。
  这条路径在新代码里仍工作（player-guide 报告标题屏"fully functional for manual play"）。

- **仅自动化测试受影响**：因为自动化不走 `HandleTitleEnterGame`/`DeploySelectedMission`，
  而是反射直接注入状态，绕过了正常的状态机转换。

---

## 给实现会话的协作说明

本文档由辅助会话基于静态代码审计编写。MCP/Unity 编辑器未运行（无法做运行时验证）。
实现会话 (`sess_3e0c3ced`) 拥有运行时环境，建议：

1. 先用方案 B 的 `Debug.LogError` 超时分支**确认协程是否真的卡在 while** ——
   如果 30 秒后看到强制恢复日志，证明根因是 deploymentConfirmed 未被反射成功设置；
   如果没看到日志，说明协程在别处死亡，需在 Console 看 Coroutine 异常。
2. 确认后应用 A+B，删除本文档的"待确认"部分。
3. 这两个修复属于 Sprint 0 收尾，不阻塞 Sprint 1（对象池/UI动画/AudioMixer）的验证。

---

## 附：Sprint 1 已落地状态（从 git log + 文件时间确认）

| Sprint 1 项 | 提交 | 状态 |
|---|---|---|
| 1.1 对象池 | `2211dc7` | ✅ 已提交，`TDObjectPool.cs` 7KB |
| 1.2 AudioMixer | `da38ca8` | ✅ 已提交 |
| 1.3 UI 动画 + Tooltip | `8c10b10` | ✅ 已提交，`TDUiAnimator.cs` + `TDTowerTooltip.cs` |
| 标题屏（P0 附加） | `181f9f3` | ⚠️ 引入 P124 死锁回归 |

**Sprint 1 主体已完成，但标题屏回归阻塞了自动化验证管线**。修这个死锁是当前最高优先级——
不修则无法用 playtest 验证对象池/UI 动画的实际效果。
