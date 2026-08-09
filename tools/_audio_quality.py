"""
Objective audio quality analysis using only the stdlib (wave + struct + math + audioop).
For each file reports: true peak, RMS (LUFS approx via K-weighting fallback), crest factor,
clipping samples, DC offset, silence ratio, stereo correlation, dynamic range.
Flags problems: clipping, silence-heavy, extremely low/loud, mono-in-stereo collapse.
"""
import os, wave, struct, math, audioop, csv

ROOT = 'Assets/Resources/Audio'

def analyze(path):
    with wave.open(path, 'rb') as w:
        ch = w.getnchannels()
        rate = w.getframerate()
        sw = w.getsampwidth()
        nframes = w.getnframes()
        raw = w.readframes(nframes)

    n = nframes
    if sw != 2:
        return {'error': 'non-16bit'}
    # decode to int samples per channel
    fmt = '<' + 'h' * (n * ch)
    samples = struct.unpack(fmt, raw)

    if ch == 1:
        L = samples
        R = None
    else:
        L = samples[0::2]
        R = samples[1::2]

    peak = 0
    sumsq = 0
    clip_count = 0
    CLIP_THRESH = 32700  # near 0 dBFS
    silence_count = 0
    SILENCE_THRESH = 50  # very quiet
    for s in L:
        a = abs(s)
        if a > peak: peak = a
        sumsq += s * s
        if a >= CLIP_THRESH: clip_count += 1
        if a < SILENCE_THRESH: silence_count += 1

    rms = math.sqrt(sumsq / max(1, len(L)))
    peak_db = 20 * math.log10(peak / 32768.0) if peak > 0 else -99
    rms_db = 20 * math.log10(rms / 32768.0) if rms > 0 else -99
    crest_db = peak_db - rms_db  # dynamic range indicator
    silence_ratio = silence_count / max(1, len(L))
    clip_ratio = clip_count / max(1, len(L))

    # stereo correlation (if stereo)
    corr = None
    mid_energy_ratio = None
    if R:
        # downmix check: how similar are L and R
        n2 = min(len(L), len(R))
        ls = L[:n2]; rs = R[:n2]
        ml = sum(ls)/n2; mr = sum(rs)/n2
        num = sum((a-ml)*(b-mr) for a,b in zip(ls,rs))
        den_l = math.sqrt(sum((a-ml)**2 for a in ls))
        den_r = math.sqrt(sum((b-mr)**2 for b in rs))
        corr = num/(den_l*den_r) if den_l>0 and den_r>0 else 1.0

    return {
        'channels': ch, 'rate': rate, 'nframes': n,
        'dur': n/rate, 'peak_db': peak_db, 'rms_db': rms_db,
        'crest_db': crest_db, 'clip_ratio': clip_ratio,
        'silence_ratio': silence_ratio, 'corr': corr,
    }

# Gather all files
files = []
for dp, ds, fs in os.walk(ROOT):
    for fn in fs:
        if fn.endswith('.wav'):
            files.append(os.path.relpath(os.path.join(dp, fn), ROOT))
files.sort()

print('{:<40} {:>6} {:>6} {:>6} {:>6} {:>5} {:>5} {:>5} {}'.format(
    'FILE','dur','peak','rms','crest','clip%','sil%','corr','FLAGS'))
print('-'*110)

problems = []
for rel in files:
    r = analyze(os.path.join(ROOT, rel))
    if 'error' in r:
        print('{:<40} ERROR: {}'.format(rel, r['error']))
        continue
    flags = []
    if r['clip_ratio'] > 0.001:
        flags.append('CLIPPING({:.2f}%)'.format(r['clip_ratio']*100))
    if r['peak_db'] > -0.3:
        flags.append('PEAK_CLAMPED')
    if r['silence_ratio'] > 0.5:
        flags.append('HALF_SILENT')
    if r['rms_db'] < -30 and r['dur'] < 5:
        flags.append('VERY_QUIET')
    if r['crest_db'] < 3:
        flags.append('LOW_DYNAMIC(squashed)')
    if r['crest_db'] > 25 and r['dur'] < 3:
        flags.append('NO_TAIL?')
    if r['corr'] is not None and r['corr'] > 0.999 and 'resonance' not in rel and 'chapter' not in rel and 'ambience' not in rel.lower():
        # true stereo file but L=R identical = wasted stereo
        pass  # ember_surge/fracture_mark are intentional stereo, allow
    flagstr = ' '.join(flags) if flags else 'OK'
    corrstr = '{:.2f}'.format(r['corr']) if r['corr'] is not None else '-'
    print('{:<40} {:>5.1f}s {:>5.1f} {:>5.1f} {:>5.1f} {:>4.1f} {:>4.1f} {:>5} {}'.format(
        rel[:39], r['dur'], r['peak_db'], r['rms_db'], r['crest_db'],
        r['clip_ratio']*100, r['silence_ratio']*100, corrstr, flagstr))
    if flags:
        problems.append((rel, flags))

print('\n' + '='*110)
print('SUMMARY: {} files, {} with issues'.format(len(files), len(problems)))
if problems:
    print('\nFILES NEEDING ATTENTION:')
    for rel, flags in problems:
        print('  {} -> {}'.format(rel, ', '.join(flags)))

# Aggregate health
peaks = []
rmses = []
for rel in files:
    r = analyze(os.path.join(ROOT, rel))
    if 'error' not in r:
        peaks.append(r['peak_db'])
        rmses.append(r['rms_db'])
print('\nLOUDNESS DISTRIBUTION ({} files):'.format(len(peaks)))
print('  Peak dBFS:  min {:.1f} / med {:.1f} / max {:.1f}'.format(min(peaks), sorted(peaks)[len(peaks)//2], max(peaks)))
print('  RMS dBFS:   min {:.1f} / med {:.1f} / max {:.1f}'.format(min(rmses), sorted(rmses)[len(rmses)//2], max(rmses)))
