using System.Windows.Forms;
using System.Drawing;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Text.Json;

namespace VelvetVoice;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            try { Tests.Run(); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
            return;
        }
        if (args.Contains("--audio-smoke")) { Tests.AudioSmoke(); return; }
        if (args.Contains("--devices"))
        {
            using var devices = new MMDeviceEnumerator();
            var lines = new List<string>();
            foreach (var flow in new[] { DataFlow.Capture, DataFlow.Render })
                foreach (var d in devices.EnumerateAudioEndPoints(flow, DeviceState.Active))
                { using (d) lines.Add($"{flow}: {d.FriendlyName} | {d.AudioClient.MixFormat}"); }
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "devices.txt"), lines);
            return;
        }
        ApplicationConfiguration.Initialize();
        if (args.Contains("--preview-trim"))
        {
            var samples = Enumerable.Range(0, 48000 * 5).Select(i => (float)(.4 * Math.Sin(i * .04) * Math.Sin(i * .0003))).ToArray();
            using var trim = new TrimDialog(new AudioClip(samples, 48000), new SoundEntry("preview", "Example clip", "unused", TrimStart: .6, TrimEnd: 3.8), _ => { });
            trim.Show(); Application.DoEvents(); using var picture = new Bitmap(trim.Width, trim.Height);
            trim.DrawToBitmap(picture, new Rectangle(Point.Empty, trim.Size)); picture.Save(Path.Combine(AppContext.BaseDirectory, "preview-trim.png")); return;
        }
        if(!args.Contains("--legacy")) { StudioWindow.Run(args); return; }
        using var form = new MainForm();
        if (args.Contains("--preview") || args.Contains("--preview-board") || args.Contains("--preview-effects"))
        {
            form.PreviewSection(args.Contains("--preview-board") ? 1 : args.Contains("--preview-effects") ? 2 : 0);
            form.Show(); Application.DoEvents();
            using var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
            bmp.Save(Path.Combine(AppContext.BaseDirectory, args.Contains("--preview-board") ? "preview-board.png" : args.Contains("--preview-effects") ? "preview-effects.png" : "preview.png")); return;
        }
        Application.Run(form);
    }
}

public record SavedConfig(string? Input, string? Output, int Channel, int Buffer, VoiceSettings Voice);
public record DeviceChoice(string Id, string Name) { public override string ToString() => Name; }

public sealed partial class MainForm : Form
{
    readonly ComboBox inputs = new(), outputs = new(), channel = new(), latency = new(), presets = new();
    readonly Button start = new(), bypass = new();
    readonly Label status = new(), meters = new(), route = new();
    readonly Dictionary<string, TrackBar> sliders = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    WasapiCapture? capture;
    WasapiOut? player;
    MMDevice? inputDevice, outputDevice;
    VoiceBuffer? stream;
    SessionMixer? mixer;
    EntertainmentPanel? entertainment;
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    bool bypassed, closing, changing, stopping;
    readonly string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VelvetVoice", "settings.json");
    readonly Color ink = Color.FromArgb(224, 232, 238), panel = Color.FromArgb(29, 38, 49);

