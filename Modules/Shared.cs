using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace BetterHI3Launcher
{
	public enum LauncherStatus
	{
		Ready, Error, CheckingUpdates, Downloading, Updating, Verifying, Unpacking, UpdateAvailable, Uninstalling, Working, DownloadPaused, Running, Preloading, PreloadVerifying
	}
	public enum HI3Server
	{
		GLB, SEA, CN, TW, KR, JP
	}
	public enum HI3Mirror
	{
		miHoYo, Hi3Mirror
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

	public struct GameVersion
	{
		private int major, minor, patch;

		internal GameVersion(int _major, int _minor, int _patch)
		{
			major = _major;
			minor = _minor;
			patch = _patch;
		}

		internal GameVersion(string _version)
		{
			string[] _version_strings = _version.Split('.', '_');
			if(_version_strings.Length < 3 || _version_strings.Length > 4)
			{
				major = 0;
				minor = 0;
				patch = 0;
				return;
			}

			major = int.Parse(_version_strings[0]);
			minor = int.Parse(_version_strings[1]);
			patch = int.Parse(_version_strings[2]);
		}

		internal bool IsNewerThan(GameVersion _other_version)
		{
			int old_version = int.Parse(string.Format("{0}{1}{2}", _other_version.major, _other_version.minor, _other_version.patch));
			int new_version = int.Parse(string.Format("{0}{1}{2}", major, minor, patch));

			if(new_version > old_version)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public override string ToString()
		{
			return $"{major}.{minor}.{patch}";
		}
	}

	public class HttpProp
	{
		public HttpProp(string URL, string Out)
		{
			this.URL = URL;
			this.Out = Out;
		}
		public string URL { get; private set; }
		public string Out { get; private set; }
		public byte Thread => (byte)App.ParallelDownloadSessions;
	}

	public partial class MainWindow
	{
		private void OnGameExit()
		{
			Dispatcher.Invoke(() =>
			{
				LaunchButton.Content = App.TextStrings["button_launch"];
				if(!PreloadDownload)
				{
					Status = LauncherStatus.Ready;
				}
			});
		}

		public bool DeleteFile(string path, bool ignore_read_only = false)
		{
			try
			{
				if(File.Exists(path))
				{
					if(ignore_read_only)
					{
						File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
					}
					File.Delete(path);
				}
				return true;
			}
			catch
			{
				Log($"Failed to delete {path}", true, 2);
				return false;
			}
		}

		public void Log(string msg, bool newline = true, int type = 0)
		{
			if(string.IsNullOrEmpty(msg))
			{
				return;
			}

			Color color;
			#if DEBUG
			ConsoleColor ccolor;
			#endif
			switch(type)
			{
				case 1:
					msg = "ERROR: " + msg;
					color = Colors.Red;
					#if DEBUG
					ccolor = ConsoleColor.Red;
					#endif
					break;
				case 2:
					msg = "WARNING: " + msg;
					color = Colors.Yellow;
					#if DEBUG
					ccolor = ConsoleColor.Yellow;
					#endif
					break;
				default:
					color = Colors.White;
					#if DEBUG
					ccolor = ConsoleColor.Gray;
					#endif
					break;
			}
			#if DEBUG
			Console.ForegroundColor = ccolor;
			if(newline)
			{
				Console.Write('\n' + msg);
			}
			else
			{
				Console.Write(msg);
			}
			#endif
			Dispatcher.Invoke(() =>
			{
				if(newline)
				{
					var brush = new SolidColorBrush(color);
					var run = new Run()
					{
						Text = msg,
						Foreground = brush
					};
					var para = new Paragraph(run)
					{
						Margin = new Thickness(0)
					};
					LogBoxRichTextBox.Document.Blocks.Add(para);
				}
				else
				{
					LogBoxRichTextBox.AppendText(msg);
				}
				if(!LogBoxScrollViewer.AreAnyTouchesCaptured)
				{
					LogBoxScrollViewer.ScrollToEnd();
				}
			});
			if(!App.DisableLogging)
			{
				try
				{
					Directory.CreateDirectory(App.LauncherDataPath);
					if(File.Exists(App.LauncherLogFile))
					{
						File.SetAttributes(App.LauncherLogFile, File.GetAttributes(App.LauncherLogFile) & ~FileAttributes.ReadOnly);
					}
					if(newline)
					{
						File.AppendAllText(App.LauncherLogFile, '\n' + msg);
					}
					else
					{
						File.AppendAllText(App.LauncherLogFile, msg);
					}
				}
				catch
				{
					App.DisableLogging = true;
					Log("Unable to write to log file, disabling logging to file...", true, 2);
				}
			}
		}
	}
}