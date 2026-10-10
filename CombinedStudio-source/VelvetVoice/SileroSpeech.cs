using System.Windows.Forms;
using System.Drawing;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VelvetVoice;

public static class SileroSpeech
{
    // Each snapshot starts with fresh recurrent state and 64 samples of context.
    public static SpeechFocusResult Extract(AudioClip cleaned)
    {
        var audio = SoundLibrary.Resample(cleaned, 16000);
        using var options = new SessionOptions { InterOpNumThreads = 1, IntraOpNumThreads = 1 };
        using var session = new InferenceSession(Path.Combine(AppContext.BaseDirectory, "silero_vad.onnx"), options);
        int frames = (audio.Samples.Length + 511) / 512;
        var probabilities = new float[frames];
        var state = new float[256]; var context = new float[64];
        for (int f = 0; f < frames; f++)
        {
            var input = new float[576]; Array.Copy(context, input, 64);
            Array.Copy(audio.Samples, f * 512, input, 64, Math.Min(512, audio.Samples.Length - f * 512));
            var values = new[] {
                NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(input, new[] { 1, 576 })),
                NamedOnnxValue.CreateFromTensor("state", new DenseTensor<float>(state, new[] { 2, 1, 128 })),
                NamedOnnxValue.CreateFromTensor("sr", new DenseTensor<long>(new long[] {16000}, Array.Empty<int>()))
            };
            using var output = session.Run(values);
            probabilities[f] = output.First(v => v.Name == "output").AsTensor<float>().First();
            state = output.First(v => v.Name == "stateN").AsTensor<float>().ToArray();
            Array.Copy(input, 512, context, 0, 64);
        }
        // Hysteresis and a quarter-second hangover retain pauses inside a phrase.
        var spans = new List<(int Start, int End)>();
        int start = -1, silence = -1;
        for (int f = 0; f < frames; f++)
        {
            if (probabilities[f] >= .5f) { if (start < 0) start = f; silence = -1; }
            else if (start >= 0 && probabilities[f] < .35f)
            {
                if (silence < 0) silence = f;
                if (f - silence >= 8) { Add(start, silence); start = -1; silence = -1; }
            }
            else silence = -1;
        }
        if (start >= 0) Add(start, frames);
        void Add(int first, int last)
        {
            if (last - first < 3) return;
            // 160 ms lead-in and 224 ms tail guard consonants and quiet endings.
            int a = Math.Max(0, (first - 5) * 1536);
            int b = Math.Min(cleaned.Samples.Length, (last + 7) * 1536);
            if (spans.Count > 0 && a <= spans[^1].End) spans[^1] = (spans[^1].Start, Math.Max(b, spans[^1].End));
            else if (b > a) spans.Add((a, b));
        }
        var samples = new List<float>();
        foreach (var span in spans)
        {
            int length = span.End - span.Start, fade = Math.Min(240, length / 2);
            for (int i = 0; i < length; i++)
            {
                float gain = fade == 0 ? 1 : Math.Min(1, Math.Min(i, length - 1 - i) / (float)fade);
                samples.Add(cleaned.Samples[span.Start + i] * gain);
            }
        }
        return new(new AudioClip(samples.ToArray(), 48000), probabilities.Count(p => p >= .5f), frames);
    }
}

