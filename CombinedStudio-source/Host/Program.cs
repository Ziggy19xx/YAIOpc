namespace CombinedStudio;
internal static class Program {
 [STAThread] static int Main(string[] args){
  try{
   if(args.Length==2&&args[0]=="--driver-updates")return FramePilot.DriverOffers.Worker(args[1]);
   if(args.Length==2&&args[0]=="--framepilot-test")return FramePilot.Tests.Run(args[1]);
   if(args.Length==2&&args[0]=="--monitor-test")return FramePilot.MonitorTests.Run(args[1]);
   if(args.Contains("--voice-test")){VelvetVoice.Tests.Run();return 0;}
   if(args.Contains("--test-worker")){Thread.Sleep(30000);return 0;}
   System.Windows.Forms.Application.EnableVisualStyles();System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
   bool test=args.Length==2&&args[0]=="--combined-smoke";
   using var instance=new Mutex(true,"Local\\FramePilotVelvetStudio-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,out bool own);if(!test&&!own){System.Windows.MessageBox.Show("FramePilot + Velvet Voice is already running.","FramePilot + Velvet Voice");return 1;}
   var application=new System.Windows.Application{ShutdownMode=System.Windows.ShutdownMode.OnMainWindowClose};

   var shell=new Shell(test?Path.Combine(Path.GetTempPath(),"CombinedStudio-test-"+Guid.NewGuid().ToString("N")):null);
   if(test)shell.Window.Loaded+=async (_,_)=>await shell.Verify(args[1]);
   application.Run(shell.Window);return Environment.ExitCode;
  }catch(Exception error){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"combined-startup-error.txt"),error.ToString());if(!args.Any(x=>x.EndsWith("test")||x.Contains("smoke")))System.Windows.MessageBox.Show(error.ToString(),"FramePilot + Velvet Voice");return 1;}
 }
}


