# Temporal clarity experiment

The complete app is published in `outputs/FramePilot-VelvetAudio-TemporalLab`. Motion Clarity has a button opening `TemporalLab.html` in the user's browser. `Open Temporal Lab.cmd` opens the same page directly. No server is needed to use the file locally. The old desktop shortcut continues pointing to the prior stable build.

The prototype draws its own alien patterns; it does not capture the user's desktop, inject into games, change monitor firmware, or copy TestUFO's shaders. Pulses start off. They stop on focus/visibility loss, Escape, discontinuous timing, and setting changes. Enabling requires a rolling animation cadence within 3% of the selected refresh and a 95th-percentile interval below 1.5 refresh intervals. These checks evaluate callbacks, not physical presentation; they do not prove every requested frame reaches the panel.

The four lanes are a native full-rate reference, a lower-rate sample-and-hold baseline, a one-refresh global pulse and a staggered-band pulse. Source/output ratios 2, 4 and 8 control target update rate and duty cycle. There is no brightness compensation; the output becomes darker. The known moving target enables independent row-band position sampling; this is not an arbitrary-game motion reconstruction algorithm.

At ideal fixed 480 Hz timing, 120fps hold lasts 8.33 ms while one-refresh pulses last 2.08 ms. This can reduce persistence blur relative to 120fps hold. Native 480fps also has a 2.08ms hold interval, so the model predicts no shorter exposure than the full-rate reference. Consequently this experiment does not establish an improvement for 700fps VALORANT on a 480Hz M10S. LCD response and display processing can further depart from the model.

Research reference: [TestUFO BFI](https://testufo.com/blackframes) and [TestUFO CRT scan simulator](https://testufo.com/crt). Software pulse/scan techniques already exist; this project makes no novelty claim and uses no AI inference model.

Validation: 24 native mathematical/rendering checks passed. The WPF renderer only reached about 267 callbacks/s on a requested 480Hz display, so it is retained as a development experiment (`--temporal-lab`), not the primary renderer. The browser renderer was used instead and its timing gate tested. Existing 22 motion-page checks passed. No physical-panel blur measurement, game latency measurement, pursuit-camera comparison or universal-monitor benefit was established. A successful timing gate is not evidence of a competitive advantage.

For a meaningful physical comparison, use fixed output Hz, confirm the browser keeps cadence, track one target with the eyes, and compare C/D with both B and A. A high-speed pursuit-camera test would provide stronger evidence. Screenshot sharpness cannot measure eye-tracking blur. Do not deploy a global game effect based on this demo alone.
