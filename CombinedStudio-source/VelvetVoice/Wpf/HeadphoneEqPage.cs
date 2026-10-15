using NAudio.CoreAudioApi;
using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;

namespace VelvetVoice;
internal sealed class EqCurve : W.FrameworkElement
{
    internal EqParameters Settings=new(new float[10],0,false);
    internal double LevelOffset;
    internal event Action<int,float> BandChanged;
    int dragBand=-1;
    internal EqCurve(){Height=190;MinWidth=240;}
    protected override void OnRender(M.DrawingContext drawing)
    {
        base.OnRender(drawing);double width=Math.Max(1,ActualWidth-48),height=140;
        double X(double f)=>36+Math.Log(f/20)/Math.Log(1000)*width;
        double Y(double db)=>16+(12-Math.Clamp(db,-12,12))/24*height;
        foreach(int db in new[]{12,0,-12}){
            drawing.DrawLine(new M.Pen(db==0?UiControls.Muted:UiControls.Line,1),new W.Point(36,Y(db)),new W.Point(36+width,Y(db)));
            Label(drawing,db.ToString(),0,Y(db)-7);
        }
        foreach(int f in new[]{20,100,1000,10000,20000}){drawing.DrawLine(new M.Pen(UiControls.Line,1),new W.Point(X(f),16),new W.Point(X(f),156));Label(drawing,f>=1000?$"{f/1000}k":f.ToString(),X(f)-10,164);}
        var curve=new M.StreamGeometry();using(var context=curve.Open()){
            for(int i=0;i<=240;i++){double f=20*Math.Pow(1000,i/240.0);var point=new W.Point(X(f),Y(HeadphoneEqProvider.Response(f,Settings)-(Settings.Bypass?Settings.Preamp:HeadphoneEqProvider.Headroom(Settings))));if(i==0)context.BeginFigure(point,false,false);else context.LineTo(point,true,false);}
        }
        curve.Freeze();drawing.DrawGeometry(null,new M.Pen(UiControls.Accent,2.5),curve);
        for(int i=0;i<10;i++)drawing.DrawEllipse(UiControls.Panel,new M.Pen(UiControls.Accent,2),new W.Point(X(HeadphoneEqProvider.Frequencies[i]),Y(Settings.Bands[i])),5,5);
    }
    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);var point=e.GetPosition(this);double width=Math.Max(1,ActualWidth-48);dragBand=Enumerable.Range(0,10).OrderBy(i=>Math.Abs(point.X-(36+Math.Log(HeadphoneEqProvider.Frequencies[i]/20)/Math.Log(1000)*width))).First();CaptureMouse();Edit(point.Y);e.Handled=true;}
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e){base.OnMouseMove(e);if(dragBand>=0&&e.LeftButton==System.Windows.Input.MouseButtonState.Pressed)Edit(e.GetPosition(this).Y);}
    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e){dragBand=-1;ReleaseMouseCapture();base.OnMouseLeftButtonUp(e);}
    void Edit(double y){if(dragBand>=0)BandChanged?.Invoke(dragBand,(float)(Math.Round(Math.Clamp(12-(y-16)/140*24,-12,12)*2)/2));}
    void Label(M.DrawingContext dc,string text,double x,double y)=>dc.DrawText(new M.FormattedText(text,System.Globalization.CultureInfo.CurrentCulture,W.FlowDirection.LeftToRight,new M.Typeface("Segoe UI"),10,UiControls.Muted,M.VisualTreeHelper.GetDpi(this).PixelsPerDip),new W.Point(x,y));
}

