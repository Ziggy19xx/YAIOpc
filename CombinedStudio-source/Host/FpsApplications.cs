using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace CombinedStudio;
internal sealed class FpsApplications {
 internal sealed record Entry(int Pid,int Parent,string Name);
 readonly Dictionary<int,Entry> processes=new();long refreshed=-5000;
 internal (string Name,HashSet<int> Pids) Resolve(int foreground){
  if(Environment.TickCount64-refreshed>1000){refreshed=Environment.TickCount64;var entries=Snapshot();processes.Clear();foreach(var entry in entries)processes[entry.Pid]=entry;}
  return (processes.TryGetValue(foreground,out var app)?Path.GetFileNameWithoutExtension(app.Name):"Desktop",Family(foreground,processes.Values));
 }
 internal static HashSet<int> Family(int root,IEnumerable<Entry> entries){var result=new HashSet<int>();if(root<=0)return result;result.Add(root);var all=entries.ToArray();bool changed;do{changed=false;foreach(var entry in all)if(entry.Pid>0&&result.Contains(entry.Parent)&&result.Add(entry.Pid))changed=true;}while(changed);return result;}
 static IEnumerable<Entry> Snapshot(){var result=new List<Entry>();using var snapshot=CreateToolhelp32Snapshot(2,0);if(snapshot.IsInvalid)return result;var entry=new ProcessEntry{Size=(uint)Marshal.SizeOf<ProcessEntry>()};if(Process32First(snapshot,ref entry)){do{result.Add(new((int)entry.Pid,(int)entry.Parent,entry.Name));entry.Size=(uint)Marshal.SizeOf<ProcessEntry>();}while(Process32Next(snapshot,ref entry));}return result;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct ProcessEntry {internal uint Size,Usage,Pid;internal UIntPtr Heap;internal uint Module,Threads,Parent;internal int Priority;internal uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]internal string Name;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags,uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32First(SafeFileHandle handle,ref ProcessEntry entry);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool Process32Next(SafeFileHandle handle,ref ProcessEntry entry);
}