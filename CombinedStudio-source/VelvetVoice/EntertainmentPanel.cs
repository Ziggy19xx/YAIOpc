using System.Windows.Forms;
using System.Drawing;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VelvetVoice;

public record ClipSource(string Id, string Name, bool Desktop)
{ public override string ToString() => (Desktop ? "Desktop · " : "Mic · ") + Name; }

public sealed partial class EntertainmentPanel : UserControl
{
    enum UtilityAction { Save, Stop, ToggleRolling, Replay }
    readonly Form host;
    readonly Func<SessionMixer?> getMixer;
    readonly Func<string?> getOutputId;
    readonly Color ink = Color.FromArgb(224, 232, 238), panel = Color.FromArgb(29, 38, 49);
    readonly string dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VelvetVoice");
    readonly SoundLibrary library;
    readonly ListView sounds = new();
    readonly ComboBox clipSource = new(), previewDevice = new(), clipFocus = new();
    readonly TrackBar seconds = new() { Minimum = 1, Maximum = 15, Value = 5, TickFrequency = 1 }, master = new() { Minimum = 0, Maximum = 100, Value = 70, TickStyle = TickStyle.None };
    readonly NumericUpDown volume = new() { Minimum = -24, Maximum = 6, DecimalPlaces = 1, Increment = .5M, Value = -3.1M };
    readonly Label rollingStatus = new(), clipLength = new(), boardStatus = new(), masterLabel = new();
    readonly Button rollingButton = new(), importButton = new(), clipKeyButton = new(), stopKeyButton = new(), rollingKeyButton = new(), replayKeyButton = new();
    readonly System.Windows.Forms.Timer ticker = new() { Interval = 200 };
    RollingCapture? rolling;
    readonly CheckBox monitorClips = new() { Text = "Hear clips in my headset", AutoSize = true };
    WasapiOut? monitor;
    MMDevice? monitorEndpoint;
    SessionMixer? monitorMixer;
    readonly EntertainmentState monitorState = new();
    WasapiOut? preview;
    MMDevice? previewEndpoint;
    GlobalKeys? keys;
    BoardConfig config = new(new());
    string? lastClip;
    bool busy, loading, disposed;
    public EntertainmentState State = new();
    public EntertainmentPanel(Form owner, Func<SessionMixer?> mixer, Func<string?> outputId, string? isolatedDataRoot = null)
    {
        if(isolatedDataRoot != null) dataRoot = isolatedDataRoot;
        host = owner; getMixer = mixer; getOutputId = outputId; library = new SoundLibrary(Path.Combine(dataRoot, "Sounds"));
        Dock = DockStyle.Fill; BackColor = Color.FromArgb(16, 23, 32); ForeColor = ink; Font = new Font("Segoe UI", 10);
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 9 };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 125));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 125));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        Controls.Add(page);
        page.Controls.Add(new Label { Text = "SOUNDBOARD + INSTANT REPLAY\nPlay sends to your Studio output. Preview sends only to your chosen headphones.", Font = new Font("Segoe UI", 12, FontStyle.Bold), Dock = DockStyle.Fill });
        var sourceRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sourceRow.Controls.Add(new Label { Text = "Rolling clip source", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
        StyleCombo(clipSource); clipSource.Dock = DockStyle.Fill; sourceRow.Controls.Add(clipSource); page.Controls.Add(sourceRow);
        var sliderRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        seconds.Dock = DockStyle.Fill; sliderRow.Controls.Add(seconds); clipLength.Dock = DockStyle.Fill; clipLength.TextAlign = ContentAlignment.MiddleCenter; sliderRow.Controls.Add(clipLength);
        seconds.ValueChanged += (_, _) => { clipLength.Text = $"Save the last {seconds.Value} seconds"; SaveBoard(); }; clipLength.Text = "Save the last 5 seconds"; page.Controls.Add(sliderRow);
        var focusRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        focusRow.Controls.Add(new Label { Text = "Speech focus", AutoSize = true, Padding = new Padding(0, 6, 12, 0) });
        StyleCombo(clipFocus); clipFocus.Width = 180; clipFocus.Items.AddRange(new object[] { "Off", "Natural", "Strong", "Voices only" }); clipFocus.SelectedIndex = 1;
        clipFocus.SelectedIndexChanged += (_, _) => SaveBoard(); focusRow.Controls.Add(clipFocus);
        focusRow.Controls.Add(new Label { Text = "Clip cleanup only · original retained · overlapping sounds may remain", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }); page.Controls.Add(focusRow);
        var clipActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        clipActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        clipActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        clipActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var clipButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        SetButton(rollingButton, "Start rolling", 130); rollingButton.Click += (_, _) => ToggleRolling();
        var save = Button("Save clip", 110, (_, _) => CaptureLast());
        var replay = Button("Replay last clip", 150, (_, _) => ReplayLast());
        clipButtons.Controls.AddRange(new Control[] { rollingButton, save, replay }); clipActions.Controls.Add(clipButtons);
        var clipKeys = new FlowLayoutPanel { Dock = DockStyle.Fill };
        SetButton(rollingKeyButton, "Start/stop key…", 200); rollingKeyButton.Click += (_, _) => SetUtilityKey(UtilityAction.ToggleRolling);
        SetButton(clipKeyButton, "Save clip key…", 200); clipKeyButton.Click += (_, _) => SetUtilityKey(UtilityAction.Save);
        SetButton(replayKeyButton, "Replay clip key…", 200); replayKeyButton.Click += (_, _) => SetUtilityKey(UtilityAction.Replay);
        clipKeys.Controls.AddRange(new Control[] { rollingKeyButton, clipKeyButton, replayKeyButton }); clipActions.Controls.Add(clipKeys);
        rollingStatus.Text = "Rolling capture is off. Nothing is recorded until you start it."; rollingStatus.Dock = DockStyle.Fill; rollingStatus.ForeColor = Color.FromArgb(148, 197, 192); clipActions.Controls.Add(rollingStatus); page.Controls.Add(clipActions);
        var soundActions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        SetButton(importButton, "Import sounds…", 160); importButton.Click += async (_, _) => await ImportSounds();
        var play = Button("Play selected", 140, (_, _) => { if (Selected is { } s) Play(s.Id); });
        var stop = Button("Stop all sounds", 145, (_, _) => StopAll());
        SetButton(stopKeyButton, "Stop shortcut…", 180); stopKeyButton.Click += (_, _) => SetUtilityKey(UtilityAction.Stop);
        soundActions.Controls.AddRange(new Control[] { importButton, play, stop, stopKeyButton }); page.Controls.Add(soundActions);
        sounds.Dock = DockStyle.Fill; sounds.View = View.Details; sounds.FullRowSelect = true; sounds.MultiSelect = false; sounds.HideSelection = false; sounds.BackColor = panel; sounds.ForeColor = ink;
        sounds.Columns.Add("Sound", 335); sounds.Columns.Add("Duration", 110); sounds.Columns.Add("Shortcut", 190); sounds.Columns.Add("Gain", 95);
        sounds.DoubleClick += (_, _) => { if (Selected is { } s) Play(s.Id); };
        sounds.SelectedIndexChanged += (_, _) => { if (Selected is { } s) { loading = true; volume.Value = Math.Clamp((decimal)(20 * Math.Log10(Math.Max(s.Gain, .0001f))), volume.Minimum, volume.Maximum); loading = false; } };
        page.Controls.Add(sounds);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill };
        options.Controls.Add(Button("Assign shortcut…", 140, (_, _) => BindSelected()));
        options.Controls.Add(Button("Clear shortcut", 110, (_, _) => { if (Selected is { } s) Replace(s with { Binding = null }); }));
        options.Controls.Add(Button("Rename…", 85, (_, _) => Rename()));
        options.Controls.Add(Button("Trim…", 75, (_, _) => TrimSelected()));
        options.Controls.Add(Button("Remove pad", 105, (_, _) => { if (Selected is { } s) { library.Remove(s); RefreshSounds(); RebindKeys(); SaveBoard(); } }));
        options.Controls.Add(new Label { Text = "Sound gain (dB)", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }); volume.Width = 85; volume.BackColor = panel; volume.ForeColor = ink; options.Controls.Add(volume);
        volume.ValueChanged += (_, _) => { if (!loading && Selected is { } s) Replace(s with { Gain = (float)Math.Pow(10, (double)volume.Value / 20) }); };
        var previewRow = new FlowLayoutPanel { Width = 810, Height = 36 };
        previewRow.Controls.Add(new Label { Text = "Headset output", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }); StyleCombo(previewDevice); previewDevice.Width = 370; previewRow.Controls.Add(previewDevice);
        previewRow.Controls.Add(Button("Preview", 100, (_, _) => PreviewSelected())); options.Controls.Add(previewRow);
        previewRow.Controls.Add(Button("Original", 110, (_, _) => PreviewSelected(original: true)));
        var volumeRow = new FlowLayoutPanel { Width = 810, Height = 40 };
        masterLabel.Text = "Soundboard level: 70%"; masterLabel.Width = 200; volumeRow.Controls.Add(masterLabel); master.Width = 370; master.Height = 30; volumeRow.Controls.Add(master);
        master.ValueChanged += (_, _) => { State.Master = master.Value / 100f; monitorState.Master = State.Master; masterLabel.Text = $"Soundboard level: {master.Value}%"; SaveBoard(); }; monitorClips.CheckedChanged += (_, _) => { if (!monitorClips.Checked) StopMonitor(); SaveBoard(); }; volumeRow.Controls.Add(monitorClips); options.Controls.Add(volumeRow); page.Controls.Add(options);
        boardStatus.Dock = DockStyle.Fill; boardStatus.Text = "Start processing on the Studio tab before using Play or shortcuts.\nClips and imported sounds are stored locally; nothing is uploaded."; page.Controls.Add(boardStatus);
        previewDevice.SelectedIndexChanged += (_, _) => { StopMonitor(); StopPreview(); SaveBoard(); };
        LoadDevices();
        ticker.Tick += (_, _) =>
        {
            if (rolling == null) return;
            if (rolling.Error is { } error) { StopRolling(); SetStatus("Rolling capture stopped: " + error); return; }
            rollingStatus.Text = $"ROLLING · {rolling.Seconds:0.0} / 15 seconds in memory. Save clip writes a soundboard WAV.";
        };
        ticker.Start(); host.Shown += async (_, _) => await LoadBoard();
    }
    SoundEntry? Selected => sounds.SelectedItems.Count == 0 ? null : sounds.SelectedItems[0].Tag as SoundEntry;
    void StyleCombo(ComboBox c) { c.DropDownStyle = ComboBoxStyle.DropDownList; c.BackColor = panel; c.ForeColor = ink; }
    void SetButton(Button b, string text, int width) { b.Text = text; b.Width = width; b.Height = 33; b.FlatStyle = FlatStyle.Flat; b.BackColor = panel; b.ForeColor = ink; b.FlatAppearance.BorderSize = 0; }
    Button Button(string text, int width, EventHandler click) { var b = new Button(); SetButton(b, text, width); b.Click += click; return b; }
    void SetStatus(string text) { if (!disposed) boardStatus.Text = text; }
    void LoadDevices()
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            string? defaultId = null;
            try { using var d = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); defaultId = d.ID; } catch { }
            foreach (var flow in new[] { DataFlow.Render, DataFlow.Capture })
                foreach (var d in e.EnumerateAudioEndPoints(flow, DeviceState.Active))
                {
                    using (d)
                    {
                        clipSource.Items.Add(new ClipSource(d.ID, d.FriendlyName, flow == DataFlow.Render));
                        if (flow == DataFlow.Render) previewDevice.Items.Add(new DeviceChoice(d.ID, d.FriendlyName));
                    }
                }
            for (int i = 0; i < clipSource.Items.Count; i++) if (clipSource.Items[i] is ClipSource s && s.Id == defaultId) clipSource.SelectedIndex = i;
            // A local preview is opt-in; do not choose speakers automatically.
            previewDevice.SelectedIndex = -1;
        }
        catch (Exception ex) { SetStatus("Device enumeration: " + ex.Message); }
    }
    public Control CreateVoicePage()
    {
        var page = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(32), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = BackColor, ForeColor = ink };
        page.Controls.Add(new Label { Text = "VOICE PLAYGROUND", Font = new Font("Segoe UI", 19, FontStyle.Bold), Width = 760, Height = 52 });
        page.Controls.Add(new Label { Text = "Small pitch shifts for clean character changes. Your Studio EQ still applies.", Width = 760, Height = 45 });
        var effect = new ComboBox { Width = 420, Name = "voiceEffect" }; StyleCombo(effect); effect.Items.AddRange(new object[] { "Off", "Low & smooth", "Bright", "Storyteller" }); effect.SelectedIndex = 0;
        effect.SelectedIndexChanged += (_, _) => { State.VoicePreset = effect.SelectedItem?.ToString() ?? "Off"; SaveBoard(); };
        page.Controls.Add(effect);
        page.Controls.Add(new Label { Text = "Low & smooth   −2 semitones, a deeper character\n\nBright                 +2 semitones, a lighter character\n\nStoryteller           −1 semitone, a subtle lower register\n\nOff                     Your original processed voice, no pitch shift", Width = 760, Height = 225, Padding = new Padding(0, 24, 0, 0) });
        page.Controls.Add(Button("Reset to original", 190, (_, _) => effect.SelectedIndex = 0));
        page.Controls.Add(new Label { Text = "Voice effects change only the live mic. Soundboard clips keep their original pitch.\n\nPitch processing adds roughly 37 ms at 48 kHz. Some voice-dependent artifacts\nare possible; these modest presets avoid extreme robot-style transformations.\n\nStart processing in Studio to use effects. Soundboard Stop stops sounds, not your mic.", Width = 760, Height = 180, Padding = new Padding(0, 25, 0, 0), ForeColor = Color.FromArgb(148, 197, 192) });
        return page;
    }
    ComboBox? EffectCombo => host.Controls.Find("voiceEffect", true).FirstOrDefault() as ComboBox;
    void ToggleRolling()
    {
        if (rolling != null) { StopRolling(); return; }
        StartRolling();
    }
    void StartRolling()
    {
        if (rolling != null) { SetStatus("Rolling is already running; its history was kept."); return; }
        if (clipSource.SelectedItem is not ClipSource source) { SetStatus("Choose a rolling source first."); return; }
        if (source.Desktop && source.Id == getOutputId()) { SetStatus("Choose your listening/headphone device, not the processed output, for desktop clips."); return; }
        try
        {
            rolling = new RollingCapture(source.Id, source.Desktop); rolling.Start();
            rollingButton.Text = "Stop rolling"; clipSource.Enabled = false;
            SaveBoard(); SetStatus("Rolling capture started. Save clip captures the previous seconds; it does not transmit until Play.");
        }
        catch (Exception ex) { rolling = null; SetStatus("Could not start rolling capture: " + ex.Message); }
    }
    void StopRolling() { var old = rolling; rolling = null; old?.Dispose(); rollingButton.Text = "Start rolling"; clipSource.Enabled = true; rollingStatus.Text = "Rolling capture off. In-memory history cleared."; }
    async void CaptureLast()
    {
        if (busy) { SetStatus("An import or clip cleanup is already in progress."); return; }
        if (rolling == null) { SetStatus("Start rolling before saving a clip."); return; }
        try
        {
            var clip = rolling.Snapshot(seconds.Value);
            if (clip.Samples.Length == 0) { SetStatus("No audio history yet. Let rolling capture run for a moment."); return; }
            string mode = clipFocus.SelectedItem?.ToString() ?? "Off";
            busy = true; SetStatus(mode == "Off" ? "Saving clip…" : "Applying speech focus locally…");
            var result = await Task.Run(() => SpeechFocus.Clean(clip, mode)); if (disposed) return;
            if (result.Clip.Samples.Length == 0) { SetStatus("No speech detected. No pad saved; try Natural or Off to keep the full capture."); return; }
            var entry = library.Add(result.Clip, "Clip " + DateTime.Now.ToString("HH-mm-ss"));
            if (mode != "Off") entry = library.SaveOriginal(entry, clip);
            lastClip = entry.Id;
            RefreshSounds(entry.Id); SaveBoard();
            SetStatus($"Saved {result.Clip.Seconds:0.0}s" + (mode == "Off" ? ". " : $" with {mode.ToLowerInvariant()} speech focus. Original retained. ") +
                (mode == "Strong" && result.SpeechFrames == 0 ? "No clear speech detected; compare Original or try Natural." : "Ready to replay through your microphone output."));
        }
        catch (Exception ex) { SetStatus("Clip failed: " + ex.Message); }
        finally { busy = false; }
    }
    async Task ImportSounds()
    {
        if (busy) return;
        using var dialog = new OpenFileDialog { Multiselect = true, Filter = "Audio files|*.mp3;*.wav;*.aiff;*.aif;*.m4a;*.wma|All files|*.*" };
        if (WpfDialogs.OpenFile(dialog,host) != DialogResult.OK) return;
        busy = true; importButton.Enabled = false;
        try
        {
            int imported = 0; var errors = new List<string>();
            foreach (string path in dialog.FileNames.Take(SoundLibrary.MaxSounds))
            {
                if (disposed) break;
                try
                {
                    SetStatus("Importing " + Path.GetFileName(path) + "…");
                    var clip = await Task.Run(() => SoundLibrary.Decode(path)); if (disposed) return;
                    library.Add(clip, Path.GetFileNameWithoutExtension(path)); imported++;
                }
                catch (Exception ex) { errors.Add(Path.GetFileName(path) + ": " + ex.Message); }
            }
            RefreshSounds(); SaveBoard(); SetStatus($"Imported {imported} sound(s). " + string.Join(" | ", errors.Take(2)));
        }
        finally { busy = false; if (!disposed) importButton.Enabled = true; }
    }
    void ReplayLast()
    {
        if (lastClip == null || !library.Clips.ContainsKey(lastClip)) { SetStatus("Save a clip first."); return; }
        Play(lastClip, restartReplay: true);
    }
    void Play(string id, bool restartReplay = false)
    {
        if (getMixer() is not { } mixer) { SetStatus("Start processing on Studio first. Play uses that microphone output."); return; }
        var entry = library.Entries.FirstOrDefault(s => s.Id == id); if (entry == null || !library.Clips.TryGetValue(id, out var clip)) return;
        try
        {
            clip = ClipTrim.Apply(clip, entry.TrimStart, entry.TrimEnd);
            mixer.Play(SoundLibrary.Resample(clip, mixer.WaveFormat.SampleRate), entry.Gain,
                group: restartReplay ? "instant-replay" : null, restart: restartReplay);
            string headsetStatus = MonitorClip(clip, entry.Gain, restartReplay);
            SetStatus((restartReplay ? "Restarted clip through microphone output: " : "Playing to microphone output: ") + entry.Name + headsetStatus);
        }
        catch (Exception ex) { SetStatus("Playback failed: " + ex.Message); }
    }
    string MonitorClip(AudioClip clip, float gain, bool restart)
    {
        if (!monitorClips.Checked) return "";
        if (previewDevice.SelectedItem is not DeviceChoice d) return " · Choose a headset output to hear it yourself.";
        if (d.Id == getOutputId() || d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase))
            return " · Choose physical headphones for headset playback.";
        try
        {
            if (monitor == null || monitor.PlaybackState == PlaybackState.Stopped)
            {
                StopMonitor(); using var devices = new MMDeviceEnumerator(); monitorEndpoint = devices.GetDevice(d.Id);
                int rate = monitorEndpoint.AudioClient.MixFormat.SampleRate;
                monitorMixer = new SessionMixer(new SilentInput(rate), monitorEndpoint.AudioClient.MixFormat.Channels, monitorState);
                monitor = new WasapiOut(monitorEndpoint, AudioClientShareMode.Shared, true, 50);
                monitor.Init(monitorMixer.ToWaveProvider()); monitor.Play();
            }
            monitorState.Master = State.Master;
            monitorMixer!.Play(SoundLibrary.Resample(clip, monitorMixer.WaveFormat.SampleRate), gain,
                group: restart ? "instant-replay" : null, restart: restart);
            return " · Also playing in your headset.";
        }
        catch (Exception ex) { StopMonitor(); return " · Headset playback failed: " + ex.Message; }
    }
    void StopMonitor()
    {
        try { monitor?.Stop(); } catch { }
        monitor?.Dispose(); monitor = null; monitorMixer = null;
        monitorEndpoint?.Dispose(); monitorEndpoint = null;
    }
    void PreviewSelected(bool original = false)
    {
        if (Selected is not { } entry || !library.Clips.TryGetValue(entry.Id, out var clip)) return;
        if (previewDevice.SelectedItem is not DeviceChoice d) { SetStatus("Choose a headphone preview device first."); return; }
        if (d.Id == getOutputId() || d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase))
        { SetStatus("For private preview choose physical headphones, not a cable or VoiceMeeter input."); return; }
        try
        {
            if (original)
            {
                if (entry.OriginalFile == null) { SetStatus("This pad has no separate original. Originals are retained for speech-focused clips."); return; }
                clip = SoundLibrary.Decode(library.Resolve(entry with { File = entry.OriginalFile }));
            }
            if (!original) clip = ClipTrim.Apply(clip, entry.TrimStart, entry.TrimEnd);
            PlayPrivate(clip, entry.Gain, entry.Name);
        }
        catch (Exception ex) { StopPreview(); SetStatus("Preview failed: " + ex.Message); }
    }
    void PlayPrivate(AudioClip clip, float gain, string name)
    {
        if (previewDevice.SelectedItem is not DeviceChoice d || d.Id == getOutputId() || d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)) { SetStatus("Choose physical headphones for preview."); return; }
        try
        {
            StopPreview(); using var e = new MMDeviceEnumerator(); previewEndpoint = e.GetDevice(d.Id);
            preview = new WasapiOut(previewEndpoint, AudioClientShareMode.Shared, true, 50);
            var provider = new NAudio.Wave.SampleProviders.VolumeSampleProvider(new MemoryAudio(clip)) { Volume = gain * State.Master };
            preview.Init(new ProtectedPreview(provider).ToWaveProvider()); preview.Play(); SaveBoard(); SetStatus("Private headphone preview: " + name);
        }
        catch (Exception ex) { StopPreview(); SetStatus("Preview failed: " + ex.Message); }
    }
    void StopPreview() { try { preview?.Stop(); } catch { } preview?.Dispose(); preview = null; previewEndpoint?.Dispose(); previewEndpoint = null; }
    void StopAll() { getMixer()?.StopAll(); StopMonitor(); StopPreview(); SetStatus("Soundboard stopped. Live microphone stays on."); }
    void RefreshSounds(string? select = null)
    {
        select ??= Selected?.Id; sounds.BeginUpdate(); sounds.Items.Clear();
        foreach (var s in library.Entries)
        {
            var row = new ListViewItem(s.Name) { Tag = s };
            row.SubItems.Add(library.Clips.TryGetValue(s.Id, out var clip) ? $"{ClipTrim.Duration(clip, s.TrimStart, s.TrimEnd):0.0}s" : "Unavailable");
            row.SubItems.Add(s.Binding?.ToString() ?? "—"); row.SubItems.Add($"{20 * Math.Log10(Math.Max(s.Gain, .0001f)):0.0} dB");
            sounds.Items.Add(row); if (s.Id == select) row.Selected = true;
        }
        sounds.EndUpdate();
    }
    void Replace(SoundEntry updated)
    {
        int i = library.Entries.FindIndex(s => s.Id == updated.Id); if (i < 0) return;
        library.Entries[i] = updated; RefreshSounds(updated.Id); RebindKeys(); SaveBoard();
    }
    void TrimSelected()
    {
        if (Selected is not { } entry || !library.Clips.TryGetValue(entry.Id, out var clip)) return;
        if(WpfDialogs.Active){var range=WpfDialogs.Trim(clip,entry,audio=>PlayPrivate(audio,entry.Gain,entry.Name));if(range is {} selection){StopAll();Replace(entry with{TrimStart=selection.Start,TrimEnd=selection.End});SetStatus("Pad shortened. Reopen Trim and choose Reset to restore the full clip.");}StopPreview();return;}
        using var dialog = new TrimDialog(clip, entry, audio => PlayPrivate(audio, entry.Gain, entry.Name));
        if (dialog.ShowDialog(host) == DialogResult.OK)
        {
            StopAll(); Replace(entry with { TrimStart = dialog.StartSeconds, TrimEnd = dialog.EndSeconds });
            SetStatus("Pad shortened. Reopen Trim and choose Reset to restore the full clip.");
        }
        StopPreview();
    }
    void Rename()
    {
        if (Selected is not { } entry) return;
        if(WpfDialogs.Active){var name=WpfDialogs.Rename(entry.Name);if(name!=null)Replace(entry with{Name=name});return;}
        using var dialog = new Form { Text = "Rename sound", Size = new Size(430, 160), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var text = new TextBox { Text = entry.Name, MaxLength = 80, Bounds = new Rectangle(20, 20, 370, 25) };
        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Bounds = new Rectangle(285, 65, 105, 32) }; dialog.Controls.AddRange(new Control[] { text, ok }); dialog.AcceptButton = ok;
        if (dialog.ShowDialog(host) == DialogResult.OK && !string.IsNullOrWhiteSpace(text.Text)) Replace(entry with { Name = text.Text.Trim() });
    }
    void BindSelected()
    {
        if (Selected is not { } s) return;
        var binding = KeyDialog.Ask(host, s.Binding); if (binding == null) return;
        if (library.Entries.Any(x => x.Id != s.Id && x.Binding == binding) || Enum.GetValues<UtilityAction>().Any(a => UtilityBinding(a) == binding))
        { SetStatus("That shortcut is already assigned on this board."); return; }
        int index = library.Entries.IndexOf(s);
        if (keys != null && !keys.Bind(index + 1, binding, () => Play(s.Id))) { SetStatus("Shortcut is invalid or duplicated on this board. Choose another combination."); return; }
        Replace(s with { Binding = binding }); SetStatus("Assigned " + binding + " to " + s.Name);
    }
    KeyBinding? UtilityBinding(UtilityAction action) => action switch
    {
        UtilityAction.Save => config.ClipKey, UtilityAction.Stop => config.StopKey,
        UtilityAction.ToggleRolling => config.RollingKey, _ => config.ReplayKey
    };
    static int UtilityId(UtilityAction action) => 100 + (int)action;
    Action UtilityHandler(UtilityAction action) => action switch
    {
        UtilityAction.Save => CaptureLast, UtilityAction.Stop => StopAll,
        UtilityAction.ToggleRolling => ToggleRolling, _ => ReplayLast
    };
    void SetUtilityKey(UtilityAction action)
    {
        var binding = KeyDialog.Ask(host, UtilityBinding(action), allowClear: true);
        if (!KeyDialog.Accepted) return;
        if (binding != null && (library.Entries.Any(x => x.Binding == binding) || Enum.GetValues<UtilityAction>().Any(a => a != action && UtilityBinding(a) == binding))) { SetStatus("That shortcut is already assigned."); return; }
        if (keys != null && !keys.Bind(UtilityId(action), binding, UtilityHandler(action))) { SetStatus("That shortcut is unavailable. Choose another combination."); return; }
        config = action switch
        {
            UtilityAction.Save => config with { ClipKey = binding }, UtilityAction.Stop => config with { StopKey = binding },
            UtilityAction.ToggleRolling => config with { RollingKey = binding }, _ => config with { ReplayKey = binding }
        };
        UpdateKeyLabels(); SaveBoard();
    }
    void UpdateKeyLabels()
    {
        clipKeyButton.Text = "Save: " + (config.ClipKey?.ToString() ?? "set key…");
        rollingKeyButton.Text = "Start/stop: " + (config.RollingKey?.ToString() ?? "set key…");
        replayKeyButton.Text = "Replay: " + (config.ReplayKey?.ToString() ?? "set key…");
        stopKeyButton.Text = "Stop: " + (config.StopKey?.ToString() ?? "set key…");
    }
    void RebindKeys()
    {
        if (!host.IsHandleCreated || disposed) return;
        keys?.Dispose(); keys = new GlobalKeys(host.Handle); int failed = 0;
        for (int i = 0; i < library.Entries.Count; i++) { var s = library.Entries[i]; if (!keys.Bind(i + 1, s.Binding, () => Play(s.Id))) failed++; }
        foreach (var action in Enum.GetValues<UtilityAction>()) if (!keys.Bind(UtilityId(action), UtilityBinding(action), UtilityHandler(action))) failed++;
        if (failed > 0) SetStatus($"{failed} shortcut(s) unavailable. Reassign them; check for invalid or duplicate bindings.");
    }

    async Task LoadBoard()
    {
        loading = true; busy = true; importButton.Enabled = false;
        try
        {
            string path = Path.Combine(dataRoot, "board.json");
            if (File.Exists(path)) config = JsonSerializer.Deserialize<BoardConfig>(File.ReadAllText(path)) ?? config;
            monitorClips.Checked = config.MonitorClips;
            seconds.Value = Math.Clamp(config.ClipSeconds, 1, 15); master.Value = (int)(Math.Clamp(config.Master, 0, 1) * 100); State.Master = master.Value / 100f;
            clipFocus.SelectedItem = clipFocus.Items.Contains(config.ClipFocus) ? config.ClipFocus : "Natural";
            State.VoicePreset = VoiceEffects.Factor(config.VoicePreset) == 1 ? "Off" : config.VoicePreset;
            if (EffectCombo is { } effect) effect.SelectedItem = State.VoicePreset;
            for (int i = 0; i < clipSource.Items.Count; i++) if (clipSource.Items[i] is ClipSource source && source.Id == config.ClipSource && source.Desktop == config.ClipDesktop) clipSource.SelectedIndex = i;
            for (int i = 0; i < previewDevice.Items.Count; i++) if (previewDevice.Items[i] is DeviceChoice d && d.Id == config.PreviewDevice) previewDevice.SelectedIndex = i;
            int missing = 0;
            foreach (var s in (config.Sounds ?? new()).Take(SoundLibrary.MaxSounds))
            {
                try
                {
                    string file = library.Resolve(s); var clip = await Task.Run(() => SoundLibrary.Decode(file));
                    if (disposed) return; library.LoadEntry(s with { Gain = Math.Clamp(s.Gain, .0631f, 1.9953f) }, clip);
                }
                catch { missing++; }
            }
            lastClip = config.LastClip != null && library.Clips.ContainsKey(config.LastClip) ? config.LastClip : library.Entries.LastOrDefault(s => s.Name.StartsWith("Clip "))?.Id;
            RefreshSounds(); UpdateKeyLabels(); RebindKeys();
            if (missing > 0) SetStatus($"{missing} sound(s) could not load. Original files remain in the Sounds folder.");
        }
        catch (Exception ex) { SetStatus("Library load: " + ex.Message); }
        finally { loading = false; busy = false; if (!disposed) importButton.Enabled = true; }
    }
    void SaveBoard()
    {
        if (loading || disposed) return;
        try
        {
            var source = clipSource.SelectedItem as ClipSource;
            config = config with { Sounds = library.Entries.ToList(), ClipSeconds = seconds.Value, ClipSource = source?.Id, ClipDesktop = source?.Desktop ?? true, Master = State.Master, VoicePreset = State.VoicePreset, PreviewDevice = (previewDevice.SelectedItem as DeviceChoice)?.Id, LastClip = lastClip != null && library.Clips.ContainsKey(lastClip) ? lastClip : null, ClipFocus = clipFocus.SelectedItem?.ToString() ?? "Off", MonitorClips = monitorClips.Checked };
            Directory.CreateDirectory(dataRoot);
            string path = Path.Combine(dataRoot, "board.json"); File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })); File.Move(path + ".tmp", path, true);
        }
        catch (Exception ex) { SetStatus("Could not save the soundboard: " + ex.Message); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed) { SaveBoard(); disposed = true; ticker.Dispose(); StopRolling(); StopMonitor(); StopPreview(); keys?.Dispose(); }
        base.Dispose(disposing);
    }
}