internal sealed class HeadphoneEqPage : IDisposable
{
    readonly HeadphoneEqStore store;
    readonly HeadphoneEqEngine engine=new();
    readonly ApoHeadphoneEq apo;
    readonly C.CheckBox advanced=new(){Content="Use separate playback routing",Margin=new W.Thickness(0,8,0,8)};
    readonly C.ComboBox source=new(){MinHeight=40},output=new(){MinHeight=40},presets=new(){MinHeight=40,MinWidth=210};
    readonly C.TextBox presetName=new(){MinHeight=38,MinWidth=180,MaxLength=48,Text="My headphone preset"};
    readonly C.TextBlock status=UiControls.Text("",12,UiControls.Muted),headroom=UiControls.Text("",12,UiControls.Muted);
    readonly C.Button start=new(){Content="Start headphone EQ",MinHeight=40};
    readonly C.CheckBox power=new(){Content="Headphone EQ · Off",FontSize=18,Foreground=UiControls.Ink,Margin=new W.Thickness(0,4,0,12)};
    readonly C.CheckBox startEnabled=new(){Content="Enable automatically when the app opens",IsChecked=true,Foreground=UiControls.Ink,Margin=new W.Thickness(0,0,0,10)};
    readonly C.TextBlock powerStatus=UiControls.Text("",12,UiControls.Muted),activePreset=UiControls.Text("",14,UiControls.Accent);
    string lastPresetName="Flat";
    readonly C.CheckBox bypass=new(){Content="Bypass tone shaping",Margin=new W.Thickness(12,10,0,10)};
    readonly C.Slider preamp=new(){Minimum=-24,Maximum=6,TickFrequency=.5,SmallChange=.5,IsSnapToTickEnabled=true,MinWidth=180};
    readonly C.TextBox preampValue=new(){Width=90,MinHeight=38,VerticalContentAlignment=W.VerticalAlignment.Center};
    readonly C.CheckBox automaticHeadroom=new(){Content="Automatic headroom for EQ boosts",IsChecked=true,Margin=new W.Thickness(0,8,0,8)};
    readonly C.CheckBox quietBoost=new(){Content="Boost quiet sounds · up to +6 dB",Margin=new W.Thickness(0,8,18,8)};
    readonly C.CheckBox strongLimiter=new(){Content="Limiter · cap loud sounds at −6 dBFS",Margin=new W.Thickness(0,8,0,8)};
    readonly C.Slider[] bands=new C.Slider[10];
    readonly EqCurve curve=new();
    readonly System.Windows.Threading.DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(150)};
    readonly C.ProgressBar meter=new(){Minimum=0,Maximum=1,Height=5,Foreground=UiControls.Accent,Background=UiControls.Raised,Margin=new W.Thickness(0,8,0,0)};
    bool changing,dirty,apoPending;
    DateTime lastChange;
    internal C.StackPanel Root {get;}=new();
    internal bool Running=>engine.Running||apo.Running;
    internal void AutoStart(){if(store.Config.StartEnabled&&!Running){SetEnabled(true);Sync();}}
    internal HeadphoneEqPage(string? root)
    {
        store=new(root);
        lastPresetName=HeadphoneEqStore.LastPresetName(store.Config);
        if(root!=null){string folder=Path.Combine(root,"APO-fixture");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"config.txt");if(!File.Exists(file))File.WriteAllText(file,"# isolated headphone EQ test configuration\n");}
        apo=new ApoHeadphoneEq(root==null?null:Path.Combine(root,"APO-fixture"));
        var routing=UiControls.Section("Headphone listening","Choose your listening device and enable EQ. Your games and music keep playing through the same output.");
        routing.Children.Add(UiControls.Text("Your headphones / speakers",12,UiControls.Muted));routing.Children.Add(output);
        var directInfo=UiControls.Text(apo.Available?"Equalizer APO is installed · direct device EQ is available.":"Direct mode requires Equalizer APO. Separate playback routing is available below.",12,UiControls.Muted);routing.Children.Add(directInfo);
        var actions=new C.WrapPanel{Margin=new W.Thickness(0,12,0,4)};actions.Children.Add(start);var refresh=Button("Refresh devices",()=>Refresh());refresh.Margin=new W.Thickness(12,0,0,0);actions.Children.Add(refresh);actions.Children.Add(bypass);routing.Children.Add(actions);routing.Children.Add(status);routing.Children.Add(meter);Root.Children.Add(UiControls.Card(routing));
        var setup=Button("Audio device setup",()=>apo.OpenDeviceSetup());setup.IsEnabled=apo.Available;setup.HorizontalAlignment=W.HorizontalAlignment.Left;routing.Children.Add(setup);
        var advancedContent=new C.StackPanel();advancedContent.Children.Add(advanced);var sourceLabel=UiControls.Text("Playback source",12,UiControls.Muted);sourceLabel.Visibility=source.Visibility=W.Visibility.Collapsed;advancedContent.Children.Add(sourceLabel);advancedContent.Children.Add(source);advancedContent.Children.Add(UiControls.Text("Optional routing for an existing virtual cable. Set your game's output to that source; keep it different from your headphones and Studio microphone output.",12,UiControls.Muted));routing.Children.Add(new C.Expander{Header="Advanced routing",Content=advancedContent});
        void ModeChanged(){sourceLabel.Visibility=source.Visibility=advanced.IsChecked==true?W.Visibility.Visible:W.Visibility.Collapsed;meter.Visibility=advanced.IsChecked==true?W.Visibility.Visible:W.Visibility.Collapsed;Change();Sync();}
        advanced.Checked+=(_,_)=>ModeChanged();advanced.Unchecked+=(_,_)=>ModeChanged();meter.Visibility=W.Visibility.Collapsed;
        var tone=UiControls.Section("Custom EQ profile","Ten bands · ±12 dB · drag the curve handles or adjust the sliders. Preamp controls the headphone level separately.");
        var presetRow=new C.WrapPanel();presetRow.Children.Add(presets);var reset=Button("Reset to flat",()=>Load(HeadphoneEqStore.Factory[0]));reset.Margin=new W.Thickness(12,0,0,0);presetRow.Children.Add(reset);tone.Children.Add(presetRow);tone.Children.Add(curve);
        var controls=new C.Primitives.UniformGrid{Columns=10,MinWidth=660};
        for(int b=0;b<10;b++){
            int index=b;var column=new C.StackPanel{Margin=new W.Thickness(3,0,3,0)};float frequency=HeadphoneEqProvider.Frequencies[b];
            var label=UiControls.Text(frequency>=1000?$"{frequency/1000:0.#}k":$"{frequency:0.#}",12,UiControls.Muted);label.TextAlignment=W.TextAlignment.Center;column.Children.Add(label);
            var slider=new C.Slider{Orientation=C.Orientation.Vertical,Minimum=-12,Maximum=12,Height=154,TickFrequency=3,SmallChange=.5,LargeChange=3,IsSnapToTickEnabled=false,HorizontalAlignment=W.HorizontalAlignment.Center,Width=32};
            slider.SetResourceReference(W.FrameworkElement.StyleProperty,"EqSlider");W.Automation.AutomationProperties.SetName(slider,$"{frequency} Hz gain in decibels");bands[b]=slider;column.Children.Add(slider);
            var value=UiControls.Text("0.0",12);value.TextAlignment=W.TextAlignment.Center;column.Children.Add(value);
            slider.ValueChanged+=(_,_)=>{value.Text=$"{slider.Value:+0.0;-0.0;0.0}";if(!changing){presets.SelectedIndex=-1;Change();}};
            slider.MouseDoubleClick+=(_,_)=>slider.Value=0;controls.Children.Add(column);
        }
        tone.Children.Add(new C.ScrollViewer{Content=controls,HorizontalScrollBarVisibility=C.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=C.ScrollBarVisibility.Disabled});
        var quick=new C.WrapPanel{Margin=new W.Thickness(0,14,0,0)};foreach(var entry in new[]{("Bass","Bass cushion"),("Vocal","Voice clarity"),("Treble","Treble detail"),("Flat","Flat"),("Footsteps","Footstep Focus")}){string target=entry.Item2;var button=Button(entry.Item1,()=>Load(HeadphoneEqStore.Factory.First(x=>x.Name==target)));button.Margin=new W.Thickness(0,0,10,8);quick.Children.Add(button);}tone.Children.Add(quick);
        curve.BandChanged+=(index,gain)=>bands[index].Value=gain;curve.ToolTip="Drag horizontally to choose a frequency band, then vertically to change its gain.";
        preamp.ValueChanged+=(_,_)=>{if(!changing){presets.SelectedIndex=-1;Change();}};bypass.Checked+=(_,_)=>Change();bypass.Unchecked+=(_,_)=>Change();
        Root.Children.Insert(0,UiControls.Card(tone));
        var soundProfiles=UiControls.Section("Sound profiles","General starting points for game audio. Choose a profile, then customize its bands.");var profileCards=new C.WrapPanel();foreach(var entry in new[]{("Footstep Focus","Boost 1–4 kHz for footstep detail"),("Combat Clarity","Bass and mid-range emphasis"),("Spatial Awareness","Balanced mid-range detail"),("Flat","Flat tone response")}){var content=new C.StackPanel{Width=230};content.Children.Add(UiControls.Text(entry.Item1,16));content.Children.Add(UiControls.Text(entry.Item2,12,UiControls.Muted));string target=entry.Item1;var button=Button("",()=>Load(HeadphoneEqStore.Factory.First(x=>x.Name==target)));button.Content=content;button.Padding=new W.Thickness(16);button.Margin=new W.Thickness(0,0,12,12);profileCards.Children.Add(button);}soundProfiles.Children.Add(profileCards);Root.Children.Insert(0,UiControls.Card(soundProfiles));
        var level=UiControls.Section("Headphone preamp","Adjust the output gain from −24 to +6 dB. Positive values amplify quiet audio; start at 0 dB and raise gradually.");var readout=new C.WrapPanel();readout.Children.Add(preampValue);readout.Children.Add(UiControls.Text("dB",14,UiControls.Muted));readout.Children.Add(Button("Reset to 0 dB",()=>preamp.Value=0));level.Children.Add(readout);level.Children.Add(preamp);level.Children.Add(automaticHeadroom);level.Children.Add(headroom);Root.Children.Insert(0,UiControls.Card(level));W.Automation.AutomationProperties.SetName(preamp,"Headphone preamp in decibels");W.Automation.AutomationProperties.SetName(preampValue,"Exact headphone preamp in decibels");
        void CommitPreamp(){if(float.TryParse(preampValue.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.CurrentCulture,out float value)&&float.IsFinite(value)&&value>=-24&&value<=6)preamp.Value=value;else{preampValue.Text=preamp.Value.ToString("0.0");status.Text="Enter a preamp value from −24 to +6 dB.";}}preampValue.LostKeyboardFocus+=(_,_)=>CommitPreamp();preampValue.KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){CommitPreamp();e.Handled=true;}};automaticHeadroom.Click+=(_,_)=>Change();
        var dynamics=UiControls.Section("Limiter & quiet-sound boost","Live dynamics are available with separate playback routing. Direct Equalizer APO mode uses the preamp, EQ curve and headroom settings.");var dynamicsRow=new C.WrapPanel();dynamicsRow.Children.Add(quietBoost);dynamicsRow.Children.Add(strongLimiter);dynamics.Children.Add(dynamicsRow);Root.Children.Add(UiControls.Card(dynamics));quietBoost.Click+=(_,_)=>Change();strongLimiter.Click+=(_,_)=>Change();
        var library=UiControls.Section("Your presets","Built-in starting points are general listening curves. Tune by ear for your headphones; no single curve fits every model.");
        var saveRow=new C.WrapPanel();saveRow.Children.Add(presetName);var save=Button("Save preset",SavePreset);save.Margin=new W.Thickness(12,0,0,0);saveRow.Children.Add(save);var delete=Button("Delete saved preset",DeletePreset);delete.Margin=new W.Thickness(12,0,0,0);saveRow.Children.Add(delete);library.Children.Add(saveRow);Root.Children.Add(UiControls.Card(library));
        var help=UiControls.Section("One device. Everyday listening.","Choose Speakers (Realtek) if that is where your headphones are plugged in, then Enable headphone EQ. Keep Windows and your games set to that same device.");
        help.Children.Add(UiControls.Text("Direct EQ uses the installed Equalizer APO engine with Velvet Audio's original controls and presets. If changes are inaudible, open Audio device setup, enable Equalizer APO on your selected output, and follow its restart instructions. Keep Windows audio enhancements enabled; exclusive-mode playback can bypass system effects.",12,UiControls.Muted));
        help.Children.Add(UiControls.Text("Enabling Velvet Audio EQ backs up the existing APO configuration and replaces its filters only for your selected device. Other devices retain their prior configuration. Disable EQ or close Velvet Audio to restore the previous setup. Changing settings in Peace while Velvet Audio is enabled can take control back; use one controller at a time. Direct mode reserves conservative headroom; its filters do not provide the app's live peak limiter. Separate routing has smooth live transitions and a −1 dBFS limiter.",12,UiControls.Muted));Root.Children.Add(UiControls.Card(help));
        presets.SelectionChanged+=(_,_)=>{if(!changing&&presets.SelectedItem is EqProfile profile){Load(profile);presetName.Text=profile.Name;}};
        source.SelectionChanged+=(_,_)=>{if(!changing)Change();};output.SelectionChanged+=(_,_)=>{if(!changing)Change();};
        start.Click+=(_,_)=>{SetEnabled(!Running);Sync();};
        var powerCard=UiControls.Section("Headphone EQ","Your last listening profile loads automatically. EQ starts on when a working playback backend is available.");powerCard.Children.Add(power);powerCard.Children.Add(startEnabled);powerCard.Children.Add(activePreset);powerCard.Children.Add(powerStatus);var setupTop=Button("Set up headphone device",()=>apo.OpenDeviceSetup());setupTop.IsEnabled=apo.Available;powerCard.Children.Add(setupTop);Root.Children.Insert(0,UiControls.Card(powerCard));W.Automation.AutomationProperties.SetName(power,"Headphone EQ on or off");power.Click+=(_,_)=>{SetEnabled(power.IsChecked==true);Sync();};startEnabled.IsChecked=store.Config.StartEnabled;startEnabled.Click+=(_,_)=>Try(Persist);
        ReloadPresets();Load(store.Config.Current);changing=true;advanced.IsChecked=store.Config.Advanced;automaticHeadroom.IsChecked=store.Config.AutomaticHeadroom;quietBoost.IsChecked=store.Config.QuietBoost;strongLimiter.IsChecked=store.Config.StrongLimiter;changing=false;ModeChanged();Try(Refresh);
        lastPresetName=HeadphoneEqStore.LastPresetName(store.Config);Sync();
        timer.Tick+=(_,_)=>{Sync();if(apoPending){apoPending=false;Try(()=>apo.Update(Parameters()));}if(dirty&&(DateTime.UtcNow-lastChange).TotalMilliseconds>600)Try(Persist);};timer.Start();
    }
    C.Button Button(string text,Action action){var button=new C.Button{Content=text,MinHeight=38};button.Click+=(_,_)=>Try(action);return button;}
    void Try(Action action){try{action();}catch(Exception e){status.Text=e.GetBaseException().Message;}}
    void SetEnabled(bool enabled){Try(()=>{if(!enabled){engine.Stop();apo.Stop();status.Text=apo.Status;return;}if(Running)return;if(output.SelectedItem is not DeviceChoice headphones)throw new InvalidOperationException("Choose your headphones or speakers in Headphone listening below.");if(advanced.IsChecked==true){if(source.SelectedItem is not DeviceChoice input)throw new InvalidOperationException("Choose a separate playback source, or turn off separate playback routing.");engine.Start(input.Id,headphones.Id,Parameters(),message=>{status.Text=message;Sync();});status.Text=engine.Status;}else{apo.Start(headphones.Id,Parameters());status.Text=apo.Status;}Persist();});}
    EqParameters Parameters()=>new(bands.Select(x=>(float)x.Value).ToArray(),(float)preamp.Value,bypass.IsChecked==true,automaticHeadroom.IsChecked==true,advanced.IsChecked==true&&quietBoost.IsChecked==true,advanced.IsChecked==true&&strongLimiter.IsChecked==true);
    void Change(){if(changing)return;dirty=true;lastChange=DateTime.UtcNow;var settings=Parameters();engine.Update(settings);apoPending=apo.Running;curve.Settings=settings;float effective=HeadphoneEqProvider.Headroom(settings)-(advanced.IsChecked!=true&&settings.AutomaticHeadroom?1:0);curve.LevelOffset=effective-HeadphoneEqProvider.Headroom(settings);curve.InvalidateVisual();preampValue.Text=preamp.Value.ToString("0.0");headroom.Text=$"Requested {preamp.Value:+0.0;-0.0;0.0} dB · applied gain {effective:+0.0;-0.0;0.0} dB · "+(advanced.IsChecked==true?(settings.StrongLimiter?"protected ceiling −6 dBFS":"protected ceiling −1 dBFS"):settings.AutomaticHeadroom?"EQ headroom + 1 dB reserve":"manual gain · direct mode has no peak limiter")+(settings.Bypass?" · tone shaping bypassed, preamp active":"");}
    void Load(EqProfile profile){changing=true;try{lastPresetName=profile.Name;for(int i=0;i<10;i++)bands[i].Value=profile.Bands[i];preamp.Value=profile.Preamp;presets.SelectedItem=presets.Items.Cast<EqProfile>().FirstOrDefault(x=>x.Name==profile.Name);}finally{changing=false;}Change();Sync();}
    void ReloadPresets(){changing=true;presets.ItemsSource=HeadphoneEqStore.Factory.Concat(store.Config.Saved).ToArray();presets.DisplayMemberPath="Name";changing=false;}
    void SavePreset(){string name=presetName.Text.Trim();if(name.Length==0)throw new InvalidOperationException("Enter a name for your preset.");if(HeadphoneEqStore.Factory.Any(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Choose a new name to keep the built-in preset.");var profile=new EqProfile(name,Parameters().Bands,(float)preamp.Value);store.Config=store.Config with{Saved=store.Config.Saved.Where(x=>!x.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).Append(profile).ToArray()};ReloadPresets();Load(profile);Persist();status.Text="Saved preset: "+name;}
    void DeletePreset(){if(presets.SelectedItem is not EqProfile profile||!store.Config.Saved.Any(x=>x.Name==profile.Name))throw new InvalidOperationException("Select one of your saved presets to delete.");store.Config=store.Config with{Saved=store.Config.Saved.Where(x=>x.Name!=profile.Name).ToArray()};ReloadPresets();Load(HeadphoneEqStore.Factory[0]);Persist();}
    void Persist(){dirty=false;store.Config=store.Config with{StartEnabled=startEnabled.IsChecked==true,LastPreset=lastPresetName,Advanced=advanced.IsChecked==true,AutomaticHeadroom=automaticHeadroom.IsChecked==true,QuietBoost=quietBoost.IsChecked==true,StrongLimiter=strongLimiter.IsChecked==true,Source=(source.SelectedItem as DeviceChoice)?.Id,Output=(output.SelectedItem as DeviceChoice)?.Id,Current=new EqProfile((presets.SelectedItem as EqProfile)?.Name??"Custom",Parameters().Bands,(float)preamp.Value)};store.Save();}
    void Refresh(){if(Running)throw new InvalidOperationException("Disable headphone EQ before refreshing devices.");changing=true;try{string? input=(source.SelectedItem as DeviceChoice)?.Id??store.Config.Source,headphones=(output.SelectedItem as DeviceChoice)?.Id??store.Config.Output;using var devices=new MMDeviceEnumerator();var choices=new List<DeviceChoice>();foreach(var device in devices.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active)){using(device)choices.Add(new(device.ID,device.FriendlyName));}source.ItemsSource=choices;output.ItemsSource=choices.ToArray();source.SelectedItem=choices.FirstOrDefault(x=>x.Id==input);output.SelectedItem=choices.FirstOrDefault(x=>x.Id==headphones)??choices.FirstOrDefault(x=>x.Name.Contains("Speakers",StringComparison.OrdinalIgnoreCase)&&x.Name.Contains("Realtek",StringComparison.OrdinalIgnoreCase));if(output.SelectedItem==null&&choices.Count>0){using var defaultDevice=devices.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);output.SelectedItem=choices.FirstOrDefault(x=>x.Id==defaultDevice.ID);}status.Text=choices.Count==0?"No active playback devices found.":"Ready · choose your headphones and enable EQ";}finally{changing=false;}}
    void Sync(){start.Content=Running?"Disable headphone EQ":"Enable headphone EQ";source.IsEnabled=output.IsEnabled=advanced.IsEnabled=!Running;quietBoost.IsEnabled=strongLimiter.IsEnabled=advanced.IsChecked==true;power.IsChecked=Running;power.Content=Running?"Headphone EQ · On":"Headphone EQ · Off";activePreset.Text="Last preset: "+lastPresetName+(presets.SelectedItem==null?" · modified":"");meter.Value=engine.Peak;if(engine.Running)status.Text=engine.Status;else if(apo.Running)status.Text=apo.Status;powerStatus.Text=status.Text;}
    internal void Verify(){
        if(Running||source.Visibility!=W.Visibility.Collapsed)throw new Exception("Headphone EQ autostarted or advanced source was shown by default");
        if(startEnabled.IsChecked!=true)throw new Exception("Headphone EQ startup should default to on");AutoStart();if(!Running||power.IsChecked!=true)throw new Exception("EQ autostart did not activate the isolated backend and power switch");SetEnabled(false);Sync();if(Running||power.IsChecked==true)throw new Exception("EQ power off failed");
        Load(HeadphoneEqStore.Factory.First(x=>x.Name=="Combat Clarity"));preamp.Value=5;Persist();if(store.Config.LastPreset!="Combat Clarity"||store.Config.Current.Preamp!=5||!store.Config.StartEnabled)throw new Exception("Last preset and preamp were not remembered for automatic startup");
        preamp.Value=6;automaticHeadroom.IsChecked=false;Change();if(curve.Settings.Preamp!=6||curve.Settings.AutomaticHeadroom||preampValue.Text!="6.0"||!headroom.Text.Contains("+6.0"))throw new Exception("Headphone preamp UI did not apply actual +6 dB gain");automaticHeadroom.IsChecked=true;Change();Load(HeadphoneEqStore.Factory[0]);
        presets.SelectedItem=HeadphoneEqStore.Factory[3];
        if(bands[0].Value!=4||bands[4].Value!=-1)throw new Exception("EQ preset did not update bands");
        bands[0].Value=2.5;if(presets.SelectedIndex!=-1||curve.Settings.Bands[0]!=2.5f)throw new Exception("EQ custom curve binding failed");
        presetName.Text="UI fixture";SavePreset();if(!store.Config.Saved.Any(p=>p.Name=="UI fixture"&&p.Bands[0]==2.5f))throw new Exception("EQ preset save failed");
        DeletePreset();if(store.Config.Saved.Any(p=>p.Name=="UI fixture"))throw new Exception("EQ preset delete failed");
        bypass.IsChecked=true;if(!curve.Settings.Bypass)throw new Exception("EQ bypass binding failed");bypass.IsChecked=false;Load(HeadphoneEqStore.Factory[0]);
    }
    public void Dispose(){timer.Stop();engine.Dispose();Try(apo.Dispose);if(dirty)Try(Persist);}
}
