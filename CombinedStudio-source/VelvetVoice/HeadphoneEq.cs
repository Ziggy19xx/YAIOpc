using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Text.Json;

namespace VelvetVoice;

internal sealed record EqProfile(string Name, float[] Bands, float Preamp = 0)
{
    public override string ToString()=>Name;
}
internal sealed record EqConfig(string? Source, string? Output, EqProfile Current, EqProfile[] Saved, bool Advanced = false);
internal sealed record EqParameters(float[] Bands, float Preamp, bool Bypass);

// Independent stereo playback DSP. Settings are immutable snapshots; filter history stays on the render thread.
internal sealed class HeadphoneEqProvider : ISampleProvider
{
    internal static readonly float[] Frequencies = {31.5f,63,125,250,500,1000,2000,4000,8000,16000};
    readonly ISampleProvider source;
    readonly BiQuadFilter[][] filters;
    readonly float[] gains = new float[10];
    EqParameters parameters = new(new float[10],0,false);
    double level = 1, mix = 1, limiter = 1;
    int update;
    internal float Peak {get;private set;}
    public WaveFormat WaveFormat => source.WaveFormat;
    internal HeadphoneEqProvider(ISampleProvider source)
    {
        this.source=source;
        filters=Enumerable.Range(0,WaveFormat.Channels).Select(_=>Frequencies.Select(f=>BiQuadFilter.PeakingEQ(WaveFormat.SampleRate,Math.Min(f,WaveFormat.SampleRate*.45f),1.4f,0)).ToArray()).ToArray();
    }
    internal void Set(EqParameters value)=>Volatile.Write(ref parameters,new EqParameters(value.Bands.Select(v=>Math.Clamp(v,-12,12)).ToArray(),Math.Clamp(value.Preamp,-24,6),value.Bypass));
    // Sum of positive boosts gives conservative headroom, including overlapping bands.
    internal static float Headroom(EqParameters value)=>Math.Min(0,-value.Bands.Where(v=>v>0).Sum())+value.Preamp;
    public int Read(float[] buffer,int offset,int count)
    {
        int read=source.Read(buffer,offset,count),channels=WaveFormat.Channels;
        var settings=Volatile.Read(ref parameters);
        double smoothing=1-Math.Exp(-1/(WaveFormat.SampleRate*.025));
        double target=Math.Pow(10,Headroom(settings)/20), peak=0;
        for(int i=offset;i<offset+read;i+=channels)
        {
            if(update++%32==0)
                for(int b=0;b<10;b++){
                    gains[b]+=(settings.Bands[b]-gains[b])*(float)(1-Math.Exp(-32/(WaveFormat.SampleRate*.04)));
                    foreach(var bank in filters)bank[b].SetPeakingEq(WaveFormat.SampleRate,Math.Min(Frequencies[b],WaveFormat.SampleRate*.45f),1.4f,gains[b]);
                }
            level+=(target-level)*smoothing;mix+=((settings.Bypass?0:1)-mix)*smoothing;
            double framePeak=0;
            for(int ch=0;ch<channels&&i+ch<offset+read;ch++){
                float dry=buffer[i+ch],wet=(float)(dry*level);
                foreach(var filter in filters[ch])wet=filter.Transform(wet);
                buffer[i+ch]=(float)(dry*(1-mix)+wet*mix);framePeak=Math.Max(framePeak,Math.Abs(buffer[i+ch]));
            }
            double needed=Math.Min(1,.89125094/Math.Max(framePeak,1e-12));
            limiter=needed<limiter?needed:limiter+(needed-limiter)*(1-Math.Exp(-1/(WaveFormat.SampleRate*.1)));
            for(int ch=0;ch<channels&&i+ch<offset+read;ch++){buffer[i+ch]=(float)Math.Clamp(buffer[i+ch]*limiter,-.89125094,.89125094);peak=Math.Max(peak,Math.Abs(buffer[i+ch]));}
        }
        Peak=(float)peak;return read;
    }
    internal static double Response(double frequency,EqParameters settings,int rate=48000)
    {
        double db=Headroom(settings);
        for(int b=0;b<10;b++){
            double a=Math.Pow(10,settings.Bands[b]/40),w=2*Math.PI*Math.Min(Frequencies[b],rate*.45)/rate,alpha=Math.Sin(w)/(2*1.4),cos=Math.Cos(w);
            var z=System.Numerics.Complex.FromPolarCoordinates(1,-2*Math.PI*frequency/rate);
            var numerator=(1+alpha*a)-2*cos*z+(1-alpha*a)*z*z;
            var denominator=(1+alpha/a)-2*cos*z+(1-alpha/a)*z*z;
            db+=20*Math.Log10((numerator/denominator).Magnitude);
        }
        return settings.Bypass?0:db;
    }
}

