using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VelvetVoice;

public record KeyBinding(int Key, int Modifiers)
{
    public override string ToString() => (Modifiers.HasFlagBits(2) ? "Ctrl+" : "") +
        (Modifiers.HasFlagBits(1) ? "Alt+" : "") + (Modifiers.HasFlagBits(4) ? "Shift+" : "") +
        (Modifiers.HasFlagBits(8) ? "Win+" : "") + ((Keys)Key).ToString();
}
static class Bits { public static bool HasFlagBits(this int value, int bits) => (value & bits) != 0; }
public record SoundEntry(string Id, string Name, string File, float Gain = .7f, KeyBinding? Binding = null, string? OriginalFile = null, double TrimStart = 0, double? TrimEnd = null);
public record BoardConfig(List<SoundEntry> Sounds, int ClipSeconds = 5, string? ClipSource = null,
    bool ClipDesktop = true, KeyBinding? ClipKey = null, KeyBinding? StopKey = null,
    float Master = .7f, string VoicePreset = "Off", string? PreviewDevice = null,
    KeyBinding? RollingKey = null, KeyBinding? ReplayKey = null, string? LastClip = null, string ClipFocus = "Natural", bool MonitorClips = false, Dictionary<string, JsonElement>? VoiceParameters = null, Dictionary<string, Dictionary<string, JsonElement>>? CustomVoices = null);
public sealed record AudioClip(float[] Samples, int SampleRate)
{
    public double Seconds => Samples.Length / (double)SampleRate;
}
public sealed class EntertainmentState
{
    public volatile float Master = .7f;
    public volatile string VoicePreset = "Off";
    public volatile string? VoiceParameters;
    public volatile string? VoiceError;
}

// Bounded ring keyed to elapsed time. Silent gaps occupy real timeline space.
public sealed class RollingAudio
{
    readonly object sync = new();
    readonly float[] ring;
    readonly int rate;
    long cursor;
    public RollingAudio(int sampleRate) { rate = sampleRate; ring = new float[rate * 15]; }
    void FillGap(long end)
    {
        if (end - cursor >= ring.Length) { Array.Clear(ring); cursor = end; return; }
        while (cursor < end) ring[(int)(cursor++ % ring.Length)] = 0;
    }
    public void Append(float[] samples, int count, double seconds)
    {
        lock (sync)
        {
            long end = Math.Max(0, (long)(seconds * rate));
            FillGap(Math.Max(cursor, end - count));
            for (int i = 0; i < count; i++) ring[(int)(cursor++ % ring.Length)] = samples[i];
        }
    }
    public AudioClip Snapshot(int seconds, double nowSeconds)
    {
        lock (sync)
        {
            FillGap(Math.Max(cursor, (long)(Math.Max(0, nowSeconds) * rate)));
            int length = (int)Math.Min(cursor, rate * Math.Clamp(seconds, 1, 15));
            var samples = new float[length];
            for (int i = 0; i < length; i++) samples[i] = ring[(int)((cursor - length + i) % ring.Length)];
            return new AudioClip(samples, rate);
        }
    }
}

