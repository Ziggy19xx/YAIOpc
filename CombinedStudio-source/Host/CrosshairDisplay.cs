using System.Runtime.InteropServices;
namespace CombinedStudio;
internal static class CrosshairDisplay {
 [DllImport("user32.dll")]static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
 [DllImport("user32.dll")]static extern int QueryDisplayConfig(uint flags,ref uint paths,[Out] byte[] pathData,ref uint modes,[Out] byte[] modeData,IntPtr topology);
 [DllImport("user32.dll")]static extern int DisplayConfigGetDeviceInfo([In,Out] byte[] packet);
 [DllImport("user32.dll")]static extern int SetDisplayConfig(uint paths,byte[] pathData,uint modes,byte[] modeData,uint flags);
 internal static uint Scaling(string device,uint? requested=null){
  for(int retry=0;retry<3;retry++){
   int result=GetDisplayConfigBufferSizes(2,out uint paths,out uint modes);if(result!=0||paths>128||modes>512)throw new InvalidOperationException("Windows could not read display scaling.");
   var data=new byte[paths*72];var modeData=new byte[modes*64];result=QueryDisplayConfig(2,ref paths,data,ref modes,modeData,IntPtr.Zero);if(result==122)continue;if(result!=0)throw new InvalidOperationException("Windows could not read the active display paths.");
   for(int i=0;i<paths;i++){int offset=i*72;var source=Packet(1,84,data,offset);if(DisplayConfigGetDeviceInfo(source)!=0||!System.Text.Encoding.Unicode.GetString(source,20,64).TrimEnd('\0').Equals(device,StringComparison.OrdinalIgnoreCase))continue;
    uint previous=BitConverter.ToUInt32(data,offset+44);if(!requested.HasValue)return previous;
    BitConverter.GetBytes(requested.Value).CopyTo(data,offset+44);
    if(requested==3){var preferred=Packet(3,80,data,offset+20);if(DisplayConfigGetDeviceInfo(preferred)==0)UsePreferredTarget(data,offset,modeData,preferred);}
    result=SetDisplayConfig(paths,data,modes,modeData,0x20|0x80|0x100);
    if(result!=0)throw new InvalidOperationException($"Windows rejected full-panel scaling (code {result}). Select Full-screen / Full panel in your GPU settings, then retry. Your resolution can be restored with Restore original.");
    if(requested==3&&NeedsStretch(data,offset,modeData)&&Scaling(device)!=3)throw new InvalidOperationException("The GPU accepted the request but did not enable stretched scaling. The game may also be adding its own letterbox. Use Fit VALORANT window below; if bars remain inside that window, this driver/game combination needs its GPU Full-screen scaling setting rather than another resolution switch.");
    return previous;
   }throw new InvalidOperationException("The selected display is no longer connected.");
  }throw new InvalidOperationException("The display changed while reading scaling. Retry.");
 }
 internal static bool NeedsStretch(byte[] paths,int offset,byte[] modes){uint source=BitConverter.ToUInt32(paths,offset+12),target=BitConverter.ToUInt32(paths,offset+32);if(source>=modes.Length/64||target>=modes.Length/64)return false;return BitConverter.ToUInt32(modes,(int)source*64+16)!=BitConverter.ToUInt32(modes,(int)target*64+40)||BitConverter.ToUInt32(modes,(int)source*64+20)!=BitConverter.ToUInt32(modes,(int)target*64+44);}
 internal static void UsePreferredTarget(byte[] paths,int offset,byte[] modes,byte[] preferred){
  uint target=BitConverter.ToUInt32(paths,offset+32);if(target==uint.MaxValue||target>=modes.Length/64)return;
  // Retain the desktop/source resolution but transmit the monitor's native timing.
  // Changing only scaling while source and target are equal is silently normalized to identity.
  Array.Copy(preferred,32,modes,(int)target*64+16,48);
 }
 // Win32 CCD layouts: PATH_INFO=72, MODE_INFO=64, SOURCE_DEVICE_NAME=84,
 // TARGET_PREFERRED_MODE=80 (signal info starts at its 8-byte aligned offset 32).
 internal static System.Drawing.Size Preferred(string device){
  for(int retry=0;retry<3;retry++){
   if(GetDisplayConfigBufferSizes(2,out uint paths,out uint modes)!=0||paths>128||modes>512)return default;
   var data=new byte[paths*72];var modeData=new byte[modes*64];int result=QueryDisplayConfig(2,ref paths,data,ref modes,modeData,IntPtr.Zero);if(result==122)continue;if(result!=0)return default;
   for(int i=0;i<paths;i++){
    int offset=i*72;var source=Packet(1,84,data,offset);
    if(DisplayConfigGetDeviceInfo(source)!=0||!System.Text.Encoding.Unicode.GetString(source,20,64).TrimEnd('\0').Equals(device,StringComparison.OrdinalIgnoreCase))continue;
    var target=Packet(3,80,data,offset+20);if(DisplayConfigGetDeviceInfo(target)!=0)return default;
    int width=BitConverter.ToInt32(target,20),height=BitConverter.ToInt32(target,24);uint rotation=BitConverter.ToUInt32(data,offset+40);
    if(rotation==2||rotation==4)(width,height)=(height,width);
    return width>0&&height>0?new(width,height):default;
   }
  }return default;
 }
 static byte[] Packet(int type,int size,byte[] path,int offset){var packet=new byte[size];BitConverter.GetBytes(type).CopyTo(packet,0);BitConverter.GetBytes(size).CopyTo(packet,4);Array.Copy(path,offset,packet,8,12);return packet;}
 internal static (double X,double Y) Compensation(System.Drawing.Size current,System.Drawing.Size native)=>native.Width>0&&native.Height>0?((double)current.Width/native.Width,(double)current.Height/native.Height):(1,1);
}
