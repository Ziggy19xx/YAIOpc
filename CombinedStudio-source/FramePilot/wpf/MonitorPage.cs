using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using FramePilot.Monitoring;

namespace FramePilot {
 public sealed class MonitorViewModel:INotifyPropertyChanged,IDisposable {
  readonly Func<IMonitorService> factory;IMonitorService service;CancellationTokenSource cancellation;Task running;bool active,enabled,disposed;int failures;
  public readonly MonitorSettings Settings;public readonly Queue<Sample> History=new Queue<Sample>();
  public Sample Latest {get;private set;} public FramePilot.Monitoring.Inventory Inventory {get;private set;}public DateTime? InventoryUtc {get;private set;}
  public string Status {get;private set;} public bool Enabled {get{return enabled;}} public bool IsRunning {get{return running!=null&&!running.IsCompleted;}}
  public event PropertyChangedEventHandler PropertyChanged;
  public MonitorViewModel(Func<IMonitorService> factory,MonitorSettings settings){this.factory=factory;Settings=settings;Status="Monitoring is off. Start it when you want live readings.";}
  void Changed(){if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(""));}
  public void SetActive(bool value){active=value;Reconcile();}
  public void SetEnabled(bool value){enabled=value;if(value)failures=0;Reconcile();}
  public void Restart(){Inventory=null;Latest=null;History.Clear();if(cancellation!=null)cancellation.Cancel();Reconcile();}
  void Reconcile(){
   if(disposed)return;
   if(!enabled||!active){if(cancellation!=null)cancellation.Cancel();if(failures<=2)Status=enabled?"Paused while this page is hidden or FramePilot is minimized.":"Monitoring is off. Previous readings remain labeled with their sample time.";Changed();return;}
   if(running==null||running.IsCompleted){cancellation=new CancellationTokenSource();running=Run(cancellation.Token);}
  }
  async Task Run(CancellationToken token){
   await Task.Yield();
   try{
    while(!token.IsCancellationRequested&&enabled&&active){
     bool retry=false;
     try{
      Status="Starting hardware monitoring…";Changed();service=factory();await service.StartAsync(Settings.SensorPath,token);
      if(Inventory==null){Status="Reading hardware inventory…";Changed();Inventory=await service.InventoryAsync(token);InventoryUtc=DateTime.UtcNow;}
      while(!token.IsCancellationRequested&&enabled&&active){
       Latest=await service.PollAsync(token);History.Enqueue(Latest);while(History.Count>120)History.Dequeue();
       Status="Live · sampled "+Latest.Utc.ToLocalTime().ToString("HH:mm:ss")+" · "+Settings.IntervalSeconds+" second interval";Changed();
       await Task.Delay(Settings.IntervalSeconds*1000,token);
      }
     }catch(OperationCanceledException){}
     catch(Exception e){
      failures++;Status="Monitoring unavailable: "+e.GetBaseException().Message;
      if(failures>2){enabled=false;Status+=" Automatic retries stopped. Use Start monitoring to retry.";}
      else {retry=true;Status+=" Retrying ("+failures+"/2)…";}
      Changed();
     }
     if(service!=null){await service.StopAsync();service.Dispose();service=null;}
     if(retry)await Task.Delay(1000*failures,token);else break;
    }
   }catch(OperationCanceledException){}
   finally{if(service!=null){service.Dispose();service=null;}running=null;if(!disposed){Changed();Reconcile();}}
  }
  public async Task StopAsync(){enabled=false;Status="Monitoring is off. Previous readings remain labeled with their sample time.";Changed();if(cancellation!=null)cancellation.Cancel();var task=running;if(task!=null)await task;}
  public void Dispose(){disposed=true;enabled=false;if(cancellation!=null)cancellation.Cancel();if(service!=null)service.Dispose();}
 }
 public sealed class MonitorRow {public string Group {get;set;} public string Reading {get;set;} public string Value {get;set;} public string Unit {get;set;} public string Availability {get;set;}}
 public sealed class MonitorPage:Grid,IDisposable {
  public readonly MonitorViewModel Model;readonly TextBlock status,summary;readonly Button start;readonly ComboBox category,interval;readonly TextBox search,sensorPath;
  readonly ScrollViewer topScroll;readonly HardwarePreview preview;readonly Expander previewExpander;readonly DataGrid table;readonly Canvas plot;readonly CheckBox temperatures;bool rendering;readonly List<MonitorRow> rows=new List<MonitorRow>();
  static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(243,236,228)),Muted=new SolidColorBrush(Color.FromRgb(163,151,137)),Panel=new SolidColorBrush(Color.FromRgb(26,22,19));

  public MonitorPage(Func<IMonitorService> factory,MonitorSettings settings){
   Model=new MonitorViewModel(factory,settings);Margin=new Thickness(28,24,24,18);Background=new SolidColorBrush(Color.FromRgb(14,11,10));
   RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
   var top=new StackPanel();topScroll=new ScrollViewer{Content=top,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=430};Children.Add(topScroll);SizeChanged+=delegate{topScroll.MaxHeight=Math.Max(180,ActualHeight*0.70);};top.Children.Add(Label("Your PC, in detail.",28,Ink));
   top.Children.Add(Label("Live hardware readings and inventory from PC Hardware Monitor.",14,Muted));
   var controls=new WrapPanel();start=Button("Start monitoring",()=>Model.SetEnabled(!Model.Enabled));controls.Children.Add(start);
   controls.Children.Add(Button("Refresh inventory",()=>{Model.Restart();Model.SetEnabled(true);}));
   controls.Children.Add(Label("Poll every",14,Muted));interval=new ComboBox{Width=85,Margin=new Thickness(8,0,12,10),ItemsSource=new[]{1,2,5,10},SelectedItem=settings.IntervalSeconds};controls.Children.Add(interval);controls.Children.Add(Label("seconds",14,Muted));
   interval.SelectionChanged+=delegate{if(!rendering){settings.IntervalSeconds=(int)interval.SelectedItem;SaveSettings();}};top.Children.Add(controls);
   status=Label(Model.Status,13,Muted);top.Children.Add(status);summary=Label("No sample yet. Missing sensors are shown as Unavailable.",15,Ink);top.Children.Add(summary);
   plot=new Canvas{Height=90,ClipToBounds=true,Margin=new Thickness(0,0,0,10),Background=Panel};plot.SizeChanged+=delegate{Draw();};top.Children.Add(plot);
   top.Children.Add(Label("Recent samples (up to 120) · CPU blue / RAM mint / GPU amber · 0–100%",11,Muted));
   var options=new Expander{Header="Monitoring settings",Foreground=Ink,Margin=new Thickness(0,0,0,12)};var settingsPanel=new StackPanel();
   settingsPanel.Children.Add(Label("Optional sensors: choose a trusted LibreHardwareMonitorLib.dll. No sensor driver is installed by FramePilot. Some readings require hardware access that may be unavailable without elevation.",12,Muted));
   sensorPath=new TextBox{Text=settings.SensorPath,Margin=new Thickness(0,5,0,5)};settingsPanel.Children.Add(sensorPath);
   var settingsActions=new WrapPanel();settingsActions.Children.Add(Button("Choose sensor DLL",delegate{var d=new Microsoft.Win32.OpenFileDialog{Filter="LibreHardwareMonitor|LibreHardwareMonitorLib.dll",CheckFileExists=true};if(d.ShowDialog()==true)sensorPath.Text=d.FileName;}));
   settingsActions.Children.Add(Button("Save and reconnect",delegate{string path=sensorPath.Text.Trim();if(path.Length>0&&(!System.IO.Path.IsPathRooted(path)||!File.Exists(path)))throw new Exception("Choose an existing, trusted sensor DLL.");settings.SensorPath=path;SaveSettings();Model.Restart();}));
   settingsActions.Children.Add(Button("Disable optional sensors",delegate{sensorPath.Text="";settings.SensorPath="";SaveSettings();Model.Restart();}));settingsPanel.Children.Add(settingsActions);options.Content=settingsPanel;top.Children.Add(options);
   var filters=new WrapPanel();category=new ComboBox{Width=170,ItemsSource=new[]{"Summary","CPU","GPU","Memory","Motherboard","Volumes","Drivers","Sensors","System"},SelectedIndex=0,Margin=new Thickness(0,0,12,10)};filters.Children.Add(category);
   search=new TextBox{Width=200,Margin=new Thickness(0,0,12,10),ToolTip="Filter hardware readings"};System.Windows.Automation.AutomationProperties.SetName(search,"Filter hardware readings");filters.Children.Add(search);
   temperatures=new CheckBox{Content="Temperatures only",Margin=new Thickness(0,0,12,10)};filters.Children.Add(temperatures);filters.Children.Add(Button("Export readings",Export));top.Children.Add(filters);
   preview=new HardwarePreview();previewExpander=new Expander{Header="Interactive reference model",Content=preview,Visibility=Visibility.Collapsed,Foreground=Ink};previewExpander.Expanded+=delegate{preview.ShowModel(Convert.ToString(category.SelectedItem)=="GPU"?"gpu":"msi_b450_tomahawk_max");};top.Children.Add(previewExpander);
   table=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,EnableRowVirtualization=true,EnableColumnVirtualization=true,Background=Panel,Foreground=Ink,RowBackground=Panel,AlternatingRowBackground=new SolidColorBrush(Color.FromRgb(30,25,21)),BorderThickness=new Thickness(0),GridLinesVisibility=DataGridGridLinesVisibility.None,HeadersVisibility=DataGridHeadersVisibility.Column,MinHeight=80};
   var header=new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));header.Setters.Add(new Setter(Control.BackgroundProperty,Panel));header.Setters.Add(new Setter(Control.ForegroundProperty,Ink));header.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(9)));table.ColumnHeaderStyle=header;
   var cellText=new Style(typeof(TextBlock));cellText.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));cellText.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(5)));cellText.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Text"){RelativeSource=new RelativeSource(RelativeSourceMode.Self)}));
   foreach(string name in new[]{"Group","Reading","Value","Unit","Availability"})table.Columns.Add(new DataGridTextColumn{ElementStyle=cellText,Header=name,Binding=new Binding(name),Width=new DataGridLength(name=="Reading"||name=="Value"?2:1,DataGridLengthUnitType.Star)});
   Grid.SetRow(table,1);Children.Add(table);category.SelectionChanged+=delegate{Render();};search.TextChanged+=delegate{Filter();};temperatures.Checked+=delegate{Render();};temperatures.Unchecked+=delegate{Render();};
   Model.PropertyChanged+=OnChanged;Render();
  }
  static TextBlock Label(string value,double size,Brush color){return new TextBlock{Text=value,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};}
  Button Button(string title,Action action){var b=new Button{Content=title};b.Click+=delegate{try{action();}catch(Exception e){MessageBox.Show(Window.GetWindow(this),e.Message,"FramePilot monitoring");}};return b;}
  void SaveSettings(){try{Model.Settings.Save();}catch(Exception e){MessageBox.Show("Monitoring preference could not be saved: "+e.Message);}}
  void OnChanged(object sender,PropertyChangedEventArgs e){if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(Render));return;}Render();}
  static string Number(double? value){return value.HasValue&&!double.IsNaN(value.Value)&&!double.IsInfinity(value.Value)?value.Value.ToString("0.##"):"Unavailable";}
  void Add(string group,string name,object value,string unit){bool available=value!=null;if(value is double)available=!double.IsNaN((double)value)&&!double.IsInfinity((double)value);string text=available?(value is double?Number((double)value):Convert.ToString(value)):"Unavailable";if(string.IsNullOrWhiteSpace(text)){text="Unavailable";available=false;}rows.Add(new MonitorRow{Group=group,Reading=name,Value=text,Unit=unit,Availability=available?"Available":"Unavailable"});}
  void Bytes(string group,string name,double? value){Add(group,name,value.HasValue?(object)(value.Value/1000000000d):null,"GB (decimal)");}
  public void Render(){
   if(table==null)return;rendering=true;try{
    status.Text=Model.Status;start.Content=Model.Enabled?"Stop monitoring":"Start monitoring";rows.Clear();var m=Model.Latest==null?null:Model.Latest.Metrics;var inv=Model.Inventory;string selected=Convert.ToString(category.SelectedItem);previewExpander.Visibility=selected=="GPU"||selected=="Motherboard"?Visibility.Visible:Visibility.Collapsed;if(previewExpander.IsExpanded&&(selected=="GPU"||selected=="Motherboard"))preview.ShowModel(selected=="GPU"?"gpu":"msi_b450_tomahawk_max");
    summary.Text=m==null?"No sample yet. Missing sensors are shown as Unavailable.":"CPU "+(m.cpu_percent.HasValue?Number(m.cpu_percent)+"%":"Unavailable")+"   ·   RAM "+(m.memory_percent.HasValue?Number(m.memory_percent)+"%":"Unavailable")+"   ·   Last sample "+Model.Latest.Utc.ToLocalTime().ToString("HH:mm:ss")+(Model.InventoryUtc.HasValue?" · Inventory "+Model.InventoryUtc.Value.ToLocalTime().ToString("HH:mm:ss"):"");
    if(selected=="CPU"||selected=="Summary"){
     Add("CPU","Processor",inv==null||inv.cpu==null?null:inv.cpu.name,"");Add("CPU","Physical cores",inv==null||inv.cpu==null?null:inv.cpu.physical_cores,"");Add("CPU","Logical processors",inv==null||inv.cpu==null?null:inv.cpu.logical_cores,"");Add("CPU","Load",m==null?null:m.cpu_percent,"%");Add("CPU","Frequency",m==null?null:m.cpu_frequency_mhz,"MHz");
     if(selected=="CPU"&&m!=null&&m.per_core_percent!=null)for(int i=0;i<m.per_core_percent.Length;i++)Add("CPU","Logical processor "+i,m.per_core_percent[i],"%");
    }
    if(selected=="Memory"||selected=="Summary"){
     Bytes("Memory","Total",m==null?null:(double?)m.memory_total_bytes);Bytes("Memory","Used",m==null?null:(double?)m.memory_used_bytes);Bytes("Memory","Available",m==null?null:(double?)m.memory_available_bytes);Add("Memory","Load",m==null?null:m.memory_percent,"%");
     if(selected=="Memory"&&inv!=null&&inv.memory!=null)foreach(var d in inv.memory.modules??new Dimm[0]){string g=d.device_locator??"DIMM";Add(g,"Manufacturer",d.manufacturer,"");Add(g,"Part",d.part_number,"");Add(g,"Serial",d.serial_number,"");Bytes(g,"Capacity",d.capacity_bytes);Add(g,"Configured speed",d.configured_speed_mhz,"MHz (SMBIOS)");Add(g,"Rated speed",d.speed_mhz,"MHz (SMBIOS)");}
    }
    if(selected=="GPU"||selected=="Summary"){
     if(m==null||m.gpu_adapters==null||m.gpu_adapters.Length==0)Add("GPU","Live telemetry",null,"");
     else foreach(var g in m.gpu_adapters){string n=g.name??"GPU";Add(n,"Load",g.utilization_percent,"%");Bytes(n,"VRAM used",g.vram_used_bytes);Bytes(n,"VRAM total",g.vram_total_bytes);Add(n,"Temperature",g.temperature_c,"°C");Add(n,"Graphics clock",g.clock_mhz,"MHz");Add(n,"Power",g.power_watts,"W");Add(n,"PCI bus",g.pci_bus_id,"");Add(n,"Driver",g.driver_version,"");}
     if(selected=="GPU"&&inv!=null)foreach(var g in inv.gpu_adapters??new Gpu[0]){Add("Adapter",g.name??"Name",g.pnp_id,"");Add(g.name??"Adapter","Driver version",g.driver_version,"");Add(g.name??"Adapter","Driver date",g.driver_date,"");}
    }
    if(selected=="Motherboard"||selected=="Summary"){
     var b=inv==null?null:inv.motherboard;Add("Motherboard","Manufacturer",b==null?null:b.manufacturer,"");Add("Motherboard","Product",b==null?null:b.product,"");Add("BIOS","Vendor",b==null?null:b.bios_vendor,"");Add("BIOS","Version",b==null?null:b.bios_version,"");Add("BIOS","Date",b==null?null:b.bios_date,"");
    }
    if(selected=="Volumes"||selected=="Summary"){
     if(m!=null)foreach(var v in m.volumes??new Volume[0]){Bytes(v.name,"Used",v.used_bytes);Bytes(v.name,"Free",v.free_bytes);Bytes(v.name,"Total",v.total_bytes);Add(v.name,"Used",v.percent,"%");}
     if(selected=="Volumes"&&inv!=null)foreach(var d in inv.storage_devices??new Disk[0]){Bytes(d.model??"Physical disk","Capacity",d.size_bytes);Add(d.model??"Physical disk","Interface",d.@interface,"");Add(d.model??"Physical disk","Reported status (not a SMART health test)",d.status,"");}
    }
    if(selected=="Drivers"&&inv!=null)foreach(var d in inv.drivers??new Driver[0]){Add(d.name??"Device","Version",d.driver_version,"");Add(d.name??"Device","Class / manufacturer",d.device_class+" / "+d.manufacturer,"");Add(d.name??"Device","Date",d.driver_date,"");Add(d.name??"Device","Problem code (0 = none reported)",d.problem_code,"");Add(d.name??"Device","Hardware IDs",string.Join("; ",d.hardware_ids??new string[0]),"");}
    if(selected=="Sensors"||selected=="CPU"){
     var sensors=m==null?new Sensor[0]:(m.sensors??new Sensor[0]);foreach(var s in sensors.Where(s=>(selected!="CPU"||(s.hardware_type??"").IndexOf("cpu",StringComparison.OrdinalIgnoreCase)>=0)&&(temperatures.IsChecked!=true||s.sensor_type=="Temperature")))Add(s.hardware??"Sensor",s.name,s.value,s.unit);
     if(sensors.Length==0)Add("Sensors","Supported hardware readings",null,"");
    }
    if(selected=="System"||selected=="Summary"){
     Add("System","Computer",inv==null?null:inv.hostname,"");Add("System","Windows",inv==null?null:inv.os_version,"");Add("System","Uptime",m==null?null:m.uptime_seconds,"seconds");Add("Network","Sent (cumulative)",m==null?null:m.network_sent_bytes,"bytes");Add("Network","Received (cumulative)",m==null?null:m.network_received_bytes,"bytes");
    }
    if(m!=null&&m.errors!=null)foreach(var error in m.errors){Add("Provider status",error.Key,error.Value,"");rows.Last().Availability="Unavailable";}if(inv!=null&&inv.provider_errors!=null)foreach(var error in inv.provider_errors){Add("Inventory status",error.Key,error.Value,"");rows.Last().Availability="Unavailable";}
    Filter();Draw();
   }finally{rendering=false;}
  }
  internal async Task VerifyViews(string report){
   foreach(string name in new[]{"CPU","GPU","Memory","Motherboard","Volumes","Drivers","Sensors","System"}){category.SelectedItem=name;Render();if(rows.Count==0)throw new Exception("Empty native monitoring view: "+name);}
   category.SelectedItem="GPU";previewExpander.IsExpanded=true;for(int i=0;!preview.Ready&&i<300;i++)await Task.Delay(100);if(!preview.Ready)throw new Exception("Reference preview did not load: "+preview.PreviewStatus);topScroll.ScrollToBottom();await Task.Delay(150);Window.GetWindow(this).UpdateLayout();
   var window=Window.GetWindow(this);var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var file=File.Create(report+"-model.png"))encoder.Save(file);
   previewExpander.IsExpanded=false;topScroll.ScrollToTop();category.SelectedItem="CPU";await Task.Delay(100);window.UpdateLayout();
  }
  void Filter(){if(table==null)return;string term=search.Text;table.ItemsSource=rows.Where(r=>(r.Group+" "+r.Reading+" "+r.Value).IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0).ToArray();}
  void Draw(){if(plot==null||plot.ActualWidth<1)return;plot.Children.Clear();var samples=Model.History.ToArray();for(int k=0;k<3;k++){Polyline line=null;for(int i=0;i<samples.Length;i++){var m=samples[i].Metrics;double? value=k==0?m.cpu_percent:k==1?m.memory_percent:m.gpu_adapters==null||m.gpu_adapters.Length==0?null:m.gpu_adapters[0].utilization_percent;if(!value.HasValue){line=null;continue;}if(line==null){line=new Polyline{Stroke=k==0?Brushes.CornflowerBlue:k==1?Brushes.MediumAquamarine:Brushes.Orange,StrokeThickness=2};plot.Children.Add(line);}line.Points.Add(new Point(i*plot.ActualWidth/Math.Max(1,samples.Length-1),plot.Height-Math.Max(0,Math.Min(100,value.Value))*plot.Height/100));}}}
  void Export(){var d=new Microsoft.Win32.SaveFileDialog{Filter="CSV readings|*.csv",FileName="framepilot-monitor.csv"};if(d.ShowDialog()!=true)return;var lines=new List<string>{"Sample UTC,Group,Reading,Value,Unit,Availability"};foreach(var r in rows)lines.Add(string.Join(",",new[]{Model.Latest==null?"":Model.Latest.Utc.ToString("o"),r.Group,r.Reading,r.Value,r.Unit,r.Availability}.Select(v=>"\""+(v??"").Replace("\"","\"\"")+"\"")));File.WriteAllLines(d.FileName,lines,System.Text.Encoding.UTF8);}
  public void Dispose(){Model.PropertyChanged-=OnChanged;Model.Dispose();}
 }
}