public sealed class RollingCapture : IDisposable
{
    readonly MMDevice device;
    readonly WasapiCapture capture;
    readonly RollingAudio rolling;
    readonly Stopwatch clock = new();
    float[] decoded = Array.Empty<float>();
    public string? Error;
    public double Seconds => Math.Min(15, clock.Elapsed.TotalSeconds);
    public RollingCapture(string deviceId, bool desktop)
    {
        using var e = new MMDeviceEnumerator(); device = e.GetDevice(deviceId);
        capture = desktop ? new WasapiLoopbackCapture(device) : new WasapiCapture(device, true, 20);
        rolling = new RollingAudio(capture.WaveFormat.SampleRate);
        capture.DataAvailable += (_, a) =>
        {
            var f = capture.WaveFormat;
            int frames = a.BytesRecorded / f.BlockAlign;
            if (decoded.Length < frames) decoded = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float value = 0;
                for (int ch = 0; ch < f.Channels; ch++) value += PcmDecode.Sample(a.Buffer, i * f.BlockAlign + ch * (f.BitsPerSample / 8), f);
                decoded[i] = float.IsFinite(value) ? value / f.Channels : 0;
            }
            rolling.Append(decoded, frames, clock.Elapsed.TotalSeconds);
        };
        capture.RecordingStopped += (_, a) => { if (a.Exception != null) Error = a.Exception.Message; };
    }
    public void Start()
    {
        try { clock.Start(); capture.StartRecording(); }
        catch { Dispose(); throw; }
    }
    public AudioClip Snapshot(int seconds) => rolling.Snapshot(seconds, clock.Elapsed.TotalSeconds);
    public void Dispose() { clock.Stop(); try { capture.StopRecording(); } catch { } capture.Dispose(); device.Dispose(); }
}
static class PcmDecode
{
    public static float Sample(byte[] bytes, int p, WaveFormat f)
    {
        bool floating = f.Encoding == WaveFormatEncoding.IeeeFloat || f is WaveFormatExtensible e &&
            e.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
        return floating ? BitConverter.ToSingle(bytes, p) : f.BitsPerSample switch
        {
            16 => BitConverter.ToInt16(bytes, p) / 32768f,
            24 => ((bytes[p] | bytes[p + 1] << 8 | bytes[p + 2] << 16) << 8 >> 8) / 8388608f,
            32 => BitConverter.ToInt32(bytes, p) / 2147483648f,
            8 => (bytes[p] - 128) / 128f,
            _ => throw new NotSupportedException("Unsupported clip source format.")
        };
    }
}
public sealed class MemoryAudio : ISampleProvider
{
    readonly float[] samples;
    int position;
    public WaveFormat WaveFormat { get; }
    public MemoryAudio(AudioClip clip) { samples = clip.Samples; WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(clip.SampleRate, 1); }
    public int Read(float[] buffer, int offset, int count)
    {
        int n = Math.Min(count, samples.Length - position);
        Array.Copy(samples, position, buffer, offset, n); position += n; return n;
    }
}

public sealed class SoundLibrary
{
    public const int Rate = 48000, MaxSounds = 32, MaxTotalSamples = Rate * 300;
    readonly string root;
    public List<SoundEntry> Entries = new();
    public Dictionary<string, AudioClip> Clips = new(); // Only the UI thread mutates this dictionary.
    public SoundLibrary(string directory) { root = directory; }
    public static AudioClip Decode(string path)
    {
        using var reader = new AudioFileReader(path);
        if (reader.TotalTime.TotalSeconds > 120.1) throw new InvalidDataException("Use sound files no longer than two minutes.");
        ISampleProvider source = reader;
        if (source.WaveFormat.Channels == 2) source = new StereoToMonoSampleProvider(source) { LeftVolume = .5f, RightVolume = .5f };
        else if (source.WaveFormat.Channels != 1) throw new InvalidDataException("Import mono or stereo files.");
        if (source.WaveFormat.SampleRate != Rate) source = new WdlResamplingSampleProvider(source, Rate);
        var data = new List<float>(); var block = new float[8192]; int n;
        while ((n = source.Read(block, 0, block.Length)) > 0)
        {
            if (data.Count + n > Rate * 120) throw new InvalidDataException("Use sound files no longer than two minutes.");
            for (int i = 0; i < n; i++) data.Add(float.IsFinite(block[i]) ? Math.Clamp(block[i], -1, 1) : 0);
        }
        if (data.Count == 0) throw new InvalidDataException("This file contains no audio.");
        return new AudioClip(data.ToArray(), Rate);
    }
    public static AudioClip Resample(AudioClip clip, int rate)
    {
        if (clip.SampleRate == rate) return clip;
        var source = new WdlResamplingSampleProvider(new MemoryAudio(clip), rate);
        var data = new List<float>(); var block = new float[8192]; int n;
        while ((n = source.Read(block, 0, block.Length)) > 0) for (int i = 0; i < n; i++) data.Add(block[i]);
        return new AudioClip(data.ToArray(), rate);
    }
    public SoundEntry Add(AudioClip clip, string name)
    {
        clip = Resample(clip, Rate);
        if (Entries.Count >= MaxSounds) throw new InvalidOperationException("The board holds 32 sounds. Remove one before adding another.");
        if (Clips.Values.Sum(c => c.Samples.Length) + clip.Samples.Length > MaxTotalSamples) throw new InvalidOperationException("The board's five-minute audio budget is full. Remove a sound first.");
        Directory.CreateDirectory(root);
        string id = Guid.NewGuid().ToString("N"), filename = id + ".wav";
        using (var writer = new WaveFileWriter(Path.Combine(root, filename), WaveFormat.CreateIeeeFloatWaveFormat(Rate, 1))) writer.WriteSamples(clip.Samples, 0, clip.Samples.Length);
        var entry = new SoundEntry(id, name, filename); Entries.Add(entry); Clips[id] = clip; return entry;
    }
    public void LoadEntry(SoundEntry entry, AudioClip clip)
    {
        if (Entries.Count >= MaxSounds || Clips.Values.Sum(c => c.Samples.Length) + clip.Samples.Length > MaxTotalSamples) throw new InvalidOperationException("Library exceeds the soundboard size limit.");
        Entries.Add(entry); Clips[entry.Id] = clip;
    }
    public SoundEntry SaveOriginal(SoundEntry entry, AudioClip original)
    {
        original = Resample(original, Rate);
        var updated = entry with { OriginalFile = entry.Id + "-original.wav" };
        using (var writer = new WaveFileWriter(Resolve(updated with { File = updated.OriginalFile! }), WaveFormat.CreateIeeeFloatWaveFormat(Rate, 1)))
            writer.WriteSamples(original.Samples, 0, original.Samples.Length);
        int i = Entries.FindIndex(e => e.Id == entry.Id); if (i >= 0) Entries[i] = updated;
        return updated;
    }
    public string Resolve(SoundEntry entry)
    {
        if (entry.File != Path.GetFileName(entry.File) || entry.File.Contains('\\') || entry.File.Contains('/')) throw new InvalidDataException("Invalid library filename.");
        return Path.Combine(root, entry.File);
    }
    public void Remove(SoundEntry entry)
    {
        Entries.Remove(entry); Clips.Remove(entry.Id);
        // Preserve the source WAV; removing a pad must not destroy the recording.
    }
}

