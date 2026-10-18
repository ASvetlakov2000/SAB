using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;

namespace SAB.NotificationSettingsPreview
{
    public static class Program
    {
        public static string Root,Output;
        [STAThread] public static int Main(string[] args)
        {
            string rootArg=args.FirstOrDefault(a=>a.StartsWith("--root="));
            Root=rootArg==null?FindRoot():rootArg.Substring(7);
            Output=Path.Combine(Root,@"outputs\ui-production-preview\notification-settings\revision-2");
            try {
                if(args.Contains("--capture")||args.Contains("--verify-boundaries")) {
                    var captureApp=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
                    return args.Contains("--capture")?Evidence.Run():Evidence.VerifyBoundaries();
                }
                string identity;
                using(var sha=SHA256.Create())identity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(Root).ToUpperInvariant()+"|revision-2"))).Replace("-","").Substring(0,16);
                using(var mutex=new Mutex(true,@"Local\SAB.NotificationSettings.Preview."+identity,out bool owner))
                using(var activate=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\SAB.NotificationSettings.Activate."+identity)) {
                    if(!owner) {activate.Set();return 0;}
                    try {
                        var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
                        var window=new SettingsWindow();app.MainWindow=window;
                        var listener=ThreadPool.RegisterWaitForSingleObject(activate,(s,timeout)=>app.Dispatcher.BeginInvoke(new Action(()=> {
                            if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
                            window.Show();window.Activate();
                        })),null,Timeout.Infinite,false);
                        try{return app.Run(window);}finally{listener.Unregister(null);}
                    }finally{mutex.ReleaseMutex();}
                }
            } catch(Exception ex) {
                Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"error.txt"),ex.ToString());return 1;
            }
        }
        static string FindRoot()
        {
            var dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while(dir!=null) {if(File.Exists(Path.Combine(dir.FullName,@"SAB\UI\Styles\SABFluentLight.xaml")))return dir.FullName;dir=dir.Parent;}
            throw new DirectoryNotFoundException("SAB project root not found.");
        }
    }
}
