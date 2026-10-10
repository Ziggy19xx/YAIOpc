"""Read-only eDirStat adapter with a disk-backed, paged index."""
import os
import sqlite3
import struct
import subprocess
import tempfile
import threading
import shutil
import time
from pathlib import Path

class StorageScanner:
    def __init__(self):
        self.work = None
        self.thread = None
        self.process = None
        self.root = None
        self.state = 'idle'
        self.message = 'Choose a folder or drive to scan.'
        self.count = 0
        self.cancelled = threading.Event()
    def start(self, root):
        if self.thread and self.thread.is_alive(): raise RuntimeError('A storage scan is already running')
        self.close()
        path = Path(root)
        if not path.is_absolute() or not path.is_dir() or path.is_symlink(): raise ValueError('Choose an existing local folder or drive')
        self.root = str(path.resolve())
        cache = Path(os.environ.get('LOCALAPPDATA', tempfile.gettempdir())) / 'FramePilot' / 'monitor-cache'
        cache.mkdir(parents=True, exist_ok=True)
        self.work = Path(tempfile.mkdtemp(prefix='scan-', dir=cache))
        self.cancelled.clear()
        self.count = 0
        self.state = 'scanning'
        self.message = 'Scanning accessible folders; protected locations may be skipped.'
        self.thread = threading.Thread(target=self._scan, daemon=True)
        self.thread.start()
        return self.status()
    def _scan(self):
        try:
            helper = Path(__file__).parent / 'native' / 'storage_scan.exe'
            output, progress, cancel = (self.work / n for n in ('scan.bin', 'progress.txt', 'cancel.request'))
            with subprocess.Popen([str(helper), self.root, str(output), str(progress), str(cancel)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=0x08000000) as process:
                self.process = process
                while process.poll() is None:
                    if self.cancelled.is_set():
                        cancel.write_text('cancel', encoding='utf-8')
                        try: process.wait(timeout=2)
                        except subprocess.TimeoutExpired: process.kill()
                        raise RuntimeError('Scan cancelled')
                    try: self.message = progress.read_text(encoding='utf-8')[:1000]
                    except OSError: pass
                    time.sleep(0.25)
                if process.returncode:
                    error = output.with_suffix('.error.txt')
                    raise RuntimeError(error.read_text(encoding='utf-8')[:1000] if error.exists() else 'Storage scanner failed')
            self.process = None
            self.state = 'indexing'
            record = struct.Struct('<IIQBI')
            with sqlite3.connect(self.work / 'scan.sqlite') as db:
                db.execute('CREATE TABLE nodes (id INTEGER PRIMARY KEY, parent INTEGER, name TEXT, size INTEGER, is_directory INTEGER)')
                batch = []
                with output.open('rb') as stream:
                    while True:
                        if self.cancelled.is_set(): raise RuntimeError('Scan cancelled')
                        header = stream.read(record.size)
                        if not header: break
                        if len(header) != record.size: raise ValueError('Truncated storage record')
                        node, parent, size, directory, length = record.unpack(header)
                        if length > 32768: raise ValueError('Storage filename exceeds limit')
                        name = stream.read(length)
                        if len(name) != length: raise ValueError('Truncated storage filename')
                        batch.append((node, None if parent == 0xffffffff else parent, name.decode('utf-8', errors='replace'), size, directory))
                        if len(batch) >= 10000:
                            db.executemany('INSERT INTO nodes VALUES (?,?,?,?,?)', batch)
                            self.count += len(batch); batch.clear()
                    if batch:
                        db.executemany('INSERT INTO nodes VALUES (?,?,?,?,?)', batch)
                        self.count += len(batch)
                db.execute('CREATE INDEX nodes_by_parent ON nodes(parent, size DESC)')
                db.commit()
            output.unlink()
            self.state = 'ready'
            self.message = f'{self.count:,} entries. Sizes reflect accessible data at scan time; rescan after changes.'
        except Exception as exc:
            self.state = 'cancelled' if self.cancelled.is_set() else 'failed'
            self.message = str(exc)
        finally: self.process = None
    def status(self):
        return {'state': self.state, 'message': self.message, 'count': self.count, 'root': self.root}
    def children(self, parent=0, offset=0):
        if self.state != 'ready': raise RuntimeError('Storage scan is not ready')
        if not isinstance(parent, int) or not isinstance(offset, int) or parent < 0 or offset < 0: raise ValueError('Invalid page')
        with sqlite3.connect(self.work / 'scan.sqlite') as db:
            result = db.execute('SELECT id,name,size,is_directory FROM nodes WHERE parent=? ORDER BY size DESC,id LIMIT 501 OFFSET ?', (parent, offset)).fetchall()
        return {'rows': [{'id': r[0], 'name': r[1], 'size_bytes': r[2], 'is_directory': bool(r[3])} for r in result[:500]], 'has_more': len(result) > 500}
    def cancel(self):
        self.cancelled.set()
        return self.status()
    def close(self):
        self.cancelled.set()
        if self.thread and self.thread.is_alive(): self.thread.join(4)
        if self.thread and self.thread.is_alive():
            if self.process:
                try: self.process.kill()
                except OSError: pass
            self.thread.join(2)
        if self.work and (not self.thread or not self.thread.is_alive()): shutil.rmtree(self.work, ignore_errors=True)
        self.work = None
        self.thread = None
