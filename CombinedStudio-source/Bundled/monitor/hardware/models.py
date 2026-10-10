"""Normalized data contracts used by providers and the UI."""

from dataclasses import asdict, dataclass, field
from typing import Any


@dataclass
class CpuInfo:
    name: str | None = None
    physical_cores: int | None = None
    logical_cores: int | None = None


@dataclass
class GpuInfo:
    name: str | None = None
    driver_version: str | None = None
    driver_provider: str | None = None
    driver_date: str | None = None
    vram_bytes: int | None = None
    pnp_id: str | None = None
    problem_code: int | None = None


@dataclass
class MemoryInfo:
    total_bytes: int | None = None
    modules: list[dict[str, Any]] = field(default_factory=list)


@dataclass
class StorageDevice:
    model: str | None = None
    size_bytes: int | None = None
    interface: str | None = None
    media_type: str | None = None
    status: str | None = None


@dataclass
class MotherboardInfo:
    manufacturer: str | None = None
    model: str | None = None
    product: str | None = None
    bios_vendor: str | None = None
    bios_version: str | None = None
    bios_date: str | None = None


@dataclass
class DeviceDriver:
    name: str | None = None
    device_class: str | None = None
    hardware_ids: list[str] = field(default_factory=list)
    manufacturer: str | None = None
    driver_version: str | None = None
    driver_date: str | None = None
    problem_code: int | None = None


@dataclass
class SystemInventory:
    hostname: str | None = None
    os_name: str | None = None
    os_version: str | None = None
    uptime_seconds: float | None = None
    cpu: CpuInfo = field(default_factory=CpuInfo)
    gpu_adapters: list[GpuInfo] = field(default_factory=list)
    memory: MemoryInfo = field(default_factory=MemoryInfo)
    motherboard: MotherboardInfo = field(default_factory=MotherboardInfo)
    storage_devices: list[StorageDevice] = field(default_factory=list)
    drivers: list[DeviceDriver] = field(default_factory=list)
    provider_errors: dict[str, str] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


@dataclass
class LiveMetrics:
    cpu_percent: float | None = None
    per_core_percent: list[float] = field(default_factory=list)
    cpu_frequency_mhz: float | None = None
    memory_total_bytes: int | None = None
    memory_used_bytes: int | None = None
    memory_available_bytes: int | None = None
    memory_percent: float | None = None
    volumes: list[dict[str, Any]] = field(default_factory=list)
    network_sent_bytes: int | None = None
    network_received_bytes: int | None = None
    uptime_seconds: float | None = None
    gpu_adapters: list[dict[str, Any]] = field(default_factory=list)
    sensors: list[dict[str, Any]] = field(default_factory=list)
    errors: dict[str, str] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)
