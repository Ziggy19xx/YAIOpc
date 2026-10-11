# Motion clarity validation — October 10, 2026

Published complete portable app: `outputs/FramePilot-VelvetAudio-MotionClarity/`.

Launch `Open Motion Clarity.cmd` for a standalone tool alongside the existing studio app. The full studio also has a Motion Clarity primary navigation item. Existing running application binaries were not replaced.

- Release build/publish passed with the existing SteamArtwork obsolete API warning.
- Standalone motion suite: 22 passing checks, exit 0. Reports and screenshots in `outputs/MotionClarity-validation/`.
- Combined UI suite: 73 passing checks, exit 0. Reports and screenshots in `outputs/MotionClarity-combined-validation/`; integrated motion suite also passed.
- Reviewed standalone, full-screen and integrated page screenshots.
- Refresh transaction checks use a fake apply function and verify preview selection lock, rollback, persistence, failed persistence rollback, and driver rejection. Tests never apply a physical display mode change.
- Hardware probe found NVIDIA GeForce RTX 5080 and 200 Hz. DDC/CI physical monitor acquisition failed. Probe mode resolution differed across desktop contexts; do not treat it as verified panel-native resolution.
- No physical ghosting measurement, input latency measurement, monitor-menu adjustment, or game benchmark was performed. This release is a tuning/comparison tool with a real Windows refresh control; it does not automatically program MBR/overdrive or claim a measured clarity improvement.

See `MOTION-CLARITY.md` for control behavior and AOC manual references.
