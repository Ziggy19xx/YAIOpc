using System.Windows.Forms;
using System.Drawing;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VelvetVoice;
static class FeatureTests
{
    sealed class Constant(float level, int rate = 48000) : ISampleProvider
    {
        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(rate, 1);
        public int Read(float[] buffer, int offset, int count) { Array.Fill(buffer, level, offset, count); return count; }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
    static void Assert(bool value, string reason) { if (!value) throw new Exception(reason); }
    public static void Run(List<string> log)
    {
        var ring = new RollingAudio(100);
        var ramp = Enumerable.Range(0, 2000).Select(i => (float)i).ToArray(); ring.Append(ramp, ramp.Length, 20);
        var clip = ring.Snapshot(15, 20);
        Assert(clip.Samples.Length == 1500 && clip.Samples[0] == 500 && clip.Samples[^1] == 1999, "Rolling history ordering failed");
        var gap = ring.Snapshot(1, 22);
        Assert(gap.Samples.Length == 100 && gap.Samples.All(v => v == 0), "Silence gap replayed stale audio");
        var fresh = new RollingAudio(100); fresh.Append(new float[80], 80, .8);
        Assert(fresh.Snapshot(15, .8).Samples.Length == 80, "Partial clip duration incorrect");
        log.Add("PASS rolling clip: 15-second chronological window, silent gaps, partial history");
        var state = new EntertainmentState { Master = 1 };
        var mixer = new SessionMixer(new Constant(.1f), 2, state);
        var loud = new AudioClip(Enumerable.Repeat(.9f, 96000).ToArray(), 48000);
        for (int i = 0; i < 8; i++) mixer.Play(loud, 2);
        var output = new float[1937];
        for (int i = 0; i < 10; i++)
        {
            mixer.Read(output, 17, 1920);
            for (int j = 17; j < output.Length; j += 2)
            { Assert(float.IsFinite(output[j]) && Math.Abs(output[j]) <= .891251f, "Soundboard mix clipped"); Assert(output[j] == output[j + 1], "Mixer stereo differs"); }
        }
        mixer.StopAll();
        for (int i = 0; i < 80; i++) mixer.Read(output, 17, 1920);
        Assert(Math.Abs(output[^1] - .1f) < .0001f, "Stop all silenced the live microphone");
        log.Add("PASS eight overlapping sounds + mic: protected ceiling, stereo/offset correctness; Stop keeps mic live");
        var replayMixer = new SessionMixer(new Constant(.1f), 1, new EntertainmentState { Master = 1 });
        replayMixer.Play(new AudioClip(Enumerable.Repeat(.03f, 48000 * 6).ToArray(), 48000), 1);
        var replaySamples = new float[48000];
        Array.Fill(replaySamples, .02f, 0, 24000); Array.Fill(replaySamples, .12f, 24000, 24000);
        var replayClip = new AudioClip(replaySamples, 48000); var replayOutput = new float[960];
        replayMixer.Play(replayClip, 1, "instant-replay", true);
        for (int i = 0; i < 30; i++) replayMixer.Read(replayOutput, 0, 960);
        Assert(Math.Abs(replayOutput[^1] - .25f) < .0001, "Replay test did not reach old clip's later audio");
        for (int press = 0; press < 100; press++)
        {
            replayMixer.Play(replayClip, 1, "instant-replay", true); replayMixer.Read(replayOutput, 0, 960);
            Assert(Math.Abs(replayOutput[^1] - .15f) < .0001, "Replay stacked audio, failed to restart, or stopped unrelated sound/mic");
        }
        log.Add("PASS 100 rapid replay presses: old replay removed, clip restarts at beginning; other sounds and mic preserved");
        foreach (string name in new[] { "Deep", "Woman", "Announcer" })
        {
            using var effects = new VoiceEffects(); var block = new float[480];
            effects.Process(block, block.Length, 48000, name);
            Assert(SpinWait.SpinUntil(() => effects.IsReady || effects.Error != null, 15000) && effects.Error == null, "VoxShift startup failed: " + effects.Error);
            int crossings = 0; float previous = 0;
            for (int chunk = 0; chunk < 200; chunk++)
            {
                for (int i = 0; i < block.Length; i++) block[i] = (float)(.03 * Math.Sin(2 * Math.PI * 440 * (chunk * 480 + i) / 48000));
                effects.Process(block, block.Length, 48000, name);
                Thread.Sleep(10);
                Assert(effects.Error == null, effects.Error ?? "VoxShift worker failed");
                foreach (float value in block)
                {
                    Assert(float.IsFinite(value) && Math.Abs(value) < .2, "Pitch preset unstable");
                    if (chunk >= 100 && previous < 0 && value >= 0) crossings++;
                    previous = value;
                }
            }
            double expected = 440 * VoiceEffects.Factor(name);
            Assert(Math.Abs(crossings - expected) < 8, $"Pitch frequency incorrect: {name} got {crossings} expected {expected}");
            log.Add($"PASS {name}: 440 Hz shifted to ~{crossings} Hz, target {expected:0.0} Hz");
        }
        string root = Path.Combine(AppContext.BaseDirectory, "feature-test-assets"); Directory.CreateDirectory(root);
        string wav = Path.Combine(root, "tone.wav");
        var tone = new float[22050]; for (int i = 0; i < tone.Length; i++) tone[i] = (float)(.1 * Math.Sin(2 * Math.PI * 440 * i / 44100));
        using (var writer = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(44100, 1))) writer.WriteSamples(tone, 0, tone.Length);
        var decoded = SoundLibrary.Decode(wav); Assert(decoded.SampleRate == 48000 && Math.Abs(decoded.Seconds - .5) < .02, "WAV decode/resample failed");
        string mp3 = Path.Combine(root, "tone.mp3");
        using (var reader = new AudioFileReader(wav)) MediaFoundationEncoder.EncodeToMp3(reader, mp3, 128000);
        var mp3Clip = SoundLibrary.Decode(mp3); Assert(mp3Clip.Samples.Any(v => Math.Abs(v) > .01) && mp3Clip.Seconds > .4, "MP3 import failed");
        var library = new SoundLibrary(Path.Combine(root, "Library")); var entry = library.Add(decoded, "Round trip");
        var restored = SoundLibrary.Decode(library.Resolve(entry)); Assert(restored.Samples.SequenceEqual(decoded.Samples), "Saved clip changed samples");
        var serialized = System.Text.Json.JsonSerializer.Serialize(new BoardConfig(new() { entry with { Binding = new KeyBinding((int)Keys.F1, 3) } },
            RollingKey: new KeyBinding((int)Keys.F2, 3), ClipKey: new KeyBinding((int)Keys.F3, 3), ReplayKey: new KeyBinding((int)Keys.F4, 3), LastClip: entry.Id));
        var restoredConfig = System.Text.Json.JsonSerializer.Deserialize<BoardConfig>(serialized);
        Assert(restoredConfig?.Sounds[0].Binding?.Key == (int)Keys.F1 && restoredConfig.Sounds[0].Binding?.Modifiers == 3, "Soundboard persistence failed");
        Assert(restoredConfig != null && restoredConfig.RollingKey?.Key == (int)Keys.F2 && restoredConfig.ClipKey?.Key == (int)Keys.F3 && restoredConfig.ReplayKey?.Key == (int)Keys.F4 && restoredConfig.LastClip == entry.Id, "Replay/rolling shortcuts or last clip not persisted");
        var voiceConfig = new BoardConfig(new(), VoicePreset: "My voice", VoiceParameters: VoxPresets.For("Woman"), CustomVoices: new() { ["My voice"] = VoxPresets.For("Robot") });
        var voiceRestored = System.Text.Json.JsonSerializer.Deserialize<BoardConfig>(System.Text.Json.JsonSerializer.Serialize(voiceConfig));
        Assert(voiceRestored!.VoiceParameters!["pitch"].GetDouble() == 7 && voiceRestored.CustomVoices!["My voice"]["ring_enabled"].GetBoolean(), "VoxShift custom preset persistence");
        log.Add("PASS VoxShift parameters and custom presets serialize and restore");
        log.Add("PASS real MP3/WAV decode, 44.1→48 kHz import, lossless saved clip, persisted shortcuts");
        using (var a = new Form()) using (var b = new Form())
        using (var first = new GlobalKeys(a.Handle)) using (var second = new GlobalKeys(b.Handle))
        {
            int triggered = 0;
            var one = new KeyBinding((int)Keys.F12, 7); var two = new KeyBinding((int)Keys.F11, 7);
            Assert(first.Bind(1, one, () => triggered++), "Observer bind failed");
            Assert(second.Bind(1, one, () => { }), "Another observer could not use the same shortcut");
            Assert(first.Bind(2, two, () => { }), "Second observer binding failed");
            Assert(!first.Bind(1, two, () => triggered += 10), "Duplicate board binding accepted");
            var pressed = new HashSet<int>();
            first.Poll(pressed.Contains);
            pressed.UnionWith(new[] { (int)Keys.F12, (int)Keys.ControlKey, (int)Keys.Menu, (int)Keys.ShiftKey });
            first.Poll(pressed.Contains); first.Poll(pressed.Contains);
            Assert(triggered == 1, "Held key repeated or failed rebind lost original action");
            pressed.Remove((int)Keys.ShiftKey); first.Poll(pressed.Contains);
            pressed.Add((int)Keys.ShiftKey); first.Poll(pressed.Contains);
            Assert(triggered == 1, "Modifier change retriggered a held primary key");
            pressed.Remove((int)Keys.F12); first.Poll(pressed.Contains);
            pressed.Add((int)Keys.F12); first.Poll(pressed.Contains);
            Assert(triggered == 2, "Release and repress failed to retrigger");
            pressed.Clear(); first.Poll(pressed.Contains);
            Assert(first.Bind(3, new KeyBinding((int)Keys.Oemcomma, 0), () => triggered += 100), "Plain key bind failed");
            pressed.UnionWith(new[] { (int)Keys.Oemcomma, (int)Keys.ControlKey }); first.Poll(pressed.Contains);
            Assert(triggered == 2, "Extra modifier activated bare-key shortcut");
            pressed.Clear(); first.Poll(pressed.Contains);
            pressed.Add((int)Keys.Oemcomma); first.Poll(pressed.Contains, enabled: false);
            first.Poll(pressed.Contains);
            Assert(triggered == 2, "Dialog key activated shortcut");
            pressed.Clear(); first.Poll(pressed.Contains);
            pressed.Add((int)Keys.Oemcomma); first.Poll(pressed.Contains);
            Assert(triggered == 102, "Plain key did not activate after dialog/release");
            Assert(RegisterHotKey(b.Handle, 900, 0x4000, (uint)Keys.Oemcomma), "Velvet Audio reserved comma from Windows");
            try { Assert(first.Bind(3, new KeyBinding((int)Keys.Oemcomma, 0), () => { }), "Observer failed while another app owns hotkey"); }
            finally { UnregisterHotKey(b.Handle, 900); }
            Assert(first.Bind(1, null, () => { }), "Unbind failed");
            first.Dispose(); first.Poll(pressed.Contains);
        }
        log.Add("PASS nonblocking shortcuts: Windows can register observed comma; duplicate rollback, hold/repress, exact modifiers, modal pause, unbind/dispose");
        SpeechFocusTests.Run(log, root);
    }
}
