# Headphone preamp and sound profiles

Open **Audio → Headphone EQ** in `outputs/YAIOpc-SoundProfiles/YAIOpc.exe`.

Sound V2 was opened and inspected directly, including its custom-profile preamp editor. These additions are an original implementation of the EQ workflow; Sound V2 code or assets are not bundled.

## Headphone preamp

The first card provides a −24 to +6 dB slider, exact numeric entry and Reset to 0 dB. Requested and applied gain are shown separately. The gain is included in saved presets and restored on startup.

The old direct backend capped every gain at −1 dB, even when the preamp was positive. That cap is removed. With Automatic headroom off, a +6 dB setting emits `Preamp: 6 dB` to Equalizer APO, and the separate-routing DSP applies the same gain to the actual samples. With Automatic headroom on, positive EQ band boosts are compensated, and direct mode reserves an additional 1 dB. A flat +6 dB setting therefore applies +5 dB in protected direct mode.

Tone-shaping bypass now leaves the headphone preamp active in both backends. Turning headphone EQ off restores the previous direct configuration as before.

Direct mode has no peak limiter. Manual positive gain can clip already-loud audio. Automatic headroom is enabled by default; start at 0 dB and raise gradually.

## Sound profiles and editor

Footstep Focus, Combat Clarity, Spatial Awareness and Flat are available as cards. These are general EQ starting points, not sound-event or directional classifiers. Bass, Vocal, Treble, Flat and Footsteps quick presets are also available below the curve.

Drag a frequency handle to change its band gain, or use the ten existing sliders. The curve shows tone response separately from the headphone preamp. Named custom presets support save, recall and delete.

## Limiter and quiet-sound boost

Separate playback routing offers an optional −6 dBFS peak ceiling and up to +6 dB of quiet-sound boost. Boost follows the stereo signal envelope with smooth transitions and preserves channel balance. The usual −1 dBFS protective ceiling remains when the stronger limiter is off.

These dynamic controls are disabled in direct Equalizer APO mode. The backend does not pretend to provide dynamics that it cannot implement. Both settings and the automatic-headroom preference are saved.

Sound V2's radar overlay is not implemented by these EQ additions.

## Verification

Offline signal tests verify ±6 dB sample gain, preamp during tone bypass, actual direct-backend gain export, reserved headroom, limiter ceiling and quiet-sound amplification. Shared UI verification includes the numeric preamp binding, custom presets and existing routes. Tests use synthetic audio and temporary APO fixtures; live listening was not performed.
