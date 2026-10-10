"""Offline protocol checks; subprocess inputs are generated fixtures, never recordings."""
import array
from pathlib import Path
import struct
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parent
WORKER = ROOT / 'speech_worker.py'
LIBRARY = ROOT.parent / 'rnnoise.dll'

class ProtocolTests(unittest.TestCase):
    def run_worker(self, payload, mode='Strong'):
        return subprocess.run([sys.executable, '-I', str(WORKER), mode, str(LIBRARY)], input=payload, capture_output=True, timeout=10)

    def test_truncated_header(self):
        result = self.run_worker(b'VVF')
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, b'')

    def test_unknown_version(self):
        result = self.run_worker(struct.pack('<4sI', b'BAD1', 0))
        self.assertNotEqual(result.returncode, 0)

    def test_oversized_snapshot(self):
        result = self.run_worker(struct.pack('<4sI', b'VVF1', 48000 * 120 + 1))
        self.assertNotEqual(result.returncode, 0)

    def test_truncated_samples(self):
        result = self.run_worker(struct.pack('<4sI', b'VVF1', 1))
        self.assertNotEqual(result.returncode, 0)

    def test_unknown_mode(self):
        result = self.run_worker(struct.pack('<4sI', b'VVF1', 0), 'unexpected')
        self.assertNotEqual(result.returncode, 0)

    def test_silent_partial_frame(self):
        samples = array.array('f', [0.0]) * 481
        result = self.run_worker(struct.pack('<4sI', b'VVF1', len(samples)) + samples.tobytes())
        self.assertEqual(result.returncode, 0, result.stderr.decode())
        self.assertEqual(struct.unpack('<4sIII', result.stdout[:16]), (b'VVR1', 481, 0, 2))
        output = array.array('f')
        output.frombytes(result.stdout[16:])
        self.assertEqual(len(output), 481)
        self.assertTrue(all(abs(value) < 1e-8 for value in output))

if __name__ == '__main__':
    unittest.main()
