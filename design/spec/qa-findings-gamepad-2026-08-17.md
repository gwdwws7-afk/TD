# QA 验收发现 —— 手柄 + 拆塔 + UX(a57824f)

日期:2026-08-17 · 验收人:QA 会话 · 驱动:`tools/qa_gamepad_acceptance.py`(InputSystem 原生事件模拟,InputSystem/GamepadState/MouseState,不经调试 API 触碰被测路径)
证据:`output/playtest/gamepad_acceptance.json` + `output/playtest/gp_*.png` + `output/playtest/gamepad_phaseB*.log`

## 结论速览

- 核心手柄交互链路(光标→轮盘→建造→升级→出售→复建→取消):**全部通过**
- 拆塔 60% 返还 + 塔位复用:**通过**(返还数值逐分核对)
- UX 四项:费用刷新 ✓ / 备战结束自动关轮盘 ✓ / 失效点选提示 ✓ / **tooltip 挂(见 TD-GP-003)**
- 混用切换(鼠标交还指针):**通过**
- **三个 bug 需分派**(下述);纯手柄主动开波不可达为其中之一,另有备战倒计时兜底,纯手柄全程可玩

## 通过项明细(节选)

| 步骤 | 结果 |
|---|---|
| A1 摇杆唤醒虚拟光标 + 首次提示 | PASS |
| A2 光标移动至塔位(±36px 停稳) | PASS |
| A3 A 键在空塔位弹轮盘 | PASS |
| A4 首按 A 高亮槽位、再按确认建造(RailLancer −40) | PASS |
| A5 A 键点塔选中 | PASS |
| A6 十字键← 升级 Damage(t0→t1,预算扣减正确) | PASS |
| A7 十字键↓ 出售:返还 floor(投入×0.6)=43(投入 72),塔位释放、占位标记复位 | PASS |
| A8 出售后同塔位可复建 | PASS |
| A15 B 键取消轮盘 | PASS |
| U1 备战期空塔位开轮盘 + 预算变化实时刷新可负担性 | PASS |
| U3 不可负担槽位确认 → 状态提示 "Tower is locked or unaffordable." + 拒绝音效 | PASS |
| U2 备战窗口关闭(倒计时到期)轮盘自动收起 + "Build window closed." | PASS |
| A9c 备战倒计时自动开波(纯手柄可玩兜底) | PASS |
| A14 鼠标活动即刻交还指针控制;A14b 交还后鼠标点击正常 | PASS |

## Bug 单

### TD-GP-001(P1)纯手柄无法主动开波 —— 焦点导航不可达
- 现象:光标模式一旦唤醒,任何游戏内输入都无法退出;`EnsureGamepadFocus` 因 `_gamepadCursorMode==true` 被跳过,EventSystem 始终无焦点(focus=null);A 键只做棋盘虚拟点击,不合成 UI 点击 → Start Wave 按钮纯手柄按不到。
- 已验证的排除项:十字键连按无效;Start 暂停→恢复(光标模式确实退出)→十字键仍无焦点(唤醒帧同帧清焦,`TDGameManager.cs:1256` 先设焦点、`:1260` 唤醒清焦)。
- 影响:纯手柄通关只能靠备战倒计时自动开波(可玩但每波干等,APv 观感差);设计验收口径"开第一波"未达成主动操作。
- 建议修法(供分派):South 悬于 UI 上时合成 pointer click(ExecuteEvents),或光标模式下给 South 增加"UI 提交"分支,或提供退出光标模式的手柄途径(如长按 B)。

### TD-GP-002(P2)悬停/tooltip/建造虚影链路未适配虚拟指针
- 现象:`UpdateBuildPreviewUnderCursor`(TDGameManager.cs:19560)的 UI 挡板判定用 `IsPointerOverBattleUi()` → `EventSystem.IsPointerOverGameObject()`(真实鼠标)。真实鼠标停在任意 UI(常发生:玩家拿起手柄前鼠标留在 HUD 上)时,手柄光标的塔悬停、tooltip、建造虚影**全部失效**;点击路径已在 a57824f 适配(`_gamepadVirtualPointerOverUi`),悬停路径漏了。
- 复现:U4b 探针(真实鼠标压 HUD + 光标悬塔 → hover=null)。
- 建议修法:悬停链路复用与点击相同的 `pointerOverUi` 判定。

### TD-GP-003(P1)tooltip 自激活死锁 —— 任何输入都不再显示
- 现象:`TDTowerTooltip.Initialize()` 末尾 `SetActive(false)`;唯一的 `SetActive(true)` 在自身 `Update()` 内(TDTowerTooltip.cs:115);禁用对象的 Update 永不运行 → **创建后及每次隐藏后再也无法显示**。鼠标悬停同样失效(非手柄专属)。
- 实证:光标悬塔 1.8s+(`_hoveredTower=Tower_4_6` 已注册)tooltip 始终 inactive;代码路径构造性成立。疑为 a57824f tooltip 改造("show-delay 不再每帧重置")引入的回归。
- 建议修法:激活/显隐交由外部驱动(如 HoverTower 内 SetActive 或 CanvasGroup alpha),或 Update 移至常驻对象。

## 纯手柄 L01 全程(B 阶段)—— 已通关 ✅

- **第 10 轮取得纯手柄胜利**:20 波全清、10 座 RailLancer 全 t2、剩 11 完整度;全程仅手柄输入(建造/升级经轮盘,开波经备战倒计时)。
- 采纳平衡套件 run_01 的获胜配置(11 点位全 t2、第 2 排优先)后通关;策略迭代记录见 `gamepad_phaseB*.log`(B5:6塔t2→17波;B7:10塔t1→11波;B9:7塔t3→17波;B10:获胜)。
- 注意:开波依赖倒计时兜底(TD-GP-001);修复 001 后应复验"主动开波 + 通关"。
- 完整报告:`output/playtest/gamepad_acceptance_final.json`(24 步;除三个 bug 取证步外全 PASS)。

## 环境备注

- 屏前模拟:GamepadState.buttons 需 `1u << (int)btn` 位掩码(非枚举值直赋);MouseState.buttons 为 ushort;合成鼠标 position 不生效(仅增量/迁移)。
- (10,6) 在授权建造列表中但静态 RoadOverlap(净空 0.159 < 阈值)——授权列表含无效点,建议美术/关卡侧复核(AUTHORED ≠ VALID)。
