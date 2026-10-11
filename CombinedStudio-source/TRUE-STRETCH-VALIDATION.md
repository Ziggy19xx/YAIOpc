# True Stretch validation — October 11, 2026

- Portable publish succeeded using the existing private .NET SDK 8.0.425. One existing SteamArtwork obsolete API warning remains.
- 18 display-controller checks passed, including the native DEVMODEW layout, invalid input, no-op switching, rejected mode recovery, preservation of the original across switches, restart persistence, preset deduplication, game exit detection, preference handling, failed restoration recovery, and read-only live primary-display inspection.
- 76 shared UI checks passed, including True Stretch navigation and layout at 1060, 1280 and 1600 pixels, plus the existing performance/audio routes and audio fixtures.
- Screenshots were visually inspected at the initial width and 1060 pixels. Controls wrap and the page scrolls without clipping horizontally.
- The UI suite required execution outside the restricted sandbox to read startup registry entries for an existing page. Test settings remain isolated inside the report directory.
- No live resolution changes, monitor device enable/disable operations, UAC actions, GPU custom-mode registration or VALORANT match tests were performed. Display transitions and game lifecycle behavior were verified through fixtures.

Runnable build: `outputs/YAIOpc-TrueStretch/YAIOpc.exe`.
Reports: `outputs/TrueStretch-validation/stretch-tests.txt`, `outputs/TrueStretch-ui-validation/combined-tests.txt`.

See `TRUE-STRETCH.md` for setup and the scope of the native implementation.
