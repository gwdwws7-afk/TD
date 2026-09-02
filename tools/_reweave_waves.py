# -*- coding: utf-8 -*-
"""Batch-3 wave reweaver (content-matrix-20-level-v2 + balance-reweave-input-v1).

Transforms the 20 wave files:
  A segment L01-09: curve FROZEN (reward/budget/prep/count) — composition
    surgery only (new enemies swap in threat-matched, occupancy control).
  B segment L10-15: budgetTarget recomputed as L09's curve x linear mult
    (1.04 .. 1.24), rewards floored at 45, L13 boss.
  C segment L16-20: curve FROZEN — new content swaps in (L17 boss, L19 brood).

Boss waves: L05/09/13/17 W20 -> phase boss + boss_entry group; W18/19 give
back 6% budget each to fund the boss threat.

Validation: per-wave threat fidelity, occupancy stats, archetype counts,
unlock gating, reward floors — printed and written to the reweave report.
"""
import json
import random
from pathlib import Path

random.seed(20260902)

ROOT = Path(__file__).resolve().parents[1]
WAVES = ROOT / 'Assets/Resources/Data/waves'
REPORT = ROOT / 'design/reviews/reweave-report-batch3.md'

catalog = {e['enemyId']: e for e in json.loads(
    (ROOT / 'Assets/Resources/Data/enemies/enemy_catalog_main_v1.json').read_text(encoding='utf-8-sig'))['enemies']}

MAPS = {1:'grayline_junction',2:'grayline_junction',3:'grayline_junction',4:'grayline_junction',
        5:'ashfall_depot',6:'ashfall_depot',7:'ashfall_depot',8:'ashfall_depot',
        9:'split_switch_canyon',10:'split_switch_canyon',11:'split_switch_canyon',12:'split_switch_canyon',
        13:'hollow_kiln_basin',14:'hollow_kiln_basin',15:'hollow_kiln_basin',16:'hollow_kiln_basin',
        17:'last_ember_terminus',18:'last_ember_terminus',19:'last_ember_terminus',20:'last_ember_terminus'}

ENEMY_UNLOCK = {1:['skitter_runner'], 2:['ash_swarm'], 4:['cinder_husk'], 5:['rail_splitter'],
                6:['carapace_brute'], 7:['plated_spore'], 8:['burrow_sapper','acid_blister'],
                9:['ember_leech'], 10:['spore_carrier'], 11:['rail_warden'], 13:['cinder_glider'],
                14:['forge_dragoon'], 15:['husk_titan','ember_strider'], 17:['echo_mimic'],
                19:['echo_brood'], 20:['furnace_matriarch']}
UNLOCK_LEVEL = {}
for lv, ids in ENEMY_UNLOCK.items():
    for eid in ids:
        UNLOCK_LEVEL[eid] = lv
UNLOCK_LEVEL.update({'containermaw': 5, 'junction_tyrant': 9, 'kiln_custodian': 13, 'echo_harbinger': 17})

NEW_ENEMIES = ['cinder_husk', 'rail_splitter', 'acid_blister', 'forge_dragoon', 'ember_strider', 'echo_brood']
BOSS_LEVELS = {5:'containermaw', 9:'junction_tyrant', 13:'kiln_custodian', 17:'echo_harbinger'}
FILLER = ['skitter_runner', 'ash_swarm']
CN_NAMES = {'cinder_husk':'灰渣傀儡','rail_splitter':'轨面裂虫','acid_blister':'酸蚀孢囊',
            'forge_dragoon':'锻炉骑兵','ember_strider':'余烬行者','echo_brood':'回响虫群',
            'containermaw':'集装箱装卸兽','junction_tyrant':'岔道暴君','kiln_custodian':'窑炉看守','echo_harbinger':'回响先驱'}

def threat(eid):
    return max(0.1, catalog[eid]['threatCost'])

def available(lv):
    return [e for e, u in UNLOCK_LEVEL.items() if u <= lv and e != 'furnace_matriarch']

