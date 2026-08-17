# QA Runbook — B103 + 开火帧实机验证(任务 2,plan v3)

> 触发条件:美术会话提交 48 张开火帧(当前 96 个未跟踪文件:
> `Assets/Resources/Art/anim/tower_<kind>_fire_00..02.png` +
> `tower_<kind>_t3_fire_00..02.png` × 8 塔种)。
> 执行前置:`git status` 确认 anim 目录无未跟踪残留;TDTower.cs 的
> 未提交改动(设计会话的)仍不动。
> 通道:Unity MCP(refresh_unity / execute_code / manage_editor /
> manage_camera 截图 / read_console);python 驱动模板见
> `tools/qa_gamepad_acceptance.py` 的 Mcp 类。

## A. B103 基线(交接单 design/spec/qa-handover-b103-2026-08-17.md)

1. `refresh_unity`(force)→ read_console 0 编译错误(Assembly-CSharp-Editor 曾长期失败,重点看)。
2. 菜单 `TD/Art/Reimport Batch 103`(`execute_menu_item`)→ console 出现 `[TDArt103] Reimported ...`(~130 张)。
3. PPU 抽查(execute_code):
   `AssetImporter.GetAtPath("Assets/Resources/Art/Decals/decal_hollow_kiln_basin_path_a.png") as TextureImporter).spritePixelsPerUnit == 1024`
   同验 `device_reserve_train`。
4. L1 进 Play(沿用 playtest runtimeSetup 模板)→ manage_camera 截图:
   - 贴花/道具无 10.24× 巨幅贴片、无悬浮方块(PPU 修复生效的视觉判据);
   - hollow_kiln 图上 `decal_hollow_kiln_basin_path_a`(57% 不透明)是否读作
     "路径贴片" → **主观结论写回交接单**,供美术决定重绘。
5. 考试设备:`TDGameManager.DebugPrepareP122ExamForTest()`(调试域,编辑器可用)
   → 截图:设备非信号柱占位图,光环/充能点正常。
6. FX 抽查(-EnemyPlan 直植):
   - `husk_titan:1:default:0.3:9` → boss_warning 10 帧(12FPS 金→红渐隐);
   - `burrow_sapper:2:default:0.4:9` → 出土 burrow_ambush;
   - `spore_carrier:2:default:0.5:9` 打至低血 → 分裂预警。
   均截图 + 观帧序/节奏。
7. 内存 sanity:重导入后 `manage_profiler` stats_get /
   `get_counters(Memory)` 只读纹理较修复前(RGBA32 未压缩)低 ≥100MB。

## B. 开火帧实机(美术只能静态验,动态归 QA)

8. 8 塔种 × 3 帧(fire)+ × 3 帧(t3_fire)资产在位:
   `Resources.LoadAll<Sprite>("Art/anim")` 过滤命名,数量 = 48。
9. 15FPS 连播观感:L1 建 RailLancer(轮盘路径),开波,录像/连拍 3 张
   (间隔 ~66ms)看 muzzle flash 帧序循环无卡帧、无回跳。
10. 后坐读感:塔体 attack 反馈(TDTower.Readability.PlayAttack)与
    开火帧同步,肉眼无错拍。
11. **T3 升级瞬间开火反馈保留**:
    - 升级到 t3 的塔开火 → 应播 `tower_<kind>_t3_fire_*`(专用帧路径);
    - 反证回退路径:任选缺 t3 帧的组合(若全部塔都有 t3 帧,临时把某塔
      t3 资源名改错再录)→ 应回退普通 fire 帧而非黑块/空帧;
    - 两路径均截图留证。
12. Console 全程 0 异常;结果落档 `design/spec/qa-b103-fireframes-result.md`。

## C. 通过后接任务 3(全量回归)

- EditMode 全量(基线 30/30);
- 编辑器 + release 构建(`manage_build` 默认路径)各一局完整 playtest;
- `tools/td_raillancer_balance_regression.ps1` 25 局,RailLancer 单塔种
  胜利必须为 0——**新变量:拆塔 60% 返还;若异常优先查 autoplay 是否
  拆塔套利**(P124 决策循环买入→升级→卖出洗预算)。
