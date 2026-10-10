using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace FramePilot {
 public sealed class Change {
  public string Id, Kind, Target, Name, Before, After, ValueKind, Status, Created, SessionId;
  public bool Existed;
 }
 public sealed partial class Journal {
  public string Root; public string Scope; public List<Change> Items; readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 16777216 };
  public Journal(string root) { Root=root; Directory.CreateDirectory(root); string p=Path.Combine(root,"journal.json"); Items=File.Exists(p)?json.Deserialize<List<Change>>(File.ReadAllText(p)):new List<Change>(); if(Items==null) throw new Exception("Invalid recovery journal. Preserve it before continuing."); }
  public static void Atomic(string path, byte[] bytes) { string tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp"; try { using(var f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { f.Write(bytes,0,bytes.Length); f.Flush(true); } if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path); } finally { if(File.Exists(tmp)) File.Delete(tmp); } }
  public void Save() { Atomic(Path.Combine(Root,"journal.json"),Encoding.UTF8.GetBytes(json.Serialize(Items))); }
  Change Record(string kind,string target,string name,string before,string after,bool exists,string type) { var c=new Change { Id=Guid.NewGuid().ToString("N"),Kind=kind,Target=target,Name=name,Before=before,After=after,Existed=exists,ValueKind=type,Status="Pending",Created=DateTime.Now.ToString("s"),SessionId=Scope }; Items.Add(c); Save(); return c; }
  public static string Hash(byte[] b) { using(var h=SHA256.Create()) return Convert.ToBase64String(h.ComputeHash(b)); }
  public void RegistryDword(string path,string name,int value) {
   using(var read=Registry.CurrentUser.OpenSubKey(path)) {
    bool exists=read!=null && read.GetValueNames().Contains(name); string old=exists?Convert.ToString(read.GetValue(name),CultureInfo.InvariantCulture):null;
    if(exists && read.GetValueKind(name)!=RegistryValueKind.DWord) throw new Exception("Unexpected registry type; no change made: "+name);
    if(exists && old==value.ToString(CultureInfo.InvariantCulture)) return;
    var c=Record("Registry",path,name,old,value.ToString(CultureInfo.InvariantCulture),exists,"DWord");
    using(var k=Registry.CurrentUser.CreateSubKey(path)) k.SetValue(name,value,RegistryValueKind.DWord);
    c.Status="Applied"; Save();
   }
  }
  public void FileEdit(string path,byte[] next) {
   path=Path.GetFullPath(path); var old=File.ReadAllBytes(path); if(old.SequenceEqual(next)) return;
   if((File.GetAttributes(path)&FileAttributes.ReadOnly)!=0) throw new Exception("Config is read-only; uncheck read-only yourself before applying.");
   string backup=Path.Combine(Root,Guid.NewGuid().ToString("N")+".backup"); Atomic(backup,old);
   var c=Record("File",path,"Config backup",backup,Hash(next),true,Hash(old));
   if(Hash(File.ReadAllBytes(path))!=c.ValueKind) throw new Exception("Config changed during preview; refresh and retry.");
   Atomic(path,next); c.Status="Applied"; Save();
  }
  public static string Run(string exe,string args) {
   var s=new ProcessStartInfo(exe,args) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
   using(var p=Process.Start(s)) { var output=p.StandardOutput.ReadToEndAsync(); var error=p.StandardError.ReadToEndAsync(); if(!p.WaitForExit(12000)) { try {p.Kill();} catch {} throw new Exception("Timed out: "+Path.GetFileName(exe)); } string o=output.Result; string e=error.Result; if(p.ExitCode!=0) throw new Exception(e+o); return o.Trim(); }
  }
  public static string Power() { var m=System.Text.RegularExpressions.Regex.Match(Run(Path.Combine(Environment.SystemDirectory,"powercfg.exe"),"/getactivescheme"),@"[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}"); if(!m.Success) throw new Exception("Could not read power plan."); return m.Value.ToLowerInvariant(); }
  public void SetPower(string guid) { if(guid!="381b4222-f694-41f0-9685-ff5bb260df2e" && guid!="8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") throw new Exception("Unsupported plan"); string old=Power(); if(old==guid)return; var c=Record("Power","Power plan","Active plan",old,guid,true,null); Run(Path.Combine(Environment.SystemDirectory,"powercfg.exe"),"/setactive "+guid); c.Status="Applied"; Save(); }
  public void LowerPriority(Process p) {
   string identity=p.Id+"|"+p.StartTime.ToUniversalTime().Ticks; if(Items.Any(item=>item.Kind=="Priority"&&item.Target==identity&&(item.Status=="Applied"||item.Status=="Pending")))return;
   var old=p.PriorityClass; if(old==ProcessPriorityClass.BelowNormal||old==ProcessPriorityClass.Idle)return;
   var c=Record("Priority",identity,p.ProcessName,old.ToString(),"BelowNormal",true,null); p.PriorityClass=ProcessPriorityClass.BelowNormal; c.Status="Applied"; Save();
  }
  public string Restore(Change c) {
   if(c.Status=="Restored")return "Already restored";
   if(c.Kind=="Registry") { RestoreRegistryValue(c);
   } else if(c.Kind=="Quarantine") { RestoreQuarantine(c);
   } else if(c.Kind=="File") {
    if(!File.Exists(c.Target))throw new Exception("Config no longer exists; backup retained.");
    string now=Hash(File.ReadAllBytes(c.Target)); if(now!=c.ValueKind) { if(now!=c.After)throw new Exception("Conflict: game config changed after tuning. Backup retained; current config untouched."); var b=File.ReadAllBytes(c.Before); if(Hash(b)!=c.ValueKind)throw new Exception("Backup integrity check failed."); Atomic(c.Target,b); }
   } else if(c.Kind=="Power") { string now=Power(); if(now!=c.Before) { if(now!=c.After)throw new Exception("Conflict: power plan changed elsewhere."); Run(Path.Combine(Environment.SystemDirectory,"powercfg.exe"),"/setactive "+c.Before); }
   } else if(c.Kind=="Priority") {
    var parts=c.Target.Split('|'); Process p=null; try {p=Process.GetProcessById(int.Parse(parts[0]));} catch(ArgumentException) {} 
    if(p!=null)using(p) { if(p.StartTime.ToUniversalTime().Ticks.ToString()==parts[1] && p.PriorityClass.ToString()!=c.Before) { if(p.PriorityClass.ToString()!=c.After)throw new Exception("Priority changed elsewhere; kept newer setting."); p.PriorityClass=(ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass),c.Before); } }
   } else throw new Exception("Unknown journal entry type");
   c.Status="Restored"; Save(); return "Restored";
  }
 }
 public static class Config {
  public const string FortniteSection="/Script/FortniteGame.FortGameUserSettings";
  public static byte[] Patch(byte[] source,Dictionary<string,string> changes,string section,char separator) {
   Encoding enc=new UTF8Encoding(false,true); int skip=0;
   if(source.Length>=3&&source[0]==239&&source[1]==187&&source[2]==191) { enc=new UTF8Encoding(true,true); skip=3; }
   else if(source.Length>=2&&source[0]==255&&source[1]==254) {enc=Encoding.Unicode;skip=2;}
   else if(source.Length>=2&&source[0]==254&&source[1]==255) {enc=Encoding.BigEndianUnicode;skip=2;}
   string text=enc.GetString(source,skip,source.Length-skip); string newline=text.Contains("\r\n")?"\r\n":"\n"; var lines=text.Split(new[]{newline},StringSplitOptions.None); bool active=section==null; var seen=new HashSet<string>(StringComparer.Ordinal);
   for(int i=0;i<lines.Length;i++) { string line=lines[i].Trim(); if(line.StartsWith("[")&&line.EndsWith("]")) {active=line=="["+section+"]";continue;} if(!active||line.StartsWith(";")||line.StartsWith("#"))continue; int n=lines[i].IndexOf(separator); if(n<0)continue; string key=lines[i].Substring(0,n).Trim(); if(changes.ContainsKey(key)) {if(!seen.Add(key))throw new Exception("Duplicate config key: "+key); lines[i]=lines[i].Substring(0,n+1)+changes[key];} }
   var missing=changes.Keys.Where(k=>!seen.Contains(k)).ToArray(); if(missing.Length>0)throw new Exception("Unsupported config version; missing keys: "+string.Join(", ",missing)+". Use the game's settings menu instead.");
   var body=enc.GetBytes(string.Join(newline,lines)); return skip>0?enc.GetPreamble().Concat(body).ToArray():body;
  }
 }
 public sealed class FrameStats {
  public int Count,Rejected; public double AverageFps,Low1,Low01,P99,MeanMs,Duration; public double[] Frames; public string Metric="FrameTimeMs";
  public static FrameStats Calculate(IEnumerable<double> values) {
   var a=values.ToArray(); if(a.Length<100||a.Any(x=>x<=0||double.IsNaN(x)||double.IsInfinity(x)))throw new Exception("Need at least 100 finite, positive frame times (milliseconds).");
   var sorted=a.OrderByDescending(x=>x).ToArray(); return new FrameStats {Count=a.Length,Frames=a,MeanMs=a.Average(),Duration=a.Sum()/1000,AverageFps=1000/a.Average(),Low1=1000/sorted.Take(Math.Max(1,(int)Math.Ceiling(a.Length*.01))).Average(),Low01=1000/sorted.Take(Math.Max(1,(int)Math.Ceiling(a.Length*.001))).Average(),P99=sorted[a.Length-(int)Math.Ceiling(a.Length*.99)]};
  }
  public static FrameStats Load(string path) {
   if(new FileInfo(path).Length>128*1024*1024)throw new Exception("CSV exceeds 128 MB. Export a shorter capture.");
   using(var r=new StreamReader(path)) { string first=r.ReadLine(); if(first==null)throw new Exception("Empty CSV"); var head=Csv(first); int col=Array.FindIndex(head,x=>new[]{"MsBetweenPresents","FrameTime","FrameTimeMs","CPUFrameTime"}.Contains(x,StringComparer.OrdinalIgnoreCase)); if(col<0)throw new Exception("Expected FrameTimeMs, FrameTime, MsBetweenPresents or CPUFrameTime column in milliseconds.");
    int app=Array.FindIndex(head,x=>x.Equals("Application",StringComparison.OrdinalIgnoreCase)); int pid=Array.FindIndex(head,x=>x.Equals("ProcessID",StringComparison.OrdinalIgnoreCase)); int chain=Array.FindIndex(head,x=>x.Equals("SwapChainAddress",StringComparison.OrdinalIgnoreCase));
    var streams=new HashSet<string>(); var frames=new List<double>(); int rejected=0; string line; while((line=r.ReadLine())!=null) { if(string.IsNullOrWhiteSpace(line))continue; var cells=Csv(line); if(cells.Length!=head.Length)throw new Exception("CSV row length does not match its header."); string stream=(app>=0?cells[app]:"")+"|"+(pid>=0?cells[pid]:"")+"|"+(chain>=0?cells[chain]:""); streams.Add(stream); double ms; if(!double.TryParse(cells[col],NumberStyles.Float,CultureInfo.InvariantCulture,out ms)||ms<0||double.IsInfinity(ms)||double.IsNaN(ms))throw new Exception("Invalid frame time in CSV."); if(ms==0){rejected++;continue;} frames.Add(ms); }
    if(streams.Count>1)throw new Exception("Multiple processes or swapchains detected. Export one game process and one swapchain per capture."); var result=Calculate(frames);result.Rejected=rejected;result.Metric=head[col];return result;
   }
  }
  static string[] Csv(string line) { var cells=new List<string>();var s=new StringBuilder();bool quote=false; for(int i=0;i<line.Length;i++) {char c=line[i];if(c=='"') {if(quote&&i+1<line.Length&&line[i+1]=='"'){s.Append('"');i++;}else quote=!quote;} else if(c==','&&!quote){cells.Add(s.ToString().Trim());s.Clear();}else s.Append(c);} if(quote)throw new Exception("Unclosed CSV quote"); cells.Add(s.ToString().Trim()); return cells.ToArray(); }
  public string Summary() {return string.Format(CultureInfo.InvariantCulture,"{0:N1} average FPS   |   {1:N1} 1% low   |   {2:N1} 0.1% low\r\n{3:N2} ms p99   |   {4:N0} frames   |   {5:N1}s capture   |   {6} zero samples skipped",AverageFps,Low1,Low01,P99,Count,Duration,Rejected);}
 }
}
