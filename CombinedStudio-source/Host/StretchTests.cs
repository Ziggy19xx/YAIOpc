namespace CombinedStudio;
internal static class StretchTests {
 sealed class Display:IStretchDisplay {
  internal StretchResolution Mode=new(2560,1440);internal bool Reject;internal int Calls;internal uint? Scaling;internal bool RejectScaling;public uint? ReadScaling(string device)=>Scaling;public void SetScaling(string device,uint scaling){if(RejectScaling)throw new InvalidOperationException("Rejected scaling");if(Scaling.HasValue)Scaling=scaling;}
  public (string Device,StretchResolution Resolution,uint Frequency,uint Bits) Current()=>("test",Mode,165,32);
  public void Apply(string device,StretchResolution resolution,uint frequency,uint bits){Calls++;if(Reject)throw new InvalidOperationException("Rejected fixture mode");if(device!="test")throw new Exception("Lost original device");Mode=resolution;}
 }
 internal static int Run(string folder){Directory.CreateDirectory(folder);var log=new List<string>();void Check(bool condition,string caption){if(!condition)throw new Exception(caption);log.Add("PASS "+caption);}void Reject(Action action,string caption){try{action();}catch(ArgumentException){log.Add("PASS "+caption);return;}throw new Exception(caption);}
  try{
   Check(System.Runtime.InteropServices.Marshal.SizeOf<WindowsStretchDisplay.Mode>()==220,"Windows DEVMODEW layout");
   Check((StretchGameWindow.BorderlessStyle(unchecked((int)0x96CF0000))&unchecked((int)0x80C40000))==0,"borderless fit removes popup and window frame styles");
   using(var testWindow=new System.Windows.Forms.Form{ShowInTaskbar=false,Opacity=0,StartPosition=System.Windows.Forms.FormStartPosition.Manual,Bounds=new System.Drawing.Rectangle(120,120,640,480)}){
    var testHandle=testWindow.Handle;CrosshairOverlay.GetWindowRect(testHandle,out var before);using var fit=new StretchGameWindow();
    var screen=System.Windows.Forms.Screen.FromHandle(testHandle);fit.FitWindow(testHandle,(uint)System.Environment.ProcessId,screen.Bounds);
    CrosshairOverlay.GetWindowRect(testHandle,out var after);Check(after.Left==screen.Bounds.Left&&after.Top==screen.Bounds.Top&&after.Right-after.Left==screen.Bounds.Width&&after.Bottom-after.Top==screen.Bounds.Height,"native borderless window fills the display and verifies client area");
    fit.Restore();CrosshairOverlay.GetWindowRect(testHandle,out var restored);Check(restored.Left==before.Left&&restored.Top==before.Top&&restored.Right==before.Right&&restored.Bottom==before.Bottom,"undo restores native window bounds");
   }
   Check(StretchResolution.Parse(" 1280 × 1024 ")==new StretchResolution(1280,1024),"resolution normalization");foreach(string value in new[]{"0x1080","1920","1920x1080junk","99999x1080","abc"})Reject(()=>StretchResolution.Parse(value),"reject invalid input "+value);
   var paths=new byte[72];var modes=new byte[128];var preferred=new byte[80];BitConverter.GetBytes(0u).CopyTo(paths,12);BitConverter.GetBytes(1u).CopyTo(paths,32);BitConverter.GetBytes(1066u).CopyTo(modes,16);BitConverter.GetBytes(1332u).CopyTo(modes,20);BitConverter.GetBytes(2368u).CopyTo(preferred,56);BitConverter.GetBytes(1332u).CopyTo(preferred,60);CrosshairDisplay.UsePreferredTarget(paths,0,modes,preferred);Check(BitConverter.ToUInt32(modes,16)==1066&&BitConverter.ToUInt32(modes,104)==2368&&CrosshairDisplay.NeedsStretch(paths,0,modes),"native output timing preserves selected desktop resolution and requires stretch");
   var display=new Display();string path=Path.Combine(folder,"fixture-state.json");if(File.Exists(path))File.Delete(path);var controller=new StretchController(display,path);
   controller.Apply(display.Mode);Check(display.Calls==0&&!controller.State.Original.HasValue,"same mode does not start a session");
   display.Reject=true;try{controller.Apply(new(1440,1080));}catch(InvalidOperationException){}Check(!controller.State.Original.HasValue,"rejected first switch clears recovery state");display.Reject=false;
   controller.Apply(new(1440,1080));controller.Apply(new(1280,1024));Check(controller.State.Original==new StretchResolution(2560,1440),"repeated switches preserve original mode");
   controller=new StretchController(display,path);Check(controller.State.Original==new StretchResolution(2560,1440)&&controller.State.Frequency==165,"original display survives restart");
   controller.AddPreset("1280X1024");controller.AddPreset("1280 × 1024");Check(controller.State.Presets.Count==1,"saved presets normalize and deduplicate");
   Check(!controller.ObserveGame(false)&&display.Mode==new StretchResolution(1280,1024),"no restore before observing game");controller.State.AutoRestore=false;controller.ObserveGame(true);Check(!controller.ObserveGame(false),"auto restore preference respected");controller.State.AutoRestore=true;Check(controller.ObserveGame(false)&&display.Mode==new StretchResolution(2560,1440),"game exit restores original mode");
   controller.Apply(new(1440,1080));display.Reject=true;try{controller.Restore();}catch(InvalidOperationException){}Check(controller.State.Original.HasValue,"failed restore retains recovery state");display.Reject=false;controller.Restore();Check(!new StretchController(display,path).State.Original.HasValue,"successful restore clears persistent session");
   display.Scaling=4;controller.Apply(new(1332,1332));Check(display.Scaling==3&&controller.State.OriginalScaling==4,"stretch applies full-panel scaling and remembers previous scaling");controller=new StretchController(display,path);controller.Restore();Check(display.Scaling==4,"restore returns previous scaling after restart");display.RejectScaling=true;try{controller.Apply(new(1776,1332));}catch(InvalidOperationException){}Check(controller.State.Original.HasValue&&display.Mode==new StretchResolution(1776,1332),"scaling failure preserves recovery after resolution changed");display.RejectScaling=false;controller.Restore();controller.Apply(display.Mode);Check(display.Scaling==3&&controller.State.Original.HasValue,"fix scaling works without changing current resolution");controller.Restore();
   var live=new WindowsStretchDisplay().Current();Check(live.Resolution.Width>0&&live.Resolution.Height>0,"read actual primary display without changing settings");
   File.WriteAllLines(Path.Combine(folder,"stretch-tests.txt"),log);return 0;
  }catch(Exception error){File.WriteAllLines(Path.Combine(folder,"stretch-tests.txt"),log.Append(error.ToString()));return 1;}
 }
}
