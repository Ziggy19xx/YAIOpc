# Motion clarity

The Motion Clarity page is integrated in FramePilot's main sidebar. It can also run alongside an existing studio instance with `FramePilot.exe --motion-clarity`.

The full-screen alien target uses the selected display's bounds and DPI, with dark, gray and light backgrounds. Speeds are expressed in physical pixels per second. Animation cadence and frame intervals describe WPF rendering, not measured panel response or input latency. Esc exits, Space pauses, Left/Right change speed, and M toggles a mouse-follow target for visual comparison.

Windows refresh controls use driver-enumerated modes at the current resolution, color depth and scan type. The driver validates the choice before applying it. Preview changes do not persist. Keep persists the selected refresh; otherwise the page restores the original mode after 15 seconds or on normal close. Forced termination or a GPU/monitor failure can prevent in-process recovery; Windows display settings remain the fallback. No custom timings, registry latency hacks or undocumented monitor writes are used.

Ten manual comparisons cover baseline, three overdrive settings, four MBR strengths, MBR Sync and another refresh rate. The user records perceived clarity, halos and brightness. Observations are saved to `%LOCALAPPDATA%/FramePilot/MotionClarity/comparisons.json`. Saving results does not program the monitor.

AOC CU34G4H guidance follows the [manufacturer manual](https://www.aoc.com/api/asset/pi47089?ext=pdf&name=CU34G4H_manual): regular MBR requires Adaptive-Sync off and at least 75 Hz; increasing MBR reduces brightness; HDR disables MBR and MBR Sync controls; Fastest overdrive may worsen blur; Low Input Lag is unavailable at 180 Hz. These are AOC-specific instructions, not a universal monitor preset.

DDC/CI capability discovery is read-only and runs away from the UI thread. Handles are released after the query. Manufacturer-specific MBR and overdrive codes are not mapped. The hardware probe on this PC reported an NVIDIA RTX 5080, 200 Hz and no accessible physical monitor controls through that API. This does not establish that all possible vendor interfaces are unsupported.

Validation: `--motion-smoke <report-folder>` runs rendering, selected-display full-screen placement, persistence and simulated refresh transaction checks without changing hardware settings. `--motion-probe <json-path>` reads current display modes and DDC capabilities. The combined UI suite also visits the integrated page. Actual panel clarity, game performance and click-to-photon latency need user observation or external measurement.
