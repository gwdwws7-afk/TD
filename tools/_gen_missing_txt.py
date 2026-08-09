import csv

# Order: enemy mechanics first, then campaign UI, then tutorial
missing_order = [
    ('burrow_ambush', '敌人机制'),
    ('spore_split', '敌人机制'),
    ('mimic_shift', '敌人机制'),
    ('attrition_siphon', '敌人机制'),
    ('support_link', '敌人机制'),
    ('elite_pressure', '敌人机制'),
    ('level_select', '战役UI'),
    ('deploy_confirm', '战役UI'),
    ('chapter_reward', '战役UI'),
    ('early_dispatch', '战役UI'),
    ('tutorial_complete', '教程'),
]

with open('design/spec/minimax_audio_prompts.csv', encoding='utf-8') as f:
    spec = {r['filename'].split('/')[-1]: r for r in csv.DictReader(f)}

stereo_set = {'ember_surge','fracture_mark','matrix_convergence','route_switch'}

out = []
out.append('还差 11 个音效,请逐个生成。延续"余烬铁道"工业废土基调(参考 Frostpunk + Disco Elysium)。')
out.append('')
out.append('全局格式:44.1kHz/16-bit,.wav,峰值≤-3dBTP,起音<5ms。')
out.append('每个生成后报时长我确认。严格按时长,不要生成超长版本。')
out.append('')
out.append('禁止:NO史诗交响/NO EDM/NO恐怖跳吓/NO卡通/NO激光/NO魔幻仙音/NO怪兽吼叫。')
out.append('核心:Must evoke "the last railway of a dying ember civilization."')
out.append('')

n = 0
last_group = None
for bare, group in missing_order:
    if group != last_group:
        out.append('')
        if group == '敌人机制':
            out.append('═══ 特殊敌人机制音效(6个,P1)═══')
            out.append('这些是特定敌人的独有行为触发音,每个都有视觉特效但缺声音。')
        elif group == '战役UI':
            out.append('═══ 战役流程UI音效(4个,P1)═══')
            out.append('玩家选关/部署/领奖/提前开波的操作反馈。')
        elif group == '教程':
            out.append('═══ 教程音效(1个,P2)═══')
        out.append('')
        last_group = group
    r = spec[bare]
    n += 1
    ch = 'mono' if bare not in stereo_set else 'stereo'
    ch_cn = '单声道' if ch=='mono' else '立体声'
    out.append('{}. {}'.format(n, bare))
    out.append('   时长 {} 秒 | {} | {} | {} | {}'.format(r['duration_seconds'], ch, ch_cn, '16-bit', r['format'].upper()))
    out.append('   响度 {} LUFS'.format(r['lufs']))
    out.append('   {}'.format(r['prompt']))
    out.append('')

text = '\n'.join(out)
with open('design/spec/minimax_missing_11.txt','w',encoding='utf-8') as f:
    f.write(text)
print(text)
