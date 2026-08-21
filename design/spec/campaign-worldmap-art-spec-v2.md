# 美术需求 — 战役世界地图（关卡选择）v2

> v2 结构性重写（用户参考图指示）：目标不是给程序化网格换皮肤，而是
> **手绘世界地图结构**——多种自然地貌连成旅程、每关锚定专属地标、
> 每关显示通过数据（三难度印章）、提供局外升级入口。
> 布局从"4 章节行"改为"绘制的世界 + 关卡锚点"，代码侧为结构性改动。
> 资产落地前界面维持现状（全部回退安全）。

## 数据事实（已核实，全部现成）

- 20 关 → 5 地图，每图 4 关：L1-4 grayline_junction（冷灰编组站）/
  L5-8 ashfall_depot（灰烬荒漠仓库）/ L9-12 split_switch_canyon（赭石裂谷）/
  L13-16 hollow_kiln_basin（暗红窑炉盆地）/ L17-20 last_ember_terminus（近黑终点站）
- 每关进度：`cleared` + `highestDifficultyCleared`（0=standard/1=veteran/
  2=ember_trial）→ 每关最多 3 枚印章
- 局外升级系统**未立项**（Resonance Doctrine 是局内策略）——入口与面板
  美术先备，系统本体待设计/代码会话排期

## 资产清单（约 30 张）

### 1. 世界地图底图（1 张，核心资产）

```
Art/UI/Campaign/world_map_bg.png    2048×1152，不透明
```

手绘俯视世界地图，**五个自然地貌区沿旅程动线铺满画布**（左上→右下蛇形）：
冷灰铁道编组平原 → 灰烬荒漠（散布仓库塔吊）→ 赭石裂谷（深谷与栈桥）→
暗红窑炉盆地（火山口与熔渣）→ 近黑终点站（废弃枢纽与最后一盏信号灯）。
区与区之间用自然过渡（河谷/山脊/铁轨干线），一条主铁路线蜿蜒贯穿全程
作为旅程视觉主线。风格锚点=现有 5 张 `map_surface_*_16x9`（同款手绘语言，
缩略为世界图）。中部保持中低对比（节点/地标/文字可读区），四角压暗，
暖光源点亮当前进度区域。**关卡锚点位置不 baked**（代码放节点与地标），
但地貌分区要给每区留出 4 个锚点的呼吸位（每区约 1/5 画布）。

### 2. 关卡地标（20 张，每关专属标识+地貌）

```
Art/UI/Campaign/landmark_L01.png ... landmark_L20.png    384×384 透明
```

每关一个微型地标立绘，绘制该关在其地貌区里的**专属构筑物**（同区 4 个
地标造型互异、共享地貌材质）：编组站的信号塔/机库/水塔/检修坑、灰烬仓库
的塔吊/料仓/车库、裂谷的栈桥/吊桥/哨塔/隧道口、窑炉的熔炉/管道桥/矿斗/
烟囱群、终点站的终站房/大灯塔/车库/纪念碑。地标即关卡身份——远看剪影
可辨，近看有细节。命名用关卡号（levelId 与 L 编号一一对应）。

### 3. 节点徽章与印章（7 张）

```
node_available / node_cleared / node_locked / node_boss / node_selected
    512×512 透明，同 v1（厚重金属圆牌+铆钉+状态色发光核心，编号代码叠加）
seal_pip.png        128×128 透明   难度印章（中性色，代码按已达成档数点亮/tint）
seal_pip_empty.png  128×128 透明   未达成印章底槽
```

通过数据标识：每关地标下方横排 3 枚印章（standard/veteran/ember_trial），
数据源 `highestDifficultyCleared`，达成 N 枚亮 N 枚。

### 4. 区域名牌与升级入口（5 张）

```
region_plate.png        1024×192 透明   地貌区名牌（九宫格，代码叠区名文字）
meta_entry_button.png   384×384 透明   局外升级入口按钮（锻铁+琥珀徽记）
meta_panel_frame.png    1536×1024 透明  局外升级面板框（九宫格，内容区留白：
                                        顶部货币条/中部 4 线节点/底部关闭，
                                        对齐 meta-upgrade-system-spec-v1 交互流）
meta_node_slot.png      192×192 透明   升级节点槽（中性色，代码三态 tint：
                                        已购琥珀亮/可购呼吸/不足灰）
campaign_title_plate.png 1536×180 透明  标题铭牌
```

局外升级入口放主界面右下（DEPLOY 侧），点击打开面板（面板内容系统待立项，
先出框体与标题位）。

### 5. 轨道连线（1 张，可选）

```
path_rail_strip.png  1024×128 透明
```

发光轨道条（中性亮度，代码 tint 拉亮暗两态），串联地标形成旅程线。

## 接入契约（代码侧，结构性改动 ~120-150 行，代码会话实施）

1. `NodePositions` 从 4 行网格改为**世界图锚点表**（20 个手工锚点贴地貌区），
   或按区域中心+扰动自动布局
2. 节点渲染改为：地标（关卡身份）+ 状态徽章（小尺寸叠在地标基座）+
   印章排（3 pips）；选中态金环叠地标
3. 背景换 `world_map_bg` 并提亮；章节带删除（地貌区即分区），区名用
   `region_plate` 叠加在每区上缘
4. 印章数据绑定 `TDCampaignLevelProgress.highestDifficultyCleared`
5. 局外升级入口按钮常驻右下；面板打开逻辑待系统立项（先挂空面板+标题）
6. 后处理器已覆盖 `UI/Campaign/`（2048 预算，`d46e064`）

## 生图提示词要点

- 世界图基底：`top-down hand-painted fantasy-industrial world map, five
  natural terrain regions connected by a winding main railway: cold-grey
  rail junction plains, ash-desert with depot cranes, ochre slot canyon
  with trestle bridges, dark-red volcanic kiln basin, near-black derelict
  terminus; natural region transitions via river valleys and ridges,
  mid-low contrast center, warm light on the journey corridor, corner
  vignette, 2048x1152, fully opaque`
- 地标基底：`small landmark vignette for a tower-defense level, [REGION
  MATERIAL PALETTE], a [SPECIFIC STRUCTURE], hand-painted, silhouette-
  readable at small size, 384px, transparent background`
- 其余同 v1 的全局风格基底（余烬铁道工业风、宝石徽章语言）

## 验证

1. 五地貌区并排可辨、主铁路旅程线连贯
2. 同区 4 地标造型互异但材质统一；20 地标缩到 ~96px 剪影仍可辨
3. 三印章绑定实测：清 standard 亮 1、veteran 亮 2、ember_trial 亮 3
4. 960×540 下地标+徽章+印章仍可读
5. 局外升级入口/面板框就位（系统空挂不报错）
