using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace FramePilot {
 public sealed class Tweak {
  public string Id,Name,Category,Description,Path,ValueName,Destination,Source;public int Value;public string Mode;
  public bool Direct {get{return Mode=="Direct";}}
  public string Read(){if(!Direct)return Mode=="Excluded"?"Excluded by design":"Open supported settings";try{if(Id=="balanced"||Id=="high-power")return Journal.Power()==Destination?"Already selected":"Available; reversible";using(var key=Registry.CurrentUser.OpenSubKey(Path)){if(key==null||!key.GetValueNames().Contains(ValueName))return "Windows default / not set";if(key.GetValueKind(ValueName)!=RegistryValueKind.DWord)return "Unexpected type; blocked";return (int)key.GetValue(ValueName)==Value?"Already selected":"Current value: "+key.GetValue(ValueName);}}catch(Exception e){return "Unavailable: "+e.Message;}}
  public void Apply(Journal journal){if(!Direct)throw new Exception("This entry is not an automatic tweak.");if(Id=="balanced"||Id=="high-power")journal.SetPower(Destination);else journal.RegistryDword(Path,ValueName,Value);}
  public static List<Tweak> All(){var a=new List<Tweak>{
   new Tweak{Id="game-mode",Name="Windows Game Mode",Category="Windows",Mode="Direct",Path=@"Software\Microsoft\GameBar",ValueName="AutoGameModeEnabled",Value=1,Description="Enable the per-user Game Mode preference. Verify in Windows Settings; benefit depends on competing workloads."},
   new Tweak{Id="dvr",Name="Game DVR recording",Category="Windows",Mode="Direct",Path=@"System\GameConfigStore",ValueName="GameDVR_Enabled",Value=0,Description="Disable Windows game recording for this account. Pair with App capture. Existing clips are kept."},
   new Tweak{Id="capture",Name="App capture",Category="Windows",Mode="Direct",Path=@"Software\Microsoft\Windows\CurrentVersion\GameDVR",ValueName="AppCaptureEnabled",Value=0,Description="Disable the companion capture preference. Recording features may stop working; restart the game to verify."},
   new Tweak{Id="transparency",Name="Desktop transparency",Category="Windows",Mode="Direct",Path=@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",ValueName="EnableTransparency",Value=0,Description="Turn off desktop transparency. Cosmetic preference; no promised game FPS gain. Sign out/in if the shell retains its old appearance."},
   new Tweak{Id="balanced",Name="Balanced power plan",Category="Power",Mode="Direct",Destination="381b4222-f694-41f0-9685-ff5bb260df2e",Description="Existing Windows Balanced plan. A sensible starting point for X3D; does not alter CPU parking or individual power parameters."},
   new Tweak{Id="high-power",Name="High performance plan",Category="Power",Mode="Direct",Destination="8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",Description="Existing Windows High performance plan. A/B experiment only: can raise heat and idle power without increasing FPS."}
  };
   string[][] guided={
    new[]{"graphics","GPU preference / HAGS","Display","ms-settings:display-advancedgraphics","Assign games to your dedicated GPU. HAGS is an A/B test and needs a reboot; don't stack it with unrelated changes."},
    new[]{"windowed","Windowed-game optimizations","Display","ms-settings:display-advancedgraphics","Use Windows' supported control for compatible DX10/11 windowed games. Restart the game after changes."},
    new[]{"refresh","Display refresh and resolution","Display","ms-settings:display-advanced","Check the actual gaming display and selected refresh rate. Confirm which display is connected to the gaming GPU."},
    new[]{"hdr","HDR / display settings","Display","ms-settings:display","Choose HDR for your display and visual preference; benchmark the game. No display mode is changed automatically."},
    new[]{"animations","Desktop animation effects","Windows","ms-settings:easeofaccess-visualeffects","Disable animation effects through Windows. Changes desktop feel; do not interpret it as measured game latency."},
    new[]{"mouse","Pointer acceleration","Input","ms-settings:mousetouchpad","Review pointer settings manually. Raw-input games and Raw Accel have their own behavior; FramePilot preserves your input tools."},
    new[]{"keyboard","Keyboard settings","Input","ms-settings:typing","Review keyboard preferences. FramePilot does not generate input or install polling-rate drivers."},
    new[]{"audio","Audio output and enhancements","Input","ms-settings:sound","Check the correct output and sample format. Keep Voicemeeter, Equalizer APO and microphone routing intact."},
    new[]{"startup","Packaged / system startup apps","Windows","ms-settings:startupapps","Review startup entries not supported by FramePilot's reversible HKCU Run manager."},
    new[]{"storage","Storage cleanup","Storage","ms-settings:storagesense","Windows-supported cleanup. Review categories yourself; actions here are outside FramePilot recovery."},
    new[]{"drives","Optimize drives","Storage","dfrgui.exe","Review Windows scheduled drive optimization. SSDs need the appropriate Windows operation, not indiscriminate defragmentation."},
    new[]{"network","Network adapter overview","Network","ms-settings:network-status","Check connection type and adapters. Diagnostics measures RTT; this does not rewrite TCP or adapter offloads."},
    new[]{"ethernet","Ethernet settings","Network","ms-settings:network-ethernet","Inspect the wired connection. A local-gateway test can help separate Wi-Fi/local congestion from remote routing."},
    new[]{"nvidia","NVIDIA official drivers","Drivers","https://www.nvidia.com/en-us/drivers/","Use official NVIDIA packages. FramePilot does not install modified drivers, force GPU clocks, or change driver profiles."},
    new[]{"amd","AMD chipset support","Drivers","https://www.amd.com/en/support","Find the chipset driver for your motherboard platform. Driver/BIOS installation is manual, outside recovery."},
    new[]{"reflex","Reflex and synchronization","Games","https://www.nvidia.com/en-gb/geforce/guides/system-latency-optimization-guide/","Use the in-game Reflex option where supported. Compare V-Sync/VRR and caps under the same conditions."},
    new[]{"shaders","Shader compilation / caches","Games","https://www.nvidia.com/en-us/geforce/technologies/frameview/","Let shaders finish compiling before measuring. Do not repeatedly clear shader caches expecting higher FPS."}
   };
   foreach(var g in guided)a.Add(new Tweak{Id=g[0],Name=g[1],Category=g[2],Destination=g[3],Description=g[4],Mode="Guided"});
   string[][] excluded={new[]{"boot","Boot timers / HPET / BCDEdit","No boot configuration, timer-resolution forcing or dynamic-tick recipes. Performance evidence is workload-specific and recovery can require reboot troubleshooting."},new[]{"irq","MSI / IRQ / CPU affinity hacks","No interrupt routing, device MSI registry edits, SMT disabling or affinity restrictions. The detected 8-core X3D does not justify blindly copying multi-CCD recipes."},new[]{"security","Security and anti-cheat changes","Secure Boot, TPM, Defender, memory integrity and anti-cheat services are never disabled by FramePilot."},new[]{"ram","RAM / standby-list cleaners","No periodic working-set trimming or standby purges. Free RAM is not itself a performance score; forced eviction can require data to be fetched again."},new[]{"driver","Modified drivers / forced clocks","No kernel driver, KBoost, P-state forcing, overclocking or undocumented driver-profile patching."},new[]{"services","Service stripping / network recipes","No broad service shutdown, update removal, undocumented TCP recipes or global power-throttling removal."}};
   foreach(var g in excluded)a.Add(new Tweak{Id=g[0],Name=g[1],Category="Not included",Description=g[2],Mode="Excluded"});return a;
  }
 }
}

