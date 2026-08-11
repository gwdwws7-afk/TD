# Sprint 2.1 美术需求 — 塔开火动画帧

> 代码框架已完成(TDSpriteAnimator 状态机 + TDTower.FireAt → PlayFire)。
> 一旦以下帧图放入 Assets/Resources/Art/anim/,开火动画会自动生效。

## 文件命名规范

```
tower_{kind}_fire_00.png   (第1帧)
tower_{kind}_fire_01.png   (第2帧)
tower_{kind}_fire_02.png   (第3帧)
```

代码加载路径: `Art/anim/tower_{kind}_fire_{00,01,02}`

## 8 种塔的 kind 标识

| 塔名 | kind | 文件名前缀 | 示例 |
|---|---|---|---|
| Rail Lancer | rail_lancer | `tower_rail_lancer_fire_00.png` | 蓝白色枪口闪光 |
| Cinder Mortar | cinder_mortar | `tower_cinder_mortar_fire_00.png` | 橙色炮口烟尘 |
| Frost Coil | frost_coil | `tower_frost_coil_fire_00.png` | 青白色冰晶爆发 |
| Arc Welder | arc_welder | `tower_arc_welder_fire_00.png` | 蓝白色电弧闪烁 |
| Siege Drill | siege_drill | `tower_siege_drill_fire_00.png` | 金色钻头火花 |
| Ember Flak | ember_flak | `tower_ember_flak_fire_00.png` | 橙红色高炮火焰 |
| Resonance Beacon | resonance_beacon | `tower_resonance_beacon_fire_00.png` | 绿色脉冲光环 |
| Grav Snare | grav_snare | `tower_grav_snare_fire_00.png` | 蓝紫色重力波纹 |

## 规格

- 尺寸: 1024×1024 PNG(与现有 idle 帧一致)
- 帧数: 每塔 3 帧(00/01/02)
- 总量: 8 塔 × 3 帧 = **24 张**
- 风格: 余烬铁道工业风,深色底,金属质感,开火瞬间高亮
- 播放速度: 15 FPS(0.2 秒完成 3 帧)
- 升级后(t3)的开火帧可选:`tower_{kind}_t3_fire_00.png` 等(同样 3 帧)

## 可选: t3 升级版开火帧

如果需要升级后的塔有不同开火效果:
```
tower_{kind}_t3_fire_00.png
tower_{kind}_t3_fire_01.png
tower_{kind}_t3_fire_02.png
```
额外 24 张(可选)。

## 放置位置

```
Assets/Resources/Art/anim/
  tower_rail_lancer_fire_00.png
  tower_rail_lancer_fire_01.png
  tower_rail_lancer_fire_02.png
  tower_cinder_mortar_fire_00.png
  ...
```

放置后无需改代码 — `ConfigureFire` 在塔初始化时自动加载。无文件时 `PlayFire()` 是静默无操作(idle 循环继续)。