    public MainForm(string? isolatedDataRoot = null)
    {
        if(isolatedDataRoot != null) configPath = Path.Combine(isolatedDataRoot,"settings.json");
        Text = "Velvet Audio • SM7B studio processor"; Size = new Size(940, 910);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        MinimumSize = new Size(820, 810); BackColor = Color.FromArgb(16, 23, 32); ForeColor = ink;
        Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterScreen;
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), ColumnCount = 1, RowCount = 9 };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 134));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        var studioTab = new TabPage("Studio") { BackColor = BackColor, ForeColor = ink };
        studioTab.Controls.Add(page); tabs.TabPages.Add(studioTab); Controls.Add(tabs);
        var title = new Label { Text = "VELVET VOICE\nBroadcast character. Every whisper intact.", Font = new Font("Segoe UI", 17, FontStyle.Bold), Dock = DockStyle.Fill };
        var branding = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        branding.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
        branding.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        using (var logoStream = typeof(MainForm).Assembly.GetManifestResourceStream("VelvetVoice.Assets.logo.png"))
        {
            if (logoStream != null)
            {
                using var image = Image.FromStream(logoStream);
                branding.Controls.Add(new PictureBox { Image = new Bitmap(image), SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 8) }, 0, 0);
            }
        }
        branding.Controls.Add(title, 1, 0); page.Controls.Add(branding);
        var devices = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        devices.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); devices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddDeviceRow(devices, "Microphone input", inputs, 0);
        AddDeviceRow(devices, "Processed output", outputs, 1);
        var refresh = MakeButton("Refresh devices"); refresh.Click += (_, _) => RefreshDevices();
        devices.Controls.Add(refresh, 1, 2); page.Controls.Add(devices);
        var opts = new FlowLayoutPanel { Dock = DockStyle.Fill };
        opts.Controls.Add(new Label { Text = "Input channel", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        StyleCombo(channel, 160); channel.Items.AddRange(new object[] { "Mono mix", "Left / channel 1", "Right / channel 2" }); channel.SelectedIndex = 0;
        opts.Controls.Add(channel);
        opts.Controls.Add(new Label { Text = "Buffer", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        StyleCombo(latency, 180); latency.Items.AddRange(new object[] { "Responsive · 20 ms", "Balanced · 40 ms", "Stable · 80 ms" }); latency.SelectedIndex = 1;
        opts.Controls.Add(latency); page.Controls.Add(opts);
        var presetRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        presetRow.Controls.Add(new Label { Text = "Voice character", AutoSize = true, Padding = new Padding(0, 8, 10, 0) });
        StyleCombo(presets, 220); presets.Items.AddRange(new object[] { "Broadcast · balanced", "Natural · transparent", "Intimate · whisper", "Custom" });
        presetRow.Controls.Add(presets); page.Controls.Add(presetRow);
        var controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 9, BackColor = panel, Padding = new Padding(12) };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        AddSlider(controls, "Input gain", -12, 24, 0, 0);
        AddSlider(controls, "Bass · smooth shelf", -8, 8, 0, 1);
        AddSlider(controls, "Warmth · 140 Hz", -4, 4, 1.5f, 2);
        AddSlider(controls, "Presence · 3.3 kHz", -3, 5, 2.5f, 3);
        AddSlider(controls, "Air · 9.5 kHz", -3, 4, 1, 4);
        AddSlider(controls, "Comp threshold", -36, -8, -20, 5);
        AddSlider(controls, "Comp ratio", 1, 4, 2.5f, 6);
        AddSlider(controls, "De-ess maximum", 0, 6, 3, 7);
        AddSlider(controls, "Output gain", -12, 18, 0, 8); page.Controls.Add(controls);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 9, 0, 0) };
        start.Text = "Start processing"; start.Width = 190; StyleButton(start); start.BackColor = Color.FromArgb(46, 133, 121);
        start.Click += (_, _) => { if (capture == null) StartAudio(); else StopAudio(); };
        bypass.Text = "Compare: processed"; bypass.Width = 220; StyleButton(bypass);
        bypass.Click += (_, _) => { bypassed = !bypassed; bypass.Text = bypassed ? "Compare: original" : "Compare: processed"; UpdateSettings(); };
        var guide = MakeButton("Setup guide"); guide.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "README.md")) { UseShellExecute = true });
        actions.Controls.AddRange(new Control[] { start, bypass, guide }); page.Controls.Add(actions);
        meters.Dock = DockStyle.Fill; meters.Font = new Font("Consolas", 10); meters.Text = "INPUT  — dBFS     OUTPUT  — dBFS     REDUCTION  — dB\nQueue — ms · underruns 0 · overruns 0"; page.Controls.Add(meters);
        route.Dock = DockStyle.Fill; route.ForeColor = Color.FromArgb(148, 197, 192);
        route.Text = "SM7B → Wave Link → VoiceMeeter B1 → Velvet Audio → CABLE Input\nIn your game, select CABLE Output as the microphone.\nKeep gates, suppression and automatic gain off upstream and in the game."; page.Controls.Add(route);
        status.Dock = DockStyle.Fill; status.Text = "Ready • no gate / no noise suppression • peak ceiling −1 dBFS"; page.Controls.Add(status);
        presets.SelectedIndexChanged += (_, _) => ApplyPreset(); presets.SelectedIndex = 0;
        outputs.SelectedIndexChanged += (_, _) => UpdateRoute();
        RefreshDevices(); LoadConfig();
        entertainment = new EntertainmentPanel(this, () => mixer, () => (outputs.SelectedItem as DeviceChoice)?.Id, isolatedDataRoot);
        var boardTab = new TabPage("Soundboard + clips") { BackColor = BackColor };
        boardTab.Controls.Add(entertainment); tabs.TabPages.Add(boardTab);
        var voiceTab = new TabPage("Voice changer") { BackColor = BackColor };
        voiceTab.Controls.Add(entertainment.CreateVoicePage()); tabs.TabPages.Add(voiceTab);
        timer.Tick += (_, _) => UpdateMeters(); timer.Start();
        FormClosing += (_, _) => { closing = true; SaveConfig(); StopAudio(); timer.Dispose(); };
    }
    public void PreviewSection(int section) => tabs.SelectedIndex = section;
    void StyleCombo(ComboBox c, int width) { c.DropDownStyle = ComboBoxStyle.DropDownList; c.Width = width; c.BackColor = panel; c.ForeColor = ink; }
    void StyleButton(Button b) { b.Height = 34; b.FlatStyle = FlatStyle.Flat; b.BackColor = panel; b.ForeColor = ink; b.FlatAppearance.BorderSize = 0; }
    Button MakeButton(string text) { var b = new Button { Text = text, Width = 145 }; StyleButton(b); return b; }
    void AddDeviceRow(TableLayoutPanel p, string title, ComboBox c, int row)
    { p.Controls.Add(new Label { Text = title, AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 0, row); StyleCombo(c, 600); c.Dock = DockStyle.Fill; p.Controls.Add(c, 1, row); }
    void AddSlider(TableLayoutPanel p, string name, int min, int max, float value, int row)
    {
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 9));
        p.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        var s = new TrackBar { Minimum = min * 10, Maximum = max * 10, Value = (int)(value * 10), TickStyle = TickStyle.None, Dock = DockStyle.Fill, AutoSize = false, SmallChange = 1, LargeChange = 10 };
        var label = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        void Display() => label.Text = name == "Comp ratio" ? $"{s.Value / 10f:0.0}:1" : $"{s.Value / 10f:0.0} dB";
        s.ValueChanged += (_, _) => { Display(); if (!changing) presets.SelectedIndex = 3; UpdateSettings(); };
        Display(); sliders[name] = s; p.Controls.Add(s, 1, row); p.Controls.Add(label, 2, row);
    }
    float Value(string name) => sliders[name].Value / 10f;
    VoiceSettings Current() => new(Value("Input gain"), Value("Output gain"), Value("Warmth · 140 Hz"), Value("Presence · 3.3 kHz"), Value("Air · 9.5 kHz"), Value("Comp threshold"), Value("Comp ratio"), Value("De-ess maximum"), bypassed, Value("Bass · smooth shelf"));
    void UpdateSettings() { if (stream != null && sliders.Count == 9) Volatile.Write(ref stream.Settings, Current()); }
    void SetVoice(VoiceSettings s)
    {
        changing = true;
        var values = new[] { s.Input, s.Bass, s.Warmth, s.Presence, s.Air, s.Threshold, s.Ratio, s.DeEss, s.Output };
        int i = 0; foreach (var t in sliders.Values) t.Value = Math.Clamp((int)(values[i++] * 10), t.Minimum, t.Maximum);
        changing = false; UpdateSettings();
    }
    void ApplyPreset()
    {
        if (changing || presets.SelectedIndex == 3) return;
        // Presets do not silently add pre-gain or raise whispers into room noise.
        var s = presets.SelectedIndex switch
        {
            1 => new VoiceSettings(Warmth: 0, Presence: 1, Air: 0, Threshold: -16, Ratio: 1.5f, DeEss: 2),
            2 => new VoiceSettings(Warmth: 1, Presence: 2, Air: .5f, Threshold: -24, Ratio: 2, DeEss: 2),
            _ => new VoiceSettings()
        };
        SetVoice(s with { Input = Value("Input gain"), Output = Value("Output gain"), Bass = Value("Bass · smooth shelf") });
    }
    void RefreshDevices()
    {
        if (capture != null) { status.Text = "Stop processing before refreshing devices."; return; }
        string? oldIn = (inputs.SelectedItem as DeviceChoice)?.Id, oldOut = (outputs.SelectedItem as DeviceChoice)?.Id;
        inputs.Items.Clear(); outputs.Items.Clear();
        try
        {
            using var e = new MMDeviceEnumerator();
            foreach (var flow in new[] { DataFlow.Capture, DataFlow.Render })
                foreach (var d in e.EnumerateAudioEndPoints(flow, DeviceState.Active))
                { using (d) (flow == DataFlow.Capture ? inputs : outputs).Items.Add(new DeviceChoice(d.ID, d.FriendlyName)); }
            SelectDevice(inputs, oldIn, "Voicemeeter Out B1", "Voicemeeter Output");
            SelectDevice(outputs, oldOut, "CABLE Input");
            status.Text = outputs.SelectedIndex < 0 ? "Choose an output. VB-CABLE provides a microphone games can select." : "Devices ready. Check routing, then start.";
        }
        catch (Exception ex) { status.Text = "Device enumeration failed: " + ex.Message; }
    }
    static void SelectDevice(ComboBox c, string? id, params string[] preferred)
    {
        for (int i = 0; i < c.Items.Count; i++) if (c.Items[i] is DeviceChoice d && d.Id == id) { c.SelectedIndex = i; return; }
        foreach (var text in preferred)
            for (int i = 0; i < c.Items.Count; i++) if (c.Items[i] is DeviceChoice d && d.Name.Contains(text, StringComparison.OrdinalIgnoreCase)) { c.SelectedIndex = i; return; }
        // Never silently send a live microphone to the default speakers.
        c.SelectedIndex = -1;
    }
    void UpdateRoute()
    {
        string name = (outputs.SelectedItem as DeviceChoice)?.Name ?? "Choose an output device";
        route.Text = name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase)
            ? "SM7B → Wave Link → VoiceMeeter B1 → Velvet Audio → CABLE Input\nGame microphone: CABLE Output. Do not return that cable to B1.\nDisable gates / suppression / automatic gain upstream and in the game."
            : "Output: " + name + "\nFor standard VoiceMeeter, use CABLE Input; games select CABLE Output.\nHardware outputs are for monitoring. See the guide for B2 routing.";
    }
    void StartAudio()
    {
        if (inputs.SelectedItem is not DeviceChoice src || outputs.SelectedItem is not DeviceChoice dst)
        { status.Text = "Select both the input and processed output devices."; return; }
        try
        {
            using var e = new MMDeviceEnumerator();
            inputDevice = e.GetDevice(src.Id); outputDevice = e.GetDevice(dst.Id);
            int ms = latency.SelectedIndex switch { 0 => 20, 2 => 80, _ => 40 };
            capture = new WasapiCapture(inputDevice, true, Math.Max(10, ms / 2));
            var inputFormat = capture.WaveFormat; var renderFormat = outputDevice.AudioClient.MixFormat;
            if (inputFormat.BitsPerSample is not (16 or 24 or 32) || renderFormat.Channels > 8) throw new NotSupportedException("Unsupported device format. Set the device to 48 kHz / 16, 24 or 32 bit in Windows Sound settings.");
            stream = new VoiceBuffer(inputFormat.SampleRate, renderFormat.SampleRate, 1, ms) { Settings = Current() };
            mixer = new SessionMixer(stream, renderFormat.Channels, entertainment!.State);
            var activeStream = stream;
            int selected = channel.SelectedIndex - 1;
            capture.DataAvailable += (_, a) => activeStream.Add(a.Buffer, a.BytesRecorded, inputFormat, selected);
            capture.RecordingStopped += (_, a) => { if (!stopping) AudioStopped(activeStream, a.Exception, "Input stopped"); };
            player = new WasapiOut(outputDevice, AudioClientShareMode.Shared, true, ms);
            player.PlaybackStopped += (_, a) => { if (!stopping) AudioStopped(activeStream, a.Exception, "Output stopped"); };
            player.Init(mixer.ToWaveProvider()); capture.StartRecording(); player.Play();
            inputs.Enabled = outputs.Enabled = channel.Enabled = latency.Enabled = false;
            start.Text = "Stop processing";
            status.Text = $"LIVE • {inputFormat.SampleRate / 1000f:0.#} → {renderFormat.SampleRate / 1000f:0.#} kHz • {ms} ms buffer + device latency + 6 ms limiting";
            SaveConfig();
        }
        catch (Exception ex) { StopAudio(); status.Text = "Could not start: " + ex.Message; }
    }
    void AudioStopped(VoiceBuffer origin, Exception? exception, string fallback)
    {
        if (closing || !IsHandleCreated) return;
        try { BeginInvoke((Action)(() => { if (capture == null || !ReferenceEquals(stream, origin)) return; StopAudio(); status.Text = exception == null ? fallback + ". Select devices and restart." : "Audio stopped: " + exception.Message; })); }
        catch (InvalidOperationException) { }
    }
    void StopAudio()
    {
        stopping = true;
        var c = capture; var p = player; capture = null; player = null;
        try { c?.StopRecording(); } catch { }
        try { p?.Stop(); } catch { }
        c?.Dispose(); p?.Dispose(); inputDevice?.Dispose(); outputDevice?.Dispose();
        inputDevice = outputDevice = null; stream = null; mixer?.Dispose(); mixer = null;
        inputs.Enabled = outputs.Enabled = channel.Enabled = latency.Enabled = true;
        start.Text = "Start processing"; status.Text = "Stopped • settings retained"; stopping = false;
    }
    static string Db(float p) => p < .000001 ? " −∞" : $"{20 * Math.Log10(p),5:0.0}";
    void UpdateMeters()
    {
        if (stream is not { } s) return;
        meters.Text = $"INPUT {Db(s.InputPeak)} dBFS    OUTPUT {Db(mixer?.OutputPeak ?? s.OutputPeak)} dBFS    MIC REDUCTION {s.Reduction:0.0} dB\nQueue {s.QueueMs} ms · underruns {s.Underruns} · overruns {s.Overruns} · input clips {s.InputClips}";
        if (s.InputClips > 0) status.Text = "Input clipped: lower Wave Link/interface gain. Processing cannot repair clipped input.";
        else if (s.Underruns > 2 || s.Overruns > 0) status.Text = "Buffer interruptions detected. Stop and choose Stable; use 48 kHz on both devices.";
    }
    void SaveConfig()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(configPath)!); File.WriteAllText(configPath, JsonSerializer.Serialize(new SavedConfig((inputs.SelectedItem as DeviceChoice)?.Id, (outputs.SelectedItem as DeviceChoice)?.Id, channel.SelectedIndex, latency.SelectedIndex, Current() with { Bypass = false }), new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { status.Text = "Settings could not be saved: " + ex.Message; }
    }
    void LoadConfig()
    {
        try
        {
            if (!File.Exists(configPath)) return;
            var c = JsonSerializer.Deserialize<SavedConfig>(File.ReadAllText(configPath)); if (c == null) return;
            SelectDevice(inputs, c.Input, "Voicemeeter Out B1", "Voicemeeter Output"); SelectDevice(outputs, c.Output, "CABLE Input");
            channel.SelectedIndex = Math.Clamp(c.Channel, 0, 2); latency.SelectedIndex = Math.Clamp(c.Buffer, 0, 2);
            presets.SelectedIndex = 3; SetVoice(c.Voice);
        }
        catch { status.Text = "Saved settings unavailable; using defaults."; }
    }
}
