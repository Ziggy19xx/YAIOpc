using System.Windows.Forms;
using System.Drawing;
using NAudio.Dsp;
using NAudio.Wave;

namespace VelvetVoice;

public record VoiceSettings(float Input = 0, float Output = 0, float Warmth = 1.5f,
    float Presence = 2.5f, float Air = 1, float Threshold = -20, float Ratio = 2.5f,
    float DeEss = 3, bool Bypass = false, float Bass = 0);

// All DSP state belongs to the render thread. No gate, expander, denoiser or AGC.
public sealed class VoiceDsp
{
    readonly int rate;
    BiQuadFilter hp, warmth, mud, presence, sibilance;
    readonly Shelf air = new();
    readonly Shelf bass = new();
    double bassDb;
    readonly double bassSmoothing;
    int bassUpdate;
    VoiceSettings? previous;
    readonly float[] delay;
    readonly float[] peaks;
    readonly long[] indices;
    int position, head, tail;
    long sampleIndex;
    double env, sibEnv, fullEnv, compGain, essGain, limiterGain = 1, inputGain = 1, outputGain = 1, bypassMix;
    public float Reduction { get; private set; }
    public VoiceDsp(int sampleRate)
    {
        rate = sampleRate;
        bassSmoothing = 1 - Math.Exp(-1 / (rate * .06));
        bass.Set(rate, 160, .7, 0, low: true);
        delay = new float[Math.Max(1, rate * 3 / 1000)];
        peaks = new float[delay.Length + 2]; indices = new long[peaks.Length];
        hp = BiQuadFilter.HighPassFilter(rate, 65, .707f);
        warmth = BiQuadFilter.PeakingEQ(rate, 140, .7f, 1.5f);
        mud = BiQuadFilter.PeakingEQ(rate, 320, 1, -1.5f);
        presence = BiQuadFilter.PeakingEQ(rate, 3300, .8f, 2.5f);
        air.Set(rate, 9500, .7, 1);
        sibilance = BiQuadFilter.HighPassFilter(rate, 5500, .707f);
    }
    double Coefficient(double ms) => Math.Exp(-1 / (rate * ms * .001));
    static double Db(double value) => 20 * Math.Log10(Math.Max(value, 1e-12));
    static double Gain(double db) => Math.Pow(10, db / 20);
    public float Process(float raw, VoiceSettings s)
    {
        if (previous == null || previous.Warmth != s.Warmth || previous.Presence != s.Presence || previous.Air != s.Air)
        {
            warmth.SetPeakingEq(rate, 140, .7f, s.Warmth);
            presence.SetPeakingEq(rate, 3300, .8f, s.Presence);
            air.Set(rate, 9500, .7, s.Air);
        }
        previous = s;
        inputGain += (Gain(s.Input) - inputGain) * (1 - Coefficient(15));
        outputGain += (Gain(s.Output) - outputGain) * (1 - Coefficient(15));
        // Ramp bass over 60 ms and keep filter history for clean live slider moves.
        double bassTarget = Math.Clamp(s.Bass, -8, 8);
        if (Math.Abs(bassTarget - bassDb) > .00001)
        {
            bassDb += (bassTarget - bassDb) * bassSmoothing;
            if (++bassUpdate >= 32)
            { bass.Set(rate, 160, .7, bassDb, low: true); bassUpdate = 0; }
        }
        float x = air.Transform(presence.Transform(mud.Transform(warmth.Transform(bass.Transform(hp.Transform((float)(raw * inputGain)))))));
        double power = x * x;
        double c = Coefficient(power > env ? 8 : 160);
        env = c * env + (1 - c) * power;
        double over = Db(Math.Sqrt(env)) - s.Threshold;
        const double knee = 8;
        double reduction = over < -knee / 2 ? 0 : over > knee / 2 ? over * (1 - 1 / s.Ratio)
            : (1 - 1 / s.Ratio) * Math.Pow(over + knee / 2, 2) / (2 * knee);
        c = Coefficient(reduction > compGain ? 5 : 180);
        compGain = c * compGain + (1 - c) * reduction;
        float high = sibilance.Transform(x);
        c = Coefficient(5);
        sibEnv = c * sibEnv + (1 - c) * high * high;
        fullEnv = c * fullEnv + (1 - c) * power;
        double targetEss = Db(Math.Sqrt(fullEnv)) > -42 && sibEnv / Math.Max(fullEnv, 1e-12) > .32
            ? Math.Min(s.DeEss, Math.Max(0, Db(Math.Sqrt(sibEnv)) + 32) * .5) : 0;
        c = Coefficient(targetEss > essGain ? 2 : 65);
        essGain = c * essGain + (1 - c) * targetEss;
        x = (float)((x + high * (Gain(-essGain) - 1)) * Gain(-compGain) * outputGain);
        bypassMix += ((s.Bypass ? 1 : 0) - bypassMix) * (1 - Coefficient(10));
        x = (float)(x * (1 - bypassMix) + raw * bypassMix);
        // Monotonic peak queue: 3 ms lookahead with a -1 dBFS sample ceiling.
        float magnitude = Math.Abs(x);
        while (tail != head && peaks[(tail - 1 + peaks.Length) % peaks.Length] <= magnitude)
            tail = (tail - 1 + peaks.Length) % peaks.Length;
        peaks[tail] = magnitude; indices[tail] = sampleIndex; tail = (tail + 1) % peaks.Length;
        while (head != tail && indices[head] < sampleIndex - delay.Length) head = (head + 1) % peaks.Length;
        float delayed = delay[position]; delay[position] = x; position = (position + 1) % delay.Length;
        sampleIndex++;
        double needed = Math.Min(1, .89125094 / Math.Max(peaks[head], 1e-12));
        limiterGain = needed < limiterGain ? needed : needed + (limiterGain - needed) * Coefficient(90);
        Reduction = (float)(compGain * (1 - bypassMix) - Db(limiterGain));
        return (float)Math.Clamp(delayed * limiterGain, -.89125094, .89125094);
    }
}

