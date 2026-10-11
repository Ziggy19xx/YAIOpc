using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using A=System.Windows.Media.Animation;
using U=VelvetVoice.UiControls;
namespace CombinedStudio;
internal sealed class CrosshairRandomOptions {
 public string Category {get;set;}="All designs";
 public int Duration {get;set;}=3;
 public int Delay {get;set;}=5;
 public bool LockColor {get;set;}
 public bool LockShape {get;set;}
 public bool Large {get;set;}
 public bool Rotate {get;set;}
 internal static readonly string[] Categories={"All designs","Precision","Rings & targets","Creative","Saved presets"};
 internal void Validate(){if(!Categories.Contains(Category)||Duration<0||Duration>6||Delay<2||Delay>30)throw new ArgumentException("Invalid randomizer settings.");}
}
internal static class CrosshairGenerator {
 internal static CrosshairDesign Generate(Random random,CrosshairRandomOptions options,CrosshairDesign current,IEnumerable<CrosshairPreset> saved){
  options.Validate();var pool=(options.Category=="Saved presets"?saved:CrosshairStore.BuiltIn()).Where(x=>options.Category switch{"Precision"=>new[]{"Cross","T shape","Dot","Chevron"}.Contains(x.Design.Shape),"Rings & targets"=>new[]{"Ring","Ring + cross","Double ring","Diamond","Square","Brackets"}.Contains(x.Design.Shape),"Creative"=>new[]{"Burst","Star","Triangle","Chevron"}.Contains(x.Design.Shape),_=>true}).ToArray();
  if(pool.Length==0)throw new ArgumentException("Save a design first, or choose another category.");var design=pool[random.Next(pool.Length)].Design.Copy();
  if(options.Category!="Saved presets"){design.Length=random.Next(3,options.Large?36:18);design.Thickness=random.Next(1,options.Large?7:4);design.Gap=random.Next(1,options.Large?16:9);design.Radius=random.Next(6,options.Large?38:20);design.DotSize=random.Next(1,options.Large?9:5);design.Dot=random.Next(2)==0;design.Outline=random.Next(3);design.Color=$"#{random.Next(80,256):X2}{random.Next(80,256):X2}{random.Next(80,256):X2}";design.Opacity=100;design.Rotation=options.Rotate?random.Next(0,24)*15:0;}
  if(options.LockColor)design.Color=current.Color;if(options.LockShape){design.Shape=current.Shape;design.ImageFile=current.ImageFile;design.ImageSize=current.ImageSize;}design.Validate();return design;
 }
}
internal sealed class CrosshairRandomizer:C.StackPanel,IDisposable {
 readonly CrosshairStore store;readonly Action<CrosshairDesign> apply;readonly Action libraryChanged;readonly Random random=new();
 readonly C.Grid viewport=new(){Height=170,ClipToBounds=true};readonly C.StackPanel strip=new(){Orientation=C.Orientation.Horizontal};readonly M.TranslateTransform translation=new();
 readonly C.TextBlock result=U.Text("Spin to discover a crosshair.",14,U.Muted);readonly C.Button spin=new(){Content="SPIN",MinWidth=180,MinHeight=48,Margin=new W.Thickness(0,0,12,12)};
 readonly C.CheckBox auto=new(){Content="Auto spin while this page is open",Foreground=U.Ink,Margin=new W.Thickness(0,4,0,12)};
 readonly System.Windows.Threading.DispatcherTimer autoTimer=new();CrosshairDesign selected;bool spinning,disposed;int generation;
 internal CrosshairRandomizer(CrosshairStore store,Action<CrosshairDesign> apply,Action libraryChanged){this.store=store;this.apply=apply;this.libraryChanged=libraryChanged;store.Settings.Randomizer??=new();var options=store.Settings.Randomizer;
  Children.Add(U.Text("Randomizer",20));Children.Add(U.Text("Spin the carousel, keep a favorite, or generate a fresh design. Every spin is free.",12,U.Muted));
  strip.RenderTransform=translation;var track=new C.Canvas();track.Children.Add(strip);viewport.Children.Add(track);viewport.Children.Add(new C.Border{Width=146,BorderBrush=U.Accent,BorderThickness=new W.Thickness(2),CornerRadius=new W.CornerRadius(10),HorizontalAlignment=W.HorizontalAlignment.Center,IsHitTestVisible=false});viewport.Children.Add(new C.TextBlock{Text="▼",Foreground=U.Accent,FontSize=20,HorizontalAlignment=W.HorizontalAlignment.Center,VerticalAlignment=W.VerticalAlignment.Top,IsHitTestVisible=false});Children.Add(viewport);
  var actions=new C.WrapPanel{Margin=new W.Thickness(0,16,0,0)};spin.Click+=async (_,_)=>await Spin();actions.Children.Add(spin);actions.Children.Add(Button("Save result",()=>{if(selected==null)throw new ArgumentException("Spin first to generate a design.");apply(selected.Copy());store.SavePreset("Random "+DateTime.Now.ToString("HHmmss-fff"));libraryChanged();result.Text="Result saved to your preset library.";}));Children.Add(actions);Children.Add(result);
  var category=new C.ComboBox{ItemsSource=CrosshairRandomOptions.Categories,SelectedItem=options.Category,MinHeight=40,Margin=new W.Thickness(0,0,0,10)};category.SelectionChanged+=(_,_)=>Run(()=>{options.Category=(string)category.SelectedItem;store.Save();});Children.Add(category);
  var locks=new C.WrapPanel();locks.Children.Add(Toggle("Keep color",options.LockColor,value=>options.LockColor=value));locks.Children.Add(Toggle("Keep shape",options.LockShape,value=>options.LockShape=value));locks.Children.Add(Toggle("Larger designs",options.Large,value=>options.Large=value));locks.Children.Add(Toggle("Random rotation",options.Rotate,value=>options.Rotate=value));Children.Add(locks);
  var settings=new C.StackPanel();settings.Children.Add(Slider("Spin duration (seconds)",0,6,options.Duration,value=>options.Duration=value));settings.Children.Add(Slider("Auto spin delay (seconds)",2,30,options.Delay,value=>{options.Delay=value;autoTimer.Interval=TimeSpan.FromSeconds(value);}));settings.Children.Add(auto);Children.Add(new C.Expander{Header="Spin settings",Foreground=U.Ink,Content=settings});
  autoTimer.Interval=TimeSpan.FromSeconds(options.Delay);autoTimer.Tick+=async (_,_)=>{if(auto.IsChecked==true&&IsVisible&&!spinning)await Spin();};auto.Click+=(_,_)=>{if(auto.IsChecked==true&&IsVisible)autoTimer.Start();else autoTimer.Stop();};IsVisibleChanged+=(_,_)=>{if(IsVisible&&auto.IsChecked==true)autoTimer.Start();else{autoTimer.Stop();Cancel();}};
  Loaded+=(_,_)=>{if(selected==null)Fill(CrosshairStore.BuiltIn().Take(7).Select(x=>x.Design).ToArray(),3);};viewport.SizeChanged+=(_,_)=>{if(!spinning)CenterStrip();};
 }
 int winnerIndex=3;
 void Fill(CrosshairDesign[] designs,int winner){strip.Children.Clear();winnerIndex=winner;foreach(var design in designs){var tile=new C.StackPanel();tile.Children.Add(new CrosshairView{Design=design,ImageFolder=store.Folder,Width=140,Height=116});tile.Children.Add(U.Text(design.Shape,11,U.Muted));strip.Children.Add(new C.Border{Child=tile,Width=146,Margin=new W.Thickness(3,8,3,8),CornerRadius=new W.CornerRadius(9),Background=U.Raised,BorderBrush=U.Line,BorderThickness=new W.Thickness(1)});}CenterStrip();}
 double Target=>viewport.ActualWidth/2-(winnerIndex*152+76);
 void CenterStrip(){translation.BeginAnimation(M.TranslateTransform.XProperty,null);translation.X=Target;}
 internal async Task Spin(){if(spinning||disposed)return;int token=++generation;try{spinning=true;spin.IsEnabled=false;var options=store.Settings.Randomizer;var current=store.Settings.Design.Copy();var designs=Enumerable.Range(0,25).Select(_=>CrosshairGenerator.Generate(random,options,current,store.Settings.Presets)).ToArray();Fill(designs,21);selected=null;result.Text="Spinning…";
   double start=viewport.ActualWidth/2-76;translation.X=start;var animation=new A.DoubleAnimation(start,Target,TimeSpan.FromSeconds(options.Duration)){EasingFunction=new A.QuinticEase{EasingMode=A.EasingMode.EaseOut},FillBehavior=A.FillBehavior.HoldEnd};translation.BeginAnimation(M.TranslateTransform.XProperty,animation);
   if(options.Duration>0)await Task.Delay(options.Duration*1000);if(disposed||token!=generation)return;CenterStrip();selected=designs[21].Copy();apply(selected.Copy());result.Text="Selected: "+selected.Shape+" · "+selected.Color+". Edit it below or save the result.";
  }catch(Exception error){result.Text=error.Message;}finally{if(!disposed&&token==generation){spinning=false;spin.IsEnabled=true;}}}
 void Cancel(){if(spinning)result.Text="Spin canceled. Press SPIN to try again.";generation++;spinning=false;spin.IsEnabled=true;CenterStrip();}
 void Run(Action action){try{action();}catch(Exception error){result.Text=error.Message;}}
 C.Button Button(string caption,Action action){var button=new C.Button{Content=caption,MinHeight=48,Margin=new W.Thickness(0,0,12,12),Padding=new W.Thickness(14,8,14,8)};button.Click+=(_,_)=>Run(action);return button;}
 C.CheckBox Toggle(string caption,bool value,Action<bool> write){var control=new C.CheckBox{Content=caption,IsChecked=value,Foreground=U.Ink,Margin=new W.Thickness(0,4,18,12)};control.Click+=(_,_)=>Run(()=>{write(control.IsChecked==true);store.Save();});return control;}
 W.UIElement Slider(string caption,int min,int max,int value,Action<int> write){var row=new C.StackPanel();var label=U.Text(caption+": "+value,12,U.Muted);var slider=new C.Slider{Minimum=min,Maximum=max,Value=value,IsSnapToTickEnabled=true,TickFrequency=1,Margin=new W.Thickness(0,0,0,12)};slider.ValueChanged+=(_,_)=>Run(()=>{int v=(int)slider.Value;write(v);label.Text=caption+": "+v;store.Save();});row.Children.Add(label);row.Children.Add(slider);return row;}
 internal async Task Verify(){int duration=store.Settings.Randomizer.Duration;try{store.Settings.Randomizer.Duration=1;var pending=Spin();Cancel();store.Settings.Randomizer.Duration=0;await Spin();var winner=selected;if(winner==null)throw new Exception("Randomizer did not produce a result.");await pending;if(!ReferenceEquals(winner,selected)||spinning)throw new Exception("Canceled spin overwrote a newer result.");if(store.Settings.Design.Shape!=selected.Shape||Math.Abs(translation.X-Target)>.1)throw new Exception("Randomizer did not apply or center its selected result.");}finally{store.Settings.Randomizer.Duration=duration;}}
 public void Dispose(){disposed=true;autoTimer.Stop();Cancel();}
}
