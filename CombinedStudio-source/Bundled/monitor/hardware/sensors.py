"""Optional LibreHardwareMonitor bridge via pythonnet."""

from __future__ import annotations

import os
import re
import unicodedata
from pathlib import Path
from typing import Any


def display_hardware_name(hardware_type: str, hardware_name: str) -> str:
    """Return a readable UI label for DIMM names with corrupt SPD text."""
    name = str(hardware_name or "Unknown hardware").strip()
    if "memory" not in str(hardware_type or "").casefold() or "team group inc" not in name.casefold():
        return name
    slot_match = re.search(r"\(#(\d+)\)\s*$", name)
    slot = slot_match.group(1) if slot_match else None
    description = name.split(" - ", 1)[1] if " - " in name else ""
    if slot:
        description = re.sub(r"\s*\(#\d+\)\s*$", "", description)
    description = description.strip()
    # Module vendor/product strings come from SPD EEPROM text. Avoid rendering
    # mojibake or control/private-use characters as part of the hardware label.
    corrupt = any(not char.isascii() or unicodedata.category(char).startswith("C") for char in description)
    if corrupt:
        return f"Team Group DIMM #{slot}" if slot else "Team Group DIMM"
    if description:
        return f"Team Group {description}" + (f" (#{slot})" if slot else "")
    return f"Team Group DIMM #{slot}" if slot else "Team Group DIMM"


class LibreHardwareMonitorProvider:
    """Loads a user-provided LHM assembly, polls it, and closes the computer cleanly."""

    def __init__(self, assembly_path: str | None = None):
        self.assembly_path = assembly_path or os.environ.get("LIBREHARDWAREMONITOR_DLL")
        self.computer = None
        self.error: str | None = None
        self._open()

    def _open(self) -> None:
        try:
            if not self.assembly_path or not Path(self.assembly_path).is_file():
                self.error = "LibreHardwareMonitorLib.dll not configured; set LIBREHARDWAREMONITOR_DLL"
                return
            import clr  # supplied by optional pythonnet dependency
            clr.AddReference(str(Path(self.assembly_path).resolve()))
            from LibreHardwareMonitor.Hardware import Computer
            self.computer = Computer()
            for name in ("IsCpuEnabled", "IsGpuEnabled", "IsMemoryEnabled", "IsMotherboardEnabled", "IsControllerEnabled", "IsStorageEnabled"):
                setattr(self.computer, name, True)
            self.computer.Open()
        except Exception as exc:
            self.error = f"LibreHardwareMonitor unavailable: {exc}"
            self.computer = None

    def poll(self) -> list[dict[str, Any]]:
        if self.computer is None: return []
        rows: list[dict[str, Any]] = []
        try:
            def visit(hardware: Any) -> None:
                try:
                    hardware.Update()
                    self._collect(hardware, rows)
                except Exception as exc:
                    self.error = f"Sensor update failed for {hardware.Name}: {exc}"
                try:
                    for sub in hardware.SubHardware: visit(sub)
                except Exception as exc:
                    self.error = f"Subhardware discovery failed for {hardware.Name}: {exc}"
            for hardware in self.computer.Hardware:
                visit(hardware)
            cpu_metrics = [r for r in rows if "cpu" in r["hardware_type"].casefold() and r["sensor_type"] in ("Temperature", "Clock", "Power")]
            if cpu_metrics and not any(r["value"] > 0 for r in cpu_metrics):
                self.error = "LibreHardwareMonitor found CPU sensors but returned zero for temperature, clock, and power readings. Check elevated sensor access and the LHM sensor driver (PawnIO)."
                # The low-level CPU outputs (including voltage) are not trustworthy if telemetry is blocked.
                # Keep OS-derived load sensors, but don't present zero/uninitialized hardware values.
                rows = [r for r in rows if not ("cpu" in r["hardware_type"].casefold() and r["sensor_type"] != "Load")]
            elif cpu_metrics:
                self.error = None
        except Exception as exc:
            self.error = f"Sensor polling failed: {exc}"
        return rows

    def _collect(self, hardware: Any, rows: list[dict[str, Any]]) -> None:
        for sensor in hardware.Sensors:
            try:
                value = sensor.Value
                if value is None: continue
                rows.append({"hardware": str(hardware.Name), "hardware_type": str(hardware.HardwareType),
                    "name": str(sensor.Name), "sensor_type": str(sensor.SensorType), "value": float(value),
                    "unit": {"Temperature":"°C", "Fan":"RPM", "Voltage":"V", "Power":"W", "Clock":"MHz", "Load":"%"}.get(str(sensor.SensorType), "")})
            except Exception as exc:
                self.error = f"Could not read sensor {sensor.Name}: {exc}"

    def close(self) -> None:
        if self.computer is not None:
            try: self.computer.Close()
            except Exception: pass
            self.computer = None