// RBJ shelves. Updating coefficients keeps the filter history intact.
sealed class Shelf
{
    double b0, b1, b2, a1, a2, z1, z2;
    public void Set(int rate, double frequency, double slope, double db, bool low = false)
    {
        double a = Math.Pow(10, db / 40), w = 2 * Math.PI * frequency / rate;
        double c = Math.Cos(w), alpha = Math.Sin(w) / 2 * Math.Sqrt((a + 1 / a) * (1 / slope - 1) + 2);
        double t = 2 * Math.Sqrt(a) * alpha;
        if (low)
        {
            double denominatorLow = (a + 1) + (a - 1) * c + t;
            b0 = a * ((a + 1) - (a - 1) * c + t) / denominatorLow;
            b1 = 2 * a * ((a - 1) - (a + 1) * c) / denominatorLow;
            b2 = a * ((a + 1) - (a - 1) * c - t) / denominatorLow;
            a1 = -2 * ((a - 1) + (a + 1) * c) / denominatorLow;
            a2 = ((a + 1) + (a - 1) * c - t) / denominatorLow;
            return;
        }
        double denominator = (a + 1) - (a - 1) * c + t;
        b0 = a * ((a + 1) + (a - 1) * c + t) / denominator;
        b1 = -2 * a * ((a - 1) + (a + 1) * c) / denominator;
        b2 = a * ((a + 1) + (a - 1) * c - t) / denominator;
        a1 = 2 * ((a - 1) - (a + 1) * c) / denominator;
        a2 = ((a + 1) - (a - 1) * c - t) / denominator;
    }
    public float Transform(float x)
    {
        double y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y;
        return (float)y;
    }
}

public sealed class VoiceBuffer : ISampleProvider
{
    readonly object sync = new();
    readonly float[] ring;
    readonly int captureRate, outputRate, channels, target;
    readonly VoiceDsp dsp;
    int read, count;
    double phase;
    bool primed;
    public VoiceSettings Settings = new();
    public float InputPeak, OutputPeak, Reduction;
    public long Underruns, Overruns, InputClips;
    public WaveFormat WaveFormat { get; }
    public int QueueMs { get { lock (sync) return count * 1000 / captureRate; } }
    public VoiceBuffer(int inputRate, int renderRate, int renderChannels, int bufferingMs)
    {
        captureRate = inputRate; outputRate = renderRate; channels = renderChannels;
        target = inputRate * bufferingMs / 1000;
        ring = new float[inputRate]; dsp = new VoiceDsp(renderRate);
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(renderRate, renderChannels);
    }
    public void Add(byte[] bytes, int length, WaveFormat format, int selectedChannel)
    {
        int size = format.BitsPerSample / 8, stride = size * format.Channels;
        bool floating = format.Encoding == WaveFormatEncoding.IeeeFloat ||
            format is WaveFormatExtensible e && e.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
        lock (sync)
        {
            float peak = 0;
            for (int frame = 0; frame + stride <= length; frame += stride)
            {
                float value = 0;
                int first = selectedChannel < 0 ? 0 : Math.Min(selectedChannel, format.Channels - 1);
                int last = selectedChannel < 0 ? format.Channels : first + 1;
                for (int ch = first; ch < last; ch++)
                {
                    int p = frame + ch * size;
                    value += floating ? BitConverter.ToSingle(bytes, p) : size switch
                    {
                        2 => BitConverter.ToInt16(bytes, p) / 32768f,
                        3 => ((bytes[p] | bytes[p + 1] << 8 | bytes[p + 2] << 16) << 8 >> 8) / 8388608f,
                        4 => BitConverter.ToInt32(bytes, p) / 2147483648f,
                        _ => (bytes[p] - 128) / 128f
                    };
                }
                value /= last - first;
                if (!float.IsFinite(value)) value = 0;
                peak = Math.Max(peak, Math.Abs(value));
                if (Math.Abs(value) >= .999f) InputClips++;
                if (count == ring.Length) { read = (read + 1) % ring.Length; count--; Overruns++; }
                ring[(read + count) % ring.Length] = value; count++;
            }
            InputPeak = Math.Max(peak, InputPeak * .85f);
        }
    }
    public int Read(float[] buffer, int offset, int samples)
    {
        var settings = Volatile.Read(ref Settings);
        float peak = 0;
        lock (sync)
        {
            if (!primed && count >= target + 2) primed = true;
            // Correct independent device clock drift without periodically dropping audio blocks.
            double correction = Math.Clamp((count - target) / (double)captureRate * .04, -.003, .003);
            double step = captureRate / (double)outputRate * (1 + correction);
            bool starved = false;
            for (int i = 0; i < samples; i += channels)
            {
                float raw = 0;
                if (primed && count >= Math.Ceiling(step) + 2)
                {
                    raw = (float)(ring[read] * (1 - phase) + ring[(read + 1) % ring.Length] * phase);
                    phase += step;
                    int consumed = (int)phase; phase -= consumed;
                    read = (read + consumed) % ring.Length; count -= consumed;
                }
                else if (primed) { starved = true; primed = false; phase = 0; }
                float processed = dsp.Process(raw, settings);
                peak = Math.Max(peak, Math.Abs(processed));
                for (int ch = 0; ch < channels && i + ch < samples; ch++) buffer[offset + i + ch] = processed;
            }
            if (starved) Underruns++;
        }
        OutputPeak = Math.Max(peak, OutputPeak * .85f); Reduction = dsp.Reduction;
        return samples;
    }
}

