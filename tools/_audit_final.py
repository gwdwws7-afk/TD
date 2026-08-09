import csv, os, wave

# Load spec
spec = {}
with open('design/spec/minimax_audio_prompts.csv', encoding='utf-8') as f:
    for r in csv.DictReader(f):
        spec[r['filename']] = {
            'id': r['id'], 'priority': r['priority'],
            'duration': float(r['duration_seconds']),
            'category': r['category'],
        }

# Index ONLY the final/usable set: sfx_final/*, music/*, ambient/*
FINAL_DIRS = ['output/audio/sfx_final', 'output/audio/music', 'output/audio/ambient']
gen = {}
for d in FINAL_DIRS:
    if not os.path.isdir(d):
        continue
    for dirpath, dirs, files in os.walk(d):
        for fn in files:
            if not fn.endswith('.wav'):
                continue
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, 'output/audio')
            bare = fn[:-4]
            try:
                with wave.open(path, 'rb') as w:
                    ch = w.getnchannels()
                    rate = w.getframerate()
                    bit = w.getsampwidth() * 8
                    dur = w.getnframes() / float(rate)
                    gen[bare] = (rel, dur, ch, rate, bit, os.path.getsize(path))
            except Exception as e:
                gen[bare] = (rel, None, None, None, None, os.path.getsize(path))

print('=' * 72)
print('FINAL AUDIO AUDIT (sfx_final + music + ambient)')
print('=' * 72)
print('Total usable files: {}'.format(len(gen)))

# Coverage
covered = []
missing = []
for spec_path, info in spec.items():
    bare = spec_path.split('/')[-1]
    if bare in gen:
        covered.append((spec_path, info, bare))
    else:
        missing.append((spec_path, info))

print('\nCOVERED: {}/{}'.format(len(covered), len(spec)))
print('MISSING: {}/{}\n'.format(len(missing), len(spec)))

if missing:
    print('--- STILL MISSING ({} items) ---'.format(len(missing)))
    for sp, info in sorted(missing, key=lambda x: (x[1]['priority'], x[0])):
        print('  [{}] {} ({}s)'.format(info['priority'], sp, info['duration']))

# Duration/channel report for covered
print('\n' + '=' * 72)
print('PER-FILE SPECS (covered)')
print('=' * 72)
print('{:<32} {:>7} {:>4} {:>5}  {:<6} {:<36}'.format('FILE', 'DUR', 'CH', 'RATE', 'SPEC', 'STATUS'))
print('-' * 96)

issues = []
for spec_path, info, bare in sorted(covered, key=lambda x: (x[1]['priority'], x[0])):
    rel, dur, ch, rate, bit, size = gen[bare]
    durstr = '{:.2f}s'.format(dur) if dur else 'ERR'
    chstr = str(ch) if ch else '?'
    spec_dur = info['duration']
    spec_str = '{:.1f}s'.format(spec_dur)
    status = 'OK'
    notes = []
    if dur is None:
        status = 'UNREADABLE'
    else:
        if info['category'] == 'SFX':
            # short SFX expected; check if wildly over
            ratio = dur / spec_dur if spec_dur > 0 else 1
            if ratio > 1.5:
                status = 'OVER'
                notes.append('{:.0f}x spec'.format(ratio))
        # channel check for SFX (mono expected except known stereo)
        stereo_ok = bare in ('ember_surge', 'fracture_mark', 'matrix_convergence', 'route_switch')
        if info['category'] == 'SFX' and ch == 2 and not stereo_ok:
            notes.append('stereo')
    if notes:
        status += ' (' + ','.join(notes) + ')'
    print('{:<32} {:>7} {:>4} {:>5}  {:<6} {:<36}'.format(bare[:31], durstr, chstr, rate or '?', spec_str, status))
    if status != 'OK':
        issues.append((bare, status, dur, spec_dur))

print('\n' + '=' * 72)
if issues:
    print('{} FILES NEED ATTENTION:'.format(len(issues)))
    for bare, status, dur, sd in issues:
        print('  {} -> {}'.format(bare, status))
else:
    print('ALL FILES COMPLIANT')
