# Crosshair Studio validation — October 11, 2026

Portable publish succeeded using the existing private .NET SDK. The pre-existing SteamArtwork obsolete API warning remains.

- 30 crosshair checks passed: preset independence, persisted design/position, input validation, negative monitor coordinates, eight starter designs, exported PNG transparency and visible pixels, local PNG import, JSON preset import, and corrupt settings recovery.
- 80 shared UI checks passed, including the Crosshair Studio route and layouts at 1060, 1280 and 1600 pixels. Previous True Stretch, performance and audio routes still pass.
- The actual overlay was briefly shown and hidden with isolated settings. Native click-through/nonactivating styles, 320-pixel window bounds, primary-monitor centering, and unchanged foreground window were verified.
- The 1060-pixel screenshot was visually inspected. The gallery wraps and the page scrolls without horizontal clipping.
- The shared suite used expanded permissions for the existing startup-registry checks. No display mode or monitor driver was changed.

Not verified: appearance during an actual game, mixed-DPI multi-monitor hardware, right-mouse polling during gameplay, or a physical press of the global shortcut. Exclusive fullscreen through Xbox Game Bar and Steam Workshop are not part of this implementation.

Runnable build: `outputs/YAIOpc-CrosshairStudio/YAIOpc.exe`.
Reports: `outputs/Crosshair-validation/crosshair-tests.txt`, `outputs/Crosshair-ui-validation/combined-tests.txt`.
