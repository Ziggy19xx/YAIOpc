# Velvet Audio

A Windows microphone processor for SM7B → Elgato Wave Link → standard VoiceMeeter B1 → game chat.

## Run

This is the **WPF + Python edition (1.5)**, with the same warm dark/copper style as FramePilot. Extract the complete portable ZIP into a new folder, close your old copy, and run **VelvetVoice.exe**. Keep `rnnoise.dll`, `silero_vad.onnx`, and the complete `python` folder beside it. No installer or driver is included in this edition.

The build is self-contained for **Windows 10/11 x64**. Recipients do not need to install .NET or Python. The WPF interface controls the existing C# real-time audio engine; saved-clip cleanup runs locally through the bundled Python/RNNoise worker, followed by Silero for Voices only. Keep the app running while using voice chat. Audio processing starts automatically when the app opens. Rolling capture starts only when requested. Existing voice settings, pads, shortcuts, and audio files are reused from your Windows account. See **WPF-PYTHON-HANDOFF.md** for build instructions and architecture.

VoiceMeeter and VB-CABLE are separate vendor drivers, not bundled or installed by setup. Recipients can select any active Windows microphone in this app, including their interface directly. For in-game use, select CABLE Input as the processed output and CABLE Output as the game's microphone. Your SM7B presets are starting points; other microphones may need different EQ.

## Soundboard and instant clips (version 1.3)

Use **Soundboard + clips** to import MP3, WAV, AIFF, M4A or WMA sounds. Codec availability varies for M4A/WMA; invalid or unsupported files show an error. Imports are decoded to local mono 48 kHz WAV copies, leaving the original file untouched. Up to 32 sounds and five minutes of total audio can be loaded; each import must be at most two minutes. Rename pads, set per-sound gain, and adjust the overall soundboard level. Double-click a pad or use Play selected to send it through the same microphone output as your live voice. Eight sounds can overlap, with 10 ms fades and a master −1 dBFS peak guard. Your microphone stays live while sounds play and when you click Stop all sounds.

Start Studio processing before using Play or playback shortcuts. The soundboard is mixed after live mic processing, so voice EQ and voice-changing presets do not alter imported sounds or clips. The Studio output meter displays the final voice-plus-soundboard mix. Compare original also bypasses live voice pitch changes; soundboard playback remains available.

For recent game/chat audio, choose **Desktop ·** followed by the device you actually hear the game through, then click **Start rolling**. Choose a **Mic ·** recording endpoint to clip that input instead. It captures the whole selected endpoint, not individual people or apps. Set the slider to an integer duration from **1 to 15 seconds** and click **Save clip**. If capture has only been running for three seconds, a 15-second request saves those three seconds. Silent gaps remain silence rather than replaying stale sound. Save clip adds a new local WAV pad without transmitting it. Replay last clip or Play selected then sends the saved sound to the game mic. Do not choose the processed cable output as your desktop capture source.

Rolling capture starts only when you click Start rolling. Its current history lives in memory; only Save clip writes that audio to disk. Stop rolling clears its memory buffer. Clips and imported sounds persist under `%LOCALAPPDATA%\VelvetVoice\Sounds`; pad metadata, source choices and shortcuts are in `board.json`. Remove pad removes the board entry while preserving its WAV file in that folder. To delete the audio itself, remove the WAV from that folder. Nothing is uploaded. Choose Headset output and enable Hear clips in my headset to hear normal Play, pad shortcuts and Replay last clip locally as well as through the microphone output. Both copies use the soundboard level and pad gain; replay restarts each copy. Stop all sounds stops both. Private Preview and Original still let you listen without transmitting. Headset monitoring contains clips only, not your live microphone. Disabling the checkbox or changing headset device stops the local copy. Separate audio-device buffers may introduce a small timing difference. If rolling capture records that same headphone device, it also records clips you play through it.

