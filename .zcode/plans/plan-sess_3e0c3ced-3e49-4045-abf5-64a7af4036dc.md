## 视觉化世界地图 + 准备界面 + 主界面打磨

### 核心改动:把任务面板的按钮网格替换为视觉化世界地图

当前 `RefreshMissionBoardUi` 把关卡显示为 5 个平铺按钮(切换章节标签才看到另外 5 个)。改造为**一张完整的世界地图**,20 个关卡节点全部可见,用路径连接,4 个章节用不同底色分区。

---

### 改动 1:世界地图节点系统(TDWorldMap.cs — 新建)

在任务面板的左侧区域(原 744px 宽的关卡区)绘制一张世界地图:

**布局** — 20 个节点沿 S 型路径排布在 ~700×400 区域内:
```
Chapter A (L01-L05)  ← 左上,从左到右
       ↘
Chapter B (L06-L10)  ← 右上,从右到左
       ↘  
Chapter C (L11-L15)  ← 左下,从左到右
       ↘
Chapter D (L16-L20)  ← 右下,从右到左 → BOSS
```

**节点视觉**:
- 圆形(用现有 `TDArtLibrary.GetSoftRingSprite()`),直径 42px
- 锁定 = 深灰 + 锁图标
- 可用 = 余烬橙 + 脉冲边框
- 已通关 = 青绿色 + 星级标记(★☆☆)
- Boss(L20) = 红色 + 放大

**路径**:用 `Drawing.LineRenderer` 或 UI `Image` 连接线连接相邻节点,通关的路径高亮

**点击**:点击节点 = `SelectMissionBoardLevel(index)`,右侧 Intel 面板更新

**章节底色**:4 个半透明色块作为章节区域背景

### 改动 2:关卡准备界面增强(改造现有 Formation Panel)

在编队面板里增加**塔升级树预览**:
- 每个已选塔显示当前升级等级(Damage ▲▲☆ / Utility ▲☆☆)
- 显示专精大招名称和解锁状态
- 用颜色标记可用/不可用塔(费用不够灰色)

### 改动 3:主界面打磨(TDTitleScreen 增强)

- 加载 `emberline_startup_background` 作为全屏背景(已有代码,验证是否生效)
- 标题 Logo 文字加大 + 加阴影/描边效果
- 菜单按钮加 hover 缩放动画(复用 TDUiAnimator)
- 版本信息 + "Press Any Key" 提示

---

### 文件改动清单

| 文件 | 改动 |
|---|---|
| **新建** `TDWorldMap.cs` | 世界地图节点系统(20 节点 + 路径 + 状态) |
| **修改** `TDGameManager.cs` | `BuildMissionBoardUi` 和 `RefreshMissionBoardUi` 集成世界地图;编队面板加升级树预览 |
| **修改** `TDTitleScreen.cs` | 背景图加载 + 按钮动画 + 视觉打磨 |

### 验证标准
- [ ] 20 个关卡节点全部在同一张地图上可见
- [ ] 锁定/可用/通关/Boss 4 种状态视觉区分
- [ ] 点击节点选关,右侧情报更新
- [ ] 编队面板显示升级树
- [ ] 编译 0 错误