internal sealed class HeadphoneEqEngine : IDisposable
{
    WasapiLoopbackCapture? capture;
    WasapiOut? player;
    MMDevice? inputDevice,outputDevice;
    HeadphoneEqProvider? processor;
    BufferedWaveProvider? queue;
    readonly object gate=new();
    int generation;
    internal bool Running {get;private set;}
    internal string Status {get;private set;}="Stopped · choose a source and headphones";
    internal float Peak=>processor?.Peak??0;
    internal void Update(EqParameters settings){lock(gate)processor?.Set(settings);}
    internal void Start(string sourceId,string outputId,EqParameters settings,Action<string> fault)
    {
        if(sourceId==outputId)throw new InvalidOperationException("Choose different source and headphone devices to prevent an audio feedback loop.");
        Stop();
        try{
            using var devices=new MMDeviceEnumerator();inputDevice=devices.GetDevice(sourceId);outputDevice=devices.GetDevice(outputId);
            capture=new WasapiLoopbackCapture(inputDevice);
            if(capture.WaveFormat.Channels>2||outputDevice.AudioClient.MixFormat.Channels>2)throw new InvalidOperationException("Select stereo devices for headphone EQ. Set both endpoints to stereo in Windows Sound settings.");
            queue=new BufferedWaveProvider(capture.WaveFormat){BufferDuration=TimeSpan.FromMilliseconds(300),DiscardOnBufferOverflow=true,ReadFully=true};
            ISampleProvider audio=queue.ToSampleProvider();
            if(audio.WaveFormat.Channels==1)audio=new MonoToStereoSampleProvider(audio);
            int rate=outputDevice.AudioClient.MixFormat.SampleRate;
            if(audio.WaveFormat.SampleRate!=rate)audio=new WdlResamplingSampleProvider(audio,rate);
            processor=new HeadphoneEqProvider(audio);processor.Set(settings);
            var activeQueue=queue;int session=++generation;
            capture.DataAvailable+=(_,e)=>{lock(gate){if(session==generation)activeQueue.AddSamples(e.Buffer,0,e.BytesRecorded);}};
            void Failed(Exception? error){if(error==null)return;System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(session!=generation)return;Stop();Status="Audio stopped: "+error.Message;fault(Status);}));}
            capture.RecordingStopped+=(_,e)=>Failed(e.Exception);
            player=new WasapiOut(outputDevice,AudioClientShareMode.Shared,true,50);player.PlaybackStopped+=(_,e)=>Failed(e.Exception);
            player.Init(processor);capture.StartRecording();player.Play();Running=true;Status="Listening · stereo EQ is active";
        }catch{Stop();throw;}
    }
    internal void Stop()
    {
        ++generation;Running=false;
        try{capture?.StopRecording();player?.Stop();}finally{capture?.Dispose();player?.Dispose();inputDevice?.Dispose();outputDevice?.Dispose();capture=null;player=null;inputDevice=null;outputDevice=null;processor=null;queue=null;Status="Stopped · headphone EQ is off";}
    }
    public void Dispose()=>Stop();
}

internal sealed class HeadphoneEqStore
{
    readonly string path;
    internal EqConfig Config;
    internal static readonly EqProfile[] Factory={
        new("Flat",new float[10]),
        new("Warm listening",new float[]{2,2,1,0,-.5f,0,.5f,0,-1,-1}),
        new("Balanced detail",new float[]{1,1,0,-1,-1,0,1,1,.5f,0}),
        new("Bass cushion",new float[]{4,3,2,0,-1,0,0,0,-.5f,-1}),
        new("Voice clarity",new float[]{-2,-2,-1,0,0,1,2,1,-1,-1}),
        new("Game focus",new float[]{-2,-1,-1,0,0,1,2,2,0,-1}),
        new("Soft treble",new float[]{1,1,0,0,0,0,-1,-2,-3,-2})};
    internal HeadphoneEqStore(string? root)
    {
        path=Path.Combine(root??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VelvetVoice"),"headphone-eq.json");
        Config=new(null,null,Factory[0],Array.Empty<EqProfile>());
        try{if(File.Exists(path)){var saved=JsonSerializer.Deserialize<EqConfig>(File.ReadAllText(path));if(saved!=null&&Valid(saved.Current)&&saved.Saved!=null&&saved.Saved.All(Valid))Config=saved;}}catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){}
    }
    internal static bool Valid(EqProfile p)=>p!=null&&!string.IsNullOrWhiteSpace(p.Name)&&p.Bands?.Length==10&&p.Bands.All(v=>float.IsFinite(v)&&v>=-12&&v<=12)&&float.IsFinite(p.Preamp)&&p.Preamp>=-24&&p.Preamp<=6;
    internal void Save(){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(Config));}
}
