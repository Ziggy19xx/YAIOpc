"""Local saved-clip RNNoise worker; no devices, files, network or live audio capture."""
import array
import ctypes
import math
from pathlib import Path
import struct
import sys

FRAME = 480
LIMIT = 48000 * 120

def exact(stream, length):
    result = bytearray()
    while len(result) < length:
        piece = stream.read(length - len(result))
        if not piece:
            raise ValueError('Truncated clip input')
        result.extend(piece)
    return result

def clean(samples, mode, library):
    library.rnnoise_create.argtypes = [ctypes.c_void_p]
    library.rnnoise_create.restype = ctypes.c_void_p
    library.rnnoise_destroy.argtypes = [ctypes.c_void_p]
    library.rnnoise_process_frame.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_float), ctypes.POINTER(ctypes.c_float)]
    library.rnnoise_process_frame.restype = ctypes.c_float
    state = library.rnnoise_create(None)
    if not state:
        raise RuntimeError('Could not allocate RNNoise model state')
    frames = (len(samples) + FRAME - 1) // FRAME
    probabilities = []
    cleaned = array.array('f', [0.0]) * len(samples)
    input_buffer = (ctypes.c_float * FRAME)()
    output_buffer = (ctypes.c_float * FRAME)()
    try:
        for frame in range(frames + 1):
            start = frame * FRAME
            for index in range(FRAME):
                value = samples[start + index] if start + index < len(samples) else 0.0
                input_buffer[index] = max(-1.0, min(1.0, value)) * 32768.0 if math.isfinite(value) else 0.0
            probability = library.rnnoise_process_frame(state, output_buffer, input_buffer)
            if frame == 0:
                continue
            probabilities.append(max(0.0, min(1.0, probability)) if math.isfinite(probability) else 0.0)
            destination = (frame - 1) * FRAME
            for index in range(min(FRAME, len(samples) - destination)):
                value = output_buffer[index] / 32768.0
                cleaned[destination + index] = max(-1.0, min(1.0, value)) if math.isfinite(value) else 0.0
        detected = sum(value >= 0.45 for value in probabilities)
        if mode == 'Strong':
            speech = [False] * frames
            for frame, probability in enumerate(probabilities):
                if probability >= 0.45:
                    for neighbor in range(max(0, frame - 8), min(frames - 1, frame + 18) + 1):
                        speech[neighbor] = True
            envelope = 1.0 if speech and speech[0] else 0.01
            for index in range(len(cleaned)):
                target = 1.0 if speech[index // FRAME] else 0.01
                coefficient = math.exp(-1.0 / (48000 * (0.005 if target > envelope else 0.03)))
                envelope = target + (envelope - target) * coefficient
                cleaned[index] *= envelope
        elif mode == 'Natural':
            for index in range(len(cleaned)):
                value = samples[index] if math.isfinite(samples[index]) else 0.0
                cleaned[index] = cleaned[index] * 0.85 + max(-1.0, min(1.0, value)) * 0.15
        return cleaned, detected, frames
    finally:
        library.rnnoise_destroy(state)

def main():
    if len(sys.argv) != 3 or sys.argv[1] not in ('Natural', 'Strong', 'Voices only'):
        raise ValueError('Unknown speech-focus mode')
    stream = sys.stdin.buffer
    header = exact(stream, 8)
    magic, count = struct.unpack('<4sI', header)
    if magic != b'VVF1' or count > LIMIT:
        raise ValueError('Invalid protocol or clip size')
    samples = array.array('f')
    samples.frombytes(exact(stream, count * 4))
    if sys.byteorder != 'little':
        samples.byteswap()
    library_path = Path(sys.argv[2]).resolve(strict=True)
    expected = Path(__file__).resolve().parent.parent / 'rnnoise.dll'
    if library_path != expected.resolve(strict=True):
        raise ValueError('RNNoise library must belong to this application')
    cleaned, detected, frames = clean(samples, sys.argv[1], ctypes.CDLL(str(library_path)))
    if sys.byteorder != 'little':
        cleaned.byteswap()
    sys.stdout.buffer.write(struct.pack('<4sIII', b'VVR1', count, detected, frames))
    sys.stdout.buffer.write(cleaned.tobytes())
    sys.stdout.buffer.flush()

if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