static class KeyDialog
{
    public static bool Accepted;
    public static KeyBinding? Ask(Form owner, KeyBinding? existing, bool allowClear = false)
    {
        if(WpfDialogs.Active)return WpfDialogs.Shortcut(existing,allowClear);
        Accepted = false; KeyBinding? result = existing;
        using var form = new Form { Text = "Global shortcut", ClientSize = new Size(450, 170), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = new Font("Segoe UI", 10) };
        form.Controls.Add(new Label { Text = "Click the field and press your shortcut. Ctrl+Alt+F1 works well.\nGlobal shortcuts may consume the same key inside your game.", Bounds = new Rectangle(18, 14, 415, 45) });
        var input = new TextBox { ReadOnly = true, Text = existing?.ToString() ?? "Press keys…", Bounds = new Rectangle(18, 66, 415, 28) };
        input.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true; e.Handled = true;
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
            result = new KeyBinding((int)e.KeyCode, (e.Alt ? 1 : 0) | (e.Control ? 2 : 0) | (e.Shift ? 4 : 0)); input.Text = result.ToString();
        };
        var save = new Button { Text = "Save", Bounds = new Rectangle(328, 117, 105, 32) };
        save.Click += (_, _) => { if (result != null) { Accepted = true; form.Close(); } };
        form.Controls.AddRange(new Control[] { input, save });
        if (allowClear) { var clear = new Button { Text = "Clear", Bounds = new Rectangle(210, 117, 105, 32) }; clear.Click += (_, _) => { result = null; Accepted = true; form.Close(); }; form.Controls.Add(clear); }
        form.ShowDialog(owner); return Accepted ? result : null;
    }
}






