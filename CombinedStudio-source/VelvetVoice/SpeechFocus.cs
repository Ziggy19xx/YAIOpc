using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;

namespace VelvetVoice;

public sealed record SpeechFocusResult(AudioClip Clip, int SpeechFrames, int TotalFrames);
public static class SpeechFocus
{
    const int Frame = 480;
    static readonly object nativeLock = new();
    [DllImport("rnnoise.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr rnnoise_create(IntPtr model);
    [DllImport("rnnoise.dll", CallingConvention = CallingConvention.Cdecl)] static extern void rnnoise_destroy(IntPtr state);
    [DllImport("rnnoise.dll", CallingConvention = CallingConvention.Cdecl)] static extern float rnnoise_process_frame(IntPtr state, [Out] float[] output, [In] float[] input);

    // Runs only on a saved snapshot, off the live microphone/render thread.
    public static SpeechFocusResult Clean(AudioClip raw, string mode)
    {
        if(mode == "Off") return new(raw,0,0);
        var source=SoundLibrary.Resample(raw,48000);
        if(source.Samples.Length==0)return new(source,0,0);
        var result=PythonSpeech.Clean(source,mode);
        return mode=="Voices only"?SileroSpeech.Extract(result.Clip):result;
    }
    // Retained only for deterministic offline parity tests.
    internal static SpeechFocusResult CleanNative(AudioClip raw, string mode)
    {
        if (mode == "Off") return new(raw, 0, 0);
        var source = SoundLibrary.Resample(raw, 48000);
        if (source.Samples.Length == 0) return new(source, 0, 0);
        lock (nativeLock)
        {
            IntPtr state = rnnoise_create(IntPtr.Zero);
            if (state == IntPtr.Zero) throw new InvalidOperationException("Speech focus could not allocate its model state.");
            try
            {
                int frames = (source.Samples.Length + Frame - 1) / Frame;
                var cleaned = new float[source.Samples.Length];
                var probability = new float[frames];
                var input = new float[Frame]; var output = new float[Frame];
                // RNNoise returns one frame of delayed audio. Flush and align it to the original.
                for (int f = 0; f <= frames; f++)
                {
                    Array.Clear(input);
                    int start = f * Frame;
                    if (start < source.Samples.Length)
                        for (int i = 0; i < Math.Min(Frame, source.Samples.Length - start); i++)
                            input[i] = Math.Clamp(source.Samples[start + i], -1, 1) * 32768;
                    float p = rnnoise_process_frame(state, output, input);
                    if (f == 0) continue;
                    int destination = (f - 1) * Frame;
                    probability[f - 1] = float.IsFinite(p) ? Math.Clamp(p, 0, 1) : 0;
                    for (int i = 0; i < Math.Min(Frame, cleaned.Length - destination); i++)
                        cleaned[destination + i] = float.IsFinite(output[i]) ? Math.Clamp(output[i] / 32768, -1, 1) : 0;
                }
                if (mode == "Voices only") return SileroSpeech.Extract(new AudioClip(cleaned, 48000));
                int detected = probability.Count(p => p >= .45f);
                if (mode == "Strong")
                {
                    var speech = new bool[frames];
                    for (int f = 0; f < frames; f++)
                        if (probability[f] >= .45f)
                            for (int j = Math.Max(0, f - 8); j <= Math.Min(frames - 1, f + 18); j++) speech[j] = true;
                    double envelope = speech[0] ? 1 : .01;
                    for (int i = 0; i < cleaned.Length; i++)
                    {
                        double target = speech[i / Frame] ? 1 : .01;
                        double coefficient = Math.Exp(-1 / (48000 * (target > envelope ? .005 : .03)));
                        envelope = target + (envelope - target) * coefficient;
                        cleaned[i] *= (float)envelope;
                    }
                }
                else
                    for (int i = 0; i < cleaned.Length; i++) cleaned[i] = cleaned[i] * .85f + source.Samples[i] * .15f;
                return new(new AudioClip(cleaned, 48000), detected, frames);
            }
            finally { rnnoise_destroy(state); }
        }
    }
}


