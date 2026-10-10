using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using Imaging=System.Windows.Media.Imaging;
namespace CombinedStudio;
internal sealed class Shell {
 internal readonly FramePilot.WpfShell Performance;
 internal readonly VelvetVoice.StudioWindow Voice;
 internal W.Window Window=>Performance.Window;
 readonly C.Grid composition=new();
 readonly C.Grid performanceRoot,voiceRoot;
 readonly W.FrameworkElement performanceRail,voiceRail;
 internal string Active {get;private set;}="FramePilot";
 internal Shell(string testRoot=null){
  Performance=new FramePilot.WpfShell(testRoot==null?null:Path.Combine(testRoot,"FramePilot"));
  Voice=new VelvetVoice.StudioWindow(testRoot==null?null:Path.Combine(testRoot,"VelvetVoice"),Window);
  performanceRoot=(C.Grid)Window.Content;voiceRoot=Voice.Root;
  performanceRail=(W.FrameworkElement)performanceRoot.Children[0];voiceRail=(W.FrameworkElement)voiceRoot.Children[0];
  performanceRoot.Children.Remove(performanceRail);voiceRoot.Children.Remove(voiceRail);
  performanceRoot.ColumnDefinitions[0].Width=new W.GridLength(0);voiceRoot.ColumnDefinitions[0].Width=new W.GridLength(0);
  voiceRoot.Resources=((W.Window)voiceRoot.Parent).Resources;
  voiceRail.Resources=voiceRoot.Resources;
  ((W.Window)voiceRoot.Parent).Content=null;Window.Content=null;
  for(int i=0;i<3;i++)composition.ColumnDefinitions.Add(new C.ColumnDefinition{Width=i==2?new W.GridLength(1,W.GridUnitType.Star):new W.GridLength(232)});
  C.Grid.SetColumn(performanceRoot,2);C.Grid.SetColumn(voiceRoot,2);
  C.Grid.SetRow(performanceRail,0);C.Grid.SetRowSpan(performanceRail,1);
  composition.Children.Add(performanceRoot);composition.Children.Add(voiceRoot);composition.Children.Add(performanceRail);composition.Children.Add(voiceRail);
  Window.Content=composition;Window.Title="FramePilot + Velvet Voice";Window.Width=1720;Window.Height=1020;Window.MinWidth=1280;Window.MinHeight=700;
  Performance.Activated+=()=>Activate("FramePilot");Voice.Activated+=()=>Activate("Velvet Voice");
  performanceRail.MouseLeftButtonUp+=(_,_)=>Activate("FramePilot");voiceRail.MouseLeftButtonUp+=(_,_)=>Activate("Velvet Voice");
  Window.Loaded+=async (_,_)=>{try{await Voice.Initialize();if(testRoot!=null)Performance.SelectPage("Overview");Activate("FramePilot");}catch(Exception error){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"combined-initialize-error.txt"),error.ToString());if(testRoot!=null){Environment.ExitCode=1;Window.Close();}else W.MessageBox.Show(Window,error.Message,"Velvet Voice could not initialize");}};
  Activate("FramePilot");
 }
 internal void Activate(string name){
  Active=name;bool performance=name=="FramePilot";
  C.Grid.SetColumn(performanceRail,performance?1:0);C.Grid.SetColumn(voiceRail,performance?0:1);
  composition.ColumnDefinitions[0].Width=new W.GridLength(performance?232:252);
  composition.ColumnDefinitions[1].Width=new W.GridLength(performance?252:232);
  performanceRoot.Visibility=performance?W.Visibility.Visible:W.Visibility.Collapsed;
  voiceRoot.Visibility=performance?W.Visibility.Collapsed:W.Visibility.Visible;
  Performance.SetWorkspaceActive(performance);
 }
 internal void Snapshot(string path){Window.UpdateLayout();var bitmap=new Imaging.RenderTargetBitmap((int)Window.ActualWidth,(int)Window.ActualHeight,96,96,M.PixelFormats.Pbgra32);bitmap.Render(Window);var encoder=new Imaging.PngBitmapEncoder();encoder.Frames.Add(Imaging.BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
 internal async Task Verify(string folder){
  Directory.CreateDirectory(folder);string previousError=Path.Combine(folder,"combined-error.txt");if(File.Exists(previousError))File.Delete(previousError);var log=new List<string>();
  void Check(bool valid,string message){if(!valid)throw new Exception(message);log.Add("PASS "+message);}
  try{
   await Task.Delay(600);Performance.SelectPage("Overview");Window.Width=1920;Window.Height=1000;
   Check(W.Application.Current.Windows.Cast<W.Window>().Count(x=>x.IsVisible)==1,"one visible application window");
   foreach(string page in new[]{"Studio","Soundboard + clips","Voice changer","Setup & help"}){Voice.SelectPage(page);await Task.Delay(80);Check(Active=="Velvet Voice"&&voiceRoot.Visibility==W.Visibility.Visible&&C.Grid.GetColumn(voiceRail)==1,"Velvet Voice route: "+page);}
   Voice.SelectPage("Studio");Snapshot(Path.Combine(folder,"combined-velvet-voice.png"));
   var slider=Voice.Model.UiSliders.First().Value;int original=slider.Value;slider.Value=original+1;
   foreach(string page in new[]{"Overview","PC monitor","Disk explorer","Game library","Optimizations","Windows tuning","Game profiles","Session mode","Background apps","Startup apps","Drivers","Storage & apps","Maintenance","Diagnostics","Compare runs","Recovery","Updates & notes","About & sources","Hardware"}){Performance.SelectPage(page);await Task.Delay(100);Check(Active=="FramePilot"&&performanceRoot.Visibility==W.Visibility.Visible&&C.Grid.GetColumn(performanceRail)==1,"FramePilot route: "+page);}
   Performance.SelectPage("Game profiles");Snapshot(Path.Combine(folder,"combined-profiles.png"));Performance.SelectPage("Overview");Snapshot(Path.Combine(folder,"combined-framepilot.png"));
   var synthetic=Voice.Model.UiSyntheticMixer();var output=new float[960];for(int warm=0;warm<10;warm++)synthetic.Read(output,0,output.Length);
   for(int i=0;i<20;i++){Activate("Velvet Voice");Activate("FramePilot");synthetic.Read(output,0,output.Length);Check(ReferenceEquals(synthetic,Voice.Model.UiMixer)&&output.All(x=>float.IsFinite(x)&&Math.Abs(x-.1f)<.0001f),"synthetic audio continues through workspace switch "+(i+1));}
   Voice.Model.UiClearSynthetic();
   Activate("Velvet Voice");Check(slider.Value==original+1&&Voice.SelectedPage=="Studio","voice settings and selected page survive repeated switches");slider.Value=original;
   Activate("FramePilot");var voiceNav=Voice.NavigationItem("Studio");voiceNav.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=W.UIElement.PreviewMouseLeftButtonDownEvent});Check(Active=="FramePilot","rails stay stable during mouse down");voiceNav.RaiseEvent(new W.RoutedEventArgs(C.Primitives.ButtonBase.ClickEvent));Check(Active=="Velvet Voice","clicking the already selected voice page activates its workspace");var performanceNav=Performance.NavigationItem("Overview");performanceNav.RaiseEvent(new W.RoutedEventArgs(C.Primitives.ButtonBase.ClickEvent));Check(Active=="FramePilot","clicking the already selected Home page activates its workspace");
   Check(!Voice.Model.UiLive,"microphone processing remains off until requested");
   foreach(int width in new[]{1280,1600}){Window.Width=width;Window.Height=800;foreach(string app in new[]{"FramePilot","Velvet Voice"}){Activate(app);Window.UpdateLayout();Check(performanceRail.ActualWidth>200&&voiceRail.ActualWidth>200,"both navigation rails visible at "+width+" in "+app);Snapshot(Path.Combine(folder,"combined-"+app.Replace(" ","")+"-"+width+".png"));}}
   log.AddRange(VelvetVoice.WpfDialogs.Verify());
   File.WriteAllLines(Path.Combine(folder,"combined-tests.txt"),log);
  }catch(Exception error){Environment.ExitCode=1;File.WriteAllText(Path.Combine(folder,"combined-error.txt"),error.ToString());}
  finally{Window.Close();}
 }
}



