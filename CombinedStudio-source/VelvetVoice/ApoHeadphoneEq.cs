using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VelvetVoice;
// Original controller for the installed Equalizer APO engine. No Peace source or presets are used.
internal sealed class ApoHeadphoneEq : IDisposable
{
    const string Marker="# Velvet Voice headphone EQ controller v1";
    const string Managed=Marker+"\nInclude: velvet-voice-route.txt\n";
    readonly string? folder;
    readonly string? fixture;
    internal bool Running {get;private set;}
    internal bool Available=>folder!=null&&Directory.Exists(folder);
    internal string Status {get;private set;}="Headphone EQ is off";
    internal ApoHeadphoneEq(string? isolated=null){fixture=isolated;folder=isolated??Discover();}
    internal static string? Discover()
    {
        try{using var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\EqualizerAPO");if(key?.GetValue("ConfigPath") is string path&&Directory.Exists(path))return path;}catch(System.Security.SecurityException){}
        string standard=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"EqualizerAPO","config");return Directory.Exists(standard)?standard:null;
    }
    internal static string EndpointGuid(string id)
    {
        var match=Regex.Match(id,@"\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}$");
        if(!match.Success)throw new InvalidOperationException("The selected headphone endpoint has an invalid device ID.");return match.Value.ToLowerInvariant();
    }
    internal static string Filters(EqParameters settings)
    {
        var text=new StringBuilder("# Original Velvet Audio listening curve\nChannel: all\n");
        if(settings.Bypass)return text.Append("# Tone shaping bypassed\n").ToString();
        // APO applies filters in the existing system path; retain 1 dB of conservative headroom.
        float safePreamp=Math.Min(-1,HeadphoneEqProvider.Headroom(settings)-1);
        string Number(float value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
        text.AppendLine("Preamp: "+Number(safePreamp)+" dB");
        for(int i=0;i<10;i++)text.AppendLine($"Filter {i+1}: ON PK Fc {Number(HeadphoneEqProvider.Frequencies[i])} Hz Gain {Number(settings.Bands[i])} dB Q 1.4");
        return text.ToString();
    }
    internal void Start(string deviceId,EqParameters settings)
    {
        if(!Available)throw new InvalidOperationException("Direct headphone EQ needs Equalizer APO installed. Advanced routing remains available.");
        string guid=EndpointGuid(deviceId),main=Path.Combine(folder!,"config.txt"),backup=Path.Combine(folder!,"velvet-voice-before.txt");
        string original=File.Exists(main)?File.ReadAllText(main):"";
        if(original!=Managed){
            // Refuse partial/damaged managed roots rather than backing them up recursively.
            if(original.Contains(Marker,StringComparison.Ordinal))throw new InvalidOperationException("The Velvet Audio audio configuration changed. Restore its backup before enabling EQ again.");
            File.WriteAllText(backup,original);
        }else if(!File.Exists(backup))throw new InvalidOperationException("The previous audio configuration backup is missing. Open Equalizer APO Configuration Editor to repair it.");
        File.WriteAllText(Path.Combine(folder!,"velvet-voice-eq.txt"),Filters(settings));
        File.WriteAllText(Path.Combine(folder!,"velvet-voice-route.txt"),$"# Only the selected headphone endpoint uses Velvet Audio. Other devices retain their previous configuration.\nIf: tolower(deviceGuid) == \"{guid}\"\nInclude: velvet-voice-eq.txt\nElse:\nInclude: velvet-voice-before.txt\nEndIf:\n");
        File.WriteAllText(main,Managed);Running=true;Status="Direct headphone EQ enabled · Equalizer APO";
    }
    internal void Update(EqParameters settings)
    {
        if(!Running)return;
        string main=Path.Combine(folder!,"config.txt");
        if(!File.Exists(main)||File.ReadAllText(main)!=Managed){Running=false;Status="Another equalizer changed the audio configuration. Enable Velvet Audio EQ again to take control.";throw new InvalidOperationException(Status);}
        File.WriteAllText(Path.Combine(folder!,"velvet-voice-eq.txt"),Filters(settings));
    }
    internal void Stop()
    {
        if(!Running)return;
        string main=Path.Combine(folder!,"config.txt"),backup=Path.Combine(folder!,"velvet-voice-before.txt");
        bool ours=File.Exists(main)&&File.ReadAllText(main)==Managed;
        if(ours){if(!File.Exists(backup))throw new IOException("The previous audio configuration backup is missing; the active configuration was left intact.");File.WriteAllText(main,File.ReadAllText(backup));}
        Running=false;Status=ours?"Headphone EQ is off · previous audio configuration restored":"Headphone EQ is off · external audio configuration left intact";
    }
    internal void OpenDeviceSetup()
    {
        if(fixture!=null)return;
        if(folder==null)throw new InvalidOperationException("Equalizer APO was not found.");
        string root=Path.GetDirectoryName(folder)!;
        string executable=new[]{"DeviceSelector.exe","Configurator.exe"}.Select(name=>Path.Combine(root,name)).FirstOrDefault(File.Exists)??throw new FileNotFoundException("Equalizer APO device setup was not found.");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable){UseShellExecute=true});
    }
    public void Dispose()=>Stop();
}
