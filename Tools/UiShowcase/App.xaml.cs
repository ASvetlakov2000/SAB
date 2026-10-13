using System;
using System.Linq;
using System.Windows;
namespace SAB.UiShowcase
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                var window = new MainWindow(); MainWindow = window;
                if (e.Args.Contains("--capture")) { Capture.Run(window, e.Args); Shutdown(); }
                else { ShutdownMode = ShutdownMode.OnMainWindowClose; window.Show(); }
            }
            catch (Exception ex)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics","error.txt"),ex.ToString());
                if (!e.Args.Contains("--capture")) MessageBox.Show(ex.Message,"SAB UI Preview");
                Shutdown(1);
            }
        }
    }
}
