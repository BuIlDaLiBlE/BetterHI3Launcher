using System.Threading;
using System.Windows;

namespace BetterHI3Launcher
{
    public partial class App : Application
    {
        // Single instance mutex solution https://stackoverflow.com/a/47849014/7570821
        static Mutex m = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "BetterHI3Launcher";
            bool createdNew = false;

            try
            {
                // Initializes a new instance of the Mutex class with a Boolean value that indicates 
                // whether the calling thread should have initial ownership of the mutex, a string that is the name of the mutex, 
                // and a Boolean value that, when the method returns, indicates whether the calling thread was granted initial ownership of the mutex.
                m = new Mutex(true, mutexName, out createdNew);

                if(!createdNew)
                {
                    Current.Shutdown(); // Exit the application
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
            if(m != null)
            {
                m.Dispose();
            }
            base.OnExit(e);
        }
    }
}
