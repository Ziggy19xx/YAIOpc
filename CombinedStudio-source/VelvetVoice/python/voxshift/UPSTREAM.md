# VoxShift integration

Source: https://github.com/why-xdd/voxshift (main downloaded 2026-10-10).
MIT license: see LICENSE. dsp.py is unchanged upstream code. presets.json contains DEFAULTS and PRESETS extracted with ast.literal_eval from voxshift.py, without importing its application or running its GUI.

The worker is this project's buffer-only adapter. C# retains microphone capture, Studio processing, routing, mixing, protection and settings. The adapter scales reverb delays with sample rate and processes at most 128 samples per DSP step because upstream delay effects assume blocks smaller than their internal delays. Revision changes reset DSP even when reselecting the same preset after bypass.

Dependencies: NumPy 2.2.6 and SciPy 1.15.3, CPython 3.13 Windows x64 wheels from PyPI. Bundled package licenses and native-library notices are retained in packages. Upstream test folders were omitted except numpy/_core/tests/_natype.py, which numpy.testing imports at runtime. No sounddevice, GUI, keyboard hooks, virtual driver installation, AI engines or downloads are part of this adapter.

Offline checks: run ../runtime/python.exe -I test_worker.py from this folder.

Upstream dsp.py SHA-256: 41eb50465e91b44f4d7bc49da1f9341bb6f182181cd75443af2b292f79eb98de
