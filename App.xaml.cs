using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BetterHI3Launcher
{
	public partial class App : Application
	{
		public static readonly LauncherVersion LocalLauncherVersion = new LauncherVersion("1.2.20210810.0");
		public static readonly string LauncherRootPath = AppDomain.CurrentDomain.BaseDirectory;
		public static readonly string LocalLowPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}Low";
		public static readonly string LauncherDataPath = Path.Combine(LocalLowPath, @"Bp\Better HI3 Launcher");
		public static readonly string LauncherBackgroundsPath = Path.Combine(LauncherDataPath, "Backgrounds");
		public static readonly string LauncherLogFile = Path.Combine(LauncherDataPath, "BetterHI3Launcher-latest.log");
		public static readonly string LauncherTranslationsFile = Path.Combine(LauncherDataPath, "BetterHI3Launcher-translations.json");
		public static string UserAgent = $"BetterHI3Launcher v{LocalLauncherVersion}";
		public static string LauncherExeName, LauncherPath, LauncherArchivePath, LauncherLanguage;
		public static readonly string OSVersion = BpUtility.GetWindowsVersion();
		public static readonly string OSLanguage = CultureInfo.CurrentUICulture.ToString();
		public static string[] CommandLineArgs = Environment.GetCommandLineArgs();
		public static RegistryKey LauncherRegKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Bp\Better HI3 Launcher");
		public static bool DisableAutoUpdate, DisableLogging, DisableTranslations, DisableSounds, AdvancedFeatures;
		public static bool FirstLaunch = LauncherRegKey.GetValue("LauncherVersion") == null ? true : false;
		public static Dictionary<string, string> TextStrings = new Dictionary<string, string>();
		public static Mutex Mutex = null;

		public App() : base()
		{
			SetupUnhandledExceptionHandling();
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			var culture = CultureInfo.InvariantCulture;
			try
			{
				Mutex = new Mutex(true, "BetterHI3Launcher", out bool new_mutex);
				if(!new_mutex)
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
			#if DEBUG
			WinConsole.Initialize();
			UserAgent += " [DEBUG]";
			#endif
			TextStrings_English();
			switch(OSLanguage)
			{
				case "de-AT":
				case "de-CH":
				case "de-DE":
				case "de-LI":
				case "de-LU":
					LauncherLanguage = "de";
					break;
				case "es-AR":
				case "es-BO":
				case "es-CL":
				case "es-CO":
				case "es-CR":
				case "es-DO":
				case "es-EC":
				case "es-ES":
				case "es-GT":
				case "es-HN":
				case "es-MX":
				case "es-NI":
				case "es-PA":
				case "es-PE":
				case "es-PR":
				case "es-PY":
				case "es-SV":
				case "es-US":
				case "es-UY":
					LauncherLanguage = "es";
					break;
				case "fr-BE":
				case "fr-CA":
				case "fr-CH":
				case "fr-FR":
				case "fr-LU":
				case "fr-MC":
					LauncherLanguage = "fr";
					break;
				case "pt-BR":
					LauncherLanguage = "pt-BR";
					break;
				case "pt-PT":
					LauncherLanguage = "pt-PT";
					break;
				case "ru-RU":
				case "uk-UA":
				case "be-BY":
					LauncherLanguage = "ru";
					break;
				case "sr-Cyrl-BA":
				case "sr-Cyrl-CS":
				case "sr-Cyrl-ME":
				case "sr-Cyrl-RS":
				case "sr-Latn-BA":
				case "sr-Latn-CS":
				case "sr-Latn-ME":
				case "sr-Latn-RS":
					LauncherLanguage = "sr";
					break;
				case "th-TH":
					LauncherLanguage = "th";
					break;
				case "vi-VN":
					LauncherLanguage = "vi";
					break;
				default:
					LauncherLanguage = "en";
					break;
			}
			base.OnStartup(e);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			if(Mutex != null)
			{
				Mutex.Dispose();
			}
			base.OnExit(e);
		}

		private void SetupUnhandledExceptionHandling()
		{
			// Catch exceptions from all threads in the AppDomain.
			AppDomain.CurrentDomain.UnhandledException += (sender, args) => ShowUnhandledException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");

			// Catch exceptions from each AppDomain that uses a task scheduler for async operations.
			TaskScheduler.UnobservedTaskException += (sender, args) => ShowUnhandledException(args.Exception, "TaskScheduler.UnobservedTaskException");

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

		private void ShowUnhandledException(Exception e, string unhandledExceptionType)
		{
			if(unhandledExceptionType == "TaskScheduler.UnobservedTaskException")
			{
				return;
			}

			string msg = $"CRITICAL ERROR: Unhandled exception occurred. Stack trace:\n{e}";
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine('\n' + msg);
			Directory.CreateDirectory(LauncherDataPath);
			if(File.Exists(LauncherLogFile))
			{
				File.SetAttributes(LauncherLogFile, File.GetAttributes(LauncherLogFile) & ~FileAttributes.ReadOnly);
			}
			File.AppendAllText(LauncherLogFile, '\n' + msg);
			if(MessageBox.Show(TextStrings["msgbox_unhandled_exception_msg"], TextStrings["msgbox_generic_error_title"], MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
			{
				BpUtility.StartProcess(LauncherLogFile, null, null, true);
			}
			Current.Shutdown();
		}
		
		public struct LauncherVersion
		{
			private int major, minor, date, hotfix;

			internal LauncherVersion(int _major, int _minor, int _date, int _hotfix)
			{
				major = _major;
				minor = _minor;
				date = _date;
				hotfix = _hotfix;
			}

			internal LauncherVersion(string _version)
			{
				string[] _version_strings = _version.Split('.');
				if(_version_strings.Length != 4)
				{
					major = 0;
					minor = 0;
					date = 0;
					hotfix = 0;
					return;
				}

				major = int.Parse(_version_strings[0]);
				minor = int.Parse(_version_strings[1]);
				date = int.Parse(_version_strings[2]);
				hotfix = int.Parse(_version_strings[3]);
			}

			internal bool IsNewerThan(LauncherVersion _other_version)
			{
				if(major >= _other_version.major && minor >= _other_version.minor && date >= _other_version.date)
				{
					if(major > _other_version.major)
					{
						return true;
					}
					else if(minor > _other_version.minor)
					{
						return true;
					}
					else if(date > _other_version.date)
					{
						return true;
					}
					else if(hotfix > _other_version.hotfix)
					{
						return true;
					}
				}
				return false;
			}

			public override string ToString()
			{
				return $"{major}.{minor}.{date}.{hotfix}";
			}
		}
	}
}