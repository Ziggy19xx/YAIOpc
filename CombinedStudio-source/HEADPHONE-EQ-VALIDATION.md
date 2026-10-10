# Headphone EQ validation — October 10, 2026

Published the latest complete self-contained Windows application to `outputs/FramePilot-VelvetVoice-DirectEQ/` using the private .NET SDK 8.0.425 and the existing Build.ps1. Build succeeds; the existing SteamArtwork obsolete API warning remains.

- Offline audio suite: exit 0, 55 passing groups, including 24 headphone EQ checks. Tests cover 44.1/48/96 kHz gain response, channel isolation, extreme gains, protected bypass, neutral flat response, smooth changes, response curve math, feedback route rejection, persistence, and malformed settings recovery. Direct APO tests use temporary fixture folders: exact configuration backup/restoration, endpoint GUID selection, retained configuration for other devices, original filter export, bypass, external-controller conflict detection, and invalid device ID rejection.
- Combined UI suite: exit 0, 58 passing checks. Includes the new navigation route, preset and slider bindings, save/delete, bypass, hidden source selector in normal mode, no audio autostart, retained microphone settings, continued synthetic microphone audio during app switching, and window sizes of 1280 and 1600 pixels.
- Rendered EQ screenshots inspected at 1920 and 1280 pixels. The ten band controls use an original vertical slider style; the preset selector displays readable names. EQ controls precede routing, saved presets, and setup instructions.
- Regression suites required execution outside the restricted sandbox for the existing temporary hotkey registration and read-only Startup registry checks.

Reports: `outputs/FramePilot-VelvetVoice-DirectEQ/test-results.txt` and `outputs/DirectEQ-validation/combined-tests.txt`. The updated build is in a separate complete folder.

Equalizer APO was detected in Program Files. Its live config.txt still contains `Include: peace.txt`; development/testing did not change live audio settings. The user activates direct mode from the app. Direct mode uses the installed APO driver; it does not install a new driver or copy Peace source/presets. Equalizer APO must already be enabled on the selected endpoint, and Windows enhancements/shared audio must permit system effects. Direct filters reserve conservative headroom rather than applying the app's live limiter. Closing/disabling restores the prior configuration, while external changes are left intact.

Live WASAPI capture/playback and subjective headphone quality were not tested. Advanced routing retains the independent stereo engine with its separate playback endpoint, smooth transitions, and protected ceiling. It is optional; ordinary direct listening requires only the headphones/speakers selection.
