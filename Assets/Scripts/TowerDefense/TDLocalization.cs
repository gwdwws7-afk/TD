using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TD
{
    public enum TDUiLanguage
    {
        English = 0,
        SimplifiedChinese = 1
    }

    [DisallowMultipleComponent]
    public sealed class TDLocalizedTextSource : MonoBehaviour
    {
        [TextArea] public string sourceText = string.Empty;
    }

    public static class TDLocalization
    {
        public const string LanguagePlayerPrefsKey = "td_p123_language";
        public const string ChineseFontPath = "Fonts/NotoSansCJKsc/NotoSansCJKsc-Regular";

        private readonly struct Replacement
        {
            public readonly string English;
            public readonly string Chinese;

            public Replacement(string english, string chinese)
            {
                English = english;
                Chinese = chinese;
            }
        }

        private static readonly Replacement[] ChineseReplacements =
        {
            new("Single-route onboarding with one anchor bend.", "单路线入门战，围绕关键弯道建立火力锚点。"),
            new("Dual-route rail depot with merge timing checks.", "双路线铁路车站，考验合流时机与火力调度。"),
            new("Tri-lane switchback with alternating split/cross pressure.", "三路线折返峡谷，分流与交叉压力交替出现。"),
            new("Braided basin routes with short reaction windows.", "盆地路线交织，留给防线的反应窗口很短。"),
            new("Final tri-route convergence and boss tempo test.", "最终三线汇流战，集中检验首领阶段节奏。"),
            new("+10 starting defense budget on every deployment.", "每次部署时初始防御资源 +10。"),
            new("+1 starting line integrity on every deployment.", "每次部署时初始防线完整度 +1。"),
            new("+5% resonance gain on every deployment.", "每次部署时共鸣获取速度 +5%。"),
            new("+10 starting budget and +1 integrity on campaign replays.", "战役重玩时初始资源 +10，防线完整度 +1。"),
            new("Match specialization traits before the exam wave", "在考试波次前匹配塔专精与敌人特性"),
            new("SPECIALIZATION FIT", "专精适配"),
            new("Specialization", "专精"),
            new("Ember for armor peaks / Fracture for route pressure", "护甲高峰使用余烬，路线压力使用断裂"),
            new("Ready for first deployment", "可以进行首次部署"),
            new("Locked until", "解锁条件：完成"),
            new("Win with Towers <=", "使用不超过指定数量的塔获胜："),
            new("Integrity", "防线完整度"),
            new("Tactical", "战术评分"),
            new("Four-Tower Line", "四塔防线"),
            new("Switchback Haste", "折返急行"),
            new("Rail / Siege / Frost / Mortar", "轨枪 / 钻机 / 霜冻 / 迫击炮"),
            new("Speed x", "速度 x"),
            new("OPTIONAL", "可选"),
            new("baseline", "基础"),
            new("coverage", "覆盖"),
            new("armor", "护甲"),
            new("economy", "经济"),
            new("branching", "分支"),
            new("counter", "克制"),
            new("resonance", "共鸣"),
            new("finale", "终局"),
            new("REQ", "需要"),
            new("TOWERS", "防御塔"),
            new("MATRIX", "克制矩阵"),
            new("COVERED", "已覆盖"),
            new("GAPS", "缺口"),
            new("BASE RULES", "基础规则"),
            new("REMIX OFF", "章节变体关闭"),
            new("NO MODIFIER", "无额外修正"),
            new("STANDARD CHARTER", "标准章程"),
            new("BASELINE", "基础"),
            new("THREAT  ", "威胁  "),
            new("Auto Fit", "自动适配"),
            new("Save & Deploy", "保存并部署"),
            new("Standard Charter", "标准章程"),
            new("Fracture Mark", "断裂标记"),
            new("Resonance Beacon", "共鸣信标"),
            new("Resonance Relay", "共鸣中继"),
            new("Adaptive", "自适应"),
            new("ADAPTIVE", "自适应"),
            new("Veteran", "老兵"),
            new("VETERAN", "老兵"),
            new("Ember Trial", "余烬试炼"),
            new("EMBER TRIAL", "余烬试炼"),
            new("Standard", "标准"),
            new("STANDARD", "标准"),
            new("\nEMBER", "\n余烬"),
            new("FRACTURE", "断裂"),
            new("Fracture", "断裂"),
            new("Either", "任一"),
            new("None claimed", "尚未领取"),
            new("None", "无"),
            new("Speed", "高速"),
            new("Swarm", "群体"),
            new("Armor", "护甲"),
            new("Budget", "资源"),
            new("Resonance", "共鸣"),
            new("missions", "个任务"),
            new("rewards", "项奖励"),
            new("characters", "字符"),
            new("waves", "波"),
            new("routes", "路线"),
            new("ENEMY", "敌人"),
            new("   TOWER ", "   防御塔 "),
            new("ADD", "加入"),
            new(" DOCTRINE ", " 学说 "),
            new("PROTOCOL", "协议"),
            new("NEW INTEL", "新情报"),
            new("UNTESTED", "未挑战"),
            new("pressure read", "压力判断"),
            new("counter check", "克制检验"),
            new("fast", "高速"),
            new("swarm", "群体"),
            new("armored", "覆甲"),
            new("heavy", "重型"),
            new("flank", "侧翼"),
            new("split", "分流"),
            new("support", "支援"),
            new("attrition", "消耗"),
            new("mixed", "混合"),
            new("boss", "首领"),
            new("INTERACTIVE TUTORIAL COMPLETE", "交互教学完成"),
            new("INTERACTIVE TUTORIAL SKIPPED", "已跳过交互教学"),
            new("COLOR-INDEPENDENT MARKERS", "非颜色状态标记"),
            new("CAMPAIGN DIFFICULTY", "战役难度"),
            new("PLAYER SAVE CONTROL", "玩家存档管理"),
            new("TOWER CONTRIBUTION", "防御塔贡献"),
            new("PREBATTLE FORMATION", "战前塔阵容"),
            new("RESONANCE DOCTRINE", "共鸣学说"),
            new("CAMPAIGN COMMAND", "战役指挥"),
            new("CAMPAIGN PROFILE", "战役档案"),
            new("COMMAND OPTIONS", "指挥设置"),
            new("AUDIO", "音频"),
            new("CHAPTER ARCHIVE", "章节档案"),
            new("EXAM SIGNATURE", "考试阵容签名"),
            new("ACTIVE LEGACY BONUSES", "已生效长期加成"),
            new("TACTICAL PROTOCOLS", "战术协议"),
            new("META REWARDS", "长期奖励"),
            new("CODEX DOSSIERS", "图鉴档案"),
            new("IN PROGRESS", "进行中"),
            new("No reward", "无奖励"),
            new("UNRECORDED", "无记录"),
            new("REWARDS", "奖励"),
            new("TACTICAL PROTOCOL", "战术协议"),
            new("NEXT MASTERY TARGETS", "下一精通目标"),
            new("FULL MASTERY ACHIEVED", "已达成全部精通"),
            new("SAVE & DEPLOY", "保存并部署"),
            new("FORMATION REQUIRED", "需要配置阵容"),
            new("FORMATION READY", "阵容就绪"),
            new("Offline until campaign L16.", "战役 L16 前不可用。"),
            new("Doctrine unlocks with", "学说解锁条件："),
            new("FORMATION & REPLAY", "配置阵容并重试"),
            new("REVIEW FORMATION", "查看阵容"),
            new("SET FORMATION", "配置阵容"),
            new("CLAIM REWARD", "领取奖励"),
            new("REWARD ACTIVE", "奖励已生效"),
            new("REWARD READY", "奖励可领取"),
            new("REWARD LOCKED", "奖励未解锁"),
            new("THREAT PACKAGE", "威胁编组"),
            new("FIELD MISSION", "战地任务"),
            new("BOSS MISSION", "首领任务"),
            new("START WAVE", "开始波次"),
            new("BUILD 1 TOWER", "建造 1 座塔"),
            new("HOLD   GOAL READ THE SWITCH", "等待   目标：观察道岔"),
            new("READ THE SWITCH", "观察道岔"),
            new("DIVERT CENTER", "改道中央路线"),
            new("center switch", "中央道岔"),
            new("CHARGES UNLIMITED", "使用次数不限"),
            new("Hold Gate", "阻滞闸门"),
            new("Call Train", "呼叫列车"),
            new("Cycle Route", "切换路线"),
            new("Purge Kiln", "净化熔炉"),
            new("Break Phase", "击破阶段"),
            new("MARKS ON", "标记开启"),
            new("PREP HOLD", "准备  等待"),
            new("PREP", "准备"),
            new("HOLD", "等待"),
            new("SPD ", "速度 "),
            new("COV ", "覆盖 "),
            new("CTR ", "克制 "),
            new("Routes:", "路线："),
            new("Center", "中央"),
            new("Left", "左路"),
            new("Right", "右路"),
            new("switch", "道岔"),
            new("WEAK", "弱点"),
            new("[SPD]", "[高速]"),
            new("[SWM]", "[群体]"),
            new("HP ", "生命 "),
            new("ARM ", "护甲 "),
            new("FROST", "霜冻"),
            new("FLAK", "高射炮"),
            new("MORTAR", "迫击炮"),
            new("RAIL", "轨枪"),
            new("ARC", "电弧"),
            new("BUDGET", "资源"),
            new("NEXT MISSION", "下一任务"),
            new("CAMPAIGN COMPLETE", "战役完成"),
            new("CAMPAIGN ARCHIVE", "战役档案"),
            new("TACTICAL UPDATE", "战术通报"),
            new("BOSS THREAT ENTERING", "首领威胁来袭"),
            new("DISPATCH THE WAVE", "派出敌军波次"),
            new("DEPLOY A TOWER", "部署防御塔"),
            new("READ THE RANGE", "查看射程"),
            new("READ ARMOR", "识别护甲"),
            new("COMMIT A BRANCH", "选择升级分支"),
            new("USE THE MAP MECHANIC", "使用地图机制"),
            new("Choose a formation tower, then click a glowing build pad. The action is accepted only after a tower is deployed.", "选择阵容中的防御塔，再点击发光塔位。只有防御塔部署完成后，这一步才会通过。"),
            new("Point at or select the tower until its coverage ring remains visible. Check where the road enters and exits the ring.", "指向或选中防御塔，直到射程环保持可见。观察道路从何处进入和离开射程。"),
            new("Use Start Wave when the defense is ready. The wave will not advance this step automatically.", "防线准备完毕后点击开始波次。本步骤不会自动派出敌军。"),
            new("[#] Armor removes flat damage. [#] BREAK means armor is reduced; use Rail or Siege pressure before rapid hits.", "[#] 护甲会固定减免伤害。[#] 破甲表示护甲已降低；先用轨枪或钻机施压，再衔接快速攻击。"),
            new("During the next prep, select the tower and buy a Damage or Utility branch. The preview shows its counter identity.", "下一次备战时选中防御塔，购买伤害或功能分支。预览会显示该分支的克制定位。"),
            new("At a Reinforce or Exam prep, activate the Scenario command. Its cost and remaining charges are shown beside the command.", "在强化关或考试关的备战阶段启用场景指令。指令旁会显示消耗和剩余次数。"),
            new("Interactive tutorial skipped", "已跳过交互教学"),
            new("Interactive tutorial complete", "交互教学已完成"),
            new("DEPOT CLOCK LIVE", "仓库时钟启动"),
            new("BANK THE RESERVE  /  HOLD BOTH RAILS", "储备资源  /  守住两条铁轨"),
            new("TRAIN COMMITTED", "增援列车已发车"),
            new("ARRIVAL WINDOW OPEN  /  DO NOT STRIP A FLANK", "到达窗口开启  /  不要抽空侧翼"),
            new("RESERVE EXAM", "储备考试"),
            new("WAIT FOR DELIVERY  /  OR DISPATCH LIGHT", "等待补给  /  或轻装出兵"),
            new("EARLY DISPATCH  /  EMPTY FLANK", "过早出兵  /  侧翼空虚"),
            new("DEPOT TIMETABLE HELD", "仓库时刻表守住"),
            new("RESERVE WINDOW MISSED", "错失增援窗口"),
            new("JUNCTION ARMED", "枢纽已武装"),
            new("READ THE SPLIT  /  PROTECT THE COMMIT", "观察分路  /  守住已选路线"),
            new("CROSS TRAFFIC", "交叉流量来袭"),
            new("ONE SWITCH  /  THREE PRESSURE LINES", "一次切换  /  三路压力"),
            new("ROUTE EXAM", "路线考试"),
            new("DIVERT BEFORE DISPATCH  /  HOLD THE NEW LANE", "出兵前改道  /  守住新路线"),
            new("LATE SWITCH  /  COVERAGE GAP", "改道过迟  /  覆盖缺口"),
            new("SWITCHBACK SECURED", "折返路线守住"),
            new("ROUTE COMMITMENT BROKE", "路线承诺失守"),
            new("KILN PRESSURE RISING", "熔窑压力上升"),
            new("STACK THE WAVE  /  SAVE THE PURGE", "聚集敌潮  /  保留净化"),
            new("BASIN SATURATED", "盆地已饱和"),
            new("ARMOR CLUSTER FORMING  /  VENT WINDOW NARROW", "护甲集群成形  /  排压窗口缩短"),
            new("PURGE EXAM", "净化考试"),
            new("BREAK THE DENSEST PACK  /  KEEP EXIT CONTROL", "击破最密集敌群  /  控制出口"),
            new("PURGE MISTIMED  /  ARMOR INTACT", "净化时机错误  /  护甲未破"),
            new("KILN PRESSURE VENTED", "熔窑压力已释放"),
            new("BASIN OVERRAN THE VENT", "盆地压力冲破排口"),
            new("TERMINUS ECHO ONLINE", "终点回声上线"),
            new("ALIGN THE MATRIX  /  BANK ONE BREAK", "对齐矩阵  /  保留一次破甲"),
            new("ELITE PHASE BUILDING", "精英阶段蓄势"),
            new("ECHOES MASK THE SURGE  /  WATCH THE CORE", "回声掩盖奔涌  /  盯紧核心"),
            new("PHASE DRILL", "阶段演练"),
            new("EXPOSE THE ELITE  /  CANCEL THE THRESHOLD", "暴露精英  /  打断阈值"),
            new("MATRIX DESYNC  /  BREAKER UNUSED", "矩阵失同步  /  破坏器未使用"),
            new("TERMINUS PHASE STABILIZED", "终点阶段已稳定"),
            new("ECHO SURGE BREACHED", "回声奔涌突破防线"),
            new("FINAL CONVERGENCE", "最终汇聚"),
            new("MATRIARCH INBOUND  /  TWO PHASES TO BREAK", "母体来袭  /  两阶段待击破"),
            new("EMBERLINE COLLAPSING", "EMBERLINE 濒临崩塌"),
            new("HOLD THREE ROUTES  /  CHARGE THE MATRIX", "守住三路  /  为矩阵充能"),
            new("TERMINUS EXAM", "终点考试"),
            new("BREAK 70% AND 35%  /  CONVERGE ON COMMAND", "在 70% 和 35% 破除阶段  /  按指令汇聚"),
            new("PHASE BREAK MISSED  /  CONVERGENCE LATE", "错过阶段破除  /  汇聚过迟"),
            new("THE LAST EMBER HELD", "最后的余烬守住了"),
            new("FINAL CONVERGENCE FAILED", "最终汇聚失败"),
            new("CAMPAIGN PROGRESS", "战役进度"),
            new("CHAPTER PROGRESS", "章节进度"),
            new("CHAPTER REWARD", "章节奖励"),
            new("MISSION RECORD", "任务记录"),
            new("MISSION INTEL", "任务情报"),
            new("COUNTER PLAN", "克制方案"),
            new("THREAT TRAITS", "威胁特性"),
            new("MISSION BOARD", "任务地图"),
            new("PROFILE READY", "档案就绪"),
            new("COPY CLOUD", "复制云存档"),
            new("MERGE CLOUD", "合并云存档"),
            new("COPY SAVE", "复制存档"),
            new("RESET PROFILE", "重置档案"),
            new("CONFIRM RESET", "确认重置"),
            new("CONFIRM IMPORT", "确认导入"),
            new("SAVE SLOTS", "存档槽"),
            new("ACTIVE SLOT", "当前槽位"),
            new("SAVE VERSION", "存档版本"),
            new("CLOUD READY", "云同步就绪"),
            new("PORTABLE", "便携码"),
            new("CHARACTERS", "字符"),
            new("CHALLENGE RECORD", "挑战记录"),
            new("MASTERED CHAPTERS", "精通章节"),
            new("MASTERIES", "精通目标"),
            new("ATTEMPTS", "尝试次数"),
            new("CHALLENGE", "挑战"),
            new("OBJECTIVES", "目标"),
            new("CONTRACT", "契约"),
            new("MUTATOR", "变体"),
            new("DIFFICULTY", "难度"),
            new("FORMATION", "阵容"),
            new("AUTO FIT", "自动适配"),
            new("COUNTER FIT", "克制适配"),
            new("TOWER ROSTER", "塔阵容"),
            new("DOCTRINE EFFECT", "学说效果"),
            new("ACCESSIBILITY", "无障碍"),
            new("SUBTITLES", "字幕"),
            new("SOUND CAPTIONS", "音效字幕"),
            new("LARGE TEXT", "大号文字"),
            new("UI SCALE", "界面缩放"),
            new("MASTER VOLUME", "主音量"),
            new("MUSIC VOLUME", "音乐音量"),
            new("EFFECTS VOLUME", "音效音量"),
            new("KEYBOARD BINDINGS", "键盘按键"),
            new("PRESS A KEY", "请按新按键"),
            new("RESET DEFAULTS", "恢复默认"),
            new("LANGUAGE", "语言"),
            new("SIMPLIFIED CHINESE", "简体中文"),
            new("ENGLISH", "英文"),
            new("OPEN OPTIONS", "打开设置"),
            new("CLOSE OPTIONS", "关闭设置"),
            new("START WAVE ACTION", "开始波次"),
            new("SCENARIO ACTION", "场景指令"),
            new("PAUSE ACTION", "暂停"),
            new("SPEED DOWN ACTION", "降低速度"),
            new("SPEED UP ACTION", "提高速度"),
            new("SETTINGS ACTION", "设置"),
            new("GAMEPAD", "手柄"),
            new("A  CONFIRM", "A  确认"),
            new("B  BACK", "B  返回"),
            new("Y  START WAVE", "Y  开始波次"),
            new("X  SCENARIO", "X  场景指令"),
            new("LB / RB  SPEED", "LB / RB  速度"),
            new("CONTROLLER READY", "手柄已就绪"),
            new("CONTROLLER NOT DETECTED", "未检测到手柄"),
            new("Grayline Junction", "灰线枢纽"),
            new("GRAYLINE JUNCTION", "灰线枢纽"),
            new("Ashfall Depot", "烬落车站"),
            new("ASHFALL DEPOT", "烬落车站"),
            new("Split Switch Canyon", "分轨峡谷"),
            new("SPLIT SWITCH CANYON", "分轨峡谷"),
            new("Hollow Kiln Basin", "空炉盆地"),
            new("HOLLOW KILN BASIN", "空炉盆地"),
            new("Last Ember Terminus", "余烬终点站"),
            new("LAST EMBER TERMINUS", "余烬终点站"),
            new("Signal Gate", "信号闸门"),
            new("Reserve Train", "预备列车"),
            new("Canyon Switch", "峡谷道岔"),
            new("Kiln Purge", "炉膛净化"),
            new("Phase Breaker", "相位破坏器"),
            new("Armor Lance", "破甲长枪"),
            new("Pinning Rail", "钉锁轨枪"),
            new("Cinder Saturation", "烬火饱和"),
            new("Ash Denial", "灰烬封锁"),
            new("Cryo Shatter", "寒霜碎裂"),
            new("Absolute Zero", "绝对零度"),
            new("Chain Overload", "连锁过载"),
            new("Conductive Net", "导电网络"),
            new("Core Bore", "核心钻击"),
            new("Breach Lock", "破口锁定"),
            new("Redline Burst", "红线爆发"),
            new("Intercept Screen", "拦截屏障"),
            new("Signal Burn", "信号灼烧"),
            new("Resonance Relay", "共鸣中继"),
            new("Event Horizon", "事件视界"),
            new("Singularity Well", "奇点力场"),
            new("Pre-breaks armor and punishes heavy targets.", "预先削减护甲并重创重型目标。"),
            new("Pins and exposes priority runners.", "定身并暴露高优先级疾行目标。"),
            new("Amplifies swarm splash and low-health burn.", "强化对群溅射与低生命灼烧。"),
            new("Impact zones stagger and expose groups.", "落点区域踉跄并暴露成群敌人。"),
            new("Shatters slowed, marked, or armored targets.", "粉碎被减速、标记或覆甲的目标。"),
            new("Deep-freeze pulses pin advancing threats.", "深冻脉冲定身推进中的威胁。"),
            new("Adds two stronger chain jumps.", "增加两次强化连锁跳跃。"),
            new("Chain links expose and pin special targets.", "连锁电弧暴露并定身特殊目标。"),
            new("Cracks armor before a massive bore hit.", "重型钻击前先击裂护甲。"),
            new("Locks breached armor and staggers support lines.", "锁定破甲状态并打乱支援队列。"),
            new("Executes fast and flanking targets.", "处决高速与侧翼目标。"),
            new("Wide stagger bursts intercept runner packs.", "大范围踉跄爆发拦截疾行群体。"),
            new("Burns marked, support, and attrition targets.", "灼烧被标记、支援及消耗型目标。"),
            new("Relays marks, exposure, and extra command charge.", "中继标记、暴露与额外指令充能。"),
            new("Damage scales with mass and route progress.", "伤害随目标质量与路线进度提升。"),
            new("Wide gravity pulses pin and expose groups.", "广域重力脉冲定身并暴露群体。"),
            new("Damage leaning", "伤害倾向"),
            new("Utility leaning", "功能倾向"),
            new("Damage specialist", "伤害专精"),
            new("Utility specialist", "功能专精"),
            new("Balanced", "均衡"),
            new("Spec effect: unlock at D2 or U2", "专精效果：伤害或功能分支达到 2 级解锁"),
            new("Spec effect: threat execute", "专精效果：威胁处决"),
            new("Spec effect: control field", "专精效果：控制力场"),
            new("Spec effect: none", "专精效果：无"),
            new("Active matrix:", "当前矩阵："),
            new("Ultimate:", "终极机制："),
            new("Matrix D", "伤害矩阵"),
            new("Matrix U", "功能矩阵"),
            new("[Ember]", "[余烬]"),
            new("> Ember", "> 余烬"),
            new("Damage", "伤害"),
            new("Utility", "功能"),
            new("MAX", "已满"),
            new("role tune", "定位强化"),
            new("targets +", "目标数 +"),
            new("slowT +", "减速时长 +"),
            new("heavy +", "重型倍率 +"),
            new("rate +", "攻速 +"),
            new("dmg +", "伤害 +"),
            new("rng +", "射程 +"),
            new("aoe +", "范围 +"),
            new("slow +", "减速 +"),
            new("PIERCE", "穿透"),
            new("BLAST", "爆破"),
            new("FLAKE", "霜片"),
            new("CHAIN", "连锁"),
            new("CRACK", "破裂"),
            new("SWARM", "群攻"),
            new("SUPPORT", "支援"),
            new("CONTROL", "控制"),
            new("DMG", "伤害"),
            new("RNG", "射程"),
            new("RATE", "攻速"),
            new("AOE", "范围"),
            new("HEAVY", "重型"),
            new("SPEC", "专精"),
            new("Rail Lancer", "轨枪塔"),
            new("Cinder Mortar", "烬火迫击炮"),
            new("Frost Coil", "霜冻线圈"),
            new("Arc Welder", "电弧焊塔"),
            new("Siege Drill", "攻城钻机"),
            new("Ember Flak", "余烬高射炮"),
            new("Resonance Beacon", "共鸣信标"),
            new("Grav Snare", "重力陷阱"),
            new("Mortar", "迫击炮"),
            new("Frost", "霜冻"),
            new("Siege", "钻机"),
            new("Beacon", "信标"),
            new("Snare", "陷阱"),
            new("Rail", "轨枪"),
            new("Flak", "高射炮"),
            new("Arc", "电弧"),
            new("Priority", "优先"),
            new("Area", "范围"),
            new("Control", "控制"),
            new("Chain", "连锁"),
            new("Intercept", "拦截"),
            new("Heavy", "重型"),
            new("Skitter Runner", "疾行爬虫"),
            new("Ash Swarm", "灰烬虫群"),
            new("Carapace Brute", "甲壳蛮兽"),
            new("Plated Spore", "覆甲孢子"),
            new("Burrow Sapper", "掘地工兵"),
            new("Ember Leech", "余烬水蛭"),
            new("Spore Carrier", "孢子载体"),
            new("Rail Warden", "铁轨守卫"),
            new("Cinder Glider", "烬火滑翔者"),
            new("Husk Titan", "空壳泰坦"),
            new("Echo Mimic", "回声拟态体"),
            new("Furnace Matriarch", "熔炉母体"),
            new("Ember Surge", "余烬奔涌"),
            new("Fracture Mark", "断裂标记"),
            new("Standard Charter", "标准章程"),
            new("Forward Recon", "前沿侦察"),
            new("Salvage Mandate", "回收授权"),
            new("Field Control", "战场管制"),
            new("Modular Reserve", "模块化预备队"),
            new("Chapter A", "第一章"),
            new("Chapter B", "第二章"),
            new("Chapter C", "第三章"),
            new("Chapter D", "第四章"),
            new("HARDENED LINE", "加固防线"),
            new("FORWARD RESERVES", "前沿预备队"),
            new("Forward Reserves", "前沿预备队"),
            new("TUNED RELAY", "调谐中继"),
            new("Tuned Relay", "调谐中继"),
            new("EMBERLINE CHARTER", "余烬战线章程"),
            new("Emberline Charter", "余烬战线章程"),
            new("Hardened Line", "加固防线"),
            new("CHAPTER A", "第一章"),
            new("CHAPTER B", "第二章"),
            new("CHAPTER C", "第三章"),
            new("CHAPTER D", "第四章"),
            new("NEXT WAVE", "下一波"),
            new("INTRODUCE", "引入"),
            new("PRACTICE", "练习"),
            new("REINFORCE", "强化"),
            new("SYNTHESIS", "综合"),
            new("EXAM", "考试"),
            new("FRONTIER", "前线"),
            new("WAVE", "波次"),
            new("LINE", "防线"),
            new("GOLD", "资源"),
            new("BUILD", "建造"),
            new("MISSIONS", "战役"),
            new("TACTICAL", "战术"),
            new("SCENARIO", "场景机制"),
            new("READINESS", "战备"),
            new("COVERAGE", "覆盖"),
            new("COUNTER", "克制"),
            new("OUTPUT", "输出"),
            new("ECON", "经济"),
            new("COMMAND", "指挥"),
            new("DAMAGE", "伤害"),
            new("UTILITY", "功能"),
            new("ARMOR", "护甲"),
            new("BREAK", "破甲"),
            new("SLOW", "减速"),
            new("SPECIAL", "专精"),
            new("RESONANCE", "共鸣"),
            new("LEAK", "漏怪"),
            new("BOSS", "首领"),
            new("CURRENT", "当前"),
            new("LOCKED", "锁定"),
            new("OPEN", "可用"),
            new("CLEARED", "已完成"),
            new("STATUS", "状态"),
            new("SCOPE", "规模"),
            new("TRAITS", "特性"),
            new("ROUTES", "路线"),
            new("ROUTE", "路线"),
            new("WAVES", "波"),
            new("STARS", "星级"),
            new("STAR", "星级"),
            new("CLEAR", "通关"),
            new("RANK", "评级"),
            new("PROFILE", "档案"),
            new("Campaign Profile", "战役档案"),
            new("Copy Save", "复制存档"),
            new("Reset Profile", "重置档案"),
            new("Copy Cloud", "复制云存档"),
            new("Merge Cloud", "合并云存档"),
            new("Import", "导入"),
            new("REVISION", "修订号"),
            new("RECORDS", "记录"),
            new("SLOT", "槽位"),
            new("  ACTIVE", "  当前"),
            new("Review Formation", "查看阵容"),
            new("Set Formation", "配置阵容"),
            new("Formation Required", "需要配置阵容"),
            new("Back", "返回"),
            new("Retry", "重试"),
            new("IMPORT", "导入"),
            new("BACK", "返回"),
            new("CLOSE", "关闭"),
            new("RETRY", "重试"),
            new("CONFIRM", "确认"),
            new("SKIP", "跳过"),
            // Cleanup for compound labels after the broad trait replacements above.
            new("BRANCHING", "分支"),
            new("FINALE", "终局"),
            new("FINAL", "终局"),
            new("经济OMY", "经济"),
            new("Ash 群体", "灰烬虫群"),
            new("克制 check", "克制检验"),
            new("护甲ed", "覆甲"),
            new("Clear", "通关"),
            // Dynamic formation/profile/settings cleanup after broad replacements.
            new("Four-tower loadout active; doctrine unlocks at L16.", "四塔阵容已启用；共鸣学说将在 L16 解锁。"),
            new("Loadout and doctrine will persist for this mission.", "本任务将保留当前阵容与学说。"),
            new("The current run has already committed its first build.", "当前战局已完成首次建造，阵容已锁定。"),
            new("Threat-matched Ember or Fracture power +4%.", "匹配威胁时，余烬或断裂强度 +4%。"),
            new("Ember Surge tower output +10%.", "余烬奔涌塔输出 +10%。"),
            new("Fracture Mark exposure damage +10%.", "断裂标记暴露伤害 +10%。"),
            new("No doctrine effect.", "无学说效果。"),
            new("Live match +4%", "动态匹配 +4%"),
            new("Surge output +10%", "奔涌输出 +10%"),
            new("Marked exposure +10%", "标记暴露 +10%"),
            new("Attrition", "消耗"),
            new(" at L16.", " 于 L16 解锁。"),
            new("ADAPT", "适配"),
            new("REMIX  OFF", "章节变体  关闭"),
            new("ACTIVE", "已启用"),
            new("RECON", "侦察"),
            new("SALVAGE", "回收"),
            new("CONTROL", "管制"),
            new("RESERVE", "预备"),
            new("DOSSIERS", "图鉴档案"),
            new("NEXT", "下一目标"),
            new("开始波次 ACTION", "开始波次操作"),
            new("场景机制 ACTION", "场景操作"),
            new("暂停 ACTION", "暂停操作"),
            new("Return to Battle", "返回战斗"),
            new("Save & Replay", "保存并重玩"),
            new("Mission Complete", "任务完成"),
            new("Line Broken", "防线失守"),
            new("Campaign Complete", "战役完成"),
            new("Campaign Archive", "战役档案"),
            new("Missions", "任务"),
            new("Next Mission", "下一任务"),
            new("Next Locked", "下一任务未解锁"),
            new("FAILED", "失败"),
            new("STEP", "步骤"),
            new("READ", "确认"),
            new("COVER", "覆盖")
        };

        private static Font _chineseFont;
        private static bool _initialized;
        private static System.Collections.Generic.List<Replacement> _activeReplacements;
        private const string LocalizationJsonPath = "Localization/strings";

        public static TDUiLanguage CurrentLanguage { get; private set; } = TDUiLanguage.English;
        public static bool IsChinese => CurrentLanguage == TDUiLanguage.SimplifiedChinese;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            LoadJsonReplacements();

            var defaultLanguage = Application.systemLanguage == SystemLanguage.Chinese ||
                                  Application.systemLanguage == SystemLanguage.ChineseSimplified
                ? TDUiLanguage.SimplifiedChinese
                : TDUiLanguage.English;
            CurrentLanguage = (TDUiLanguage)Mathf.Clamp(
                PlayerPrefs.GetInt(LanguagePlayerPrefsKey, (int)defaultLanguage),
                (int)TDUiLanguage.English,
                (int)TDUiLanguage.SimplifiedChinese);
            _initialized = true;
        }

        public static void SetLanguage(TDUiLanguage language, bool persist = true)
        {
            Initialize();
            CurrentLanguage = language;
            _localizationCache.Clear();
            if (!persist)
            {
                return;
            }

            PlayerPrefs.SetInt(LanguagePlayerPrefsKey, (int)language);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load replacement pairs from Localization/strings.json (data-driven).
        /// Falls back to the hardcoded ChineseReplacements array if the JSON
        /// is missing or fails to parse.
        /// </summary>
        private static void LoadJsonReplacements()
        {
            _activeReplacements = null;
            try
            {
                var jsonAsset = Resources.Load<TextAsset>(LocalizationJsonPath);
                if (jsonAsset != null)
                {
                    var parsed = ParseLocalizationJson(jsonAsset.text);
                    if (parsed != null && parsed.Count > 0)
                    {
                        _activeReplacements = parsed;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TD] Localization JSON load failed, using hardcoded fallback: {e.Message}");
            }

            // Fallback: use the hardcoded array directly.
            _activeReplacements ??= new System.Collections.Generic.List<Replacement>(ChineseReplacements);
        }

        private static System.Collections.Generic.List<Replacement> ParseLocalizationJson(string json)
        {
            // Minimal JSON parser for {"languages":{"zh":{"en":"zh",...}}}
            // Avoids dependency on Newtonsoft or JsonUtility (which needs [Serializable]).
            var result = new System.Collections.Generic.List<Replacement>();
            var langKey = IsChinese ? "zh" : "en";

            // Use JsonUtility with a wrapper since Unity's JsonUtility handles Dictionary-like
            // structures via [Serializable]. But our JSON is nested, so we use a simple
            // regex-based extraction for the zh block.
            var zhMatch = System.Text.RegularExpressions.Regex.Match(
                json, @"""zh""\s*:\s*\{");
            if (!zhMatch.Success)
            {
                return null;
            }

            // Find the zh block: from zhMatch.Index+zhMatch.Length to the matching closing brace.
            var start = zhMatch.Index + zhMatch.Length;
            var depth = 1;
            var end = start;
            while (end < json.Length && depth > 0)
            {
                if (json[end] == '{') depth++;
                else if (json[end] == '}') depth--;
                end++;
            }

            if (depth != 0)
            {
                return null;
            }

            var zhBlock = json.Substring(start, end - start - 1);

            // Extract "key": "value" pairs.
            var pairPattern = new System.Text.RegularExpressions.Regex(
                @"""((?:[^""\\]|\\.)*)""\s*:\s*""((?:[^""\\]|\\.)*)""");
            var matches = pairPattern.Matches(zhBlock);
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var key = UnescapeJson(m.Groups[1].Value);
                var value = UnescapeJson(m.Groups[2].Value);
                if (!string.IsNullOrEmpty(key))
                {
                    result.Add(new Replacement(key, value));
                }
            }

            return result;
        }

        private static string UnescapeJson(string s)
        {
            return s
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t")
                .Replace("\\\\", "\\");
        }

        public static Font ResolveFont(Font latinFallback)
        {
            Initialize();
            if (!IsChinese)
            {
                return latinFallback;
            }

            if (_chineseFont == null)
            {
                _chineseFont = Resources.Load<Font>(ChineseFontPath);
            }

            if (_chineseFont == null)
            {
                _chineseFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial Unicode MS" },
                    16);
            }

            return _chineseFont != null ? _chineseFont : latinFallback;
        }

        // Memoized results for the replacement scan: the HUD calls this with a
        // bounded set of source strings, and each scan walks the full
        // replacement table producing intermediate strings — cache per source.
        private static readonly System.Collections.Generic.Dictionary<string, string> _localizationCache = new();

        public static string LocalizeRuntimeString(string source)
        {
            Initialize();
            if (!IsChinese || string.IsNullOrEmpty(source))
            {
                return source ?? string.Empty;
            }

            if (_localizationCache.TryGetValue(source, out var cached))
            {
                return cached;
            }

            if (string.Equals(source, "ON", StringComparison.Ordinal))
            {
                return "开启";
            }

            if (string.Equals(source, "OFF", StringComparison.Ordinal))
            {
                return "关闭";
            }

            // Keep the product mark intact; the generic LINE metric is localized later.
            if (string.Equals(source, "EMBERLINE", StringComparison.Ordinal))
            {
                return source;
            }

            var localized = source;
            var replacements = _activeReplacements ?? new System.Collections.Generic.List<Replacement>(ChineseReplacements);
            for (var i = 0; i < replacements.Count; i++)
            {
                var replacement = replacements[i];
                localized = localized.Replace(replacement.English, replacement.Chinese);
            }

            // The HUD brand shares a label with localized mission text.
            localized = localized.Replace("EMBER防线", "EMBERLINE");
            _localizationCache[source] = localized;
            return localized;
        }

        public static void SetLabel(Text label, string sourceText, Font latinFallback = null)
        {
            if (label == null)
            {
                return;
            }

            var source = label.GetComponent<TDLocalizedTextSource>() ?? label.gameObject.AddComponent<TDLocalizedTextSource>();
            var nextSource = sourceText ?? string.Empty;
            if (nextSource.Length > 0 && source.sourceText == nextSource && !string.IsNullOrEmpty(label.text))
            {
                // Same source text — skip the replacement scan and font
                // resolve. HUD updates call this every frame with unchanged
                // strings; the localize pass dominates that cost.
                return;
            }

            source.sourceText = nextSource;
            label.text = LocalizeRuntimeString(nextSource);
            label.font = ResolveFont(latinFallback != null ? latinFallback : label.font);
        }

        public static void RefreshLabels(GameObject root, Font latinFallback = null)
        {
            if (root == null)
            {
                return;
            }

            var labels = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label == null)
                {
                    continue;
                }

                var source = label.GetComponent<TDLocalizedTextSource>();
                if (source == null)
                {
                    source = label.gameObject.AddComponent<TDLocalizedTextSource>();
                    source.sourceText = label.text ?? string.Empty;
                }

                label.text = LocalizeRuntimeString(source.sourceText);
                label.font = ResolveFont(latinFallback != null ? latinFallback : label.font);
            }
        }

        public static string GetLanguageName(TDUiLanguage language)
        {
            return language == TDUiLanguage.SimplifiedChinese ? "SIMPLIFIED CHINESE" : "ENGLISH";
        }
    }
}
