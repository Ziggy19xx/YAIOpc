# Crosshair Studio

Open **Customization → Crosshair Studio**. The Customization chevron also contains True Stretch. This independently implemented native tab offers a custom desktop crosshair workflow inspired by [Crosshair X](https://store.steampowered.com/app/1366800/Crosshair_X/). No Crosshair X code, executable, assets or community library are bundled.

## Using the overlay

Choose a quick design or edit a shape, then select **Enable crosshair overlay**. The overlay remains active when changing tabs or minimizing YAIOpc. The default global toggle is **Ctrl + Alt + X**; you can choose C, F8 or F9 instead. A shortcut conflict is reported on the page. Closing YAIOpc removes the overlay and releases its shortcut. Launching the app leaves the overlay off.

Use a windowed or borderless game. This desktop overlay does not include Xbox Game Bar exclusive fullscreen support or Steam Workshop integration.

## Designer

- 32 original starter designs in an expandable library.
- Cross, T shape, dot, ring, ring + cross, chevron, diamond, triangle, square, brackets, double ring, burst, star and imported PNG shapes.
- Live preview; color, outline color, length, thickness, gap, dot size, ring radius, outline width, opacity and rotation controls.
- Monitor choice, horizontal/vertical offsets, recenter and an option to hide while holding the right mouse button.
- Named local presets with save, recall and delete. Presets are copied independently of current edits.
- Import/export of JSON design presets. Exported PNGs have transparent backgrounds.
- PNG imports are decoded and copied into local storage, so the original file can be moved. Supported PNGs are at most 10 MB and 2048 pixels per side. Import/export image designs as PNGs.

The topmost transparent overlay is click-through, excluded from the taskbar, and uses nonactivating window styles. It does not hook into game rendering. Size and offsets are expressed in pixels; placement uses the monitor bounds rather than the taskbar work area.

Settings and images are stored in `%LOCALAPPDATA%\YAIOpc\Crosshair`. The editor works offline. The local saved preset limit is 128.

## Build and checks

Latest runnable folder: `outputs/YAIOpc-Customization/`. Run `YAIOpc.exe` and keep the whole folder together. This build also contains True Stretch.

## Randomizer

Spin shows a carousel that slows to a result under its center marker and applies the winner to the editor and any enabled overlay. All spins are free. Save result adds a copy to the preset library.

Filters: All designs, Precision, Rings & targets, Creative, or Saved presets. Generated designs vary geometry, color and dot settings; saved-preset spins choose a copy from your existing library. Keep color and Keep shape constrain generation. Larger designs and Random rotation offer further variety.

Spin settings let you choose a duration of 0–6 seconds and an auto-spin delay of 2–30 seconds. Auto-spin is an optional session-only toggle; it pauses while the page is hidden. Leaving the page cancels a pending spin before it applies a result. Preferences for categories and modifiers are saved.

```powershell
YAIOpc.exe --crosshair-test <absolute-report-folder>
YAIOpc.exe --combined-smoke <absolute-report-folder>
```

The crosshair suite verifies persistence, preset independence, validation, monitor coordinates, all starter designs, PNG transparency, PNG import and JSON import. The shared UI suite checks navigation/layout and briefly shows the real overlay to check native click-through styles, placement and lack of focus theft, then hides it. It uses isolated settings and does not change monitor drivers or display modes.
