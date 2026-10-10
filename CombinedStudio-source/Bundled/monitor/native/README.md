# eDirStat storage scanner

`storage_scan.exe` is a small, hidden-window helper built from the eDirStat 2.2.0 Rust scanner. It uses eDirStat's NTFS MFT fast path when launched with administrator rights and falls back to its parallel directory traversal when raw NTFS access is unavailable. The helper streams compact binary scan records; the PySide6 app imports them into a temporary SQLite index and displays folders lazily in the native Storage tab.

The helper source is in `storage_scan.rs`. The upstream eDirStat source used for this build is in `source/`; its upstream MIT license is included as `LICENSE`.

To rebuild on Windows with the Rust nightly toolchain and the Visual C++ build tools installed, from `source/` run:

```powershell
cargo +nightly build --release -p edirstat --bin storage_scan --locked
Copy-Item target\release\storage_scan.exe ..\storage_scan.exe
```

Upstream: [xangelix/edirstat](https://github.com/xangelix/edirstat), revision `777a58e678ed1ed1c7eaec8c28725908a03648a9`.