**Assign shortcut** captures a key combination for the selected pad. The three buttons below the clip controls let you separately bind **Start/stop rolling**, **Save clip**, and **Replay last clip**. Both the rolling button and its shortcut toggle capture on each press: start, stop, start, stop. The button reads Start rolling when stopped and Stop rolling when running. Stopping clears unsaved rolling history; saved clips remain available. Existing Start rolling bindings now toggle without needing to reassign the key. Save clip saves the previous slider-selected duration. Replay immediately replaces the previous replay in the mix and starts the last saved clip at its beginning, allowing rapid repeated presses without stacking copies. Other soundboard sounds and your live microphone continue. The saved last-clip reference and all shortcuts persist after restarting. Existing audio already sent to a driver/game buffer cannot be withdrawn, so audible retrigger timing still depends on your selected buffer/device latency. Stop shortcut stops soundboard sounds.

Shortcuts observe assigned key states without reserving or swallowing the keys: your game and other apps can receive the same press. Duplicate assignments within Velvet Audio are rejected. Holding a key does not repeat; release and press again to retrigger. Extra modifiers prevent a bare-key binding from firing. Actions pause while Velvet Audio has a modal dialog open. Windows privilege restrictions may prevent detection in an elevated game, and very brief presses during a busy UI may be missed. No keyboard hooks or typed-text logging are used. Plain comma/period/slash bindings also trigger while typing those characters elsewhere; use a modifier combination to avoid that.

## Trim a pad

Select a sound and click **Trim…**. Set start and end times in seconds; the highlighted waveform shows the retained section. Preview selection plays privately through Headset output. Save trim applies to pad playback, shortcuts, replay and the local headset copy. The pad keeps its name, keybind and gain. Trimming is reversible: reopen Trim, Reset, then Save trim. Full audio stays on disk, so trimming shortens playback but does not reclaim the library audio budget. Original preview still plays the unprocessed source when available.

## Speech focus for mixed game clips

**Voices only** runs RNNoise, then Silero VAD v6.0 locally. Save clip keeps detected speech regions and removes the longer gaps between them, making the pad shorter. Short pauses and padding around words are retained, with small fades at cuts. If no speech is found, no empty pad is added. Existing pads are unchanged. The unprocessed full snapshot is retained as Original for successfully saved clips. This is voice activity detection plus denoising, not guaranteed separation of voices from overlapping game effects. Quiet voices can be missed; choose Natural or Off when needed. Models and runtime are bundled; no cloud requests or recipient model download is required. Keep silero_vad.onnx and rnnoise.dll beside the portable executable.

Choose **Speech focus** before Save clip: **Off** saves the raw mix; **Natural** blends neural noise reduction with a little original sound; **Strong** also softly attenuates sections where speech is not detected. Natural is the default. This processes only newly saved rolling clips, never your live microphone. Processing runs locally after the capture snapshot, so the save may take a moment. Replay last clip sends the processed saved clip through your microphone output as before.

For processed clips, **Original preview** lets you compare the retained raw recording privately through the selected headphones. Original files are stored beside their processed WAVs. Removing a pad preserves both files. Existing saved clips are unchanged.

This is speech-focused noise reduction, not guaranteed voice extraction. Game sounds can resemble speech and gunshots overlapping words may remain or damage the cleaned voice. Strong can attenuate quiet or unusual voices; try Natural or Off when that happens. Separate voice-chat and game output devices, where a game supports them, provide cleaner isolation than processing a combined mix. RNNoise v0.2 and its bundled model perform cleanup without a cloud service or model download. Keep rnnoise.dll beside VelvetVoice.exe in the portable build.

## Voice changer

The **Voice changer** tab uses VoxShift's 46 DSP presets, independent pitch/formant processing, EQ, distortion, bitcrusher, ring modulation, tremolo, vibrato, echo and reverb. Select a category/preset, enable and tune effects, then save a custom preset. Off bypasses the voice changer. Only the live microphone is affected; soundboard clips retain their sound. Studio processing still runs upstream. The optional VoxShift gate defaults to off. These are DSP characters, not AI voice cloning.

A persistent buffer-only Python worker runs outside the audio callback using bundled NumPy 2.2.6 and SciPy 1.15.3. No installation or network is needed. Worker buffering targets 40 ms plus roughly 21–23 ms pitch delay, alongside device and Studio buffering. Preset changes reset effect tails and briefly refill the queue. Failure/overload bypasses effects and displays a status; restart Studio to retry. Microphone selection and WASAPI routing remain managed by the existing app.

