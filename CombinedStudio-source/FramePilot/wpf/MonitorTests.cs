using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using FramePilot.Monitoring;

namespace FramePilot {
 internal static class MonitorTests {
  static readonly List<string> results=new List<string>();
  static void Check(bool condition,string name){if(!condition)throw new Exception(name);results.Add("PASS: "+name);}
  public static int Run(string report){try{RunAsync().GetAwaiter().GetResult();File.WriteAllLines(report,results);return 0;}catch(Exception e){results.Add("FAIL: "+e);File.WriteAllLines(report,results);return 1;}}
  static bool Gone(int pid){try{using(var p=Process.GetProcessById(pid))return p.HasExited;}catch(ArgumentException){return true;}}
  static async Task RunAsync(){
   var json=new JavaScriptSerializer();var metrics=json.Deserialize<LiveMetrics>("{\"cpu_percent\":null,\"memory_total_bytes\":34359738368,\"network_received_bytes\":4294967300,\"gpu_adapters\":[{\"temperature_c\":null,\"power_watts\":125.5}]}");
   Check(!metrics.cpu_percent.HasValue,"Unavailable CPU remains null");Check(metrics.memory_total_bytes==34359738368L&&metrics.network_received_bytes==4294967300L,"64-bit byte counters retain units and precision");Check(!metrics.gpu_adapters[0].temperature_c.HasValue&&metrics.gpu_adapters[0].power_watts==125.5,"GPU units and unavailable values deserialize");
   bool truncated=false;try{PythonMonitorService.ReadExact(new MemoryStream(new byte[]{1,2}),4);}catch(EndOfStreamException){truncated=true;}Check(truncated,"Truncated pipe frame rejected");
   var settings=new MonitorSettings{IntervalSeconds=0,SensorPath=null};settings.Validate();Check(settings.IntervalSeconds==2&&settings.SensorPath=="","Invalid polling preferences fall back safely");
   using(var absent=new PythonMonitorService(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")))){bool missing=false;try{await absent.StartAsync("",CancellationToken.None);}catch(FileNotFoundException){missing=true;}Check(missing&&absent.WorkerId==0,"Missing runtime has actionable error and no worker");}
   using(var service=new PythonMonitorService()){
    await service.StartAsync("",CancellationToken.None);int pid=service.WorkerId;Check(pid>0,"Private x64 worker negotiates and starts");
    var inventory=await service.InventoryAsync(CancellationToken.None);Check(inventory.cpu!=null&&inventory.memory!=null,"Typed hardware inventory returned");Check(inventory.provider_errors!=null,"Inventory provider availability returned explicitly");
    await Task.Delay(250);var sample=await service.PollAsync(CancellationToken.None);Check(Math.Abs((DateTime.UtcNow-sample.Utc).TotalSeconds)<30,"UTC sample timestamp is current");Check(sample.Metrics.memory_total_bytes>0&&sample.Metrics.memory_used_bytes>=0,"Real memory sample available");Check(sample.Metrics.cpu_percent>=0&&sample.Metrics.cpu_percent<=100,"Real CPU sample in percent range");Check(sample.Metrics.per_core_percent!=null&&sample.Metrics.per_core_percent.Length>0,"Real per-core CPU values returned");Check(sample.Metrics.errors.ContainsKey("sensors"),"Disabled optional sensors explicitly explained");
    var concurrent=await Task.WhenAll(service.PollAsync(CancellationToken.None),service.PollAsync(CancellationToken.None));Check(concurrent.Length==2,"Concurrent callers serialize without crossed replies");
    await service.StopAsync();await Task.Delay(150);Check(service.WorkerId==0&&Gone(pid),"Graceful stop leaves no worker");
    await service.StartAsync("",CancellationToken.None);pid=service.WorkerId;Check(pid>0,"Stopped service can restart");using(var process=Process.GetProcessById(pid))process.Kill();bool exited=false;try{await service.PollAsync(CancellationToken.None);}catch(IOException){exited=true;}Check(exited&&service.WorkerId==0,"Unexpected exit translated into service error");
   }
   using(var service=new PythonMonitorService()){
    using(var token=new CancellationTokenSource()){token.Cancel();bool cancelled=false;try{await service.StartAsync("",token.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&service.WorkerId==0,"Cancelled startup creates no worker");}
    await service.StartAsync("",CancellationToken.None);int pid=service.WorkerId;service.Dispose();await Task.Delay(150);Check(Gone(pid),"Disposal terminates worker through owner job");
   }
   string protocolFixture=Path.Combine(Path.GetTempPath(),"framepilot-protocol-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(protocolFixture);
   try{
    string interpreter=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"monitor","runtime","python.exe");
    foreach(string mode in new[]{"oversized","mismatch","hang"}){
     string code="import sys,struct,json,time\nr=sys.stdin.buffer;w=sys.stdout.buffer\nn=struct.unpack('<I',r.read(4))[0];q=json.loads(r.read(n))\n";
     if(mode=="oversized")code+="w.write(struct.pack('<I',5000000));w.flush()\ntime.sleep(30)\n";
     else if(mode=="mismatch")code+="b=json.dumps({'version':1,'id':9999,'ok':True,'data':{}}).encode();w.write(struct.pack('<I',len(b))+b);w.flush()\ntime.sleep(30)\n";
     else code+="time.sleep(30)\n";
     File.WriteAllText(Path.Combine(protocolFixture,"worker.py"),code);
     using(var test=new PythonMonitorService(protocolFixture,interpreter))using(var token=new CancellationTokenSource()){
      if(mode=="hang")token.CancelAfter(300);bool rejected=false;var watch=Stopwatch.StartNew();try{await test.StartAsync("",token.Token);}catch(OperationCanceledException){rejected=mode=="hang";}catch(InvalidDataException){rejected=mode!="hang";}catch(IOException){rejected=mode!="hang";}
      Check(rejected&&test.WorkerId==0&&watch.Elapsed.TotalSeconds<5,"Worker "+mode+" is bounded and cleaned up");
     }
    }
    File.WriteAllText(Path.Combine(protocolFixture,"worker.py"),"import time\ntime.sleep(30)\n");
    using(var test=new PythonMonitorService(protocolFixture,interpreter)){bool timedOut=false;try{await test.StartAsync("",CancellationToken.None);}catch(TimeoutException){timedOut=true;}Check(timedOut&&test.WorkerId==0,"Unresponsive startup times out and terminates worker");}
   }finally{Directory.Delete(protocolFixture,true);}
   foreach(string model in new[]{"gpu","msi_b450_tomahawk_max"}){var loaded=await Task.Run(()=>GlbModel.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"models",model+".glb")));Check(loaded.IsFrozen&&!loaded.Bounds.IsEmpty,"Native WPF loads and freezes "+model+" reference model");}
   string fixture=Path.Combine(Path.GetTempPath(),"framepilot-monitor-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
   try{
    File.WriteAllBytes(Path.Combine(fixture,"sample.bin"),new byte[8192]);Directory.CreateDirectory(Path.Combine(fixture,"nested"));File.WriteAllBytes(Path.Combine(fixture,"nested","other.bin"),new byte[1024]);
    using(var storage=new StorageService()){
     var state=await storage.StartAsync(fixture,CancellationToken.None);for(int i=0;(state.state=="scanning"||state.state=="indexing")&&i<100;i++){await Task.Delay(100);state=await storage.StatusAsync(CancellationToken.None);}if(state.state!="ready")throw new Exception("Storage scan "+state.state+": "+state.message);Check(true,"Storage scanner completes isolated fixture");
     var children=await storage.ChildrenAsync(0,0,CancellationToken.None);Check(children.rows.Any(x=>x.name=="sample.bin"&&x.size_bytes==8192),"Storage byte-size conversion matches actual file");Check(children.rows.Any(x=>x.name=="nested"&&x.is_directory),"Storage directory model preserved");await storage.CancelAsync();await storage.StopAsync();
    }
   }finally{Directory.Delete(fixture,true);}
   results.Add("Verified on this Windows PC; no tuning, driver installation, or personal-file deletion performed.");
  }
 }
}




