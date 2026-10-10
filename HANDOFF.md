# FramePilot + Velvet Voice — continuation handoff

Updated: 2026-10-10
Workspace: `C:\Users\Ziggy\Documents\ChatGPT\performance app 2`

## User request and completed work

The user requested FramePilot's More Tools pages redesigned to match its copper/dark Home and Game Library screenshots, faster Game Library artwork, Velvet Voice converted to WPF with Python support and the same visual style, then both applications merged into one window matching the final two screenshots.

The combined application is implemented and packaged. Both sidebars stay visible. Clicking either application's navigation displays its pages and moves its sidebar beside the workspace, leaving the inactive sidebar at the outer left. Clicking an already selected page also switches applications. Selected pages and voice settings remain intact across switches. Both applications share one visible window; Velvet Voice's engine remains alive while FramePilot is foreground. The final request was for a handoff, not additional implementation changes.

Attached documents were treated as reference material; the user's direct requests defined the work.

## Authoritative source and deliverables

Continue work in `C:\Users\Ziggy\Documents\ChatGPT\performance app 2\CombinedStudio-source`.

- Project: `CombinedStudio-source\CombinedStudio.csproj`
- Build: `CombinedStudio-source\Build.ps1`
- Technical handoff: `CombinedStudio-source\HANDOFF.md`
- Usage/build instructions: `CombinedStudio-source\README.md`
- Portable app directory: `outputs\FramePilot-VelvetVoice`
- Entry point: `outputs\FramePilot-VelvetVoice\FramePilot.exe`
- Portable ZIP: `outputs\FramePilot-VelvetVoice.zip` (134,146,688 bytes)
- Source ZIP: `outputs\FramePilot-VelvetVoice-Source.zip` (74,904,969 bytes)
- Validation reports and screenshots: `outputs\CombinedStudio-validation`
- Release validation summary: `outputs\FramePilot-VelvetVoice\VALIDATION.txt`
- Release file hashes: `outputs\FramePilot-VelvetVoice\SHA256SUMS.txt`

Extract the complete release folder and run FramePilot.exe. It is self-contained Windows x64; separate .NET/Python installations are unnecessary. Adjacent DLLs and the python, monitor and models folders must accompany the executable. The source archive excludes bin/obj and includes bundled dependencies.

Earlier standalone outputs remain available, but are not the authoritative combined source:

- `source-review\FramePilot-full-source` and `outputs\FramePilot-Tools-Redesign`
- `VelvetVoice-source` and `outputs\VelvetVoice-Wpf-Python`

Original inputs:

- `C:\Users\Ziggy\Desktop\new\FramePilot-full-source.rar`
- `C:\Users\Ziggy\Desktop\new\REDESIGN-HANDOFF.md`
- `C:\Users\Ziggy\Documents\Codex\2026-10-03\hello-hello-can-you-hear-me\outputs\VelvetVoice-Source.zip`

## Architecture and files to inspect

One .NET 8 WPF process compiles both existing namespaces. FramePilot's prior .NET Framework services were ported to .NET 8, retaining their original feature models and handlers. There is no cross-process window embedding.

- `Host\Program.cs`: shared entry point, instance mutex, CLI test/worker dispatch, application lifecycle.
- `Host\Shell.cs`: composes both workspaces and rails, active workspace selection, shared window, smoke checks and preview rendering.
- `Host\LegacyJson.cs`: JavaScriptSerializer compatibility using System.Text.Json; preserves fields, dictionary/ArrayList object graphs and numeric values. AllowReadingFromString handles WMI inventory values supplied as strings.
- `FramePilot\wpf\Program.cs`: native FramePilot shell, navigation activation and foreground sampling control.
- `FramePilot\wpf\MainWindow.xaml`: original copper/dark theme and Home layout.
- `FramePilot\wpf\ToolPages.cs`: native WPF presentation of all More Tools pages, backed by original WinForms models and handlers.
- `FramePilot\wpf\ArtworkImages.cs` and `FramePilot\src\SteamArtwork.cs`: frozen decoded image cache, shared background requests, local artwork priority and missing-result caching.
- `FramePilot\wpf\Bridge.cs`: feature/UI integration. EXE-only updater remains disabled.
- `VelvetVoice\Wpf\StudioWindow.cs` and `Shell.xaml`: Studio, Soundboard + clips, Voice changer and Setup & help; embedded into shared window while retaining its resource/name scopes.
- `VelvetVoice\Wpf\Bridge.cs`, `UiControls.cs`, `WpfDialogs.cs`: shared feature state, native controls, dialogs owned by the actual shared window, shortcut pause during modals.
- `VelvetVoice\Dsp.cs`, `Entertainment.cs`, `Program.cs`: NAudio real-time voice engine, mixer, clips and capture lifecycle.
- `VelvetVoice\PythonSpeech.cs` and `python\speech_worker.py`: local saved-clip RNNoise cleanup over bounded binary stdio. Real-time audio remains C#/NAudio; Silero runs through .NET ONNX Runtime.
- `Bundled\monitor` and `Bundled\models`: private monitoring runtime, libraries and reference models copied into the portable release.

