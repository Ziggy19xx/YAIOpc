using System.Windows.Forms;
using System.Drawing;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace VelvetVoice;
static class Tests
{
    sealed class MutedProvider(ISampleProvider source) : ISampleProvider
    {
        public WaveFormat WaveFormat => source.WaveFormat;
        public int Read(float[] buffer, int offset, int count)
        { source.Read(buffer, offset, count); Array.Clear(buffer, offset, count); return count; }
    }
    public static void AudioSmoke()
    {
        using var e = new MMDeviceEnumerator();
        using var input = e.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .First(d => d.FriendlyName.Contains("Voicemeeter Out B1"));
        using var output = e.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .First(d => d.FriendlyName.Contains("CABLE Input"));
        using var capture = new WasapiCapture(input, true, 20);
        var f = output.AudioClient.MixFormat;
        var stream = new VoiceBuffer(capture.WaveFormat.SampleRate, f.SampleRate, 1, 40);
        var state = new EntertainmentState { VoicePreset = "Low & smooth" };
        var mixer = new SessionMixer(stream, f.Channels, state);
        mixer.Play(new AudioClip(Enumerable.Repeat(.01f, f.SampleRate).ToArray(), f.SampleRate), .5f);
        long packets = 0, bytes = 0; Exception? fault = null;
        capture.DataAvailable += (_, a) => { stream.Add(a.Buffer, a.BytesRecorded, capture.WaveFormat, -1); Interlocked.Increment(ref packets); Interlocked.Add(ref bytes, a.BytesRecorded); };
        capture.RecordingStopped += (_, a) => { if (a.Exception != null) fault = a.Exception; };
        using var player = new WasapiOut(output, AudioClientShareMode.Shared, true, 40);
        player.PlaybackStopped += (_, a) => { if (a.Exception != null) fault = a.Exception; };
        player.Init(new MutedProvider(mixer).ToWaveProvider()); capture.StartRecording(); player.Play();
        Thread.Sleep(4000);
        capture.StopRecording(); player.Stop();
        using (var defaultOutput = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
        using (var rolling = new RollingCapture(defaultOutput.ID, true))
        {
            rolling.Start(); Thread.Sleep(1500); var history = rolling.Snapshot(1);
            Assert(history.Samples.Length == history.SampleRate, "Live desktop rolling history duration incorrect");
        }
        Assert(fault == null, "WASAPI fault: " + fault?.Message);
        Assert(packets > 0 && bytes > 0, "No capture packets received");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "audio-smoke-results.txt"),
            $"PASS 4-second live WASAPI B1 → DSP → voice preset + soundboard mixer → muted CABLE Input\nPASS desktop loopback rolling capture: exactly 1 second of recent history\nCapture packets: {packets}; bytes: {bytes}; queue: {stream.QueueMs} ms\nUnderruns: {stream.Underruns}; overruns: {stream.Overruns}; input clips: {stream.InputClips}\nOutput was muted for this hardware check; no captured microphone or desktop audio was saved. This does not verify subjective voice quality or in-game routing.\n");
    }
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        var log = new List<string>();
        foreach (int rate in new[] { 44100, 48000, 96000 })
        {
            var d = new VoiceDsp(rate); var s = new VoiceSettings(Input: 24, Output: 18, Bass: 8);
            float peak = 0;
            for (int i = 0; i < rate * 2; i++)
            {
                float x = i % 1700 == 0 ? 1 : (float)(.7 * Math.Sin(2 * Math.PI * 170 * i / rate));
                float y = d.Process(x, s); Assert(float.IsFinite(y), "Non-finite DSP output"); peak = Math.Max(peak, Math.Abs(y));
            }
            Assert(peak <= .891251f, "Limiter exceeded ceiling"); log.Add($"PASS {rate} Hz extreme gain + impulses: finite, ceiling {peak:0.000000}");
            d = new VoiceDsp(rate); double energy = 0;
            for (int i = 0; i < rate; i++)
            {
                float y = d.Process((float)(.0001 * Math.Sin(2 * Math.PI * 220 * i / rate)), new VoiceSettings());
                if (i > rate / 2) energy += y * y;
            }
            Assert(energy > 1e-7, "Quiet whisper was gated"); log.Add($"PASS {rate} Hz −80 dBFS signal survives");
            d = new VoiceDsp(rate);
            for (int i = 0; i < rate; i++) Assert(d.Process(0, new VoiceSettings()) == 0, "Silence generates noise");
            log.Add($"PASS {rate} Hz silence remains silent");
        }
        foreach (int inputRate in new[] { 44100, 48000, 96000 })
        {
            var b = new VoiceBuffer(inputRate, 48000, 2, 40);
            var format = WaveFormat.CreateIeeeFloatWaveFormat(inputRate, 2);
            var bytes = new byte[inputRate / 50 * 8]; var rendered = new float[1920];
            long totalFrames = 0;
            for (int block = 0; block < 400; block++)
            {
                for (int j = 0; j < bytes.Length; j += 8)
                {
                    float v = (float)(.05 * Math.Sin(2 * Math.PI * 220 * totalFrames++ / inputRate));
                    BitConverter.TryWriteBytes(bytes.AsSpan(j, 4), v); BitConverter.TryWriteBytes(bytes.AsSpan(j + 4, 4), v);
                }
                b.Add(bytes, bytes.Length, format, -1); b.Read(rendered, 0, rendered.Length);
                Assert(rendered.All(float.IsFinite), "Bad resample output");
                for (int k = 0; k < rendered.Length; k += 2) Assert(rendered[k] == rendered[k + 1], "Stereo mismatch");
            }
            Assert(b.Underruns == 0 && b.Overruns == 0, "Stable stream interrupted");
            Assert(b.OutputPeak > .001, "Stream output silent");
            log.Add($"PASS {inputRate} → 48000 Hz 8-second stereo stream: no interruptions, queue {b.QueueMs} ms");
            for (int j = 0; j < 100; j++) b.Read(rendered, 0, rendered.Length);
            Assert(b.Underruns > 0, "Starvation not reported");
        }
        // Verify channel selection and signed PCM decoding.
        var pcm = new VoiceBuffer(48000, 48000, 1, 20);
        var data = new byte[4800 * 4];
        for (int i = 0; i < data.Length; i += 4) { BitConverter.TryWriteBytes(data.AsSpan(i, 2), (short)16000); BitConverter.TryWriteBytes(data.AsSpan(i + 2, 2), (short)-8000); }
        pcm.Add(data, data.Length, new WaveFormat(48000, 16, 2), 1);
        Assert(Math.Abs(pcm.InputPeak - 8000f / 32768) < .001, "PCM channel selection failed"); log.Add("PASS PCM16 right-channel decoding");
        double ToneRms(double frequency, float bass)
        {
            var d = new VoiceDsp(48000);
            var settings = new VoiceSettings(Warmth: 0, Presence: 0, Air: 0, Ratio: 1, DeEss: 0, Bass: bass);
            double energy = 0;
            for (int i = 0; i < 48000; i++)
            {
                float y = d.Process((float)(.01 * Math.Sin(2 * Math.PI * frequency * i / 48000)), settings);
                if (i >= 24000) energy += y * y;
            }
            return Math.Sqrt(energy / 24000);
        }
        double boost = 20 * Math.Log10(ToneRms(90, 8) / ToneRms(90, 0));
        double cut = 20 * Math.Log10(ToneRms(90, -8) / ToneRms(90, 0));
        double mids = 20 * Math.Log10(ToneRms(1000, 8) / ToneRms(1000, 0));
        Assert(boost > 4 && boost < 8.1 && cut < -4 && cut > -8.1, "Bass shelf response incorrect");
        Assert(Math.Abs(mids) < .2, "Bass shelf changes midrange excessively");
        log.Add($"PASS bass shelf: 90 Hz boost {boost:0.00} dB / cut {cut:0.00} dB; 1 kHz change {mids:0.00} dB");
        var moving = new VoiceDsp(48000); float last = 0, maxStep = 0;
        for (int i = 0; i < 96000; i++)
        {
            float y = moving.Process((float)(.01 * Math.Sin(2 * Math.PI * 120 * i / 48000)),
                new VoiceSettings(Ratio: 1, DeEss: 0, Bass: i / 24000 % 2 == 0 ? -8 : 8));
            Assert(float.IsFinite(y), "Bass transition unstable");
            maxStep = Math.Max(maxStep, Math.Abs(y - last)); last = y;
        }
        Assert(maxStep < .004, "Bass slider introduced a large discontinuity");
        log.Add($"PASS live bass transitions: maximum sample step {maxStep:0.000000}");
        var legacy = System.Text.Json.JsonSerializer.Deserialize<VoiceSettings>("{\"Input\":2,\"Output\":1}");
        Assert(legacy != null && legacy.Bass == 0 && legacy.Input == 2, "Old settings migration failed");
        log.Add("PASS previous settings load with neutral bass");
        HeadphoneEqTests.Run(log);
        FeatureTests.Run(log);
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "test-results.txt"), log);
    }
}

