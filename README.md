# FramePilot + Velvet Voice

A Windows x64 desktop application that combines FramePilot performance tools and Velvet Voice audio tools in one .NET 8 WPF window.

The maintained application is in `CombinedStudio-source/`. It includes both feature sets, native dependencies, audio models, and bundled Python runtimes. Earlier source copies and generated distributions in this workspace are excluded from Git.

## Build

Install the .NET 8 SDK on Windows, then run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CombinedStudio-source\Build.ps1 -DotNet dotnet
```

The complete portable application is written to `outputs/FramePilot-VelvetVoice/`. Run `FramePilot.exe` there. Keep the whole folder together; distributing only the executable will break dependencies.

See [application documentation](CombinedStudio-source/README.md), [architecture handoff](CombinedStudio-source/HANDOFF.md), and [GitHub setup instructions](GITHUB-SETUP.md).

## Validation

`CombinedStudio-source/VALIDATION.txt` records previous validation; these results are not newly verified by the GitHub setup. The application documentation lists the available validation switches. Live microphone routing and audio quality still require testing with the intended devices.

The GitHub Actions workflow builds the self-contained Windows application and uploads the portable folder as an artifact. It does not run microphone, tuning, or hardware tests.

## Licensing

No license has been selected for the project's own code. Third-party notices remain in the source tree and are copied into the published application. Review redistribution rights for bundled dependencies and assets before making the repository public.
