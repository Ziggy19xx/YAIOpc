using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using Imaging=System.Windows.Media.Imaging;
namespace CombinedStudio;
internal sealed partial class Shell {
 internal readonly FramePilot.WpfShell Performance;
 internal readonly VelvetVoice.StudioWindow Voice;
 internal W.Window Window=>Performance.Window;
 readonly C.Grid composition=new();
 readonly C.Grid performanceRoot,voiceRoot;
 readonly FpsPage fps;readonly C.RadioButton fpsNavigation;
 readonly TrueStretchPage stretch;
 readonly C.RadioButton stretchNavigation;
 readonly CrosshairPage crosshair;
 readonly C.RadioButton crosshairNavigation;
 readonly AppThemeService themeService;
 readonly ThemesPage themes;
 readonly C.RadioButton themesNavigation;readonly C.RadioButton homeNavigation;readonly C.RadioButton gameLibraryNavigation;string performancePage="PC monitor";
 readonly C.Border sidebar;
 readonly C.Expander performanceGroup,voiceGroup,customizationGroup,moreTools;
 string customizationPage="Crosshair Studio";
 readonly C.TextBlock activeLabel=new(){FontSize=12,TextWrapping=W.TextWrapping.Wrap};
 bool activating;
 static Imaging.BitmapFrame BrandImage(string resource){using var stream=typeof(Shell).Assembly.GetManifestResourceStream(resource)??throw new InvalidOperationException("Missing brand asset: "+resource);var image=Imaging.BitmapFrame.Create(stream,Imaging.BitmapCreateOptions.None,Imaging.BitmapCacheOption.OnLoad);image.Freeze();return image;}
 internal string Active {get;private set;}="Home";
 internal Shell(string testRoot=null){
  themeService=new AppThemeService(testRoot==null?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YAIOpc","Appearance"):Path.Combine(testRoot,"Appearance"));
  themeService.UseReferenceStyleOnce();
  Performance=new FramePilot.WpfShell(testRoot==null?null:Path.Combine(testRoot,"FramePilot"));
  CyberSidebar.Install(Window);Performance.StartupPageOverride="Overview";
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
  themes=new ThemesPage(themeService){Visibility=W.Visibility.Collapsed};C.Grid.SetColumn(themes,1);composition.Children.Add(themes);
  fps=new FpsPage(Window,testRoot==null?null:Path.Combine(testRoot,"FPS")){Visibility=W.Visibility.Collapsed};C.Grid.SetColumn(fps,1);composition.Children.Add(fps);
  moreTools=Performance.MoreToolsMenu;Detach(moreTools);
  var performanceItems=new C.StackPanel{Margin=new W.Thickness(4,2,0,8)};
  foreach(var item in Performance.PrimaryNavigation){Detach(item);item.GroupName="StudioNavigation";if((string)item.Tag!="Overview"&&(string)item.Tag!="Game library")performanceItems.Children.Add(item);}
  fpsNavigation=new C.RadioButton{Content=CyberSidebar.Caption("\uE9D9","FPS Counter"),Style=(W.Style)Window.FindResource("NavItem"),GroupName="StudioNavigation"};W.Automation.AutomationProperties.SetName(fpsNavigation,"FPS Counter");fpsNavigation.Checked+=(_,_)=>{if(!activating)Activate("FPS Counter");};performanceItems.Children.Insert(1,fpsNavigation);
  var customizationItems=new C.StackPanel{Margin=new W.Thickness(4,2,0,8)};
  var stretchCaption=new C.StackPanel{Orientation=C.Orientation.Horizontal};stretchCaption.Children.Add(new C.TextBlock{Text="\uE7F4",FontFamily=new M.FontFamily("Segoe MDL2 Assets"),Width=32,FontSize=18,VerticalAlignment=W.VerticalAlignment.Center});stretchCaption.Children.Add(new C.TextBlock{Text="True Stretch",VerticalAlignment=W.VerticalAlignment.Center});
  stretchNavigation=new C.RadioButton{Content=stretchCaption,Style=(W.Style)Window.FindResource("NavItem"),GroupName="StudioNavigation"};W.Automation.AutomationProperties.SetName(stretchNavigation,"True Stretch");stretchNavigation.Checked+=(_,_)=>{if(!activating)Activate("True Stretch");};customizationItems.Children.Add(stretchNavigation);
  var crosshairCaption=new C.StackPanel{Orientation=C.Orientation.Horizontal};crosshairCaption.Children.Add(new C.TextBlock{Text="\uE81D",FontFamily=new M.FontFamily("Segoe MDL2 Assets"),Width=32,FontSize=18,VerticalAlignment=W.VerticalAlignment.Center});crosshairCaption.Children.Add(new C.TextBlock{Text="Crosshair Studio",VerticalAlignment=W.VerticalAlignment.Center});
  crosshairNavigation=new C.RadioButton{Content=crosshairCaption,Style=(W.Style)Window.FindResource("NavItem"),GroupName="StudioNavigation"};W.Automation.AutomationProperties.SetName(crosshairNavigation,"Crosshair Studio");crosshairNavigation.Checked+=(_,_)=>{if(!activating)Activate("Crosshair Studio");};customizationItems.Children.Add(crosshairNavigation);
  var themesCaption=new C.StackPanel{Orientation=C.Orientation.Horizontal};themesCaption.Children.Add(new C.TextBlock{Text="\uE790",FontFamily=new M.FontFamily("Segoe MDL2 Assets"),Width=32,FontSize=18,VerticalAlignment=W.VerticalAlignment.Center});themesCaption.Children.Add(new C.TextBlock{Text="Themes",VerticalAlignment=W.VerticalAlignment.Center});
  themesNavigation=new C.RadioButton{Content=themesCaption,Style=(W.Style)Window.FindResource("NavItem"),GroupName="StudioNavigation"};W.Automation.AutomationProperties.SetName(themesNavigation,"Themes");themesNavigation.Checked+=(_,_)=>{if(!activating)Activate("Themes");};customizationItems.Children.Add(themesNavigation);
  var audioItems=new C.StackPanel{Margin=new W.Thickness(4,2,0,8)};
  foreach(var item in Voice.NavigationItems){Detach(item);item.GroupName="StudioNavigation";audioItems.Children.Add(item);}
  foreach(var item in ((C.StackPanel)moreTools.Content).Children.OfType<C.RadioButton>())item.GroupName="StudioNavigation";
  performanceGroup=Group("PC performance","\uE7FC",performanceItems);
  voiceGroup=Group("Audio","\uE720",audioItems);
  customizationGroup=Group("Customization","\uE790",customizationItems);
  moreTools.Header="More tools";moreTools.Template=(C.ControlTemplate)Window.FindResource("CyberGroup");moreTools.Foreground=CyberSidebar.Brush("#F0EBDC");moreTools.FontFamily=new M.FontFamily("Consolas");moreTools.Margin=new W.Thickness(0,0,0,9);
  var homeCaption=new C.StackPanel{Orientation=C.Orientation.Horizontal};homeCaption.Children.Add(new C.TextBlock{Text="\uE80F",FontFamily=new M.FontFamily("Segoe MDL2 Assets"),Width=32,FontSize=18});homeCaption.Children.Add(new C.TextBlock{Text="Home"});homeNavigation=new C.RadioButton{Content=homeCaption,Style=(W.Style)Window.FindResource("NavItem"),GroupName="StudioNavigation"};W.Automation.AutomationProperties.SetName(homeNavigation,"Home");homeNavigation.Click+=(_,_)=>Performance.SelectPage("Overview");gameLibraryNavigation=Performance.NavigationItem("Game library");homeNavigation.Content=CyberSidebar.Caption("\uE80F","Home");gameLibraryNavigation.Content=CyberSidebar.Caption("\uE7FC","Game Library");homeNavigation.Style=gameLibraryNavigation.Style=(W.Style)Window.FindResource("CyberNav");var navigation=new C.StackPanel();navigation.Children.Add(homeNavigation);navigation.Children.Add(gameLibraryNavigation);navigation.Children.Add(performanceGroup);navigation.Children.Add(voiceGroup);
  navigation.Children.Add(customizationGroup);
  navigation.Children.Add(CyberSidebar.Divider());navigation.Children.Add(moreTools);
  var layout=new C.DockPanel();
  var brand=new C.StackPanel{Margin=new W.Thickness(10,4,0,12)};
  brand.Children.Add(new CyberFrame{Width=80,Height=80,HorizontalAlignment=W.HorizontalAlignment.Left,Child=new C.Image{Source=BrandImage("yaiopc-logo.png"),Margin=new W.Thickness(7),Stretch=M.Stretch.Uniform},Margin=new W.Thickness(0,0,0,11)});
  brand.Children.Add(new C.TextBlock{Text="YAIOpc",FontFamily=new M.FontFamily("Consolas"),FontSize=29,FontWeight=W.FontWeights.Bold,Foreground=CyberSidebar.Brush("#FFD347")});
  brand.Children.Add(new C.TextBlock{Text="Your All-in-One PC",FontFamily=new M.FontFamily("Consolas"),FontSize=12,Foreground=CyberSidebar.Brush("#36C2CB"),Margin=new W.Thickness(0,7,0,10)});brand.Children.Add(CyberSidebar.Divider());C.DockPanel.SetDock(brand,C.Dock.Top);layout.Children.Add(brand);
  activeLabel.Foreground=CyberSidebar.Brush("#4BB6B5");activeLabel.FontFamily=new M.FontFamily("Consolas");activeLabel.FontSize=11;
  var footer=new CyberFrame{Child=new C.Border{Child=activeLabel,Padding=new W.Thickness(12)},Margin=new W.Thickness(0,12,0,0)};C.DockPanel.SetDock(footer,C.Dock.Bottom);layout.Children.Add(footer);  var startup=new C.CheckBox{Content="Start with Windows",Style=(W.Style)Window.FindResource("CyberStartup"),Margin=new W.Thickness(10,0,0,12),IsEnabled=testRoot==null};
  if(testRoot==null){try{startup.IsChecked=WindowsStartup.Enabled;}catch(Exception error){startup.ToolTip=error.Message;}startup.Click+=(_,_)=>{try{WindowsStartup.SetEnabled(startup.IsChecked==true);}catch(Exception error){W.MessageBox.Show(Window,error.Message,"Windows startup");}};}
  C.DockPanel.SetDock(startup,C.Dock.Bottom);layout.Children.Add(startup);
  layout.Children.Add(new C.ScrollViewer{Content=navigation,VerticalScrollBarVisibility=C.ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=C.ScrollBarVisibility.Disabled});
  sidebar=new C.Border{Child=layout,Background=CyberSidebar.Background(),BorderBrush=CyberSidebar.Brush("#286266"),BorderThickness=new W.Thickness(0,0,1,0),Padding=new W.Thickness(12,14,12,18)};composition.Children.Add(sidebar);
  Window.Content=composition;Window.Icon=BrandImage("yaiopc.ico");Window.Title="YAIOpc";Window.Width=1480;Window.Height=1020;Window.MinWidth=1060;Window.MinHeight=700;
  if(testRoot==null)WindowFit.Attach(Window);
  Performance.Activated+=()=>Activate(Performance.SelectedPage=="Overview"?"Home":Performance.SelectedPage=="Game library"?"Game Library":"FramePilot");Voice.Activated+=()=>Activate("Velvet Audio");
  performanceGroup.Expanded+=(_,_)=>{if(!activating){if(performancePage=="FPS Counter")Activate("FPS Counter");else Performance.SelectPage(performancePage);}};
  voiceGroup.Expanded+=(_,_)=>{if(!activating)Voice.SelectPage(Voice.SelectedPage);};
  customizationGroup.Expanded+=(_,_)=>{if(!activating)Activate(customizationPage);};
  Window.Loaded+=async (_,_)=>{try{if(testRoot==null)Performance.StartLiveMonitoring();await Voice.Initialize();Performance.SelectPage("Overview");Activate("Home");}catch(Exception error){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"combined-initialize-error.txt"),error.ToString());if(testRoot!=null){Environment.ExitCode=1;Window.Close();}else W.MessageBox.Show(Window,error.Message,"Velvet Audio could not initialize");}};
  Activate("Home");
 }
 static void Detach(W.FrameworkElement item){if(item.Parent is C.Panel parent)parent.Children.Remove(item);else throw new InvalidOperationException("Navigation item has an unexpected parent.");}
 C.Expander Group(string name,string glyph,W.UIElement content){
  var group=new C.Expander{Header=CyberSidebar.Caption(glyph,name),Content=content,Template=(C.ControlTemplate)Window.FindResource("CyberGroup"),Foreground=CyberSidebar.Brush("#F0EBDC"),Margin=new W.Thickness(0,0,0,9),Tag=false};W.Automation.AutomationProperties.SetName(group,name+" navigation");return group;
 } internal void Activate(string name){
  if(activating)return;if(name=="FramePilot"&&Performance.SelectedPage=="Overview")name="Home";else if(name=="FramePilot"&&Performance.SelectedPage=="Game library")name="Game Library";activating=true;
  try{
   Active=name;bool home=name=="Home";homeNavigation.IsChecked=home;bool library=name=="Game Library";gameLibraryNavigation.IsChecked=library;bool performance=name=="FramePilot",trueStretch=name=="True Stretch",crosshairActive=name=="Crosshair Studio",audio=name=="Velvet Audio";
   bool themeActive=name=="Themes";
   if(!audio){foreach(var item in Voice.NavigationItems)item.IsChecked=false;}if(!performance&&!home&&!library)Performance.ClearNavigationSelection();fps.Visibility=name=="FPS Counter"?W.Visibility.Visible:W.Visibility.Collapsed;fpsNavigation.IsChecked=name=="FPS Counter";
   stretch.Visibility=trueStretch?W.Visibility.Visible:W.Visibility.Collapsed;stretchNavigation.IsChecked=trueStretch;
   crosshair.Visibility=crosshairActive?W.Visibility.Visible:W.Visibility.Collapsed;crosshairNavigation.IsChecked=crosshairActive;
   themes.Visibility=themeActive?W.Visibility.Visible:W.Visibility.Collapsed;themesNavigation.IsChecked=themeActive;
   performanceRoot.Visibility=performance||home||library?W.Visibility.Visible:W.Visibility.Collapsed;voiceRoot.Visibility=audio?W.Visibility.Visible:W.Visibility.Collapsed;
   performanceGroup.Foreground=CyberSidebar.Brush(performance||name=="FPS Counter"?"#FFD347":"#F0EBDC");voiceGroup.Foreground=CyberSidebar.Brush(audio?"#FFD347":"#F0EBDC");customizationGroup.Foreground=CyberSidebar.Brush(trueStretch||crosshairActive||themeActive?"#FFD347":"#F0EBDC");
   performanceGroup.Tag=performance||name=="FPS Counter";voiceGroup.Tag=audio;customizationGroup.Tag=trueStretch||crosshairActive||themeActive;moreTools.Tag=performance&&moreTools.IsExpanded;
   foreach(var tab in new[]{homeNavigation,gameLibraryNavigation})if(tab.Content is C.StackPanel caption&&caption.Children[0] is C.TextBlock icon)icon.Foreground=CyberSidebar.Brush(tab.IsChecked==true?"#FFD347":"#36C2CB");
   if(home||library){performanceGroup.IsExpanded=voiceGroup.IsExpanded=customizationGroup.IsExpanded=moreTools.IsExpanded=false;}else if(performance){performancePage=Performance.SelectedPage??"PC monitor";if(Performance.SelectedPage==null||Performance.PrimaryNavigation.Any(item=>(string)item.Tag==Performance.SelectedPage))performanceGroup.IsExpanded=true;else moreTools.IsExpanded=true;Performance.NavigationItem(Performance.SelectedPage??"Overview").IsChecked=true;}
   else if(name=="FPS Counter"){performancePage="FPS Counter";performanceGroup.IsExpanded=true;}else if(trueStretch||crosshairActive||themeActive){customizationPage=name;customizationGroup.IsExpanded=true;}else{voiceGroup.IsExpanded=true;Voice.NavigationItem(Voice.SelectedPage).IsChecked=true;}
   activeLabel.Text="Viewing "+(home?"Home":library?"Game Library":name=="FPS Counter"?"FPS Counter":themeActive?"Themes":crosshairActive?"Crosshair Studio":trueStretch?"True Stretch":performance?"PC performance":"Audio")+"\nLocal audio · starts when you choose";Performance.SetWorkspaceActive(performance||home||library);
  }finally{activating=false;}
 }
 internal void Snapshot(string path){Window.UpdateLayout();var bitmap=new Imaging.RenderTargetBitmap((int)Window.ActualWidth,(int)Window.ActualHeight,96,96,M.PixelFormats.Pbgra32);bitmap.Render(Window);var encoder=new Imaging.PngBitmapEncoder();encoder.Frames.Add(Imaging.BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
 internal void VerifyLibraryNavigation(string folder){
  Directory.CreateDirectory(folder);var log=new List<string>();
  void Check(bool ok,string text){if(!ok)throw new Exception(text);log.Add("PASS "+text);}
  try{
   var nav=(C.StackPanel)homeNavigation.Parent;
   Check(nav.Children[0]==homeNavigation&&nav.Children[1]==gameLibraryNavigation&&nav.Children[2]==performanceGroup,"sidebar order is Home, Game Library, PC performance");
   Check(Active=="Home"&&homeNavigation.IsChecked==true,"Home remains startup dashboard");
   gameLibraryNavigation.IsChecked=true;gameLibraryNavigation.RaiseEvent(new W.RoutedEventArgs(C.Primitives.ButtonBase.ClickEvent));Window.UpdateLayout();
   Check(Active=="Game Library"&&gameLibraryNavigation.IsChecked==true&&homeNavigation.IsChecked!=true&&performanceRoot.IsVisible,"sidebar click opens Game Library with its own selection");
   Check(!performanceGroup.IsExpanded&&!voiceGroup.IsExpanded,"Game Library opens without expanding other apps");
   foreach(double width in new[]{1060d,1280d,1600d}){Window.Width=width;Window.Height=900;Window.UpdateLayout();Check(gameLibraryNavigation.IsVisible&&performanceRoot.ActualWidth>700,"Game Library layout at "+width);Snapshot(Path.Combine(folder,"game-library-"+width+".png"));}
   homeNavigation.RaiseEvent(new W.RoutedEventArgs(C.Primitives.ButtonBase.ClickEvent));Window.UpdateLayout();Check(Active=="Home"&&gameLibraryNavigation.IsChecked!=true,"Home returns to dashboard and clears library selection");Snapshot(Path.Combine(folder,"home.png"));
   performanceGroup.IsExpanded=true;Window.UpdateLayout();Check(Active=="FramePilot"&&gameLibraryNavigation.IsChecked!=true,"PC performance opens its retained page independently");
   Performance.SelectPage("Game library");Check(Active=="Game Library"&&gameLibraryNavigation.IsChecked==true,"dashboard game shortcuts route to independent Game Library");   var startupPreview=((C.DockPanel)sidebar.Child).Children.OfType<C.CheckBox>().Single();startupPreview.IsChecked=true;
   Performance.SelectPage("PC monitor");foreach(double width in new[]{1060d,1280d,1600d}){Window.Width=width;Window.Height=1000;Window.UpdateLayout();Snapshot(Path.Combine(folder,"pc-monitor-"+width+".png"));}startupPreview.IsChecked=false;
   File.WriteAllLines(Path.Combine(folder,"navigation-tests.txt"),log);
  }catch(Exception ex){Environment.ExitCode=1;log.Add("FAIL "+ex);File.WriteAllLines(Path.Combine(folder,"navigation-tests.txt"),log);}finally{W.Application.Current.Shutdown(Environment.ExitCode);}
 }
 internal async Task Verify(string folder){
  Directory.CreateDirectory(folder);string previousError=Path.Combine(folder,"combined-error.txt");if(File.Exists(previousError))File.Delete(previousError);var log=new List<string>();
  void Check(bool valid,string message){if(!valid)throw new Exception(message);log.Add("PASS "+message);}
  try{
   Check(Active=="Home"&&homeNavigation.IsChecked==true&&!performanceGroup.IsExpanded&&!voiceGroup.IsExpanded&&!customizationGroup.IsExpanded&&!moreTools.IsExpanded,"Home is startup page and every chevron starts collapsed");Check(!((C.StackPanel)performanceGroup.Content).Children.OfType<C.RadioButton>().Any(x=>(string)x.Tag is "Overview" or "Game library"),"Home and Game Library are removed from PC performance");Snapshot(Path.Combine(folder,"startup-home.png"));crosshair.VerifyShortcut(Window);Check(true,"global shortcut applies a spin from hidden page without stealing focus");Performance.SelectPage("Game library");Window.UpdateLayout();Check(Active=="Game Library"&&gameLibraryNavigation.IsChecked==true&&homeNavigation.IsChecked!=true&&!performanceGroup.IsExpanded,"Game Library has its own sidebar route");Snapshot(Path.Combine(folder,"home-game-library.png"));Performance.SelectPage("Overview");
   await crosshair.Verify(folder);Activate("Crosshair Studio");Window.UpdateLayout();Check(crosshair.IsVisible&&crosshairNavigation.IsChecked==true&&customizationGroup.IsExpanded,"Crosshair Studio route, randomizer, persistent presets, PNG export and native click-through overlay");
   Check(crosshairNavigation.Parent==stretchNavigation.Parent&&crosshairNavigation.Parent==customizationGroup.Content,"True Stretch and Crosshair Studio belong to Customization");customizationGroup.IsExpanded=false;Window.UpdateLayout();Check(!crosshairNavigation.IsVisible&&!stretchNavigation.IsVisible,"Customization chevron collapses both pages");customizationGroup.IsExpanded=true;Window.UpdateLayout();Check(Active=="Crosshair Studio"&&crosshairNavigation.IsVisible,"Customization restores its last selected page");
   foreach(double width in new[]{1060d,1280d,1600d}){Window.Width=width;Window.UpdateLayout();Check(crosshair.ActualWidth>700&&crosshair.IsVisible,"Crosshair Studio layout at "+width);Snapshot(Path.Combine(folder,"crosshair-"+width+".png"));crosshair.ShowGameLibrary();Window.UpdateLayout();Snapshot(Path.Combine(folder,"crosshair-games-"+width+".png"));crosshair.ShowGameLibrary("Valorant");Window.UpdateLayout();Snapshot(Path.Combine(folder,"crosshair-valorant-"+width+".png"));}
   stretch.Verify();Activate("True Stretch");Window.UpdateLayout();Check(stretch.IsVisible&&stretchNavigation.IsChecked==true,"True Stretch route, presets, original capture and game exit restore");Snapshot(Path.Combine(folder,"true-stretch.png"));
   foreach(double width in new[]{1060d,1280d,1600d}){Window.Width=width;Window.UpdateLayout();Check(stretch.ActualWidth>700&&stretch.IsVisible&&sidebar.IsVisible,"True Stretch layout at "+width);Snapshot(Path.Combine(folder,"true-stretch-"+width+".png"));}
   await Task.Delay(600);Performance.SelectPage("Overview");Window.Width=1920;Window.Height=1000;
   Activate("Themes");Window.UpdateLayout();Check(themes.IsVisible&&themesNavigation.Parent==customizationGroup.Content,"Themes route belongs to Customization");
   var themeAccent=VelvetVoice.UiControls.Accent;
   foreach(var theme in ThemePalette.Themes){themes.Select(theme.Id);Window.UpdateLayout();Check(ThemePalette.Current.Id==theme.Id&&themeService.SavedId==theme.Id,"theme applied and saved: "+theme.Name);Check(((M.SolidColorBrush)Window.Background).Color==theme.Color("Bg")&&((M.SolidColorBrush)themeAccent).Color==theme.Color("Accent"),"live window and existing code controls themed: "+theme.Name);if(theme.Id is "amethyst" or "ocean"){Window.Width=1060;Snapshot(Path.Combine(folder,"themes-"+theme.Id+".png"));Window.Width=1920;}}
   Check(themeService.VerifyRestart(),"saved theme restored through startup loading");themes.Select("copper");Activate("FramePilot");Window.UpdateLayout();
   Check(W.Application.Current.Windows.Cast<W.Window>().Count(x=>x.IsVisible)==1,"one visible application window");
   Check(Window.Title=="YAIOpc","shared window uses YAIOpc branding");
   Check(composition.ColumnDefinitions.Count==2&&sidebar.IsVisible,"one shared sidebar beside the workspace");
   Performance.SelectPage("Overview");Check(Active=="Home"&&!performanceGroup.IsExpanded&&!voiceGroup.IsExpanded&&!customizationGroup.IsExpanded&&!moreTools.IsExpanded,"returning Home collapses all navigation groups");
   performanceGroup.IsExpanded=false;Window.UpdateLayout();Check(!Performance.NavigationItem("PC monitor").IsVisible&&sidebar.IsVisible,"FramePilot collapses inside the shared sidebar");
   voiceGroup.IsExpanded=true;Window.UpdateLayout();Check(Active=="Velvet Audio"&&Voice.NavigationItem("Studio").IsVisible,"expanding Velvet Audio opens its retained page");
   voiceGroup.IsExpanded=false;Window.UpdateLayout();Check(!Voice.NavigationItem("Studio").IsVisible,"Velvet Audio collapses independently");
   performanceGroup.IsExpanded=true;Window.UpdateLayout();Check(Active=="FramePilot"&&Performance.NavigationItem("PC monitor").IsVisible,"expanding FramePilot opens its retained page");
   Check(Performance.NavigationItem("PC monitor").IsChecked==true&&Voice.NavigationItem("Studio").IsChecked!=true,"shared navigation marks only one active page");
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
   foreach(string page in new[]{"Overview","PC monitor","Disk explorer","Game library","Optimizations","Windows tuning","Game profiles","Session mode","Background apps","Startup apps","Drivers","Storage & apps","Maintenance","Diagnostics","Compare runs","Recovery","Updates & notes","About & sources","Hardware"}){Performance.SelectPage(page);await Task.Delay(100);Check(Active==(page=="Overview"?"Home":page=="Game library"?"Game Library":"FramePilot")&&performanceRoot.Visibility==W.Visibility.Visible&&sidebar.IsVisible,"workspace route: "+page);}
   Performance.SelectPage("Game profiles");Snapshot(Path.Combine(folder,"combined-profiles.png"));Performance.SelectPage("Overview");Snapshot(Path.Combine(folder,"combined-framepilot.png"));
   var synthetic=Voice.Model.UiSyntheticMixer();var output=new float[960];for(int warm=0;warm<10;warm++)synthetic.Read(output,0,output.Length);
   for(int i=0;i<20;i++){Activate("Velvet Audio");Activate("FramePilot");synthetic.Read(output,0,output.Length);Check(ReferenceEquals(synthetic,Voice.Model.UiMixer)&&output.All(x=>float.IsFinite(x)&&Math.Abs(x-.1f)<.0001f),"synthetic audio continues through workspace switch "+(i+1));}
   Voice.Model.UiClearSynthetic();
   Activate("Velvet Audio");Check(slider.Value==original+1&&Voice.SelectedPage=="Studio","voice settings and selected page survive repeated switches");slider.Value=original;
   Activate("FramePilot");var voiceNav=Voice.NavigationItem("Studio");voiceNav.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=W.UIElement.PreviewMouseLeftButtonDownEvent});Check(Active=="Home","rails stay stable during mouse down");voiceNav.RaiseEvent(new W.RoutedEventArgs(C.Primitives.ButtonBase.ClickEvent));Check(Active=="Velvet Audio","clicking the already selected voice page activates its workspace");var performanceNav=homeNavigation;performanceNav.RaiseEvent(new W.RoutedEventArgs(C.Primitives.ButtonBase.ClickEvent));Check(Active=="Home","clicking Home activates its dashboard");
   Check(!Voice.Model.UiLive,"microphone processing remains off until requested");
   foreach(int width in new[]{1060,1280,1600}){Window.Width=width;Window.Height=800;foreach(string app in new[]{"FramePilot","Velvet Audio"}){Activate(app);Window.UpdateLayout();Check(sidebar.ActualWidth==276&&sidebar.IsVisible&&composition.ColumnDefinitions[1].ActualWidth>700,"one sidebar and usable workspace at "+width+" in "+app);Snapshot(Path.Combine(folder,"combined-"+app.Replace(" ","")+"-"+width+".png"));}Voice.SelectPage("Headphone EQ");Snapshot(Path.Combine(folder,"combined-headphone-eq-"+width+".png"));}
   log.AddRange(VelvetVoice.WpfDialogs.Verify());
   File.WriteAllLines(Path.Combine(folder,"combined-tests.txt"),log);
  }catch(Exception error){Environment.ExitCode=1;File.WriteAllText(Path.Combine(folder,"combined-error.txt"),error.ToString());}
  finally{Window.Close();}
 }
}
