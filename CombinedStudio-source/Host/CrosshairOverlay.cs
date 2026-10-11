using System.Runtime.InteropServices;
using W=System.Windows;
using M=System.Windows.Media;
using H=System.Windows.Interop;
using F=System.Windows.Forms;
namespace CombinedStudio;
internal sealed class CrosshairOverlay:IDisposable {
 readonly W.Window window;readonly CrosshairView view;readonly CrosshairSettings settings;
 readonly System.Windows.Threading.DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(100)};
 internal bool Enabled {get;private set;}internal bool Visible=>window.IsVisible;internal IntPtr Handle=>new H.WindowInteropHelper(window).Handle;
 internal event Action Changed;
 [StructLayout(LayoutKind.Sequential)]internal struct Rect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
 [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]internal static extern int GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr window,int index,int value);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 internal CrosshairOverlay(CrosshairSettings settings,string imageFolder){this.settings=settings;view=new(){Design=settings.Design,ImageFolder=imageFolder,PhysicalPixels=true,IsHitTestVisible=false};
  window=new(){Title="YAIOpc Crosshair overlay",Width=320,Height=320,WindowStyle=W.WindowStyle.None,AllowsTransparency=true,Background=M.Brushes.Transparent,Topmost=true,ShowInTaskbar=false,ShowActivated=false,ResizeMode=W.ResizeMode.NoResize,Focusable=false,IsHitTestVisible=false,Content=view};
  window.SourceInitialized+=(_,_)=>{int styles=GetWindowLong(Handle,-20);SetWindowLong(Handle,-20,styles|0x20|0x80|0x80000|0x8000000);if(H.HwndSource.FromHwnd(Handle) is H.HwndSource source)source.AddHook(Hook);};
  timer.Tick+=(_,_)=>{if(!Enabled)return;bool hide=settings.HideOnAim&&(GetAsyncKeyState(2)&0x8000)!=0;if(hide){if(window.IsVisible)window.Hide();}else{if(!window.IsVisible)window.Show();Position();}};
 }
 IntPtr Hook(IntPtr handle,int message,IntPtr wParam,IntPtr lParam,ref bool handled){if(message==0x21){handled=true;return new IntPtr(3);}if(message==0x84){handled=true;return new IntPtr(-1);}if(message==0x02E0)view.InvalidateVisual();return IntPtr.Zero;}
 internal static System.Drawing.Point Center(System.Drawing.Rectangle bounds,int x,int y)=>new(bounds.Left+bounds.Width/2+x,bounds.Top+bounds.Height/2+y);
 internal void Position(){var screens=F.Screen.AllScreens;var screen=screens.FirstOrDefault(x=>x.DeviceName==settings.Monitor)??F.Screen.PrimaryScreen??screens.First();var center=Center(screen.Bounds,settings.OffsetX,settings.OffsetY);SetWindowPos(Handle,new IntPtr(-1),center.X-160,center.Y-160,320,320,0x10|0x40);}
 internal void Update(){view.Design=settings.Design;view.InvalidateVisual();if(Enabled&&window.IsVisible)Position();}
 internal void SetEnabled(bool enabled){Enabled=enabled;if(enabled){window.Show();Position();timer.Start();}else{timer.Stop();window.Hide();}Changed?.Invoke();}
 public void Dispose(){timer.Stop();Enabled=false;window.Close();}
}
internal sealed class CrosshairHotkey:IDisposable {
 readonly H.HwndSource source;readonly Action toggle;const int Id=0x5943;bool registered;
 [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
 [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr window,int id);
 internal CrosshairHotkey(W.Window owner,Action toggle){this.toggle=toggle;source=H.HwndSource.FromHwnd(new H.WindowInteropHelper(owner).EnsureHandle());source.AddHook(Hook);}
 internal bool Register(string key){if(registered)UnregisterHotKey(source.Handle,Id);uint code=key switch{"C"=>0x43,"F8"=>0x77,"F9"=>0x78,_=>0x58};registered=RegisterHotKey(source.Handle,Id,0x4000|1|2,code);return registered;}
 IntPtr Hook(IntPtr handle,int message,IntPtr wParam,IntPtr lParam,ref bool handled){if(message==0x312&&wParam.ToInt32()==Id){handled=true;toggle();}return IntPtr.Zero;}
 public void Dispose(){if(registered)UnregisterHotKey(source.Handle,Id);source.RemoveHook(Hook);}
}
