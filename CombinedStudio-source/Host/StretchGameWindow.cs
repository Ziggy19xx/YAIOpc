using System.Runtime.InteropServices;
namespace CombinedStudio;
internal sealed class StretchGameWindow:IDisposable {
 IntPtr handle;int originalStyle;CrosshairOverlay.Rect originalBounds;bool originalMaximized;
 [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll",SetLastError=true)]static extern int SetWindowLong(IntPtr window,int index,int value);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
 [DllImport("user32.dll")]static extern bool IsZoomed(IntPtr window);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
 [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr window,out CrosshairOverlay.Rect rect);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 uint processId;
 internal void Fit(){
  using var game=System.Diagnostics.Process.GetProcessesByName("VALORANT-Win64-Shipping").FirstOrDefault(x=>x.MainWindowHandle!=IntPtr.Zero)??throw new InvalidOperationException("VALORANT's game window was not found. Start the game in Windowed mode, then click Fit VALORANT window.");
  FitWindow(game.MainWindowHandle,(uint)game.Id,System.Windows.Forms.Screen.FromHandle(game.MainWindowHandle).Bounds);
 }
 internal static int BorderlessStyle(int style)=>(style&~unchecked((int)0x80CF0000))|0x00030000;
 internal void FitWindow(IntPtr target,uint owner,System.Drawing.Rectangle bounds){
  if(handle!=target){Restore();handle=target;processId=owner;originalStyle=GetWindowLong(handle,-16);originalMaximized=IsZoomed(handle);if(!CrosshairOverlay.GetWindowRect(handle,out originalBounds)){handle=IntPtr.Zero;throw new InvalidOperationException("Could not read the game window bounds.");}}
  // Keep a windowed surface. WS_POPUP can make the game choose its fullscreen
  // aspect-ratio path and embed letterboxing even when the outer bounds fit.
  System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
  int previous=SetWindowLong(handle,-16,BorderlessStyle(originalStyle));
  if(previous==0&&System.Runtime.InteropServices.Marshal.GetLastPInvokeError()!=0)throw new InvalidOperationException("Windows could not update the game window style. Run YAIOpc with the same permissions as VALORANT.");
  ShowWindow(handle,3);
  if(!SetWindowPos(handle,IntPtr.Zero,bounds.Left,bounds.Top,bounds.Width,bounds.Height,0x4|0x10|0x20))throw new InvalidOperationException("Windows could not fit the game window. Run YAIOpc with the same permissions as VALORANT.");
  if(!GetClientRect(handle,out var client)||client.Right-client.Left!=bounds.Width||client.Bottom-client.Top!=bounds.Height)throw new InvalidOperationException("The game did not accept a full-display client area. Select Windowed mode in VALORANT and retry.");
 }
 internal void Restore(){if(handle==IntPtr.Zero)return;GetWindowThreadProcessId(handle,out uint owner);if(IsWindow(handle)&&owner==processId){ShowWindow(handle,9);SetWindowLong(handle,-16,originalStyle);SetWindowPos(handle,IntPtr.Zero,originalBounds.Left,originalBounds.Top,originalBounds.Right-originalBounds.Left,originalBounds.Bottom-originalBounds.Top,0x4|0x10|0x20);if(originalMaximized)ShowWindow(handle,3);}handle=IntPtr.Zero;}
 public void Dispose()=>Restore();
}
