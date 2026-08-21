"""Assemble the world-map-only MiniMax Code handoff document.

Focused counterpart to minimax_imagegen_prompt_full.txt: the campaign
level-select world map batch only (wave 1 gate + wave 2), grouped by
category with region palettes, self-contained for pasting into MiniMax
Code. Output: design/spec/minimax_worldmap_prompt_full.txt
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_worldmap import (  # noqa: E402
    WAVE1, WAVE2, SPECS, BG_PROMPT, REGIONS, REGION_PALETTES,
)

OUT = Path("design/spec/minimax_worldmap_prompt_full.txt")

REGION_ZH = {
    "grayline junction plains": "灰线编组平原(L1-L4,冷灰钢构+青色信号灯)",
    "ashfall depot": "灰烬仓库区(L5-L8,暖灰+漂浮余烬+锈橙塔吊)",
    "split switch canyon": "裂谷道岔区(L9-L12,赭石红岩层+暗色木栈桥)",
    "hollow kiln basin": "窑炉盆地区(L13-L16,火山玄武岩+窑红熔渣)",
    "last ember terminus": "最后余烬终点站(L17-L20,近黑废墟+一盏暖琥珀信号灯)",
}

WAVE2_ZH = {
    "node_available": "关卡节点-可挑战(琥珀信号灯徽章,中心留发光凹槽给编号叠加)",
    "node_cleared":   "关卡节点-已通关(绿色盖章徽章)",
    "node_locked":    "关卡节点-锁定(铁板+挂锁铁链)",
    "node_boss":      "关卡节点-Boss(危险条纹+兽首徽章,份量最重)",
    "node_selected":  "选中高亮环(金色细环,中心镂空不遮节点)",
    "seal_pip":        "难度印章-中性(金属小徽章,未点亮,供代码染色)",
    "seal_pip_empty":  "难度印章-空槽(深色凹槽)",
    "region_plate":    "地貌区名牌横幅(锻铁+琥珀描边+铆钉,九宫格拉伸)",
    "meta_entry_button": "局外升级入口按钮(锻铁+琥珀'残渣水晶嵌齿轮'徽记)",
    "meta_panel_frame":  "局外升级面板框(锻铁边框:顶部货币条/中部四行升级线/底部关闭区,内容留白)",
    "meta_node_slot":    "升级节点槽(六边形金属槽,中性色供三态染色)",
    "campaign_title_plate": "标题铭牌(锻铁+琥珀描边,中心留白给文字叠加)",
    "path_rail_strip": "发光轨道条(双轨+发光槽,中性亮度供亮暗两态染色,左右可平铺)",
}


def main():
    lines = []
    A = lines.append
    A("我要为一个 2D 塔防游戏《Emberline Defense》(余烬铁道)重做关卡选择界面,批量生成\"手绘世界地图\"美术。请你先通读世界观、调色板和技术约束,然后严格按 第一部分(世界图,1 张)→ 第二部分(其余 33 张)的顺序逐个生成。每张严格按对应文件名命名。第一部分是世界观基调,做完这一张先停下等验收,不要直接做第二部分。")
    A("")
    A("# 一、世界观(所有图必须服从)")
    A("")
    A("人类文明的余烬铁道在衰败。玩家是最后一位线务司令。关卡选择界面是一张手绘的战役世界地图:五个自然地貌区沿一条主铁路蜿蜒铺开,20 个关卡各自锚定在自己的地貌区里,每个关卡有专属地标构筑物、状态徽章和三枚难度印章,右下角有局外升级入口。视觉基调\"温暖的衰败\"——夕阳下老机器还在轰鸣的忧郁工业浪漫。")
    A("")
    A("五个地貌区(自上而下旅程顺序)与调色板:")
    for region, zh in REGION_ZH.items():
        A(f"- {zh}:{REGION_PALETTES[region]}")
    A("")
    A("# 二、全局技术约束")
    A("")
    A("- 手绘数字绘画质感(hand-painted),与上述五区调色板同语言")
    A("- 透明背景 PNG(仅 world_map_bg 不透明),单主体居中占画布 60-80%")
    A("- 边缘干净:严禁残留底、灰雾薄膜、棋盘格(之前批次踩过的坑,硬性红线)")
    A("- 严禁文字、水印、UI 外框、多余场景、3D 渲染感")
    A("")
    A("# 三、第一部分:世界地图底图(1 张,验收门禁——上传参考图)")
    A("")
    A("参考图上传:design/spec/assets/world_map_reference.png(五地貌蛇形拼图,给配色与材质定调)")
    A("保存文件名:world_map_bg.png(任意 16:9 横图)")
    A("```text")
    A(BG_PROMPT)
    A("- transform the uploaded reference collage of the five region terrains into one continuous painted world map, keeping each region's palette and material language")
    A("```")
    A("")
    A("# 四、第二部分:关卡地标(20 张,每张带所在区的调色板)")
    A("")
    A("同区 4 个地标造型互异、共享地貌材质;远看剪影可辨。")
    current = None
    for lid in [f"L{i:02d}" for i in range(1, 21)]:
        region = REGIONS[lid].split(":")[0]
        if region != current:
            current = region
            A(f"\n## {REGION_ZH[region]}")
        prompt = WAVE2[f"landmark_{lid}"]
        w, h = SPECS[f"landmark_{lid}"][1]
        A(f"- {lid} 保存文件名:landmark_{lid}.png(目标 {w}×{h})")
        A("  ```text")
        A("  " + prompt)
        A("  ```")
    A("\n# 五、第二部分:节点徽章与印章(7 张)")
    A("")
    for name in ["node_available", "node_cleared", "node_locked", "node_boss",
                 "node_selected", "seal_pip", "seal_pip_empty"]:
        w, h = SPECS[name][1]
        A(f"- {WAVE2_ZH[name]} 保存文件名:{name}.png(目标 {w}×{h})")
        A("  ```text")
        A("  " + WAVE2[name])
        A("  ```")
    A("\n# 六、第二部分:名牌、升级入口与轨道(6 张)")
    A("")
    for name in ["region_plate", "meta_entry_button", "meta_panel_frame",
                 "meta_node_slot", "campaign_title_plate", "path_rail_strip"]:
        w, h = SPECS[name][1]
        A(f"- {WAVE2_ZH[name]} 保存文件名:{name}.png(目标 {w}×{h})")
        A("  ```text")
        A("  " + WAVE2[name])
        A("  ```")
    A("\n# 七、产出落地与导入")
    A("")
    A("原图统一放 output/imagegen/_worldmap_raw/(按上面各文件名),然后在仓库根目录:")
    A("```bash")
    A("python tools/generate_worldmap.py --wave 1 --import-only   # 先导世界图")
    A("python tools/generate_worldmap.py --wave 2 --import-only   # 验收通过后导其余 33 张")
    A("# 单张重导示例")
    A("python tools/generate_worldmap.py --only landmark_L07 node_boss --import-only")
    A("```")
    A("导入命令自动完成:抠透明背景 → 按目标尺寸/形态后处理(世界图 16:9 裁切放大、宽幅铭牌内容裁切、小件降采样) → 落位 Assets/Resources/Art/UI/Campaign/。")
    OUT.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"written {OUT} ({len(lines)} lines)")


if __name__ == "__main__":
    main()
