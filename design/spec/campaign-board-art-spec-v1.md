# 美术需求 — 战役关卡选择界面（World Map）v1

> 背景：`TDWorldMap.cs`（463 行）当前为纯程序化 UI——实色章节带、彩色圆圈节点、
> 默认字体、调暗的启动背景。用户实机反馈"非常原始"。本规格定义专属美术资产
> 与接入契约。资产落地前界面维持现状（全部回退安全）。

## 界面结构（现状，代码为准）

- 4 个章节行（CHAPTER A-D），每行 5 个关卡节点，共 20 节点 + 19 段连线
- 每行左侧章节标签，右侧情报面板（选中关详情 + DEPLOY 按钮）
- 画布基准：16:9，mapArea 约 1520×760

## 资产清单

### 1. 全屏背景（1 张）

```
Art/UI/Campaign/world_map_bg.png    1920×1080，不透明
```

灰烬铁道调度图风格：深炭色做旧图纸/蓝图混合质感，四条横向地形带自上而下
渐变（灰线编组冷灰 → 裂谷赭石 → 窑炉暗红 → 终点站近黑），画面上有蜿蜒的
铁轨网络线与股道岔线（不必与节点位置精确对齐，代码层不依赖），散布暗琥珀
余烬微粒、四角轻微暗角。中央偏左 60% 区域保持低对比（节点与文字的可读区）。

### 2. 关卡节点徽章（5 张）

```
Art/UI/Campaign/node_available.png   512×512 透明   琥珀铁路信号灯徽章
Art/UI/Campaign/node_cleared.png     512×512 透明   绿色盖章/封蜡徽章
Art/UI/Campaign/node_locked.png      512×512 透明   深色铁板+挂锁/铁链
Art/UI/Campaign/node_boss.png        512×512 透明   红色警示徽章（危险条纹+兽首/骸骨）
Art/UI/Campaign/node_selected.png    512×512 透明   金色高亮圆环（叠加层，中心镂空）
```

与 P11/P13 宝石徽章图标语言同源（圆形徽章+金属包边+身份色发光），但更厚实
（地图节点要扛得住缩小到 ~64px）。selected 是叠加环，中心透明，不遮节点内容。

### 3. 章节铭牌横幅（1 张）

```
Art/UI/Campaign/zone_banner.png      1536×300 透明
```

横向铁质铭牌条带（可九宫格拉伸）：深色锻铁底、琥珀描边、铆钉细节、左右端
轻微做旧收边。中性色绘制，代码沿用现有 ChapterColors 着色 tint。

### 4. 路轨连接条（1 张，可选）

```
Art/UI/Campaign/path_rail_strip.png  1024×128 透明
```

铁轨枕木条带（九宫格横向拉伸），深色轨枕+双轨+轻微磨损。代码将连线从纯色
线换为此贴图；不做则维持现状色线（可接受降级）。

### 5. 标题铭牌（1 张，可选）

```
Art/UI/Campaign/campaign_title_plate.png  1536×180 透明
```

"CAMPAIGN MAP" 的承载铭牌（铁牌+琥珀描边，文字仍由代码叠加，铭牌不 baked
文字），替代当前纯文字标题。

## 接入契约（代码侧，~40 行，建议代码会话实施）

全部走 `LoadSpriteOrFallback`/可选加载模式，缺图回退现状：

1. 背景：`world_map_bg` 存在则替换 `emberline_startup_background`，且
   `bgArt.color` 从 0.35 提到 ~0.92
2. 章节带：`zone_banner` sprite + 保留 ChapterColors tint（Image.color 叠加）
3. 节点：按状态换 sprite（available/cleared/locked/boss），选中节点叠加
   `node_selected` 呼吸动画（可选）；缺图回退当前实色圆
4. 连线：`path_rail_strip` 九宫格 + 按 cleared/locked 着色
5. 后处理器覆盖：`Assets/Editor/TDArtBatch103Import.cs` 的 IsManagedAsset
   加 `UI/Campaign/` 目录分支（PPU 512、其余同库标准；背景图 PPU 无关紧要）

## 生图提示词基底（英文，全局拼接）

```text
2D game UI asset, hand-painted, dark industrial post-apocalyptic ember-belt
style, weathered charcoal and rust-brown metal with glowing amber-orange
accents, subtle teal energy details, painterly texture with soft volumetric
glow, consistent with tower-defense gem-badge icon language, fully
transparent background (except backgrounds), PNG alpha, no text, no
watermark
```

背景追加：`top-down railway dispatch map of an ash-belt network, dark
charcoal blueprint-parchment hybrid, four horizontal terrain bands fading
from cold grey through ochre canyon to dark kiln red to near-black terminus,
winding rail lines and switch tracks, scattered dim amber ember motes,
corner vignette, low-contrast center-left playfield area, 1920x1080
landscape, fully opaque`

## 验证

1. 目检：徽章三态 + boss + 选中环并排可辨；铭牌拉伸无破绽
2. 实机：L1 清档状态（1 available + 19 locked）、通关 2 关（cleared+available）、
   选中态（金环叠加不遮节点）
3. 960×540 小分辨率下节点徽章仍可辨（≥64px 显示）
