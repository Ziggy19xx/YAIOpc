"""Optional read-only NVIDIA telemetry from the installed nvidia-smi utility."""

from __future__ import annotations

import csv
import shutil
import subprocess
from typing import Any


FIELDS = ("name", "driver_version", "memory.total", "memory.used", "utilization.gpu",
          "temperature.gpu", "clocks.current.graphics", "power.draw", "pci.bus_id")


def _number(value: str, scale: float = 1.0) -> float | None:
    value = value.strip()
    if not value or value.upper() in {"N/A", "NA", "NOT SUPPORTED", "[N/A]"}: return None
    try: return float(value) * scale
    except ValueError: return None


class NvidiaSmiProvider:
    """Fetch one inexpensive snapshot per poll; missing/unsupported values stay None."""

    def __init__(self, executable: str | None = None):
        self.executable = executable or shutil.which("nvidia-smi")
        self.error: str | None = None

    @staticmethod
    def parse_csv(output: str) -> list[dict[str, Any]]:
        adapters = []
        for row in csv.reader(output.splitlines(), skipinitialspace=True):
            if len(row) < len(FIELDS): continue
            values = [value.strip() for value in row[:len(FIELDS)]]
            adapters.append({
                "name": values[0] or None,
                "driver_version": values[1] or None,
                "vram_total_bytes": _number(values[2], 1024**2),
                "vram_used_bytes": _number(values[3], 1024**2),
                "utilization_percent": _number(values[4]),
                "temperature_c": _number(values[5]),
                "clock_mhz": _number(values[6]),
                "power_watts": _number(values[7]),
                "pci_bus_id": values[8] or None,
                "provider": "NVIDIA nvidia-smi",
            })
        return adapters

    def poll(self) -> list[dict[str, Any]]:
        if not self.executable:
            self.error = "NVIDIA telemetry unavailable (nvidia-smi not found)"
            return []
        try:
            result = subprocess.run([self.executable, f"--query-gpu={','.join(FIELDS)}", "--format=csv,noheader,nounits"],
                capture_output=True, text=True, timeout=3, check=False, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            if result.returncode:
                self.error = result.stderr.strip() or f"nvidia-smi exited with status {result.returncode}"
                return []
            self.error = None
            return self.parse_csv(result.stdout)
        except (OSError, subprocess.SubprocessError) as exc:
            self.error = f"nvidia-smi query failed: {exc}"
            return []
