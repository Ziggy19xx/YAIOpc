using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FramePilot {
 // Compiled only into the WPF edition. The original services and UI remain shared.
 public sealed partial class MainForm {
  public event Action WpfChanged;
  public string WpfPage { get { return currentPage; } }
  public string WpfStatus { get { return status.Text; } }
  public string[] WpfMetrics { get { return new[]{cpuValue.Text,ramValue.Text,gpuValue.Text,gpuDetail.Text,dashboardCounts.Text}; } }
  public ResourceSample[] WpfSamples { get { return samples.ToArray(); } }
  public bool WpfMonitoring { get { return monitoring.Checked; } set { monitoring.Checked=value; } }
  public bool WpfBusy { get { return busy; } }
  public bool WpfSampling { get { return sampling; } }
  public bool WpfTimerRunning { get { return resourceTimer.Enabled; } }
  public IEnumerable<Tweak> WpfTweaks { get { return tweaks; } }
  public bool WpfSelected(string id) { return selectedTweaks.Contains(id); }
  public void WpfSelect(string id,bool selected) { if(selected)selectedTweaks.Add(id);else selectedTweaks.Remove(id); }
  public static string WpfImpact(string name) { return Impact(name); }
  public string[] WpfPages { get { return pages.Keys.ToArray(); } }
  public Control WpfToolSource(string name) { return pages[name]; }
  // Invoke the original guarded handler even when its WinForms host is hidden.
  public void WpfToolClick(Button button) { if(!button.Enabled)return;try{typeof(Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});}catch(System.Reflection.TargetInvocationException e){throw e.InnerException??e;}NotifyWpf(); }
  public static bool WpfControlVisible(Control control) { return (bool)typeof(Control).GetMethod("GetState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(control,new object[]{2}); }
  public void WpfNavigate(string name) { if(!pages.ContainsKey(name))throw new ArgumentException("Unknown page");ShowPage(name);NotifyWpf(); }
  public async Task WpfSnapshot() { await SampleResources(true);NotifyWpf(); }
  public void WpfPause(bool paused) { if(paused)resourceTimer.Stop();else resourceTimer.Start(); }
  void NotifyWpf() { if(WpfChanged!=null)WpfChanged(); }
  // Games found by the Game library scan (or added manually). Empty until a scan has run.
  public FoundGame[] WpfGames { get { return GameDiscovery.Distinct(discovered.Concat(customGames)).ToArray(); } }
  public void WpfSetGames(IEnumerable<FoundGame> games) { discovered=GameDiscovery.Distinct(games??Enumerable.Empty<FoundGame>());if(discoveredGrid!=null&&!IsDisposed)FillDiscovered();NotifyWpf(); }
  public void WpfLaunchGame(FoundGame game) {
   if(!GameDiscovery.CanLaunch(game))throw new InvalidOperationException("This game link is unavailable. Open the Game library to add its current EXE or launcher shortcut.");
   Open(game.Launch);NotifyWpf();
  }
  public async Task WpfScanGames(bool folder) { await ScanGames(folder);NotifyWpf(); }
  public void WpfAddCustomGame() { AddCustomGame();NotifyWpf(); }
  public void WpfSaveArtworkKey(string key) { SteamArtwork.SaveApiKey(key);Status("SteamGridDB key saved encrypted for this Windows account.");NotifyWpf(); }
  public string WpfGameScanStatus { get { return discoveryStatus==null?"":discoveryStatus.Text; } }
  public string[] WpfGameScanIssues { get { return lastGameWarnings.Take(30).ToArray(); } }
  public void WpfOpenGameFolder(FoundGame game) { if(game==null||!Directory.Exists(game.Folder))throw new InvalidOperationException("Installation folder is no longer available.");Open(game.Folder); }
  public void WpfRemoveCustomGame(FoundGame game) { if(game==null||!customGames.Any(x=>string.Equals(x.Launch,game.Launch,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Only custom links can be removed. Scanned entries return on the next scan.");customGames.RemoveAll(x=>string.Equals(x.Launch,game.Launch,StringComparison.OrdinalIgnoreCase));SaveCustom();FillDiscovered();Status("Link removed; game files were not touched.");NotifyWpf(); }
  public void WpfOpenGameProfile(FoundGame item) {
   string target=SelectMatchingGameProfile(item);
   ShowPage("Game profiles");Status(target==null?"Choose a built-in game profile or load a saved preset for "+(item==null?"your game":item.Name)+".":"Showing settings and presets for "+target+". Loading a profile only fills the editor.");NotifyWpf();
  }
  public void WpfCreateGameProfile(FoundGame item){string target=SelectMatchingGameProfile(item);string name=target??(item==null?"game":item.Name);if(presetName!=null)presetName.Text="My "+name+" profile";ShowPage("Game profiles");Status("Profile editor ready for "+name+". Review the options, then save the local preset when you are ready.");NotifyWpf();}
  string SelectMatchingGameProfile(FoundGame item){string name=item==null?"":(item.Name??"").ToLowerInvariant();string normalized=new string(name.Where(char.IsLetterOrDigit).ToArray());string target=null;if(normalized.Contains("valorant"))target="Valorant";else if(normalized.Contains("fortnite"))target="Fortnite";else if(normalized.Contains("minecraft"))target="Minecraft Java";else if(normalized.Contains("tarkov"))target="Escape from Tarkov";else if(normalized.Contains("callofduty")||normalized.Contains("modernwarfare")||normalized.Contains("blackops")||normalized.Contains("warzone"))target="Call of Duty";if(target!=null&&game.Items.Cast<object>().Any(x=>string.Equals(Convert.ToString(x),target,StringComparison.OrdinalIgnoreCase)))game.SelectedItem=target;return target;}
  // Newest three journal entries as { name, status, created }.
  public string[][] WpfActivity { get { return journal.Items.OrderByDescending(x=>x.Created).Take(3).Select(x=>new[]{string.IsNullOrEmpty(x.Name)?x.Target:x.Name,x.Status,x.Created}).ToArray(); } }
  public void WpfPrepare() {
   Page("PC monitor","PC monitor","Native WPF hardware monitor");Page("Disk explorer","Disk explorer","Native WPF storage explorer");
   var updates=pages["Updates & notes"] as FlowLayoutPanel;
   if(updates!=null){
    foreach(Control child in updates.Controls){
     var label=child as Label;if(label!=null&&label.Text.StartsWith("One portable"))label.Text="Combined WPF edition: keep FramePilot.exe, monitor and models together.\r\nExtract a complete release into a new folder to update. Local preferences remain shared.";
     foreach(Control control in child.Controls){var button=control as Button;if(button!=null&&(button.Text=="Create desktop EXE"||button.Text=="Install local update"||button.Text=="Restore previous app EXE")){button.Enabled=false;button.Text+=" (EXE-only edition)";}}
    }
    var distribution=Row();distribution.Controls.Add(Button("Open application folder",delegate{Open(AppDomain.CurrentDomain.BaseDirectory);}));updates.Controls.Add(distribution);
    updates.Controls.Add(Note("The combined edition uses a complete application folder. Its Python runtime,\r\nmonitoring libraries and reference models must accompany the executable.\r\nKeep the previous release folder if you want to return to it. Recovery continues\r\nto handle Windows settings separately. The original WinForms EXE updater is unchanged.",110));
   }
   TopLevel=false;FormBorderStyle=FormBorderStyle.None;MinimumSize=System.Drawing.Size.Empty;Dock=DockStyle.Fill;
   foreach(Control control in Controls)if(control!=content)control.Visible=false;
   content.Padding=new Padding(20,18,16,12);
   status.TextChanged+=delegate{NotifyWpf();};
   foreach(var page in pages.Values)page.VisibleChanged+=delegate{NotifyWpf();};
   foreach(var label in new[]{cpuValue,ramValue,gpuValue,gpuDetail,dashboardCounts})label.TextChanged+=delegate{NotifyWpf();};
  }
  // Both UIs call the same Apply/Undo handlers and use the same journal instance.
  public void WpfCatalogAction(string id,string action) {
   var tweak=tweaks.Single(x=>x.Id==id);
   if(action=="Apply"&&tweak.Direct)ApplyCatalogTweak(tweak);
   else if(action=="Undo"&&tweak.Direct)UndoCatalogTweak(tweak);
   else if(action=="Open settings / guide"&&tweak.Mode=="Guided")OpenDestination(tweak.Destination);
   else throw new InvalidOperationException("This action is not available for this control.");
   RefreshDashboardCounts();NotifyWpf();
  }
  public void WpfApplySelection() { ApplyCatalogSelection();RefreshDashboardCounts();NotifyWpf(); }
 }
}