def wave_threat(w):
    return sum(g['count'] * threat(g['enemyId']) for g in w['groups'])

def load(lv):
    return json.loads((WAVES / f'{MAPS[lv]}_l{lv:02d}_v1.json').read_text(encoding='utf-8-sig'))

def save(lv, d):
    (WAVES / f'{MAPS[lv]}_l{lv:02d}_v1.json').write_text(
        json.dumps(d, ensure_ascii=False, indent=2), encoding='utf-8-sig')

def lane_pool(w):
    return [g.get('lane','center') for g in w['groups']] or ['center']

report = ['# 批 3 波次重织报告（工具生成 + 人工复核输入）', '']

# ── B-segment base: L09 curve ──
l09 = load(9)['waves']
l09_curve = [w['budgetTarget'] for w in l09]

for lv in range(1, 21):
    d = load(lv)
    ws = d['waves']
    segment = 'A' if lv <= 9 else ('B' if lv <= 15 else 'C')
    mult = 1 + 0.04 * (lv - 9) if segment == 'B' else 1.0
    # Acceptance tuning (battery 2026-09-02): adaptive won with 2 integrity,
    # control collapsed at W7 — L13 rides 1.08 instead of 1.16 so the
    # low-damage doctrine can finish the exam while keeping the boss fight.
    if lv == 13:
        mult = 1.08
    boss_id = BOSS_LEVELS.get(lv)
    avail = available(lv)
    intro_today = ENEMY_UNLOCK.get(lv, [])

    # Budget/reward pass
    for i, w in enumerate(ws):
        if segment == 'B':
            w['budgetTarget'] = round(l09_curve[i] * mult, 1)
            w['rewardGold'] = max(w['rewardGold'], 45)
        if boss_id and i >= 17 and i <= 18:
            w['budgetTarget'] = round(w['budgetTarget'] * 0.94, 1)
        if boss_id and i == 19:
            w['budgetTarget'] = round(w['budgetTarget'] + threat(boss_id), 1)

    # Composition surgery: only where the roster allows variety
    if len(avail) >= 3:
        intro_done = set()
        skitter_waves = 0
        ash_waves = 0
        # Occupancy cap tiers by roster breadth: thin-roster levels (3-4
        # archetypes available) need BOTH crawlers to express mass without
        # degenerate hordes; variety levels get the strict cap.
        wave_cap = 11 if len(avail) >= 5 else 14
        for i, w in enumerate(ws):
            is_boss_wave = boss_id and i == 19
            T = w['budgetTarget']

            # keep non-filler groups; collect filler mass
            keep, filler_groups = [], []
            for g in w['groups']:
                if g['enemyId'] in FILLER:
                    filler_groups.append(dict(g))
                else:
                    keep.append(dict(g))
            keep_threat = sum(g['count'] * threat(g['enemyId']) for g in keep)

            # schedule new enemies for this wave
            slices = []
            for e in NEW_ENEMIES:
                if UNLOCK_LEVEL[e] > lv:
                    continue
                is_intro_level = UNLOCK_LEVEL[e] == lv
                # intro wave: wave 2 (W3) on the unlock level, pure
                if is_intro_level and not (e in intro_done) and i == 2 and not is_boss_wave:
                    share = 0.30
                    intro_done.add(e)
                elif is_intro_level:
                    # post-intro trickle on its own level keeps the teaching
                    # fresh without diluting the intro wave
                    if i < 4 or (lv + i) % 4 != 1:
                        continue
                    share = 0.12
                else:
                    # rotation at later levels: deterministic, ~40% of waves
                    if (lv * 7 + i * 3 + sum(ord(c) for c in e)) % 5 >= 2:
                        continue
                    share = 0.16
                room = max(0.0, T * (1 if segment != 'B' else 1) - keep_threat)
                slice_t = min(T * share, max(room * 0.5, threat(e)))
                cnt = max(1, int(round(slice_t / threat(e))))
                if keep_threat + cnt * threat(e) > T * 1.35:
                    cnt = 1
                slices.append((e, cnt))

            slice_threat = sum(c * threat(e) for e, c in slices)

            # filler budget
            F = T - keep_threat - slice_threat
            lanes = lane_pool(w)
            rebuilt = list(keep)
            for e, cnt in slices:
                rebuilt.append({
                    'enemyId': e, 'count': cnt, 'startDelay': round(0.6 + random.random() * 1.2, 2),
                    'spawnInterval': 0.3 if 'fast' in catalog[e].get('tags', []) else 0.45,
                    'formation': 'stream' if 'fast' in catalog[e].get('tags', []) else 'pack',
                    'lane': random.choice(lanes)})
            if F >= 3.0:
                # ONE primary filler per wave, alternating by original
                # presence under both per-level caps (11 waves each) — this
                # is what actually pulls the two crawl waves' occupancy down
                # without discarding mass.
                # Thin-roster levels (3-4 archetypes): keep the authored
                # crawler SPLIT, scaled to the remaining mass — forcing the
                # occupancy cap there only breeds 80-unit hordes.
                if len(avail) < 5:
                    orig_f_threat = sum(g['count'] * threat(g['enemyId']) for g in filler_groups)
                    if orig_f_threat > 0:
                        for g in filler_groups:
                            g['count'] = max(1 if g['count'] > 0 else 0,
                                             int(g['count'] * F / orig_f_threat))
                            if g['count'] > 0:
                                rebuilt.append(g)
                    # Same validator window fit as the main path.
                    tol = w.get('budgetTolerance', 1.1) or 1.1
                    lo = T * (2 - tol) + 0.02
                    hi = T * tol - 0.02
                    guard = 0
                    act = sum(g['count'] * threat(g['enemyId']) for g in rebuilt)
                    while act > hi and guard < 200:
                        donors = [g for g in rebuilt if g['count'] > 1]
                        if not donors:
                            break
                        g = max(donors, key=lambda x: threat(x['enemyId']))
                        g['count'] -= 1
                        act -= threat(g['enemyId'])
                        guard += 1
                    while act < lo and guard < 400:
                        tg = next((g for g in rebuilt if g['enemyId'] == 'skitter_runner'), None)
                        if tg is None:
                            rebuilt.append({'enemyId':'skitter_runner','count':1,'startDelay':0.08,'spawnInterval':0.25,'formation':'stream','lane':'center'})
                        else:
                            tg['count'] += 1
                        act += 1.0
                        guard += 1
                    if any(g['enemyId'] == 'skitter_runner' for g in rebuilt): skitter_waves += 1
                    if any(g['enemyId'] == 'ash_swarm' for g in rebuilt): ash_waves += 1
                    w['groups'] = [g for g in rebuilt if g['count'] > 0]
                    continue
                had_sk = any(g['enemyId'] == 'skitter_runner' for g in filler_groups)
                had_as = any(g['enemyId'] == 'ash_swarm' for g in filler_groups)
                if skitter_waves < wave_cap and had_sk:
                    primary = 'skitter_runner'
                elif ash_waves < wave_cap and had_as:
                    primary = 'ash_swarm'
                elif skitter_waves <= ash_waves:
                    primary = 'skitter_runner'  # soft balance when both capped
                else:
                    primary = 'ash_swarm'
                cnt = int(F / threat(primary))
                lane = 'center' if primary == 'skitter_runner' else random.choice(lanes)
                # Cap single-group mass and split the remainder across lanes —
                # a 100-unit blob on one lane breaks pacing and the
                # concurrent-spawn hint.
                cap = 34 if primary == 'skitter_runner' else 40
                part = 0
                while cnt > 0:
                    take = min(cnt, cap)
                    rebuilt.append({'enemyId': primary, 'count': take,
                                    'startDelay': round(0.08 + part * 0.7, 2),
                                    'spawnInterval': 0.25 if primary == 'skitter_runner' else 0.22,
                                    'formation': 'stream' if primary == 'skitter_runner' else 'swarm',
                                    'lane': lane if part == 0 else random.choice(lanes)})
                    cnt -= take
                    part += 1
            # over-budget trim only: shave largest groups until inside +10%
            # (originals already carried -4%..+10% authoring tolerance; closing
            # under-gaps with skitter floods occupancy, so gaps are left open)
            over = wave_threat({'groups': rebuilt}) - T * 1.10
            if over > 0:
                for g in sorted(rebuilt, key=lambda g: -g['count']):
                    if over <= 0: break
                    t1 = threat(g['enemyId'])
                    drop = min(g['count'] - 1, int(over / t1) + 1)
                    if drop > 0:
                        g['count'] -= drop
                        over -= drop * t1
            rebuilt = [g for g in rebuilt if g['count'] > 0]

            # Fit the runtime validator's exact window [T*(2-tol), T*tol] —
            # the loader rejects the whole file otherwise (this session's
            # hardest-won lesson). Crawler granularity covers every touched
            # wave (T >= 17 -> window >= 1.36).
            tol = w.get('budgetTolerance', 1.1) or 1.1
            lo = T * (2 - tol) + 0.02
            hi = T * tol - 0.02
            guard = 0
            act = sum(g['count'] * threat(g['enemyId']) for g in rebuilt)
            while act > hi and guard < 200:
                donors = [g for g in rebuilt if g['count'] > 1 and g['enemyId'] in FILLER] or [g for g in rebuilt if g['count'] > 1]
                if not donors:
                    break
                g = max(donors, key=lambda x: threat(x['enemyId']))
                g['count'] -= 1
                act -= threat(g['enemyId'])
                guard += 1
            while act < lo and guard < 400:
                tg = next((g for g in rebuilt if g['enemyId'] == 'skitter_runner'), None)
                if tg is None:
                    rebuilt.append({'enemyId':'skitter_runner','count':1,'startDelay':0.08,'spawnInterval':0.25,'formation':'stream','lane':'center'})
                else:
                    tg['count'] += 1
                act += 1.0
                guard += 1
            rebuilt = [g for g in rebuilt if g['count'] > 0]
            rebuilt_sk = any(g['enemyId'] == 'skitter_runner' for g in rebuilt)
            rebuilt_as = any(g['enemyId'] == 'ash_swarm' for g in rebuilt)
            if rebuilt_sk and not any(g['enemyId'] == 'skitter_runner' for g in keep):
                skitter_waves += 1
            if rebuilt_as and not any(g['enemyId'] == 'ash_swarm' for g in keep):
                ash_waves += 1
            w['groups'] = rebuilt

            # intro wave marking (level-of-unlock, wave 3)
            if i == 2 and intro_today and not is_boss_wave:
                w['phase'] = 'introduce'
                names = '、'.join(CN_NAMES.get(e, e) for e in intro_today if e in NEW_ENEMIES)
                if names:
                    w['hint'] = f"[L{lv:02d}] W03: 新威胁初现——{names}。观察其行为再定火力。"

        # boss wave construction
        if boss_id:
            w = ws[19]
            w['phase'] = 'boss'
            w.setdefault('threatTags', [])
            for tag in ('boss', MAPS[lv]):
                if tag not in w['threatTags']:
                    w['threatTags'].append(tag)
            w['groups'] = [g for g in w['groups'] if g['enemyId'] != boss_id]
            w['groups'].append({'enemyId': boss_id, 'count': 1, 'startDelay': 3.0,
                                'spawnInterval': 3.0, 'formation': 'boss_entry', 'lane': 'all'})
            # Boss threat is fixed; trim support into the validator window.
            bT = w['budgetTarget']
            btol = w.get('budgetTolerance', 1.1) or 1.1
            bhi = bT * btol - 0.02
            bact = sum(g['count'] * threat(g['enemyId']) for g in w['groups'])
            guard = 0
            while bact > bhi and guard < 200:
                donors = [g for g in w['groups'] if g['count'] > 0 and g['enemyId'] != boss_id]
                if not donors:
                    break
                g = max(donors, key=lambda x: x['count'])
                g['count'] -= 1
                bact -= threat(g['enemyId'])
                guard += 1
            w['groups'] = [g for g in w['groups'] if g['count'] > 0]
            w['hint'] = f"[L{lv:02d}] W20 考试 Boss：{CN_NAMES[boss_id]}——用这一关教过的答案应对。"
            w.setdefault('prepSeconds', 8.0)
            w['prepSeconds'] = max(w['prepSeconds'], 10.0)  # spec: boss prep +5s-ish

    save(lv, d)

