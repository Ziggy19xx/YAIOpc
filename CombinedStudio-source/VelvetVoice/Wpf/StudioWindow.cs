using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using Imaging=System.Windows.Media.Imaging;
using D=System.Windows.Threading;
using F=System.Windows.Forms;

namespace VelvetVoice;
internal sealed class StudioWindow
{
    internal static W.Window? Owner;
    readonly MainForm model;
    readonly HeadphoneEqPage headphoneEq;
    readonly UiControls ui=new();
    readonly Dictionary<string,W.FrameworkElement> pages=new();
    readonly Dictionary<string,C.RadioButton> navigation=new();
    readonly W.Window window;
    readonly W.Window hostWindow;
    internal event Action Activated;
    internal C.Grid Root=>(C.Grid)window.Content;
    internal C.RadioButton NavigationItem(string name)=>navigation[name];
    internal IEnumerable<C.RadioButton> NavigationItems=>navigation.Values;
    internal string SelectedPage=>current;
    internal MainForm Model=>model;
    internal void VerifyEq()=>headphoneEq.Verify();
    internal void VerifyVoice()
    {
        var board = model.UiBoard;
        var effect = model.UiVoicePage.Controls.Cast<F.Control>().OfType<F.ComboBox>().First();
        if (board.VoiceNames.Count() != 47 || board.VoiceNames.Contains("Bright")) throw new Exception("VoxShift preset replacement");
        effect.SelectedItem = "Woman";
        if (board.VoiceParameters["pitch"].GetDouble() != 7 || board.VoiceParameters["formant"].GetDouble() != 3) throw new Exception("VoxShift parameters");
        board.SetVoiceParameter("pitch", 9.0); board.SaveCustomVoice("Test custom");
        effect.SelectedItem = "Robot"; effect.SelectedItem = "Test custom";
        if (board.VoiceParameters["pitch"].GetDouble() != 9) throw new Exception("Custom VoxShift preset");
        effect.SelectedItem = "Hall";
        if (!board.VoiceParameters["reverb_enabled"].GetBoolean() || board.VoiceParameters["pitch"].GetDouble() != 0) throw new Exception("Effects-only preset");
        effect.SelectedItem = "Off";
    }
    internal void SelectPage(string name)=>Show(name);
    readonly bool isolatedSession;
    internal async Task Initialize(){await model.UiInitialize();if(closing)return;if(!isolatedSession){model.UiStartAudio();headphoneEq.AutoStart();}ui.Sync();}
    readonly D.DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(100)};
    string current="Studio";
    bool closing;
    T Get<T>(string name) where T:class=>(T)window.FindName(name);
    internal static void Run(string[] args)
    {
        bool preview=args.Any(x=>x.StartsWith("--preview",StringComparison.Ordinal))||args.Contains("--ui-smoke");
        string? isolated=preview?Path.Combine(Path.GetTempPath(),"VelvetVoice-ui-"+Guid.NewGuid().ToString("N")):null;
        var application=new W.Application{ShutdownMode=W.ShutdownMode.OnMainWindowClose};
        try{var studio=new StudioWindow(isolated);studio.window.Loaded+=async (_,_)=>{try{await studio.Initialize();if(preview)await studio.Preview(args);}catch(Exception error){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"ui-error.txt"),error.ToString());if(preview){Environment.ExitCode=1;studio.window.Close();}else W.MessageBox.Show(studio.window,error.Message,"Velvet Audio");}};application.Run(studio.window);}
        catch(Exception error){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"ui-error.txt"),error.ToString());Environment.ExitCode=1;if(!preview)W.MessageBox.Show(error.Message,"Velvet Audio could not start");}
    }
    internal StudioWindow(string? isolated,W.Window? sharedWindow=null)
    {
        using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("VelvetVoice.Wpf.Shell.xaml"))window=(W.Window)System.Windows.Markup.XamlReader.Load(source!);
        isolatedSession=isolated!=null;hostWindow=sharedWindow??window;Owner=hostWindow;model=new MainForm(isolated);WpfDialogs.Model=model;
        using(var image=Assembly.GetExecutingAssembly().GetManifestResourceStream("VelvetVoice.Assets.hero.jpg")){if(image!=null){var bitmap=new Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=image;bitmap.EndInit();bitmap.Freeze();Get<C.Border>("HeroArt").Background=new M.ImageBrush(bitmap){Stretch=M.Stretch.UniformToFill};}}
        if(model.Icon!=null)window.Icon=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(model.Icon.Handle,W.Int32Rect.Empty,Imaging.BitmapSizeOptions.FromEmptyOptions());
        headphoneEq=new HeadphoneEqPage(isolated);
        pages["Studio"]=Studio();pages["Soundboard + clips"]=Board();pages["Voice changer"]=Voice();pages["Setup & help"]=Help();pages["Headphone EQ"]=headphoneEq.Root;
        string[] glyphs={"\uE720","\uE8D6","\uE767","\uE946","\uE7F6"};int index=0;
        foreach(string name in pages.Keys){var row=new C.StackPanel{Orientation=C.Orientation.Horizontal};row.Children.Add(new C.TextBlock{Text=glyphs[index++],FontFamily=new M.FontFamily("Segoe MDL2 Assets"),FontSize=19,Width=32,Foreground=UiControls.Muted});row.Children.Add(new C.TextBlock{Text=name});var nav=new C.RadioButton{Content=row,Style=(W.Style)window.FindResource("NavItem"),GroupName="VelvetVoiceNavigation",Tag=name};nav.Checked+=(_,_)=>Show(name);nav.Click+=(_,_)=>Activated?.Invoke();navigation[name]=nav;Get<C.StackPanel>("Navigation").Children.Add(nav);}
        Get<C.Button>("MinButton").Click+=(_,_)=>hostWindow.WindowState=W.WindowState.Minimized;
        Get<C.Button>("MaxButton").Click+=(_,_)=>hostWindow.WindowState=hostWindow.WindowState==W.WindowState.Maximized?W.WindowState.Normal:W.WindowState.Maximized;
        Get<C.Button>("CloseButton").Click+=(_,_)=>hostWindow.Close();
        timer.Tick+=(_,_)=>{if(hostWindow.WindowState!=W.WindowState.Minimized)ui.Sync();};timer.Start();
        ui.Watch(()=>{Get<C.TextBlock>("Status").Text=model.UiStatus;Get<C.TextBlock>("Status").ToolTip=model.UiStatus;Get<C.TextBlock>("EngineStatus").Text=model.UiLive?"Studio is live":"Ready when you are";Get<C.TextBlock>("EngineStatus").Foreground=model.UiLive?UiControls.Tone("#2FCB8D"):UiControls.Ink;Get<C.TextBlock>("EngineDetail").Text=model.UiLive?"Your selected output is active.":"Start processing in Studio.";});
        hostWindow.Closed+=(_,_)=>{if(closing)return;closing=true;timer.Stop();headphoneEq.Dispose();model.UiShutdown();model.Dispose();WpfDialogs.Model=null;Owner=null;};
        Show("Studio");
    }
    void Show(string name)
    {
        Activated?.Invoke();current=name;Get<C.ContentControl>("PageContent").Content=pages[name];Get<C.ScrollViewer>("PageScroll").ScrollToTop();Get<C.TextBlock>("Eyebrow").Text=name.ToUpperInvariant();
        var title=Get<C.TextBlock>("PageTitle");var subtitle=Get<C.TextBlock>("PageSubtitle");
        (title.Text,subtitle.Text)=name switch{
            "Studio"=>("Your voice. Smooth and clear.","Shape your microphone with gentle EQ, compression, and a protective limiter. Every whisper stays in the conversation."),
            "Soundboard + clips"=>("Keep the moment. Replay the fun.","Build your soundboard and save recent audio locally. Rolling capture and playback start only when you choose."),
            "Voice changer"=>("Find your voice character.","Explore VoxShift presets and build a custom voice with your Studio settings."),
            "Headphone EQ"=>("Your headphones. Your balance.","Give music and games a smoother shape with a personal stereo equalizer and listening presets."),
            _=>("A clear route from mic to game.","Choose your devices, check the signal path, and keep your voice studio working smoothly.")};
        if(navigation.TryGetValue(name,out var nav))nav.IsChecked=true;ui.Sync();
    }
    C.StackPanel Studio()
    {
        var page=new C.StackPanel();var stats=new C.Primitives.UniformGrid{Columns=4,Margin=new W.Thickness(0,0,0,2)};
        foreach(string name in new[]{"Input level","Output level","Gain reduction","Capture queue"}){
            var stack=UiControls.Section(name);var value=UiControls.Text("—",28);value.FontWeight=W.FontWeights.SemiBold;stack.Children.Add(value);var status=UiControls.Text("Not processing",11,UiControls.Muted);stack.Children.Add(status);var meter=new C.ProgressBar{Minimum=0,Maximum=100,Height=4,Foreground=UiControls.Accent,Background=UiControls.Raised,BorderThickness=new W.Thickness(0),Margin=new W.Thickness(0,8,0,0)};stack.Children.Add(meter);
            ui.Watch(()=>{var metrics=model.UiMetrics;float? metric=name switch{"Input level"=>metrics.Input,"Output level"=>metrics.Output,"Gain reduction"=>metrics.Reduction,_=>metrics.Queue};value.Text=metric==null?"—":name.Contains("level")?(metric<=.000001?"−∞":$"{20*Math.Log10(metric.Value):0.0}"):name=="Gain reduction"?$"{metric:0.0}":$"{metric:0}";status.Text=metric==null?"Not processing":name.Contains("level")?"dBFS":name=="Gain reduction"?"dB · mic compression":"ms · capture buffering";meter.Value=metric==null?0:name.Contains("level")?Math.Clamp((20*Math.Log10(Math.Max(.000001f,metric.Value))+60)/60*100,0,100):name=="Gain reduction"?Math.Clamp(metric.Value/12*100,0,100):Math.Clamp(metric.Value,0,100);});var card=UiControls.Card(stack);card.Margin=new W.Thickness(0,0,12,14);stats.Children.Add(card);
        }page.Children.Add(stats);
        var columns=new C.Grid();columns.ColumnDefinitions.Add(new C.ColumnDefinition{Width=new W.GridLength(4,W.GridUnitType.Star)});columns.ColumnDefinitions.Add(new C.ColumnDefinition{Width=new W.GridLength(6,W.GridUnitType.Star)});
        var left=new C.StackPanel();var devices=UiControls.Section("Audio routing","Your microphone in. A processed signal out.");devices.Children.Add(UiControls.Text("Microphone input",12,UiControls.Muted));devices.Children.Add(ui.Convert(model.UiInput));devices.Children.Add(UiControls.Text("Processed output",12,UiControls.Muted));devices.Children.Add(ui.Convert(model.UiOutput));devices.Children.Add(UiControls.Text("Input channel",12,UiControls.Muted));devices.Children.Add(ui.Convert(model.UiChannel));devices.Children.Add(UiControls.Text("Buffer preference",12,UiControls.Muted));devices.Children.Add(ui.Convert(model.UiLatency));var refresh=new C.Button{Content="Refresh devices",HorizontalAlignment=W.HorizontalAlignment.Left,Margin=new W.Thickness(0,4,0,0)};refresh.Click+=(_,_)=>ui.Write(model.UiRefreshDevices);devices.Children.Add(refresh);left.Children.Add(UiControls.Card(devices));
        var controls=UiControls.Section("Studio controls","Processing starts automatically when the app opens.");var actions=new C.WrapPanel();var start=ui.Convert(model.UiStart);start.SetValue(W.FrameworkElement.StyleProperty,window.FindResource("PrimaryButton"));start.Margin=new W.Thickness(0,4,10,10);actions.Children.Add(start);actions.Children.Add(ui.Convert(model.UiBypass));controls.Children.Add(actions);var route=UiControls.Text(model.UiRoute,12,UiControls.Muted);ui.Watch(()=>route.Text=model.UiRoute);controls.Children.Add(route);left.Children.Insert(0,UiControls.Card(controls));left.Margin=new W.Thickness(0,0,14,0);columns.Children.Add(left);
        var voice=UiControls.Section("Voice shaping","Start with a character, then make it your own.");voice.Children.Add(ui.Convert(model.UiPreset));foreach(var pair in model.UiSliders)voice.Children.Add(ui.SliderRow(pair.Key,pair.Value));voice.Children.Add(UiControls.Text("No live mic gate, denoiser, or automatic level ride. Peak ceiling: −1 dBFS.",11,UiControls.Muted));var voiceCard=UiControls.Card(voice);C.Grid.SetColumn(voiceCard,1);columns.Children.Add(voiceCard);page.Children.Add(columns);
        var detail=UiControls.Section("Signal health");var meterText=UiControls.Text(model.UiMeterText,12,UiControls.Muted);ui.Watch(()=>meterText.Text=model.UiMeterText);detail.Children.Add(meterText);page.Children.Add(UiControls.Card(detail));return page;
    }
    C.StackPanel Board()
    {
        var page=new C.StackPanel();var source=(F.TableLayoutPanel)model.UiBoard.UiSource;
        var rolling=UiControls.Section("Rolling clips","Up to 15 seconds in memory. Save clip adds a local pad without playing it.");for(int row=1;row<=4;row++)rolling.Children.Add(ui.TableRow(source,row));rolling.Children.Add(UiControls.Text("Speech focus cleans saved clips locally. Original audio is retained for private comparison.",11,UiControls.Muted));page.Children.Add(UiControls.Card(rolling));
        var library=UiControls.Section("Your soundboard","Select a pad to manage it. Double-click or choose Play selected to send it through the Studio output.");library.Children.Add(ui.TableRow(source,5));var pads=ui.SoundPads(model.UiBoard.UiSounds,model.UiBoard.UiPlaySelected);library.Children.Add(pads);var empty=UiControls.Text("No sounds yet. Import a sound or save a rolling clip to build your board.",13,UiControls.Muted);ui.Watch(()=>empty.Visibility=model.UiBoard.UiSounds.Items.Count==0?W.Visibility.Visible:W.Visibility.Collapsed);library.Children.Add(empty);page.Children.Add(UiControls.Card(library));
        var options=UiControls.Section("Pad settings & private listening","Preview and Original play only through the chosen headphones.");options.Children.Add(ui.TableRow(source,7));options.Children.Add(ui.TableRow(source,8));page.Children.Add(UiControls.Card(options));return page;
    }
    C.StackPanel Voice()
    {
        var board = model.UiBoard;
        var effect = model.UiVoicePage.Controls.Cast<F.Control>().OfType<F.ComboBox>().First();
        var page = new C.StackPanel();
        var presets = UiControls.Section("VoxShift voices", "46 DSP presets · independent pitch and formant · full effect chain");
        var category = new C.ComboBox { ItemsSource = new[] { "All", "Human", "Cartoon", "Sci-Fi", "Horror", "Famous", "FX", "Custom" }, SelectedIndex = 0, Margin = new W.Thickness(0,8,0,8) };
        var selector = new C.ComboBox { Margin = new W.Thickness(0,0,0,12) };
        bool updating = false;
        void RefreshChoices() { updating = true; selector.ItemsSource = board.VoiceNames.Where(n => n == "Off" || category.SelectedItem?.ToString() == "All" || board.VoiceCategory(n) == category.SelectedItem?.ToString()).ToArray(); selector.SelectedItem = effect.SelectedItem; updating = false; }
        category.SelectionChanged += (_,_) => RefreshChoices();
        selector.SelectionChanged += (_,_) => { if (!updating && selector.SelectedItem is string name) ui.Write(() => effect.SelectedItem = name); };
        presets.Children.Add(category); presets.Children.Add(selector);
        var reset = new C.Button { Content = "Off · original voice", HorizontalAlignment = W.HorizontalAlignment.Left };
        reset.Click += (_,_) => ui.Write(() => effect.SelectedItem = "Off"); presets.Children.Add(reset);
        var status = UiControls.Text("", 12, UiControls.Muted); presets.Children.Add(status);
        ui.Watch(() => status.Text = board.State.VoiceError ?? (board.State.VoicePreset == "Off" ? "Voice effects are off." : "Active: " + board.State.VoicePreset + " · Start Studio processing to hear your voice."));
        page.Children.Add(UiControls.Card(presets));
        var controls = UiControls.Section("Shape your voice", "Enable each effect before adjusting it. Studio processing still applies; clips retain their original sound.");
        var editors = new Dictionary<string, Action<JsonElement>>();
        foreach (var group in VoxPresets.Defaults.GroupBy(p => p.Key.Contains('_') ? p.Key.Split('_')[0] : "voice"))
        {
            var rows = new C.StackPanel();
            foreach (var pair in group)
            {
                string key = pair.Key; var row = new C.StackPanel { Margin = new W.Thickness(0,4,0,4) };
                string label = key switch {
                    "pitch" => "Pitch (semitones)", "formant" => "Formant (semitones)", "preserve" => "Preserve formants", "gain" => "Output gain", "gate_threshold" => "Gate threshold (amplitude)",
                    "eq_low" => "Low EQ (dB)", "eq_mid" => "Mid EQ (dB)", "eq_high" => "High EQ (dB)",
                    "ring_freq" => "Carrier frequency (Hz)", "trem_rate" or "vib_rate" => "Rate (Hz)", "vib_depth_ms" => "Depth (ms)", "echo_time_ms" => "Delay (ms)",
                    _ => key.EndsWith("_enabled") ? "Enable effect" : key.Substring(key.IndexOf('_') + 1).Replace('_', ' ')
                };
                if (pair.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    var toggle = new C.CheckBox { Content = label };
                    toggle.Checked += (_,_) => { if (!updating) board.SetVoiceParameter(key, true); };
                    toggle.Unchecked += (_,_) => { if (!updating) board.SetVoiceParameter(key, false); };
                    editors[key] = value => toggle.IsChecked = value.GetBoolean(); row.Children.Add(toggle);
                }
                else
                {
                    var range = VoxPresets.Range(key); var text = UiControls.Text(label,12,UiControls.Muted);
                    var slider = new C.Slider { Minimum = range.Min, Maximum = range.Max, IsSnapToTickEnabled = key is "crush_bits" or "crush_downsample", TickFrequency = 1 };
                    slider.ValueChanged += (_,_) => { text.Text = label + "  " + slider.Value.ToString("0.##"); if (!updating) board.SetVoiceParameter(key, slider.Value); };
                    editors[key] = value => { slider.Value = value.GetDouble(); text.Text = label + "  " + slider.Value.ToString("0.##"); };
                    row.Children.Add(text); row.Children.Add(slider);
                }
                rows.Children.Add(row);
            }
            controls.Children.Add(new C.Expander { Header = UiControls.Text(group.Key switch { "voice" => "Pitch, formant and volume", "gate" => "Noise gate", "eq" => "Three-band EQ", "dist" => "Distortion", "crush" => "Bitcrusher", "ring" => "Ring modulation", "trem" => "Tremolo", "vib" => "Vibrato", "echo" => "Echo", _ => "Reverb" }, 14, UiControls.Ink), Content = rows, IsExpanded = group.Key == "voice", Margin = new W.Thickness(0,8,0,8) });
        }
        void RefreshControls() { updating = true; foreach (var pair in board.VoiceParameters) editors[pair.Key](pair.Value); updating = false; }
        effect.SelectedIndexChanged += (_,_) => { RefreshChoices(); RefreshControls(); };
        string? lastParameters = null;
        ui.Watch(() => { if (lastParameters != board.State.VoiceParameters) { lastParameters = board.State.VoiceParameters; RefreshChoices(); RefreshControls(); } });
        var nameBox = new C.TextBox { MaxLength = 60, Margin = new W.Thickness(0,12,0,8) };
        controls.Children.Add(UiControls.Text("Custom preset name",12,UiControls.Muted)); controls.Children.Add(nameBox);
        var save = new C.Button { Content = "Save custom preset", HorizontalAlignment = W.HorizontalAlignment.Left };
        save.Click += (_,_) => { try { board.SaveCustomVoice(nameBox.Text); category.SelectedIndex = 0; RefreshChoices(); } catch (ArgumentException error) { W.MessageBox.Show(window,error.Message,"Custom voice"); } }; controls.Children.Add(save);
        page.Children.Add(UiControls.Card(controls));
        page.Children.Add(UiControls.Text("Local DSP effects, not AI voice cloning. Extreme shifts can sound processed. The worker adds buffering and pitch delay; device buffering adds further latency.",13,UiControls.Muted));
        RefreshChoices(); RefreshControls(); return page;
    }
    C.StackPanel Help()
    {
        var page=new C.StackPanel();var route=UiControls.Section("Your signal path","SM7B → Wave Link → VoiceMeeter B1 → Velvet Audio → CABLE Input → game microphone");
        foreach(var step in new[]{"1. Send only your microphone to VoiceMeeter B1. Keep upstream gates and compressors off initially.","2. In Studio, choose Voicemeeter Out B1 as input and CABLE Input as processed output.","3. Start processing. In your game or Discord, choose CABLE Output as its microphone.","4. Keep your normal headphones as the listening device. Do not loop the processed cable back into B1.","5. Prefer matching 48 kHz device rates. Use Stable buffering if the interruption counters grow."})route.Children.Add(UiControls.Text(step,13,UiControls.Muted));var guide=new C.Button{Content="Open full setup guide",HorizontalAlignment=W.HorizontalAlignment.Left};guide.Click+=(_,_)=>Open(Path.Combine(AppContext.BaseDirectory,"VOICE-SETUP.md"));route.Children.Add(guide);page.Children.Add(UiControls.Card(route));
        var privacy=UiControls.Section("Local processing. Your choices.");privacy.Children.Add(UiControls.Text("Microphone processing starts automatically when the app opens. Rolling capture starts only when you choose. Clips are written only when you choose Save clip or import sounds. The Python worker processes saved snapshots locally; it does not open audio devices or upload recordings.",13,UiControls.Muted));privacy.Children.Add(UiControls.Text("VoiceMeeter and VB-CABLE are separate drivers. This build does not install drivers or change your Windows audio defaults.",13,UiControls.Muted));var files=new C.Button{Content="Open local sound library",HorizontalAlignment=W.HorizontalAlignment.Left};files.Click+=(_,_)=>{string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VelvetVoice","Sounds");if(Directory.Exists(folder))Open(folder);else W.MessageBox.Show(window,"Import a sound or save a clip to create the sound library.","Velvet Audio");};privacy.Children.Add(files);page.Children.Add(UiControls.Card(privacy));return page;
    }
    void Open(string path)=>ui.Write(()=>Process.Start(new ProcessStartInfo(path){UseShellExecute=true}));
    void Snapshot(string path){window.UpdateLayout();var bitmap=new Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,M.PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new Imaging.PngBitmapEncoder();encoder.Frames.Add(Imaging.BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
    async Task Preview(string[] args)
    {
        var log=new List<string>();await Task.Delay(300);
        if(args.Contains("--ui-smoke")){
            foreach(int width in new[]{1380,1060}){window.Width=width;window.Height=width==1380?930:700;foreach(string name in pages.Keys){Show(name);await Task.Delay(100);window.UpdateLayout();if(model.UiLive)throw new Exception("Audio autostarted during UI test");Snapshot(Path.Combine(AppContext.BaseDirectory,$"preview-{name.Replace(" + ","-").Replace(" & ","-").Replace(" ","-")}-{width}.png"));log.Add($"PASS native {name} at {width}; audio remains off");}}
            Show("Studio");window.UpdateLayout();var source=model.UiSliders.First().Value;int original=source.Value;var slider=Find<C.Slider>(pages["Studio"]).First();slider.Value=original+1;if(source.Value!=original+1)throw new Exception("Studio slider binding");source.Value=original;ui.Sync();if(slider.Value!=original)throw new Exception("Studio reverse binding");log.Add("PASS Studio sliders synchronize both directions");
            var sound=new F.ListViewItem(new[]{"Fixture pad","1.0s","Unbound","0 dB"});model.UiBoard.UiSounds.Items.Add(sound);Show("Soundboard + clips");window.UpdateLayout();ui.Sync();var pads=Find<C.ListBox>(pages["Soundboard + clips"]).First();pads.SelectedIndex=0;if(model.UiBoard.UiSounds.SelectedItems.Count!=1)throw new Exception("Pad selection binding");pads.BringIntoView();await Task.Delay(80);window.UpdateLayout();if(!Find<C.Border>(pads).Any(x=>x.CornerRadius.TopLeft==8))throw new Exception("Rounded pad template missing");Snapshot(Path.Combine(AppContext.BaseDirectory,"preview-board-fixture.png"));model.UiBoard.UiSounds.Items.Remove(sound);ui.Sync();log.Add("PASS soundboard pad selection reaches original handlers");
            var effect=model.UiVoicePage.Controls.Cast<F.Control>().OfType<F.ComboBox>().First();effect.SelectedItem="Woman";ui.Sync();if(model.UiBoard.State.VoicePreset!="Woman")throw new Exception("Voice preset state");effect.SelectedItem="Off";log.Add("PASS voice presets synchronize with shared audio state");
            log.AddRange(WpfDialogs.Verify());log.Add("PASS isolated settings; no audio recording, playback, or system routing changes performed");File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"ui-test-results.txt"),log);
        }else{string name=args.Contains("--preview-board")?"Soundboard + clips":args.Contains("--preview-effects")?"Voice changer":"Studio";Show(name);await Task.Delay(100);Snapshot(Path.Combine(AppContext.BaseDirectory,args.Contains("--preview-board")?"preview-board.png":args.Contains("--preview-effects")?"preview-effects.png":"preview.png"));}
        window.Close();
    }
    static IEnumerable<T> Find<T>(W.DependencyObject root) where T:W.DependencyObject{for(int i=0;i<M.VisualTreeHelper.GetChildrenCount(root);i++){var child=M.VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Find<T>(child))yield return nested;}}
}
