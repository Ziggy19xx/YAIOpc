using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace FramePilot {
 static class WpfProgram {
  [STAThread] static int Main(string[] args) {
   if(args.Length==2&&args[0]=="--artwork-test"){try{File.WriteAllLines(args[1],ArtworkTests.Run().GetAwaiter().GetResult());return 0;}catch(Exception e){File.WriteAllText(args[1],"FAIL: "+e);return 1;}}
   if(args.Length==2&&args[0]=="--tools-smoke"){
    Forms.Application.EnableVisualStyles();Forms.Application.SetCompatibleTextRenderingDefault(false);
    var testApp=new Application();var testShell=new WpfShell(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"FramePilot-tools-test-"+Guid.NewGuid().ToString("N")));testShell.RunToolsSmoke(args[1]);testApp.Run(testShell.Window);return testShell.ExitCode;
   }
   if(args.Length==2&&args[0]=="--driver-updates")return DriverOffers.Worker(args[1]);
   if(args.Length>0&&args[0]=="--apply-update")return Releases.Swap(args);
   if(args.Length==2&&args[0]=="--monitor-test")return MonitorTests.Run(args[1]);
   if(args.Length==2&&args[0]=="--steam-test")return Tests.RunSteam(args[1]);
   if(args.Contains("--self-test"))return Tests.Run(args.Last());
   if(args.Contains("--test-worker")){System.Threading.Thread.Sleep(30000);return 0;}
   bool own;
   using(var mutex=new System.Threading.Mutex(true,"Local\\FramePilot-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,out own)) {
    if(!own){MessageBox.Show("FramePilot is already running. Close the other copy before opening this one.","FramePilot");return 1;}
    Forms.Application.EnableVisualStyles();Forms.Application.SetCompatibleTextRenderingDefault(false);
    try {
     var app=new Application();
     var shell=new WpfShell();
     if(args.Length==2&&args[0]=="--ui-smoke")shell.RunSmoke(args[1]);
     app.Run(shell.Window);return shell.ExitCode;
    }catch(Exception e){MessageBox.Show(e.ToString(),"FramePilot WPF could not start");return 1;}
   }
  }
 }
 sealed class WpfShell {
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  public readonly Window Window;
  public int ExitCode;
  internal event Action Activated;
  bool workspaceActive=true;
  internal RadioButton NavigationItem(string name){return navigation[name];}
  internal string SelectedPage {get{return page;}}
  internal bool MoreToolsExpanded {get{return Get<Expander>("MoreTools").IsExpanded;}set{Get<Expander>("MoreTools").IsExpanded=value;}}
  internal Expander MoreToolsMenu=>Get<Expander>("MoreTools");
  internal IEnumerable<RadioButton> PrimaryNavigation=>PrimaryNav.Select(item=>navigation[item[0]]);
  internal IEnumerable<RadioButton> NavigationItems=>navigation.Values;
  internal void ClearNavigationSelection(){foreach(var item in navigation.Values)item.IsChecked=false;UpdateNavGlyphs();}
  internal void SelectPage(string name){Navigate(name);}
  internal void StartLiveMonitoring(){features.WpfMonitoring=true;Sync();}
  internal void SetWorkspaceActive(bool active){workspaceActive=active;features.WpfPause(!active||Window.WindowState==WindowState.Minimized);monitorPage.Model.SetActive(active&&page=="PC monitor"&&Window.WindowState!=WindowState.Minimized);}
  readonly MainForm features;
  readonly MonitorPage monitorPage;readonly DiskExplorerPage diskExplorer;readonly GameLibraryPage gameLibrary;
  readonly WindowsFormsHost host;
  readonly ToolPages toolPages;
  readonly Dictionary<string,RadioButton> navigation=new Dictionary<string,RadioButton>();
  readonly Brush ink=Brush("#F3ECE4"),muted=Brush("#A39789"),dim=Brush("#6F6358"),accent=Brush("#E8964A"),accentHot=Brush("#F2A55A"),panel=Brush("#1A1613"),lineBrush=Brush("#2B231D"),good=Brush("#2FCB8D"),info=Brush("#4C8DF6"),violet=Brush("#9B6BF2"),warn=Brush("#F0A93B"),bad=Brush("#F0605A");
  readonly Dictionary<string,TextBlock> navGlyphs=new Dictionary<string,TextBlock>();readonly List<double> diskHistory=new List<double>();
  DateTime lastDisk=DateTime.MinValue;string gamesKey,activityKey;
  readonly CancellationTokenSource shutdown=new CancellationTokenSource();
  string page;bool pending,closing,ready,rendering;
  readonly bool testing;
  static readonly string[] PageNames={"Overview","PC monitor","Disk explorer","Optimizations","Game library","Game profiles","Session mode","Background apps","Startup apps","Drivers","Storage & apps","Maintenance","Diagnostics","Compare runs","Recovery","Updates & notes","About & sources","Hardware","Windows tuning"};
  public WpfShell(string dataDirectory=null) {
   testing=dataDirectory!=null;
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))Window=(Window)XamlReader.Load(stream);
   features=new MainForm(dataDirectory);features.WpfPrepare();
   monitorPage=new MonitorPage(()=>new FramePilot.Monitoring.PythonMonitorService(),FramePilot.Monitoring.MonitorSettings.Load());monitorPage.Visibility=Visibility.Collapsed;Get<Grid>("ContentGrid").Children.Add(monitorPage);diskExplorer=new DiskExplorerPage{Visibility=Visibility.Collapsed};Get<Grid>("ContentGrid").Children.Add(diskExplorer);gameLibrary=new GameLibraryPage(features){Visibility=Visibility.Collapsed};Get<Grid>("ContentGrid").Children.Add(gameLibrary);
   host=Get<WindowsFormsHost>("LegacyHost");
   var container=new Forms.Panel{Dock=Forms.DockStyle.Fill,BackColor=System.Drawing.Color.FromArgb(14,11,10)};
   container.Controls.Add(features);host.Child=container;
   toolPages=new ToolPages(features){Visibility=Visibility.Collapsed};Get<Grid>("ContentGrid").Children.Add(toolPages);
   BuildNavigation();
   try{using(var art=Assembly.GetExecutingAssembly().GetManifestResourceStream("hero.jpg")){if(art!=null){var bmp=new System.Windows.Media.Imaging.BitmapImage();bmp.BeginInit();bmp.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bmp.StreamSource=art;bmp.EndInit();bmp.Freeze();Get<Border>("HeroCard").Background=new ImageBrush(bmp){Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Right,AlignmentY=AlignmentY.Center};}}}catch{}

   Click("Browse",()=>Navigate("Optimizations"));Click("QaMonitor",()=>Navigate("PC monitor"));Click("QaDiagnostics",()=>Navigate("Diagnostics"));Click("HelpButton",()=>Navigate("About & sources"));Click("MinButton",()=>Window.WindowState=WindowState.Minimized);Click("MaxButton",()=>Window.WindowState=Window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized);Click("CloseButton",()=>Window.Close());Click("Games",()=>Navigate("Game library"));Click("Recover",()=>Navigate("Recovery"));Click("Hardware",()=>Navigate("Hardware"));Click("WindowsControls",()=>Navigate("Windows tuning"));
   Get<Button>("Snapshot").Click+=async delegate{await features.WpfSnapshot();};
   Get<CheckBox>("Monitoring").Checked+=delegate{features.WpfMonitoring=true;};Get<CheckBox>("Monitoring").Unchecked+=delegate{features.WpfMonitoring=false;};
   var category=Get<ComboBox>("Category");category.ItemsSource=new[]{"All categories","Windows","Power","Display","Input","Storage","Network","Drivers","Games","Not included"};category.SelectedIndex=0;
   category.SelectionChanged+=delegate{RenderCatalog();};Get<TextBox>("CatalogSearch").TextChanged+=delegate{RenderCatalog();};
   Click("RefreshCatalog",RenderCatalog);Click("ApplySelected",delegate{features.WpfApplySelection();RenderCatalog();});
   Get<WrapPanel>("Cards").SizeChanged+=delegate{ResizeCards();};
   Get<Canvas>("ResourcePlot").SizeChanged+=delegate{DrawPlot();};foreach(string spark in new[]{"CpuSpark","GpuSpark","RamSpark","DiskSpark"})Get<Canvas>(spark).SizeChanged+=delegate{DrawSparks();};
   features.WpfChanged+=QueueSync;monitorPage.Model.PropertyChanged+=delegate{QueueSync();};
   features.FormClosed+=delegate{if(!closing)Window.Dispatcher.BeginInvoke(new Action(()=>Window.Close()));};
   Window.SourceInitialized+=delegate{
    try{int on=1;DwmSetWindowAttribute(new WindowInteropHelper(Window).Handle,20,ref on,4);
     if(features.Icon!=null)Window.Icon=Imaging.CreateBitmapSourceFromHIcon(features.Icon.Handle,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
    }catch{};
   };
   Window.Loaded+=delegate{features.Show();ready=true;Navigate(features.WpfPage);Sync();ScanInstalledGames();};
   Window.Closed+=delegate{shutdown.Cancel();shutdown.Dispose();};
   Window.StateChanged+=delegate{UpdateChrome();features.WpfPause(!workspaceActive||Window.WindowState==WindowState.Minimized);monitorPage.Model.SetActive(workspaceActive&&page=="PC monitor"&&Window.WindowState!=WindowState.Minimized);};
   Window.Closing+=async delegate(object sender,System.ComponentModel.CancelEventArgs e){
    if(closing)return;
    closing=true;
    if(!features.IsDisposed){features.Close();if(!features.IsDisposed){e.Cancel=true;closing=false;Sync();return;}}
    e.Cancel=true;try{await monitorPage.Model.StopAsync();await diskExplorer.StopAsync();}finally{QueueClose();}
   };
   Window.Closed+=delegate{monitorPage.Dispose();diskExplorer.Dispose();gameLibrary.Dispose();toolPages.Dispose();features.WpfChanged-=QueueSync;host.Dispose();};
  }
  void QueueClose(){Window.Dispatcher.BeginInvoke(new Action(()=>Window.Close()));}
  T Get<T>(string name) where T:class{return (T)Window.FindName(name);}
  static Brush Brush(string value){var b=(SolidColorBrush)new BrushConverter().ConvertFromString(value);b.Freeze();return b;}
  void Click(string name,Action action){Get<Button>(name).Click+=delegate{Guard(action);};}
  void Guard(Action action){try{action();}catch(Exception e){if(testing)throw;MessageBox.Show(Window,e.GetBaseException().Message,"FramePilot",MessageBoxButton.OK,MessageBoxImage.Information);}}
  void QueueSync(){if(pending||closing||!ready)return;pending=true;Window.Dispatcher.BeginInvoke(new Action(delegate{pending=false;if(!closing&&!features.IsDisposed)Sync();}));}
  void Navigate(string name){if(!ready)return;if(Activated!=null)Activated();Guard(delegate{if(features.WpfPage!=name)features.WpfNavigate(name);Display(name);Sync();});}
  void Display(string name){
   if(!PrimaryNav.Any(item=>item[0]==name))MoreToolsExpanded=true;
   bool changed=page!=name;page=name;diskExplorer.Visibility=name=="Disk explorer"?Visibility.Visible:Visibility.Collapsed;monitorPage.Visibility=name=="PC monitor"?Visibility.Visible:Visibility.Collapsed;monitorPage.Model.SetActive(workspaceActive&&name=="PC monitor"&&Window.WindowState!=WindowState.Minimized);
   bool tools=name!="Overview"&&name!="Optimizations"&&name!="PC monitor"&&name!="Disk explorer"&&name!="Game library";
   host.Visibility=Visibility.Collapsed;
   toolPages.Visibility=tools?Visibility.Visible:Visibility.Collapsed;if(tools)toolPages.ShowPage(name);
   gameLibrary.Visibility=name=="Game library"?Visibility.Visible:Visibility.Collapsed;
   Get<ScrollViewer>("Overview").Visibility=name=="Overview"?Visibility.Visible:Visibility.Collapsed;
   Get<Grid>("Optimizations").Visibility=name=="Optimizations"?Visibility.Visible:Visibility.Collapsed;
   RadioButton selected;if(navigation.TryGetValue(name,out selected))selected.IsChecked=true;UpdateNavGlyphs();
   if(changed&&name=="Optimizations")RenderCatalog();
  }
  // ===== Navigation =====
  static readonly string[][] PrimaryNav={new[]{"Overview","Home","\uE80F"},new[]{"PC monitor","PC Monitor","\uE7F4"},new[]{"Disk explorer","Disk Explorer","\uEDA2"},new[]{"Game library","Game Library","\uE7FC"},new[]{"Optimizations","Optimizations","\uE945"},new[]{"Windows tuning","Windows Tuning","\uE713"}};
  void BuildNavigation(){
   var primary=Get<StackPanel>("Navigation");var more=Get<StackPanel>("NavigationMore");var used=new HashSet<string>();
   foreach(string[] item in PrimaryNav){
    used.Add(item[0]);
    var glyph=new TextBlock{Text=item[2],FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=19,Width=32,Foreground=muted,VerticalAlignment=VerticalAlignment.Center};
    var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(glyph);row.Children.Add(new TextBlock{Text=item[1],VerticalAlignment=VerticalAlignment.Center});
    navGlyphs[item[0]]=glyph;primary.Children.Add(MakeNav(item[0],item[1],row,"NavItem"));
   }
   foreach(string name in PageNames)if(!used.Contains(name))more.Children.Add(MakeNav(name,name,new TextBlock{Text=name},"NavSub"));
  }
  RadioButton MakeNav(string name,string label,object content,string style){
   var radio=new RadioButton{Content=content,Tag=name,GroupName="FramePilotNavigation",Style=(Style)Window.FindResource(style)};
   AutomationProperties.SetName(radio,label);
   radio.Checked+=delegate(object sender,RoutedEventArgs e){if(ready)Navigate((string)((RadioButton)sender).Tag);};
   radio.Click+=delegate{if(Activated!=null)Activated();};

   navigation[name]=radio;return radio;
  }
  void UpdateNavGlyphs(){foreach(var pair in navGlyphs){RadioButton r;pair.Value.Foreground=navigation.TryGetValue(pair.Key,out r)&&r.IsChecked==true?accentHot:muted;}}
  void UpdateChrome(){
   bool max=Window.WindowState==WindowState.Maximized;var t=SystemParameters.WindowResizeBorderThickness;
   Get<Grid>("Root").Margin=max?new Thickness(t.Left,t.Top,t.Right,t.Bottom):new Thickness(0);
   var button=Get<Button>("MaxButton");button.Content=max?"\uE923":"\uE922";button.ToolTip=max?"Restore":"Maximize";
  }
  // ===== Home dashboard =====
  static double Parse(string text){double v;return double.TryParse((text??"").Trim().TrimEnd('%').Trim(),NumberStyles.Float,CultureInfo.CurrentCulture,out v)?v:double.NaN;}
  static double DiskPercent(){try{var d=new DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory));if(!d.IsReady||d.TotalSize<=0)return double.NaN;return 100.0*(d.TotalSize-d.TotalFreeSpace)/d.TotalSize;}catch{return double.NaN;}}
  static string Greeting(){int h=DateTime.Now.Hour;return h<12?"GOOD MORNING,":h<18?"GOOD AFTERNOON,":"GOOD EVENING,";}
  void SetStat(string id,double value,double warnAt,double highAt,string low,string mid,string top,string missing){
   var text=Get<TextBlock>(id+"Value");var dot=Get<Ellipse>(id+"Dot");var state=Get<TextBlock>(id+"State");
   if(double.IsNaN(value)){text.Text="--";dot.Fill=dim;state.Text=missing;return;}
   text.Text=Math.Round(value).ToString("0",CultureInfo.CurrentCulture)+"%";
   if(value>=highAt){dot.Fill=bad;state.Text=top;}else if(value>=warnAt){dot.Fill=warn;state.Text=mid;}else{dot.Fill=good;state.Text=low;}
  }
  void UpdateHealth(double cpu,double ram,double gpu,bool live){
   bool measured=!double.IsNaN(cpu)||!double.IsNaN(ram);
   bool busy=(!double.IsNaN(cpu)&&cpu>=85)||(!double.IsNaN(ram)&&ram>=88)||(!double.IsNaN(gpu)&&gpu>=85);
   string glyph,sideTitle,sideText,title,body;Brush tone,badge;
   if(!measured){tone=dim;badge=dim;glyph="\uE946";sideTitle="Not measuring yet";sideText="Turn on live monitoring on Home.";title="Let's take a look";body="Turn on live monitoring or take a snapshot to see how your PC is doing.";}
   else if(busy){tone=warn;badge=warn;glyph="\uE7BA";sideTitle="Your PC is busy";sideText="Something is using a lot right now.";title="Your PC is busy";body="CPU, graphics or memory use is high right now. Open PC Monitor to see more.";}
   else{tone=good;badge=accent;glyph="\uE73E";sideTitle="All systems look good!";sideText=live?"Your PC is running smoothly.":"Based on your last snapshot.";title="You're all set!";body="Your PC is running great and ready for your next adventure.";}
   Get<Border>("SideStatusDot").Background=tone;Get<TextBlock>("SideStatusGlyph").Text=glyph;Get<TextBlock>("SideStatusTitle").Text=sideTitle;Get<TextBlock>("SideStatusText").Text=sideText;
   Get<Border>("AllSetBadge").Background=badge;Get<TextBlock>("AllSetGlyph").Text=glyph;Get<TextBlock>("AllSetTitle").Text=title;Get<TextBlock>("AllSetText").Text=body;
  }
  void Sync(){
   if(page!=features.WpfPage)Display(features.WpfPage);
   string text=page=="PC monitor"?monitorPage.Model.Status:page=="Disk explorer"?"Disk explorer: choose a folder to inspect. File management opens in Explorer.":features.WpfStatus;
   var statusBlock=Get<TextBlock>("Status");statusBlock.Text=text;statusBlock.ToolTip=text;
   var metrics=features.WpfMetrics;double cpu=Parse(metrics[0]),ram=Parse(metrics[1]),gpu=Parse(metrics[2]),disk=DiskPercent();
   SetStat("Cpu",cpu,60,85,"Normal","Busy","High","Not measuring");SetStat("Ram",ram,70,88,"Good","Busy","High","Not measuring");
   SetStat("Gpu",gpu,60,85,"Normal","Busy","High","Not available");SetStat("Disk",disk,80,92,"Good","Getting full","Almost full","Unavailable");
   Get<TextBlock>("GpuDetail").Text=metrics[3];Get<TextBlock>("Counts").Text=metrics[4];
   bool live=features.WpfMonitoring;var monitoring=Get<CheckBox>("Monitoring");if(monitoring.IsChecked!=live)monitoring.IsChecked=live;
   UpdateHealth(cpu,ram,gpu,live);
   if(page=="Overview"){
    Get<TextBlock>("Greeting").Text=Greeting();
    if((DateTime.Now-lastDisk).TotalSeconds>=2&&!double.IsNaN(disk)){lastDisk=DateTime.Now;diskHistory.Add(disk);if(diskHistory.Count>60)diskHistory.RemoveAt(0);}
    RenderGames();RenderActivity();DrawPlot();
   }
  }
  void DrawSparks(){
   var samples=features.WpfSamples;var cpu=new List<double>();var gpu=new List<double>();var ram=new List<double>();
   for(int i=Math.Max(0,samples.Length-60);i<samples.Length;i++){if(!double.IsNaN(samples[i].Cpu))cpu.Add(samples[i].Cpu);if(!double.IsNaN(samples[i].Gpu))gpu.Add(samples[i].Gpu);if(!double.IsNaN(samples[i].Ram))ram.Add(samples[i].Ram);}
   Spark("CpuSpark",cpu,good);Spark("GpuSpark",gpu,info);Spark("RamSpark",ram,violet);Spark("DiskSpark",diskHistory,warn);
  }
  void Spark(string name,IList<double> values,Brush color){
   var canvas=Get<Canvas>(name);if(canvas==null)return;canvas.Children.Clear();
   if(canvas.ActualWidth<1||canvas.ActualHeight<6||values.Count<2)return;
   double w=canvas.ActualWidth,h=canvas.ActualHeight-6,lo=values.Min(),hi=values.Max();
   if(hi-lo<8){double mid=(hi+lo)/2;lo=mid-4;hi=mid+4;}
   var points=new PointCollection();for(int i=0;i<values.Count;i++)points.Add(new Point(i*w/(values.Count-1),3+h-(values[i]-lo)/(hi-lo)*h));
   var fill=new PointCollection(points);fill.Add(new Point(w,canvas.ActualHeight));fill.Add(new Point(0,canvas.ActualHeight));
   var c=((SolidColorBrush)color).Color;
   canvas.Children.Add(new Polygon{Points=fill,Fill=new LinearGradientBrush(Color.FromArgb(70,c.R,c.G,c.B),Color.FromArgb(0,c.R,c.G,c.B),90)});
   canvas.Children.Add(new Polyline{Points=points,Stroke=color,StrokeThickness=2,StrokeLineJoin=PenLineJoin.Round});
  }
  static string Ago(double seconds){if(seconds<1)return "";return seconds>=90?Math.Round(seconds/60)+" min ago":Math.Round(seconds)+" sec ago";}
  static double Metric(ResourceSample sample,int metric){return metric==0?sample.Cpu:metric==1?sample.Gpu:sample.Ram;}
  void DrawPlot(){
   DrawSparks();
   var plot=Get<Canvas>("ResourcePlot");if(plot==null||plot.ActualWidth<1)return;plot.Children.Clear();
   double width=plot.ActualWidth,height=plot.Height;
   for(int i=0;i<=4;i++)plot.Children.Add(new Line{X1=0,X2=width,Y1=i*height/4,Y2=i*height/4,Stroke=lineBrush,StrokeThickness=1});
   var samples=features.WpfSamples;
   Get<TextBlock>("PlotEmpty").Visibility=samples.Length<2?Visibility.Visible:Visibility.Collapsed;
   if(samples.Length<2){Get<TextBlock>("TimeStart").Text="";Get<TextBlock>("TimeMid").Text="";Get<TextBlock>("TimeEnd").Text="";return;}
   Brush[] colors={good,info,violet};
   for(int metric=0;metric<3;metric++){
    Polyline line=null;
    for(int i=0;i<samples.Length;i++){
     double value=Metric(samples[i],metric);
     if(double.IsNaN(value)){if(metric!=1)line=null;continue;}  // GPU is sampled less often, so bridge its gaps
     if(line==null){line=new Polyline{Stroke=colors[metric],StrokeThickness=2,StrokeLineJoin=PenLineJoin.Round};plot.Children.Add(line);}
     line.Points.Add(new Point(i*width/Math.Max(1,samples.Length-1),height-Math.Max(0,Math.Min(100,value))*height/100));
    }
   }
   double span=(samples[samples.Length-1].Time-samples[0].Time).TotalSeconds;
   Get<TextBlock>("TimeStart").Text=Ago(span);Get<TextBlock>("TimeMid").Text=Ago(span/2);Get<TextBlock>("TimeEnd").Text="Now";
  }
  static readonly string[][] Hues={new[]{"#3B6FD8","#1B2F63"},new[]{"#D8733B","#63301B"},new[]{"#2FA37A","#144A38"},new[]{"#8B5CF6","#3A2570"},new[]{"#D8B03B","#63501B"}};
  void RenderGames(){
   FoundGame[] games;try{games=features.WpfGames;}catch{games=new FoundGame[0];}
   var shown=games.Take(4).ToArray();string key=string.Join("|",shown.Select(g=>g.Name+"/"+g.Source+"/"+g.AppId).ToArray());
   if(key==gamesKey)return;gamesKey=key;
   var tiles=Get<WrapPanel>("GameTiles");tiles.Children.Clear();
   Get<TextBlock>("GamesEmpty").Visibility=shown.Length==0?Visibility.Visible:Visibility.Collapsed;
   foreach(var game in shown)tiles.Children.Add(GameTile(game));
   var add=new Button{Style=(Style)Window.FindResource("GameTile"),Width=112,Height=118,Margin=new Thickness(0,0,12,12),ToolTip="Open the Game Library to find or add games"};
   var addBody=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
   addBody.Children.Add(new TextBlock{Text="+",FontSize=30,Foreground=muted,HorizontalAlignment=HorizontalAlignment.Center});addBody.Children.Add(new TextBlock{Text="Add Game",FontSize=12,Foreground=muted,HorizontalAlignment=HorizontalAlignment.Center});
   add.Content=addBody;AutomationProperties.SetName(add,"Add game");add.Click+=delegate{Navigate("Game library");};tiles.Children.Add(add);
  }
  Button GameTile(FoundGame game){
   int hash=0;foreach(char ch in game.Name??"?")hash=(hash*31+ch)&0x7FFFFFFF;var hue=Hues[hash%Hues.Length];
   string initial=string.IsNullOrWhiteSpace(game.Name)?"?":game.Name.Trim().Substring(0,1).ToUpperInvariant();
   var placeholder=new TextBlock{Text=initial,FontSize=30,FontWeight=FontWeights.Bold,Foreground=Brushes.White,Opacity=0.9,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};var cover=new Image{Stretch=Stretch.UniformToFill,Visibility=Visibility.Collapsed};var artGrid=new Grid();artGrid.Children.Add(placeholder);artGrid.Children.Add(cover);
   var art=new Border{Height=72,CornerRadius=new CornerRadius(9,9,0,0),Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(hue[0]),(Color)ColorConverter.ConvertFromString(hue[1]),45),ClipToBounds=true,Child=artGrid};LoadGameCoverAsync(game,cover);
   var label=new StackPanel{Margin=new Thickness(8,7,8,8)};
   label.Children.Add(new TextBlock{Text=game.Name,FontSize=12.5,TextTrimming=TextTrimming.CharacterEllipsis});
   label.Children.Add(new TextBlock{Text=game.Source,FontSize=10.5,Foreground=muted,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,2,0,0)});
   var body=new StackPanel();body.Children.Add(art);body.Children.Add(label);
   var button=new Button{Style=(Style)Window.FindResource("GameTile"),Width=112,Margin=new Thickness(0,0,12,12),Content=body,ToolTip="Launch "+game.Name};
   AutomationProperties.SetName(button,"Launch "+game.Name);button.Click+=delegate{Guard(delegate{features.WpfLaunchGame(game);});};return button;
  }
  async void LoadGameCoverAsync(FoundGame game,Image target){try{var cached=ArtworkImages.Peek(game);if(cached!=null){target.Source=cached;target.Visibility=Visibility.Visible;return;}var bitmap=await ArtworkImages.GetAsync(game,shutdown.Token);if(bitmap==null||shutdown.IsCancellationRequested)return;target.Source=bitmap;target.Visibility=Visibility.Visible;}catch{}}
  async void ScanInstalledGames(){var empty=Get<TextBlock>("GamesEmpty");empty.Text="Scanning installed game libraries…";try{var result=await Task.Run(()=>GameDiscovery.Scan(shutdown.Token));if(shutdown.IsCancellationRequested)return;features.WpfSetGames(result.Games);empty.Text=result.Games.Count==0?"No installed games found. Open Game Library to review scan details.":"";}catch(OperationCanceledException){}catch{if(!shutdown.IsCancellationRequested)empty.Text="Could not scan installed games. Open Game Library to retry.";}}
  static string When(string created){DateTime t;if(!DateTime.TryParse(created,CultureInfo.InvariantCulture,DateTimeStyles.None,out t))return "";var d=DateTime.Now-t;if(d.TotalMinutes<1)return "Just now";if(d.TotalMinutes<60)return (int)d.TotalMinutes+" min ago";if(d.TotalHours<24)return (int)d.TotalHours+" hours ago";if(d.TotalDays<2)return "Yesterday";return (int)d.TotalDays+" days ago";}
  void RenderActivity(){
   string[][] items;try{items=features.WpfActivity;}catch{items=new string[0][];}
   string key=string.Join("|",items.Select(i=>string.Join("~",i)).ToArray());if(key==activityKey)return;activityKey=key;
   var list=Get<StackPanel>("ActivityList");list.Children.Clear();
   if(items.Length==0){list.Children.Add(new TextBlock{Text="Nothing yet. Changes you apply will show up here, with a way to undo them.",FontSize=13,Foreground=muted,TextWrapping=TextWrapping.Wrap});return;}
   foreach(string[] item in items){
    bool restored=item[1]=="Restored";
    var icon=new Border{Width=26,Height=26,CornerRadius=new CornerRadius(13),Background=restored?info:good,Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Top,Child=new TextBlock{Text=restored?"\uE777":"\uE73E",FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=12,Foreground=Brush("#0D0B0A"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
   var texts=new StackPanel();Grid.SetColumn(texts,1);
    texts.Children.Add(new TextBlock{Text=(restored?"Restored: ":item[1]=="Applied"?"Changed: ":item[1]+": ")+item[0],FontSize=13,TextWrapping=TextWrapping.Wrap});
    texts.Children.Add(new TextBlock{Text=When(item[2]),FontSize=11.5,Foreground=muted,Margin=new Thickness(0,2,0,0)});
    var row=new Grid{Margin=new Thickness(0,0,0,12)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
    row.Children.Add(icon);row.Children.Add(texts);list.Children.Add(row);
   }
  }
  TextBlock Text(string value,Brush color,double size){return new TextBlock{Text=value,Foreground=color,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};}
  Button ActionButton(string label,Action action){var b=new Button{Content=label};b.Click+=delegate{Guard(action);};return b;}
  Border Badge(string value){
   Brush tone=value.IndexOf("High",StringComparison.OrdinalIgnoreCase)>=0?Brush("#F08A4B"):value.IndexOf("Medium",StringComparison.OrdinalIgnoreCase)>=0?warn:info;var c=((SolidColorBrush)tone).Color;
   return new Border{Background=new SolidColorBrush(Color.FromArgb(40,c.R,c.G,c.B)),BorderBrush=new SolidColorBrush(Color.FromArgb(110,c.R,c.G,c.B)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),Padding=new Thickness(9,4,9,4),Margin=new Thickness(0,0,8,8),Child=new TextBlock{Text=value.ToUpperInvariant(),Foreground=tone,FontWeight=FontWeights.SemiBold,FontSize=11}};
  }
  void RenderCatalog(){
   if(rendering)return;rendering=true;
   try{
    var cards=Get<WrapPanel>("Cards");cards.Children.Clear();
    string term=Get<TextBox>("CatalogSearch").Text,category=Convert.ToString(Get<ComboBox>("Category").SelectedItem);
    var matching=features.WpfTweaks.Where(t=>(category=="All categories"||category==t.Category)&&((t.Name+" "+t.Description).IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
    foreach(var item in matching){
     var tweak=item;var stack=new StackPanel();
     stack.Children.Add(Text(tweak.Category.ToUpperInvariant()+" / "+tweak.Mode.ToUpperInvariant(),accent,11));
     var title=Text(tweak.Name,ink,19);title.FontWeight=FontWeights.SemiBold;stack.Children.Add(title);
     stack.Children.Add(Text(tweak.Description,muted,14));stack.Children.Add(Text(tweak.Read(),accent,13));
     var actions=new WrapPanel();
     if(tweak.Direct){
      var select=new CheckBox{Content="Select",IsChecked=features.WpfSelected(tweak.Id)};
      select.Checked+=delegate{features.WpfSelect(tweak.Id,true);UpdateSelection();};select.Unchecked+=delegate{features.WpfSelect(tweak.Id,false);UpdateSelection();};stack.Children.Add(select);
      actions.Children.Add(ActionButton("Apply",delegate{features.WpfCatalogAction(tweak.Id,"Apply");RenderCatalog();}));
      actions.Children.Add(ActionButton("Undo",delegate{features.WpfCatalogAction(tweak.Id,"Undo");RenderCatalog();}));
     }else if(tweak.Mode=="Guided")actions.Children.Add(ActionButton("Open settings / guide",delegate{features.WpfCatalogAction(tweak.Id,"Open settings / guide");}));
     stack.Children.Add(actions);
     var badges=new WrapPanel();
     if(tweak.Mode=="Excluded"){badges.Children.Add(Badge("Not offered"));badges.Children.Add(Badge("Protected"));}
     else foreach(string impact in MainForm.WpfImpact(tweak.Name).Split('|').Take(2))badges.Children.Add(Badge(impact.Trim()));
     stack.Children.Add(badges);stack.Children.Add(Text(tweak.Mode=="Excluded"?"Excluded to preserve stability and security.":"GAIN / LOSS: NOT MEASURED",ink,12));
     cards.Children.Add(new Border{Style=(Style)Window.FindResource("Card"),Margin=new Thickness(0,0,12,12),Child=stack});
    }
    Get<TextBlock>("CatalogCount").Text=matching.Length+" controls shown. Selections stay selected when you filter.";ResizeCards();UpdateSelection();
   }finally{rendering=false;}
  }
  void UpdateSelection(){int count=features.WpfTweaks.Count(t=>t.Direct&&features.WpfSelected(t.Id));Get<Button>("ApplySelected").Content="Apply selected direct controls ("+count+")";Get<Button>("ApplySelected").IsEnabled=count>0;}
  void ResizeCards(){var cards=Get<WrapPanel>("Cards");double available=cards.ActualWidth;if(available<1)return;int columns=available>=760?2:1;foreach(Border card in cards.Children)card.Width=Math.Max(100,available/columns-12);}
  public void RunSmoke(string report){
   Window.Loaded+=async delegate{
    var results=new List<string>();
    try{
     for(int i=0;features.WpfBusy&&i<120;i++)await Task.Delay(250);
     if(features.WpfBusy)throw new Exception("Hardware startup did not finish");
     if(!new HashSet<string>(features.WpfPages).SetEquals(PageNames))throw new Exception("Missing page route");
     foreach(string name in PageNames){Navigate(name);await Task.Delay(70);if(features.WpfPage!=name||navigation[name].IsChecked!=true)throw new Exception("Navigation: "+name);results.Add("PASS: route "+name);}
     Navigate("Overview");await features.WpfSnapshot();Sync();
     if(features.WpfSamples.Length==0)throw new Exception("No real samples");results.Add("PASS: real resource sample and WPF metrics");
     features.WpfNavigate("Hardware");await Task.Delay(100);if(page!="Hardware")throw new Exception("Internal navigation synchronization");results.Add("PASS: legacy navigation synchronizes WPF shell");
     Navigate("Optimizations");Get<TextBox>("CatalogSearch").Text="Game Mode";if(Get<WrapPanel>("Cards").Children.Count!=1)throw new Exception("Search results");results.Add("PASS: native catalog search");
     features.WpfSelect("game-mode",true);Get<TextBox>("CatalogSearch").Text="";RenderCatalog();if(!features.WpfSelected("game-mode"))throw new Exception("Lost selection");features.WpfSelect("game-mode",false);RenderCatalog();results.Add("PASS: selection survives filtering without applying changes");
     Get<ComboBox>("Category").SelectedItem="Not included";if(Get<WrapPanel>("Cards").Children.Count==0)throw new Exception("Category results");Get<ComboBox>("Category").SelectedIndex=0;results.Add("PASS: category filter");
     Window.Width=1050;Window.Height=700;await Task.Delay(150);results.Add("PASS: minimum-size layout rendered");
     Navigate("Overview");features.WpfMonitoring=true;await Task.Delay(2400);
     Navigate("Optimizations");for(int i=0;features.WpfSampling&&i<80;i++)await Task.Delay(100);
     if(features.WpfSampling)throw new Exception("Sampler did not settle");int sampleCount=features.WpfSamples.Length;
     await Task.Delay(2200);if(features.WpfSamples.Length!=sampleCount)throw new Exception("Monitoring continued off Overview");results.Add("PASS: monitoring pauses off Overview");
     Navigate("Overview");Window.WindowState=WindowState.Minimized;for(int i=0;features.WpfSampling&&i<80;i++)await Task.Delay(100);
     sampleCount=features.WpfSamples.Length;await Task.Delay(2200);
     if(features.WpfTimerRunning||features.WpfSamples.Length!=sampleCount)throw new Exception("Monitoring continued while minimized");
     Window.WindowState=WindowState.Normal;if(!features.WpfTimerRunning)throw new Exception("Monitoring timer did not resume");features.WpfMonitoring=false;results.Add("PASS: monitoring pauses minimized and timer resumes restored");
     if(Assembly.GetExecutingAssembly().GetManifestResourceNames().Count(n=>n.StartsWith("starter-"))!=5)throw new Exception("Missing presets");results.Add("PASS: five embedded starter profiles");
     Navigate("PC monitor");monitorPage.Model.Settings.SensorPath="";monitorPage.Model.Settings.IntervalSeconds=1;
     int beats=0;var heartbeat=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};heartbeat.Tick+=delegate{beats++;};heartbeat.Start();
     monitorPage.Model.SetEnabled(true);for(int i=0;monitorPage.Model.History.Count<2&&i<600;i++)await Task.Delay(100);
     heartbeat.Stop();if(monitorPage.Model.History.Count<2)throw new Exception("Native monitor failed: "+monitorPage.Model.Status);if(beats<10)throw new Exception("UI did not remain responsive");results.Add("PASS: native Python monitoring updates while WPF remains responsive");
     if(host.Visibility!=Visibility.Collapsed)throw new Exception("Legacy window covers monitor");results.Add("PASS: monitoring uses native WPF view");
     Navigate("Optimizations");for(int i=0;monitorPage.Model.IsRunning&&i<100;i++)await Task.Delay(100);if(monitorPage.Model.IsRunning)throw new Exception("Monitor worker did not pause");int count=monitorPage.Model.History.Count;await Task.Delay(1200);if(monitorPage.Model.History.Count!=count)throw new Exception("Hidden monitor kept polling");results.Add("PASS: Python monitoring stops off page");
     Navigate("PC monitor");for(int i=0;monitorPage.Model.History.Count==count&&i<200;i++)await Task.Delay(100);if(monitorPage.Model.History.Count==count)throw new Exception("Monitor did not resume");
     Window.WindowState=WindowState.Minimized;for(int i=0;monitorPage.Model.IsRunning&&i<100;i++)await Task.Delay(100);if(monitorPage.Model.IsRunning)throw new Exception("Minimized monitor kept running");results.Add("PASS: Python monitor resumes and pauses minimized");Window.WindowState=WindowState.Normal;
     for(int i=0;(monitorPage.Model.Latest==null||!monitorPage.Model.Latest.Metrics.cpu_percent.HasValue)&&i<100;i++)await Task.Delay(100);
     await monitorPage.Model.StopAsync();if(monitorPage.Model.IsRunning)throw new Exception("Monitor stop still pending");results.Add("PASS: native monitor stops cleanly");
     await monitorPage.VerifyViews(report);results.Add("PASS: all native hardware detail views render");await Task.Delay(200);Window.UpdateLayout();
     var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Window.ActualWidth,(int)Window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(Window);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=File.Create(report+".png"))encoder.Save(output);
     Releases.Validate(Assembly.GetExecutingAssembly().Location,false);results.Add("PASS: portable release identity retained");
    }catch(Exception e){ExitCode=1;results.Add("FAIL: "+e);}
    File.WriteAllLines(report,results);Window.Close();
   };
  }
  static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);var match=child as T;if(match!=null)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}}
  void SaveView(string path){Window.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Window.ActualWidth,(int)Window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(Window);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=File.Create(path))encoder.Save(output);}
  public void RunToolsSmoke(string report){
   Window.Loaded+=async delegate{
    var results=new List<string>();
    try{
     for(int i=0;features.WpfBusy&&i<120;i++)await Task.Delay(250);
     await Task.Delay(600);var tools=PageNames.Where(x=>x!="Overview"&&x!="Optimizations"&&x!="PC monitor"&&x!="Disk explorer"&&x!="Game library").ToArray();
     foreach(double width in new[]{1320.0,1050.0}){
      Window.Width=width;Window.Height=width==1050?700:900;
      foreach(string name in tools){Navigate(name);await Task.Delay(100);Window.UpdateLayout();if(features.WpfPage!=name||toolPages.Visibility!=Visibility.Visible||host.Visibility!=Visibility.Collapsed)throw new Exception("Native tool route: "+name+" current="+features.WpfPage+" view="+toolPages.Visibility+" host="+host.Visibility+" ready="+ready);if(!Descendants<Border>(toolPages).Any(x=>x.CornerRadius.TopLeft==14))throw new Exception("Cards missing: "+name);if(!Descendants<TextBlock>(toolPages).Any(x=>x.Text==name.ToUpperInvariant()))throw new Exception("Header missing: "+name);SaveView(report+"."+name.Replace(" & ","-").Replace(" ","-")+"."+(int)width+".png");results.Add("PASS: native cards and layout "+name+" at "+width);}
     }
     Navigate("Game profiles");await Task.Delay(100);
     var source=features.WpfToolSource("Game profiles");var sourceCombos=AllForms(source).OfType<Forms.ComboBox>().ToArray();var combo=Descendants<ComboBox>(toolPages).First(x=>x.Items.Count==sourceCombos[0].Items.Count);int original=sourceCombos[0].SelectedIndex;combo.SelectedIndex=(original+1)%combo.Items.Count;if(sourceCombos[0].SelectedIndex!=combo.SelectedIndex)throw new Exception("Profile selection did not reach shared editor");sourceCombos[0].SelectedIndex=original;((ToolView)toolPages.Content).Sync();if(combo.SelectedIndex!=original)throw new Exception("Source selection did not reach WPF");results.Add("PASS: profile selection synchronizes both directions");
     var sourceText=AllForms(source).OfType<Forms.TextBox>().First(x=>x.AccessibleName=="Preset name");var text=Descendants<TextBox>(toolPages).First(x=>AutomationProperties.GetName(x)=="Preset name");string oldText=sourceText.Text;text.Text="Fixture profile";if(sourceText.Text!=text.Text)throw new Exception("Preset name binding");sourceText.Text=oldText;((ToolView)toolPages.Content).Sync();if(text.Text!=oldText)throw new Exception("Preset name reverse binding");results.Add("PASS: editable preset name synchronizes both directions");
     int clicks=0;var fixture=new Forms.Button{Enabled=true};fixture.Click+=delegate{clicks++;};features.WpfToolClick(fixture);fixture.Enabled=false;features.WpfToolClick(fixture);fixture.Dispose();if(clicks!=1)throw new Exception("Shared handler or disabled guard");results.Add("PASS: original handler invoked and disabled actions blocked");
     Navigate("Session mode");await Task.Delay(100);var check=Descendants<CheckBox>(toolPages).First(x=>x.Content.ToString().StartsWith("Temporarily disable"));var sourceCheck=AllForms(features.WpfToolSource("Session mode")).OfType<Forms.CheckBox>().First(x=>x.Text.StartsWith("Temporarily disable"));check.IsChecked=true;if(!sourceCheck.Checked)throw new Exception("Checkbox binding");check.IsChecked=false;results.Add("PASS: session preference binding without starting a session");
     Navigate("Diagnostics");Window.UpdateLayout();var toolView=(ToolView)toolPages.Content;toolView.Sync();var reportSource=AllForms(features.WpfToolSource("Diagnostics")).OfType<Forms.TextBox>().First(x=>x.Multiline);string oldReport=reportSource.Text;reportSource.Text="Fixture report changed asynchronously";toolView.Sync();if(!Descendants<TextBox>(toolPages).Any(x=>x.Text==reportSource.Text))throw new Exception("Report refresh");reportSource.Text=oldReport;results.Add("PASS: asynchronous reports refresh in native view");
     var fixturePage=features.WpfToolSource("Diagnostics");var fixtureChecks=new Forms.CheckedListBox();fixtureChecks.Items.Add("Protected fixture");fixtureChecks.Items.Add("Eligible fixture");fixtureChecks.ItemCheck+=delegate(object s,Forms.ItemCheckEventArgs e){if(e.Index==0)e.NewValue=Forms.CheckState.Unchecked;};fixturePage.Controls.Add(fixtureChecks);
     var fixtureList=new Forms.ListView{View=Forms.View.Details};fixtureList.Columns.Add("Name");fixtureList.Items.Add(new Forms.ListViewItem("Inventory fixture"));fixturePage.Controls.Add(fixtureList);var fixtureView=new ToolView(features,"Diagnostics");toolPages.Content=fixtureView;Window.UpdateLayout();fixtureView.Sync();var fixtureWpfChecks=Descendants<CheckBox>(fixtureView).ToArray();fixtureWpfChecks[0].IsChecked=true;if(fixtureChecks.GetItemChecked(0)||fixtureWpfChecks[0].IsChecked==true)throw new Exception("Protected check guard");fixtureWpfChecks[1].IsChecked=true;if(fixtureChecks.CheckedItems.Count!=1)throw new Exception("Checked item selection");results.Add("PASS: checked lists preserve source validation and selection");var fixtureWpfList=Descendants<ListView>(fixtureView).First();fixtureWpfList.SelectedIndex=0;if(fixtureList.SelectedItems.Count!=1)throw new Exception("Inventory selection binding");results.Add("PASS: inventory selection reaches original action model");fixturePage.Controls.Remove(fixtureChecks);fixturePage.Controls.Remove(fixtureList);fixtureChecks.Dispose();fixtureList.Dispose();toolPages.Content=toolView;
     Window.Width=1320;Window.Height=900;Navigate("Game library");await Task.Delay(100);SaveView(report+".Game-library.png");
     results.Add("PASS: test uses isolated preferences; no optimization, restore, cleanup or game launch action executed");
    }catch(Exception e){ExitCode=1;results.Add("FAIL: "+e);}
    File.WriteAllLines(report,results);Window.Close();
   };
  }
  static IEnumerable<Forms.Control> AllForms(Forms.Control source){foreach(Forms.Control child in source.Controls){yield return child;foreach(var nested in AllForms(child))yield return nested;}}
 }
}
