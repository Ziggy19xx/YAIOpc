using NAudio.Wave;
namespace VelvetVoice;
internal static class HeadphoneEqTests
{
    sealed class Signal(int rate,double frequency,float amplitude) : ISampleProvider
    {
        long frame;
        public WaveFormat WaveFormat {get;}=WaveFormat.CreateIeeeFloatWaveFormat(rate,2);
        public int Read(float[] buffer,int offset,int count){for(int i=0;i<count;i+=2){buffer[offset+i]=(float)(amplitude*Math.Sin(2*Math.PI*frequency*frame++/rate));buffer[offset+i+1]=0;}return count;}
    }
    internal static void Run(List<string> log)
    {
        void Check(bool valid,string message){if(!valid)throw new Exception("Headphone EQ: "+message);log.Add("PASS headphone EQ: "+message);}
        foreach(int rate in new[]{44100,48000,96000}){
            var parameters=new EqParameters(new float[]{0,0,0,0,0,6,0,0,0,0},0,false);
            var processor=new HeadphoneEqProvider(new Signal(rate,1000,.1f));processor.Set(parameters);var data=new float[rate*2];processor.Read(data,0,data.Length);processor.Read(data,0,data.Length);
            double rms=Math.Sqrt(data.Where((_,i)=>i%2==0).Select(v=>(double)v*v).Average());
            Check(Math.Abs(rms-.1/Math.Sqrt(2))<.001,$"1 kHz +6 dB with automatic −6 dB headroom at {rate} Hz");
            Check(data.Where((_,i)=>i%2==1).All(v=>v==0),$"stereo channels remain independent at {rate} Hz");
            processor.Set(new(new float[]{12,12,12,12,12,12,12,12,12,12},6,false));processor.Read(data,0,data.Length);
            Check(data.All(v=>float.IsFinite(v)&&Math.Abs(v)<=.891251),$"maximum boosts remain finite and protected at {rate} Hz");
        }
        var hot=new HeadphoneEqProvider(new Signal(48000,200,2));hot.Set(new(new float[10],0,true));var samples=new float[96000];hot.Read(samples,0,samples.Length);
        Check(samples.All(v=>float.IsFinite(v)&&Math.Abs(v)<=.891251),"bypass retains the −1 dBFS protective ceiling");
        var flat=new HeadphoneEqProvider(new Signal(48000,200,.1f));flat.Read(samples,0,samples.Length);
        Check(Math.Abs(Math.Sqrt(samples.Where((_,i)=>i%2==0).Select(v=>(double)v*v).Average())-.1/Math.Sqrt(2))<.00001,"flat curve leaves quiet audio unchanged");
        var changing=new HeadphoneEqProvider(new Signal(48000,120,.05f));float last=0,maxStep=0;var block=new float[960];
        for(int n=0;n<200;n++){changing.Set(new(new float[]{n%2==0?12:-12,0,0,0,0,0,0,0,0,0},0,n%3==0));changing.Read(block,0,block.Length);for(int i=0;i<block.Length;i+=2){maxStep=Math.Max(maxStep,Math.Abs(block[i]-last));last=block[i];}}
        Check(maxStep<.01f,"live band and bypass transitions avoid large sample jumps");
        var shape=new float[10];shape[5]=-6;Check(Math.Abs(HeadphoneEqProvider.Response(1000,new(shape,0,false))+6)<.001,"drawn response matches a −6 dB center-frequency cut");
        using(var engine=new HeadphoneEqEngine()){bool rejected=false;try{engine.Start("same","same",new(new float[10],0,false),_=>{});}catch(InvalidOperationException){rejected=true;}Check(rejected&&!engine.Running,"same-device feedback route is rejected before opening audio");}
        string folder=Path.Combine(Path.GetTempPath(),"VelvetEq-test-"+Guid.NewGuid().ToString("N"));
        try{var store=new HeadphoneEqStore(folder);var custom=new EqProfile("My test",shape,-3);store.Config=new("source-id","headphone-id",custom,new[]{custom},true);store.Save();var loaded=new HeadphoneEqStore(folder);Check(loaded.Config.Current.Bands.SequenceEqual(shape)&&loaded.Config.Saved.Length==1&&loaded.Config.Source=="source-id"&&loaded.Config.Output=="headphone-id"&&loaded.Config.Current.Preamp==-3&&loaded.Config.Advanced,"custom preset, preamp, routing mode, and device preferences round-trip");File.WriteAllText(Path.Combine(folder,"headphone-eq.json"),"{broken");Check(new HeadphoneEqStore(folder).Config.Current.Name=="Flat","invalid settings recover to flat");
            string apoFolder=Path.Combine(folder,"APO");Directory.CreateDirectory(apoFolder);string config=Path.Combine(apoFolder,"config.txt"),original="Include: peace.txt\r\n";File.WriteAllText(config,original);
            string id="{0.0.0.00000000}.{01234567-89AB-CDEF-0123-456789ABCDEF}";
            using(var apo=new ApoHeadphoneEq(apoFolder)){
                Check(apo.Available&&!apo.Running,"direct backend detects installation without enabling audio");apo.Start(id,new(shape,-3,false));
                Check(apo.Running&&File.ReadAllText(Path.Combine(apoFolder,"velvet-voice-before.txt"))==original,"direct mode backs up the previous configuration exactly");
                string route=File.ReadAllText(Path.Combine(apoFolder,"velvet-voice-route.txt"));Check(route.Contains("tolower(deviceGuid) == \"{01234567-89ab-cdef-0123-456789abcdef}\"")&&route.Contains("Else:\nInclude: velvet-voice-before.txt"),"direct mode selects one exact endpoint and retains other devices' prior configuration");
                string filters=File.ReadAllText(Path.Combine(apoFolder,"velvet-voice-eq.txt"));Check(filters.Contains("Preamp: -4 dB")&&filters.Contains("Fc 1000 Hz Gain -6 dB Q 1.4"),"direct mode exports original filters with conservative headroom");
                apo.Update(new(shape,0,true));Check(!File.ReadAllText(Path.Combine(apoFolder,"velvet-voice-eq.txt")).Contains("Filter 1:"),"direct bypass removes Velvet Audio tone shaping without adding Peace filters");
                apo.Stop();Check(!apo.Running&&File.ReadAllText(config)==original,"disabling direct mode restores the previous configuration exactly");
                apo.Start(id,new(shape,0,false));File.WriteAllText(config,"Include: changed-by-another-app.txt\n");bool conflict=false;try{apo.Update(new(shape,0,false));}catch(InvalidOperationException){conflict=true;}apo.Stop();Check(conflict&&!apo.Running&&File.ReadAllText(config).Contains("changed-by-another-app"),"direct mode detects outside edits and does not overwrite them during stop");
            }
            bool invalid=false;try{ApoHeadphoneEq.EndpointGuid("device\nInclude: injected.txt");}catch(InvalidOperationException){invalid=true;}Check(invalid,"direct endpoint input cannot inject APO commands");
        }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
