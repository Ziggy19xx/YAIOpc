using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace VelvetVoice;
static class SpeechFocusTests
{
    static void Assert(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    static double Rms(float[] samples, int offset, int length)
    { double e = 0; for (int i = offset; i < offset + length; i++) e += samples[i] * samples[i]; return Math.Sqrt(e / length); }
    public static void Run(List<string> log, string root)
    {
        var silence = new AudioClip(new float[48000 + 133], 48000);
        var silent = SpeechFocus.Clean(silence, "Strong");
        Assert(silent.Clip.Samples.Length == silence.Samples.Length && silent.Clip.Samples.All(v => Math.Abs(v) < 1e-8), "Speech focus changes duration or generates noise in silence");
        var random = new Random(404); var noise = new float[96000];
        for (int i = 0; i < noise.Length; i++) noise[i] = (float)((random.NextDouble() * 2 - 1) * .045);
        var cleanNoise = SpeechFocus.Clean(new AudioClip(noise, 48000), "Strong");
        double reduction = 20 * Math.Log10(Math.Max(1e-12, Rms(cleanNoise.Clip.Samples, 48000, 48000)) / Rms(noise, 48000, 48000));
        Assert(reduction < -10, $"Speech focus fails to reduce noise-only test: {reduction:0.0} dB, VAD {cleanNoise.SpeechFrames}/{cleanNoise.TotalFrames}");
        Assert(cleanNoise.Clip.Samples.All(v => float.IsFinite(v) && Math.Abs(v) <= 1), "Speech focus output invalid");
        log.Add($"PASS speech focus: silence/partial frames/duration; noise-only suppression {reduction:0.0} dB");
        foreach(string mode in new[]{"Natural","Strong"}){
            var fixture=new AudioClip(noise[..49133],48000);var python=SpeechFocus.Clean(fixture,mode);var native=SpeechFocus.CleanNative(fixture,mode);
            double delta=python.Clip.Samples.Zip(native.Clip.Samples,(a,b)=>Math.Abs(a-b)).Max();
            Assert(delta<.000001&&python.SpeechFrames==native.SpeechFrames&&python.TotalFrames==native.TotalFrames,"Python worker changed RNNoise alignment or processing: "+mode);
            log.Add($"PASS Python/native {mode} parity: partial frames, matching VAD counts, max sample error {delta:0.000000000}");
        }
        string speechPath = Path.Combine(root, "speech-fixture.wav");
        object? speakerObject = null, fileObject = null;
        try
        {
            speakerObject = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice")!);
            fileObject = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpFileStream")!);
            dynamic speaker = speakerObject!, file = fileObject!;
            file.Open(speechPath, 3, false); speaker.AudioOutputStream = file;
            speaker.Speak("This is a voice chat test. Keep the spoken words clear while reducing background sounds. Can you hear this sentence?", 0);
            file.Close();
        }
        finally
        {
            if (fileObject != null) Marshal.FinalReleaseComObject(fileObject);
            if (speakerObject != null) Marshal.FinalReleaseComObject(speakerObject);
        }
        var speech = SoundLibrary.Decode(speechPath);
        var mixed = new float[speech.Samples.Length + 96000];
        for (int i = 0; i < mixed.Length; i++) mixed[i] = (float)((random.NextDouble() * 2 - 1) * .018);
        for (int i = 0; i < speech.Samples.Length; i++) mixed[i + 48000] += speech.Samples[i] * .7f;
        // Synthetic impact bursts exercise transient interference; they are not real game recordings.
        for (int start = 96000; start < mixed.Length - 48000; start += 72000)
            for (int j = 0; j < 2400 && start + j < mixed.Length; j++) mixed[start + j] += (float)((random.NextDouble() * 2 - 1) * .5 * Math.Exp(-j / 200.0));
        var focused = SpeechFocus.Clean(new AudioClip(mixed, 48000), "Strong");
        Assert(focused.SpeechFrames > 10, "Voice detector missed synthesized speech");
        Assert(Rms(focused.Clip.Samples, 48000, speech.Samples.Length) > .003, "Speech cleanup erased the speech fixture");
        Assert(focused.Clip.Samples.Length == mixed.Length && focused.Clip.Samples.All(float.IsFinite), "Speech cleanup duration or stability failed");
        log.Add($"PASS synthesized speech + noise/impacts: {focused.SpeechFrames} speech frames detected; speech energy retained (subjective game quality unverified)");
        var voiceOnly = SpeechFocus.Clean(new AudioClip(mixed, 48000), "Voices only");
        Assert(voiceOnly.SpeechFrames > 5 && voiceOnly.Clip.Seconds > 1 && voiceOnly.Clip.Seconds < mixed.Length / 48000.0,
            "Silero failed to retain speech and remove background-only time");
        Assert(voiceOnly.Clip.Samples.All(float.IsFinite), "Silero produced invalid samples");
        var empty = SpeechFocus.Clean(silence, "Voices only");
        Assert(empty.Clip.Samples.Length == 0 && empty.SpeechFrames == 0, "Silero keeps speech-free silence");
        var again = SpeechFocus.Clean(new AudioClip(mixed, 48000), "Voices only");
        Assert(again.Clip.Samples.SequenceEqual(voiceOnly.Clip.Samples), "Silero recurrent state leaks across saved clips");
        log.Add($"PASS RNNoise → Silero: retained {voiceOnly.Clip.Seconds:0.00}s of {mixed.Length / 48000.0:0.00}s synthetic mix; silence discarded; independent snapshots");
        var trimSource = new AudioClip(Enumerable.Range(0, 1000).Select(i => i / 1000f).ToArray(), 1000);
        var trimmed = ClipTrim.Apply(trimSource, .2, .6);
        Assert(trimmed.Samples.Length == 400 && trimmed.Samples[0] == .2f && trimmed.Samples[^1] == .599f, "Trim boundaries incorrect");
        Assert(ClipTrim.Apply(trimSource, 0, null).Samples.Length == 1000 && trimSource.Samples.Length == 1000, "Trim altered original");
        var savedTrim = System.Text.Json.JsonSerializer.Deserialize<SoundEntry>(System.Text.Json.JsonSerializer.Serialize(new SoundEntry("id", "trim", "trim.wav", TrimStart: .2, TrimEnd: .6)))!;
        Assert(ClipTrim.Apply(trimSource, savedTrim.TrimStart, savedTrim.TrimEnd).Samples.SequenceEqual(trimmed.Samples), "Trim persistence failed");
        log.Add("PASS reversible trim: exact boundaries, original retained, reset and metadata persistence");
        var library = new SoundLibrary(Path.Combine(root, "SpeechLibrary"));
        var entry = library.Add(focused.Clip, "Focused test"); entry = library.SaveOriginal(entry, new AudioClip(mixed, 48000));
        var original = SoundLibrary.Decode(library.Resolve(entry with { File = entry.OriginalFile! }));
        Assert(original.Samples.SequenceEqual(mixed), "Retained original changed samples");
        log.Add("PASS speech-focused clip retains a lossless original for comparison");
    }
}


