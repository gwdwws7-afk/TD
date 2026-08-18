# QA 会话结果 — 计划 v4(2026-08-18)

## 任务 1:ProjectSettings.asset 清理 ✅

构建器副作用(scriptingDefineSymbols 空序列化残留 + scriptingBackend 翻转)已还原至提交基线(`{}` 定义 + 原后端),编辑器已重载。基线本已在库,无需新提交;后续构建再弄脏时按 `git checkout -- ProjectSettings/ProjectSettings.asset` 即清。

## 任务 2:蓄力解耦回归 ✅ 通过 + 1 项新发现(见 TD-WINDUP-001)

- **EditMode 全量:33/33**(30 基线 + 3 个新 windup 用例)。
- **数值对照**:8 塔新状态表 windupDuration(0.28/0.38/0.22/0.20/0.40/0.14/0.25/0.36)与旧 profile chargeDuration 逐塔相等。
- **一局 playtest**:全检查绿、0 控制台问题(3911 帧正常)。
- **开火手感**:连拍抽查炮口火焰/后坐/弹道正常,无卡帧(`windup_B_fire_*.png`)。

## 任务 3:手柄三 bug 复验 ✅ 全过(4d7087e)

`tools/qa_gamepad_acceptance.py` Phase A **19/19 全绿**:

- **TD-GP-001 ✅**:光标移到 Start Wave + South → 合成指针点击 → "Wave 1 starting now"(主动开波通路实测)
- **TD-GP-002 ✅**:真实鼠标压 HUD 时虚拟光标悬停注册、tooltip 正常显示
- **TD-GP-003 ✅**:tooltip 显示→移开隐藏,全链路复活
- 出售 60% 返还 / 塔位复用 / 轮盘交互 / 混用交还等全部复测通过

**纯手柄 L01 通关状态**:昨日已以倒计时开波取得 20/20 胜利;今日以主动开波复跑 5 局(两种开波路径×三种升级策略)均止步 7-15 波。**根因见下——不是手柄问题,是战斗平衡漂移**。建议 windup 修复落地后复跑一次主动开波通关终验。

## 🔴 新发现:TD-WINDUP-001(P1)蓄力解耦存在实测战斗行为变化

- **现象**:完全相同的驱动/布局/种子下,新代码(846a695)前三波即漏怪(3 局完整度 22→19@波3),回退到父提交后前三波零漏(完整度 22 保持到波 4-5,2 局,与昨日夺冠局一致)。漏怪起点提前约 2 波。
- **排除项**:数值表逐塔相等;设计单测 3 用例通过;开波路径无关(按钮/倒计时同败);标准 playtest 3 塔脚本局波内击杀正常。
- **疑点**:旧代码从 profile 对象**活读** chargeDuration(若 profile 被运行时按层级修改/或有其它消费方),新代码读静态状态表——回退实验证明行为面确实不同,机制待代码会话根因(重点查 `source.windupDuration` 拷贝语义、profile 的运行时变更、以及 846a695 是否顺带移动了 `shotsPerSecond` 的消费)。
- **证据**:`output/playtest/gamepad_B_countdown.log`(新代码)vs `gamepad_B_oldwindup.log`(临时回退,已还原、git 干净)。
- **关联**:任务 4 的 L13/L20 重标定应在本修复之后进行,否则基线又会漂移。

## 遗留待办(不变)

- TD-WINDUP-001 → 代码会话根因;修复后复验:主动开波通关终验 + 平衡矩阵(任务 4)。
- hollow_kiln 贴花已由美术修复(98658eb)——复看并入下一轮。
