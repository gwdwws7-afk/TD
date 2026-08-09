import csv, os, wave

# Load spec
spec = {}
with open('design/spec/minimax_audio_prompts.csv', encoding='utf-8') as f:
    for r in csv.DictReader(f):
        spec[r['filename']] = {
            'id': r['id'], 'priority': r['priority'],
            'duration': float(r['duration_seconds']),
            'loop': r['loop'], 'format': r['format'], 'category': r['category'],
        }

# Index ALL generated wav files by their bare filename (last segment without ext)
# and record (relpath, duration, channels, rate, bit)
gen = {}  # bare_name -> list of (relpath, dur, ch, rate, bit)
for dirpath, dirs, files in os.walk('output/audio'):
    for fn in files:
        if not fn.endswith('.wav'):
            continue
        path = os.path.join(dirpath, fn)
        rel = os.path.relpath(path, 'output/audio')
        bare = fn[:-4]
        info = None
        try:
            with wave.open(path, 'rb') as w:
                ch = w.getnchannels()
                rate = w.getframerate()
                bit = w.getsampwidth() * 8
                dur = w.getnframes() / float(rate)
                info = (rel, dur, ch, rate, bit)
        except Exception as e:
            info = (rel, None, None, None, str(e))
        gen.setdefault(bare, []).append(info)

# Coverage check
print('=' * 70)
print('COVERAGE CHECK')
print('=' * 70)
covered = []
missing = []
for spec_path, sp_info in spec.items():
    bare = spec_path.split('/')[-1]
    if bare in gen:
        covered.append((spec_path, sp_info, bare))
    else:
        missing.append((spec_path, sp_info))

print('Covered: {}/{}'.format(len(covered), len(spec)))
print('Missing: {}/{}'.format(len(missing), len(spec)))
if missing:
    print('\n--- MISSING ({} items) ---'.format(len(missing)))
    for sp, info in sorted(missing, key=lambda x: (x[1]['priority'], x[0])):
        print('  [{}] {} ({}s)'.format(info['priority'], sp, info['duration']))

# Duplicates: same bare name in multiple dirs
print('\n' + '=' * 70)
print('DUPLICATE NAMES (same sound in multiple folders)')
print('=' * 70)
dups = {k: v for k, v in gen.items() if len(v) > 1}
if dups:
    for name, entries in sorted(dups.items()):
        print('\n  {} ({} copies):'.format(name, len(entries)))
        for rel, dur, ch, rate, bit in entries:
            durstr = '{:.2f}s'.format(dur) if dur else 'ERR'
            chstr = '{}ch'.format(ch) if ch else '?'
            print('    {:<40} {:>7} {}'.format(rel, durstr, chstr))
else:
    print('  none')

# Duration/channel compliance for covered items (prefer sfx_final version if exists)
print('\n' + '=' * 70)
print('COMPLIANCE CHECK (duration + channels vs spec)')
print('=' * 70)
noncompliant = []
for spec_path, sp_info, bare in covered:
    options = gen[bare]
    # prefer the shortest-duration version (sfx_final are short, sfx/ are bloated)
    valid = [o for o in options if o[1] is not None]
    if not valid:
        noncompliant.append((spec_path, sp_info, 'UNREADABLE', None))
        continue
    # pick the version closest to spec duration
    best = min(valid, key=lambda o: abs(o[1] - sp_info['duration']))
    rel, dur, ch, rate, bit = best
    issues = []
    if dur is None:
        issues.append('unreadable')
    else:
        # duration tolerance: SFX within 30%, music/ambience just check it exists
        tol = 0.30 if sp_info['category'] == 'SFX' else 0.5
        if sp_info['category'] == 'SFX' and abs(dur - sp_info['duration']) / sp_info['duration'] > tol:
            issues.append('dur {:.2f}s vs spec {:.2f}s'.format(dur, sp_info['duration']))
        if sp_info['category'] == 'SFX' and ch != 1 and bare not in ('ember_surge', 'fracture_mark', 'matrix_convergence', 'route_switch'):
            issues.append('expected mono got {}ch'.format(ch))
    if issues:
        noncompliant.append((spec_path, sp_info, '; '.join(issues), rel))

if noncompliant:
    print('{} items with issues:'.format(len(noncompliant)))
    for sp, info, msg, rel in noncompliant:
        print('  [{}] {:<42} {}'.format(info['priority'], sp.split('/')[-1], msg))
else:
    print('All covered items compliant.')

# Summary of what's clean and usable from sfx_final
print('\n' + '=' * 70)
print('FINAL USABLE SET (sfx_final + music + ambient)')
print('=' * 70)
usable = 0
for dirpath, dirs, files in os.walk('output/audio'):
    if 'sfx' in dirpath and 'sfx_final' not in dirpath and 'scene' not in dirpath:
        continue  # skip the bloated sfx/ dupes
    for fn in files:
        if fn.endswith('.wav'):
            usable += 1
print('Usable files (sfx_final + music + ambient): {}'.format(usable))
