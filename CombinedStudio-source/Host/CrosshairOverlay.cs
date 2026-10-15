using System.Runtime.InteropServices;
using W=System.Windows;
using M=System.Windows.Media;
using H=System.Windows.Interop;
using F=System.Windows.Forms;
namespace CombinedStudio;
internal sealed class CrosshairOverlay:IDisposable {
 string displayKey="";System.Drawing.Size nativeSize;readonly W.Window window;readonly CrosshairView view;readonly CrosshairSettings settings;
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
 internal CrosshairOverlay(CrosshairSettings settings,string imageFolder){this.settings=settings;view=new(){Design=settings.Design,ImageFolder=imageFolder,ScalePercent=settings.SizePercent,WidthPercent=settings.WidthPercent,HeightPercent=settings.HeightPercent,PhysicalPixels=true,IsHitTestVisible=false};
  window=new(){Title="YAIOpc Crosshair overlay",Width=320,Height=320,WindowStyle=W.WindowStyle.None,AllowsTransparency=true,Background=M.Brushes.Transparent,Topmost=true,ShowInTaskbar=false,ShowActivated=false,ResizeMode=W.ResizeMode.NoResize,Focusable=false,IsHitTestVisible=false,Content=view};
  window.SourceInitialized+=(_,_)=>{int styles=GetWindowLong(Handle,-20);SetWindowLong(Handle,-20,styles|0x20|0x80|0x80000|0x8000000);if(H.HwndSource.FromHwnd(Handle) is H.HwndSource source)source.AddHook(Hook);};
  timer.Tick+=(_,_)=>{if(!Enabled)return;bool hide=settings.HideOnAim&&(GetAsyncKeyState(2)&0x8000)!=0;if(hide){if(window.IsVisible)window.Hide();}else{if(!window.IsVisible)window.Show();Position();}};
 }
 IntPtr Hook(IntPtr handle,int message,IntPtr wParam,IntPtr lParam,ref bool handled){if(message==0x21){handled=true;return new IntPtr(3);}if(message==0x84){handled=true;return new IntPtr(-1);}if(message==0x007E){displayKey="";if(Enabled)Position();}if(message==0x02E0){displayKey="";view.InvalidateVisual();}return IntPtr.Zero;}
 internal static System.Drawing.Point Center(System.Drawing.Rectangle bounds,int x,int y)=>new(bounds.Left+bounds.Width/2+x,bounds.Top+bounds.Height/2+y);
 internal int Size=>Math.Clamp((int)Math.Ceiling(320*Math.Max(1,settings.SizePercent/100d*Math.Max(settings.WidthPercent,settings.HeightPercent)/100d)),320,2880);
 internal void Position(){var screens=F.Screen.AllScreens;var screen=screens.FirstOrDefault(x=>x.DeviceName==settings.Monitor)??F.Screen.PrimaryScreen??screens.First();string key=screen.DeviceName+screen.Bounds.ToString();if(displayKey!=key){nativeSize=CrosshairDisplay.Preferred(screen.DeviceName);displayKey=key;var compensation=CrosshairDisplay.Compensation(screen.Bounds.Size,nativeSize);view.ResolutionScaleX=compensation.X;view.ResolutionScaleY=compensation.Y;view.InvalidateVisual();}var compensationOffset=CrosshairDisplay.Compensation(screen.Bounds.Size,nativeSize);var center=Center(screen.Bounds,(int)Math.Round(settings.OffsetX*compensationOffset.X),(int)Math.Round(settings.OffsetY*compensationOffset.Y));SetWindowPos(Handle,new IntPtr(-1),center.X-Size/2,center.Y-Size/2,Size,Size,0x10|0x40);}
 internal void Update(){view.Design=settings.Design;view.ScalePercent=settings.SizePercent;view.WidthPercent=settings.WidthPercent;view.HeightPercent=settings.HeightPercent;view.InvalidateVisual();if(Enabled&&window.IsVisible)Position();}
 internal void SetEnabled(bool enabled){Enabled=enabled;if(enabled){window.Show();Position();timer.Start();}else{timer.Stop();window.Hide();}Changed?.Invoke();}
 public void Dispose(){timer.Stop();Enabled=false;window.Close();}
}
internal sealed class CrosshairHotkey:IDisposable {
 readonly H.HwndSource source;readonly Action toggle;readonly int Id;bool registered;string lastKey;uint lastModifiers;
 [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
 [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr window,int id);
 internal CrosshairHotkey(W.Window owner,Action toggle,int id=0x5943){Id=id;this.toggle=toggle;source=H.HwndSource.FromHwnd(new H.WindowInteropHelper(owner).EnsureHandle());source.AddHook(Hook);}
 internal static bool ValidKey(string name)=>Enum.TryParse<System.Windows.Input.Key>(name,out var key)&&((key>=System.Windows.Input.Key.A&&key<=System.Windows.Input.Key.Z)||(key>=System.Windows.Input.Key.D0&&key<=System.Windows.Input.Key.D9)||(key>=System.Windows.Input.Key.F1&&key<=System.Windows.Input.Key.F24)||(key>=System.Windows.Input.Key.NumPad0&&key<=System.Windows.Input.Key.Divide)||(key>=System.Windows.Input.Key.Oem1&&key<=System.Windows.Input.Key.OemBackslash)||new[]{System.Windows.Input.Key.Space,System.Windows.Input.Key.Insert,System.Windows.Input.Key.Delete,System.Windows.Input.Key.Home,System.Windows.Input.Key.End,System.Windows.Input.Key.PageUp,System.Windows.Input.Key.PageDown,System.Windows.Input.Key.Left,System.Windows.Input.Key.Right,System.Windows.Input.Key.Up,System.Windows.Input.Key.Down,System.Windows.Input.Key.Tab,System.Windows.Input.Key.Return,System.Windows.Input.Key.Back}.Contains(key));
 internal bool Register(string key,uint modifiers=3){if(!ValidKey(key)||modifiers>15)return false;bool hadPrevious=registered;string previousKey=lastKey;uint previousModifiers=lastModifiers;Unregister();uint code=(uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(Enum.Parse<System.Windows.Input.Key>(key));registered=RegisterHotKey(source.Handle,Id,0x4000|modifiers,code);if(registered){lastKey=key;lastModifiers=modifiers;return true;}if(hadPrevious){registered=RegisterHotKey(source.Handle,Id,0x4000|previousModifiers,(uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(Enum.Parse<System.Windows.Input.Key>(previousKey)));}return false;}
 internal void Unregister(){if(registered)UnregisterHotKey(source.Handle,Id);registered=false;}
 IntPtr Hook(IntPtr handle,int message,IntPtr wParam,IntPtr lParam,ref bool handled){if(message==0x312&&wParam.ToInt32()==Id){handled=true;toggle();}return IntPtr.Zero;}
 public void Dispose(){if(registered)UnregisterHotKey(source.Handle,Id);source.RemoveHook(Hook);}
}