public sealed class PeakGuard
{
    readonly float[] delay, peaks;
    readonly long[] positions;
    int p, head, tail;
    long index;
    double gain = 1;
    readonly double release;
    public PeakGuard(int rate)
    {
        delay = new float[Math.Max(1, rate * 3 / 1000)]; peaks = new float[delay.Length + 2]; positions = new long[peaks.Length]; release = Math.Exp(-1 / (rate * .09));
    }
    public float Process(float x)
    {
        if (!float.IsFinite(x)) x = 0;
        float magnitude = Math.Abs(x);
        while (tail != head && peaks[(tail - 1 + peaks.Length) % peaks.Length] <= magnitude) tail = (tail - 1 + peaks.Length) % peaks.Length;
        peaks[tail] = magnitude; positions[tail] = index; tail = (tail + 1) % peaks.Length;
        while (head != tail && positions[head] < index - delay.Length) head = (head + 1) % peaks.Length;
        float sample = delay[p]; delay[p] = x; p = (p + 1) % delay.Length; index++;
        double needed = Math.Min(1, .89125094 / Math.Max(peaks[head], 1e-12));
        gain = needed < gain ? needed : needed + (gain - needed) * release;
        return (float)Math.Clamp(sample * gain, -.89125094, .89125094);
    }
}
public sealed class ProtectedPreview(ISampleProvider source) : ISampleProvider
{
    readonly PeakGuard guard = new(source.WaveFormat.SampleRate);
    int remaining = source.WaveFormat.SampleRate * 3 / 1000;
    bool ended;
    public WaveFormat WaveFormat => source.WaveFormat;
    public int Read(float[] buffer, int offset, int count)
    {
        int n = ended ? 0 : source.Read(buffer, offset, count);
        if (n < count)
        {
            ended = true;
            int tail = Math.Min(count - n, remaining); Array.Clear(buffer, offset + n, tail); remaining -= tail; n += tail;
        }
        for (int i = 0; i < n; i++) buffer[offset + i] = guard.Process(buffer[offset + i]);
        return n;
    }
}
public sealed class SessionMixer : ISampleProvider, IDisposable
{
    sealed class Playing(AudioClip clip, float gain, string? group)
    {
        public AudioClip Clip = clip; public float Gain = gain; public int Position; public int StopRemaining = -1;
        public string? Group = group;
    }
    readonly ISampleProvider mic;
    readonly EntertainmentState state;
    readonly VoiceEffects effects = new();
    readonly PeakGuard guard;
    readonly object sync = new();
    readonly List<Playing> playing = new();
    float[] mono = new float[16384];
    public float OutputPeak;
    public WaveFormat WaveFormat { get; }
    public SessionMixer(ISampleProvider microphone, int channels, EntertainmentState settings)
    {
        if (microphone.WaveFormat.Channels != 1) throw new ArgumentException("Mixer microphone must be mono.");
        mic = microphone; state = settings; guard = new PeakGuard(mic.WaveFormat.SampleRate);
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(mic.WaveFormat.SampleRate, channels);
    }
    public void Play(AudioClip clip, float gain, string? group = null, bool restart = false)
    {
        if (clip.SampleRate != WaveFormat.SampleRate) throw new ArgumentException("Resample sounds before queueing.");
        lock (sync)
        {
            if (restart && group != null) playing.RemoveAll(p => p.Group == group);
            if (playing.Count >= 8) throw new InvalidOperationException("Eight sounds are already playing. Stop sounds first.");
            playing.Add(new Playing(clip, gain, group));
        }
    }
    public void Dispose() => effects.Dispose();
    public void StopAll() { lock (sync) foreach (var p in playing) p.StopRemaining = Math.Max(1, WaveFormat.SampleRate / 100); }
    public int Read(float[] buffer, int offset, int count)
    {
        int frames = count / WaveFormat.Channels;
        if (mono.Length < frames) mono = new float[frames];
        int read = mic.Read(mono, 0, frames); Array.Clear(mono, read, frames - read);
        effects.Process(mono, frames, WaveFormat.SampleRate, mic is VoiceBuffer b && b.Settings.Bypass ? "Off" : state.VoicePreset, state.VoiceParameters);
        state.VoiceError = effects.Error;
        float peak = 0;
        lock (sync)
        {
            int fadeFrames = Math.Max(1, WaveFormat.SampleRate / 100);
            float master = state.Master;
            for (int frame = 0; frame < frames; frame++)
            {
                float x = mono[frame];
                foreach (var p in playing)
                {
                    if (p.Position >= p.Clip.Samples.Length || p.StopRemaining == 0) continue;
                    float fade = Math.Min(1, Math.Min(p.Position, p.Clip.Samples.Length - p.Position - 1) / (float)fadeFrames);
                    if (p.StopRemaining > 0) fade *= p.StopRemaining-- / (float)fadeFrames;
                    x += p.Clip.Samples[p.Position++] * p.Gain * master * fade;
                }
                float final = guard.Process(x); peak = Math.Max(peak, Math.Abs(final));
                for (int ch = 0; ch < WaveFormat.Channels; ch++) buffer[offset + frame * WaveFormat.Channels + ch] = final;
            }
            playing.RemoveAll(p => p.Position >= p.Clip.Samples.Length || p.StopRemaining == 0);
        }
        OutputPeak = Math.Max(peak, OutputPeak * .85f); return frames * WaveFormat.Channels;
    }
}

