# 美术需求 — 塔中间档（T2）外观帧 v1

> 背景：当前塔只有基础形态（T0-T2 共用）与 T3 形态两套视觉——第二笔升级完全无外观反馈，直到 T3 才跳变（体验缺口，v4 任务 3）。
> 本规格补齐中间档。格式沿用 `sprint2-tower-fire-animation-art-spec.md`。

## 文件命名规范

```
tower_{kind}_t2_00.png ... tower_{kind}_t2_05.png   (每塔 6 帧 idle 循环)
```

代码加载路径: `Art/anim/tower_{kind}_t2_{00..05}`

## 生效机制（挂钩已落地 `dcc649c`，与 fire 帧同为"放入即生效"）

`TDTower.ResolveVisualResourcePaths` 已支持形态回退链：Tier≥2 探测 `_t2_00` 存在即切换前缀，Tier≥3 优先 `_t3`，缺帧自动回退低档。**帧图放入 `Assets/Resources/Art/anim/` 后无需任何代码改动，升级到 Tier 2 的塔即时换肤。**

## 设计定位：三档可读的中间档

- **T0 基础**：现有 idle 6 帧，不动。
- **T2 中间档（本规格）**：**保持与基础形态 ~70% 轮廓一致**，增加 1-2 个明确可读的强化模块（镀层/副武器/支撑结构）。远看能区分"升过级"，近看能读出"强化了什么"。
- **T3 终极**：现有 `_t3` 6 帧，保留"完全体变形"的专属跳变感——T2 不得抢走 T3 的视觉份量。

## 规格表

| 塔名 | kind | T2 强化模块（在基础形态上叠加） | 视觉提示 |
|---|---|---|---|
| Rail Lancer | rail_lancer | 加长双轨枪管 + 侧面装填棘轮 | 射程感（对齐 Utility 射程分支） |
| Cinder Mortar | cinder_mortar | 副炮管 + 弹匣护板 | 双发装填感 |
| Frost Coil | frost_coil | 环形散热鳍片 + 第二线圈 | 控制覆盖感 |
| Arc Welder | arc_welder | 悬浮电极臂×2 + 接地缆 | 链路延伸感 |
| Siege Drill | siege_drill | 钻头加粗 + 液压撑脚 | 破甲力度感 |
| Ember Flak | ember_flak | 弹链鼓 + 炮口制退器 | 射速压制感 |
| Resonance Beacon | resonance_beacon | 环形天线阵 + 中继灯球 | 标记网络感 |
| Grav Snare | grav_snare | 相位环 + 锚定爪 | 力场扩张感 |

## 规格

- 尺寸: 1024×1024 PNG（与现有 idle/T3 帧一致）
- 帧数: 每塔 6 帧（00–05）循环
- 总量: 8 塔 × 6 帧 = **48 张**
- 帧率: 跟随各塔 idle 帧率（7 / 6 / 7.5 / 8.5 / 6.6 / 9 / 7.6 / 6.8 FPS，运行时按塔配置自动应用）
- 风格: 余烬铁道工业风，与基础/T3 帧同底色系；新模块用塔身份色高亮（八塔身份色见 `TDUiVisualIdentity`）
- 脚底锚点: 与基础帧相同的地面接触位置（塔不走路，无 foot_anchors 需求）
- 可读性约束: 不得遮挡塔基座、等级 pip（塔底 y=-0.34）与专精光环区域

## 可选: T2 开火帧

```
tower_{kind}_t2_fire_00.png ... _02.png   (每塔 3 帧，共 24 张，可选)
```

无 T2 fire 帧时自动回退基础 fire 帧（回退逻辑已存在并覆盖任意形态前缀）。建议第二批再考虑。

## 放置位置与导入

```
Assets/Resources/Art/anim/
  tower_rail_lancer_t2_00.png
  ... (共 48 张)
```

导入设置无需手工配置：`TDArtImporter`（AssetPostprocessor）会自动将 `anim/` 目录新图规整为 Sprite/Single + Standalone 512 压缩（与 fire 帧同流程）。

## 验证

1. 挂钩已生效（`dcc649c`）：升到 Tier 2 的塔应即时换 `_t2` 皮肤（升级瞬间切换，无过渡动画，与 T3 行为一致）
2. P134 审计（`DebugAuditP134ForTest`）八塔身份 ID 互异检查保持通过
3. QA 抽查：T0/T2/T3 三档在 960×540 下远观可区分
