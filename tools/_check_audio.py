import os, wave

root = 'output/audio'
results = []
for dirpath, dirs, files in os.walk(root):
    for fn in files:
        if not fn.endswith('.wav'):
            continue
        path = os.path.join(dirpath, fn)
        rel = os.path.relpath(path, root)
        size = os.path.getsize(path)
        try:
            with wave.open(path, 'rb') as w:
                chans = w.getnchannels()
                rate = w.getframerate()
                sampwidth = w.getsampwidth()
                frames = w.getnframes()
                dur = frames / float(rate)
                results.append((rel, size, dur, chans, rate, sampwidth * 8))
        except Exception as e:
            results.append((rel, size, None, None, None, str(e)))

results.sort(key=lambda r: r[0])
print('{:<48} {:>8} {:>7} {:>3} {:>6} {:>4}'.format('FILE', 'BYTES', 'DUR', 'CH', 'RATE', 'BIT'))
print('-' * 82)
for rel, size, dur, ch, rate, bit in results:
    durstr = '{:.2f}s'.format(dur) if dur else 'ERR'
    chstr = str(ch) if ch else '?'
    ratestr = str(rate) if rate else '?'
    bitstr = str(bit) if bit else '?'
    print('{:<48} {:>8} {:>7} {:>3} {:>6} {:>4}'.format(rel, size, durstr, chstr, ratestr, bitstr))
print('\nTOTAL: {} files'.format(len(results)))