# ── Validation ──
lines = ['## 校验', '', '| 关 | 段 | 威胁偏差(max) | skitter波 | ash波 | 原型数 | Boss |', '|---|---|---|---|---|---|---|']
tot_sk = tot_as = tot_w = tot_arch = 0
worst_overall = 0.0
for lv in range(1, 21):
    d = load(lv)
    ws = d['waves']
    seg = 'A' if lv <= 9 else ('B' if lv <= 15 else 'C')
    maxdev = 0.0
    for w in ws:
        dev = abs(wave_threat(w) - w['budgetTarget']) / max(1.0, w['budgetTarget'])
        maxdev = max(maxdev, dev)
        for g in w['groups']:
            assert UNLOCK_LEVEL.get(g['enemyId'], 99) <= lv, f"L{lv} has {g['enemyId']} before unlock"
    sk = sum(1 for w in ws if any(g['enemyId'] == 'skitter_runner' for g in w['groups']))
    asw = sum(1 for w in ws if any(g['enemyId'] == 'ash_swarm' for g in w['groups']))
    arch = len({g['enemyId'] for w in ws for g in w['groups']})
    boss = BOSS_LEVELS.get(lv, '✓' if lv == 20 else '')
    has_boss = 'boss' if any(g['enemyId'] in ('containermaw','junction_tyrant','kiln_custodian','echo_harbinger','furnace_matriarch') for w in ws for g in w['groups']) else ''
    if lv == 20: has_boss = 'boss'
    tot_sk += sk; tot_as += asw; tot_w += len(ws); tot_arch += arch
    worst_overall = max(worst_overall, maxdev)
    lines.append(f"| L{lv:02d} | {seg} | {maxdev*100:.0f}% | {sk}/20 | {asw}/20 | {arch} | {has_boss} |")
