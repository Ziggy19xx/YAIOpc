# Headphone EQ startup and power controls

Run `outputs/YAIOpc-EQAutoOn/YAIOpc.exe` and open Audio → Headphone EQ.

The first card provides an explicit Headphone EQ On/Off switch, startup preference, last preset name and backend status. Automatic startup defaults to on. Turning the session switch off does not uncheck automatic startup; uncheck the separate startup preference if you want future launches to remain off.

The complete current curve, preamp, device and routing preferences are restored before startup. Last preset identity is retained even after preamp or band edits mark the curve custom. Older custom configurations can recover a matching built-in or saved preset name from their band values. Otherwise the existing custom curve remains intact.

Headphone EQ startup is attempted before microphone initialization, so microphone startup failure no longer prevents headphone EQ from starting. If the playback backend is missing or cannot write its configuration, the switch stays off and the error is visible at the top. Device setup remains available beside the switch.

The live configuration inspected during this fix contained the app's managed EQ root plus an appended Sound V2 include. The old backend treated that as an external replacement, stopped tracking its enabled state, and then refused to recover the root. The backend now distinguishes appended content from a full replacement, continues updating its own gain file, and recovers managed roots across restarts. Off restores the previous configuration while preserving third-party additions without duplicating existing includes. Full replacements by other tools are still preserved rather than overwritten on shutdown.

Close Sound V2/other EQ controllers for a clean comparison of this app's on/off state and profiles. The current app controller does not remove unrelated EQ configurations.

Tests use synthetic audio and isolated APO files. They verify startup-on state, power switch state, custom preset retention, restart recovery, appended Sound V2 content, restoration without duplicate includes, actual preamp gain, and the existing app routes. Live headphone listening remains unverified.
