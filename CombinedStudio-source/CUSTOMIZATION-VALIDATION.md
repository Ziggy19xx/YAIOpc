# Customization update — October 11, 2026

True Stretch and Crosshair Studio now live in a separate Customization sidebar group with the shared chevron template. Expanding it restores the last selected customization page. The PC performance group contains its original navigation only.

Crosshair X was inspected directly through its open Windows interface, including the Randomizer page and settings dialog. Its carousel, category selectors, spin duration and auto-spin interaction informed this original implementation. No Crosshair X assets or program code were copied.

Crosshair Studio now has 32 built-in designs and seven additional geometric shapes. The randomizer generates varied designs, applies the carousel winner, saves results, supports category filters and color/shape locks, and offers size/rotation options. Duration and auto-spin delay controls are under Spin settings. Auto-spin pauses off-page; cancellation cannot overwrite a newer result.

Validation:

- Portable publish succeeded, with the existing SteamArtwork obsolete API warning.
- 86 crosshair/model/rendering checks passed, including 1,000 valid random rolls with more than 990 distinct designs, locking, categories, saved-preset independence, all 32 PNG renders and transparency.
- 83 shared UI checks passed, including Customization ownership, chevron collapse/restore, three window widths, randomizer application/centering and cancellation, native overlay placement/focus, True Stretch and the prior performance/audio routes.
- The 1060-pixel screenshot was visually inspected. The selected carousel item matches the live preview and all new controls fit the scrolling page.
- Shared UI tests use isolated settings and expanded access for existing startup-registry read checks. Display modes and monitor drivers were not changed.

Build: `outputs/YAIOpc-Customization/YAIOpc.exe`.
Reports: `outputs/Customization-validation/crosshair-tests.txt` and `outputs/Customization-ui-validation/combined-tests.txt`.

Actual gameplay and mixed-DPI multi-monitor behavior remain unverified. This remains a native desktop overlay for windowed/borderless games.
