"""Aggregate all 25 run results from the 5-pass full run."""
import json
import glob
import re

results = []
for f in sorted(glob.glob('output/playtest/full_run_5pass/*/*_summary.json')):
    tag = f.split('/')[-2]
    with open(f, encoding='utf-8-sig') as fh:
        s = json.load(fh)
    logs = s.get('consoleLogs', {}).get('data', []) or []
    wave_lines = [l for l in logs if 'WaveStat' in l]
    run_lines = [l for l in logs if 'RunSummary' in l]
    errs = [l for l in logs if 'Exception' in l or 'NullReference' in l or 'error CS' in l]
    tc = s.get('tacticalScore', -1)
    issues = s.get('effectiveConsoleIssues', []) or []

    last_w = 0
    last_kills = 0
    last_esc = 0
    for w in wave_lines:
        m = re.search(r'wave=(\d+)', w)
        if m:
            last_w = int(m.group(1))
        m = re.search(r'kills=(\d+)', w)
        if m:
            last_kills = int(m.group(1))
        m = re.search(r'escapes=(\d+)', w)
        if m:
            last_esc = int(m.group(1))

    results.append({
        'tag': tag, 'wave': last_w, 'tc': tc, 'kills': last_kills,
        'escapes': last_esc, 'errs': len(errs), 'issues': len(issues),
        'victory': bool(run_lines), 'error_lines': errs[:3],
    })

print('Total runs: %d' % len(results))
header = '%-45s %5s %4s %6s %4s %5s %7s' % ('Tag', 'Wave', 'TC', 'Kills', 'Esc', 'Errs', 'Issues')
print(header)
print('-' * 80)
for r in results:
    print('%-45s %4d/20 %4d %6d %4d %5d %7d' % (
        r['tag'], r['wave'], r['tc'], r['kills'], r['escapes'], r['errs'], r['issues']))

# Summary stats
print()
total_errs = sum(r['errs'] for r in results)
total_esc = sum(r['escapes'] for r in results)
valid_tcs = [r['tc'] for r in results if r['tc'] > 0]
avg_tc = sum(valid_tcs) / max(1, len(valid_tcs))
zero_wave = [r for r in results if r['wave'] == 0]
print('Total errors: %d' % total_errs)
print('Total escapes across all runs: %d' % total_esc)
print('Average tactical score (valid runs): %.1f' % avg_tc)
print('Runs with wave=0 (failed start): %d' % len(zero_wave))
for r in zero_wave:
    print('  FAILED: %s' % r['tag'])
    for e in r['error_lines']:
        print('    %s' % e[:200])

# Per-level averages
print()
print('=== Per-level breakdown ===')
for lvl_name in ['L01', 'L05', 'L09', 'L13', 'L20']:
    lvl_results = [r for r in results if r['tag'].startswith(lvl_name)]
    if lvl_results:
        avg_w = sum(r['wave'] for r in lvl_results) / len(lvl_results)
        lvl_valid = [r['tc'] for r in lvl_results if r['tc'] > 0]
        avg_tc_l = sum(lvl_valid) / max(1, len(lvl_valid))
        avg_esc = sum(r['escapes'] for r in lvl_results) / len(lvl_results)
        print('%s: avg_wave=%.1f/20 avg_tc=%.1f avg_escapes=%.1f runs=%d' % (
            lvl_name, avg_w, avg_tc_l, avg_esc, len(lvl_results)))

# Strategy comparison
print()
print('=== Strategy comparison ===')
for strat in ['focused_fire', 'control_lattice', 'adaptive_network']:
    strat_results = [r for r in results if strat in r['tag']]
    if strat_results:
        avg_w = sum(r['wave'] for r in strat_results) / len(strat_results)
        valid = [r['tc'] for r in strat_results if r['tc'] > 0]
        avg_tc = sum(valid) / max(1, len(valid))
        print('%s: avg_wave=%.1f avg_tc=%.1f runs=%d' % (strat, avg_w, avg_tc, len(strat_results)))

# All error/issue lines across runs
print()
print('=== All console issues ===')
all_issues = set()
for r in results:
    # Re-read to get issue text
    pass
for f in sorted(glob.glob('output/playtest/full_run_5pass/*/*_summary.json')):
    with open(f, encoding='utf-8-sig') as fh:
        s = json.load(fh)
    issues = s.get('effectiveConsoleIssues', []) or []
    for issue in issues:
        all_issues.add(issue[:200])

for issue in sorted(all_issues):
    print('  - %s' % issue)
