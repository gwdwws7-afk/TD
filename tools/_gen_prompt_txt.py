import csv

CSV_PATH = 'design/spec/minimax_audio_prompts.csv'
OUT_PATH = 'design/spec/minimax_prompt_full.txt'

with open(CSV_PATH, 'r', encoding='utf-8') as f:
    rows = list(csv.DictReader(f))

# Group by priority preserving CSV order within each group
order = ['P0', 'P1', 'P2']
by_pri = {p: [r for r in rows if r['priority'] == p] for p in order}

lines = []
A = lines.append

A('我要为一个 2D 塔防游戏《Emberline Defense》(余烬铁道)批量生成完整的音乐和音效。请你先通读下面的世界观、调色板和交付清单,然后按 P0 → P1 → P2 的顺序逐个生成音频文件。每生成一个,用对应的文件名命名。')
A('')
A('# 一、世界观(所有声音必须服从)')
A('')
A('人类文明的余烬铁道在衰败。玩家是最后一位线务司令,在锈蚀的铁轨、冷却的熔炉、崩塌的终点站之间,用工业化战争机器抵御从灰烬中涌出的变异生物。基调是"温暖的衰败"——不是冰冷的科幻,也不是黑暗的恐怖,而是夕阳下老机器还在轰鸣的忧郁工业浪漫。')
A('')
A('参考坐标:《Frostpunk》原声的工业重量感 + 《Disco Elysium》的苍白庄严。')
A('')
A('# 二、声音调色板(每个声音都要落在这 6 类里)')
A('')
A('1. 金属呼吸:打击铁轨、汽缸排气、钢板共鸣、低频钟鸣 → 用于塔、机制、UI')
A('2. 余烬纹理:暖噪合成、火舌喷气、煤渣碎裂、低频 drone → 用于共振、危险')
A('3. 机械节奏:蒸汽脉冲、齿轮咬合、连杆往复、工业打击乐 → 用于音乐节律')
A('4. 生物质感:甲壳碎裂、湿滑蠕动、低吼共鸣、群体嘶鸣(有机但不恶心)→ 用于敌人')
A('5. 空间氛围:远处列车、风穿废墟、空旷混响尾音、磁带嘶声 → 用于环境、菜单')
A('6. 人性温度:单把大提琴、孤立钢琴单音、女声无词哼鸣(极少,仅情绪高潮)→ 用于胜利/失败')
A('')
A('# 三、全局混音约束(每个文件必须满足)')
A('')
A('- SFX:44.1kHz / 16-bit / mono / .wav,峰值 ≤ -3 dBTP,起音 < 5ms')
A('- 音乐/环境:44.1kHz / stereo / .ogg(或 wav),-16 LUFS(音乐)/ -26~-30 LUFS(环境)')
A('- 循环文件必须首尾无缝,无点击声')
A('- 母带整体 -14 LUFS')
A('- 瞬态清晰,战斗中不被音乐掩蔽')
A('')
A('# 四、绝对禁止(避免廉价感)')
A('')
A('NO epic orchestral(不要史诗交响)')
A('NO EDM drops(不要电子舞曲 drop)')
A('NO horror stingers(不要恐怖跳吓)')
A('NO cartoonish SFX(不要卡通音效)')
A('NO laser zap / sci-fi plasma(不要激光/等离子科幻音)')
A('NO magic sparkle / fantasy chime(不要魔幻仙音)')
A('NO generic RPG level-up stinger(不要通用 RPG 升级音)')
A('NO monster roar / kaiju screech(不要怪兽吼叫)')
A('')
A('每个声音的核心理念:Must evoke "the last railway of a dying ember civilization"(必须唤起"一个垂死的余烬文明最后一条铁道"的感觉)。')
A('')

idx = 0
total = len(rows)
A(f'# 五、交付清单(共 {total} 个,按优先级)')
A('')

cat_names_zh = {'Music': '音乐', 'SFX': '音效/战斗', 'Ambience': '环境'}

for p in order:
    group = by_pri[p]
    if not group:
        continue
    p_label = {'P0': 'P0 必需', 'P1': 'P1 强烈建议', 'P2': 'P2 锦上添花'}[p]
    A(f'## {p_label}({len(group)} 个{"，先做这些" if p=="P0" else "，做完P0后做" if p=="P1" else ""})')
    A('')
    # group within priority by category
    for cat in ['Music', 'SFX', 'Ambience']:
        cat_group = [r for r in group if r['category'] == cat]
        if not cat_group:
            continue
        A(f'【{cat_names_zh[cat]} {len(cat_group)} 个】')
        for r in cat_group:
            idx += 1
            loop_tag = 'loop' if r['loop'] == 'yes' else 'no loop'
            A(f"{idx}. {r['filename']} — {r['duration_seconds']}s,{loop_tag},{r['format']},{r['lufs']}LUFS。{r['prompt']}")
        A('')
    A('')

A('# 六、输出要求')
A('')
A('- 每个文件用第五部分的路径英文名命名(如 menu_theme.ogg、fire_rail_lancer.wav)')
A('- 音乐和环境床放 .ogg,SFX 放 .wav')
A('- 严格按 P0 → P1 → P2 顺序生成')
A('- 每生成一个告诉我文件名,我确认后再继续下一个')
A('- 如果某个声音你不确定,先描述你打算怎么做,我确认后再生成')
A('')
A('现在请先开始生成 P0 的第 1 个。')

with open(OUT_PATH, 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))

print(f'Wrote {OUT_PATH}: {len(lines)} lines, {idx} deliverables')