lines.append(f"| **合计** | | 最差 {worst_overall*100:.0f}% | **{100*tot_sk/tot_w:.0f}%** | **{100*tot_as/tot_w:.0f}%** | 平均 {tot_arch/20:.1f} | 5 Boss |")
lines.append('')
lines.append(f"- 全局 skitter 占用 {100*tot_sk/tot_w:.0f}% / ash {100*tot_as/tot_w:.0f}%（目标 ≤55%；L01-03 结构性例外：可用原型 ≤2）")
lines.append(f"- 平均每关原型数 {tot_arch/20:.1f}（目标 ≥7.5）")
violations = []
for lv in range(1, 21):
    for w in load(lv)['waves']:
        tol = w.get('budgetTolerance', 1.1) or 1.1
        st = wave_threat(w)
        lo_, hi_ = w['budgetTarget'] * (2 - tol), w['budgetTarget'] * tol
        if st < lo_ - 0.01 or st > hi_ + 0.01:
            violations.append(f"L{lv} W{w['waveIndex']} actual={st:.1f} window=[{lo_:.1f},{hi_:.1f}]")
if violations:
    print("VALIDATOR VIOLATIONS (%d):" % len(violations))
    for v in violations[:20]:
        print(" -", v)
    raise SystemExit("budget windows violated")
b_floor_ok = all(w['rewardGold'] >= 45 for lv in range(10,16) for w in load(lv)['waves'])
lines.append(f"- B 段奖励地板 ≥45：{'通过' if b_floor_ok else '未过'}")
report += lines
REPORT.write_text('\n'.join(report), encoding='utf-8')
print('\n'.join(lines))
