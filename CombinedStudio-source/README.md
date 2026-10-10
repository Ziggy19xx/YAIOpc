# FramePilot + Velvet Voice

One Windows x64 application with two persistent navigation rails and a shared workspace.

Extract the complete ZIP, then run **FramePilot.exe**. No separate .NET or Python installation is needed. Keep every file and the monitor, models, and python folders together.

Click any FramePilot or Velvet Voice navigation item to display its page. The active application's rail sits beside its workspace; the other rail stays at the far left. Clicking the already selected page also switches apps. Each application retains its selected page and settings during switching.

Velvet Voice's live processing, rolling capture, and soundboard remain available while FramePilot is foreground. Microphone processing never starts automatically: choose devices in Studio and press Start processing. Close the shared window to stop processing and exit both tools. Full voice setup instructions are in VOICE-SETUP.md.

Existing FramePilot and VelvetVoice preferences and sound libraries continue to use their separate LocalAppData folders. FramePilot's Recovery continues to use its existing journal. Game artwork uses the improved local image cache and shared background requests.

To update, extract the next complete release to a new folder. The original EXE-only updater is disabled because this edition has runtime and model dependencies.

## Build from source

Use a .NET 8 SDK on Windows: `powershell -File Build.ps1 -DotNet dotnet`. NuGet restores the pinned NAudio, ONNX Runtime, System.Management and ProtectedData packages. Both private Python runtimes, monitoring libraries, native audio models and the hero image are included in the source package.

## Validation

`FramePilot.exe --combined-smoke <absolute-results-directory>` checks all routes, sidebar ordering, state retention, synthetic audio continuity, modal dialogs and multiple window sizes with isolated settings and no microphone capture or playback.

`FramePilot.exe --framepilot-test <absolute-report-path>` checks the FramePilot services using temporary file/registry fixtures.

`FramePilot.exe --voice-test` runs the offline audio and soundboard suite and writes test-results.txt beside the executable.

`FramePilot.exe --monitor-test <absolute-report-path>` checks the bundled Python monitor.
