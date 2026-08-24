"""Assemble the complete MiniMax Code image-generation handoff document.

Mirrors tools/_gen_prompt_txt.py (the audio handoff): one big prompt file
the user pastes into MiniMax Code. Prompt payloads are imported from the
generation drivers so the document never drifts from the scripted route.
Output: design/spec/minimax_imagegen_prompt_full.txt
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_worldmap import WAVE1, WAVE2, SPECS, BG_PROMPT  # noqa: E402
from generate_tower_t2 import MODULES, PROMPT_TMPL  # noqa: E402

OUT = Path("design/spec/minimax_imagegen_prompt_full.txt")

DEATH_SPEC = [
    ("skitter_runner", "疾行爬虫", "步足摊开、体壳塌折"),
    ("ash_swarm", "灰烬虫群", "个体离散崩解、灰化飘散"),
    ("carapace_brute", "甲壳蛮兽", "甲壳开裂崩落、躯干侧倾"),
    ("plated_spore", "覆甲孢子", "甲片剥离、孢子液泄出"),
    ("burrow_sapper", "掘地工兵", "钻头卡死、土石回填"),
    ("ember_leech", "余烬水蛭", "躯体瘪塌、余烬熄灭"),
    ("spore_carrier", "孢子载体", "囊腔破裂、孢子雾散开"),
    ("rail_warden", "铁轨守卫", "护盾碎裂、装甲解体"),
    ("cinder_glider", "烬火滑翔者", "翼膜撕裂、螺旋坠地"),
    ("husk_titan", "空壳泰坦", "大体量分段坍塌"),
    ("echo_mimic", "回声拟态体", "拟态形态剥离、回声残影消散"),
    ("furnace_matriarch", "熔炉母体", "相位爆裂、炉核熄灭"),
]

DEATH_STAGES = [
    ("00", "the death moment: {hint} just beginning, pose still continuous with the living idle frame, first cracks and sparks appearing"),
    ("01", "destruction mid-progress: {hint} actively happening, body breaking apart, embers scattering"),
    ("02", "near-wreck: {hint} mostly complete, slumped geometry, dimming glow, small residual fires"),
    ("03", "final wreckage freeze: collapsed remains of {hint}, dark inert metal and shell fragments, faint dying embers only"),
]

A = None


def main():
    lines = []
    A = lines.append
    A("我要为一个 2D 塔防游戏《Emberline Defense》(余烬铁道)批量生成游戏美术图片。请你先通读下面的世界观、视觉调色板和技术约束,然后按 P0 → P1 → P2 的顺序逐个生成图片文件。每生成一张,严格用对应的文件名命名。")
    A("")
    A("# 一、世界观(所有图片必须服从)")
    A("")
    A("人类文明的余烬铁道在衰败。玩家是最后一位线务司令,在锈蚀的铁轨、冷却的熔炉、崩塌的终点站之间,用工业化战争机器抵御从灰烬中涌出的变异生物。视觉基调是\"温暖的衰败\"——夕阳下老机器还在轰鸣的忧郁工业浪漫。")
    A("")
    A("视觉参考坐标:现有资产库(塔=机械构造+能量核心+宝石徽章图标语言;地图=5 张手绘地表:冷灰编组站/灰烬荒漠/赭石裂谷/暗红窑炉盆地/近黑终点站)。")
    A("")
    A("# 二、视觉调色板(每张图都要落在这 6 类里)")
    A("")
    A("1. 金属呼吸:锻铁、铆钉、做旧钢板、锈蚀流痕 → 塔身、机械、UI 框架")
    A("2. 余烬纹理:琥珀橙色发光、火舌、煤渣、暖色体积光 → 能量核心、发光标识")
    A("3. 机械结构:齿轮、连杆、液压撑脚、铁轨枕木 → 结构细节与升级模块")
    A("4. 生物质感:甲壳、湿滑有机形态、低饱和暗色皮膜(有机但不恶心) → 敌人")
    A("5. 空间氛围:暗角、远处暖光源、薄雾、手绘笔触 → 背景与氛围层")
    A("6. 点缀色:青色/蓝紫/绿色按身份色少量出现(各塔/各区身份识别)")
    A("")
    A("# 三、全局技术约束(每张图必须满足)")
    A("")
    A("- 尺寸:1024×1024(方形资产)或 1536×1024(横幅资产);世界图任意 16:9 横图")
    A("- 格式:PNG;除 world_map_bg(不透明)外全部透明背景")
    A("- 单主体居中,占画布 60-80%,边缘干净无残留底、无灰雾薄膜、无棋盘格")
    A("- 手绘数字绘画质感(hand-painted),与上述参考坐标同语言")
    A("- 严禁:文字、水印、UI 框、额外背景场景、拍照感、3D 渲染感")
    A("")
    A("# 四、参考图规则(img2img 类资产)")
    A("")
    A("- P0-A 的 4 张 T2 帧:每张必须上传对应底图(见各条目),保持塔身姿态像素级一致,只叠加升级模块")
    A("- P0-B 世界图:必须上传五地貌蛇形参考拼图,保持各区配色与材质语言")
    A("- P2 死亡帧:每敌上传该敌 idle 首帧作底图,第 0 帧姿态与存活态连续")
    A("")
    A("# 五、收尾批 A：塔 T2 补帧(4 张,上传对应 idle 底图)")
    A("")
    t2_missing = [("ember_flak", 2), ("grav_snare", 4), ("grav_snare", 5), ("rail_lancer", 3)]
    for kind, i in t2_missing:
        m, c = MODULES[kind]
        A(f"## {kind} 第 {i} 帧")
        A(f"底图上传:Assets/Resources/Art/anim/tower_{kind}_{i:02d}.png")
        A(f"保存文件名:tower_{kind}_t2_{i:02d}.png")
        A("```text")
        A(PROMPT_TMPL.format(modules=m, color=c))
        A("```")
        A("")
    A("# 六、世界图两波(34 张):已完成入库,本节仅存档——勿重做")
    A("")
    A("# 八、P2:敌人死亡帧(12 敌 × 4 帧 = 48 张,每敌上传 idle 首帧作底图)")
    A("")
    A("帧推进:00=死亡瞬间(姿态衔接存活态) → 01=破坏进行中 → 02=接近残骸 → 03=残骸定格。禁止大面积高亮闪烁。")
    A("")
    for kind, zh, hint in DEATH_SPEC:
        for idx, (frame, stage) in enumerate(DEATH_STAGES):
            A(f"## {zh}({kind})第 {frame} 帧")
            A(f"底图上传:Assets/Resources/Art/anim/enemy_{kind}_00.png")
            A(f"保存文件名:enemy_{kind}_death_{frame}.png")
            A("```text")
            A(f"using the provided enemy sprite as the base, keep the same viewpoint and ground contact, "
              f"render its death sequence: {stage.format(hint=hint)}, dark industrial ember-belt style, "
              f"hand-painted 2D game sprite, transparent background, no text, no watermark")
            A("```")
        A("")
    A("# 九、产出落地与导入(生成完成后)")
    A("")
    A("将原图按下列目录放置,然后在仓库根目录运行导入命令(自动抠底/缩放/落位,无需手工):")
    A("")
    A("- T2 补 4 帧 → output/imagegen/_t2_raw/(对应文件名)")
    A("  python tools/generate_tower_t2.py")
    A("- 世界图两波 → output/imagegen/_worldmap_raw/(对应文件名)")
    A("  python tools/generate_worldmap.py --wave 1 --import-only")
    A("  python tools/generate_worldmap.py --wave 2 --import-only")
    A("- 死亡帧 → Assets/Resources/Art/anim/ 直接放最终文件名(1024×1024)")
    A("")
    OUT.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"written {OUT} ({len(lines)} lines)")


if __name__ == "__main__":
    main()
