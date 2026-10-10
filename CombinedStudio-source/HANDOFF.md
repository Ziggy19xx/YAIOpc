# Combined application handoff

Implementation: a single .NET 8 WPF process compiles the existing FramePilot and VelvetVoice namespaces. Host/Shell.cs composes their native WPF workspaces and sidebars in one Window. There is no second application window or cross-process embedding. Inactive workspaces are collapsed, with both rails visible. Radio Click handlers activate already selected pages after mouse release, so reordering does not lose a click.

FramePilot and Velvet Voice keep their existing feature models and separate settings paths. WpfShell supports foreground activation; expensive FramePilot sampling pauses when its workspace is inactive. VelvetVoice's MainForm and audio engine live for the entire shared window session and are stopped only on explicit audio actions or shared window closure. Dialogs belong to the actual shared window. Closing respects FramePilot's busy-action checks before disposing Velvet Voice.

The .NET 8 port uses Host/LegacyJson.cs to preserve FramePilot's serializer field support, numeric types and ArrayList/dictionary object graphs. The original JavaScriptSerializer dependency is replaced; the original updater remains disabled. Native hardware discovery uses the System.Management NuGet package and encrypted artwork credentials retain Windows DPAPI.

Real-time audio stays in NAudio/C#; Python performs saved-clip RNNoise cleanup. No automatic recording, audio playback, Windows tuning, driver installation, game launch or routing change occurs during combined smoke validation. Synthetic mixer validation exercises continuity without opening an audio device. Live microphone hardware still needs user verification with the intended input/output route.

Packaging: Build.ps1 publishes a complete self-contained Windows x64 folder. FramePilot.exe is the sole entry point. All adjacent DLLs, models, Python and monitor dependencies must stay with it. The source archive contains only source and bundled dependencies, with build outputs excluded.
