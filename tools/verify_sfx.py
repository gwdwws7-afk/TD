import wave
from pathlib import Path

p = Path('E:/TD/output/audio/sfx_final')
files = sorted(p.rglob('*.wav'))
print('%-32s %-7s %-7s %-8s %-10s' % ('file', 'ch', 'sr', 'dur_s', 'bytes'))
print('-' * 70)
for f in files:
    with wave.open(str(f), 'rb') as wf:
        ch = wf.getnchannels()
        sr = wf.getframerate()
        n = wf.getnframes()
        sw = wf.getsampwidth()
        dur = n / sr
    rel = str(f.relative_to(p))
    print('%-32s %-7d %-7d %-8.3f %-10d' % (rel, ch, sr, dur, f.stat().st_size))

total = sum(f.stat().st_size for f in files)
print()
print('files: %d  total: %.1f KB (%.2f MB)' % (len(files), total/1024, total/1024/1024))
