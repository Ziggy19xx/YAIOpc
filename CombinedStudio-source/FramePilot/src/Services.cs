using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace FramePilot {
 public sealed partial class Journal {
  public void RegistryText(string path,string name,string value,RegistryValueKind kind) {
   if(kind!=RegistryValueKind.String&&kind!=RegistryValueKind.ExpandString)throw new Exception("Unsupported text type");
   using(var read=Registry.CurrentUser.OpenSubKey(path)) {
    bool exists=read!=null&&read.GetValueNames().Contains(name);string old=exists?Convert.ToString(read.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames)):null;
    if(exists&&read.GetValueKind(name)!=kind)throw new Exception("Unexpected setting type; left unchanged.");if(exists&&old==value)return;
    var c=Record("Registry",path,name,old,value,exists,kind.ToString());using(var k=Registry.CurrentUser.CreateSubKey(path))k.SetValue(name,value,kind);c.Status="Applied";Save();
   }
  }
  public void RemoveStartup(string name,string expected) {
   const string path=@"Software\Microsoft\Windows\CurrentVersion\Run";
   if(!Startup.IsEligible(name,expected))throw new Exception("This startup entry is protected or not recognized.");
   using(var k=Registry.CurrentUser.OpenSubKey(path,true)) {
    if(k==null||!k.GetValueNames().Contains(name))throw new Exception("Startup entry no longer exists.");
    var kind=k.GetValueKind(name);if(kind!=RegistryValueKind.String&&kind!=RegistryValueKind.ExpandString)throw new Exception("Unexpected startup type.");
    string before=Convert.ToString(k.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames));if(before!=expected)throw new Exception("Startup entry changed since scan. Rescan first.");
    var c=Record("Registry",path,name,before,null,true,kind.ToString());k.DeleteValue(name,true);c.Status="Applied";Save();
   }
  }
  void RestoreRegistryValue(Change c) {
   var kind=(RegistryValueKind)Enum.Parse(typeof(RegistryValueKind),c.ValueKind??"DWord");if(kind!=RegistryValueKind.DWord&&kind!=RegistryValueKind.String&&kind!=RegistryValueKind.ExpandString)throw new Exception("Unsupported registry recovery type.");
   using(var read=Registry.CurrentUser.OpenSubKey(c.Target)) {
    bool exists=read!=null&&read.GetValueNames().Contains(c.Name);string now=exists?Convert.ToString(read.GetValue(c.Name,null,RegistryValueOptions.DoNotExpandEnvironmentNames),CultureInfo.InvariantCulture):null;
    if((!exists&&!c.Existed)||(exists&&c.Existed&&now==c.Before&&read.GetValueKind(c.Name)==kind))return;
    if(c.After==null?exists:(!exists||now!=c.After||read.GetValueKind(c.Name)!=kind))throw new Exception("Conflict: setting changed elsewhere; kept the newer value.");
   }
   using(var k=Registry.CurrentUser.CreateSubKey(c.Target)) {if(c.Existed)k.SetValue(c.Name,kind==RegistryValueKind.DWord?(object)int.Parse(c.Before,CultureInfo.InvariantCulture):c.Before,kind);else k.DeleteValue(c.Name,false);}
  }
  public void Quarantine(TempItem item,string root) {
   Maintenance.Validate(item.Path,root);var fi=new FileInfo(item.Path);if(fi.Length!=item.Bytes||fi.LastWriteTimeUtc.Ticks!=item.Modified)throw new Exception("File changed since scan; skipped.");
   string backup=Path.Combine(Root,Guid.NewGuid().ToString("N")+".quarantine");string hash=Hash(File.ReadAllBytes(item.Path));var c=Record("Quarantine",item.Path,"Temporary file: "+fi.Name,backup,hash,true,Path.GetFullPath(root));
   // Open exclusively to reject files that are in use; revalidate immediately before moving.
   using(var f=new FileStream(item.Path,FileMode.Open,FileAccess.Read,FileShare.None)) {if(f.Length!=item.Bytes)throw new Exception("File changed; skipped.");}
   Maintenance.Validate(item.Path,root);if(Hash(File.ReadAllBytes(item.Path))!=hash)throw new Exception("File changed; skipped.");File.Move(item.Path,backup);c.Status="Applied";Save();
  }
  void RestoreQuarantine(Change c) {
   Maintenance.ValidateLocation(c.Target,c.ValueKind);
   if(File.Exists(c.Target)) {if(!File.Exists(c.Before)&&Hash(File.ReadAllBytes(c.Target))==c.After)return;throw new Exception("A new file occupies the original path; retained quarantine.");}
   if(!File.Exists(c.Before)||Hash(File.ReadAllBytes(c.Before))!=c.After)throw new Exception("Quarantine missing or changed.");File.Move(c.Before,c.Target);
  }
 }
 public sealed class StartupItem {public string Name,Command;public bool Eligible;public override string ToString(){return (Eligible?"Can disable: ":"Manage in Windows: ")+(Name=="com.blitz.app"?"Blitz":Name=="electron.app.CurseForge"?"CurseForge":Name=="EpicGamesLauncher"?"Epic Games Launcher":Name);}}
 public static class Startup {
  public const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
  public static bool IsEligible(string name,string command) {
   if(string.IsNullOrWhiteSpace(command))return false;
   // Match both the startup name and a known application path; never execute the stored command.
   var map=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase) {{"Discord",new[]{@"\Discord\Update.exe",@"\Discord.exe"}},{"Spotify",new[]{@"\Spotify.exe"}},{"Slack",new[]{@"\slack.exe",@"\slack\Update.exe"}},{"com.squirrel.Teams.Teams",new[]{@"\Microsoft\Teams\Update.exe"}},{"MicrosoftEdgeAutoLaunch",new[]{@"\Microsoft\Edge\Application\msedge.exe"}}};
   map["Steam"]=new[]{@"\Steam\steam.exe"};map["EpicGamesLauncher"]=new[]{@"\EpicGamesLauncher.exe"};map["OneDrive"]=new[]{@"\Microsoft\OneDrive\OneDrive.exe"};map["electron.app.CurseForge"]=new[]{@"\CurseForge Windows\CurseForge.exe"};map["com.blitz.app"]=new[]{@"\Blitz\Blitz.exe"};map["Overwolf"]=new[]{@"\Overwolf\OverwolfLauncher.exe"};
   string key=name.StartsWith("MicrosoftEdgeAutoLaunch_",StringComparison.OrdinalIgnoreCase)?"MicrosoftEdgeAutoLaunch":name;
   string[] tails;if(!map.TryGetValue(key,out tails))return false;
   string expanded=Environment.ExpandEnvironmentVariables(command).Trim();string exe;
   if(expanded.StartsWith("\"")){int end=expanded.IndexOf('"',1);if(end<0)return false;exe=expanded.Substring(1,end-1);}else {int end=expanded.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);if(end<0)return false;exe=expanded.Substring(0,end+4);}
   return Path.IsPathRooted(exe)&&tails.Any(t=>exe.EndsWith(t,StringComparison.OrdinalIgnoreCase));
  }
  public static List<StartupItem> Read(){var list=new List<StartupItem>();using(var key=Registry.CurrentUser.OpenSubKey(RunKey)){if(key!=null)foreach(var name in key.GetValueNames()){string command=Convert.ToString(key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames));list.Add(new StartupItem{Name=name,Command=command,Eligible=IsEligible(name,command)});}}return list.OrderByDescending(x=>x.Eligible).ThenBy(x=>x.Name).ToList();}
 }
 public sealed class TempItem {public string Path;public long Bytes,Modified;public override string ToString(){return System.IO.Path.GetFileName(Path)+"  |  "+(Bytes/1048576.0).ToString("0.00")+" MB  |  "+new DateTime(Modified,DateTimeKind.Utc).ToLocalTime().ToShortDateString();}}
 public static class Maintenance {
  public static void ValidateLocation(string file,string root) {
   string full=Path.GetFullPath(file),basePath=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
   if(!string.Equals(Path.GetDirectoryName(full),basePath,StringComparison.OrdinalIgnoreCase))throw new Exception("Only files directly inside the scanned temp folder are eligible.");
   for(var d=new DirectoryInfo(basePath);d!=null;d=d.Parent)if((d.Attributes&FileAttributes.ReparsePoint)!=0)throw new Exception("Reparse-point folders are excluded.");
   if(File.Exists(full)&&(File.GetAttributes(full)&(FileAttributes.ReparsePoint|FileAttributes.ReadOnly))!=0)throw new Exception("Linked or read-only files are excluded.");
  }
  public static void Validate(string file,string root) {ValidateLocation(file,root);var f=new FileInfo(file);if(!new[]{".tmp",".log",".dmp"}.Contains(f.Extension,StringComparer.OrdinalIgnoreCase)||f.LastWriteTimeUtc>DateTime.UtcNow.AddDays(-7)||f.Length>50*1048576)throw new Exception("Only .tmp/.log/.dmp files older than 7 days and at most 50 MB are eligible.");}
  public static List<TempItem> Scan(string root){var list=new List<TempItem>();foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.TopDirectoryOnly).Take(5000)){try{Validate(file,root);var f=new FileInfo(file);list.Add(new TempItem{Path=f.FullName,Bytes=f.Length,Modified=f.LastWriteTimeUtc.Ticks});if(list.Count>=200)break;}catch{}}return list;}
 }
 public sealed class GamePreset {
  public int Version=2;public string Game,Name,ConfigPath;public int Quality,Cap,Render,Simulation,Resolution;public bool Vsync,DisableFrameGen,DisableNanite;public Dictionary<string,int> Graphics=new Dictionary<string,int>();
  public void Validate(){if(Version!=2||!new[]{"Valorant","Fortnite","Call of Duty","Minecraft Java","Escape from Tarkov"}.Contains(Game)||string.IsNullOrWhiteSpace(Name)||Name.Length>60||Cap<30||Cap>1000||Render<2||Render>32||Simulation<5||Simulation>32||Quality<0||Quality>1||Resolution<0||Resolution>4||Graphics==null)throw new Exception("Invalid or unsupported preset.");string[] keys={"sg.TextureQuality","sg.ViewDistanceQuality","sg.ShadowQuality","sg.EffectsQuality","sg.PostProcessQuality","sg.FoliageQuality"};if(Graphics.Count!=6||keys.Any(k=>!Graphics.ContainsKey(k))||Graphics.Values.Any(v=>v<0||v>3))throw new Exception("Invalid graphics values.");if(ConfigPath!=null){if(!Path.IsPathRooted(ConfigPath)||!(Path.GetFileName(ConfigPath).Equals("GameUserSettings.ini",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(ConfigPath).Equals("options.txt",StringComparison.OrdinalIgnoreCase)))throw new Exception("Invalid config path.");}}
  public override string ToString(){return Name+"  /  "+Game;}
 }
 public static class Presets {
  public static void Save(string path,GamePreset preset){preset.Validate();Journal.Atomic(path,Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(preset)));}
  public static GamePreset Load(string path){if(new FileInfo(path).Length>65536)throw new Exception("Preset too large.");var p=new JavaScriptSerializer().Deserialize<GamePreset>(File.ReadAllText(path));if(p==null)throw new Exception("Empty preset.");p.Validate();return p;}
 }
 public sealed class ResourceSample {public DateTime Time;public double Cpu=double.NaN,Ram=double.NaN,Gpu=double.NaN;public string GpuDetails="GPU sample unavailable";}
 public sealed class ResourceSampler {
  [StructLayout(LayoutKind.Sequential)]struct Memory {public uint Length,Load;public ulong TotalPhys,AvailPhys,TotalPage,AvailPage,TotalVirtual,AvailVirtual,AvailExtended;}
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GlobalMemoryStatusEx(ref Memory m);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
  long lastIdle,lastTotal;
  public ResourceSample Sample(bool gpu){var result=new ResourceSample{Time=DateTime.Now};long idle,kernel,user;if(GetSystemTimes(out idle,out kernel,out user)){long total=kernel+user;if(lastTotal>0&&total>lastTotal)result.Cpu=Math.Max(0,Math.Min(100,100.0*(1-(double)(idle-lastIdle)/(total-lastTotal))));lastTotal=total;lastIdle=idle;}var memory=new Memory();memory.Length=(uint)Marshal.SizeOf(memory);if(GlobalMemoryStatusEx(ref memory))result.Ram=memory.Load;
   if(gpu)try{string exe=Path.Combine(Environment.SystemDirectory,"nvidia-smi.exe");if(!File.Exists(exe))throw new Exception("nvidia-smi not installed");string raw=Journal.Run(exe,"--query-gpu=name,utilization.gpu,memory.used,memory.total,temperature.gpu,power.draw --format=csv,noheader,nounits");var cols=raw.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)[0].Split(',').Select(x=>x.Trim()).ToArray();double utilization;if(cols.Length==6){if(double.TryParse(cols[1],NumberStyles.Float,CultureInfo.InvariantCulture,out utilization))result.Gpu=utilization;result.GpuDetails=cols[0]+"  |  "+cols[2]+" / "+cols[3]+" MiB VRAM  |  "+cols[4]+" C  |  "+cols[5]+" W";}}catch(Exception e){result.GpuDetails="GPU unavailable: "+e.Message;}return result;
  }
 }
 public sealed class NetworkResult {
  public int Sent,Received;public double Median,Jitter,Loss;
  public static NetworkResult Calculate(int sent,List<long> replies){if(sent<=0||replies.Count>sent)throw new Exception("Invalid ping counts");var a=replies.OrderBy(x=>x).ToArray();return new NetworkResult{Sent=sent,Received=a.Length,Loss=100.0*(sent-a.Length)/sent,Median=a.Length==0?double.NaN:(a[(a.Length-1)/2]+a[a.Length/2])/2.0,Jitter=replies.Count<2?double.NaN:Enumerable.Range(1,replies.Count-1).Average(i=>(double)Math.Abs(replies[i]-replies[i-1]))};}
  public override string ToString(){return string.Format("Replies: {0}/{1}   Loss: {2:0.0}%   Median RTT: {3:0.0} ms   Mean successive RTT variation: {4:0.0} ms",Received,Sent,Loss,Median,Jitter);}
  public static async Task<NetworkResult> Measure(string host){if(Uri.CheckHostName(host)==UriHostNameType.Unknown)throw new Exception("Enter a hostname or IP address only.");var values=new List<long>();using(var ping=new Ping())for(int i=0;i<10;i++){try{var reply=await ping.SendPingAsync(host,750);if(reply.Status==IPStatus.Success)values.Add(reply.RoundtripTime);}catch(PingException){}await Task.Delay(150);}return Calculate(10,values);}
  public static string Gateway(){foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(x=>x.OperationalStatus==OperationalStatus.Up&&x.NetworkInterfaceType!=NetworkInterfaceType.Loopback)){var g=adapter.GetIPProperties().GatewayAddresses.FirstOrDefault(x=>x.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&!x.Address.Equals(IPAddress.Any));if(g!=null)return g.Address.ToString();}return "1.1.1.1";}
 }
}


