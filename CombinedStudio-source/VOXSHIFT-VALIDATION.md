# VoxShift voice changer — 2026-10-10

Portable app: `outputs/FramePilot-VelvetAudio-VoxShift/FramePilot.exe`. Close the older app and run this executable with the entire folder intact.

The previous Low & smooth/Bright/Storyteller engine was replaced with upstream VoxShift DSP and all 46 presets, categorized selection, full effect controls, and local custom preset saving. Microphone selection, Studio DSP, WASAPI routing, soundboard mixing and peak protection stay in the existing pipeline. A bounded background buffer worker uses the existing embedded Python runtime with bundled NumPy/SciPy. No drivers, GUI, AI engine or network installation from upstream are invoked. Third-party licenses and source provenance are included.

Verified on Windows x64:
- Release portable publish succeeds (existing SteamArtwork obsolete-API warning).
- 72 shared-window UI checks, including preset replacement, pitch/formant, effects-only presets, custom save/recall and bypass.
- 56 offline audio groups, including real worker pitch shifts: Deep 329 Hz, Woman 659 Hz, Announcer 392 Hz from a 440 Hz input; custom configuration serialization.
- All 46 presets at 44.1/48 kHz over variable block lengths, finite bounded output, pitch frequencies, persistent binary protocol, preset changes and worker shutdown.

Reports: `outputs/VoxShift-validation/combined-tests.txt` and `outputs/FramePilot-VelvetAudio-VoxShift/test-results.txt`. Voice page screenshot: `outputs/VoxShift-validation/combined-voxshift.png`.

No live microphone listening or in-game audio-quality test was performed. Voice effects are traditional DSP characters. Worker buffering targets 40 ms, plus roughly 21–23 ms pitch delay and existing device/Studio buffering. Preset changes refill the queue. Worker failures bypass effects and show a status; restart Studio to retry.
