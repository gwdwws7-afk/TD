# QA 实机验证结果 — B103 + 开火帧(任务 2,plan v3)

日期:2026-08-17 · 执行人:QA 会话 · 通道:Unity MCP + InputSystem 模拟
截图证据:`output/playtest/b103_*.png` · runbook:`tools/qa-runbook-b103-fireframes.md`

## A. B103 基线 — 全部通过 ✅

| 步骤 | 结果 | 证据 |
|---|---|---|
| A1 编译 | ✅ 0 错误(Assembly-CSharp-Editor 修复生效) | read_console |
| A2 Reimport 菜单 | ✅ `TD/Art/Reimport Batch 103 (P12 Exam + anim fx + decal/prop)` 执行成功(幂等无变更日志=设置已正确) | execute_menu_item |
| A3 PPU 抽查 | ✅ 4/4 = 1024(decal_hollow_kiln_basin_path_a、device_reserve_train、fire 帧 ×2) | TextureImporter 实读 |
| A4a L1 贴花/道具 | ✅ 无 10.24× 巨幅贴片、无悬浮方块,场景协调 | b103_A4a 截图 + 视觉模型 |
| A4b hollow_kiln 贴花可读性 | ⚠️ **回美术**:尺寸/贴地正确,但读作"分散大色块"而非路径贴片 → 建议降不透明度(57%→~35%)+ 收窄贴片 | b103_A4b 截图 + 视觉模型 |
| A5 考试设备 | ✅ 正经美术(工业熔炉机械+光环+充能读数),无信号柱占位图 | b103_A5 截图;fixture ready=True, identity=purge_timing |
| A6 FX 抽查 | ✅ husk_titan 金红警示环+横幅、burrow_sapper 出土爆尘、spore 正常渲染、无洋红错误纹理 | b103_A6 截图 ×2 |
| A7 内存 sanity | ✅ 884 张运行时纹理共 255MB(512 压缩+DXT5 生效) | Profiler 实读 |

## B. 开火帧实机 — 全部通过 ✅

| 步骤 | 结果 | 证据 |
|---|---|---|
| B8 资产在位 | ✅ 48 帧 = 24 基础 + 24 t3(8 塔种 × 3 帧,Resources.LoadAll 实数) | execute_code |
| B9 15FPS 连播 | ✅ 炮口黄白能量爆发清晰可见,连拍 4 帧无卡帧/回跳 | b103_B9_fire_a-d |
| B10 后坐读感 | ✅ 塔体发射姿态/后坐偏移 + 地面阴影位移同步,弹道可见 | 同上,视觉模型确认 |
| B11 T3 专用帧 | ✅ t3 塔 fireFrames 首帧 = `tower_rail_lancer_t3_fire_00`(功能级直读) | animator 反射 |
| B11 回退路径 | ✅ 临时移除 t3_fire_00 后重建 t3 塔 → 回落 `tower_rail_lancer_fire_00` 基础帧(5b2f103 逻辑实测生效);资产已恢复,git 干净 | 双向反射实测 |

## 结论

- B103 导入修复 + 48 开火帧 + T3 解析/回退:**实机验证全部通过**,可进入全量回归(任务 3)。
- 唯一遗留(非阻塞):A4b hollow_kiln 路径贴花观感 → 回美术会话决策(降不透明度/收窄/重绘)。
- 交接单"已知非阻塞项"(3 张建议重跑 prop、假 PNG logo)维持 backlog 状态。
