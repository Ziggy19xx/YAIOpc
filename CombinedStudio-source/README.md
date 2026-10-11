# YAIOpc

One Windows x64 application with a shared sidebar and workspace.

Extract the complete ZIP, then run **YAIOpc.exe**. No separate .NET or Python installation is needed. Keep every file and the monitor, models, and python folders together.

PC performance and Audio are the two main expandable sections in one sidebar. Open either section and select a page to display its workspace. Opening a section restores that application's selected page. Both sections can expand independently, and each application retains its selected page and settings during switching. A single page is highlighted across the shared navigation.

More tools is a separate expandable section underneath both applications. It starts closed and opens automatically when you navigate to a secondary FramePilot page. The shared sidebar scrolls when expanded sections exceed the window height; its width stays fixed while switching apps.

Velvet Audio's live processing, rolling capture, and soundboard remain available while FramePilot is foreground. Microphone processing never starts automatically: choose devices in Studio and press Start processing. Close the shared window to stop processing and exit both tools. Full voice setup instructions are in VOICE-SETUP.md.

Velvet Audio also includes **Headphone EQ** below Setup & help. Choose your headphones/speakers and enable EQ. Direct mode uses an existing Equalizer APO installation, with original Velvet Audio controls, ten bands (31.5 Hz–16 kHz), a response curve, seven built-in presets, custom saved presets, preamp, and conservative headroom. Preferences save locally in VelvetVoice/headphone-eq.json. EQ starts automatically when the app opens and continues while switching pages or apps.

For direct mode, keep Windows and games playing to your normal output, such as Speakers (Realtek). The source dropdown is hidden. Equalizer APO must be enabled for that endpoint; the Audio device setup button opens its installed device selector. Enabling EQ backs up config.txt to velvet-voice-before.txt and selects the Velvet Audio filters only on your chosen device, retaining the previous configuration for others. Disabling EQ or closing the app restores the previous configuration. Use one controller at a time: changing Peace settings can replace the managed configuration. Exclusive audio and disabled Windows enhancements can bypass APO processing. The app does not install a driver, change Windows defaults, or copy Peace code/presets.

Advanced routing retains the app's independent stereo playback engine, smooth live filter transitions, and linked −1 dBFS peak limiter. Choose Use separate playback routing, route a game's playback into an existing separate endpoint, and select that endpoint as Playback source. Source and headphones must differ. Keep Windows cable monitoring off and avoid reusing Studio's microphone output cable. Direct mode reserves conservative headroom rather than implementing that live limiter. Presets are general starting points rather than headphone-specific measurements.

Existing FramePilot and VelvetVoice preferences and sound libraries continue to use their separate LocalAppData folders. FramePilot's Recovery continues to use its existing journal. Game artwork uses the improved local image cache and shared background requests.

To update, extract the next complete release to a new folder. The original EXE-only updater is disabled because this edition has runtime and model dependencies.

## Build from source

Use a .NET 8 SDK on Windows: `powershell -File Build.ps1 -DotNet dotnet`. NuGet restores the pinned NAudio, ONNX Runtime, System.Management and ProtectedData packages. Both private Python runtimes, monitoring libraries, native audio models and the hero image are included in the source package.

## Validation

`FramePilot.exe --combined-smoke <absolute-results-directory>` checks all routes, sidebar ordering, state retention, synthetic audio continuity, modal dialogs and multiple window sizes with isolated settings and no microphone capture or playback.

`FramePilot.exe --framepilot-test <absolute-report-path>` checks the FramePilot services using temporary file/registry fixtures.

`FramePilot.exe --voice-test` runs the offline audio and soundboard suite and writes test-results.txt beside the executable.

`FramePilot.exe --monitor-test <absolute-report-path>` checks the bundled Python monitor.

Windows startup: use **Start with Windows** in the shared sidebar to run this portable executable at sign-in. Keep the full portable folder at the same path. On normal launch, saved microphone processing, headphone EQ, and FramePilot live monitoring start automatically. Missing devices or unavailable Equalizer APO are reported in their audio pages; configure devices there and use the start button to retry. Isolated test/preview sessions keep audio inactive.

YAIOpc means Your All-in-One PC. Motion clarity, UFO tests, temporal labs, and the experimental strobing switch have been removed from this release.