## Your routing: standard VoiceMeeter + Wave Link

1. In Wave Link, send your SM7B to the microphone effects output. Disable its effects initially, especially gates, suppression, compressors and automatic gain. Set interface gain while speaking normally so peaks reach about −18 to −10 dBFS; loud speech must remain below 0 dBFS. Avoid adding gain to already clipped audio. Your detected hardware endpoint is **Microphone (Elgato Wave XLR)**; if Wave Link's MicrophoneFX endpoint is unavailable, use that raw Wave XLR endpoint as VoiceMeeter's hardware input.
2. In VoiceMeeter's hardware input, select the **Wave Link MicrophoneFX** recording device (its name may vary by version). Enable **B** on that microphone strip; standard VoiceMeeter's B bus is B1. Keep the mic strip's gate and compressor at zero. Send only the microphone to B: disable B on desktop/game audio and other input strips. Prefer the MicrophoneFX source to a stream mix containing game audio.
3. Install **VB-CABLE** if absent: https://vb-audio.com/Cable/ . This is a separate signed virtual audio driver from VB-Audio, not bundled with this app. Follow the vendor's installation/reboot instructions. A user-mode app alone cannot create a system-wide Windows microphone device.
4. In Velvet Audio choose **Voicemeeter Out B1** (older name: **Voicemeeter Output**) as microphone input. Choose **CABLE Input (VB-Audio Virtual Cable)** as processed output. The cable names are intentionally reversed: the app plays into CABLE Input, while games record from CABLE Output.
5. Click **Start processing**. In the game/Discord choose **CABLE Output** as the microphone and your normal headphones as the listening output. Disable its suppression, automatic gain and voice threshold if available; use continuous voice activation or push-to-talk. An app cannot override a game's own gate or voice codec.
6. Do not feed CABLE Output into the B1 input, and do not enable Windows "Listen to this device" on B1/cable. Both can create a feedback loop. Keep headphones on if testing a hardware speaker output.

Use 48 kHz on Wave Link, VoiceMeeter, CABLE Input and CABLE Output where possible. Shared WASAPI handles endpoint formats; the app explicitly converts between capture and output rates. Select Mono mix for a dual-mono B1 signal; use Left/channel 1 if only one channel carries the microphone (mixing a silent channel reduces the input by 6 dB).

## Dial in your voice

Start with **Broadcast · balanced**. This is a conservative starting point, not a measurement of your voice or room. No universal "Joe Rogan settings" can guarantee a particular sound.

- **Input gain:** bring quiet input up modestly. Prefer clean gain at the interface; software gain also raises room noise. Watch INPUT and avoid clipping upstream.
- **Bass · smooth shelf:** −8 to +8 dB broad low shelf centered at 160 Hz. Zero keeps your existing tone; positive adds depth, negative reduces bass. Start around +2 to +3 dB for gentle body. Changes ramp over 60 ms while preserving filter history, so adjusting it live stays smooth. The 65 Hz rumble filter and output limiter remain active. Bass is saved with your settings and retained when switching voice characters.
- **Warmth:** a broad 140 Hz bell. Lower it if proximity makes the sound boomy.
- **Presence:** a broad 3.3 kHz bell. Raise slightly for intelligibility; reduce for a sharp or nasal voice.
- **Air:** a gentle 9.5 kHz shelf. Keep this small if your room/preamp is noisy.
- **Compression:** 8 dB soft knee, RMS detector, 5 ms gain attack / 180 ms release. Default 2.5:1 at −20 dBFS. Aim for roughly 2–5 dB reduction during normal speaking, more only on loud phrases. No automatic makeup gain.
- **De-ess maximum:** limits attenuation of the high-frequency component when sibilance dominates and the voice is above −42 dBFS. This detector threshold activates de-essing only; it never closes the microphone. Set to zero to disable.
- **Output gain:** raise gently after compression. Aim for normal output peaks around −12 to −6 dBFS. The 3 ms lookahead limiter caps sample peaks at −1 dBFS; frequent limiting means gain is too high. This is a sample-peak limiter, not a certified true-peak limiter.
- **Compare:** switches smoothly between the original input and processing, with the protective limiter retained on both. Compare at similar loudness. "Original" ignores input/output gain and EQ.

