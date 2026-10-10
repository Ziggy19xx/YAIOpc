"""Orchestrates static inventory and inexpensive one-second live polling."""

from __future__ import annotations

import time
from typing import Any

import psutil

from .models import LiveMetrics
from .gpu_nvidia import NvidiaSmiProvider
from .sensors import LibreHardwareMonitorProvider
from .windows import WindowsInventoryProvider


class HardwareService:
    def __init__(self, inventory_provider=None, sensor_provider=None, gpu_provider=None):
        self.inventory_provider = inventory_provider or WindowsInventoryProvider()
        self.sensor_provider = sensor_provider or LibreHardwareMonitorProvider()
        self.gpu_provider = gpu_provider or NvidiaSmiProvider()
        self._network_baseline = psutil.net_io_counters()

    def inventory(self):
        return self.inventory_provider.collect()

    def poll(self) -> LiveMetrics:
        metrics = LiveMetrics()
        try:
            metrics.cpu_percent = psutil.cpu_percent(interval=None)
            metrics.per_core_percent = psutil.cpu_percent(interval=None, percpu=True)
            freq = psutil.cpu_freq(); metrics.cpu_frequency_mhz = freq.current if freq else None
        except Exception as exc: metrics.errors["cpu"] = str(exc)
        try:
            mem = psutil.virtual_memory(); metrics.memory_total_bytes = mem.total
            metrics.memory_used_bytes = mem.used; metrics.memory_available_bytes = mem.available; metrics.memory_percent = mem.percent
        except Exception as exc: metrics.errors["memory"] = str(exc)
        try:
            for part in psutil.disk_partitions(all=False):
                try:
                    usage = psutil.disk_usage(part.mountpoint)
                    if usage.total: metrics.volumes.append({"name":part.device or part.mountpoint,"mountpoint":part.mountpoint,
                        "used_bytes":usage.used,"free_bytes":usage.free,"total_bytes":usage.total,"percent":usage.percent})
                except (PermissionError, OSError): continue
        except Exception as exc: metrics.errors["volumes"] = str(exc)
        try:
            current = psutil.net_io_counters()
            metrics.network_sent_bytes = current.bytes_sent; metrics.network_received_bytes = current.bytes_recv
            self._network_baseline = current
        except Exception as exc: metrics.errors["network"] = str(exc)
        try: metrics.uptime_seconds = max(0.0,time.time()-psutil.boot_time())
        except Exception as exc: metrics.errors["uptime"] = str(exc)
        try:
            metrics.gpu_adapters = self.gpu_provider.poll()
            gpu_error = getattr(self.gpu_provider, "error", None)
            if gpu_error: metrics.errors["gpu"] = gpu_error
        except Exception as exc: metrics.errors["gpu"] = str(exc)
        try:
            metrics.sensors = self.sensor_provider.poll()
            sensor_error = getattr(self.sensor_provider, "error", None)
            if sensor_error: metrics.errors["sensors"] = sensor_error
        except Exception as exc: metrics.errors["sensors"] = str(exc)
        return metrics

    def close(self):
        self.sensor_provider.close()
