"""Buffer-only VoxShift adapter. No audio devices, GUI, drivers or network."""
import json
from pathlib import Path
import struct
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / 'packages'))
sys.path.insert(0, str(ROOT))
import numpy as np
import dsp


class Engine:
    def __init__(self, rate, params):
        self.gate = dsp.NoiseGate(rate)
        self.shifter = dsp.PitchFormantShifter(samplerate=rate)
        self.eq = dsp.EQ3(rate)
        self.dist = dsp.Distortion()
        self.crush = dsp.Bitcrusher()
        self.ring = dsp.RingModulator(rate)
        self.trem = dsp.Tremolo(rate)
        self.vib = dsp.Vibrato(rate)
        self.echo = dsp.Echo(rate)
        self.reverb = dsp.Reverb(rate)
        # Upstream uses fixed 44.1 kHz reverb delays. Scale them for this pipeline.
        self.reverb.COMBS = [max(129, round(x * rate / 44100)) for x in dsp.Reverb.COMBS]
        self.reverb.ALLPASS = [max(129, round(x * rate / 44100)) for x in dsp.Reverb.ALLPASS]
        self.chain = [self.gate, self.shifter, self.eq, self.dist, self.crush,
                      self.ring, self.trem, self.vib, self.echo, self.reverb]
        self.gate.threshold = params['gate_threshold']
        self.shifter.pitch = params['pitch']
        self.shifter.formant = params['formant']
        self.shifter.preserve = params['preserve']
        for prefix in ('eq', 'dist', 'crush', 'ring', 'trem', 'vib', 'echo', 'reverb'):
            effect = getattr(self, prefix)
            for key, value in params.items():
                if key.startswith(prefix + '_'):
                    setattr(effect, key[len(prefix) + 1:], value)
        self.gain = params['gain']
        for effect in self.chain:
            effect.reset()

    def process(self, samples):
        result = np.empty(len(samples), dtype='<f4')
        # Reverb requires blocks smaller than its shortest delay; do not feed
        # arbitrary WASAPI buffer lengths directly into the upstream chain.
        for start in range(0, len(samples), 128):
            block = samples[start:start + 128].astype(np.float64)
            for effect in self.chain:
                block = effect.process(block)
            result[start:start + len(block)] = np.clip(np.nan_to_num(block * self.gain), -1, 1)
        return result


def exact(stream, size):
    data = bytearray()
    while len(data) < size:
        part = stream.read(size - len(data))
        if not part:
            raise EOFError('Truncated VoxShift request')
        data.extend(part)
    return data


def main():
    src, dst = sys.stdin.buffer, sys.stdout.buffer
    dst.write(b'VXS1'); dst.flush()
    engine, previous = None, None
    while True:
        header = src.read(16)
        if not header:
            return
        header += exact(src, 16 - len(header))
        rate, count, length, revision = struct.unpack('<IIII', header)
        if not 8000 <= rate <= 192000 or not 1 <= count <= 16384 or length > 16384:
            raise ValueError('Invalid VoxShift buffer dimensions')
        settings = bytes(exact(src, length))
        if previous != (rate, settings, revision):
            engine = Engine(rate, json.loads(settings))
            previous = (rate, settings, revision)
        samples = np.frombuffer(exact(src, count * 4), dtype='<f4')
        dst.write(engine.process(samples).tobytes()); dst.flush()


if __name__ == '__main__':
    main()
