# 美术需求 — 战前编队界面（Prebattle Formation）v1

> 触发：用户实机截图反馈"还是程序化的"。参照世界图规格 v2 的成功模式
> （现状代码事实 → 资产清单+命名契约 → 回退安全接入 → 提示词 → 验证）。
> 界面归属：`TDGameManager.BuildFormationUi()`（任务板内嵌面板，1120×660）。

## 一、现状诊断（代码+截图核实）

**已有的**（不重做）：
- 面板底框：`AddUiPanelChrome → TDUiWorldSkin.ApplyPanel` 已上 P12
  `frame_command` 九宫格指挥框 + P132 表面装饰 + Instrument 青色 accent
- 塔图标：P11 宝石徽章图标（`TDUiVisualIdentity.iconResourcePath`）+ 身份色
  底光条，已接入

**程序化的（本规格目标）**：
1. ROSTER 8 塔按钮：无贴图平底矩形（133×76），选中只有 Outline 描边
2. RESONANCE DOCTRINE 3 信条按钮：平底 + 裸文字
3. CAMPAIGN DIFFICULTY 3 难度按钮：平底 + 裸文字，选中无视觉差异
4. 顶部威胁条（THREAT MIX）：裸文字无容器
5. 右栏 FORMATION FIT / FORMATION MATRIX：裸文字墙直接贴在面板上
6. 分区标题（ROSTER/DOCTRINE/DIFFICULTY）：纯文字无饰条

## 二、设计定位

**铁路调度仪表台**（railway dispatch console）：内容件做成"仪表卡片/拨杆
铭牌"，与已有指挥框（锻铁+青色仪表光）同语言。所有卡片中性色绘制，
选中/状态由代码叠加身份色 tint——一套贴图服务多状态。

## 三、资产清单（11 张，`Assets/Resources/Art/UI/Formation/`）

```
roster_card_base.png      512×288 透明   编队塔卡片底（九宫格：宝石图标位
                                          左、名次文字位右、底部状态条槽）
roster_card_selected.png  512×288 透明   选中态卡片底（边框琥珀微亮、
                                          状态条槽点亮成琥珀）
roster_card_locked.png    512×288 透明   锁定态（整体压暗、右下小锁槽）
doctrine_plate_base.png   560×170 透明   信条铭牌（左侧圆形纹章槽+右侧
                                          两行文字区，拨杆开关感）
doctrine_plate_on.png     560×170 透明   激活态铭牌（纹章槽点亮、边缘琥珀）
difficulty_plate_base.png 560×140 透明   难度铭牌（左档位指示灯槽+文字区）
difficulty_plate_on.png   560×140 透明   选中态（指示灯点亮、青色仪表光）
threat_strip.png          1536×192 透明  威胁条容器横幅（左侧警铃纹章槽+
                                          长文字槽，两端做旧收边，九宫格）
intel_card.png            768×1024 透明  右栏情报卡背（九宫格：标题槽+
                                          大正文槽，表面比面板底框浅一档）
header_ornament.png       512×96  透明   分区标题饰条（短铁条+两端铆钉，
                                          文字代码叠加居中）
```

复用不动：frame_command 底框、P11 塔图标、身份色 accent、Barlow 字体。

## 四、接入契约（代码侧，~60-80 行，缺图回退现状）

1. `TDUiWorldSkin` 增加通用卡背加载（`LoadFormationSprite(path)` +
   `ApplyCardBackground(rect, sprite, tint)`），沿用 SpriteCache 模式
2. `BuildFormationUi`：
   - ROSTER 按钮底图 = roster_card 三态（选中/锁定换 sprite，未选=base）
   - 信条/难度按钮底图 = 对应 plate 两态（选中换 _on，或 base+青 tint）
   - 威胁条文字套 threat_strip 容器
   - Fit/Matrix 文字套 intel_card 卡背（一张卡，两段文字分区）
   - 分区标题文字下垫 header_ornament
3. 状态切换沿现有刷新链（选中描边保留作为额外强调）
4. 后处理器加 `UI/Formation/` 分支（Campaign 同档 1024，DXT5——threat_strip 1536×192
   与 intel_card 768×1024 在 1120 宽面板内显示超过 512px，512 档会 2× 放大发虚）

## 五、提示词要点（英文基底沿用世界图批）

全局基底 + 逐条：

- 卡/牌通用：`flat UI card background for a 2D game, forged-iron dispatch
  console style matching the existing dark charcoal command frame with teal
  instrument light and amber trim, subtle inset slots for icons and text,
  blank content areas (no text/icons baked), nine-slice friendly borders,
  transparent background`
- roster_card：`...tower roster card, left slot sized for a 50px gem badge,
  right area for two text lines, bottom status strip groove`
- 威胁条：`...wide threat banner strip, left circular alarm-emblem slot,
  long text groove, weathered ends`
- 情报卡：`...tall intel card, title slot on top, large body area, surface
  one step lighter than the command frame`

## 六、验证

1. 三态 roster 卡并排可辨（选中琥珀亮边、锁定压暗）；信条/难度选中态
   即时反馈明显
2. 九宫格拉伸：roster 卡在 133×76 显示、intel 卡在 446×188~518 区无破绽
3. 威胁条与底框、卡片与 P132 装饰不打架（层级：底框→卡片→文字）
4. 960×540 可读；手柄焦点框仍可见（描边保留）
5. 缺任一贴图回退现状平底（零副作用）

## 七、扩展说明

同一套件（卡/牌/饰条语言）可直接平移到任务板章节卡（Chapter Zone 按钮）
与战役档案面板——本规格先交付 Formation，后续按总导演排期复用。
