"""Assemble the formation-panel MiniMax Code handoff document.

Same pattern as _gen_worldmap_prompt.py: single paste-ready document,
prompts imported from the driver (single source of truth).
Output: design/spec/minimax_formation_prompt_full.txt
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_formation import PROMPTS, SPECS, ORDER, STYLE, STYLE_ANCHORED  # noqa: E402

OUT = Path("design/spec/minimax_formation_prompt_full.txt")

ZH = {
    "roster_card_base":      "编队塔卡片-默认态(左侧宝石图标槽+右侧两行文字区+底部状态条槽,中性未点亮)",
    "roster_card_selected":  "编队塔卡片-选中态(边框琥珀亮+状态条琥珀发光)",
    "roster_card_locked":    "编队塔卡片-锁定态(整体压暗无电感+右下小锁槽)",
    "doctrine_plate_base":   "共鸣信条铭牌-默认态(左圆纹章槽+右两行文字区,拨杆感)",
    "doctrine_plate_on":     "共鸣信条铭牌-激活态(纹章槽琥珀亮+边缘辉光)",
    "difficulty_plate_base": "难度铭牌-默认态(左指示灯槽+右文字区)",
    "difficulty_plate_on":   "难度铭牌-激活态(指示灯青色仪表光)",
    "threat_strip":          "威胁条横幅(左警铃纹章槽+长文字槽,两端做旧收边)",
    "intel_card":            "右栏情报卡背(顶部标题槽+大正文区,表面比底框浅一档)",
    "header_ornament":       "分区标题饰条(短铁条+两端铆钉,中心留白给文字)",
}


def main():
    lines = []
    A = lines.append
    A("我要为一个 2D 塔防游戏《Emberline Defense》(余烬铁道)的\"战前编队界面\"生成一套调度仪表台控件皮肤(10 张卡/牌/横幅)。请你先通读世界观和技术约束,然后按清单顺序逐个生成,每张严格按对应文件名命名。注意:清单里有 4 张是\"状态变体\"——必须以上一张基础卡的产出作为参考图(img2img),保持构图、比例、槽位完全一致,只改变点亮状态。")
    A("")
    A("# 一、世界观(所有图必须服从)")
    A("")
    A("人类文明的余烬铁道在衰败。这个界面是\"铁路调度仪表台\":锻铁深炭色底、青色仪表光点缀、琥珀描边。所有卡片是仪表台的控件——嵌槽留白给代码叠加的图标与文字,卡上绝不 baked 任何文字或图标。与现有指挥框(frame_command,锻铁+青仪表光)同一语言。")
    A("")
    A("# 二、全局技术约束")
    A("")
    A("- 透明背景 PNG,横幅类按 3:2 横图出,情报卡按 2:3 竖图出")
    A("- 卡片为\"底板\":内容区(图标槽/文字槽)必须留白凹陷槽,不画内容")
    A("- 九宫格友好(四边可拉伸),边缘干净无残留底/灰雾(硬性红线)")
    A("- 手绘数字绘画质感,严禁文字、图标、水印、3D 渲染感")
    A("")
    A("# 三、交付清单(10 张,按此顺序)")
    A("")
    for name in ORDER:
        _, target, _, base = SPECS[name]
        A(f"## {name}(目标 {target[0]}×{target[1]})")
        A(f"说明:{ZH[name]}")
        if base:
            A(f"参考图上传:上一步生成的 {base}.png 的原图(保持构图槽位完全一致)")
        elif name in STYLE_ANCHORED:
            A("参考图上传:材质锚 output/imagegen/_formation_raw/formation_style_reference.png(threat_strip 与 doctrine_plate_on 两张过审原图的拼图)——新卡片必须完全匹配参考图的锻铁材质语言:拉丝金属纹、深炭色锈蚀、边角铆钉、青色仪表勾边")
        A(f"保存文件名:{name}.png")
        A("```text")
        A(PROMPTS[name])
        A("```")
        A("")
    A("# 四、产出落地与导入")
    A("")
    A("原图统一放 output/imagegen/_formation_raw/(按上面各文件名),然后在仓库根目录:")
    A("```bash")
    A("python tools/generate_formation.py --import-only   # 全部导入")
    A("python tools/generate_formation.py --only roster_card_selected --import-only  # 单张重导")
    A("```")
    A("导入命令自动完成抠透明背景、内容裁切、按目标尺寸落位 Assets/Resources/Art/UI/Formation/。")
    A("")
    A("状态变体若手动生成:先做 base,把 base 原图上传给 AI 再生成变体,确保构图一致。")
    OUT.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"written {OUT} ({len(lines)} lines)")


if __name__ == "__main__":
    main()
