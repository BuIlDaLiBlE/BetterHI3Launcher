using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BetterHI3Launcher
{
	public partial class App : Application
	{
		public static readonly string OSVersion = BpUtility.GetWindowsVersion();
		public static readonly string OSLanguage = CultureInfo.CurrentUICulture.ToString();
		static Mutex mutex = null;

		public App() : base()
		{
			SetupUnhandledExceptionHandling();
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			var culture = CultureInfo.InvariantCulture;
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
			Thread.CurrentThread.CurrentCulture = culture;
			Thread.CurrentThread.CurrentUICulture = culture;
			CultureInfo.DefaultThreadCurrentCulture = culture;
			CultureInfo.DefaultThreadCurrentUICulture = culture;
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

		private void SetupUnhandledExceptionHandling()
		{
			// Catch exceptions from all threads in the AppDomain.
			AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
				ShowUnhandledException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");

			// Catch exceptions from each AppDomain that uses a task scheduler for async operations.
			TaskScheduler.UnobservedTaskException += (sender, args) =>
				ShowUnhandledException(args.Exception, "TaskScheduler.UnobservedTaskException");

			// Catch exceptions from a single specific UI dispatcher thread.
			Dispatcher.UnhandledException += (sender, args) =>
			{
				// If we are debugging, let Visual Studio handle the exception and take us to the code that threw it.
				if(!Debugger.IsAttached)
				{
					args.Handled = true;
					ShowUnhandledException(args.Exception, "Dispatcher.UnhandledException");
				}
			};
		}

		void ShowUnhandledException(Exception e, string unhandledExceptionType)
		{
			if(unhandledExceptionType == "TaskScheduler.UnobservedTaskException")
				return;

			MessageBox.Show($"Unhandled exception occurred. Please report this.\n\n{e}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			Current.Shutdown();
		}
	}
}