Important interaction detail: use RadioButton Click activation after mouse release. Moving the sidebar on PreviewMouseDown or mouse-induced GotKeyboardFocus can move the target before release and lose the user's click. This was fixed and regression-checked. Both radio groups have distinct names.

Inactive workspaces are collapsed, not recreated. FramePilot sampling pauses while Velvet Voice is foreground. Velvet Voice is stopped only by explicit audio controls or actual shared-window closure. FramePilot's existing busy-action close checks run before final closure disposes the voice engine. The unshown Velvet WPF shell provides name/resource scopes; only one window is visible.

Existing settings and libraries retain their separate LocalAppData FramePilot and VelvetVoice locations. DPAPI artwork credentials and FramePilot recovery journal remain in use. No settings migration was introduced. Shared application title is FramePilot + Velvet Voice; executable/assembly identity remains FramePilot.

## Build

A private .NET SDK 8.0.425 is available here; the system dotnet command previously had no SDK:

`C:\Users\Ziggy\Documents\Codex\2026-10-03\hello-hello-can-you-hear-me\work\dotnet\dotnet.exe`

From the workspace:

```powershell
& .\CombinedStudio-source\Build.ps1 -DotNet 'C:\Users\Ziggy\Documents\Codex\2026-10-03\hello-hello-can-you-hear-me\work\dotnet\dotnet.exe'
```

Build.ps1 publishes Release, win-x64, self-contained, runtime 8.0.31, with PublishSingleFile=false. Dependencies: NAudio 2.2.1, ONNX Runtime 1.20.1, System.Management 8.0.0 and ProtectedData 8.0.0. NuGet restore succeeded with normal network access. Legacy deprecation warnings remain; the build has no errors. Do not enable trimming or single-file publication without revisiting legacy metadata tests and runtime dependencies.

## Validation completed

- 56 shared-window UI checks: 23 page routes, both rail arrangements, selected-page activation, mouse-down stability, retained voice settings, 20 synthetic mixer continuity switches, native modal dialogs and multiple window sizes.
- 97 FramePilot service checks using temporary files/registry fixtures; recovery/config behavior, discovery and artwork checks included.
- 31 offline Velvet Voice audio/soundboard/clip test groups, including RNNoise Python/native parity and Silero processing.
- 29 Python monitor checks: actual read-only inventory/samples, worker restart/cancellation/cleanup, models and isolated storage fixture.
- 6 Python saved-clip protocol tests.
- Final portable app smoke exit code: 0. Monitor exit code: 0.
- ZIP contents verified: 1,143 release entries, 769 source entries, expected runtimes/models present, bin/obj absent from source archive.

Reports: `combined-tests.txt`, `framepilot-tests.txt`, `voice-tests.txt`, `monitor-tests.txt` under `outputs\CombinedStudio-validation`.

Rendered previews include `combined-framepilot.png`, `combined-velvet-voice.png`, `combined-profiles.png` and both layouts at 1280/1600 widths. These were visually inspected; the combined rails, native controls and copper/dark cards match the requested composition/style. Pixel-for-pixel equality to every screenshot was not measured.

Useful commands:

```powershell
.\outputs\FramePilot-VelvetVoice\FramePilot.exe --combined-smoke "C:\Users\Ziggy\Documents\ChatGPT\performance app 2\outputs\CombinedStudio-validation"
.\outputs\FramePilot-VelvetVoice\FramePilot.exe --framepilot-test "C:\Users\Ziggy\Documents\ChatGPT\performance app 2\outputs\CombinedStudio-validation\framepilot-tests.txt"
.\outputs\FramePilot-VelvetVoice\FramePilot.exe --voice-test
.\outputs\FramePilot-VelvetVoice\FramePilot.exe --monitor-test "C:\Users\Ziggy\Documents\ChatGPT\performance app 2\outputs\CombinedStudio-validation\monitor-tests.txt"
.\outputs\FramePilot-VelvetVoice\python\runtime\python.exe -I .\outputs\FramePilot-VelvetVoice\python\test_worker.py
```

GUI EXEs return immediately when invoked directly from PowerShell. For reliable completion/exit codes use Start-Process -Wait -PassThru with properly quoted ArgumentList. Normal read access is needed for startup registry/hardware checks; restricted sandbox access previously failed. Elevated tool access was used for authorized isolated validation, not production tuning. Tests can emit dialog previews/voice fixtures beside the executable; move them into the validation folder before repackaging.

## Remaining practical checks and constraints

No live microphone capture, actual audio playback, game launch, driver installation, personal-file deletion or real optimization was performed. Audio continuity was checked using a synthetic mixer signal, not an active hardware microphone. Verify the user's intended input/output route and subjective voice quality if requested. Processing never autostarts. UI routing/code verification does not establish end-to-end in-game audio quality.

Existing release ZIPs correspond to the completed implementation before this standalone continuation handoff. No application code was changed for this handoff request. Rebuild/repackage both artifacts if subsequent code changes are made.

Do not stop the user's running standalone apps. If a test hangs, identify the exact workspace executable and test arguments before terminating only that test process. Do not replace user settings with test fixtures. Use isolated paths for UI checks. Current instructions prohibit spawning agents unless the user or applicable repository/skill instructions explicitly request delegation.