// Observe assigned keys without reserving or suppressing Windows input.
public sealed class GlobalKeys : IDisposable
{
    readonly Dictionary<int, Action> actions = new();
    readonly Dictionary<int, KeyBinding> bindings = new();
    readonly Dictionary<int, bool> held = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 10 };
    readonly IntPtr window;
    bool disposed;
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    static bool IsDown(int key) => GetAsyncKeyState(key) < 0;
    public GlobalKeys(IntPtr window)
    {
        this.window = window;
        timer.Tick += (_, _) => Poll(IsDown, Form.FromHandle(window)?.Enabled != false);
        timer.Start();
    }
    public bool Bind(int id, KeyBinding? binding, Action action)
    {
        if (disposed) return false;
        if (binding != null && (binding.Key < 1 || binding.Key > 255 || (binding.Modifiers & ~15) != 0 ||
            bindings.Any(p => p.Key != id && p.Value == binding))) return false;
        bindings.Remove(id); actions.Remove(id); held.Remove(id);
        if (binding == null) return true;
        bindings[id] = binding; actions[id] = action; held[id] = IsDown(binding.Key);
        return true;
    }
    internal void Poll(Func<int, bool> down, bool enabled = true)
    {
        if (disposed) return;
        int modifiers = (down((int)Keys.Menu) ? 1 : 0) | (down((int)Keys.ControlKey) ? 2 : 0) |
            (down((int)Keys.ShiftKey) ? 4 : 0) | ((down((int)Keys.LWin) || down((int)Keys.RWin)) ? 8 : 0);
        foreach (var pair in bindings.ToArray())
        {
            if (disposed) break;
            bool pressed = down(pair.Value.Key);
            bool wasHeld = held.GetValueOrDefault(pair.Key);
            held[pair.Key] = pressed;
            // A modifier change while holding the primary key must not retrigger.
            if (enabled && pressed && !wasHeld && modifiers == pair.Value.Modifiers &&
                actions.TryGetValue(pair.Key, out var action)) action();
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; timer.Stop(); timer.Dispose(); bindings.Clear(); actions.Clear(); held.Clear();
    }
}

// A silent input keeps the headset mix restricted to soundboard clips.
public sealed class SilentInput(int rate) : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(rate, 1);
    public int Read(float[] buffer, int offset, int count) { Array.Clear(buffer, offset, count); return count; }
}
