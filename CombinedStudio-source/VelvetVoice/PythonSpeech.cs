using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Text;
namespace VelvetVoice;

public static class PythonSpeech
{
    const int Limit = 48000 * 120;
    public static SpeechFocusResult Clean(AudioClip source,string mode) => Run(source,mode).GetAwaiter().GetResult();
    static async Task<SpeechFocusResult> Run(AudioClip source,string mode)
    {
        if(mode is not ("Natural" or "Strong" or "Voices only"))throw new ArgumentException("Unknown speech-focus mode.");
        if(source.SampleRate!=48000||source.Samples.Length>Limit)throw new ArgumentException("Speech worker needs a bounded mono 48 kHz snapshot.");
        string root=Path.Combine(AppContext.BaseDirectory,"python");
        string runtime=Path.Combine(root,"runtime","python.exe"),worker=Path.Combine(root,"speech_worker.py");
        if(!File.Exists(runtime)||!File.Exists(worker))throw new FileNotFoundException("Bundled Python clip worker is missing. Extract the complete portable ZIP with its python folder.");
        var info=new ProcessStartInfo(runtime){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
        info.ArgumentList.Add("-I");info.ArgumentList.Add(worker);info.ArgumentList.Add(mode);info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"rnnoise.dll"));
        using var process=Process.Start(info)??throw new IOException("Could not start the Python clip worker.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var error=process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            using(var input=new BinaryWriter(process.StandardInput.BaseStream,Encoding.UTF8,true)){
                input.Write(Encoding.ASCII.GetBytes("VVF1"));input.Write(source.Samples.Length);input.Flush();
            }
            var bytes=new byte[source.Samples.Length*4];Buffer.BlockCopy(source.Samples,0,bytes,0,bytes.Length);
            await process.StandardInput.BaseStream.WriteAsync(bytes,timeout.Token).ConfigureAwait(false);process.StandardInput.Close();
            byte[] header=new byte[16];await process.StandardOutput.BaseStream.ReadExactlyAsync(header,timeout.Token).ConfigureAwait(false);
            if(Encoding.ASCII.GetString(header,0,4)!="VVR1")throw new InvalidDataException("Python clip worker returned an invalid protocol version.");
            int count=BitConverter.ToInt32(header,4),speech=BitConverter.ToInt32(header,8),frames=BitConverter.ToInt32(header,12);
            int expected=(source.Samples.Length+479)/480;
            if(count!=source.Samples.Length||count>Limit||frames!=expected||speech<0||speech>frames)throw new InvalidDataException("Python clip worker returned inconsistent audio dimensions.");
            byte[] output=new byte[count*4];await process.StandardOutput.BaseStream.ReadExactlyAsync(output,timeout.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            if(process.ExitCode!=0)throw new IOException("Python clip worker failed: "+(await error.ConfigureAwait(false)).Trim());
            var samples=new float[count];Buffer.BlockCopy(output,0,samples,0,output.Length);
            if(samples.Any(x=>!float.IsFinite(x)||Math.Abs(x)>1.00001f))throw new InvalidDataException("Python clip worker produced invalid samples.");
            return new(new AudioClip(samples,48000),speech,frames);
        }
        catch(OperationCanceledException){throw new TimeoutException("Local clip processing timed out. The clip was not saved; try a shorter snapshot.");}
        catch(EndOfStreamException){string detail=await error.ConfigureAwait(false);throw new IOException("Python clip worker stopped before returning audio. "+detail.Trim());}
        finally{try{if(!process.HasExited)process.Kill(true);}catch{} }
    }
}

