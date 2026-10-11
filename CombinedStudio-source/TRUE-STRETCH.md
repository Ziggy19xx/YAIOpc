# True Stretch

Open **Customization → True Stretch**. This native page provides the display switching workflow described by [EasyTS v4.0.0](https://github.com/ahhmilo/EasyTS/releases/tag/v4.0.0), implemented independently. No EasyTS code or executable is bundled.

Register custom resolutions with your GPU first and select Full-screen / Full-panel scaling. In VALORANT choose Windowed Fullscreen and Fill. Turn the monitor driver off before launching VALORANT, then apply a resolution after loading into a match.

Popular modes: 1440x1080, 1280x1024, 1600x1080, 1280x1080, 1280x960, 1154x1080, 1080x1080. You can type a custom resolution, save it, click a saved preset to apply it, and delete saved presets.

The primary display is changed through Windows display APIs, after mode enumeration and a Windows test of the requested mode. Refresh rate is preserved where available. Unsupported modes produce an error instead of registering a GPU mode. The original display, resolution, refresh rate and bit depth are saved before the first switch. Subsequent switches keep the original recovery target. Restore original works across app restarts. Failed restoration keeps that target for retry.

Automatic restore checks for VALORANT-Win64-Shipping every two seconds while YAIOpc remains open, including when another page is selected. It restores only after observing the game running and then exiting. The preference is saved. Closing with an active session offers restore and close, keep the mode and close, or cancel.

Monitor controls use an elevated YAIOpc helper and Windows pnputil, limited to devices classified as Monitor. Status is refreshed explicitly. Turning off affects all monitor devices; it may affect brightness controls. Re-enable from this page or Device Manager → Monitors → Enable device. This action is never applied just by opening the page. For black bars, use the GPU scaling guide; this implementation does not include EasyTS's registry scaling patch.

Settings: `%LOCALAPPDATA%\YAIOpc\TrueStretch\settings.json`. The game files are not modified. The page works offline.

Portable build: `outputs/YAIOpc-TrueStretch/YAIOpc.exe`. Keep the complete folder together.

Verification switches:

```powershell
YAIOpc.exe --stretch-test <absolute-report-folder>
YAIOpc.exe --combined-smoke <absolute-report-folder>
```

The first switch tests persistence, validation, rejected modes, restoration failures and game exit transitions with a simulated display. It also reads the actual primary display without changing it. The shared UI smoke uses a simulated display and monitor controls are disabled, so it cannot change live resolution or monitor devices.
