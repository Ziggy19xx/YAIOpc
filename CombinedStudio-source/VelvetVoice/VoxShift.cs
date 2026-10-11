using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace VelvetVoice;

public record VoxPreset(string Category, Dictionary<string, JsonElement> Parameters);
public static class VoxPresets
{
    public static readonly Dictionary<string, JsonElement> Defaults;
    public static readonly Dictionary<string, VoxPreset> Builtin = new();
    static VoxPresets()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "python", "voxshift", "presets.json")));
        Defaults = doc.RootElement.GetProperty("DEFAULTS").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        foreach (var p in doc.RootElement.GetProperty("PRESETS").EnumerateObject())
        {
            var values = new Dictionary<string, JsonElement>(Defaults);
            foreach (var v in p.Value.GetProperty("params").EnumerateObject()) values[v.Name] = v.Value.Clone();
            Builtin[p.Name] = new(p.Value.GetProperty("cat").GetString()!, values);
        }
    }
    public static Dictionary<string, JsonElement> For(string name) => new(Builtin.TryGetValue(name, out var preset) ? preset.Parameters : Defaults);
    public static (double Min, double Max) Range(string key) => key switch
    {
        "pitch" or "formant" => (-24, 24),
        "eq_low" or "eq_mid" or "eq_high" => (-18, 18),
        "gate_threshold" => (0, .2), "dist_drive" => (1, 50),
        "crush_bits" => (2, 16), "crush_downsample" => (1, 50),
        "ring_freq" => (0, 2000), "trem_rate" or "vib_rate" => (.1, 20),
        "vib_depth_ms" => (0, 15), "echo_time_ms" => (20, 1000),
        "echo_feedback" => (0, .95), "gain" => (0, 2), _ => (0, 1)
    };
    public static Dictionary<string, JsonElement> Sanitize(Dictionary<string, JsonElement>? values)
    {
        var result = new Dictionary<string, JsonElement>(Defaults);
        if (values == null) return result;
        foreach (var pair in values)
        {
            if (!Defaults.TryGetValue(pair.Key, out var d)) continue;
            if (d.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                if (pair.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) result[pair.Key] = pair.Value.Clone();
            }
            else if (pair.Value.ValueKind == JsonValueKind.Number && pair.Value.TryGetDouble(out double v) && double.IsFinite(v))
            {
                var range = Range(pair.Key); v = Math.Clamp(v, range.Min, range.Max);
                if (pair.Key is "crush_bits" or "crush_downsample") v = Math.Round(v);
                result[pair.Key] = JsonSerializer.SerializeToElement(v);
            }
        }
        return result;
    }
}

// The audio callback only touches bounded queues. Python and pipe I/O run on
// a background thread; a slow or failed worker can never block WASAPI.
public sealed class VoiceEffects : IDisposable
{
    record Work(float[] Samples, int Rate, string Settings, int Revision);
    readonly BlockingCollection<Work> requests = new(4);
    readonly Queue<float> output = new();
    readonly object sync = new();
    int revision, currentRate;
    string current = "";
    bool primed, disposed;
    volatile bool ready;
    Task? runner;
    Process? process;
    public volatile string? Error;
    internal bool IsReady => ready;
    public static float Factor(string name) => (float)Math.Pow(2, VoxPresets.For(name)["pitch"].GetDouble() / 12);
    public void Process(float[] samples, int count, int rate, string name, string? settings = null)
    {
        if (disposed || count == 0) return;
        string config = name == "Off" ? "" : settings ?? JsonSerializer.Serialize(VoxPresets.For(name));
        lock (sync)
        {
            if (config != current || rate != currentRate)
            {
                revision++; current = config; currentRate = rate; output.Clear(); primed = false;
                while (requests.TryTake(out _)) { }
            }
            if (config.Length == 0 || Error != null) return;
            runner ??= Task.Run(Run);
            if (!ready) return;
            if (count > 16384 || !requests.TryAdd(new(samples.Take(count).ToArray(), rate, config, revision)))
            { Error = "VoxShift cannot keep up. Effects bypassed; restart Studio."; output.Clear(); return; }
            if (!primed && output.Count >= rate / 25) primed = true;
            if (primed && output.Count < count) primed = false;
            for (int i = 0; i < count; i++) samples[i] = primed ? output.Dequeue() : 0;
        }
    }
    void Run()
    {
        try
        {
            string root = Path.Combine(AppContext.BaseDirectory, "python");
            var info = new ProcessStartInfo(Path.Combine(root, "runtime", "python.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add("-I"); info.ArgumentList.Add(Path.Combine(root, "voxshift", "worker.py"));
            process = System.Diagnostics.Process.Start(info)!;
            var errors = process.StandardError.ReadToEndAsync();
            var stream = process.StandardOutput.BaseStream;
            byte[] handshake = new byte[4];
            stream.ReadExactlyAsync(handshake).AsTask().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            if (Encoding.ASCII.GetString(handshake) != "VXS1") throw new IOException("Invalid VoxShift worker handshake.");
            ready = true;
            using var writer = new BinaryWriter(process.StandardInput.BaseStream, Encoding.UTF8, true);
            foreach (var work in requests.GetConsumingEnumerable())
            {
                var config = Encoding.UTF8.GetBytes(work.Settings);
                writer.Write(work.Rate); writer.Write(work.Samples.Length); writer.Write(config.Length); writer.Write(work.Revision); writer.Write(config);
                byte[] bytes = new byte[work.Samples.Length * 4]; Buffer.BlockCopy(work.Samples, 0, bytes, 0, bytes.Length);
                writer.Write(bytes); writer.Flush();
                stream.ReadExactlyAsync(bytes).AsTask().WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
                var result = new float[work.Samples.Length]; Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
                if (result.Any(x => !float.IsFinite(x) || Math.Abs(x) > 1.001)) throw new IOException("Invalid VoxShift audio.");
                lock (sync)
                {
                    if (work.Revision != revision || disposed) continue;
                    if (output.Count + result.Length > work.Rate / 4) throw new IOException("VoxShift output queue overflow.");
                    foreach (float sample in result) output.Enqueue(sample);
                }
            }
        }
        catch (Exception e) { if (!disposed) Error = "VoxShift effects bypassed: " + e.Message + " Restart Studio to retry."; }
        finally { try { if (process != null && !process.HasExited) process.Kill(true); } catch { } process?.Dispose(); }
    }
    public void Dispose()
    {
        lock (sync) { disposed = true; requests.CompleteAdding(); output.Clear(); }
        try { if (process != null && !process.HasExited) process.Kill(true); } catch { }
        try { runner?.Wait(2500); } catch { }
    }
}
