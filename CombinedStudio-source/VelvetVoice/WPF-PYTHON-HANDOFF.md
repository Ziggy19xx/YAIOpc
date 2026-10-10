# Velvet Voice WPF + Python edition

Updated October 10, 2026. Version 1.5.0.

## Architecture

- Native WPF shell and controls styled with FramePilot's warm dark/copper tokens.
- Studio, Soundboard + clips, Voice changer, and Setup & help use native WPF pages.
- Rename, shortcut assignment, and reversible trim dialogs use WPF.
- The original C# WASAPI/DSP engine handles real-time microphone processing,
  pitch effects, rolling audio, soundboard mixing, previews, and device lifecycle.
- Saved snapshots go to a bundled Python 3.13.16 worker over binary standard
  input/output. Python drives the same RNNoise model and reproduces its alignment,
  natural blend, VAD counts and strong attenuation. It never opens audio devices,
  records from the microphone, uploads clips, or writes audio files.
- Voices only still runs local Silero VAD in the existing ONNX Runtime boundary
  after Python denoising. No voice-extraction quality guarantee has been added.
- WinForms controls remain internal editor state for the shared feature handlers;
  the WPF edition does not display those legacy pages. `--legacy` retains a
  developer fallback. Native library sources and attribution remain included.

## Distribution

Extract the complete portable ZIP into a new folder and run VelvetVoice.exe.
Keep rnnoise.dll, silero_vad.onnx, and python/ beside the executable. The Windows
.NET runtime is self-contained and Python is private to this application; the
recipient does not need to install either. No driver or installer is included.
Close the old VelvetVoice copy before using this one for live processing.

The existing %LOCALAPPDATA%/VelvetVoice settings.json, board.json and Sounds folder
are reused. Studio processing and rolling capture never autostart. Old user data
is not copied into the distribution. The old installed copy has not been replaced.

## Build

Use a .NET 8 SDK. Existing packages remain pinned to NAudio 2.2.1 and ONNX Runtime
1.20.1. The private Python runtime is included in the source archive.

`powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Wpf-Python.ps1`

Pass `-DotNet <path-to-sdk-dotnet.exe>` when using a private SDK.

## Verification

`VelvetVoice.exe --self-test` exercises offline synthetic DSP, mixing, pitch,
shortcuts, Python/RNNoise cleanup, Silero extraction, trim, persistence, and
lossless original retention. It never records or transmits live audio.

`VelvetVoice.exe --ui-smoke` uses temporary preferences and renders each WPF page
at 1380x930 and 1060x700. It verifies Studio bindings, pad selection and voice
preset state with audio off. Preview commands also use temporary preferences.

`python/runtime/python.exe -I python/test_worker.py` checks malformed/truncated
protocol data, size/mode validation and silent partial-frame output.

Real-device listening quality, game routing, latency and every DPI/device
combination require user validation. No live audio capture test was performed
during this redesign.
