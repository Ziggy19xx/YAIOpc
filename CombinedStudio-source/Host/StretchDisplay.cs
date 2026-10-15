using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Management;
namespace CombinedStudio;

internal readonly record struct StretchResolution(int Width,int Height) {
 public override string ToString()=>$"{Width}x{Height}";
 internal static StretchResolution Parse(string input){
  var match=Regex.Match(input??"",@"^\s*(\d{3,5})\s*[xX×]\s*(\d{3,5})\s*$");
  if(!match.Success||!int.TryParse(match.Groups[1].Value,out int width)||!int.TryParse(match.Groups[2].Value,out int height)||width<320||height<320||width>16384||height>16384)throw new ArgumentException("Enter a resolution such as 1440x1080 (320–16384 pixels per side).");
  return new(width,height);
 }
}
internal sealed class StretchState {
 public string Device {get;set;}="";
 public StretchResolution? Original {get;set;}
 public uint Frequency {get;set;}
 public uint Bits {get;set;}
 public uint? OriginalScaling {get;set;}
 public bool FillMonitor {get;set;}=true;
 public bool AutoRestore {get;set;}=true;
 public List<string> Presets {get;set;}=new();
}
internal interface IStretchDisplay {
 (string Device,StretchResolution Resolution,uint Frequency,uint Bits) Current();
 void Apply(string device,StretchResolution resolution,uint frequency,uint bits);
 uint? ReadScaling(string device)=>null;void SetScaling(string device,uint scaling){}
}
internal sealed class WindowsStretchDisplay:IStretchDisplay {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct DisplayDevice {
  public int Size;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;
  public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;
 }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct Mode {
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string DeviceName;
  public ushort SpecVersion,DriverVersion,Size,DriverExtra;public uint Fields;
  public int X,Y;public uint Orientation,FixedOutput;
  public short Color,Duplex,YResolution,TTOption,Collate;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string FormName;
  public ushort LogPixels;public uint Bits,Width,Height,Flags,Frequency,ICMMethod,ICMIntent,MediaType,DitherType,Reserved1,Reserved2,PanningWidth,PanningHeight;
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool EnumDisplayDevices(string device,uint index,ref DisplayDevice info,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool EnumDisplaySettings(string device,int mode,ref Mode settings);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int ChangeDisplaySettingsEx(string device,ref Mode settings,IntPtr window,uint flags,IntPtr parameter);
 public uint? ReadScaling(string device)=>CrosshairDisplay.Scaling(device);public void SetScaling(string device,uint scaling)=>CrosshairDisplay.Scaling(device,scaling);
 static Mode Read(string device,int index){var mode=new Mode{Size=(ushort)Marshal.SizeOf<Mode>()};if(!EnumDisplaySettings(device,index,ref mode))throw new InvalidOperationException("Windows could not read this display mode.");return mode;}
 public (string Device,StretchResolution Resolution,uint Frequency,uint Bits) Current(){
  for(uint i=0;;i++){var info=new DisplayDevice{Size=Marshal.SizeOf<DisplayDevice>()};if(!EnumDisplayDevices(null,i,ref info,0))break;if((info.Flags&5)==5){var mode=Read(info.Name,-1);return(info.Name,new((int)mode.Width,(int)mode.Height),mode.Frequency,mode.Bits);}}
  throw new InvalidOperationException("Windows did not report an active primary display.");
 }
 public void Apply(string device,StretchResolution resolution,uint frequency,uint bits){
  var available=new List<Mode>();
  for(int i=0;;i++){var candidate=new Mode{Size=(ushort)Marshal.SizeOf<Mode>()};if(!EnumDisplaySettings(device,i,ref candidate))break;if(candidate.Width==resolution.Width&&candidate.Height==resolution.Height)available.Add(candidate);}
  if(available.Count==0)throw new InvalidOperationException("This resolution is not registered with your GPU. Add it as a custom resolution in your graphics control panel first.");
  var mode=available.OrderBy(x=>x.Frequency==frequency?0:1).ThenBy(x=>x.Bits==bits?0:1).ThenByDescending(x=>x.Frequency).First();
  mode.Fields=0x40000|0x80000|0x100000|0x400000;
  int result=ChangeDisplaySettingsEx(device,ref mode,IntPtr.Zero,2,IntPtr.Zero);
  if(result!=0)throw new InvalidOperationException($"Windows rejected the display mode (code {result}). Check your GPU custom resolution and scaling settings.");
  result=ChangeDisplaySettingsEx(device,ref mode,IntPtr.Zero,0,IntPtr.Zero);
  if(result!=0)throw new InvalidOperationException($"Windows could not switch resolution (code {result}).");
 }
}
internal sealed class StretchController {
 readonly IStretchDisplay display;readonly string path;bool sawGame;
 internal StretchState State {get;private set;}
 internal StretchController(IStretchDisplay display,string path){this.display=display;this.path=path;State=File.Exists(path)?JsonSerializer.Deserialize<StretchState>(File.ReadAllText(path))??new():new();State.Presets??=new();}
 internal void Save(){Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(State));File.Move(temp,path,true);}
 internal (string Device,StretchResolution Resolution,uint Frequency,uint Bits) Current()=>display.Current();
 internal void Apply(StretchResolution resolution){var current=Current();if(current.Resolution==resolution&&(!State.FillMonitor||display.ReadScaling(current.Device) is not uint same||same==3))return;
  if(State.Original.HasValue&&State.Device!=current.Device)throw new InvalidOperationException("The primary display changed. Restore the saved display before starting another stretch session.");
  bool captured=!State.Original.HasValue;
  if(captured){State.Device=current.Device;State.Original=current.Resolution;State.Frequency=current.Frequency;State.Bits=current.Bits;State.OriginalScaling=display.ReadScaling(current.Device);Save();}
  try{display.Apply(current.Device,resolution,current.Frequency,current.Bits);if(State.FillMonitor)display.SetScaling(current.Device,3);}catch{if(captured&&Current().Resolution==State.Original){State.Original=null;State.OriginalScaling=null;Save();}throw;}
 }
 internal void Restore(){if(!State.Original.HasValue)return;display.Apply(State.Device,State.Original.Value,State.Frequency,State.Bits);if(State.OriginalScaling is uint scaling)display.SetScaling(State.Device,scaling);State.OriginalScaling=null;State.Original=null;sawGame=false;Save();}
 internal bool ObserveGame(bool running){if(!State.Original.HasValue){sawGame=false;return false;}if(running)sawGame=true;else if(sawGame&&State.AutoRestore){Restore();return true;}return false;}
 internal void AddPreset(string input){string value=StretchResolution.Parse(input).ToString();if(!State.Presets.Contains(value)){State.Presets.Add(value);Save();}}
 internal static bool GameRunning(){var processes=Process.GetProcessesByName("VALORANT-Win64-Shipping");try{return processes.Length>0;}finally{foreach(var process in processes)process.Dispose();}}
}
internal static class StretchMonitorDriver {
 internal static List<(string Id,bool Disabled)> Devices(){
  using var search=new ManagementObjectSearcher("SELECT PNPDeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE PNPClass = 'Monitor'");using var results=search.Get();
  var devices=new List<(string,bool)>();foreach(ManagementObject device in results){using(device){string id=device["PNPDeviceID"] as string;if(!string.IsNullOrEmpty(id))devices.Add((id,Convert.ToUInt32(device["ConfigManagerErrorCode"])==22));}}return devices;
 }
 internal static int Worker(string action){if(action!="enable"&&action!="disable")return 2;var devices=Devices();if(devices.Count==0)return 3;
  foreach(var device in devices){var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","pnputil.exe")){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add(action=="enable"?"/enable-device":"/disable-device");start.ArgumentList.Add(device.Id);using var process=Process.Start(start);process.WaitForExit();if(process.ExitCode!=0&&process.ExitCode!=3010)return process.ExitCode;}return 0;
 }
 internal static async Task SetEnabled(bool enabled){string executable=Environment.ProcessPath!;var start=new ProcessStartInfo(executable){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(StretchMonitorDriver).Assembly.Location);start.ArgumentList.Add("--stretch-monitor");start.ArgumentList.Add(enabled?"enable":"disable");using var process=Process.Start(start);await process.WaitForExitAsync();if(process.ExitCode!=0)throw new InvalidOperationException($"Windows could not change all monitor devices (code {process.ExitCode}). Check Device Manager.");}
}
