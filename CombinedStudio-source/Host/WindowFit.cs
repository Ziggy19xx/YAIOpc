using System.Runtime.InteropServices;
using W=System.Windows;
using H=System.Windows.Interop;
namespace CombinedStudio;
internal static class WindowFit {
 [StructLayout(LayoutKind.Sequential)]struct Point {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")]static extern bool GetCursorPos(out Point point);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromPoint(Point point,uint flags);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
 [DllImport("user32.dll")]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("shcore.dll")]static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 internal static W.Rect Bounds(W.Rect work,double dpiX,double dpiY,double desiredWidth=1480,double desiredHeight=1020){
  double margin=Math.Min(16,Math.Min(work.Width,work.Height)/20);
  double width=Math.Min(desiredWidth*dpiX,Math.Max(1,work.Width-margin*2));
  double height=Math.Min(desiredHeight*dpiY,Math.Max(1,work.Height-margin*2));
  return new W.Rect(Math.Round(work.Left+(work.Width-width)/2),Math.Round(work.Top+(work.Height-height)/2),Math.Floor(width),Math.Floor(height));
 }
 internal static void Attach(W.Window window){
  window.WindowStartupLocation=W.WindowStartupLocation.Manual;window.WindowState=W.WindowState.Normal;
  window.SourceInitialized+=(_,_)=>{
   var handle=new H.WindowInteropHelper(window).Handle;
   void Fit(bool opening){
    if(window.WindowState!=W.WindowState.Normal)return;
    IntPtr monitor=opening&&GetCursorPos(out var pointer)?MonitorFromPoint(pointer,2):MonitorFromWindow(handle,2);
    var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(!GetMonitorInfo(monitor,ref info))return;
    double dpiX=1,dpiY=1;if(GetDpiForMonitor(monitor,0,out uint dx,out uint dy)==0){dpiX=dx/96d;dpiY=dy/96d;}
    var work=new W.Rect(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
    var bounds=Bounds(work,dpiX,dpiY,opening?1480:window.Width,opening?1020:window.Height);
    // Minimum sizes must also fit smaller and scaled monitors.
    window.MinWidth=Math.Min(1060,bounds.Width/dpiX);window.MinHeight=Math.Min(700,bounds.Height/dpiY);
    window.Width=bounds.Width/dpiX;window.Height=bounds.Height/dpiY;
    window.Left=bounds.Left/dpiX;window.Top=bounds.Top/dpiY;
    SetWindowPos(handle,IntPtr.Zero,(int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height,0x4|0x10);
   }
   Fit(true);
   H.HwndSource.FromHwnd(handle)?.AddHook((IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)=>{
    if(message==0x007E||message==0x02E0)window.Dispatcher.BeginInvoke(new Action(()=>Fit(false)));
    return IntPtr.Zero;
   });
  };
 }
 internal static int Test(string folder){Directory.CreateDirectory(folder);var log=new List<string>();
  foreach(var work in new[]{new W.Rect(0,0,1920,1040),new W.Rect(0,0,1066,1292),new W.Rect(-1280,0,1280,984),new W.Rect(0,-720,1280,680),new W.Rect(0,0,800,560)})foreach(double dpi in new[]{1d,1.25,1.5,2}){
   var bounds=Bounds(work,dpi,dpi);if(!work.Contains(bounds))throw new Exception("Window exceeds monitor work area.");
   if(bounds.Width/dpi>1480||bounds.Height/dpi>1020)throw new Exception("Window exceeds preferred size.");
   log.Add("PASS fitting "+work+" at DPI scale "+dpi);
  }
  var application=new W.Application();var probe=new W.Window{Opacity=0,ShowActivated=false,ShowInTaskbar=false,Width=1480,Height=1020,MinWidth=1060,MinHeight=700};
  try{Attach(probe);probe.Show();probe.UpdateLayout();var handle=new H.WindowInteropHelper(probe).Handle;var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
   if(!GetMonitorInfo(MonitorFromWindow(handle,2),ref info)||!CrosshairOverlay.GetWindowRect(handle,out var rect)||rect.Left<info.Work.Left||rect.Top<info.Work.Top||rect.Right>info.Work.Right||rect.Bottom>info.Work.Bottom)throw new Exception("Live window exceeds monitor work area.");
   log.Add("PASS native startup window fits current monitor work area");
  }finally{probe.Close();application.Shutdown();}
  File.WriteAllLines(Path.Combine(folder,"window-fit-tests.txt"),log);return 0;
 }
}
