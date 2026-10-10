using System.Windows.Forms;
using System.Drawing;
namespace VelvetVoice;

public static class ClipTrim
{
    public static (int Start, int End) Bounds(AudioClip clip, double start, double? end)
    {
        int count = clip.Samples.Length;
        if (count == 0) return (0, 0);
        if (!double.IsFinite(start)) start = 0;
        double last = end is double e && double.IsFinite(e) ? e : clip.Seconds;
        int a = (int)(Math.Clamp(start, 0, clip.Seconds) * clip.SampleRate);
        int b = (int)Math.Round(Math.Clamp(last, 0, clip.Seconds) * clip.SampleRate);
        a = Math.Min(a, count - 1); b = Math.Clamp(b, a + 1, count);
        return (a, b);
    }
    public static double Duration(AudioClip clip, double start, double? end)
    { var b = Bounds(clip, start, end); return (b.End - b.Start) / (double)clip.SampleRate; }
    public static AudioClip Apply(AudioClip clip, double start, double? end)
    {
        var b = Bounds(clip, start, end);
        if (b.Start == 0 && b.End == clip.Samples.Length) return clip;
        return new AudioClip(clip.Samples[b.Start..b.End], clip.SampleRate);
    }
}

public sealed class TrimDialog : Form
{
    readonly NumericUpDown start = new(), end = new();
    readonly Label duration = new();
    readonly Panel waveform = new() { Bounds = new Rectangle(20, 20, 650, 150), BackColor = Color.FromArgb(20, 29, 39) };
    readonly AudioClip clip;
    public double StartSeconds => (double)start.Value;
    public double EndSeconds => (double)end.Value;
    public TrimDialog(AudioClip audio, SoundEntry entry, Action<AudioClip> preview)
    {
        clip = audio; Text = "Trim pad · " + entry.Name; ClientSize = new Size(690, 330);
        StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(16, 23, 32); ForeColor = Color.White;
        Controls.Add(waveform); waveform.Paint += DrawWave;
        Controls.Add(new Label { Text = "Start (seconds)", Bounds = new Rectangle(20, 185, 135, 25) });
        Controls.Add(new Label { Text = "End (seconds)", Bounds = new Rectangle(280, 185, 125, 25) });
        foreach (var input in new[] { start, end }) { input.DecimalPlaces = 3; input.Increment = .01M; input.Maximum = (decimal)audio.Seconds; input.Width = 115; Controls.Add(input); }
        start.Location = new Point(155, 182); end.Location = new Point(410, 182);
        var bounds = ClipTrim.Bounds(audio, entry.TrimStart, entry.TrimEnd);
        start.Value = (decimal)bounds.Start / audio.SampleRate; end.Value = Math.Min(end.Maximum, (decimal)bounds.End / audio.SampleRate);
        duration.Bounds = new Rectangle(20, 220, 650, 26); Controls.Add(duration);
        start.ValueChanged += (_, _) => UpdateRange(); end.ValueChanged += (_, _) => UpdateRange();
        Button Make(string text, int x, int width, Action click)
        { var button = new Button { Text = text, Bounds = new Rectangle(x, 265, width, 35), ForeColor = Color.White, BackColor = Color.FromArgb(29, 38, 49), FlatStyle = FlatStyle.Flat }; button.Click += (_, _) => click(); Controls.Add(button); return button; }
        Make("Reset", 20, 90, () => { start.Value = 0; end.Value = (decimal)audio.Seconds; });
        Make("Preview selection", 120, 165, () => { if (Valid()) preview(ClipTrim.Apply(clip, StartSeconds, EndSeconds)); });
        var save = Make("Save trim", 435, 115, () => { if (Valid()) { DialogResult = DialogResult.OK; Close(); } });
        var cancel = Make("Cancel", 560, 110, () => { DialogResult = DialogResult.Cancel; Close(); });
        AcceptButton = save; CancelButton = cancel; UpdateRange();
    }
    bool Valid()
    {
        if (EndSeconds > StartSeconds && EndSeconds - StartSeconds >= 1.0 / clip.SampleRate) return true;
        duration.Text = "End must be after start."; return false;
    }
    void UpdateRange() { duration.Text = $"Selection: {Math.Max(0, EndSeconds - StartSeconds):0.000}s · original {clip.Seconds:0.000}s. Full audio is retained."; waveform.Invalidate(); }
    void DrawWave(object? sender, PaintEventArgs e)
    {
        if (clip.Samples.Length == 0) return;
        int width = waveform.Width, height = waveform.Height;
        float left = (float)(StartSeconds / clip.Seconds * width), right = (float)(EndSeconds / clip.Seconds * width);
        using var selection = new SolidBrush(Color.FromArgb(48, 105, 99));
        e.Graphics.FillRectangle(selection, left, 0, Math.Max(0, right - left), height);
        using var pen = new Pen(Color.FromArgb(139, 234, 217));
        for (int x = 0; x < width; x++)
        {
            int a = x * clip.Samples.Length / width, b = Math.Max(a + 1, (x + 1) * clip.Samples.Length / width);
            float peak = 0; for (int i = a; i < Math.Min(b, clip.Samples.Length); i++) peak = Math.Max(peak, Math.Abs(clip.Samples[i]));
            float size = Math.Max(1, peak * (height / 2 - 4)); e.Graphics.DrawLine(pen, x, height / 2 - size, x, height / 2 + size);
        }
    }
}



