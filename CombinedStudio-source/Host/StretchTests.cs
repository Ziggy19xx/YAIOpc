namespace CombinedStudio;
internal static class StretchTests {
 sealed class Display:IStretchDisplay {
  internal StretchResolution Mode=new(2560,1440);internal bool Reject;internal int Calls;
  public (string Device,StretchResolution Resolution,uint Frequency,uint Bits) Current()=>("test",Mode,165,32);
  public void Apply(string device,StretchResolution resolution,uint frequency,uint bits){Calls++;if(Reject)throw new InvalidOperationException("Rejected fixture mode");if(device!="test")throw new Exception("Lost original device");Mode=resolution;}
 }
 internal static int Run(string folder){Directory.CreateDirectory(folder);var log=new List<string>();void Check(bool condition,string caption){if(!condition)throw new Exception(caption);log.Add("PASS "+caption);}void Reject(Action action,string caption){try{action();}catch(ArgumentException){log.Add("PASS "+caption);return;}throw new Exception(caption);}
  try{
   Check(System.Runtime.InteropServices.Marshal.SizeOf<WindowsStretchDisplay.Mode>()==220,"Windows DEVMODEW layout");
   Check(StretchResolution.Parse(" 1280 × 1024 ")==new StretchResolution(1280,1024),"resolution normalization");foreach(string value in new[]{"0x1080","1920","1920x1080junk","99999x1080","abc"})Reject(()=>StretchResolution.Parse(value),"reject invalid input "+value);
   var display=new Display();string path=Path.Combine(folder,"fixture-state.json");if(File.Exists(path))File.Delete(path);var controller=new StretchController(display,path);
   controller.Apply(display.Mode);Check(display.Calls==0&&!controller.State.Original.HasValue,"same mode does not start a session");
   display.Reject=true;try{controller.Apply(new(1440,1080));}catch(InvalidOperationException){}Check(!controller.State.Original.HasValue,"rejected first switch clears recovery state");display.Reject=false;
   controller.Apply(new(1440,1080));controller.Apply(new(1280,1024));Check(controller.State.Original==new StretchResolution(2560,1440),"repeated switches preserve original mode");
   controller=new StretchController(display,path);Check(controller.State.Original==new StretchResolution(2560,1440)&&controller.State.Frequency==165,"original display survives restart");
   controller.AddPreset("1280X1024");controller.AddPreset("1280 × 1024");Check(controller.State.Presets.Count==1,"saved presets normalize and deduplicate");
   Check(!controller.ObserveGame(false)&&display.Mode==new StretchResolution(1280,1024),"no restore before observing game");controller.State.AutoRestore=false;controller.ObserveGame(true);Check(!controller.ObserveGame(false),"auto restore preference respected");controller.State.AutoRestore=true;Check(controller.ObserveGame(false)&&display.Mode==new StretchResolution(2560,1440),"game exit restores original mode");
   controller.Apply(new(1440,1080));display.Reject=true;try{controller.Restore();}catch(InvalidOperationException){}Check(controller.State.Original.HasValue,"failed restore retains recovery state");display.Reject=false;controller.Restore();Check(!new StretchController(display,path).State.Original.HasValue,"successful restore clears persistent session");
   var live=new WindowsStretchDisplay().Current();Check(live.Resolution.Width>0&&live.Resolution.Height>0,"read actual primary display without changing settings");
   File.WriteAllLines(Path.Combine(folder,"stretch-tests.txt"),log);return 0;
  }catch(Exception error){File.WriteAllLines(Path.Combine(folder,"stretch-tests.txt"),log.Append(error.ToString()));return 1;}
 }
}
