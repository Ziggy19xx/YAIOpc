"""Offline DSP and persistent protocol checks; never opens audio devices."""
import json
from pathlib import Path
import subprocess
import struct
import sys
import time
sys.path.insert(0, str(Path(__file__).resolve().parent))
import worker
import numpy as np

data = json.loads((worker.ROOT / 'presets.json').read_text(encoding='utf-8'))
assert len(data['PRESETS']) == 46
for rate in (44100, 48000):
    for name, preset in data['PRESETS'].items():
        params = dict(data['DEFAULTS'], **preset['params'])
        engine = worker.Engine(rate, params)
        start = time.perf_counter()
        for count in (1, 127, 128, 129, 480, 512, 1920, 4096):
            signal = .03 * np.sin(2 * np.pi * 220 * np.arange(count) / rate)
            output = engine.process(signal)
            assert output.shape == signal.shape, name
            assert np.isfinite(output).all() and np.max(np.abs(output)) <= 1, name
        assert time.perf_counter() - start < .5, f'{name} too slow'
print('PASS all 46 presets at 44.1/48 kHz: variable buffers, finite bounded output')

for pitch in (-5, 7):
    params = dict(data['DEFAULTS'], pitch=pitch, preserve=False)
    engine = worker.Engine(48000, params)
    signal = .03 * np.sin(2 * np.pi * 440 * np.arange(48000) / 48000)
    output = engine.process(signal)[24000:]
    bins = np.abs(np.fft.rfft(output * np.hanning(len(output))))
    frequency = np.argmax(bins) * 48000 / len(output)
    target = 440 * 2 ** (pitch / 12)
    assert abs(frequency - target) < 5, (frequency, target)
print('PASS pitch frequency shifts')

process = subprocess.Popen([sys.executable, '-I', str(worker.ROOT / 'worker.py')], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
try:
    assert worker.exact(process.stdout, 4) == b'VXS1'
    for name in ('Woman', 'Robot', 'Hall', 'Normal'):
        config = json.dumps(dict(data['DEFAULTS'], **data['PRESETS'][name]['params'])).encode()
        signal = np.full(1920, .03, dtype='<f4')
        process.stdin.write(struct.pack('<IIII', 48000, len(signal), len(config), 1) + config + signal.tobytes())
        process.stdin.flush()
        result = np.frombuffer(worker.exact(process.stdout, len(signal) * 4), dtype='<f4')
        assert np.isfinite(result).all() and len(result) == len(signal)
    process.stdin.close()
    assert process.wait(timeout=5) == 0, process.stderr.read().decode()
finally:
    if process.poll() is None:
        process.kill()
print('PASS persistent worker framing, preset changes, shutdown')
