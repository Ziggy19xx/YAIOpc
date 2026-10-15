namespace CombinedStudio;
internal static class Program {
 [STAThread] static int Main(string[] args){
  try{
   if(args.Length==2&&args[0]=="--fps-stream-test")return FpsCapture.Probe(args[1]);
   if(args.Length==2&&args[0]=="--fps-test")return FpsTests.Run(args[1]);
   if(args.Length==2&&args[0]=="--fps-worker")return FpsCapture.Worker(args[1]);
   if(args.Length==2&&args[0]=="--discovery-test")return FramePilot.GameLibraryPage.VerifyDiscovery(args[1]);
   if(args.Length==2&&args[0]=="--window-fit-test")return WindowFit.Test(args[1]);
   if(args.Length==2&&args[0]=="--crosshair-test")return CrosshairTests.Run(args[1]);
   if(args.Length==2&&args[0]=="--crosshair-community-test")return CrosshairTests.RunCommunity(args[1]);
   if(args.Length==2&&args[0]=="--stretch-test")return StretchTests.Run(args[1]);
   if(args.Length==2&&args[0]=="--stretch-monitor")return StretchMonitorDriver.Worker(args[1]);
   if(args.Contains("--enable-startup")){WindowsStartup.SetEnabled(true);return 0;}
   if(args.Length==2&&args[0]=="--driver-updates")return FramePilot.DriverOffers.Worker(args[1]);
   if(args.Length==2&&args[0]=="--framepilot-test")return FramePilot.Tests.Run(args[1]);
   if(args.Length==2&&args[0]=="--monitor-test")return FramePilot.MonitorTests.Run(args[1]);
   if(args.Contains("--voice-test")){VelvetVoice.Tests.Run();return 0;}
   if(args.Contains("--test-worker")){Thread.Sleep(30000);return 0;}
   System.Windows.Forms.Application.EnableVisualStyles();System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
   bool layoutReview=args.Length==2&&args[0]=="--layout-review";bool navigationTest=args.Length==2&&args[0]=="--library-navigation-test";bool test=layoutReview||navigationTest||(args.Length==2&&args[0]=="--combined-smoke");
   using var instance=new Mutex(true,"Local\\FramePilotVelvetStudio-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,out bool own);if(!test&&!own){if(args.Contains("--startup"))return 0;System.Windows.MessageBox.Show("YAIOpc is already running.","YAIOpc");return 1;}
   var application=new System.Windows.Application{ShutdownMode=System.Windows.ShutdownMode.OnMainWindowClose};

   var shell=new Shell(test?Path.Combine(Path.GetFullPath(args[1]),"test-state-"+Guid.NewGuid().ToString("N")):null);
   if(layoutReview)shell.Window.Loaded+=async (_,_)=>{await Task.Delay(200);await shell.ReviewLayouts(args[1]);};else if(navigationTest)shell.Window.Loaded+=(_,_)=>shell.Window.Dispatcher.BeginInvoke(new Action(()=>shell.VerifyLibraryNavigation(args[1])));else if(test)shell.Window.Loaded+=async (_,_)=>await shell.Verify(args[1]);
   application.Run(shell.Window);return Environment.ExitCode;
  }catch(Exception error){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"combined-startup-error.txt"),error.ToString());if(!args.Any(x=>x.EndsWith("test")||x.Contains("smoke")))System.Windows.MessageBox.Show(error.ToString(),"YAIOpc");return 1;}
 }
}
