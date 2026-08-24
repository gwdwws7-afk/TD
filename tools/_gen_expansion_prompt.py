"""Assemble the expansion MiniMax handoff documents (one per batch).

Batches: towers (C-1), enemies (C-2+D), bosses (C-3), portraits.
Prompts imported from generate_expansion.py (single source of truth).
Outputs: design/spec/minimax_expansion_{batch}.txt
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_expansion as G  # noqa: E402

OUTDIR = Path("design/spec")
RAWDIR = "output/imagegen/_expansion_raw"

BATCH_ZH = {
    "towers": ("扩充新塔批（4 塔 × 24 帧 + 弹/效/UI 徽章，共 107 张）",
               "每座塔先生成第 0 帧(全新),随后 01-05 帧以前一帧为参考图(细微待机摇摆);t2 每帧以对应 idle 帧为底(逐帧对应,不许多帧共用 00 底图);t3 以对应 t2 帧为底;fire 帧以 idle/t3 首帧为底只叠加开火特效。链式依赖见各条目。"),
    "enemies": ("扩充新敌批（6 敌 × idle 8 + death 4 + 行为 FX 6，共 108 张）",
                "每敌第 0 帧全新,01-07 帧以前一帧为参考图(待机运动);death 4 帧以 idle 首帧为底按死亡四段推进;行为 FX 为独立特效序列不需要底图。"),
    "bosses": ("扩充 Boss 批（4 Boss × 10 帧 + 每 Boss 警告 FX 10 + 相位 FX 6，共 104 张）",
               "每 Boss 第 0 帧全新,后续帧以前一帧为参考图(庞然待机呼吸);警告 FX/相位 FX 为独立特效序列不需要底图。furnace_matriarch 无战斗帧(已有),只有警告 FX 沿用通用版不重做。"),
    "portraits": ("Boss 立绘批（5 Boss × 立绘/全身/图标 = 15 张）",
                  "立绘构图重心偏上(下方留 HUD 位);全身图战斗姿态;图标为剪影徽章。furnace_matriarch 按既有设计:宽矮重甲六足、炉核唯一高亮、两层可剥离炉壳可读出双相位。"),
}


def build(batch: str):
    jobs = [a for a in G.A if a["batch"] == batch]
    title, chain_note = BATCH_ZH[batch]
    lines = []
    A = lines.append
    A(f"我要为一个 2D 塔防游戏《Emberline Defense》(余烬铁道)的内容扩充生成{title}。请先通读世界观和技术约束,严格按清单顺序生成,每张按对应文件名命名。{chain_note}")
    A("")
    A("# 一、世界观")
    A("")
    A("余烬铁道的温暖衰败:锈蚀铁轨、冷却熔炉、崩塌终点站。塔=工业战争机械(锻铁+身份色发光核心);敌人=甲壳有机体(暗色+余烬点缀,有机但不恶心);Boss=庞然移动堡垒(炉核是唯一高亮源)。与现有塔/敌帧同手绘语言。")
    A("")
    A("# 二、技术约束")
    A("")
    A("- 1024×1024 透明背景 PNG(立绘/全身例外,见条目);单主体居中")
    A("- 链式条目必须上传对应参考图原图,保持构图/剪影/接地位像素级一致")
    A("- 边缘干净:严禁残留底、半透明雾场、棋盘格(硬性红线)")
    A("- 手绘质感;严禁文字、水印、3D 渲染感")
    A("")
    A("# 三、交付清单")
    A("")
    for a in jobs:
        A(f"## {a['name']}")
        if a["dep"]:
            A(f"参考图上传:{RAWDIR}/{a['dep']}.png")
        A(f"保存文件名:{a['name']}.png")
        A("```text")
        A(a["prompt"])
        A("```")
        A("")
    A("# 四、产出落地与导入")
    A("")
    A(f"原图统一放 {RAWDIR}/,然后:")
    A("```bash")
    A(f"python tools/generate_expansion.py --batch {batch} --import-only")
    A(f"python tools/generate_expansion.py --only <资产名> --import-only  # 单张重导")
    A("```")
    out = OUTDIR / f"minimax_expansion_{batch}.txt"
    out.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    return out, len(jobs)


def main():
    for batch in ("towers", "enemies", "bosses", "portraits"):
        out, n = build(batch)
        print(f"{out}: {n} assets")


if __name__ == "__main__":
    main()
