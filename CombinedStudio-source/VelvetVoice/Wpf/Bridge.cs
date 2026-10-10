using System.Windows.Forms;
using System.Drawing;
namespace VelvetVoice;

public sealed partial class MainForm
{
    internal ComboBox UiInput=>inputs;
    internal ComboBox UiOutput=>outputs;
    internal ComboBox UiChannel=>channel;
    internal ComboBox UiLatency=>latency;
    internal ComboBox UiPreset=>presets;
    internal IReadOnlyDictionary<string,TrackBar> UiSliders=>sliders;
    internal Button UiStart=>start;
    internal Button UiBypass=>bypass;
    internal string UiStatus=>status.Text;
    internal string UiRoute=>route.Text;
    internal string UiMeterText=>meters.Text;
    internal bool UiLive=>capture!=null;
    internal SessionMixer UiMixer=>mixer;
    internal SessionMixer UiSyntheticMixer(){if(capture!=null)throw new InvalidOperationException("Synthetic fixture requires inactive audio");mixer=new SessionMixer(new SyntheticSignal(),1,new EntertainmentState{Master=1});return mixer;}
    internal void UiClearSynthetic(){mixer=null;}
    sealed class SyntheticSignal:NAudio.Wave.ISampleProvider{public NAudio.Wave.WaveFormat WaveFormat=>NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000,1);public int Read(float[] data,int offset,int count){Array.Fill(data,.1f,offset,count);return count;}}
    internal (float? Input,float? Output,float? Reduction,int? Queue) UiMetrics=>stream is {} s?(s.InputPeak,mixer?.OutputPeak??s.OutputPeak,s.Reduction,s.QueueMs):(null,null,null,null);
    internal EntertainmentPanel UiBoard=>entertainment!;
    internal Control UiVoicePage=>tabs.TabPages[2].Controls[0];
    internal void UiRefreshDevices()=>RefreshDevices();
    internal async Task UiInitialize(){_ = Handle;await entertainment!.UiLoad();}
    internal void UiShutdown(){closing=true;SaveConfig();StopAudio();timer.Stop();timer.Dispose();}
    internal static void UiClick(Button button){if(!button.Enabled)return;try{typeof(Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(button,new object[]{EventArgs.Empty});}catch(System.Reflection.TargetInvocationException error){throw error.InnerException??error;}}
}
public sealed partial class EntertainmentPanel
{
    internal Task UiLoad()=>LoadBoard();
    internal ListView UiSounds=>sounds;
    internal Control UiSource=>Controls[0];
    internal void UiPlaySelected(){if(Selected is {} entry)Play(entry.Id);}
    internal void UiTrim()=>TrimSelected();
    internal void UiPauseKeys(bool paused){host.Enabled=!paused;}
}


