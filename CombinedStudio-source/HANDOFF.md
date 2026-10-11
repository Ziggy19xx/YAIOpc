# Automatic startup update � October 10, 2026

Latest portable build: `outputs/FramePilot-VelvetAudio-Autostart`. Windows sign-in startup is enabled for this folder through HKCU Run / FramePilotVelvetStudio; the sidebar Start with Windows checkbox controls it. Normal launches automatically start microphone processing, headphone EQ, and live monitoring. Devices and EQ routing mode are restored. Isolated UI/audio tests remain inactive for live hardware. Build passed; UI 71 checks and offline audio 55 groups passed. Live hardware listening was not verified. Prior statements below that normal audio never autostarts are superseded.

# Current architecture update — October 10, 2026

The authoritative continuation notes are in `../HANDOFF.md`. The latest app is **FramePilot + Velvet Audio**, with one 276-pixel shared sidebar containing independently expandable FramePilot and Velvet Audio sections and More tools underneath both. The two-rail design described below is superseded. Host/Shell.cs moves the existing navigation controls into shared expanders, preserving feature models and detached control namescopes.

Latest complete portable build: `outputs/FramePilot-VelvetAudio-Unified/FramePilot.exe` from the repository root. Validation: 71 shared UI checks and 55 offline audio groups pass. See SIDEBAR-VALIDATION.md and the reports in outputs/Unified-sidebar-validation. Live listening remains unverified.

Direct headphone EQ uses the already installed Equalizer APO through original Velvet Audio controls and presets; advanced separate routing remains optional. Internal VelvetVoice names, saved settings paths, and original APO marker/file names are retained for compatibility. No live APO files were changed by development/testing.

GitHub main's last pushed commit is b759ac7 (EQ). The rename and sidebar redesign are local and uncommitted/unpushed. This handoff does not authorize another push. Existing ZIPs and the GitHub artifact correspond to earlier builds.

---

## Historical architecture notes

# Combined application handoff

Implementation: a single .NET 8 WPF process compiles the existing FramePilot and VelvetVoice namespaces. Host/Shell.cs composes their native WPF workspaces and sidebars in one Window. There is no second application window or cross-process embedding. Inactive workspaces are collapsed, with both rails visible. Radio Click handlers activate already selected pages after mouse release, so reordering does not lose a click.

FramePilot and Velvet Voice keep their existing feature models and separate settings paths. WpfShell supports foreground activation; expensive FramePilot sampling pauses when its workspace is inactive. VelvetVoice's MainForm and audio engine live for the entire shared window session and are stopped only on explicit audio actions or shared window closure. Dialogs belong to the actual shared window. Closing respects FramePilot's busy-action checks before disposing Velvet Voice.

The .NET 8 port uses Host/LegacyJson.cs to preserve FramePilot's serializer field support, numeric types and ArrayList/dictionary object graphs. The original JavaScriptSerializer dependency is replaced; the original updater remains disabled. Native hardware discovery uses the System.Management NuGet package and encrypted artwork credentials retain Windows DPAPI.

Real-time audio stays in NAudio/C#; Python performs saved-clip RNNoise cleanup. No automatic recording, audio playback, Windows tuning, driver installation, game launch or routing change occurs during combined smoke validation. Synthetic mixer validation exercises continuity without opening an audio device. Live microphone hardware still needs user verification with the intended input/output route.

Packaging: Build.ps1 publishes a complete self-contained Windows x64 folder. FramePilot.exe is the sole entry point. All adjacent DLLs, models, Python and monitor dependencies must stay with it. The source archive contains only source and bundled dependencies, with build outputs excluded.
