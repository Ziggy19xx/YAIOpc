using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FramePilot.Monitoring {
 // Wire DTOs are confined to this service boundary; null means unavailable, never zero.
 public sealed class LiveMetrics {
  public double? cpu_percent {get;set;} public double?[] per_core_percent {get;set;}
  public double? cpu_frequency_mhz {get;set;} public long? memory_total_bytes {get;set;}
  public long? memory_used_bytes {get;set;} public long? memory_available_bytes {get;set;}
  public double? memory_percent {get;set;} public Volume[] volumes {get;set;}
  public long? network_sent_bytes {get;set;} public long? network_received_bytes {get;set;}
  public double? uptime_seconds {get;set;} public Gpu[] gpu_adapters {get;set;}
  public Sensor[] sensors {get;set;} public Dictionary<string,string> errors {get;set;}
 }
 public sealed class Volume {public string name {get;set;} public string mountpoint {get;set;} public long? used_bytes {get;set;} public long? free_bytes {get;set;} public long? total_bytes {get;set;} public double? percent {get;set;}}
 public sealed class Gpu {public string name {get;set;} public string driver_version {get;set;} public double? vram_total_bytes {get;set;} public double? vram_used_bytes {get;set;} public double? utilization_percent {get;set;} public double? temperature_c {get;set;} public double? clock_mhz {get;set;} public double? power_watts {get;set;} public string pci_bus_id {get;set;} public string provider {get;set;} public string driver_provider {get;set;} public string driver_date {get;set;} public string pnp_id {get;set;}}
 public sealed class Sensor {public string hardware {get;set;} public string hardware_type {get;set;} public string name {get;set;} public string sensor_type {get;set;} public double? value {get;set;} public string unit {get;set;}}
 public sealed class Cpu {public string name {get;set;} public int? physical_cores {get;set;} public int? logical_cores {get;set;}}
 public sealed class Dimm {public long? capacity_bytes {get;set;} public string manufacturer {get;set;} public string part_number {get;set;} public string serial_number {get;set;} public int? speed_mhz {get;set;} public int? configured_speed_mhz {get;set;} public string device_locator {get;set;} public int? form_factor {get;set;}}
 public sealed class Memory {public long? total_bytes {get;set;} public Dimm[] modules {get;set;}}
 public sealed class Board {public string manufacturer {get;set;} public string model {get;set;} public string product {get;set;} public string bios_vendor {get;set;} public string bios_version {get;set;} public string bios_date {get;set;}}
 public sealed class Disk {public string model {get;set;} public long? size_bytes {get;set;} public string @interface {get;set;} public string media_type {get;set;} public string status {get;set;}}
 public sealed class Driver {public string name {get;set;} public string device_class {get;set;} public string[] hardware_ids {get;set;} public string manufacturer {get;set;} public string driver_version {get;set;} public string driver_date {get;set;} public int? problem_code {get;set;}}
 public sealed class Inventory {public string hostname {get;set;} public string os_name {get;set;} public string os_version {get;set;} public double? uptime_seconds {get;set;} public Cpu cpu {get;set;} public Memory memory {get;set;} public Board motherboard {get;set;} public Gpu[] gpu_adapters {get;set;} public Disk[] storage_devices {get;set;} public Driver[] drivers {get;set;} public Dictionary<string,string> provider_errors {get;set;}}
 public sealed class Sample {public DateTime Utc {get;set;} public LiveMetrics Metrics {get;set;}}
 public interface IMonitorService:IDisposable {Task StartAsync(string sensorPath,CancellationToken token);Task<Sample> PollAsync(CancellationToken token);Task<Inventory> InventoryAsync(CancellationToken token);Task StopAsync();}
 public sealed class MonitorSettings {
  public int IntervalSeconds {get;set;} public string SensorPath {get;set;}
  public MonitorSettings(){IntervalSeconds=2;SensorPath="";}
  public void Validate(){if(!new[]{1,2,5,10}.Contains(IntervalSeconds))IntervalSeconds=2;if(SensorPath==null)SensorPath="";}
  public static string FilePath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FramePilot","monitor-settings.json");}}
  public static MonitorSettings Load(){try{var s=new JavaScriptSerializer().Deserialize<MonitorSettings>(File.ReadAllText(FilePath));s.Validate();return s;}catch{return new MonitorSettings();}}
  public void Save(){Validate();Directory.CreateDirectory(Path.GetDirectoryName(FilePath));Journal.Atomic(FilePath,Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(this)));}
 }
 internal sealed class Reply {public int version {get;set;} public int id {get;set;} public bool ok {get;set;} public string utc {get;set;} public string error {get;set;} public object data {get;set;}}
 public sealed class PythonMonitorService:IMonitorService {
  public const int MaxFrame=4*1024*1024;
  readonly string directory;readonly string testInterpreter; readonly SemaphoreSlim gate=new SemaphoreSlim(1,1); readonly object processLock=new object();
  Process process;OwnedJob job;int sequence;bool disposed;string diagnostic="";
  readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=MaxFrame,RecursionLimit=64};
  public PythonMonitorService():this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"monitor")){}
  public PythonMonitorService(string root){directory=root;}
  internal PythonMonitorService(string root,string interpreter):this(root){testInterpreter=interpreter;}
  public int WorkerId {get {lock(processLock)return process==null?0:process.Id;}}
  public async Task StartAsync(string sensorPath,CancellationToken token){
   await gate.WaitAsync(token).ConfigureAwait(false);
   try{
    if(disposed)throw new ObjectDisposedException("monitor");if(process!=null)return;
    string exe=testInterpreter??Path.Combine(directory,"runtime","python.exe"),script=Path.Combine(directory,"worker.py");
    if(!File.Exists(exe)||!File.Exists(script))throw new FileNotFoundException("Monitoring files are missing. Extract the complete FramePilot application folder, including monitor/runtime.");
    var p=new Process{StartInfo=new ProcessStartInfo(exe,"-I -B -u \""+script+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=directory}};
    p.StartInfo.EnvironmentVariables.Remove("PYTHONPATH");p.StartInfo.EnvironmentVariables.Remove("PYTHONHOME");p.StartInfo.EnvironmentVariables.Remove("LIBREHARDWAREMONITOR_DLL");
    p.ErrorDataReceived+=delegate(object s,DataReceivedEventArgs e){if(e.Data!=null)diagnostic=e.Data.Length>1000?e.Data.Substring(0,1000):e.Data;};
    lock(processLock){if(disposed)throw new ObjectDisposedException("monitor");job=new OwnedJob();process=p;try{p.Start();job.Assign(p);p.BeginErrorReadLine();}catch{Abort();throw;}}
    var hello=await Exchange("hello",null,10000,token).ConfigureAwait(false);
    var values=json.ConvertToType<Dictionary<string,object>>(hello.data);
    if(Convert.ToInt32(values["protocol"])!=1||Convert.ToInt32(values["architecture_bits"])!=64)throw new InvalidDataException("Incompatible monitoring worker protocol or architecture.");
    await Exchange("configure",sensorPath,20000,token).ConfigureAwait(false);
   }catch{Abort();throw;}finally{gate.Release();}
  }
  public async Task<Sample> PollAsync(CancellationToken token){await gate.WaitAsync(token).ConfigureAwait(false);try{var r=await Exchange("poll",null,12000,token).ConfigureAwait(false);DateTime utc;if(!DateTime.TryParse(r.utc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out utc))throw new InvalidDataException("Invalid sample timestamp");return new Sample{Utc=utc,Metrics=json.ConvertToType<LiveMetrics>(r.data)};}finally{gate.Release();}}
  public async Task<Inventory> InventoryAsync(CancellationToken token){await gate.WaitAsync(token).ConfigureAwait(false);try{return json.ConvertToType<Inventory>((await Exchange("inventory",null,45000,token).ConfigureAwait(false)).data);}finally{gate.Release();}}
  public async Task<T> StorageRequest<T>(string operation,Dictionary<string,object> arguments,CancellationToken token){await gate.WaitAsync(token).ConfigureAwait(false);try{return json.ConvertToType<T>((await Exchange(operation,null,10000,token,arguments).ConfigureAwait(false)).data);}finally{gate.Release();}}
  async Task<Reply> Exchange(string operation,string sensor,int timeout,CancellationToken token,Dictionary<string,object> arguments=null){
   Process p;lock(processLock)p=process;if(p==null)throw new InvalidOperationException("Monitoring worker is not running");
   int id=++sequence;var request=new Dictionary<string,object>{{"version",1},{"id",id},{"operation",operation}};if(sensor!=null)request["sensor_path"]=sensor;
   if(arguments!=null)foreach(var pair in arguments)request[pair.Key]=pair.Value;
   byte[] payload=Encoding.UTF8.GetBytes(json.Serialize(request));
   Task<Reply> work=Task.Run(delegate{
    p.StandardInput.BaseStream.Write(BitConverter.GetBytes(payload.Length),0,4);p.StandardInput.BaseStream.Write(payload,0,payload.Length);p.StandardInput.BaseStream.Flush();
    var header=ReadExact(p.StandardOutput.BaseStream,4);int length=BitConverter.ToInt32(header,0);if(length<=0||length>MaxFrame)throw new InvalidDataException("Invalid monitoring frame length");
    return json.Deserialize<Reply>(Encoding.UTF8.GetString(ReadExact(p.StandardOutput.BaseStream,length)));
   });
   using(var timeoutCancel=CancellationTokenSource.CreateLinkedTokenSource(token)){
    var delay=Task.Delay(timeout,timeoutCancel.Token);
    if(await Task.WhenAny(work,delay).ConfigureAwait(false)!=work){Abort();try{await work.ConfigureAwait(false);}catch{}token.ThrowIfCancellationRequested();throw new TimeoutException("Monitoring timed out during "+operation+". You can retry.");}
    timeoutCancel.Cancel();
   }
   Reply reply;
   try{reply=await work.ConfigureAwait(false);}catch(Exception e){Abort();throw new IOException("Monitoring worker stopped: "+(diagnostic.Length>0?diagnostic:e.Message),e);}
   if(reply==null||reply.version!=1||reply.id!=id){Abort();throw new InvalidDataException("Monitoring response identity mismatch");}
   if(!reply.ok)throw new InvalidOperationException(reply.error??"Monitoring provider failed");return reply;
  }
  internal static byte[] ReadExact(Stream stream,int length){var bytes=new byte[length];int read=0;while(read<length){int n=stream.Read(bytes,read,length-read);if(n==0)throw new EndOfStreamException("Monitoring pipe closed");read+=n;}return bytes;}
  public async Task StopAsync(){await gate.WaitAsync().ConfigureAwait(false);try{if(process!=null)try{await Exchange("shutdown",null,4000,CancellationToken.None).ConfigureAwait(false);}catch{}finally{Abort();}}finally{gate.Release();}}
  void Abort(){lock(processLock){if(job!=null){job.Dispose();job=null;}if(process!=null){try{if(!process.HasExited)process.Kill();process.WaitForExit(1500);}catch{}process.Dispose();process=null;}}}
  public void Dispose(){disposed=true;Abort();}
 }
 // All worker descendants (including nvidia-smi) die when the owner closes or crashes.
 internal sealed class OwnedJob:IDisposable {
  IntPtr handle;
  [StructLayout(LayoutKind.Sequential)]struct Basic{public long PerProcess,PerJob;public uint Flags;public UIntPtr Min,Max;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
  [StructLayout(LayoutKind.Sequential)]struct Io {public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
  [StructLayout(LayoutKind.Sequential)]struct Extended{public Basic Basic;public Io Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr attributes,string name);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int info,ref Extended data,int length);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
  public OwnedJob(){handle=CreateJobObject(IntPtr.Zero,null);var data=new Extended();data.Basic.Flags=0x2000;if(handle==IntPtr.Zero||!SetInformationJobObject(handle,9,ref data,Marshal.SizeOf(typeof(Extended)))){Dispose();throw new System.ComponentModel.Win32Exception();}}
  public void Assign(Process p){if(!AssignProcessToJobObject(handle,p.Handle))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Could not attach monitor to its lifecycle job");}
  public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
 }
}




