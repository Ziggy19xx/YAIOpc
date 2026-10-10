"""FramePilot private monitor protocol v1. No GUI, sockets, downloads or tuning."""
import sys
import os
import json
import struct
import math
import time
from datetime import datetime, timezone
from pathlib import Path

# The app-local runtime uses an isolated ._pth; never load a global site installation.
ROOT = Path(__file__).resolve().parent
SITE = ROOT / 'runtime' / 'Lib' / 'site-packages'
for folder in (SITE, SITE / 'win32', SITE / 'win32' / 'lib'):
    sys.path.insert(0, str(folder))
DLL_HANDLES = []
for folder in (SITE / 'pywin32_system32', SITE / 'win32'):
    if folder.is_dir():
        DLL_HANDLES.append(os.add_dll_directory(str(folder)))
sys.path.insert(0, str(ROOT))
MAX_FRAME = 4 * 1024 * 1024

def clean(value):
    if isinstance(value, float) and not math.isfinite(value): return None
    if isinstance(value, dict): return {str(k): clean(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)): return [clean(v) for v in value]
    return value

def read_exact(stream, count):
    parts = bytearray()
    while len(parts) < count:
        data = stream.read(count - len(parts))
        if not data: raise EOFError()
        parts.extend(data)
    return bytes(parts)

def read_frame(stream):
    size = struct.unpack('<I', read_exact(stream, 4))[0]
    if not 0 < size <= MAX_FRAME: raise ValueError('Invalid frame length')
    return json.loads(read_exact(stream, size).decode('utf-8'))

def write_frame(stream, payload):
    data = json.dumps(clean(payload), ensure_ascii=True, allow_nan=False, separators=(',', ':')).encode('utf-8')
    if len(data) > MAX_FRAME: raise ValueError('Response exceeds frame limit')
    stream.write(struct.pack('<I', len(data)) + data)
    stream.flush()

class NoSensors:
    error = 'Optional hardware sensors are off. Select a trusted LibreHardwareMonitor DLL in monitoring settings to enable them.'
    def poll(self): return []
    def close(self): pass

class Monitor:
    def __init__(self):
        self.service = None
        self.primed = False
    def open(self, sensor_path):
        import psutil
        from hardware.service import HardwareService
        from hardware.sensors import LibreHardwareMonitorProvider
        self.close()
        sensor = LibreHardwareMonitorProvider(sensor_path) if sensor_path else NoSensors()
        self.service = HardwareService(sensor_provider=sensor)
        psutil.cpu_percent(interval=None)
        psutil.cpu_percent(interval=None, percpu=True)
        self.primed = False
        self.started = time.monotonic()
        return {'python': sys.version.split()[0], 'architecture_bits': struct.calcsize('P') * 8, 'provider': 'PC Hardware Monitor'}
    def poll(self):
        if self.service is None: raise RuntimeError('Configure the monitor first')
        data = self.service.poll().to_dict()
        # A first nonblocking CPU counter is a baseline, not a real zero sample.
        if not self.primed and time.monotonic() - self.started < 0.2:
            data['cpu_percent'] = None
            data['per_core_percent'] = []
            data['errors']['cpu'] = 'Warming up CPU counters'
        self.primed = True
        return data
    def close(self):
        if self.service is not None: self.service.close()
        self.service = None

def main():
    monitor = Monitor()
    from storage import StorageScanner
    scanner = StorageScanner()
    input_stream, output_stream = sys.stdin.buffer, sys.stdout.buffer
    # Providers may print diagnostics. Keep stdout exclusively framed protocol data.
    sys.stdout = sys.stderr
    negotiated = False
    try:
        while True:
            request = read_frame(input_stream)
            request_id = request.get('id')
            try:
                if request.get('version') != 1: raise ValueError('Unsupported protocol version')
                operation = request.get('operation')
                if operation == 'hello':
                    result = {'protocol': 1, 'architecture_bits': struct.calcsize('P') * 8}
                    negotiated = True
                elif not negotiated: raise ValueError('Startup negotiation required')
                elif operation == 'configure': result = monitor.open(request.get('sensor_path') or '')
                elif operation == 'poll': result = monitor.poll()
                elif operation == 'inventory':
                    if monitor.service is None: raise ValueError('Configure the monitor first')
                    result = monitor.service.inventory().to_dict()
                elif operation == 'scan_start': result = scanner.start(request.get('root', ''))
                elif operation == 'scan_status': result = scanner.status()
                elif operation == 'scan_cancel': result = scanner.cancel()
                elif operation == 'scan_children': result = scanner.children(request.get('parent', 0), request.get('offset', 0))
                elif operation == 'shutdown':
                    scanner.close()
                    monitor.close()
                    write_frame(output_stream, {'version': 1, 'id': request_id, 'ok': True, 'utc': datetime.now(timezone.utc).isoformat(), 'data': {}})
                    return
                else: raise ValueError('Unknown operation')
                response = {'version': 1, 'id': request_id, 'ok': True, 'utc': datetime.now(timezone.utc).isoformat(), 'data': result}
            except Exception as error:
                response = {'version': 1, 'id': request_id, 'ok': False, 'utc': datetime.now(timezone.utc).isoformat(), 'error': type(error).__name__ + ': ' + str(error)}
            write_frame(output_stream, response)
    except (EOFError, BrokenPipeError): pass
    finally:
        scanner.close()
        monitor.close()

if __name__ == '__main__': main()

