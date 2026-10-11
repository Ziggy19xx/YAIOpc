using Microsoft.Win32;
namespace CombinedStudio;
internal static class WindowsStartup {
 const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
 const string ValueName="FramePilotVelvetStudio";
 internal static string Command=>"\""+Environment.ProcessPath+"\" --startup";
 internal static bool Enabled {get {using var key=Registry.CurrentUser.OpenSubKey(KeyPath);return string.Equals(key?.GetValue(ValueName) as string,Command,StringComparison.OrdinalIgnoreCase);}}
 internal static void SetEnabled(bool enabled){using var key=Registry.CurrentUser.CreateSubKey(KeyPath);if(enabled)key.SetValue(ValueName,Command);else key.DeleteValue(ValueName,false);}
}
