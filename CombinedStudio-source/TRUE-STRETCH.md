# True Stretch side guide — October 15, 2026

Latest portable build: outputs/YAIOpc-TrueStretch-Guide/YAIOpc.exe. True Stretch now has a responsive right-hand setup guide (stacks below on narrow windows) with seven steps covering registered resolutions, full-panel GPU scaling, Windowed → Fullscreen + Fill → Windowed + Fill, Apply + fit, and restore. Adjacent resolution picker buttons only populate the input; no immediate display change.

1080p choices: 1440x1080 (4:3), 1350x1080 (5:4), 1728x1080 (16:10), 1080x1080 (1:1), 1920x1080 (16:9 native). 1440p choices: 1920x1440 (4:3), 1800x1440 (5:4), 2304x1440 (16:10), 1440x1440 (1:1), 2560x1440 (16:9 native). Lower alternatives: 1280x960, 1600x1200, 1280x1024, 1600x1000. Notes distinguish example ratios from actual GPU support and native/no-stretch modes.

Build passed with existing SteamArtwork warning. Stretch tests exited 0; layout review exited 0 at 1060 and 1480 widths with zero overflow candidates. Wide True Stretch screenshot visually inspected. Reports: outputs/TrueStretch-Guide-Validation and outputs/TrueStretch-Guide-Layout. No live display changes made for this UI update. Source changes local/uncommitted.
# Confirmed True Stretch method — October 15, 2026

User confirmed this exact order produces both stretch and no black bars:
1. VALORANT: Windowed mode.
2. Switch to Fullscreen and Fill; apply/confirm.
3. Switch back to Windowed and Fill; apply/confirm.
4. Then apply the custom stretch resolution in YAIOpc.

Fill alone removed the bars but lost the desired stretch. Do not treat no bars alone as success. This supersedes older Windowed Fullscreen guidance for this user's setup. Keep this recipe visible in True Stretch.

Added Apply + fit VALORANT: switches the chosen resolution then performs the existing borderless window fit. It does NOT execute VALORANT's internal menu transitions. Those transitions remain unautomated: Windows game UI input failed with SendInput error 87 during live testing; no working settings-control API was established. Do not claim full one-click engine setup. User confirmed the combined Apply + fit VALORANT action works perfectly on October 15, 2026, preserving stretch and removing the black bars. This validates their setup; it does not establish automated internal game-menu transitions. No game configs, Windows monitor devices or live resolutions were changed by this update.

Latest build: outputs/YAIOpc-TrueStretch-Workflow/YAIOpc.exe. All changes local/unpushed.
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
