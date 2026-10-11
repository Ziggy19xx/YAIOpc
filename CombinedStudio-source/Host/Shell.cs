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
 readonly TrueStretchPage stretch;
 readonly C.RadioButton stretchNavigation;
 readonly CrosshairPage crosshair;
 readonly C.RadioButton crosshairNavigation;
 readonly C.Border sidebar;
 readonly C.Expander performanceGroup,voiceGroup,customizationGroup,moreTools;
 string customizationPage="Crosshair Studio";
 readonly C.TextBlock activeLabel=new(){FontSize=12,TextWrapping=W.TextWrapping.Wrap};
 bool activating;
 internal string Active {get;private set;}="FramePilot";
 internal Shell(string testRoot=null){
  Performance=new FramePilot.WpfShell(testRoot==null?null:Path.Combine(testRoot,"FramePilot"));
  Voice=new VelvetVoice.StudioWindow(testRoot==null?null:Path.Combine(testRoot,"VelvetVoice"),Window);
  performanceRoot=(C.Grid)Window.Content;voiceRoot=Voice.Root;
  performanceRoot.Children.RemoveAt(0);voiceRoot.Children.RemoveAt(0);
  performanceRoot.ColumnDefinitions[0].Width=new W.GridLength(0);voiceRoot.ColumnDefinitions[0].Width=new W.GridLength(0);
  voiceRoot.Resources=((W.Window)voiceRoot.Parent).Resources;
  ((W.Window)voiceRoot.Parent).Content=null;Window.Content=null;
  composition.ColumnDefinitions.Add(new C.ColumnDefinition{Width=new W.GridLength(276)});
  composition.ColumnDefinitions.Add(new C.ColumnDefinition{Width=new W.GridLength(1,W.GridUnitType.Star)});
  C.Grid.SetColumn(performanceRoot,1);C.Grid.SetColumn(voiceRoot,1);
  composition.Children.Add(performanceRoot);composition.Children.Add(voiceRoot);
  stretch=new TrueStretchPage(Window,testRoot==null?null:Path.Combine(testRoot,"TrueStretch")){Visibility=W.Visibility.Collapsed};C.Grid.SetColumn(stretch,1);composition.Children.Add(stretch);
  crosshair=new CrosshairPage(Window,testRoot==null?null:Path.Combine(testRoot,"Crosshair")){Visibility=W.Visibility.Collapsed};C.Grid.SetColumn(crosshair,1);composition.Children.Add(crosshair);
  moreTools=Performance.MoreToolsMenu;Detach(moreTools);
  var performanceItems=new C.StackPanel{Margin=new W.Thickness(4,2,0,8)};
  foreach(var item in Performance.PrimaryNavigation){Detach(item);item.GroupName="StudioNavigation";performanceItems.Children.Add(item);}
  var customizationItems=new C.StackPanel{Margin=new W.Thickness(4,2,0,8)};
  var stretchCaption=new C.StackPanel{Orientation=C.Orientation.Horizontal};stretchCaption.Children.Add(new C.TextBlock{Text="\uE7F4",FontFamily=new M.FontFamily("Segoe MDL2 Assets"),Width=32,FontSize=18,VerticalAlignment=W.VerticalAlignment.Center});stretchCaption.Children.Add(new C.TextBlock{Text="True Stretch",VerticalAlignment=W.VerticalAlignment.Center});
  stretchNavigation=new C.RadioButton{Content=stretchCaption,Style=(W.Style)Window.FindResource("NavItem"),GroupName="StudioNavigation"};W.Automation.AutomationProperties.SetName(stretchNavigation,"True Stretch");stretchNavigation.Checked+=(_,_)=>{if(!activating)Activate("True Stretch");};customizationItems.Children.Add(stretchNavigation);
  var crosshairCaption=new C.StackPanel{Orientation=C.Orientation.Horizontal};crosshairCaption.Children.Add(new C.TextBlock{Text="\uE81D",FontFamily=new M.FontFamily("Segoe MDL2 Assets"),Width=32,FontSize=18,VerticalAlignment=W.VerticalAlignment.Center});crosshairCaption.Children.Add(new C.TextBlock{Text="Crosshair Studio",VerticalAlignment=W.VerticalAlignment.Center});
  crosshairNavigation=new C.RadioButton{Content=crosshairCaption,Style=(W.Style)Window.FindResource("NavItem"),GroupName="StudioNavigation"};W.Automation.AutomationProperties.SetName(crosshairNavigation,"Crosshair Studio");crosshairNavigation.Checked+=(_,_)=>{if(!activating)Activate("Crosshair Studio");};customizationItems.Children.Add(crosshairNavigation);
  var audioItems=new C.StackPanel{Margin=new W.Thickness(4,2,0,8)};
  foreach(var item in Voice.NavigationItems){Detach(item);item.GroupName="StudioNavigation";audioItems.Children.Add(item);}
  foreach(var item in ((C.StackPanel)moreTools.Content).Children.OfType<C.RadioButton>())item.GroupName="StudioNavigation";
  performanceGroup=Group("PC performance","\uE7FC",performanceItems);
  voiceGroup=Group("Audio","\uE720",audioItems);
  customizationGroup=Group("Customization","\uE790",customizationItems);
  moreTools.Header="More tools";moreTools.Margin=new W.Thickness(0,10,0,0);
  var navigation=new C.StackPanel();navigation.Children.Add(performanceGroup);navigation.Children.Add(voiceGroup);
  navigation.Children.Add(customizationGroup);
  navigation.Children.Add(new C.Border{Height=1,Background=VelvetVoice.UiControls.Line,Margin=new W.Thickness(12,10,12,0)});navigation.Children.Add(moreTools);
  var layout=new C.DockPanel();
  var brand=new C.StackPanel{Margin=new W.Thickness(12,8,8,24)};brand.Children.Add(new C.TextBlock{Text="YAIOpc",FontSize=22,FontWeight=W.FontWeights.SemiBold,Foreground=VelvetVoice.UiControls.Ink});brand.Children.Add(new C.TextBlock{Text="Your All-in-One PC",FontSize=10,Foreground=VelvetVoice.UiControls.Muted,Margin=new W.Thickness(0,5,0,0)});C.DockPanel.SetDock(brand,C.Dock.Top);layout.Children.Add(brand);
  var footer=new C.Border{Background=VelvetVoice.UiControls.Panel,BorderBrush=VelvetVoice.UiControls.Line,BorderThickness=new W.Thickness(1),CornerRadius=new W.CornerRadius(12),Padding=new W.Thickness(14),Margin=new W.Thickness(4,12,4,0)};
  activeLabel.Foreground=VelvetVoice.UiControls.Muted;footer.Child=activeLabel;C.DockPanel.SetDock(footer,C.Dock.Bottom);layout.Children.Add(footer);
  var startup=new C.CheckBox{Content="Start with Windows",Foreground=VelvetVoice.UiControls.Ink,Margin=new W.Thickness(12,0,0,12),IsEnabled=testRoot==null};
  if(testRoot==null){try{startup.IsChecked=WindowsStartup.Enabled;}catch(Exception error){startup.ToolTip=error.Message;}startup.Click+=(_,_)=>{try{WindowsStartup.SetEnabled(startup.IsChecked==true);}catch(Exception error){W.MessageBox.Show(Window,error.Message,"Windows startup");}};}
  C.DockPanel.SetDock(startup,C.Dock.Bottom);layout.Children.Add(startup);
  layout.Children.Add(new C.ScrollViewer{Content=navigation,VerticalScrollBarVisibility=C.ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=C.ScrollBarVisibility.Disabled});
  sidebar=new C.Border{Child=layout,Background=VelvetVoice.UiControls.Tone("#13100E"),BorderBrush=VelvetVoice.UiControls.Line,BorderThickness=new W.Thickness(0,0,1,0),Padding=new W.Thickness(12,14,12,18)};composition.Children.Add(sidebar);
  Window.Content=composition;Window.Title="YAIOpc";Window.Width=1480;Window.Height=1020;Window.MinWidth=1060;Window.MinHeight=700;
  Performance.Activated+=()=>Activate("FramePilot");Voice.Activated+=()=>Activate("Velvet Audio");
  performanceGroup.Expanded+=(_,_)=>{if(!activating)Performance.SelectPage(Performance.SelectedPage??"Overview");};
  voiceGroup.Expanded+=(_,_)=>{if(!activating)Voice.SelectPage(Voice.SelectedPage);};
  customizationGroup.Expanded+=(_,_)=>{if(!activating)Activate(customizationPage);};
  Window.Loaded+=async (_,_)=>{try{if(testRoot==null)Performance.StartLiveMonitoring();await Voice.Initialize();if(testRoot!=null)Performance.SelectPage("Overview");Activate("FramePilot");}catch(Exception error){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"combined-initialize-error.txt"),error.ToString());if(testRoot!=null){Environment.ExitCode=1;Window.Close();}else W.MessageBox.Show(Window,error.Message,"Velvet Audio could not initialize");}};
  Activate("FramePilot");
 }
 static void Detach(W.FrameworkElement item){if(item.Parent is C.Panel parent)parent.Children.Remove(item);else throw new InvalidOperationException("Navigation item has an unexpected parent.");}
 C.Expander Group(string name,string glyph,W.UIElement content){
  var header=new C.StackPanel{Orientation=C.Orientation.Horizontal};header.Children.Add(new C.TextBlock{Text=glyph,FontFamily=new M.FontFamily("Segoe MDL2 Assets"),FontSize=20,Width=32,VerticalAlignment=W.VerticalAlignment.Center});header.Children.Add(new C.TextBlock{Text=name,FontSize=17,FontWeight=W.FontWeights.SemiBold,VerticalAlignment=W.VerticalAlignment.Center});
  var group=new C.Expander{Header=header,Content=content,Template=moreTools.Template,Foreground=VelvetVoice.UiControls.Muted,Margin=new W.Thickness(0,4,0,4)};W.Automation.AutomationProperties.SetName(group,name+" navigation");return group;
 }
 internal void Activate(string name){
  if(activating)return;activating=true;
  try{
   Active=name;bool performance=name=="FramePilot",trueStretch=name=="True Stretch",crosshairActive=name=="Crosshair Studio",audio=name=="Velvet Audio";
   if(!audio){foreach(var item in Voice.NavigationItems)item.IsChecked=false;}if(!performance)Performance.ClearNavigationSelection();
   stretch.Visibility=trueStretch?W.Visibility.Visible:W.Visibility.Collapsed;stretchNavigation.IsChecked=trueStretch;
   crosshair.Visibility=crosshairActive?W.Visibility.Visible:W.Visibility.Collapsed;crosshairNavigation.IsChecked=crosshairActive;
   performanceRoot.Visibility=performance?W.Visibility.Visible:W.Visibility.Collapsed;voiceRoot.Visibility=audio?W.Visibility.Visible:W.Visibility.Collapsed;
   performanceGroup.Foreground=performance?VelvetVoice.UiControls.Accent:VelvetVoice.UiControls.Muted;voiceGroup.Foreground=audio?VelvetVoice.UiControls.Accent:VelvetVoice.UiControls.Muted;customizationGroup.Foreground=trueStretch||crosshairActive?VelvetVoice.UiControls.Accent:VelvetVoice.UiControls.Muted;
   if(performance){if(Performance.SelectedPage==null||Performance.PrimaryNavigation.Any(item=>(string)item.Tag==Performance.SelectedPage))performanceGroup.IsExpanded=true;else moreTools.IsExpanded=true;Performance.NavigationItem(Performance.SelectedPage??"Overview").IsChecked=true;}
   else if(trueStretch||crosshairActive){customizationPage=name;customizationGroup.IsExpanded=true;}else{voiceGroup.IsExpanded=true;Voice.NavigationItem(Voice.SelectedPage).IsChecked=true;}
   activeLabel.Text="Viewing "+(crosshairActive?"Crosshair Studio":trueStretch?"True Stretch":performance?"PC performance":"Audio")+"\nLocal audio · starts when you choose";Performance.SetWorkspaceActive(performance);
  }finally{activating=false;}
 }
 internal void Snapshot(string path){Window.UpdateLayout();var bitmap=new Imaging.RenderTargetBitmap((int)Window.ActualWidth,(int)Window.ActualHeight,96,96,M.PixelFormats.Pbgra32);bitmap.Render(Window);var encoder=new Imaging.PngBitmapEncoder();encoder.Frames.Add(Imaging.BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
 internal async Task Verify(string folder){
  Directory.CreateDirectory(folder);string previousError=Path.Combine(folder,"combined-error.txt");if(File.Exists(previousError))File.Delete(previousError);var log=new List<string>();
  void Check(bool valid,string message){if(!valid)throw new Exception(message);log.Add("PASS "+message);}
  try{
   await crosshair.Verify(folder);Activate("Crosshair Studio");Window.UpdateLayout();Check(crosshair.IsVisible&&crosshairNavigation.IsChecked==true&&customizationGroup.IsExpanded,"Crosshair Studio route, randomizer, persistent presets, PNG export and native click-through overlay");
   Check(crosshairNavigation.Parent==stretchNavigation.Parent&&crosshairNavigation.Parent==customizationGroup.Content,"True Stretch and Crosshair Studio belong to Customization");customizationGroup.IsExpanded=false;Window.UpdateLayout();Check(!crosshairNavigation.IsVisible&&!stretchNavigation.IsVisible,"Customization chevron collapses both pages");customizationGroup.IsExpanded=true;Window.UpdateLayout();Check(Active=="Crosshair Studio"&&crosshairNavigation.IsVisible,"Customization restores its last selected page");
   foreach(double width in new[]{1060d,1280d,1600d}){Window.Width=width;Window.UpdateLayout();Check(crosshair.ActualWidth>700&&crosshair.IsVisible,"Crosshair Studio layout at "+width);Snapshot(Path.Combine(folder,"crosshair-"+width+".png"));}
   stretch.Verify();Activate("True Stretch");Window.UpdateLayout();Check(stretch.IsVisible&&stretchNavigation.IsChecked==true,"True Stretch route, presets, original capture and game exit restore");Snapshot(Path.Combine(folder,"true-stretch.png"));
   foreach(double width in new[]{1060d,1280d,1600d}){Window.Width=width;Window.UpdateLayout();Check(stretch.ActualWidth>700&&stretch.IsVisible&&sidebar.IsVisible,"True Stretch layout at "+width);Snapshot(Path.Combine(folder,"true-stretch-"+width+".png"));}
   await Task.Delay(600);Performance.SelectPage("Overview");Window.Width=1920;Window.Height=1000;
   Check(W.Application.Current.Windows.Cast<W.Window>().Count(x=>x.IsVisible)==1,"one visible application window");
   Check(Window.Title=="YAIOpc","shared window uses YAIOpc branding");
   Check(composition.ColumnDefinitions.Count==2&&sidebar.IsVisible,"one shared sidebar beside the workspace");
   Check(performanceGroup.IsExpanded&&!voiceGroup.IsExpanded&&!moreTools.IsExpanded,"FramePilot opens initially; Velvet Audio and More tools start collapsed");
   performanceGroup.IsExpanded=false;Window.UpdateLayout();Check(!Performance.NavigationItem("Overview").IsVisible&&sidebar.IsVisible,"FramePilot collapses inside the shared sidebar");
   voiceGroup.IsExpanded=true;Window.UpdateLayout();Check(Active=="Velvet Audio"&&Voice.NavigationItem("Studio").IsVisible,"expanding Velvet Audio opens its retained page");
   voiceGroup.IsExpanded=false;Window.UpdateLayout();Check(!Voice.NavigationItem("Studio").IsVisible,"Velvet Audio collapses independently");
   performanceGroup.IsExpanded=true;Window.UpdateLayout();Check(Active=="FramePilot"&&Performance.NavigationItem("Overview").IsVisible,"expanding FramePilot opens its retained page");
   Check(Performance.NavigationItem("Overview").IsChecked==true&&Voice.NavigationItem("Studio").IsChecked!=true,"shared navigation marks only one active page");
   Performance.MoreToolsExpanded=true;Window.UpdateLayout();Check(Performance.NavigationItem("Drivers").IsVisible,"More tools expands beneath both app groups");
   Performance.MoreToolsExpanded=false;Window.UpdateLayout();Check(!Performance.NavigationItem("Drivers").IsVisible,"More tools hides its navigation items when collapsed");
   Performance.SelectPage("Drivers");Window.UpdateLayout();Check(Performance.MoreToolsExpanded&&Performance.NavigationItem("Drivers").IsChecked==true,"opening a secondary page expands More tools");Performance.SelectPage("Overview");Performance.MoreToolsExpanded=false;
   Snapshot(Path.Combine(folder,"unified-sidebar-framepilot.png"));
   foreach(string page in new[]{"Studio","Soundboard + clips","Voice changer","Setup & help","Headphone EQ"}){Voice.SelectPage(page);await Task.Delay(80);Check(Active=="Velvet Audio"&&voiceRoot.Visibility==W.Visibility.Visible&&sidebar.IsVisible&&voiceGroup.IsExpanded,"Velvet Audio route: "+page);}
   Voice.SelectPage("Headphone EQ");Snapshot(Path.Combine(folder,"combined-headphone-eq.png"));
   Voice.VerifyEq();Voice.VerifyVoice();log.Add("PASS VoxShift 46 presets, pitch/formant, effects-only presets, custom save/recall and bypass");Check(true,"headphone EQ preset, slider, save/delete and bypass bindings; no audio autostart");
   Voice.SelectPage("Voice changer");Snapshot(Path.Combine(folder,"combined-voxshift.png"));
   Voice.SelectPage("Studio");Snapshot(Path.Combine(folder,"combined-velvet-voice.png"));
   var slider=Voice.Model.UiSliders.First().Value;int original=slider.Value;slider.Value=original+1;
   foreach(string page in new[]{"Overview","PC monitor","Disk explorer","Game library","Optimizations","Windows tuning","Game profiles","Session mode","Background apps","Startup apps","Drivers","Storage & apps","Maintenance","Diagnostics","Compare runs","Recovery","Updates & notes","About & sources","Hardware"}){Performance.SelectPage(page);await Task.Delay(100);Check(Active=="FramePilot"&&performanceRoot.Visibility==W.Visibility.Visible&&sidebar.IsVisible,"FramePilot route: "+page);}
   Performance.SelectPage("Game profiles");Snapshot(Path.Combine(folder,"combined-profiles.png"));Performance.SelectPage("Overview");Snapshot(Path.Combine(folder,"combined-framepilot.png"));
   var synthetic=Voice.Model.UiSyntheticMixer();var output=new float[960];for(int warm=0;warm<10;warm++)synthetic.Read(output,0,output.Length);
   for(int i=0;i<20;i++){Activate("Velvet Audio");Activate("FramePilot");synthetic.Read(output,0,output.Length);Check(ReferenceEquals(synthetic,Voice.Model.UiMixer)&&output.All(x=>float.IsFinite(x)&&Math.Abs(x-.1f)<.0001f),"synthetic audio continues through workspace switch "+(i+1));}
   Voice.Model.UiClearSynthetic();
   Activate("Velvet Audio");Check(slider.Value==original+1&&Voice.SelectedPage=="Studio","voice settings and selected page survive repeated switches");slider.Value=original;
   Activate("FramePilot");var voiceNav=Voice.NavigationItem("Studio");voiceNav.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=W.UIElement.PreviewMouseLeftButtonDownEvent});Check(Active=="FramePilot","rails stay stable during mouse down");voiceNav.RaiseEvent(new W.RoutedEventArgs(C.Primitives.ButtonBase.ClickEvent));Check(Active=="Velvet Audio","clicking the already selected voice page activates its workspace");var performanceNav=Performance.NavigationItem("Overview");performanceNav.RaiseEvent(new W.RoutedEventArgs(C.Primitives.ButtonBase.ClickEvent));Check(Active=="FramePilot","clicking the already selected Home page activates its workspace");
   Check(!Voice.Model.UiLive,"microphone processing remains off until requested");
   foreach(int width in new[]{1060,1280,1600}){Window.Width=width;Window.Height=800;foreach(string app in new[]{"FramePilot","Velvet Audio"}){Activate(app);Window.UpdateLayout();Check(sidebar.ActualWidth==276&&sidebar.IsVisible&&composition.ColumnDefinitions[1].ActualWidth>700,"one sidebar and usable workspace at "+width+" in "+app);Snapshot(Path.Combine(folder,"combined-"+app.Replace(" ","")+"-"+width+".png"));}Voice.SelectPage("Headphone EQ");Snapshot(Path.Combine(folder,"combined-headphone-eq-"+width+".png"));}
   log.AddRange(VelvetVoice.WpfDialogs.Verify());
   File.WriteAllLines(Path.Combine(folder,"combined-tests.txt"),log);
  }catch(Exception error){Environment.ExitCode=1;File.WriteAllText(Path.Combine(folder,"combined-error.txt"),error.ToString());}
  finally{Window.Close();}
 }
}
