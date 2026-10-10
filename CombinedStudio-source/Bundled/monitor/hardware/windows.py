"""Read-only Windows WMI/SMBIOS inventory providers."""

from __future__ import annotations

import platform
import socket
import time
from typing import Any

import psutil

from .models import CpuInfo, DeviceDriver, GpuInfo, MemoryInfo, MotherboardInfo, StorageDevice, SystemInventory


def _value(obj: Any, name: str, default: Any = None) -> Any:
    value = getattr(obj, name, default)
    return value if value not in ("", "To be filled by O.E.M.", "Default string") else default


class WindowsInventoryProvider:
    """Collect static Windows inventory; each category fails independently."""

    def collect(self) -> SystemInventory:
        inv = SystemInventory(hostname=socket.gethostname(), os_name=platform.system(),
                              os_version=platform.version(), uptime_seconds=max(0.0, time.time()-psutil.boot_time()))
        inv.cpu.physical_cores = psutil.cpu_count(logical=False)
        inv.cpu.logical_cores = psutil.cpu_count(logical=True)
        inv.memory.total_bytes = psutil.virtual_memory().total
        self._collect_registry_identity(inv)
        self._collect_display_devices(inv)
        try:
            import wmi
            c = wmi.WMI()
            try:
                cpus = c.Win32_Processor()
                inv.cpu.name = _value(cpus[0], "Name") if cpus else None
                if cpus:
                    inv.cpu.physical_cores = _value(cpus[0], "NumberOfCores", inv.cpu.physical_cores)
                    inv.cpu.logical_cores = _value(cpus[0], "NumberOfLogicalProcessors", inv.cpu.logical_cores)
            except Exception as exc:
                inv.provider_errors["cpu_identity"] = str(exc)
            try:
                boards = c.Win32_BaseBoard()
                bios = c.Win32_BIOS()
                board = boards[0] if boards else None
                fw = bios[0] if bios else None
                inv.motherboard = MotherboardInfo(
                    manufacturer=_value(board, "Manufacturer"), model=_value(board, "Model"),
                    product=_value(board, "Product"), bios_vendor=_value(fw, "Manufacturer"),
                    bios_version="; ".join(x for x in (_value(fw, "SMBIOSBIOSVersion"), _value(fw, "Version")) if x) or None,
                    bios_date=_value(fw, "ReleaseDate"))
            except Exception as exc:
                inv.provider_errors["motherboard"] = str(exc)
            try:
                inv.memory.modules = [{"capacity_bytes": _value(x, "Capacity"), "manufacturer": _value(x, "Manufacturer"),
                    "part_number": _value(x, "PartNumber"), "serial_number": _value(x, "SerialNumber"),
                    "speed_mhz": _value(x, "Speed"), "configured_speed_mhz": _value(x, "ConfiguredClockSpeed"),
                    "device_locator": _value(x, "DeviceLocator"), "form_factor": _value(x, "FormFactor")}
                    for x in c.Win32_PhysicalMemory()]
            except Exception as exc:
                inv.provider_errors["memory_modules"] = str(exc)
            try:
                for x in c.Win32_VideoController():
                    ram = _value(x, "AdapterRAM")
                    # Win32_VideoController.AdapterRAM is a 32-bit field and can overflow for large GPUs.
                    inv.gpu_adapters.append(GpuInfo(name=_value(x, "Name"), driver_version=_value(x, "DriverVersion"),
                        driver_provider=_value(x, "DriverProviderName"), driver_date=_value(x, "DriverDate"),
                        vram_bytes=int(ram) if ram and int(ram) > 0 else None,
                        pnp_id=_value(x, "PNPDeviceID")))
            except Exception as exc:
                inv.provider_errors["gpu"] = str(exc)
            try:
                for x in c.Win32_DiskDrive():
                    inv.storage_devices.append(StorageDevice(model=_value(x,"Model"), size_bytes=_value(x,"Size"),
                        interface=_value(x,"InterfaceType"), media_type=_value(x,"MediaType"), status=_value(x,"Status")))
            except Exception as exc:
                inv.provider_errors["physical_storage"] = str(exc)
            try:
                for x in c.Win32_PnPSignedDriver():
                    raw_ids = _value(x, "HardwareID", []) or []
                    if isinstance(raw_ids, str): raw_ids = [raw_ids]
                    inv.drivers.append(DeviceDriver(name=_value(x,"DeviceName"), device_class=_value(x,"DeviceClass"),
                        hardware_ids=list(raw_ids), manufacturer=_value(x,"Manufacturer"),
                        driver_version=_value(x,"DriverVersion"), driver_date=_value(x,"DriverDate")))
            except Exception as exc:
                inv.provider_errors["drivers"] = str(exc)
            try:
                devices = c.Win32_PnPEntity()
                for driver in inv.drivers:
                    for dev in devices:
                        ids = getattr(dev, "HardwareID", []) or []
                        if any(str(hid).casefold() in {str(item).casefold() for item in driver.hardware_ids} for hid in ids):
                            driver.problem_code = int(dev.ConfigManagerErrorCode); break
            except Exception as exc:
                inv.provider_errors["device_status"] = str(exc)
        except Exception as exc:
            inv.provider_errors["wmi"] = str(exc)
        return inv

    @staticmethod
    def _collect_registry_identity(inv: SystemInventory) -> None:
        """Read Windows' local BIOS identity strings without requiring WMI."""
        if __import__("sys").platform != "win32": return
        try:
            import winreg
            path = r"HARDWARE\DESCRIPTION\System\BIOS"
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, path, 0, winreg.KEY_READ) as key:
                values = {}
                for name in ("SystemManufacturer", "SystemProductName", "BaseBoardManufacturer", "BaseBoardProduct", "BIOSVendor", "BIOSVersion", "BIOSReleaseDate"):
                    try: values[name] = winreg.QueryValueEx(key, name)[0]
                    except OSError: values[name] = None
                inv.motherboard.manufacturer = inv.motherboard.manufacturer or values["BaseBoardManufacturer"] or values["SystemManufacturer"]
                inv.motherboard.model = inv.motherboard.model or values["BaseBoardProduct"] or values["SystemProductName"]
                inv.motherboard.product = inv.motherboard.product or values["BaseBoardProduct"]
                inv.motherboard.bios_vendor = inv.motherboard.bios_vendor or values["BIOSVendor"]
                inv.motherboard.bios_version = inv.motherboard.bios_version or values["BIOSVersion"]
                inv.motherboard.bios_date = inv.motherboard.bios_date or values["BIOSReleaseDate"]
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"HARDWARE\DESCRIPTION\System\CentralProcessor\0", 0, winreg.KEY_READ) as key:
                try: inv.cpu.name = inv.cpu.name or winreg.QueryValueEx(key, "ProcessorNameString")[0].strip()
                except OSError: pass
        except Exception as exc:
            inv.provider_errors["registry_identity"] = str(exc)

    @staticmethod
    def _collect_display_devices(inv: SystemInventory) -> None:
        """Enumerate installed display adapters using EnumDisplayDevicesW."""
        if __import__("sys").platform != "win32": return
        try:
            import ctypes
            from ctypes import wintypes
            class DISPLAY_DEVICEW(ctypes.Structure):
                _fields_ = [("cb", wintypes.DWORD), ("DeviceName", wintypes.WCHAR*32), ("DeviceString", wintypes.WCHAR*128),
                    ("StateFlags", wintypes.DWORD), ("DeviceID", wintypes.WCHAR*128), ("DeviceKey", wintypes.WCHAR*128)]
            enum = ctypes.windll.user32.EnumDisplayDevicesW
            enum.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, ctypes.POINTER(DISPLAY_DEVICEW), wintypes.DWORD]
            enum.restype = wintypes.BOOL
            index = 0
            while True:
                device = DISPLAY_DEVICEW(); device.cb = ctypes.sizeof(device)
                if not enum(None, index, ctypes.byref(device), 0): break
                if device.DeviceString and device.StateFlags & 1:
                    if not any(g.name == device.DeviceString for g in inv.gpu_adapters):
                        inv.gpu_adapters.append(GpuInfo(name=device.DeviceString, pnp_id=device.DeviceID or None))
                index += 1
        except Exception as exc:
            inv.provider_errors["display_devices"] = str(exc)