All characters retain a 65 Hz high-pass and a modest 320 Hz cleanup cut. The live microphone has **no gate, expander, denoiser or automatic level ride**. Whispers stay audible; room sound also stays audible. Intimate/whisper uses gentler compression and less air. It does not magically raise whispers to shouting volume.

Keep the SM7B roughly 2–4 inches from your mouth, slightly off-axis, with its windscreen installed. Start with its rear EQ switches flat so software tuning is predictable. Test normal voice, whispers, S sounds and loud phrases. Shure notes that the SM7B needs substantial clean preamp gain; software cannot recover signal buried under preamp/room noise: https://service.shure.com/articles/en_US/Knowledge/sm7-output-level-and-preamp-gain-specifications . Avoid stacking multiple compressors.

## Latency and stability

Balanced starts with a 40 ms capture queue and 40 ms requested playback buffering, plus device/VoiceMeeter/cable latency and two 3 ms protection stages. Enabled pitch effects add their own processing delay. These are requested buffers, not a measured end-to-end latency. Responsive requests smaller buffers; Stable is useful under game CPU load. There is a small adaptive rate correction to compensate independent device clock drift. The queue readout shows capture buffering, not total latency.

If the underrun/overrun count grows, stop, choose Stable, and make device rates consistent. Audio device disconnection stops processing and displays an error; reconnect, Refresh devices and restart. The app never claims to prevent every driver or game-induced interruption.

## Optional Banana/Potato route without a cable

Capture B1 in this app and output to **Voicemeeter AUX Input**. Route only that AUX virtual strip to **B2**, with its B1 send off. Route the raw mic only to B1, with B2 off. Games record **Voicemeeter Out B2 / AUX Output**. Sending processed AUX back to B1 creates feedback. Standard VoiceMeeter lacks this independent AUX/B2 route, so use VB-CABLE for your setup.

## Files and development

Earlier installers used `%LOCALAPPDATA%\Programs\VelvetVoice`; this portable edition runs from its extracted folder. The portable desktop build contains a self-contained executable, rnnoise.dll and guide. Studio settings are saved under `%LOCALAPPDATA%\VelvetVoice\settings.json`; soundboard settings are in board.json. Device selection and parameters persist; live processing starts automatically when the app opens; rolling capture starts only when requested. Audio clips are stored only when you request Save clip. No network access, cloud processing or telemetry is implemented. Native runtime files may be extracted to the user's temporary .NET bundle cache on first launch.

Source: Wpf/ (shell, controls and dialogs), Program.cs (shared audio lifecycle/editor state), Dsp.cs (signal chain/rate buffer), PythonSpeech.cs + python/speech_worker.py (saved-clip cleanup), Tests.cs (offline checks). Build with a .NET 8 SDK:

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Wpf-Python.ps1
```

The script includes the private Python runtime, models, native libraries and guides. `VelvetVoice.exe --self-test` writes test-results.txt. `--ui-smoke` tests the WPF pages and dialogs using temporary preferences. `--devices` writes active Windows capture/render devices to devices.txt. `--preview` renders the WPF Studio to preview.png without starting audio. `--audio-smoke` runs B1 capture/DSP with a muted CABLE output for four seconds and writes a hardware-check report; it neither stores nor transmits microphone audio.

Offline checks validate numerical stability, extreme gain limiting, quiet signal survival, silence, resampling and PCM channel selection. They do not establish subjective voice quality or live round-trip latency. A live speech/whisper listening test is required to tune for your voice and room.

NAudio 2.2.1 is used under its MIT license and RNNoise v0.2 under its BSD license; see THIRD-PARTY.txt. Routing references: https://voicemeeter.com/quick-tips-voicemeeter-virtual-inputs-and-outputs-windows-10-and-up/ and https://vb-audio.com/Cable/ .







Windows startup: use **Start with Windows** in the shared sidebar to run this portable executable at sign-in. Keep the full portable folder at the same path. On normal launch, saved microphone processing, headphone EQ, and FramePilot live monitoring start automatically. Missing devices or unavailable Equalizer APO are reported in their audio pages; configure devices there and use the start button to retry. Isolated test/preview sessions keep audio inactive.
