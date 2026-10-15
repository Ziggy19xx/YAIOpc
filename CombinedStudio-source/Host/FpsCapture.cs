using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
namespace CombinedStudio;
internal sealed record FpsReading(int Pid,string Name,string Swapchain,double Fps,double FrameMs,double Low1,int Frames);
internal sealed class FpsFrames {
 readonly Func<long> clock;internal FpsFrames(Func<long> clock=null){this.clock=clock??(()=>Environment.TickCount64);}readonly object gate=new();readonly Dictionary<string,Stream> streams=new();Dictionary<string,int> columns;
 sealed class Stream {internal int Pid;internal string Name,Chain;internal double LastTime;internal long Seen;internal readonly Queue<(double Time,double Ms)> Frames=new();}
 internal static string[] Csv(string line){var fields=new List<string>();var value=new StringBuilder();bool quoted=false;for(int i=0;i<line.Length;i++){char c=line[i];if(c=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){value.Append('"');i++;}else quoted=!quoted;}else if(c==','&&!quoted){fields.Add(value.ToString());value.Clear();}else value.Append(c);}fields.Add(value.ToString());return fields.ToArray();}
 internal void Clear(){lock(gate){streams.Clear();columns=null;}}
 internal bool Feed(string line){var row=Csv(line);lock(gate){if(row.Any(x=>x.Equals("ProcessID",StringComparison.OrdinalIgnoreCase))&&row.Any(x=>x.Equals("MsBetweenPresents",StringComparison.OrdinalIgnoreCase))){columns=row.Select((x,i)=>(x,i)).ToDictionary(x=>x.x.Trim(),x=>x.i,StringComparer.OrdinalIgnoreCase);return false;}if(columns==null)return false;
  string Get(string key)=>columns.TryGetValue(key,out int i)&&i<row.Length?row[i]:"";
  if(!int.TryParse(Get("ProcessID"),out int pid)||pid<=0||!double.TryParse(Get("MsBetweenPresents"),NumberStyles.Float,CultureInfo.InvariantCulture,out double ms)||!double.IsFinite(ms)||ms<=0||ms>10000||!double.TryParse(Get("TimeInSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out double time)||!double.IsFinite(time))return false;
  string chain=Get("SwapChainAddress"),key=pid+":"+chain;long now=clock();
  foreach(var old in streams.Where(x=>now-x.Value.Seen>10000).Select(x=>x.Key).ToArray())streams.Remove(old);
  if(!streams.TryGetValue(key,out var stream)){if(streams.Count>=256)return false;streams[key]=stream=new(){Pid=pid,Name=Get("Application"),Chain=chain};}
  if(stream.Frames.Count>0&&time<stream.LastTime)stream.Frames.Clear();stream.LastTime=time;stream.Seen=now;stream.Frames.Enqueue((time,ms));while(stream.Frames.Count>4096||stream.Frames.Count>0&&time-stream.Frames.Peek().Time>5)stream.Frames.Dequeue();return true;
 }}
 internal FpsReading Reading(int pid)=>Reading(new HashSet<int>{pid});
 internal FpsReading Reading(ISet<int> pids){lock(gate){long now=clock();var stream=streams.Values.Where(x=>pids.Contains(x.Pid)&&now-x.Seen<2500&&x.Frames.Count>1).OrderByDescending(x=>x.Frames.Count(f=>x.LastTime-f.Time<=1)).FirstOrDefault();if(stream==null)return null;var recent=stream.Frames.Where(x=>stream.LastTime-x.Time<=1).Select(x=>x.Ms).ToArray();if(recent.Length<2)return null;double mean=recent.Average();var all=stream.Frames.Select(x=>x.Ms).OrderByDescending(x=>x).ToArray();double slow=all.Take(Math.Max(1,(int)Math.Ceiling(all.Length*.01))).Average();return new(stream.Pid,stream.Name,stream.Chain,1000/mean,mean,1000/slow,all.Length);}}
}
internal sealed class FpsCapture:IDisposable {
 long lines,accepted;string firstLine="";internal string Diagnostics=>"Stream: "+Interlocked.Read(ref lines)+" rows · "+Interlocked.Read(ref accepted)+" valid frames · "+firstLine;
 internal readonly FpsFrames Frames=new();internal int WorkerId=>worker?.Id??0;NamedPipeServerStream pipe;StreamWriter control;Process worker;CancellationTokenSource cancellation;Task reader;int generation;internal bool Running {get;private set;}internal string Status {get;private set;}="Stopped";
 internal async Task Start(bool elevated){if(Running)return;Stop();Frames.Clear();lines=accepted=0;firstLine="";string name="YAIOpc-Fps-"+Guid.NewGuid().ToString("N");pipe=new(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);cancellation=new();var lifetime=cancellation;var server=pipe;var startupToken=lifetime.Token;int startupGeneration=generation;
  try{Status=elevated?"Waiting for administrator approval…":"Starting frame capture…";var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=elevated,CreateNoWindow=!elevated,WindowStyle=ProcessWindowStyle.Hidden};if(elevated){info.Verb="runas";info.Arguments="--fps-worker "+name;}else{info.ArgumentList.Add("--fps-worker");info.ArgumentList.Add(name);}worker=await Task.Run(()=>Process.Start(info))??throw new IOException("Could not start frame capture helper.");startupToken.ThrowIfCancellationRequested();if(generation!=startupGeneration)throw new OperationCanceledException();lifetime.CancelAfter(TimeSpan.FromSeconds(25));await server.WaitForConnectionAsync(startupToken);startupToken.ThrowIfCancellationRequested();lifetime.CancelAfter(Timeout.InfiniteTimeSpan);control=new StreamWriter(server,new UTF8Encoding(false),1024,true){AutoFlush=true};Running=true;Status="Capture active for all applications.";var connection=server;var token=startupToken;int id=generation;reader=Task.Run(()=>Read(connection,token,id));
  }catch{if(generation==startupGeneration)Stop();throw;}
 }
 async Task Read(Stream stream,CancellationToken token,int id){try{using var input=new StreamReader(stream,Encoding.UTF8,false,4096,true);while(!token.IsCancellationRequested){string line=await input.ReadLineAsync(token);if(line==null||id!=generation||token.IsCancellationRequested)break;if(line.StartsWith("#ERROR ")){Status=line.Substring(7);break;}if(!line.StartsWith("#")){if(Interlocked.Increment(ref lines)==1)firstLine=line.Length>180?line.Substring(0,180):line;if(Frames.Feed(line))Interlocked.Increment(ref accepted);}}}catch(OperationCanceledException){}catch(ObjectDisposedException){}catch(IOException ex){if(id==generation)Status="Capture disconnected: "+ex.Message;}finally{if(id==generation){Running=false;if(!token.IsCancellationRequested&&Status.StartsWith("Capture active"))Status="Frame capture ended. Start again to retry.";}}}
 internal void Stop(){generation++;try{control?.WriteLine("STOP");}catch{}cancellation?.Cancel();cancellation?.Dispose();cancellation=null;try{control?.Dispose();}catch{}control=null;pipe?.Dispose();pipe=null;worker?.Dispose();worker=null;Running=false;Frames.Clear();Status="Stopped";}
 internal static int Probe(string folder){Directory.CreateDirectory(folder);using var capture=new FpsCapture();try{capture.Start(false).GetAwaiter().GetResult();Thread.Sleep(6000);File.WriteAllText(Path.Combine(folder,"stream-diagnostic.txt"),capture.Status+"\n"+capture.Diagnostics);return 0;}catch(Exception ex){File.WriteAllText(Path.Combine(folder,"stream-diagnostic.txt"),ex+"\n"+capture.Diagnostics);return 1;}}
 public void Dispose()=>Stop();
 internal static int Worker(string pipeName){if(!pipeName.StartsWith("YAIOpc-Fps-")||pipeName.Length>80)return 2;try{return WorkerAsync(pipeName).GetAwaiter().GetResult();}catch{return 1;}}
 static async Task<int> WorkerAsync(string pipeName){
  using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous);await pipe.ConnectAsync(15000);using var output=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true};using var input=new StreamReader(pipe,Encoding.UTF8,false,1024,true);string exe=Path.Combine(AppContext.BaseDirectory,"fps","PresentMon.exe");string session="YAIOpc-FPS-"+Guid.NewGuid().ToString("N");using var job=new FramePilot.Monitoring.OwnedJob();using var process=new Process();
  try{
   if(!File.Exists(exe))throw new FileNotFoundException("The bundled FPS engine is missing. Keep the entire app folder together.",exe);
   process.StartInfo=new(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string arg in new[]{"--output_stdout","--no_console_stats","--v1_metrics","--session_name",session})process.StartInfo.ArgumentList.Add(arg);
   process.Start();job.Assign(process);var errors=process.StandardError.ReadToEndAsync();
   async Task Forward(){while(await process.StandardOutput.ReadLineAsync() is string line)await output.WriteLineAsync(line);}
   var forward=Forward();var stop=input.ReadLineAsync();await Task.WhenAny(forward,stop);
   if(forward.IsCompleted){await process.WaitForExitAsync();string error=await errors;await output.WriteLineAsync("#ERROR Frame capture stopped ("+process.ExitCode+"). "+(error.Contains("access denied",StringComparison.OrdinalIgnoreCase)?"Use Start with admin access to allow Windows frame tracing.":error.Replace('\r',' ').Replace('\n',' ').Substring(0,Math.Min(600,error.Length))));}
   return 0;
  }catch(Exception ex){try{await output.WriteLineAsync("#ERROR "+ex.Message);}catch{}return 1;}
  finally{
   try{var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true};foreach(string arg in new[]{"--session_name",session,"--terminate_existing_session"})info.ArgumentList.Add(arg);using var closer=Process.Start(info);if(closer!=null)await Task.WhenAny(closer.WaitForExitAsync(),Task.Delay(1500));}catch{}try{if(process.Id>0&&!process.HasExited)process.Kill();}catch{}
  }
 }
}