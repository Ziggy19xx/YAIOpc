# Third-party notices — FramePilot combined edition

The Python GUI libraries (PySide6, Qt, pyqtgraph, NumPy, Dear PyGui and Shiboken)
are not included or loaded. Monitoring libraries below came from the user-supplied
`pc_hardware_monitor.rar`; they are vendored without using pip. Exact files and
SHA-256 values are recorded in the source package's `vendor-manifest.json`.
Original distribution metadata and license files remain with each installed library.

| Component | Version / source | License location in application folder |
|---|---|---|
| CPython x64 | 3.13.16, official python.org embeddable ZIP | monitor/runtime/LICENSE.txt |
| psutil | 7.2.2 | monitor/runtime/Lib/site-packages/psutil-7.2.2.dist-info/LICENSE |
| WMI | 1.5.1 | monitor/runtime/Lib/site-packages/WMI-1.5.1.dist-info |
| pywin32 | 312 | monitor/runtime/Lib/site-packages/pywin32-312.dist-info |
| pythonnet | 3.2.1, optional sensor bridge | monitor/runtime/Lib/site-packages/pythonnet-3.2.1.dist-info/licenses |
| clr_loader | 0.3.1 | monitor/runtime/Lib/site-packages/clr_loader-0.3.1.dist-info |
| cffi | 2.1.1 | monitor/runtime/Lib/site-packages/cffi-2.1.1.dist-info/licenses |
| pycparser | 3.11 | monitor/runtime/Lib/site-packages/pycparser-3.11.dist-info |
| eDirStat storage helper | supplied 2.2.0 build, upstream revision 777a58e678ed1ed1c7eaec8c28725908a03648a9 | monitor/native/LICENSE (MIT); README.md |
| GPU and motherboard models | supplied GLB assets | models/ATTRIBUTION.md (CC BY 4.0) |

CPython was downloaded from:
https://www.python.org/ftp/python/3.13.16/python-3.13.16-embed-amd64.zip
SHA-256: 97DAE5274CC54867065E8D5A3226E48C35017ED332A0FDB0E27D5B5821961297
The extracted python.exe Authenticode signature was verified as valid, signed by
the Python Software Foundation. Recorded hashes pin the supplied inputs; they do
not independently authenticate the libraries originally present in the user's archive.

Monitoring source under `monitor/hardware` was supplied by the user and adapted for
FramePilot. The original Python GUI and provider source is retained only in the
source package under `integration-source/pc_hardware_monitor`.

Model credits: “GeForce RTX 3080 Graphics Card” by _surovic_, and
“MSI B450 TOMAHAWK MAX” by shig0. Original asset links and license links are in
models/ATTRIBUTION.md. FramePilot renders them using a new native WPF viewer with
simplified material lighting; no claim is made that either model is installed hardware.

LibreHardwareMonitor and its hardware-access drivers are not distributed. NVIDIA's
utility is not distributed; monitoring uses a utility already installed with the driver.
Microsoft .NET Framework and Windows are platform prerequisites, not bundled runtimes.


### WMI MIT license

Copyright Tim Golden 2003 - 2015.
The original module names the MIT license; its standard permission text is reproduced here.

Permission is hereby granted, free of charge, to any person obtaining a copy of this
software and associated documentation files (the "Software"), to deal in the Software
without restriction, including without limitation the rights to use, copy, modify,
merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
