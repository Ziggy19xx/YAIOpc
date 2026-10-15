# Sound-profile EQ update — October 11, 2026

Sound V2's main page and custom EQ/preamp editor were inspected directly through the installed application. No reference settings were saved or applied.

Added a prominent headphone-preamp card, numeric entry, automatic-headroom control, sound-profile cards, quick presets and draggable EQ handles. Positive direct-mode preamp is no longer capped at −1 dB. Tone-shaping bypass preserves the preamp in both backends. Separate routing includes an optional −6 dBFS limiter and up to +6 dB of quiet-sound boost; those controls are disabled in direct mode.

Validation:

- Build and portable publish succeeded, with the existing SteamArtwork obsolete API warning.
- 64 offline audio checks passed. New checks verify real ±6 dB sample gain, bypass gain, direct +6 dB export, +5 dB automatic-reserve export, the −6 dBFS limiter, quiet-sound boost, and saved dynamics/headroom preferences. Existing independent audio and reversible APO checks still pass.
- 101 shared UI checks passed, including numeric preamp binding, presets, bypass and all existing themes/performance/audio/customization routes.
- The 1060-pixel headphone EQ screenshot was visually inspected. The preamp card and sound-profile cards wrap within the page.

Tests use synthetic signals, isolated settings and temporary APO fixtures. Actual headphone playback, in-game quality and Sound V2's radar were not tested or implemented.

Run `outputs/YAIOpc-SoundProfiles/YAIOpc.exe`; keep the complete folder together. This build includes Themes, True Stretch and Crosshair Studio.

Reports: `outputs/YAIOpc-SoundProfiles/test-results.txt`, `outputs/SoundProfiles-ui-validation/combined-tests.txt`.
