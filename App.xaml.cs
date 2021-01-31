using System.Threading;
using System.Windows;

namespace BetterHI3Launcher
{
    public partial class App : Application
    {
        static Mutex mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew = false;

            try
            {
                mutex = new Mutex(true, "BetterHI3Launcher", out createdNew);

                if(!createdNew)
                {
                    Shutdown();
                }
            }
            catch
            {
                throw;
            }
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if(mutex != null)
            {
                mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
