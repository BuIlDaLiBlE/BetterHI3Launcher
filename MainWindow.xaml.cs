using IniParser;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using PartialZip;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace BetterHI3Launcher
{
	enum LauncherStatus
	{
		Ready, Error, CheckingUpdates, Downloading, Updating, Verifying, Unpacking, CleaningUp, UpdateAvailable, Uninstalling, Working, DownloadPaused, Running, Preloading, PreloadVerifying
	}
	enum HI3Server
	{
		Global, SEA
	}
	enum HI3Mirror
	{
		miHoYo, MediaFire, GoogleDrive
	}

	public partial class MainWindow : Window
	{
		public static readonly string miHoYoPath = Path.Combine(App.LocalLowPath, "miHoYo");
		public static readonly string GameExeName = "BH3.exe";
		public static string GameInstallPath, GameCachePath, GameRegistryPath, GameArchivePath, GameArchiveName, GameExePath, CacheArchivePath, LauncherExeName, LauncherPath, LauncherArchivePath;
		public static string RegistryVersionInfo;
		public static string GameRegistryLocalVersionRegValue, GameWebProfileURL, GameFullName;
		public static string[] CommandLineArgs = Environment.GetCommandLineArgs();
		public static bool FirstLaunch = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Bp\Better HI3 Launcher") == null ? true : false;
		public static bool DisableAutoUpdate, DisableLogging, DisableTranslations, DisableSounds, AdvancedFeatures, DownloadPaused, PatchDownload;
		public static int PatchDownloadInt;
		public static RegistryKey LauncherRegKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Bp\Better HI3 Launcher");
		public dynamic LocalVersionInfo, OnlineVersionInfo, OnlineRepairInfo, miHoYoVersionInfo, GameGraphicSettings, GameScreenSettings, GameCacheMetadata, GameCacheMetadataNumeric;
		LauncherStatus _status;
		HI3Server _gameserver;
		HI3Mirror _downloadmirror;
		DownloadPauseable download;
		DownloadProgressTracker tracker = new DownloadProgressTracker(50, TimeSpan.FromMilliseconds(500));

		internal LauncherStatus Status
		{
			get => _status;
			set
			{
				Dispatcher.Invoke(() =>
				{
					void ToggleUI(bool val)
					{
						LaunchButton.IsEnabled = val;
						OptionsButton.IsEnabled = val;
						ServerDropdown.IsEnabled = val;
						MirrorDropdown.IsEnabled = val;
						ToggleContextMenuItems(val);
					}
					void ToggleProgressBar(bool val)
					{
						ProgressBar.Visibility = val ? Visibility.Visible : Visibility.Hidden;
						ProgressBar.IsIndeterminate = true;
						TaskbarItemInfo.ProgressState = val ? TaskbarItemProgressState.Indeterminate : TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
					}

					_status = value;
					WindowState = WindowState.Normal;
					switch(_status)
					{
						case LauncherStatus.Ready:
							ProgressText.Text = string.Empty;
							ToggleUI(true);
							ToggleProgressBar(false);
							ProgressBar.IsIndeterminate = false;
							break;
						case LauncherStatus.Error:
							ProgressText.Text = App.TextStrings["progresstext_error"];
							ToggleUI(false);
							ToggleProgressBar(false);
							ProgressBar.IsIndeterminate = false;
							ShowLogCheckBox.IsChecked = true;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
							break;
						case LauncherStatus.CheckingUpdates:
							ProgressText.Text = App.TextStrings["progresstext_checking_update"];
							PreloadGrid.Visibility = Visibility.Collapsed;
							ToggleUI(false);
							ToggleProgressBar(true);
							break;
						case LauncherStatus.Downloading:
							DownloadPaused = false;
							ProgressText.Text = App.TextStrings["progresstext_initiating_download"];
							LaunchButton.Content = App.TextStrings["button_downloading"];
							ToggleUI(false);
							ToggleProgressBar(true);
							ProgressBar.IsIndeterminate = false;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
							break;
						case LauncherStatus.DownloadPaused:
							DownloadPaused = true;
							ProgressText.Text = string.Empty;
							ToggleUI(true);
							ToggleProgressBar(false);
							ToggleContextMenuItems(false);
							ProgressBar.IsIndeterminate = false;
							break;
						case LauncherStatus.Preloading:
							PreloadBottomText.Text = App.TextStrings["button_downloading"];
							PreloadButton.Visibility = Visibility.Collapsed;
							PreloadPauseButton.IsEnabled = true;
							PreloadPauseButton.Visibility = Visibility.Visible;
							PreloadPauseButton.Background = (ImageBrush)Resources["PreloadPauseButton"];
							PreloadCircle.Visibility = Visibility.Visible;
							PreloadCircleProgressBar.Visibility = Visibility.Visible;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
							ServerDropdown.IsEnabled = false;
							MirrorDropdown.IsEnabled = false;
							ToggleContextMenuItems(false);
							break;
						case LauncherStatus.PreloadVerifying:
							PreloadPauseButton.IsEnabled = false;
							PreloadCircleProgressBar.Value = 0;
							PreloadBottomText.Text = App.TextStrings["label_verifying"];
							PreloadStatusMiddleRightText.Text = string.Empty;
							PreloadStatusBottomRightText.Text = string.Empty;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
							break;
						case LauncherStatus.Working:
							ToggleUI(false);
							ToggleProgressBar(true);
							break;
						case LauncherStatus.Running:
							ProgressText.Text = string.Empty;
							LaunchButton.Content = App.TextStrings["button_running"];
							ToggleUI(false);
							OptionsButton.IsEnabled = true;
							ToggleProgressBar(false);
							ToggleContextMenuItems(false);
							ProgressBar.IsIndeterminate = false;
							break;
						case LauncherStatus.Verifying:
							ProgressText.Text = App.TextStrings["progresstext_verifying_files"];
							ToggleUI(false);
							ToggleProgressBar(true);
							break;
						case LauncherStatus.Unpacking:
							ProgressText.Text = App.TextStrings["progresstext_unpacking_1"];
							ToggleProgressBar(true);
							ProgressBar.IsIndeterminate = false;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
							break;
						case LauncherStatus.CleaningUp:
							ProgressText.Text = App.TextStrings["progresstext_cleaning_up"];
							break;
						case LauncherStatus.UpdateAvailable:
							ToggleUI(true);
							ToggleProgressBar(false);
							ToggleContextMenuItems(false, true);
							ProgressBar.IsIndeterminate = false;
							break;
						case LauncherStatus.Uninstalling:
							ProgressText.Text = App.TextStrings["progresstext_uninstalling"];
							ToggleUI(false);
							ToggleProgressBar(true);
							break;
					}
				});
			}
		}
		internal HI3Server Server
		{
			get => _gameserver;
			set
			{
				_gameserver = value;
				switch(_gameserver)
				{
					case HI3Server.Global:
						RegistryVersionInfo = "VersionInfoGlobal";
						GameFullName = "Honkai Impact 3rd";
						GameWebProfileURL = "https://global.user.honkaiimpact3.com";
						break;
					case HI3Server.SEA:
						RegistryVersionInfo = "VersionInfoSEA";
						GameFullName = "Honkai Impact 3";
						GameWebProfileURL = "https://asia.user.honkaiimpact3.com";
						break;
				}
				GameRegistryPath = $@"SOFTWARE\miHoYo\{GameFullName}";
				GameCachePath = Path.Combine(miHoYoPath, GameFullName);
				GameRegistryLocalVersionRegValue = null;
				var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
				if(key != null)
				{
					var version_candidates = new List<string>();
					var version_candidate_values = new List<string>();
					foreach(string regValue in key.GetValueNames())
					{
						if(regValue.Contains("LocalVersion_h"))
						{
							version_candidates.Add(regValue);
							version_candidate_values.Add(Encoding.UTF8.GetString((byte[])key.GetValue(regValue)));
						}
					}
					if(version_candidates.Count > 0)
					{
						string version_candidate = null;
						int version_candidate_major = 0;
						int version_candidate_minor = 0;
						int version_candidate_patch = 0;
						for(int i = 0; i < version_candidates.Count; i++)
						{
							if(version_candidate_values[i].Length < 5)
							{
								continue;
							}
							int major = (int)char.GetNumericValue(version_candidate_values[i][0]);
							int minor = (int)char.GetNumericValue(version_candidate_values[i][2]);
							int patch = (int)char.GetNumericValue(version_candidate_values[i][4]);
							if(version_candidate_major < major)
							{
								version_candidate_major = major;
								version_candidate = version_candidates[i];
							}
							if(version_candidate_minor < minor)
							{
								version_candidate_minor = minor;
								version_candidate = version_candidates[i];
							}
							if(version_candidate_patch < patch)
							{
								version_candidate_patch = patch;
								version_candidate = version_candidates[i];
							}
						}
						GameRegistryLocalVersionRegValue = version_candidate;
					}
				}
			}
		}
		internal HI3Mirror Mirror
		{
			get => _downloadmirror;
			set
			{
				_downloadmirror = value;
			}
		}

		public MainWindow()
		{
			InitializeComponent();
			var args = new List<string>();
			for(int i = 1; i < CommandLineArgs.Length; i++)
			{
				args.Add(CommandLineArgs[i].ToUpper());
			}
			if(args.Contains("NOLOG"))
			{
				DisableLogging = true;
			}
			if(!DisableLogging)
			{
				try
				{
					if(File.Exists(App.LauncherLogFile))
					{
						string old_log_path_1 = Path.Combine(App.LauncherDataPath, "BetterHI3Launcher-old1.log");
						for(int i = 9; i > 0; i--)
						{
							string old_log_path_2 = Path.Combine(App.LauncherDataPath, $"BetterHI3Launcher-old{i}.log");
							if(File.Exists(old_log_path_2))
							{
								string old_log_path_3 = Path.Combine(App.LauncherDataPath, $"BetterHI3Launcher-old{i + 1}.log");
								string old_log_path_4 = Path.Combine(App.LauncherDataPath, "BetterHI3Launcher-old10.log");
								if(File.Exists(old_log_path_4))
								{
									File.Delete(old_log_path_4);
								}
								File.Move(old_log_path_2, old_log_path_3);
							}
						}
						File.Move(App.LauncherLogFile, old_log_path_1);
					}
				}
				catch
				{
					Log("WARNING: Unable to rename log files", true, 2);
				}
			}
			DeleteFile(App.LauncherLogFile, true);
			Log(App.UserAgent, false);
			Log($"Working directory: {App.LauncherRootPath}");
			Log($"OS version: {App.OSVersion}");
			Log($"OS language: {App.OSLanguage}");
			#if !DEBUG
			if(args.Contains("NOUPDATE"))
			{
				DisableAutoUpdate = true;
				App.UserAgent += " [NOUPDATE]";
				Log("Auto-update disabled");
			}
			#endif
			if(args.Contains("NOLOG"))
			{
				App.UserAgent += " [NOLOG]";
				Log("Logging to file disabled");
			}
			if(args.Contains("NOTRANSLATIONS"))
			{
				DisableTranslations = true;
				App.LauncherLanguage = "en";
				App.UserAgent += " [NOTRANSLATIONS]";
				Log("Translations disabled, only English will be available");
			}
			if(args.Contains("ADVANCED"))
			{
				AdvancedFeatures = true;
				App.UserAgent += " [ADVANCED]";
				Log("Advanced features enabled");
			}
			else
			{
				RepairBoxGenerateButton.Visibility = Visibility.Collapsed;
			}
			string language_log_msg = $"Launcher language: {App.LauncherLanguage}";
			var language_reg = LauncherRegKey.GetValue("Language");
			if(!DisableTranslations)
			{
				if(language_reg != null)
				{
					if(LauncherRegKey.GetValueKind("Language") == RegistryValueKind.String)
					{
						SetLanguage(language_reg.ToString());
					}
				}
				else
				{
					SetLanguage(App.LauncherLanguage);
					language_log_msg += " (autodetect)";
				}
			}
			Log(language_log_msg);
			App.UserAgent += $" [{App.LauncherLanguage}] [{App.OSVersion}]";

			LaunchButton.Content = App.TextStrings["button_download"];
			OptionsButton.Content = App.TextStrings["button_options"];
			ServerLabel.Text = $"{App.TextStrings["label_server"]}:";
			MirrorLabel.Text = $"{App.TextStrings["label_mirror"]}:";
			IntroBoxTitleTextBlock.Text = App.TextStrings["introbox_title"];
			IntroBoxMessageTextBlock.Text = App.TextStrings["introbox_msg"];
			IntroBoxOKButton.Content = App.TextStrings["button_ok"];
			DownloadCacheBoxTitleTextBlock.Text = App.TextStrings["contextmenu_download_cache"];
			DownloadCacheBoxFullCacheButton.Content = App.TextStrings["downloadcachebox_button_full_cache"];
			DownloadCacheBoxNumericFilesButton.Content = App.TextStrings["downloadcachebox_button_numeric_files"];
			DownloadCacheBoxCancelButton.Content = App.TextStrings["button_cancel"];
			RepairBoxTitleTextBlock.Text = App.TextStrings["contextmenu_repair"];
			RepairBoxYesButton.Content = App.TextStrings["button_yes"];
			RepairBoxNoButton.Content = App.TextStrings["button_no"];
			RepairBoxGenerateButton.Content = App.TextStrings["button_generate"];
			FPSInputBoxTitleTextBlock.Text = App.TextStrings["fpsinputbox_title"];
			CombatFPSInputBoxTextBlock.Text = App.TextStrings["fpsinputbox_label_combatfps"];
			MenuFPSInputBoxTextBlock.Text = App.TextStrings["fpsinputbox_label_menufps"];
			FPSInputBoxOKButton.Content = App.TextStrings["button_confirm"];
			FPSInputBoxCancelButton.Content = App.TextStrings["button_cancel"];
			ResolutionInputBoxTitleTextBlock.Text = App.TextStrings["resolutioninputbox_title"];
			ResolutionInputBoxWidthTextBlock.Text = $"{App.TextStrings["resolutioninputbox_label_width"]}:";
			ResolutionInputBoxHeightTextBlock.Text = $"{App.TextStrings["resolutioninputbox_label_height"]}:";
			ResolutionInputBoxFullscreenTextBlock.Text = $"{App.TextStrings["resolutioninputbox_label_fullscreen"]}:";
			ResolutionInputBoxOKButton.Content = App.TextStrings["button_confirm"];
			ResolutionInputBoxCancelButton.Content = App.TextStrings["button_cancel"];
			ChangelogBoxTitleTextBlock.Text = App.TextStrings["changelogbox_title"];
			ChangelogBoxMessageTextBlock.Text = App.TextStrings["changelogbox_1_msg"];
			ChangelogBoxOKButton.Content = App.TextStrings["button_ok"];
			AboutBoxTitleTextBlock.Text = App.TextStrings["contextmenu_about"];
			AboutBoxAppNameTextBlock.Text += $" v{App.LocalLauncherVersion}";
			AboutBoxMessageTextBlock.Text = $"{App.TextStrings["aboutbox_msg"]}\n\nMade by Bp (BuIlDaLiBlE production).";
			AboutBoxGitHubButton.Content = App.TextStrings["button_github"];
			AboutBoxOKButton.Content = App.TextStrings["button_ok"];
			ShowLogLabel.Text = App.TextStrings["label_log"];
			PreloadTopText.Text = App.TextStrings["label_preload"];
			PreloadStatusTopLeftText.Text = App.TextStrings["label_downloaded_2"];
			PreloadStatusMiddleLeftText.Text = App.TextStrings["label_eta"];
			PreloadStatusBottomLeftText.Text = App.TextStrings["label_speed"];

			Grid.MouseLeftButtonDown += delegate{DragMove();};
			PreloadGrid.Visibility = Visibility.Collapsed;
			LogBox.Visibility = Visibility.Collapsed;
			LogBoxRichTextBox.Document.PageWidth = LogBox.Width;
			IntroBox.Visibility = Visibility.Collapsed;
			RepairBox.Visibility = Visibility.Collapsed;
			FPSInputBox.Visibility = Visibility.Collapsed;
			ResolutionInputBox.Visibility = Visibility.Collapsed;
			DownloadCacheBox.Visibility = Visibility.Collapsed;
			ChangelogBox.Visibility = Visibility.Collapsed;
			AboutBox.Visibility = Visibility.Collapsed;

			OptionsContextMenu.Items.Clear();
			var CM_Download_Cache = new MenuItem{Header = App.TextStrings["contextmenu_download_cache"]};
			CM_Download_Cache.Click += async (sender, e) => await CM_DownloadCache_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Download_Cache);
			var CM_Fix_Subtitles = new MenuItem{Header = App.TextStrings["contextmenu_fix_subtitles"]};
			CM_Fix_Subtitles.Click += async (sender, e) => await CM_FixSubtitles_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Fix_Subtitles);
			var CM_Repair = new MenuItem{Header = App.TextStrings["contextmenu_repair"]};
			CM_Repair.Click += async (sender, e) => await CM_Repair_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Repair);
			var CM_Move = new MenuItem{Header = App.TextStrings["contextmenu_move"]};
			CM_Move.Click += async (sender, e) => await CM_Move_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Move);
			var CM_Uninstall = new MenuItem{Header = App.TextStrings["contextmenu_uninstall"]};
			CM_Uninstall.Click += async (sender, e) => await CM_Uninstall_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Uninstall);
			var CM_Game_Settings = new MenuItem{Header = App.TextStrings["contextmenu_game_settings"]};
			var CM_Custom_FPS = new MenuItem{Header = App.TextStrings["contextmenu_custom_fps"]};
			CM_Custom_FPS.Click += (sender, e) => CM_CustomFPS_Click(sender, e);
			CM_Game_Settings.Items.Add(CM_Custom_FPS);
			var CM_Custom_Resolution = new MenuItem{Header = App.TextStrings["contextmenu_custom_resolution"]};
			CM_Custom_Resolution.Click += (sender, e) => CM_CustomResolution_Click(sender, e);
			CM_Game_Settings.Items.Add(CM_Custom_Resolution);
			var CM_Download_Type = new MenuItem{Header = App.TextStrings["contextmenu_reset_download_type"]};
			CM_Download_Type.Click += (sender, e) => CM_ResetDownloadType_Click(sender, e);
			CM_Game_Settings.Items.Add(CM_Download_Type);
			var CM_Reset_Game_Settings = new MenuItem{Header = App.TextStrings["contextmenu_reset_game_settings"]};
			CM_Reset_Game_Settings.Click += (sender, e) => CM_ResetGameSettings_Click(sender, e);
			CM_Game_Settings.Items.Add(CM_Reset_Game_Settings);
			OptionsContextMenu.Items.Add(CM_Game_Settings);
			OptionsContextMenu.Items.Add(new Separator());
			var CM_Web_Profile = new MenuItem{Header = App.TextStrings["contextmenu_web_profile"]};
			CM_Web_Profile.Click += (sender, e) => BpUtility.StartProcess(GameWebProfileURL, null, App.LauncherRootPath, true);
			OptionsContextMenu.Items.Add(CM_Web_Profile);
			var CM_Feedback = new MenuItem{Header = App.TextStrings["contextmenu_feedback"]};
			CM_Feedback.Click += (sender, e) => BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new/choose", null, App.LauncherRootPath, true);
			OptionsContextMenu.Items.Add(CM_Feedback);
			var CM_Changelog = new MenuItem{Header = App.TextStrings["contextmenu_changelog"]};
			CM_Changelog.Click += (sender, e) => CM_Changelog_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Changelog);
			var CM_Custom_Background = new MenuItem{Header = App.TextStrings["contextmenu_custom_background"]};
			CM_Custom_Background.Click += (sender, e) => CM_CustomBackground_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Custom_Background);
			var CM_Sounds = new MenuItem{Header = App.TextStrings["contextmenu_sounds"], IsChecked = true};
			CM_Sounds.Click += (sender, e) => CM_Sounds_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Sounds);
			var CM_Language = new MenuItem{Header = App.TextStrings["contextmenu_language"]};
			var CM_Language_System = new MenuItem{Header = App.TextStrings["contextmenu_language_system"]};
			CM_Language_System.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_System);
			var CM_Language_English = new MenuItem{Header = App.TextStrings["contextmenu_language_english"]};
			CM_Language_English.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_English);
			var CM_Language_Russian = new MenuItem{Header = App.TextStrings["contextmenu_language_russian"]};
			CM_Language_Russian.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Russian);
			var CM_Language_Spanish = new MenuItem{Header = App.TextStrings["contextmenu_language_spanish"]};
			CM_Language_Spanish.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Spanish);
			var CM_Language_Portuguese = new MenuItem{Header = App.TextStrings["contextmenu_language_portuguese"]};
			CM_Language_Portuguese.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Portuguese);
			var CM_Language_German = new MenuItem{Header = App.TextStrings["contextmenu_language_german"]};
			CM_Language_German.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_German);
			var CM_Language_Vietnamese = new MenuItem{Header = App.TextStrings["contextmenu_language_vietnamese"]};
			CM_Language_Vietnamese.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Vietnamese);
			var CM_Language_Serbian = new MenuItem{Header = App.TextStrings["contextmenu_language_serbian"]};
			CM_Language_Serbian.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Serbian);
			var CM_Language_Thai = new MenuItem {Header = App.TextStrings["contextmenu_language_thai"]};
			CM_Language_Thai.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Thai);
			CM_Language.Items.Add(new Separator());
			var CM_Language_Contribute = new MenuItem{Header = App.TextStrings["contextmenu_language_contribute"]};
			CM_Language_Contribute.Click += (sender, e) => BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher#contributing-translations", null, App.LauncherRootPath, true);
			CM_Language.Items.Add(CM_Language_Contribute);
			OptionsContextMenu.Items.Add(CM_Language);
			var CM_About = new MenuItem{Header = App.TextStrings["contextmenu_about"]};
			CM_About.Click += (sender, e) => CM_About_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_About);

			if(!DisableTranslations)
			{
				if(language_reg == null)
				{
					CM_Language_System.IsChecked = true;
				}
				else
				{
					switch(language_reg.ToString())
					{
						case "ru":
							CM_Language_Russian.IsChecked = true;
							break;
						case "es":
							CM_Language_Spanish.IsChecked = true;
							break;
						case "pt":
							CM_Language_Portuguese.IsChecked = true;
							break;
						case "de":
							CM_Language_German.IsChecked = true;
							break;
						case "vi":
							CM_Language_Vietnamese.IsChecked = true;
							break;
						case "sr":
							CM_Language_Serbian.IsChecked = true;
							break;
						case "th":
							CM_Language_Thai.IsChecked = true;
							break;
						default:
							CM_Language_English.IsChecked = true;
							break;
					}
				}
			}
			else
			{
				CM_Language_English.IsChecked = true;
				foreach(dynamic item in CM_Language.Items)
				{
					if(item.GetType() == typeof(MenuItem))
					{
						if(item.Header.ToString() == App.TextStrings["contextmenu_language_contribute"])
						{
							continue;
						}
					}
					item.IsEnabled = false;
				}
			}

			var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
			if(key == null || (int)key.GetValue("Release") < 394254)
			{
				MessageBox.Show(App.TextStrings["msgbox_net_version_old_msg"], App.TextStrings["msgbox_start_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
				Application.Current.Shutdown();
				return;
			}

			try
			{
				var last_selected_server_reg = LauncherRegKey.GetValue("LastSelectedServer");
				if(last_selected_server_reg != null)
				{
					if(LauncherRegKey.GetValueKind("LastSelectedServer") == RegistryValueKind.DWord)
					{
						if((int)last_selected_server_reg == 0)
						{
							Server = HI3Server.Global;
						}
						else if((int)last_selected_server_reg == 1)
						{
							Server = HI3Server.SEA;
						}
					}
				}
				else
				{
					Server = HI3Server.Global;
				}
				ServerDropdown.SelectedIndex = (int)Server;

				try
				{
					FetchOnlineVersionInfo();
				}
				catch(Exception ex)
				{
					if(Status == LauncherStatus.Error)
					{
						return;
					}
					Status = LauncherStatus.Error;
					MessageBox.Show($"{App.TextStrings["msgbox_conn_bp_error_msg"]}\n{ex}", App.TextStrings["msgbox_net_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
					Application.Current.Shutdown();
					return;
				}
				try
				{
					FetchmiHoYoVersionInfo();
				}
				catch(Exception ex)
				{
					if(Status == LauncherStatus.Error)
					{
						return;
					}
					Status = LauncherStatus.Error;
					MessageBox.Show($"{App.TextStrings["msgbox_conn_mihoyo_error_msg"]}\n{ex}", App.TextStrings["msgbox_net_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
					Application.Current.Shutdown();
					return;
				}

				var last_selected_mirror_reg = LauncherRegKey.GetValue("LastSelectedMirror");
				if(last_selected_mirror_reg != null)
				{
					if(LauncherRegKey.GetValueKind("LastSelectedMirror") == RegistryValueKind.DWord)
					{
						if((int)last_selected_mirror_reg == 0)
						{
							Mirror = HI3Mirror.miHoYo;
						}
						else if((int)last_selected_mirror_reg == 1)
						{
							Mirror = HI3Mirror.MediaFire;
						}
						else if((int)last_selected_mirror_reg == 2)
						{
							Mirror = HI3Mirror.GoogleDrive;
						}
					}
				}
				else
				{
					Mirror = HI3Mirror.miHoYo;
				}
				MirrorDropdown.SelectedIndex = (int)Mirror;

				var show_log_reg = LauncherRegKey.GetValue("ShowLog");
				if(show_log_reg != null)
				{
					if(LauncherRegKey.GetValueKind("ShowLog") == RegistryValueKind.DWord)
					{
						if((int)show_log_reg == 1)
						{
							ShowLogCheckBox.IsChecked = true;
						}
					}
				}

				var sounds_reg = LauncherRegKey.GetValue("Sounds");
				if(sounds_reg != null)
				{
					if(LauncherRegKey.GetValueKind("Sounds") == RegistryValueKind.DWord)
					{
						if((int)sounds_reg == 0)
						{
							DisableSounds = true;
							CM_Sounds.IsChecked = false;
						}
					}
				}

				Log($"Using server: {((ComboBoxItem)ServerDropdown.SelectedItem).Content as string}");
				Log($"Using mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");

				if(!DisableTranslations)
				{
					DownloadLauncherTranslations();
				}

				DownloadBackgroundImage();
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				MessageBox.Show(string.Format(App.TextStrings["msgbox_start_error_msg"], ex), App.TextStrings["msgbox_start_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
				Application.Current.Shutdown();
				return;
			}
		}

		private void FetchOnlineVersionInfo()
		{
			#if DEBUG
				var version_info_url = new[]{"https://bpnet.host/bh3?launcher_status=debug"};
			#else
				var version_info_url = new[]{"https://bpnet.host/bh3?launcher_status=prod", "https://serioussam.ucoz.ru/bbh3l_prod.json"};
			#endif
			string version_info;
			var web_client = new BpWebClient();
			try
			{
				version_info = web_client.DownloadString(version_info_url[0]);
			}
			catch
			{
				version_info = web_client.DownloadString(version_info_url[1]);
			}
			OnlineVersionInfo = JsonConvert.DeserializeObject<dynamic>(version_info);
			if(OnlineVersionInfo.status == "success")
			{
				OnlineVersionInfo = OnlineVersionInfo.launcher_status;
				LauncherExeName = OnlineVersionInfo.launcher_info.name;
				LauncherPath = Path.Combine(App.LauncherRootPath, LauncherExeName);
				LauncherArchivePath = Path.Combine(App.LauncherRootPath, OnlineVersionInfo.launcher_info.url.ToString().Substring(OnlineVersionInfo.launcher_info.url.ToString().LastIndexOf('/') + 1));
			}
			else
			{
				Status = LauncherStatus.Error;
				Dispatcher.Invoke(() =>
				{
					MessageBox.Show(string.Format(App.TextStrings["msgbox_net_error_msg"], OnlineVersionInfo.status_message), App.TextStrings["msgbox_net_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
					Application.Current.Shutdown();
				});
			}
		}

		private async void FetchChangelog()
		{
			if(ChangelogBoxTextBox.Text != string.Empty)
			{
				return;
			}

			string changelog;
			var web_client = new BpWebClient();

			Dispatcher.Invoke(() => {ChangelogBoxTextBox.Text = App.TextStrings["changelogbox_2_msg"];});
			await Task.Run(() =>
			{
				try
				{
					if(App.LauncherLanguage == "ru")
					{
						changelog = web_client.DownloadString(OnlineVersionInfo.launcher_info.changelog_url.ru.ToString());
					}
					else
					{
						changelog = web_client.DownloadString(OnlineVersionInfo.launcher_info.changelog_url.en.ToString());
					}
				}
				catch
				{
					changelog = App.TextStrings["changelogbox_3_msg"];
				}
				Dispatcher.Invoke(() => {ChangelogBoxTextBox.Text = changelog;});
			});
		}

		private void FetchmiHoYoVersionInfo()
		{
			string url;
			if(Server == HI3Server.Global)
			{
				url = OnlineVersionInfo.game_info.mirror.mihoyo.resource_info.global.ToString();
			}
			else
			{
				url = OnlineVersionInfo.game_info.mirror.mihoyo.resource_info.os.ToString();
			}
			var web_request = BpUtility.CreateWebRequest(url);
			using(var web_response = (HttpWebResponse)web_request.GetResponse())
			{
				using(var data = new MemoryStream())
				{
					web_response.GetResponseStream().CopyTo(data);
					miHoYoVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
					if(miHoYoVersionInfo.retcode == 0)
					{
						if(miHoYoVersionInfo.data != null)
						{
							miHoYoVersionInfo = miHoYoVersionInfo.data;
						}
						else
						{
							throw new WebException();
						}
					}
					else
					{
						throw new WebException(miHoYoVersionInfo.message.ToString());
					}
				}
			}
			GameArchiveName = Path.GetFileName(HttpUtility.UrlDecode(miHoYoVersionInfo.game.latest.path.ToString()));
			web_request = BpUtility.CreateWebRequest(miHoYoVersionInfo.game.latest.path.ToString(), "HEAD");
			using(var web_response = (HttpWebResponse)web_request.GetResponse())
			{
				miHoYoVersionInfo.size = web_response.ContentLength;
				miHoYoVersionInfo.last_modified = web_response.LastModified.ToUniversalTime().ToString();
			}
			Dispatcher.Invoke(() =>
			{
				GameVersionText.Text = $"{App.TextStrings["version"]}: v{miHoYoVersionInfo.game.latest.version.ToString()}";
			});
		}

		private DateTime FetchmiHoYoResourceVersionDateModified()
		{
			var url = new string[2];
			var time = new DateTime[2];
			if(Server == HI3Server.Global)
			{
				url[0] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.global[0].ToString();
				url[1] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.global[1].ToString();
			}
			else
			{
				url[0] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.os[0].ToString();
				url[1] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.os[1].ToString();
			}
			try
			{
				for(int i = 0; i < url.Length; i++)
				{
					var web_request = BpUtility.CreateWebRequest(url[i], "HEAD");
					using(var web_response = (HttpWebResponse)web_request.GetResponse())
					{
						time[i] = web_response.LastModified.ToUniversalTime();
					}
				}
				if(DateTime.Compare(time[0], time[1]) >= 0)
				{
					return time[0];
				}
				else
				{
					return time[1];
				}
			}
			catch
			{
				return new DateTime(0);
			}
		}

		private dynamic FetchMediaFireFileMetadata(string id, bool numeric)
		{
			if(string.IsNullOrEmpty(id))
			{
				throw new ArgumentNullException();
			}

			string url = $"https://www.mediafire.com/file/{id}";
			try
			{
				var web_request = BpUtility.CreateWebRequest(url, "HEAD");
				using(var web_response = (HttpWebResponse)web_request.GetResponse())
				{
					dynamic metadata = new ExpandoObject();
					metadata.title = web_response.Headers["Content-Disposition"].Replace("attachment; filename=", string.Empty).Replace("\"", string.Empty);
					metadata.modifiedDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
					metadata.downloadUrl = url;
					metadata.fileSize = web_response.ContentLength;
					if(!numeric)
					{
						if(Server == HI3Server.Global)
						{
							metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache.global.md5.ToString();
						}
						else
						{
							metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache.os.md5.ToString();
						}
					}
					else
					{
						if(Server == HI3Server.Global)
						{
							metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.md5.ToString();
						}
						else
						{
							metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.md5.ToString();
						}
					}
					return metadata;
				}
			}
			catch(WebException ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to fetch MediaFire file metadata:\n{ex}", true, 1);
				Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_mirror_error_msg"], ex.Message)).ShowDialog();});
			}
			return null;
		}

		private dynamic FetchGDFileMetadata(string id)
		{
			if(string.IsNullOrEmpty(id))
			{
				throw new ArgumentNullException();
			}

			string url = $"https://www.googleapis.com/drive/v2/files/{id}?key={OnlineVersionInfo.launcher_info.gd_key}";
			try
			{
				var web_request = BpUtility.CreateWebRequest(url);
				using(var web_response = (HttpWebResponse)web_request.GetResponse())
				{
					using(var data = new MemoryStream())
					{
						web_response.GetResponseStream().CopyTo(data);
						var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
						return json;
					}
				}
			}
			catch(WebException ex)
			{
				Status = LauncherStatus.Error;
				string msg;
				if(ex.Response != null)
				{
					using(var data = new MemoryStream())
					{
						ex.Response.GetResponseStream().CopyTo(data);
						var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
						if(json.error != null)
						{
							msg = json.error.errors[0].message;
						}
						else
						{
							msg = ex.Message;
						}
					}
				}
				else
				{
					msg = ex.Message;
				}
				Log($"ERROR: Failed to fetch Google Drive file metadata:\n{ex}", true, 1);
				Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_mirror_error_msg"], msg)).ShowDialog();});
			}
			return null;
		}

		private bool LauncherUpdateCheck()
		{
			var OnlineLauncherVersion = new App.LauncherVersion(OnlineVersionInfo.launcher_info.version.ToString());
			if(OnlineLauncherVersion.IsNewerThan(App.LocalLauncherVersion))
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		private async void GameUpdateCheck(bool server_changed = false)
		{
			if(Status == LauncherStatus.Error)
			{
				return;
			}

			Status = LauncherStatus.CheckingUpdates;
			Log("Checking for game update...");
			LocalVersionInfo = null;
			await Task.Run(() =>
			{
				FetchOnlineVersionInfo();
				try
				{
					int game_needs_update;
					long download_size = 0;
					if(Mirror == HI3Mirror.miHoYo)
					{
						// space_usage is probably when archive is unpacked, here I get the download size instead
						// download_size = (long)miHoYoVersionInfo.game.latest.size;
						download_size = miHoYoVersionInfo.size;
					}
					else if(Mirror == HI3Mirror.MediaFire)
					{
						dynamic mediafire_metadata;
						if(Server == HI3Server.Global)
						{
							mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString(), false);
						}
						else
						{
							mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString(), false);
						}
						if(mediafire_metadata == null)
						{
							Log("WARNING: Failed to use the current mirror, switching back to miHoYo", true, 2);
							Status = LauncherStatus.Ready;
							Dispatcher.Invoke(() => {MirrorDropdown.SelectedIndex = 0;});
							return;
						}
						download_size = mediafire_metadata.fileSize;
					}
					else if(Mirror == HI3Mirror.GoogleDrive)
					{
						dynamic gd_metadata;
						if(Server == HI3Server.Global)
						{
							gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
						}
						else
						{
							gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
						}
						if(gd_metadata == null)
						{
							Log("WARNING: Failed to use the current mirror, switching back to miHoYo", true, 2);
							Status = LauncherStatus.Ready;
							Dispatcher.Invoke(() => {MirrorDropdown.SelectedIndex = 0;});
							return;
						}
						download_size = gd_metadata.fileSize;
					}
					if(LauncherRegKey.GetValue(RegistryVersionInfo) != null)
					{
						LocalVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])LauncherRegKey.GetValue(RegistryVersionInfo)));
						GameInstallPath = LocalVersionInfo.game_info.install_path.ToString();
						var config_ini_file = Path.Combine(GameInstallPath, "config.ini");
						if(File.Exists(config_ini_file))
						{
							var data = new FileIniDataParser().ReadFile(config_ini_file);
							if(data["General"]["game_version"] != null)
							{
								if(data["General"]["game_version"] == miHoYoVersionInfo.game.latest.version.ToString())
								{
									LocalVersionInfo.game_info.installed = true;
								}
								LocalVersionInfo.game_info.version = data["General"]["game_version"];
							}
						}
						var local_game_version = new GameVersion(LocalVersionInfo.game_info.version.ToString());
						game_needs_update = GameVersionUpdateCheck(local_game_version);
						GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
						GameExePath = Path.Combine(GameInstallPath, "BH3.exe");

						Log($"Game version: {local_game_version}");
						Log($"Game directory: {GameInstallPath}");
						if(game_needs_update != 0)
						{
							PatchDownload = false;
							if(game_needs_update == 2 && Mirror == HI3Mirror.miHoYo)
							{
								var url = miHoYoVersionInfo.game.diffs[PatchDownloadInt].path.ToString();
								var web_request = BpUtility.CreateWebRequest(url, "HEAD");
								using(var web_response = (HttpWebResponse)web_request.GetResponse())
								{
									download_size = web_response.ContentLength;
								}
								GameArchiveName = Path.GetFileName(HttpUtility.UrlDecode(url));
								GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
								PatchDownload = true;
							}
							Log("Game requires an update!");
							Status = LauncherStatus.UpdateAvailable;
						}
						else if(LocalVersionInfo.game_info.installed == false)
						{
							DownloadPaused = true;
							Status = LauncherStatus.UpdateAvailable;
						}
						else if(!File.Exists(GameExePath))
						{
							Log("WARNING: Game executable is missing, resetting game version info...", true, 2);
							DeleteGameFiles();
							GameUpdateCheck();
							return;
						}
						else
						{
							var process = Process.GetProcessesByName("BH3");
							if(process.Length > 0)
							{
								process[0].EnableRaisingEvents = true;
								process[0].Exited += new EventHandler((object s, EventArgs ea) => {OnGameExit();});
								Status = LauncherStatus.Running;
							}
							else
							{
								Status = LauncherStatus.Ready;
								Dispatcher.Invoke(() => {LaunchButton.Content = App.TextStrings["button_launch"];});
							}
						}
						if(Status == LauncherStatus.UpdateAvailable)
						{
							if(File.Exists(GameArchivePath))
							{
								DownloadPaused = true;
								var remaining_size = download_size - new FileInfo(GameArchivePath).Length;
								Dispatcher.Invoke(() =>
								{
									if(remaining_size > 0)
									{
										ProgressText.Text = $"{App.TextStrings["progresstext_download_size"]}: {BpUtility.ToBytesCount(remaining_size)}";
									}
									else
									{
										ProgressText.Text = string.Empty;
									}
									LaunchButton.Content = App.TextStrings["button_update"];
								});
							}
							else
							{
								Dispatcher.Invoke(() =>
								{
									LaunchButton.Content = App.TextStrings["button_update"];
									ProgressText.Text = $"{App.TextStrings["progresstext_download_size"]}: {BpUtility.ToBytesCount(download_size)}";
								});
							}
						}
						else
						{
							Dispatcher.Invoke(() =>
							{
								if(miHoYoVersionInfo.pre_download_game != null)
								{
									var path = Path.Combine(GameInstallPath, Path.GetFileName(HttpUtility.UrlDecode(miHoYoVersionInfo.pre_download_game.latest.path.ToString())));
									if(File.Exists(path))
									{
										PreloadButton.Visibility = Visibility.Collapsed;
										PreloadCheckmark.Visibility = Visibility.Visible;
										PreloadCircle.Visibility = Visibility.Visible;
										PreloadCircleProgressBar.Visibility = Visibility.Visible;
										PreloadCircleProgressBar.Value = 100;
										PreloadBottomText.Text = App.TextStrings["label_done"];
									}
									else
									{
										PreloadButton.Visibility = Visibility.Visible;
										PreloadCheckmark.Visibility = Visibility.Collapsed;
										PreloadCircle.Visibility = Visibility.Collapsed;
										PreloadCircleProgressBar.Visibility = Visibility.Collapsed;
										PreloadCircleProgressBar.Value = 0;
										PreloadBottomText.Text = App.TextStrings["label_get_now"];
									}
									PreloadPauseButton.Visibility = Visibility.Collapsed;
									PreloadGrid.Visibility = Visibility.Visible;
								}
								else
								{
									PreloadGrid.Visibility = Visibility.Collapsed;
								}
							});
						}	
					}
					else
					{
						Log("Game is not installed :^(");
						if(server_changed)
						{
							FetchmiHoYoVersionInfo();
						}
						Status = LauncherStatus.Ready;
						Dispatcher.Invoke(() =>
						{
							LaunchButton.Content = App.TextStrings["button_download"];
							ProgressText.Text = $"{App.TextStrings["progresstext_download_size"]}: {BpUtility.ToBytesCount(download_size)}";
							ToggleContextMenuItems(false);
							var path = CheckForExistingGameDirectory(App.LauncherRootPath);
							if(string.IsNullOrEmpty(path))
							{
								path = CheckForExistingGameDirectory(Environment.ExpandEnvironmentVariables("%ProgramW6432%"));
							}
							if(path.Length < 4)
							{
								path = string.Empty;
							}
							if(!string.IsNullOrEmpty(path))
							{
								if(new DialogWindow(App.TextStrings["msgbox_install_title"], string.Format(App.TextStrings["msgbox_install_existing_dir_msg"], path), DialogWindow.DialogType.Question).ShowDialog() == true)
								{
									Log($"Existing install directory selected: {path}");
									GameInstallPath = path;
									var server = CheckForExistingGameClientServer();
									if(server >= 0)
									{
										if((int)Server != server)
										{
											ServerDropdown.SelectedIndex = server;
										}
										WriteVersionInfo(true, true);
										GameUpdateCheck();
									}
									else
									{
										Status = LauncherStatus.Error;
										Log($"ERROR: Directory {GameInstallPath} doesn't contain a valid installation of the game.\nThis launcher only supports Global and SEA clients!", true, 1);
										new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_existing_dir_invalid_msg"]).ShowDialog();
										Status = LauncherStatus.Ready;
										return;
									}
								}
							}
						});
					}
					if(server_changed)
					{
						DownloadBackgroundImage();
					}
				}
				catch(Exception ex)
				{
					Status = LauncherStatus.Error;
					Log($"ERROR: Checking for game update failed:\n{ex}", true, 1);
					Dispatcher.Invoke(() =>
					{
						new DialogWindow(App.TextStrings["msgbox_update_check_error_title"], App.TextStrings["msgbox_update_check_error_msg"]).ShowDialog();
						return;
					});
				}
			});
		}

		private int GameVersionUpdateCheck(GameVersion local_game_version)
		{
			if(LocalVersionInfo != null)
			{
				FetchmiHoYoVersionInfo();
				var online_game_version = new GameVersion(miHoYoVersionInfo.game.latest.version.ToString());
				if(online_game_version.IsNewerThan(local_game_version))
				{
					for(var i = 0; i < miHoYoVersionInfo.game.diffs.Count; i++)
					{
						if(miHoYoVersionInfo.game.diffs[i].version == local_game_version.ToString())
						{
							PatchDownloadInt = i;
							return 2;
						}
					}
					return 1;
				}
				else
				{
					return 0;
				}
			}
			else
			{
				return 0;
			}
		}

		private void DownloadLauncherUpdate()
		{
			Log("Downloading update...");
			Dispatcher.Invoke(() =>
			{
				ProgressText.Text = App.TextStrings["progresstext_updating_launcher"];
				ProgressBar.IsIndeterminate = false;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
			});
			try
			{
				tracker.NewFile();
				var eta_calc = new ETACalculator();
				var download = new DownloadPauseable(OnlineVersionInfo.launcher_info.url.ToString(), LauncherArchivePath);
				download.Start();
				while(!download.Done)
				{
					tracker.SetProgress(download.BytesWritten, download.ContentLength);
					eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
					Dispatcher.Invoke(() =>
					{
						var progress = tracker.GetProgress();
						ProgressBar.Value = progress;
						TaskbarItemInfo.ProgressValue = progress;
						ProgressText.Text = $"{App.TextStrings["progresstext_updating_launcher"]}\n{BpUtility.ToBytesCount(download.BytesWritten)}/{BpUtility.ToBytesCount(download.ContentLength)} ({tracker.GetBytesPerSecondString()})\n{string.Format(App.TextStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"))}";
					});
					Thread.Sleep(100);
				}
				Log("success!", false);
				Dispatcher.Invoke(() =>
				{
					ProgressText.Text = App.TextStrings["progresstext_updating_launcher"];
					ProgressBar.IsIndeterminate = true;
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
				});
				while(BpUtility.IsFileLocked(new FileInfo(LauncherArchivePath)))
				{
					Thread.Sleep(10);
				}
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to download launcher update:\n{ex}", true, 1);
				Dispatcher.Invoke(() =>
				{
					new DialogWindow(App.TextStrings["msgbox_net_error_title"], App.TextStrings["msgbox_launcher_download_error_msg"]).ShowDialog();
					MessageBox.Show(string.Format(App.TextStrings["msgbox_launcher_download_error_msg"], ex), App.TextStrings["msgbox_net_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
					Application.Current.Shutdown();
					return;
				});
			}
		}

		private void DownloadLauncherTranslations()
		{
			try
			{
				string translations_url = OnlineVersionInfo.launcher_info.translations.url.ToString();
				string translations_md5 = OnlineVersionInfo.launcher_info.translations.md5.ToString();
				string translations_version = OnlineVersionInfo.launcher_info.translations.version;
				bool Validate()
				{
					if(File.Exists(App.LauncherTranslationsFile))
					{
						var translations_version_reg = LauncherRegKey.GetValue("TranslationsVersion");
						if(translations_version_reg != null)
						{
							if(LauncherRegKey.GetValueKind("TranslationsVersion") == RegistryValueKind.String)
							{
								if(translations_version == LauncherRegKey.GetValue("TranslationsVersion").ToString())
								{
									string actual_md5 = BpUtility.CalculateMD5(App.LauncherTranslationsFile);
									if(actual_md5 != translations_md5)
									{
										Log($"WARNING: Translations validation failed. Expected MD5: {translations_md5}, got MD5: {actual_md5}", true, 2);
									}
									else
									{
										return true;
									}
								}
							}
						}
					}
					return false;
				}
				if(!Validate())
				{
					DeleteFile(App.LauncherTranslationsFile, true);
					int attempts = 3;
					for(int i = 0; i < attempts; i++)
					{
						if(!File.Exists(App.LauncherTranslationsFile))
						{
							Log("Downloading translations...");
							Directory.CreateDirectory(App.LauncherDataPath);
							var web_client = new BpWebClient();
							web_client.DownloadFile(translations_url, App.LauncherTranslationsFile);
							try
							{
								BpUtility.WriteToRegistry("TranslationsVersion", translations_version);
							}
							catch(Exception ex)
							{
								Status = LauncherStatus.Error;
								Log($"ERROR: Failed to write critical registry info:\n{ex}", true, 1);
								MessageBox.Show(App.TextStrings["msgbox_registry_error_msg"], App.TextStrings["msgbox_registry_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
								break;
							}
							Log("success!", false);
						}
						if(Validate())
						{
							BpUtility.RestartApp();
							break;
						}
						else
						{
							if(i == attempts - 1)
							{
								Log("Giving up...");
								throw new CryptographicException("Failed to validate translations");
							}
							else
							{
								DeleteFile(App.LauncherTranslationsFile, true);
								Log("Attempting to download again...");
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				Log($"ERROR: Failed to download translations:\n{ex}", true, 1);
				MessageBox.Show(App.TextStrings["msgbox_translations_download_error_msg"], App.TextStrings["msgbox_generic_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
				DeleteFile(App.LauncherTranslationsFile, true);
				Array.Resize(ref CommandLineArgs, CommandLineArgs.Length + 1);
				CommandLineArgs[CommandLineArgs.Length - 1] = "NOTRANSLATIONS";
				BpUtility.RestartApp();
			}
		}

		private void DownloadBackgroundImage()
		{
			try
			{
				string url;
				if(Server == HI3Server.Global)
				{
					url = OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.global.ToString();
				}
				else
				{
					url = OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.os.ToString();
				}
				Directory.CreateDirectory(App.LauncherBackgroundsPath);
				string background_image_url;
				string background_image_md5;
				var web_request = BpUtility.CreateWebRequest(url, "GET", 10000);
				using(var web_response = (HttpWebResponse)web_request.GetResponse())
				{
					using(var data = new MemoryStream())
					{
						web_response.GetResponseStream().CopyTo(data);
						var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
						if(json.retcode == 0)
						{
							if(json.data != null && json.data.adv != null && json.data.adv.background != null)
							{
								background_image_url = json.data.adv.background.ToString();
							}
							else
							{
								return;
							}
						}
						else
						{
							Log($"WARNING: Failed to fetch background image info: {json.message.ToString()}", true, 2);
							return;
						}
					}
				}
				string background_image_name = Path.GetFileName(HttpUtility.UrlDecode(background_image_url));
				string background_image_path = Path.Combine(App.LauncherBackgroundsPath, background_image_name);
				background_image_md5 = background_image_name.Split('_')[0].ToUpper();
				bool Validate()
				{
					if(File.Exists(background_image_path))
					{
						string actual_md5 = BpUtility.CalculateMD5(background_image_path);
						if(actual_md5 != background_image_md5)
						{
							Log($"WARNING: Background image validation failed. Expected MD5: {background_image_md5}, got MD5: {actual_md5}", true, 2);
						}
						else
						{
							Resources["BackgroundImage"] = new BitmapImage(new Uri(background_image_path));
							return true;
						}
					}
					return false;
				}
				try
				{
					foreach(var file in Directory.GetFiles(App.LauncherDataPath, "*.png"))
					{
						File.Move(file, Path.Combine(App.LauncherBackgroundsPath, Path.GetFileName(file)));
					}
				}catch{}
				if(!Validate())
				{
					DeleteFile(background_image_path, true);
					int attempts = 3;
					for(int i = 0; i < attempts; i++)
					{
						if(!File.Exists(background_image_path))
						{
							Log("Downloading background image...");
							Directory.CreateDirectory(App.LauncherDataPath);
							var web_client = new BpWebClient();
							web_client.DownloadFile(background_image_url, background_image_path);
							Log("success!", false);
						}
						if(Validate())
						{
							break;
						}
						else
						{
							if(i == attempts - 1)
							{
								Log("Giving up...");
								throw new CryptographicException("Failed to validate background image");
							}
							else
							{
								DeleteFile(background_image_path, true);
								Log("Attempting to download again...");
							}
						}
					}
				}
			}
			catch(Exception ex)
			{
				Log($"WARNING: Failed to download background image: {ex.Message}", true, 2);
			}
		}

		private async Task DownloadGameFile()
		{
			try
			{
				string title;
				long time;
				string url;
				string md5;
				bool abort = false;
				if(Mirror == HI3Mirror.miHoYo)
				{
					title = GameArchiveName;
					time = -1;
					url = miHoYoVersionInfo.game.latest.path.ToString();
					if(PatchDownload)
					{
						md5 = miHoYoVersionInfo.game.diffs[PatchDownloadInt].md5.ToString();
					}
					else
					{
						md5 = miHoYoVersionInfo.game.latest.md5.ToString();
					}
				}
				else if(Mirror == HI3Mirror.MediaFire)
				{
					dynamic mediafire_metadata;
					if(Server == HI3Server.Global)
					{
						mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString(), false);
					}
					else
					{
						mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString(), false);
					}
					if(mediafire_metadata == null)
					{
						return;
					}
					title = mediafire_metadata.title.ToString();
					time = ((DateTimeOffset)mediafire_metadata.modifiedDate).ToUnixTimeSeconds();
					url = mediafire_metadata.downloadUrl.ToString();
					md5 = mediafire_metadata.md5Checksum.ToString();
					GameArchivePath = Path.Combine(GameInstallPath, title);
					if(!mediafire_metadata.title.Contains(miHoYoVersionInfo.game.latest.version.ToString()))
					{
						Status = LauncherStatus.Error;
						Log("ERROR: Mirror is outdated!", true, 1);
						new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_mirror_old_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
						GameUpdateCheck();
						return;
					}
					try
					{
						var web_request = BpUtility.CreateWebRequest(url);
						var web_response = (HttpWebResponse)web_request.GetResponse();
					}
					catch(WebException ex)
					{
						Status = LauncherStatus.Error;
						Log($"ERROR: Failed to download from MediaFire:\n{ex}", true, 1);
						new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_mirror_error_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
						GameUpdateCheck();
						return;
					}
				}
				else
				{
					dynamic gd_metadata;
					if(Server == HI3Server.Global)
					{
						gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
					}
					else
					{
						gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
					}
					if(gd_metadata == null)
					{
						return;
					}
					title = gd_metadata.title.ToString();
					time = ((DateTimeOffset)gd_metadata.modifiedDate).ToUnixTimeSeconds();
					url = gd_metadata.downloadUrl.ToString();
					md5 = gd_metadata.md5Checksum.ToString();
					GameArchivePath = Path.Combine(GameInstallPath, title);
					if(DateTime.Compare(DateTime.Parse(miHoYoVersionInfo.last_modified.ToString()), DateTime.Parse(gd_metadata.modifiedDate.ToString())) > 0)
					{
						Status = LauncherStatus.Error;
						Log("ERROR: Mirror is outdated!", true, 1);
						new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_mirror_old_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
						GameUpdateCheck();
						return;
					}
					try
					{
						var web_request = BpUtility.CreateWebRequest(url);
						var web_response = (HttpWebResponse)web_request.GetResponse();
					}
					catch(WebException ex)
					{
						Status = LauncherStatus.Error;
						if(ex.Response != null)
						{
							using(var data = new MemoryStream())
							{
								ex.Response.GetResponseStream().CopyTo(data);
								var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
								string msg;
								if(json.error != null)
								{
									msg = json.error.errors[0].message;
								}
								else
								{
									msg = ex.Message;
								}
								Log($"ERROR: Failed to download from Google Drive:\n{msg}", true, 1);
								new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_mirror_error_msg"]).ShowDialog();
								Status = LauncherStatus.Ready;
								GameUpdateCheck();
							}
						}
						return;
					}
				}

				Log($"Starting to download game archive: {title} ({url})");
				Status = LauncherStatus.Downloading;
				await Task.Run(() =>
				{
					tracker.NewFile();
					var eta_calc = new ETACalculator();
					download = new DownloadPauseable(url, GameArchivePath);
					download.Start();
					Dispatcher.Invoke(() =>
					{
						LaunchButton.IsEnabled = true;
						LaunchButton.Content = App.TextStrings["button_pause"];
					});
					while(download != null && !download.Done)
					{
						if(DownloadPaused)
						{
							continue;
						}
						tracker.SetProgress(download.BytesWritten, download.ContentLength);
						eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
						Dispatcher.Invoke(() =>
						{
							var progress = tracker.GetProgress();
							ProgressBar.Value = progress;
							TaskbarItemInfo.ProgressValue = progress;
							ProgressText.Text = $"{string.Format(App.TextStrings["progresstext_downloaded"], BpUtility.ToBytesCount(download.BytesWritten), BpUtility.ToBytesCount(download.ContentLength), tracker.GetBytesPerSecondString())}\n{string.Format(App.TextStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"))}";
						});
						Thread.Sleep(100);
					}
					if(download == null)
					{
						abort = true;
						return;
					}
					download = null;
					Log("Successfully downloaded game archive");
					while(BpUtility.IsFileLocked(new FileInfo(GameArchivePath)))
					{
						Thread.Sleep(10);
					}
					Dispatcher.Invoke(() =>
					{
						ProgressText.Text = string.Empty;
						LaunchButton.Content = App.TextStrings["button_launch"];
					});
				});
				try
				{
					if(abort)
					{
						return;
					}
					await Task.Run(() =>
					{
						Log("Validating game archive...");
						Status = LauncherStatus.Verifying;
						string actual_md5 = BpUtility.CalculateMD5(GameArchivePath);
						if(actual_md5 != md5.ToUpper())
						{
							Status = LauncherStatus.Error;
							Log($"ERROR: Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
							DeleteFile(GameArchivePath);
							abort = true;
							Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_verify_error_title"], App.TextStrings["msgbox_verify_error_1_msg"]).ShowDialog();});
							Status = LauncherStatus.Ready;
							GameUpdateCheck();
						}
						else
						{
							Log("success!", false);
						}
						if(abort)
						{
							return;
						}
						if(!PatchDownload)
						{
							try
							{
								foreach(var file in Directory.GetFiles(Path.Combine(GameInstallPath, @"BH3_Data\StreamingAssets\Asb\pc"), "*.wmv"))
								{
									DeleteFile(file);
								}
							}catch{}
						}
						var skipped_files = new List<string>();
						using(var archive = ArchiveFactory.Open(GameArchivePath))
						{
							int unpacked_files = 0;
							int file_count = 0;

							Log("Unpacking game archive...");
							Status = LauncherStatus.Unpacking;
							foreach(var entry in archive.Entries)
							{
								if(!entry.IsDirectory)
								{
									file_count++;
								}
							}
							var reader = archive.ExtractAllEntries();
							while(reader.MoveToNextEntry())
							{
								try
								{
									Dispatcher.Invoke(() =>
									{
										ProgressText.Text = string.Format(App.TextStrings["progresstext_unpacking_2"], unpacked_files + 1, file_count);
										var progress = (unpacked_files + 1f) / file_count;
										ProgressBar.Value = progress;
										TaskbarItemInfo.ProgressValue = progress;
									});
									reader.WriteEntryToDirectory(GameInstallPath, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
									if(!reader.Entry.IsDirectory)
									{
										unpacked_files++;
									}
								}
								catch
								{
									if(!reader.Entry.IsDirectory)
									{
										skipped_files.Add(reader.Entry.ToString());
										file_count--;
										Log($"Unpack ERROR: {reader.Entry}");
									}
								}
							}
						}
						if(skipped_files.Count > 0)
						{
							DeleteFile(GameArchivePath);
							throw new ArchiveException("Game archive is corrupt");
						}
						Log("success!", false);
						DeleteFile(GameArchivePath);
						Dispatcher.Invoke(() => 
						{
							PatchDownload = false;
							WriteVersionInfo(false, true);
							Log("Successfully installed the game");
							GameUpdateCheck();
						});
						if(time != -1)
						{
							SendStatistics(title, time);
						}
					});
				}
				catch(Exception ex)
				{
					Status = LauncherStatus.Error;
					Log($"ERROR: Failed to install the game:\n{ex}", true, 1);
					Dispatcher.Invoke(() =>
					{
						new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_error_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
						GameUpdateCheck();
					});
				}
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to download the game:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				GameUpdateCheck();
			}
		}

		private void WriteVersionInfo(bool check_for_local_version = false, bool is_installed = false)
		{
			try
			{
				var config_ini_file = Path.Combine(GameInstallPath, "config.ini");
				var ini_parser = new FileIniDataParser();
				ini_parser.Parser.Configuration.AssigmentSpacer = string.Empty;
				dynamic version_info = new ExpandoObject();
				version_info.game_info = new ExpandoObject();
				if(!PatchDownload)
				{
					version_info.game_info.version = miHoYoVersionInfo.game.latest.version.ToString();
				}
				else
				{
					version_info.game_info.version = LocalVersionInfo.game_info.version.ToString();
				}
				version_info.game_info.install_path = GameInstallPath;
				version_info.game_info.installed = is_installed;

				if(GameInstallPath.Length < 4)
				{
					throw new Exception("Install path can't be on a root drive");
				}
				if(check_for_local_version)
				{
					var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
					if(File.Exists(config_ini_file))
					{
						var data = ini_parser.ReadFile(config_ini_file);
						if(data["General"]["game_version"] != null)
						{
							version_info.game_info.version = data["General"]["game_version"];
						}
					}
					else if(LauncherRegKey.GetValue(RegistryVersionInfo) == null && (key != null && key.GetValue(GameRegistryLocalVersionRegValue) != null && key.GetValueKind(GameRegistryLocalVersionRegValue) == RegistryValueKind.Binary))
					{
						var version = Encoding.UTF8.GetString((byte[])key.GetValue(GameRegistryLocalVersionRegValue)).TrimEnd('\u0000');
						if(!miHoYoVersionInfo.game.latest.version.ToString().Contains(version))
						{
							version_info.game_info.version = version;
						}
					}
					else
					{
						if(new DialogWindow(App.TextStrings["msgbox_install_title"], App.TextStrings["msgbox_install_existing_no_local_version_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
						{
							version_info.game_info.version = new GameVersion();
						}
					}
					if(key != null)
					{
						key.Close();
					}
				}
				Log("Writing game version info...");
				BpUtility.WriteToRegistry(RegistryVersionInfo, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(version_info)), RegistryValueKind.Binary);
				if(is_installed)
				{
					if(File.Exists(config_ini_file))
					{
						var data = ini_parser.ReadFile(config_ini_file);
						data["General"]["game_version"] = version_info.game_info.version;
						ini_parser.WriteFile(config_ini_file, data);
					}
				}
				Log("success!", false);
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to write version info:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
			}
		}

		private void DeleteGameFiles(bool DeleteGame = false)
		{
			if(DeleteGame)
			{
				if(Directory.Exists(GameInstallPath))
				{
					Directory.Delete(GameInstallPath, true);
				}
			}
			try{LauncherRegKey.DeleteValue(RegistryVersionInfo);}catch{}
			Dispatcher.Invoke(() => {LaunchButton.Content = App.TextStrings["button_download"];});
		}

		private async void DownloadGameCache(bool FullCache)
		{
			try
			{
				string title;
				long time;
				string url;
				string md5;
				long size;
				bool abort = false;
				if(FullCache)
				{
					title = GameCacheMetadata.title.ToString();
					time = ((DateTimeOffset)GameCacheMetadata.modifiedDate).ToUnixTimeSeconds();
					url = GameCacheMetadata.downloadUrl.ToString();
					md5 = GameCacheMetadata.md5Checksum.ToString();
					size = (long)GameCacheMetadata.fileSize;
				}
				else
				{
					title = GameCacheMetadataNumeric.title.ToString();
					time = ((DateTimeOffset)GameCacheMetadataNumeric.modifiedDate).ToUnixTimeSeconds();
					url = GameCacheMetadataNumeric.downloadUrl.ToString();
					md5 = GameCacheMetadataNumeric.md5Checksum.ToString();
					size = (long)GameCacheMetadataNumeric.fileSize;
				}
				CacheArchivePath = Path.Combine(miHoYoPath, title);

				var game_cache_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(CacheArchivePath) && x.IsReady).FirstOrDefault();
				if(game_cache_drive == null)
				{
					new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_wrong_drive_type_msg"]).ShowDialog();
					return;
				}
				else if(game_cache_drive.TotalFreeSpace < size * 2)
				{
					if(new DialogWindow(App.TextStrings["msgbox_install_title"], App.TextStrings["msgbox_install_little_space_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						return;
					}
				}
				try
				{
					var web_request = BpUtility.CreateWebRequest(url);
					var web_response = (HttpWebResponse)web_request.GetResponse();
				}
				catch(WebException ex)
				{
					Status = LauncherStatus.Error;
					Log($"ERROR: Failed to download cache from mirror:\n{ex}", true, 1);
					new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_mirror_error_msg"]).ShowDialog();
					Status = LauncherStatus.Ready;
					return;
				}

				Log($"Starting to download game cache: {title} ({url})");
				Status = LauncherStatus.Downloading;
				await Task.Run(() =>
				{
					tracker.NewFile();
					var eta_calc = new ETACalculator();
					var download = new DownloadPauseable(url, CacheArchivePath);
					download.Start();
					while(!download.Done)
					{
						tracker.SetProgress(download.BytesWritten, download.ContentLength);
						eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
						Dispatcher.Invoke(() =>
						{
							var progress = tracker.GetProgress();
							ProgressBar.Value = progress;
							TaskbarItemInfo.ProgressValue = progress;
							ProgressText.Text = $"{string.Format(App.TextStrings["progresstext_downloaded"], BpUtility.ToBytesCount(download.BytesWritten), BpUtility.ToBytesCount(download.ContentLength), tracker.GetBytesPerSecondString())}\n{string.Format(App.TextStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"))}";
						});
						Thread.Sleep(100);
					}
					Log("Successfully downloaded game cache");
					while(BpUtility.IsFileLocked(new FileInfo(CacheArchivePath)))
					{
						Thread.Sleep(10);
					}
					Dispatcher.Invoke(() =>
					{
						ProgressText.Text = string.Empty;
						LaunchButton.Content = App.TextStrings["button_launch"];
					});
				});
				try
				{
					if(abort)
					{
						return;
					}
					await Task.Run(() =>
					{
						Log("Validating game cache...");
						Status = LauncherStatus.Verifying;
						string actual_md5 = BpUtility.CalculateMD5(CacheArchivePath);
						if(actual_md5 != md5.ToUpper())
						{
							Status = LauncherStatus.Error;
							Log($"ERROR: Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
							Dispatcher.Invoke(() =>
							{
								if(new DialogWindow(App.TextStrings["msgbox_verify_error_title"], App.TextStrings["msgbox_verify_error_2_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
								{
									DeleteFile(CacheArchivePath);
									abort = true;
									Status = LauncherStatus.Ready;
									GameUpdateCheck();
								}
							});
						}
						else
						{
							Log("success!", false);
						}
						if(abort)
						{
							return;
						}
						try
						{
							foreach(var file in Directory.GetFiles(Path.Combine(miHoYoPath, $@"{GameFullName}\Data\data"), "*.unity3d"))
							{
								DeleteFile(file);
							}
						}catch{}
						var skipped_files = new List<string>();
						using(var archive = ArchiveFactory.Open(CacheArchivePath))
						{
							int unpacked_files = 0;
							int file_count = 0;

							Log("Unpacking game cache...");
							Status = LauncherStatus.Unpacking;
							foreach(var entry in archive.Entries)
							{
								if(!entry.IsDirectory)
								{
									file_count++;
								}
							}
							Directory.CreateDirectory(miHoYoPath);
							var reader = archive.ExtractAllEntries();
							while(reader.MoveToNextEntry())
							{
								try
								{
									Dispatcher.Invoke(() =>
									{
										ProgressText.Text = string.Format(App.TextStrings["progresstext_unpacking_2"], unpacked_files + 1, file_count);
										var progress = (unpacked_files + 1f) / file_count;
										ProgressBar.Value = progress;
										TaskbarItemInfo.ProgressValue = progress;
									});
									reader.WriteEntryToDirectory(miHoYoPath, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
									if(!reader.Entry.IsDirectory)
									{
										unpacked_files++;
									}
								}
								catch
								{
									if(!reader.Entry.IsDirectory)
									{
										skipped_files.Add(reader.Entry.ToString());
										file_count--;
										Log($"Unpack ERROR: {reader.Entry}");
									}
								}
							}
						}
						if(skipped_files.Count > 0)
						{
							throw new ArchiveException("Cache archive is corrupt");
						}
						Log("success!", false);
						DeleteFile(CacheArchivePath);
						SendStatistics(title, time);
					});
				}
				catch(Exception ex)
				{
					Status = LauncherStatus.Error;
					Log($"ERROR: Failed to install game cache:\n{ex}", true, 1);
					new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_error_msg"]).ShowDialog();
				}
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to download game cache:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_error_msg"]).ShowDialog();
			}
			Dispatcher.Invoke(() => {LaunchButton.Content = App.TextStrings["button_launch"];});
			Status = LauncherStatus.Ready;
		}

		private void SendStatistics(string file, long time)
		{
			if(string.IsNullOrEmpty(file))
			{
				throw new ArgumentNullException();
			}

			string server = (int)Server == 0 ? "global" : "os";
			string mirror = (int)Mirror == 2 ? "gd" : "mediafire";
			try
			{
				var data = Encoding.ASCII.GetBytes($"save_stats={server}&mirror={mirror}&file={file}&time={time}");
				var web_request = BpUtility.CreateWebRequest(OnlineVersionInfo.launcher_info.stat_url.ToString(), "POST", 10000);
				web_request.ContentType = "application/x-www-form-urlencoded";
				web_request.ContentLength = data.Length;
				using(var stream = web_request.GetRequestStream())
				{
					stream.Write(data, 0, data.Length);
				}
				using(var web_response = (HttpWebResponse)web_request.GetResponse())
				{
					var responseData = new StreamReader(web_response.GetResponseStream()).ReadToEnd();
					if(!string.IsNullOrEmpty(responseData))
					{
						var json = JsonConvert.DeserializeObject<dynamic>(responseData);
						if(json.status != "success")
						{
							Log($"WARNING: Failed to send download stat of {file}", true, 2);
						}
					}
				}
			}
			catch
			{
				Log($"WARNING: Failed to send download stat of {file}", true, 2);
			}
		}

		private async void Window_ContentRendered(object sender, EventArgs e)
		{
			#if DEBUG
				DisableAutoUpdate = true;
			#endif
			try
			{
				string exe_name = Process.GetCurrentProcess().MainModule.ModuleName;
				string old_exe_name = $"{Path.GetFileNameWithoutExtension(LauncherPath)}_old.exe";
				bool launcher_needs_update = LauncherUpdateCheck();

				if(Process.GetCurrentProcess().MainModule.ModuleName != LauncherExeName)
				{
					Status = LauncherStatus.Error;
					DeleteFile(LauncherPath, true);
					File.Move(Path.Combine(App.LauncherRootPath, exe_name), LauncherPath);
					BpUtility.RestartApp();
					return;
				}
				DeleteFile(Path.Combine(App.LauncherRootPath, old_exe_name), true);
				DeleteFile(Path.Combine(App.LauncherRootPath, "BetterHI3Launcher.exe.bak"), true); // legacy name
				await Task.Run(() =>
				{
					if(DisableAutoUpdate)
					{
						return;
					}

					if(!launcher_needs_update)
					{
						if(BpUtility.CalculateMD5(LauncherPath) != OnlineVersionInfo.launcher_info.exe_md5.ToString().ToUpper())
						{
							Log($"ERROR: Launcher integrity error, attempting self-repair...", true, 1);
							launcher_needs_update = true;
						}
					}
					if(launcher_needs_update)
					{
						Log("A newer version of the launcher is available!");
						Status = LauncherStatus.Working;
						DownloadLauncherUpdate();
						Log("Validating update...");
						string md5 = OnlineVersionInfo.launcher_info.md5.ToString().ToUpper();
						string actual_md5 = BpUtility.CalculateMD5(LauncherArchivePath);
						if(actual_md5 != md5)
						{
							Status = LauncherStatus.Error;
							Log($"ERROR: Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
							DeleteFile(LauncherArchivePath, true);
							Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_verify_error_title"], App.TextStrings["msgbox_verify_error_1_msg"]).ShowDialog();});
							return;
						}
						Log("success!", false);
						Log("Performing update...");
						File.Move(Path.Combine(App.LauncherRootPath, exe_name), Path.Combine(App.LauncherRootPath, old_exe_name));
						using(var archive = ArchiveFactory.Open(LauncherArchivePath))
						{
							var reader = archive.ExtractAllEntries();
							while(reader.MoveToNextEntry())
							{
								reader.WriteEntryToDirectory(App.LauncherRootPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true, PreserveFileTime = true });
							}
						}
						Log("success!", false);
						Dispatcher.Invoke(() => {BpUtility.RestartApp();});
						return;
					}
					else
					{
						DeleteFile(LauncherArchivePath, true);
						DeleteFile(Path.Combine(App.LauncherRootPath, "BetterHI3Launcher.7z"), true); // legacy name
						if(!File.Exists(LauncherPath))
						{
							File.Copy(Path.Combine(App.LauncherRootPath, exe_name), LauncherPath, true);
						}
					}
				});
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to start the launcher:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_start_error_title"], string.Format(App.TextStrings["msgbox_start_error_msg"], ex.Message)).ShowDialog();
				return;
			}

			if(FirstLaunch)
			{
				IntroBox.Visibility = Visibility.Visible;
			}
			if(LauncherRegKey != null && LauncherRegKey.GetValue("LauncherVersion") != null)
			{
				if(new App.LauncherVersion(App.LocalLauncherVersion.ToString()).IsNewerThan(new App.LauncherVersion(LauncherRegKey.GetValue("LauncherVersion").ToString())))
				{
					ChangelogBox.Visibility = Visibility.Visible;
					ChangelogBoxMessageTextBlock.Visibility = Visibility.Visible;
					FetchChangelog();
				}
			}
			try
			{
				if(LauncherRegKey.GetValue("LauncherVersion") == null || LauncherRegKey.GetValue("LauncherVersion") != null && LauncherRegKey.GetValue("LauncherVersion").ToString() != App.LocalLauncherVersion.ToString())
				{
					BpUtility.WriteToRegistry("LauncherVersion", App.LocalLauncherVersion.ToString());
				}
				// legacy values
				BpUtility.DeleteFromRegistry("RanOnce");
				BpUtility.DeleteFromRegistry("BackgroundImageName");
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to write critical registry info:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_registry_error_title"], App.TextStrings["msgbox_registry_error_msg"]).ShowDialog();
				return;
			}
			var custom_background_name_reg = LauncherRegKey.GetValue("CustomBackgroundName");
			if(custom_background_name_reg != null)
			{
				if(LauncherRegKey.GetValueKind("CustomBackgroundName") == RegistryValueKind.String)
				{
					string path = Path.Combine(App.LauncherBackgroundsPath, custom_background_name_reg.ToString());
					Log(path);
					if(File.Exists(path))
					{
						SetCustomBackgroundFile(path);
					}
					else
					{
						Log("WARNING: Custom background file can't be found, resetting to official...", true, 2);
						BpUtility.DeleteFromRegistry("CustomBackgroundName");
					}
				}
			}
			if(!FirstLaunch)
			{
				GameUpdateCheck();
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

		private async void LaunchButton_Click(object sender, RoutedEventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Click);
			if(Status == LauncherStatus.Ready || Status == LauncherStatus.Preloading || Status == LauncherStatus.PreloadVerifying)
			{
				if(DownloadPaused)
				{
					DownloadPaused = false;
					await DownloadGameFile();
					return;
				}

				if(LocalVersionInfo != null)
				{
					if(!File.Exists(GameExePath))
					{
						new DialogWindow(App.TextStrings["msgbox_no_game_exe_title"], App.TextStrings["msgbox_no_game_exe_msg"]).ShowDialog();
						return;
					}
					try
					{
						var processes = Process.GetProcessesByName("BH3");
						if(processes.Length > 0)
						{
							processes[0].EnableRaisingEvents = true;
							processes[0].Exited += new EventHandler((object s, EventArgs ea) => {OnGameExit();});
							Status = LauncherStatus.Running;
							return;
						}
						var start_info = new ProcessStartInfo(GameExePath);
						start_info.WorkingDirectory = GameInstallPath;
						start_info.UseShellExecute = true;
						var process = Process.Start(start_info);
						process.EnableRaisingEvents = true;
						process.Exited += new EventHandler((object s1, EventArgs ea1) =>
						{
							processes = Process.GetProcessesByName("BH3");
							if(processes.Length > 0)
							{
								processes[0].EnableRaisingEvents = true;
								processes[0].Exited += new EventHandler((object s2, EventArgs ea2) => {OnGameExit();});
							}
							else
							{
								OnGameExit();
							}
						});
						Status = LauncherStatus.Running;
						WindowState = WindowState.Minimized;
					}
					catch(Exception ex)
					{
						Status = LauncherStatus.Error;
						Log($"ERROR: Failed to start the game:\n{ex}", true, 1);
						new DialogWindow(App.TextStrings["msgbox_start_error_title"], App.TextStrings["msgbox_process_start_error_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
					}
				}
				else
				{
					try
					{
						string SelectGameInstallDirectory()
						{
							// https://stackoverflow.com/a/17712949/7570821
							var dialog = new CommonOpenFileDialog
							{
								IsFolderPicker = true,
								InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
								AddToMostRecentlyUsedList = false,
								AllowNonFileSystemItems = false,
								DefaultDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
								EnsureFileExists = true,
								EnsurePathExists = true,
								EnsureReadOnly = false,
								EnsureValidNames = true,
								Multiselect = false,
								ShowPlacesList = true
							};

							if(dialog.ShowDialog() == CommonFileDialogResult.Ok)
							{
								GameInstallPath = Path.Combine(dialog.FileName, GameFullName);
							}
							else
							{
								GameInstallPath = null;
							}

							if(string.IsNullOrEmpty(GameInstallPath))
							{
								return string.Empty;
							}
							else
							{
								var path = CheckForExistingGameDirectory(dialog.FileName);
								if(path.Length < 4)
								{
									path = string.Empty;
								}
								if(!string.IsNullOrEmpty(path))
								{
									if(new DialogWindow(App.TextStrings["msgbox_install_title"], string.Format(App.TextStrings["msgbox_install_existing_dir_msg"], path), DialogWindow.DialogType.Question).ShowDialog() == true)
									{
										Log($"Existing install directory selected: {path}");
										GameInstallPath = path;
										var server = CheckForExistingGameClientServer();
										if(server >= 0)
										{
											if((int)Server != server)
											{
												ServerDropdown.SelectedIndex = server;
											}
											WriteVersionInfo(true, true);
											GameUpdateCheck();
										}
										else
										{
											Status = LauncherStatus.Error;
											Log($"ERROR: Directory {GameInstallPath} doesn't contain a valid installation of the game. This launcher only supports Global and SEA clients.", true, 1);
											new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_existing_dir_invalid_msg"]).ShowDialog();
											Status = LauncherStatus.Ready;
										}
									}
									return string.Empty;
								}
								return GameInstallPath;
							}
						}
						if(string.IsNullOrEmpty(SelectGameInstallDirectory()))
						{
							return;
						}
						while(new DialogWindow(App.TextStrings["msgbox_install_title"], string.Format(App.TextStrings["msgbox_install_msg"], GameInstallPath), DialogWindow.DialogType.Question).ShowDialog() == false)
						{
							if(string.IsNullOrEmpty(SelectGameInstallDirectory()))
							{
								return;
							}
						}
						var game_install_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(GameInstallPath) && x.IsReady).FirstOrDefault();
						if(game_install_drive == null || game_install_drive.DriveType == DriveType.CDRom)
						{
							new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_wrong_drive_type_msg"]).ShowDialog();
							return;
						}
						else if(game_install_drive.TotalFreeSpace < (long)miHoYoVersionInfo.game.latest.size)
						{
							if(new DialogWindow(App.TextStrings["msgbox_install_title"], App.TextStrings["msgbox_install_little_space_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
							{
								return;
							}
						}
						Directory.CreateDirectory(GameInstallPath);
						GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
						GameExePath = Path.Combine(GameInstallPath, "BH3.exe");
						Log($"Install dir selected: {GameInstallPath}");
						await DownloadGameFile();
					}
					catch(Exception ex)
					{
						Status = LauncherStatus.Error;
						Log($"ERROR: Failed to select game install directory:\n{ex}", true, 1);
						new DialogWindow(App.TextStrings["msgbox_install_dir_error_title"], App.TextStrings["msgbox_install_dir_error_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
						return;
					}
				}
			}
			else if(Status == LauncherStatus.UpdateAvailable)
			{
				var game_install_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(GameInstallPath) && x.IsReady).FirstOrDefault();
				if(game_install_drive.TotalFreeSpace < (long)miHoYoVersionInfo.game.latest.size)
				{
					if(new DialogWindow(App.TextStrings["msgbox_install_title"], App.TextStrings["msgbox_install_little_space_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						return;
					}
				}
				if(!PatchDownload)
				{
					Directory.CreateDirectory(GameInstallPath);
				}
				await DownloadGameFile();
			}
			else if(Status == LauncherStatus.Downloading || Status == LauncherStatus.DownloadPaused)
			{
				if(!DownloadPaused)
				{
					download.Pause();
					Status = LauncherStatus.DownloadPaused;
					LaunchButton.Content = App.TextStrings["button_resume"];
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
				}
				else
				{
					Status = LauncherStatus.Downloading;
					LaunchButton.IsEnabled = true;
					LaunchButton.Content = App.TextStrings["button_pause"];
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
					try
					{
						await download.Start();
					}
					catch(Exception ex)
					{
						Status = LauncherStatus.Error;
						Log($"ERROR: Failed to download the game:\n{ex}", true, 1);
						new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_error_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
					}
				}
			}
		}

		private void OptionsButton_Click(object sender, RoutedEventArgs e)
		{
			var button = sender as Button;
			OptionsContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
			OptionsContextMenu.PlacementTarget = button;
			OptionsContextMenu.VerticalOffset = button.Height;
			OptionsContextMenu.IsOpen = true;
			BpUtility.PlaySound(Properties.Resources.Click);
		}

		private async void PreloadButton_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}

			try
			{
				string url = miHoYoVersionInfo.pre_download_game.latest.path.ToString();
				string title = Path.GetFileName(HttpUtility.UrlDecode(url));
				long size;
				string md5 = miHoYoVersionInfo.pre_download_game.latest.md5.ToString();
				string path = Path.Combine(GameInstallPath, $"{title}_tmp");
				bool abort = false;

				var web_request = BpUtility.CreateWebRequest(url, "HEAD");
				using(var web_response = (HttpWebResponse) web_request.GetResponse())
				{
					size = web_response.ContentLength;
				}
				if(!File.Exists(path))
				{
					if(new DialogWindow(App.TextStrings["label_preload"], $"{App.TextStrings["msgbox_pre_install_msg"]}\n{App.TextStrings["progresstext_download_size"]}: {BpUtility.ToBytesCount(size)}", DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						return;
					}
					var game_install_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(GameInstallPath) && x.IsReady).FirstOrDefault();
					if(game_install_drive.TotalFreeSpace < (long)miHoYoVersionInfo.pre_download_game.latest.size)
					{
						if(new DialogWindow(App.TextStrings["msgbox_install_title"], App.TextStrings["msgbox_install_little_space_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
						{
							return;
						}
					}
					Log($"Starting to preload game: {title} ({url})");
				}
				else
				{
					Log("Preload resumed");
				}
				Status = LauncherStatus.Preloading;
				await Task.Run(() =>
				{
					tracker.NewFile();
					var eta_calc = new ETACalculator();
					download = new DownloadPauseable(url, path);
					download.Start();
					while(download != null && !download.Done)
					{
						tracker.SetProgress(download.BytesWritten, download.ContentLength);
						eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
						Dispatcher.Invoke(() =>
						{
							var progress = tracker.GetProgress();
							PreloadCircleProgressBar.Value = progress;
							TaskbarItemInfo.ProgressValue = progress;
							PreloadBottomText.Text = string.Format(App.TextStrings["label_downloaded_1"], Math.Round(progress * 100));
							PreloadStatusTopRightText.Text = $"{BpUtility.ToBytesCount(download.BytesWritten)}/{BpUtility.ToBytesCount(download.ContentLength)}";
							PreloadStatusMiddleRightText.Text = eta_calc.ETR.ToString("hh\\:mm\\:ss");
							PreloadStatusBottomRightText.Text = tracker.GetBytesPerSecondString();
						});
						Thread.Sleep(100);
					}
					if(download == null)
					{
						abort = true;
						Status = LauncherStatus.Ready;
						return;
					}
					Log("Downloaded preload archive");
					while(BpUtility.IsFileLocked(new FileInfo(path)))
					{
						Thread.Sleep(10);
					}
				});
				if(abort)
				{
					return;
				}
				Status = LauncherStatus.PreloadVerifying;
				try
				{
					await Task.Run(() =>
					{
						Log("Validating preload archive...");
						string actual_md5 = BpUtility.CalculateMD5(path);
						if(actual_md5 == md5.ToUpper())
						{
							Log("success!", false);
							var new_path = path.Substring(0, path.Length - 4);
							if(!File.Exists(new_path))
							{
								File.Move(path, new_path);
							}
							else if(File.Exists(new_path) && new FileInfo(new_path).Length != size)
							{
								DeleteFile(new_path, true);
								File.Move(path, new_path);
							}
							else
							{
								DeleteFile(path);
							}
							Log("Successfully preloaded the game");
							GameUpdateCheck();
						}
						else
						{
							Status = LauncherStatus.Error;
							Log($"ERROR: Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
							DeleteFile(path);
							Dispatcher.Invoke(() =>
							{
								PreloadBottomText.Text = App.TextStrings["label_retry"];
							});
							Status = LauncherStatus.Ready;
						}
					});
				}
				catch(Exception ex)
				{
					Status = LauncherStatus.Error;
					Log($"ERROR: Failed to preload the game:\n{ex}", true, 1);
					new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_error_msg"]).ShowDialog();
					Status = LauncherStatus.Ready;
				}
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to download game preload archive:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
			}
			TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
			WindowState = WindowState.Normal;
		}

		private void PreloadPauseButton_Click(object sender, RoutedEventArgs e)
		{
			if(download != null)
			{
				Log("Preload paused");
				download.Pause();
				download = null;
				PreloadPauseButton.Background = (ImageBrush)Resources["PreloadResumeButton"];
				PreloadBottomText.Text = PreloadBottomText.Text.Replace(App.TextStrings["label_downloaded_1"], App.TextStrings["label_paused"]);
				PreloadStatusMiddleRightText.Text = string.Empty;
				PreloadStatusBottomRightText.Text = string.Empty;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
			}
			else
			{
				try
				{
					var peer = new ButtonAutomationPeer(PreloadButton);
					var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
					inv_prov.Invoke();
				}
				catch(Exception ex)
				{
					Log($"ERROR: Failed to resume preloading:\n{ex}", true, 1);
				}
			}
		}

		private async Task CM_DownloadCache_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}

			Status = LauncherStatus.CheckingUpdates;
			Dispatcher.Invoke(() => {ProgressText.Text = App.TextStrings["progresstext_mirror_connect"];});
			Log("Fetching mirror data...");
			try
			{
				string mirror;
				string time;
				string last_updated;

				await Task.Run(() =>
				{
					FetchOnlineVersionInfo();
					if(Server == HI3Server.Global)
					{
						if(Mirror == HI3Mirror.GoogleDrive)
						{
							GameCacheMetadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_cache.global.ToString());
							if(GameCacheMetadata != null)
							{
								GameCacheMetadataNumeric = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_cache_numeric.global.ToString());
							}
						}
						else
						{
							GameCacheMetadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache.global.id.ToString(), false);
							if(GameCacheMetadata != null)
							{
								GameCacheMetadataNumeric = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.id.ToString(), true);
							}
						}
					}
					else
					{
						if(Mirror == HI3Mirror.GoogleDrive)
						{
							GameCacheMetadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_cache.os.ToString());
							if(GameCacheMetadata != null)
							{
								GameCacheMetadataNumeric = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_cache_numeric.os.ToString());
							}
						}
						else
						{
							GameCacheMetadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache.os.id.ToString(), false);
							if(GameCacheMetadata != null)
							{
								GameCacheMetadataNumeric = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.id.ToString(), true);
							}
						}
					}
					if(GameCacheMetadata == null || GameCacheMetadataNumeric == null)
					{
						Status = LauncherStatus.Ready;
						return;
					}
					mirror = Mirror == HI3Mirror.GoogleDrive ? "Google Drive" : "MediaFire";
					try
					{
						time = Mirror == HI3Mirror.GoogleDrive ? GameCacheMetadataNumeric.modifiedDate.ToString() : new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((double)OnlineVersionInfo.game_info.mirror.mediafire.last_updated).ToString();
						if(DateTime.Compare(FetchmiHoYoResourceVersionDateModified(), DateTime.Parse(time)) >= 0)
						{
							last_updated = $"{DateTime.Parse(time).ToLocalTime().ToString(new CultureInfo(App.OSLanguage))} ({App.TextStrings["outdated"].ToLower()})";
						}
						else
						{
							last_updated = DateTime.Parse(time).ToLocalTime().ToString(new CultureInfo(App.OSLanguage));
						}
						Log("success!", false);
					}
					catch
					{
						last_updated = App.TextStrings["msgbox_generic_error_title"];
						Log($"WARNING: Failed to load last cache update time", true, 2);
					}
					Dispatcher.Invoke(() =>
					{
						DownloadCacheBox.Visibility = Visibility.Visible;
						DownloadCacheBoxMessageTextBlock.Text = string.Format(App.TextStrings["downloadcachebox_msg"], mirror, last_updated, OnlineVersionInfo.game_info.mirror.maintainer.ToString());
						Status = LauncherStatus.Ready;
					});
				});
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to fetch cache metadata:\n{ex}", true, 1);
				Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_mirror_error_msg"], ex.Message)).ShowDialog();});
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private async Task CM_Repair_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}

			Status = LauncherStatus.CheckingUpdates;
			Dispatcher.Invoke(() => {ProgressText.Text = App.TextStrings["progresstext_fetching_hashes"];});
			Log("Fetching repair data...");
			try
			{
				string server = (int)Server == 0 ? "global" : "os";
				var web_client = new BpWebClient();
				await Task.Run(() =>
				{
					OnlineRepairInfo = JsonConvert.DeserializeObject<dynamic>(web_client.DownloadString($"https://bpnet.host/bh3?launcher_repair={server}"));
				});
				if(OnlineRepairInfo.status == "success")
				{
					OnlineRepairInfo = OnlineRepairInfo.repair_info;
					if(OnlineRepairInfo.game_version != LocalVersionInfo.game_info.version && !AdvancedFeatures)
					{
						ProgressText.Text = string.Empty;
						ProgressBar.Visibility = Visibility.Hidden;
						new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_repair_1_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
						return;
					}
				}
				else
				{
					Status = LauncherStatus.Error;
					Log($"ERROR: Failed to fetch repair data: {OnlineRepairInfo.status_message}", true, 1);
					new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_net_error_msg"], OnlineRepairInfo.status_message)).ShowDialog();
					Status = LauncherStatus.Ready;
					return;
				}
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to fetch repair data:\n{ex}", true, 1);
				Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_net_error_msg"], ex.Message)).ShowDialog();});
				Status = LauncherStatus.Ready;
				return;
			}
			Dispatcher.Invoke(() =>
			{
				RepairBox.Visibility = Visibility.Visible;
				RepairBoxMessageTextBlock.Text = string.Format(App.TextStrings["repairbox_msg"], OnlineRepairInfo.mirrors, OnlineVersionInfo.game_info.mirror.maintainer.ToString());
				Log("success!", false);
				Status = LauncherStatus.Ready;
			});
		}

		private async Task CM_Move_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			if(!Directory.Exists(GameInstallPath))
			{
				new DialogWindow(App.TextStrings["msgbox_no_game_dir_title"], App.TextStrings["msgbox_no_game_dir_msg"]).ShowDialog();
				return;
			}
			if(App.LauncherRootPath.Contains(GameInstallPath))
			{
				new DialogWindow(App.TextStrings["msgbox_move_title"], App.TextStrings["msgbox_move_3_msg"]).ShowDialog();
				return;
			}

			var dialog = new CommonOpenFileDialog
			{
				IsFolderPicker = true,
				InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
				AddToMostRecentlyUsedList = false,
				AllowNonFileSystemItems = false,
				DefaultDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
				EnsureFileExists = true,
				EnsurePathExists = true,
				EnsureReadOnly = false,
				EnsureValidNames = true,
				Multiselect = false,
				ShowPlacesList = true
			};

			if(dialog.ShowDialog() == CommonFileDialogResult.Ok)
			{
				string path = Path.Combine(dialog.FileName, GameFullName);
				if(!path.Contains(GameInstallPath))
				{
					var game_move_to_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(path) && x.IsReady).FirstOrDefault();
					if(game_move_to_drive == null)
					{
						new DialogWindow(App.TextStrings["msgbox_move_error_title"], App.TextStrings["msgbox_move_wrong_drive_type_msg"]).ShowDialog();
						return;
					}
					else if(game_move_to_drive.TotalFreeSpace < new DirectoryInfo(GameInstallPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(x => x.Length))
					{
						if(new DialogWindow(App.TextStrings["msgbox_move_title"], App.TextStrings["msgbox_move_little_space_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
						{
							return;
						}
					}
					if(new DialogWindow(App.TextStrings["msgbox_move_title"], string.Format(App.TextStrings["msgbox_move_1_msg"], path), DialogWindow.DialogType.Question).ShowDialog() == true)
					{
						Status = LauncherStatus.Working;
						ProgressText.Text = App.TextStrings["progresstext_moving_files"];
						Log($"Moving game files to: {path}");
						await Task.Run(() =>
						{
							try
							{
								if(Directory.GetDirectoryRoot(GameInstallPath) == Directory.GetDirectoryRoot(path))
								{
									Directory.Move(GameInstallPath, path);
								}
								else
								{
									Directory.CreateDirectory(path);
									Directory.SetCreationTime(path, Directory.GetCreationTime(GameInstallPath));
									Directory.SetLastWriteTime(path, Directory.GetLastWriteTime(GameInstallPath));
									string[] files = Directory.GetFiles(GameInstallPath);
									foreach(string file in files)
									{
										string name = Path.GetFileName(file);
										string dest = Path.Combine(path, name);
										new FileInfo(file).Attributes &= ~FileAttributes.ReadOnly;
										File.Copy(file, dest, true);
										File.SetCreationTime(dest, File.GetCreationTime(file));
									}
									string[] dirs = Directory.GetDirectories(GameInstallPath, "*", SearchOption.AllDirectories);
									foreach(string dir in dirs)
									{
										string name = dir.Replace(GameInstallPath, string.Empty);
										string dest = $"{path}{name}";
										new DirectoryInfo(dir).Attributes &= ~FileAttributes.ReadOnly;
										Directory.CreateDirectory(dest);
										Directory.SetCreationTime(dest, Directory.GetCreationTime(dir));
										Directory.SetLastWriteTime(dest, Directory.GetLastWriteTime(dir));
										string[] nested_files = Directory.GetFiles(dir);
										foreach(string nested_file in nested_files)
										{
											string nested_name = Path.GetFileName(nested_file);
											string nested_dest = Path.Combine(dest, nested_name);
											new FileInfo(nested_file).Attributes &= ~FileAttributes.ReadOnly;
											File.Copy(nested_file, nested_dest, true);
											File.SetCreationTime(nested_dest, File.GetCreationTime(nested_file));
										}
									}
									try
									{
										new DirectoryInfo(GameInstallPath).Attributes &= ~FileAttributes.ReadOnly;
										Directory.Delete(GameInstallPath, true);
									}
									catch
									{
										Log($"WARNING: Failed to delete old game directory, you may want to do it manually: {GameInstallPath}", true, 2);
									}
								}
								GameInstallPath = path;
								WriteVersionInfo(false, true);
								Log("Successfully moved game files");
								GameUpdateCheck();
							}
							catch(Exception ex)
							{
								Status = LauncherStatus.Error;
								Log($"ERROR: Failed to move the game:\n{ex}", true, 1);
								Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_move_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();});
								Status = LauncherStatus.Ready;
								return;
							}
						});
					}
				}
				else
				{
					new DialogWindow(App.TextStrings["msgbox_move_title"], App.TextStrings["msgbox_move_2_msg"]).ShowDialog();
				}
			}
		}

		private async Task CM_Uninstall_Click(object sender, RoutedEventArgs e)
		{
			if((Status != LauncherStatus.Ready || Status != LauncherStatus.UpdateAvailable || Status != LauncherStatus.DownloadPaused) && string.IsNullOrEmpty(GameInstallPath))
			{
				return;
			}
			try
			{
				if(App.LauncherRootPath.Contains(GameInstallPath))
				{
					new DialogWindow(App.TextStrings["msgbox_uninstall_title"], App.TextStrings["msgbox_uninstall_5_msg"]).ShowDialog();
					return;
				}
				var dialog = new DialogWindow(App.TextStrings["msgbox_uninstall_title"], App.TextStrings["msgbox_uninstall_1_msg"], DialogWindow.DialogType.Uninstall);
				if(dialog.ShowDialog() == false)
				{
					return;
				}
				bool delete_game_files = (bool)dialog.UninstallGameFilesCheckBox.IsChecked;
				bool delete_game_cache = (bool)dialog.UninstallGameCacheCheckBox.IsChecked;
				bool delete_game_settings = (bool)dialog.UninstallGameSettingsCheckBox.IsChecked;
				if(!delete_game_files && !delete_game_cache && !delete_game_settings)
				{
					return;
				}
				string delete_list = "\n";
				if(delete_game_files)
				{
					delete_list += $"\n{App.TextStrings["msgbox_uninstall_game_files"]}";
				}
				if(delete_game_cache)
				{
					delete_list += $"\n{App.TextStrings["msgbox_uninstall_game_cache"]}";
				}
				if(delete_game_settings)
				{
					delete_list += $"\n{App.TextStrings["msgbox_uninstall_game_settings"]}";
				}
				if(new DialogWindow(App.TextStrings["msgbox_uninstall_title"], App.TextStrings["msgbox_uninstall_2_msg"] + delete_list, DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					return;
				}
				string msg;
				if(delete_game_files)
				{
					msg = App.TextStrings["msgbox_uninstall_3_msg"] + delete_list;
				}
				else
				{
					msg = App.TextStrings["msgbox_uninstall_4_msg"];
				}
				if(new DialogWindow(App.TextStrings["msgbox_uninstall_title"], msg, DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					return;
				}
				Status = LauncherStatus.Uninstalling;
				await Task.Run(() =>
				{
					if(delete_game_files)
					{
						Log("Deleting game files...");
						DeleteGameFiles(true);
						Log("Sucessfully deleted game files");
					}
					if(delete_game_cache)
					{
						Log("Deleting game cache...");
						if(Directory.Exists(GameCachePath))
						{
							Directory.Delete(GameCachePath, true);
							Log("Successfully deleted game cache");
						}
					}
					if(delete_game_settings)
					{
						Log("Deleting game settings...");
						var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
						if(key != null)
						{
							Registry.CurrentUser.DeleteSubKeyTree(GameRegistryPath, true);
							key.Close();
							Log("Successfully deleted game settings");
						}
					}
					Dispatcher.Invoke(() =>
					{
						ProgressText.Text = string.Empty;
						ProgressBar.Visibility = Visibility.Hidden;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
						WindowState = WindowState.Normal;
						new DialogWindow(App.TextStrings["msgbox_uninstall_title"], App.TextStrings["msgbox_uninstall_6_msg"] + delete_list).ShowDialog();
					});
					GameUpdateCheck();
				});
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to uninstall the game:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_uninstall_error_title"], App.TextStrings["msgbox_uninstall_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private async Task CM_FixSubtitles_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			if(new DialogWindow(App.TextStrings["contextmenu_fix_subtitles"], App.TextStrings["msgbox_fix_subtitles_1_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
			{
				return;
			}

			try
			{
				Status = LauncherStatus.Working;
				Log("Starting to fix subtitles...");
				var game_video_path = Path.Combine(GameInstallPath, @"BH3_Data\StreamingAssets\Video");
				if(Directory.Exists(game_video_path))
				{
					var subtitle_archives = Directory.EnumerateFiles(game_video_path, "*.zip", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith(".zip", StringComparison.CurrentCultureIgnoreCase)).ToList();
					Dispatcher.Invoke(() =>
					{
						ProgressBar.IsIndeterminate = false;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
					});
					if(subtitle_archives.Count > 0)
					{
						int unpacked_files = 0;
						await Task.Run(() =>
						{
							var skipped_files = new List<string>();
							var skipped_file_paths = new List<string>();
							foreach(var subtitle_archive in subtitle_archives)
							{
								bool unpack_ok = true;
								Dispatcher.Invoke(() =>
								{
									ProgressText.Text = string.Format(App.TextStrings["msgbox_fix_subtitles_2_msg"], unpacked_files + 1, subtitle_archives.Count);
									var progress = (unpacked_files + 1f) / subtitle_archives.Count;
									ProgressBar.Value = progress;
									TaskbarItemInfo.ProgressValue = progress;
								});
								using(var archive = ArchiveFactory.Open(subtitle_archive))
								{
									var reader = archive.ExtractAllEntries();
									while(reader.MoveToNextEntry())
									{
										try
										{
											var entryPath = Path.Combine(game_video_path, reader.Entry.ToString());
											if(File.Exists(entryPath))
											{
												File.SetAttributes(entryPath, File.GetAttributes(entryPath) & ~FileAttributes.ReadOnly);
											}
											reader.WriteEntryToDirectory(game_video_path, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
										}
										catch
										{
											unpack_ok = false;
											skipped_files.Add($"{reader.Entry} ({Path.GetFileName(subtitle_archive)})");
											skipped_file_paths.Add(subtitle_archive);
											Log($"ERROR: Failed to unpack {subtitle_archive} ({reader.Entry})", true, 1);
											if(reader.Entry.ToString() == "CG_09_mux_1_en.srt")
											{
												Log("The above one line of error is normal, miHoYo somehow messed up the file");
											}
										}
									}
								}
								if(unpack_ok)
								{
									Log($"Unpacked {subtitle_archive}");
								}
								File.SetAttributes(subtitle_archive, File.GetAttributes(subtitle_archive) & ~FileAttributes.ReadOnly);
								if(!skipped_file_paths.Contains(subtitle_archive))
								{
									DeleteFile(subtitle_archive);
								}
								unpacked_files++;
							}
							Dispatcher.Invoke(() =>
							{
								if(skipped_files.Count > 0)
								{
									ShowLogCheckBox.IsChecked = true;
									if(Server == HI3Server.Global && skipped_files.Count == 1)
									{
									}
									else
									{
										TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
										new DialogWindow(App.TextStrings["msgbox_extract_skip_title"], App.TextStrings["msgbox_extract_skip_msg"]).ShowDialog();
									}
								}
							});
							Log($"Unpacked {unpacked_files} archives");
						});
					}
					ProgressBar.IsIndeterminate = true;
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
					var subtitle_files = Directory.EnumerateFiles(game_video_path, "*.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith(".srt", StringComparison.CurrentCultureIgnoreCase)).ToList();
					var subs_fixed = new List<string>();
					ProgressBar.IsIndeterminate = false;
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
					if(subtitle_files.Count > 0)
					{
						int subtitles_parsed = 0;
						await Task.Run(() =>
						{
							foreach(var subtitle_file in subtitle_files)
							{
								var sub_lines = File.ReadAllLines(subtitle_file);
								var sub_lines_to_remove = new List<int>();
								bool sub_fixed = false;
								int line_count = sub_lines.Length;
								int lines_replaced = 0;
								int lines_removed = 0;
								Dispatcher.Invoke(() =>
								{
									ProgressText.Text = string.Format(App.TextStrings["msgbox_fix_subtitles_3_msg"], subtitles_parsed + 1, subtitle_files.Count);
									var progress = (subtitles_parsed + 1f) / subtitle_files.Count;
									ProgressBar.Value = progress;
									TaskbarItemInfo.ProgressValue = progress;
								});
								File.SetAttributes(subtitle_file, File.GetAttributes(subtitle_file) & ~FileAttributes.ReadOnly);
								if(new FileInfo(subtitle_file).Length == 0)
								{
									subtitles_parsed++;
									continue;
								}
								for(int at_line = 1; at_line < line_count; at_line++)
								{
									var line = File.ReadLines(subtitle_file).Skip(at_line).Take(1).First();
									if(string.IsNullOrEmpty(line) || new Regex(@"^\d+$").IsMatch(line))
										continue;

									bool line_fixed = false;
									void LogLine()
									{
										if(line_fixed)
											return;

										lines_replaced++;
										line_fixed = true;
										if(AdvancedFeatures)
										{
											Log($"Fixed line {1 + at_line}: {line}");
										}
									}

									if(line.Contains("-->"))
									{
										if(line.Contains("."))
										{
											sub_lines[at_line] = line.Replace(".", ",");
											LogLine();
										}
										if(line.Contains(" ,"))
										{
											sub_lines[at_line] = line.Replace(" ,", ",");
											LogLine();
										}
										if(line.Contains("  "))
										{
											sub_lines[at_line] = line.Replace("  ", " ");
											LogLine();
										}
										if(at_line + 1 < line_count && string.IsNullOrEmpty(sub_lines[at_line + 1]))
										{
											sub_lines_to_remove.Add(at_line + 1);
										}
									}
									else
									{
										if(line.Contains(" ,"))
										{
											sub_lines[at_line] = line.Replace(" ,", ",");
											LogLine();
										}
									}
								}
								foreach(var line in sub_lines_to_remove)
								{
									sub_lines = sub_lines.Where((source, index) => index != line - lines_removed).ToArray();
									lines_removed++;
								}
								if(lines_replaced > 0 || lines_removed > 0)
								{
									File.WriteAllLines(subtitle_file, sub_lines);
									sub_fixed = true;
								}
								var subLine = File.ReadAllText(subtitle_file);
								if(subLine.Contains($"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}"))
								{
									subLine = subLine.Replace($"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}", $"{Environment.NewLine}{Environment.NewLine}");
									File.WriteAllText(subtitle_file, subLine);
									sub_fixed = true;
								}
								if(sub_fixed && !subs_fixed.Contains(subtitle_file))
								{
									subs_fixed.Add(subtitle_file);
									Log($"Subtitle fixed: {subtitle_file}");
								}
								subtitles_parsed++;
							}
						});
						Log($"Parsed {subtitles_parsed} subtitles, fixed {subs_fixed.Count} of them");
					}
					if(Server == HI3Server.Global)
					{
						ProgressBar.IsIndeterminate = true;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
						subtitle_files = Directory.EnumerateFiles(game_video_path, "*id.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("id.srt", StringComparison.CurrentCultureIgnoreCase)).ToList();
						subtitle_files.AddRange(subtitle_files = Directory.EnumerateFiles(game_video_path, "*th.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("th.srt", StringComparison.CurrentCultureIgnoreCase)).ToList());
						subtitle_files.AddRange(subtitle_files = Directory.EnumerateFiles(game_video_path, "*vn.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("vn.srt", StringComparison.CurrentCultureIgnoreCase)).ToList());
						if(subtitle_files.Count > 0)
						{
							int deletedSubs = 0;
							await Task.Run(() =>
							{
								foreach(var subtitle_file in subtitle_files)
								{
									try
									{
										if(File.Exists(subtitle_file))
										{
											File.Delete(subtitle_file);
										}
										deletedSubs++;
									}
									catch
									{
										Log($"WARNING: Failed to delete {subtitle_file}", true, 2);
									}
								}
							});
							Log($"Deleted {deletedSubs} useless subtitles");
						}
					}
					ProgressText.Text = string.Empty;
					ProgressBar.Visibility = Visibility.Hidden;
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
					WindowState = WindowState.Normal;
					if(subtitle_archives.Count > 0 && subs_fixed.Count == 0)
					{
						new DialogWindow(App.TextStrings["msgbox_notice_title"], string.Format(App.TextStrings["msgbox_fix_subtitles_4_msg"], subtitle_archives.Count)).ShowDialog();
					}
					else if(subtitle_archives.Count == 0 && subs_fixed.Count > 0)
					{
						new DialogWindow(App.TextStrings["msgbox_notice_title"], string.Format(App.TextStrings["msgbox_fix_subtitles_5_msg"], subs_fixed.Count)).ShowDialog();
					}
					else if(subtitle_archives.Count > 0 && subs_fixed.Count > 0)
					{
						new DialogWindow(App.TextStrings["msgbox_notice_title"], $"{string.Format(App.TextStrings["msgbox_fix_subtitles_4_msg"], subtitle_archives.Count)}\n{string.Format(App.TextStrings["msgbox_fix_subtitles_5_msg"], subs_fixed.Count)}").ShowDialog();
					}
					else
					{
						new DialogWindow(App.TextStrings["msgbox_notice_title"], App.TextStrings["msgbox_fix_subtitles_6_msg"]).ShowDialog();
					}
				}
				else
				{
					Status = LauncherStatus.Error;
					Log("ERROR: No CG directory!", true, 1);
					new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_no_video_dir_msg"]).ShowDialog();
				}
				Status = LauncherStatus.Ready;
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private void CM_CustomFPS_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}

			try
			{
				var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
				string value = "GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411";
				if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.Binary)
				{
					if(key.GetValue(value) != null)
					{
						key.DeleteValue(value);
					}
					new DialogWindow(App.TextStrings["msgbox_registry_error_title"], $"{App.TextStrings["msgbox_registry_empty_1_msg"]}\n{App.TextStrings["msgbox_registry_empty_3_msg"]}").ShowDialog();
					return;
				}
				var value_before = key.GetValue(value);
				var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])value_before));
				if(json == null)
				{
					new DialogWindow(App.TextStrings["msgbox_registry_error_title"], $"{App.TextStrings["msgbox_registry_empty_1_msg"]}\n{App.TextStrings["msgbox_registry_empty_3_msg"]}").ShowDialog();
					return;
				}
				key.Close();
				FPSInputBox.Visibility = Visibility.Visible;
				if(json.TargetFrameRateForInLevel != null)
				{
					CombatFPSInputBoxTextBox.Text = json.TargetFrameRateForInLevel;
				}
				else
				{
					CombatFPSInputBoxTextBox.Text = "60";
				}
				if(json.TargetFrameRateForOthers != null)
				{
					MenuFPSInputBoxTextBox.Text = json.TargetFrameRateForOthers;
				}
				else
				{
					MenuFPSInputBoxTextBox.Text = "60";
				}
				GameGraphicSettings = json;
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to access registry:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_registry_error_title"], App.TextStrings["msgbox_registry_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private void CM_CustomResolution_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}

			try
			{
				var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
				string value = "GENERAL_DATA_V2_ScreenSettingData_h1916288658";
				if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.Binary)
				{
					if(key.GetValue(value) != null)
					{
						key.DeleteValue(value);
					}
					new DialogWindow(App.TextStrings["msgbox_registry_error_title"], $"{App.TextStrings["msgbox_registry_empty_1_msg"]}\n{App.TextStrings["msgbox_registry_empty_3_msg"]}").ShowDialog();
					return;
				}
				var value_before = key.GetValue(value);
				var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])value_before));
				if(json == null)
				{
					new DialogWindow(App.TextStrings["msgbox_registry_error_title"], $"{App.TextStrings["msgbox_registry_empty_1_msg"]}\n{App.TextStrings["msgbox_registry_empty_3_msg"]}").ShowDialog();
					return;
				}
				key.Close();
				ResolutionInputBox.Visibility = Visibility.Visible;

				if(json.width != null)
				{
					ResolutionInputBoxWidthTextBox.Text = json.width;
				}
				else
				{
					ResolutionInputBoxWidthTextBox.Text = "720";
				}
				if(json.height != null)
				{
					ResolutionInputBoxHeightTextBox.Text = json.height;
				}
				else
				{
					ResolutionInputBoxHeightTextBox.Text = "480";
				}
				if(json.isfullScreen != null)
				{
					ResolutionInputBoxFullscreenCheckbox.IsChecked = json.isfullScreen;
				}
				else
				{
					ResolutionInputBoxFullscreenCheckbox.IsChecked = false;
				}
				GameScreenSettings = json;
			}

			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to access registry:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_registry_error_title"], App.TextStrings["msgbox_registry_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}

		}

		private void CM_ResetDownloadType_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			if(new DialogWindow(App.TextStrings["contextmenu_reset_download_type"], App.TextStrings["msgbox_download_type_1_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
			{
				return;
			}

			try
			{
				var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
				string value = "GENERAL_DATA_V2_ResourceDownloadType_h2238376574";
				if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.DWord)
				{
					if(key.GetValue(value) != null)
					{
						key.DeleteValue(value);
					}
					new DialogWindow(App.TextStrings["msgbox_registry_error_title"], $"{App.TextStrings["msgbox_registry_empty_1_msg"]}\n{App.TextStrings["msgbox_registry_empty_2_msg"]}").ShowDialog();
					return;
				}
				var value_before = key.GetValue(value);
				int value_after;
				if((int)value_before != 0)
				{
					value_after = 0;
				}
				else
				{
					new DialogWindow(App.TextStrings["contextmenu_reset_download_type"], App.TextStrings["msgbox_download_type_3_msg"]).ShowDialog();
					return;
				}
				key.SetValue(value, value_after, RegistryValueKind.DWord);
				key.Close();
				Log($"Changed ResourceDownloadType from {value_before} to {value_after}");
				new DialogWindow(App.TextStrings["contextmenu_reset_download_type"], App.TextStrings["msgbox_download_type_2_msg"]).ShowDialog();
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to access registry:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_registry_error_title"], App.TextStrings["msgbox_registry_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private void CM_ResetGameSettings_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			if(new DialogWindow(App.TextStrings["contextmenu_reset_game_settings"], App.TextStrings["msgbox_reset_game_settings_moved_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
			{
				return;
			}
			foreach(dynamic item in OptionsContextMenu.Items)
			{
				if(item.GetType() == typeof(MenuItem) && item.Header.ToString() == App.TextStrings["contextmenu_uninstall"])
				{
					var peer = new MenuItemAutomationPeer(item);
					var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
					inv_prov.Invoke();
					break;
				}
			}
		}

		private void CM_Changelog_Click(object sender, RoutedEventArgs e)
		{
			ChangelogBox.Visibility = Visibility.Visible;
			ChangelogBoxScrollViewer.ScrollToHome();
			FetchChangelog();
		}

		private void CM_CustomBackground_Click(object sender, RoutedEventArgs e)
		{
			bool first_time = LauncherRegKey.GetValue("CustomBackgroundName") == null ? true : false;
			if(first_time)
			{
				if(new DialogWindow(App.TextStrings["contextmenu_custom_background"], string.Format(App.TextStrings["msgbox_custom_background_1_msg"], Grid.Width, Grid.Height), DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					return;
				}
			}
			else
			{
				var dialog = new DialogWindow(App.TextStrings["contextmenu_custom_background"], App.TextStrings["msgbox_custom_background_2_msg"], DialogWindow.DialogType.CustomBackground);
				if(dialog.ShowDialog() == false)
				{
					return;
				}
				if((bool)dialog.CustomBackgroundDeleteRadioButton.IsChecked)
				{
					Log("Deleting custom background...");
					BackgroundImage.Source = (BitmapImage)Resources["BackgroundImage"];
					BackgroundImage.Visibility = Visibility.Visible;
					BackgroundMedia.Visibility = Visibility.Collapsed;
					BackgroundMedia.Source = null;
					DeleteFile(Path.Combine(App.LauncherBackgroundsPath, LauncherRegKey.GetValue("CustomBackgroundName").ToString()));	
					BpUtility.DeleteFromRegistry("CustomBackgroundName");
					Log("success!", false);
					return;
				}
			}

			try
			{
				while(true)
				{ 
					var dialog = new OpenFileDialog
					{
						InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
						Filter = "Bitmap Files|*.bmp;*.dib|JPEG|*.jpg;*.jpeg;*.jpe;*.jfif|GIF|*.gif|TIFF|*.tif;*.tiff|PNG|*.png|MPEG|*.mp4;*.m4v;*.mpeg;*.mpg;*.mpv|AVI|*.avi|WMV|*.wmv|FLV|*.flv|All Supported Files|*.bmp;*.dib;*.jpg;*.jpeg;*.jpe;*.jfif;*.gif;*.tif;*.tiff;*.png;*.mp4;*.m4v;*.mpeg;*.mpg;*.mpv;*.avi;*.wmv;*.flv|All Files|*.*",
						FilterIndex = 10
					};
					if(dialog.ShowDialog() == true)
					{
						long file_size = new FileInfo(dialog.FileName).Length;
						int file_size_limit = 52428800;
						var launcher_data_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(App.LauncherDataPath) && x.IsReady).FirstOrDefault();
						if(launcher_data_drive == null)
						{
							throw new DriveNotFoundException("Launcher data drive is unavailable");
						}
						else if(dialog.FileName.Contains(App.LauncherBackgroundsPath))
						{
							new DialogWindow(App.TextStrings["contextmenu_custom_background"], string.Format(App.TextStrings["msgbox_custom_background_4_msg"], BpUtility.ToBytesCount(file_size_limit))).ShowDialog();
							continue;
						}
						else if(file_size > file_size_limit)
						{
							new DialogWindow(App.TextStrings["contextmenu_custom_background"], string.Format(App.TextStrings["msgbox_custom_background_5_msg"], BpUtility.ToBytesCount(file_size_limit))).ShowDialog();
							continue;
						}
						else if(launcher_data_drive.TotalFreeSpace < file_size)
						{
							new DialogWindow(App.TextStrings["contextmenu_custom_background"], App.TextStrings["msgbox_custom_background_6_msg"]).ShowDialog();
							continue;
						}
						SetCustomBackgroundFile(dialog.FileName, true);
					}
					break;
				}
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to set custom background:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
			}
		}

		private void CM_Sounds_Click(object sender, RoutedEventArgs e)
		{
			var item = sender as MenuItem;
			if(item.IsChecked)
			{
				Log("Disabled sounds");
			}
			else
			{
				Log("Enabled sounds");
			}
			DisableSounds = item.IsChecked;
			item.IsChecked = !item.IsChecked;
			try
			{
				BpUtility.WriteToRegistry("Sounds", item.IsChecked, RegistryValueKind.DWord);
			}
			catch(Exception ex)
			{
				Log($"ERROR: Failed to write value with key Sounds to registry:\n{ex}", true, 1);
			}
		}

		private void CM_Language_Click(object sender, RoutedEventArgs e)
		{
			var item = sender as MenuItem;
			if(item.IsChecked)
			{
				return;
			}
			if(Status == LauncherStatus.Downloading || Status == LauncherStatus.Verifying || Status == LauncherStatus.Unpacking || Status == LauncherStatus.Uninstalling || Status == LauncherStatus.Working || Status == LauncherStatus.Preloading || Status == LauncherStatus.PreloadVerifying)
			{
				return;
			}

			string lang = item.Header.ToString();
			string msg;
			if(App.LauncherLanguage != "en" && App.LauncherLanguage != "de" && App.LauncherLanguage != "vi")
			{
				msg = string.Format(App.TextStrings["msgbox_language_msg"], lang.ToLower());
			}
			else
			{
				msg = string.Format(App.TextStrings["msgbox_language_msg"], lang);
			}
			if(App.LauncherLanguage == "vi")
			{
				msg = string.Format(App.TextStrings["msgbox_language_msg"], char.ToLower(lang[0]) + lang.Substring(1));
			}
			if(new DialogWindow(App.TextStrings["contextmenu_language"], msg, DialogWindow.DialogType.Question).ShowDialog() == false)
			{
				return;
			}
			if(Status == LauncherStatus.DownloadPaused)
			{
				if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_1_msg"]}\n{App.TextStrings["msgbox_abort_2_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					return;
				}
				Status = LauncherStatus.CleaningUp;
				DeleteFile(GameArchivePath);
				DeleteFile(CacheArchivePath);
			}

			try
			{
				if(lang == App.TextStrings["contextmenu_language_system"])
				{
					try{BpUtility.DeleteFromRegistry("Language");}catch{}
				}
				else
				{
					if(lang == App.TextStrings["contextmenu_language_english"])
					{
						App.LauncherLanguage = "en";
					}
					else if(lang == App.TextStrings["contextmenu_language_russian"])
					{
						App.LauncherLanguage = "ru";
					}
					else if(lang == App.TextStrings["contextmenu_language_spanish"])
					{
						App.LauncherLanguage = "es";
					}
					else if(lang == App.TextStrings["contextmenu_language_portuguese"])
					{
						App.LauncherLanguage = "pt";
					}
					else if(lang == App.TextStrings["contextmenu_language_german"])
					{
						App.LauncherLanguage = "de";
					}
					else if(lang == App.TextStrings["contextmenu_language_vietnamese"])
					{
						App.LauncherLanguage = "vi";
					}
					else if(lang == App.TextStrings["contextmenu_language_serbian"])
					{
						App.LauncherLanguage = "sr";
					}
					else if(lang == App.TextStrings["contextmenu_language_thai"])
					{
						App.LauncherLanguage = "th";
					}
					else
					{
						Log($"ERROR: Translation for {lang} doesn't exist", true, 1);
						return;
					}
					BpUtility.WriteToRegistry("Language", App.LauncherLanguage);
				}
				Log($"Set language to {App.LauncherLanguage}");
				BpUtility.RestartApp();
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to set language:\n{ex}", true, 1);
				Status = LauncherStatus.Ready;
			}
		}

		private void CM_About_Click(object sender, RoutedEventArgs e)
		{
			AboutBox.Visibility = Visibility.Visible;
		}

		private void ServerDropdown_Opened(object sender, EventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Click);
		}

		private void ServerDropdown_Changed(object sender, SelectionChangedEventArgs e)
		{
			var index = ServerDropdown.SelectedIndex;
			if((int)Server == index)
			{
				return;
			}

			if(DownloadPaused)
			{
				if(new DialogWindow(App.TextStrings["msgbox_notice_title"], App.TextStrings["msgbox_game_download_paused_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					ServerDropdown.SelectedIndex = (int)Server;
					return;
				}
				download = null;
				DownloadPaused = false;
				DeleteFile(GameArchivePath);
				if(!PatchDownload)
				{
					DeleteGameFiles();
				}
			}
			switch(index)
			{
				case 0:
					Server = HI3Server.Global;
					break;
				case 1:
					Server = HI3Server.SEA;
					break;
			}
			try
			{
				BpUtility.WriteToRegistry("LastSelectedServer", index, RegistryValueKind.DWord);
			}
			catch(Exception ex)
			{
				Log($"ERROR: Failed to write value with key LastSelectedServer to registry:\n{ex}", true, 1);
			}
			Log($"Switched server to {((ComboBoxItem)ServerDropdown.SelectedItem).Content as string}");
			GameUpdateCheck(true);
		}

		private void MirrorDropdown_Opened(object sender, EventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Click);
		}

		private void MirrorDropdown_Changed(object sender, SelectionChangedEventArgs e)
		{
			var index = MirrorDropdown.SelectedIndex;
			if((int)Mirror == index)
			{
				return;
			}

			if(DownloadPaused)
			{
				if(new DialogWindow(App.TextStrings["msgbox_notice_title"], App.TextStrings["msgbox_game_download_paused_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					MirrorDropdown.SelectedIndex = (int)Mirror;
					return;
				}
				download = null;
				DownloadPaused = false;
				DeleteFile(GameArchivePath);
				if(!PatchDownload)
				{
					DeleteGameFiles();
				}
			}
			else if(Mirror == HI3Mirror.miHoYo && index != 0)
			{
				if(new DialogWindow(App.TextStrings["msgbox_notice_title"], App.TextStrings["msgbox_mirror_info_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					MirrorDropdown.SelectedIndex = 0;
					return;
				}
			}
			switch(index)
			{
				case 0:
					Mirror = HI3Mirror.miHoYo;
					break;
				case 1:
					Mirror = HI3Mirror.MediaFire;
					break;
				case 2:
					Mirror = HI3Mirror.GoogleDrive;
					break;
			}
			try
			{
				BpUtility.WriteToRegistry("LastSelectedMirror", index, RegistryValueKind.DWord);
			}
			catch(Exception ex)
			{
				Log($"ERROR: Failed to write value with key LastSelectedMirror to registry:\n{ex}", true, 1);
			}
			GameUpdateCheck();
			Log($"Selected mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");
		}
		
		private void FPSInputBoxTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			e.Handled = !e.Text.Any(x => char.IsDigit(x));
		}

		// https://stackoverflow.com/q/1268552/7570821
		private void FPSInputBoxTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
		{
			bool IsTextAllowed(string text)
			{
				return Array.TrueForAll(text.ToCharArray(), delegate (char c){return char.IsDigit(c) || char.IsControl(c);});
			}

			if(e.DataObject.GetDataPresent(typeof(string)))
			{
				string text = (string)e.DataObject.GetData(typeof(string));
				if(!IsTextAllowed(text))
				{
					e.CancelCommand();
				}
			}
			else
			{
				e.CancelCommand();
			}
		}

		private void IntroBoxCloseButton_Click(object sender, RoutedEventArgs e)
		{
			IntroBox.Visibility = Visibility.Collapsed;
			if(FirstLaunch)
			{
				GameUpdateCheck();
			}
		}

		private void DownloadCacheBoxFullCacheButton_Click(object sender, RoutedEventArgs e)
		{
			if(DownloadCacheBoxMessageTextBlock.Text.Contains(App.TextStrings["outdated"].ToLower()))
			{
				if(new DialogWindow(App.TextStrings["contextmenu_download_cache"], App.TextStrings["msgbox_download_cache_4_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					return;
				}
			}
			if(new DialogWindow(App.TextStrings["contextmenu_download_cache"], $"{App.TextStrings["msgbox_download_cache_1_msg"]}\n{string.Format(App.TextStrings["msgbox_download_cache_3_msg"], BpUtility.ToBytesCount((long)GameCacheMetadata.fileSize))}", DialogWindow.DialogType.Question).ShowDialog() == false)
			{
				return;
			}
			DownloadCacheBox.Visibility = Visibility.Collapsed;
			DownloadGameCache(true);
		}

		private void DownloadCacheBoxNumericFilesButton_Click(object sender, RoutedEventArgs e)
		{
			if(DownloadCacheBoxMessageTextBlock.Text.Contains(App.TextStrings["outdated"].ToLower()))
			{
				if(new DialogWindow(App.TextStrings["contextmenu_download_cache"], App.TextStrings["msgbox_download_cache_4_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					return;
				}
			}
			if(new DialogWindow(App.TextStrings["contextmenu_download_cache"], $"{App.TextStrings["msgbox_download_cache_2_msg"]}\n{string.Format(App.TextStrings["msgbox_download_cache_3_msg"], BpUtility.ToBytesCount((long)GameCacheMetadataNumeric.fileSize))}", DialogWindow.DialogType.Question).ShowDialog() == false)
			{
				return;
			}
			DownloadCacheBox.Visibility = Visibility.Collapsed;
			DownloadGameCache(false);
		}

		private void DownloadCacheBoxCloseButton_Click(object sender, RoutedEventArgs e)
		{
			DownloadCacheBox.Visibility = Visibility.Collapsed;
		}

		private async void RepairBoxYesButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				async Task Verify()
				{
					var corrupted_files = new List<string>();
					var corrupted_file_hashes = new List<string>();
					long corrupted_files_size = 0;

					Log("Verifying game files...");
					if(AdvancedFeatures)
					{
						Log($"Repair data game version: {OnlineRepairInfo.game_version}");
					}
					await Task.Run(() =>
					{
						for(int i = 0; i < OnlineRepairInfo.files.names.Count; i++)
						{
							string name = OnlineRepairInfo.files.names[i].ToString().Replace("/", @"\");
							string md5 = OnlineRepairInfo.files.hashes[i].ToString().ToUpper();
							long size = OnlineRepairInfo.files.sizes[i];
							string path = Path.Combine(GameInstallPath, name);

							Dispatcher.Invoke(() =>
							{
								ProgressText.Text = string.Format(App.TextStrings["progresstext_verifying_file"], i + 1, OnlineRepairInfo.files.names.Count);
								var progress = (i + 1f) / OnlineRepairInfo.files.names.Count;
								ProgressBar.Value = progress;
								TaskbarItemInfo.ProgressValue = progress;
							});
							if(!File.Exists(path) || BpUtility.CalculateMD5(path) != md5)
							{
								if(File.Exists(path))
								{
									Log($"File corrupted: {name}");
								}
								else
								{
									Log($"File missing: {name}");
								}
								corrupted_files.Add(name);
								corrupted_file_hashes.Add(md5);
								corrupted_files_size += size;
							}
							else
							{
								if(AdvancedFeatures)
								{
									Log($"File OK: {name}");
								}
							}
						}
					});
					ProgressText.Text = string.Empty;
					ProgressBar.Visibility = Visibility.Hidden;
					ProgressBar.Value = 0;
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
					TaskbarItemInfo.ProgressValue = 0;
					WindowState = WindowState.Normal;
					if(corrupted_files.Count > 0)
					{
						Log($"Finished verifying files, found corrupted/missing files: {corrupted_files.Count}");
						if(new DialogWindow(App.TextStrings["contextmenu_repair"], string.Format(App.TextStrings["msgbox_repair_3_msg"], corrupted_files.Count, BpUtility.ToBytesCount(corrupted_files_size)), DialogWindow.DialogType.Question).ShowDialog() == true)
						{
							string[] urls = OnlineRepairInfo.zip_urls.ToObject<string[]>();
							int repaired_files = 0;
							bool abort = false;

							Status = LauncherStatus.Downloading;
							await Task.Run(async () =>
							{
								if(urls.Length == 0)
								{
									throw new InvalidOperationException("No download URLs are present in repair data.");
								}
								for(int i = 0; i < corrupted_files.Count; i++)
								{
									string path = Path.Combine(GameInstallPath, corrupted_files[i]);

									Dispatcher.Invoke(() =>
									{
										ProgressText.Text = string.Format(App.TextStrings["progresstext_downloading_file"], i + 1, corrupted_files.Count);
										var progress = (i + 1f) / corrupted_files.Count;
										ProgressBar.Value = progress;
										TaskbarItemInfo.ProgressValue = progress;
									});
									for(int j = 0; j < urls.Length; j++)
									{
										string url = null;

										try
										{
											if(string.IsNullOrEmpty(urls[j]))
											{
												throw new NullReferenceException($"Download URL with index {j} is empty.");
											}
											else if(urls[j].Contains("www.mediafire.com"))
											{
												var metadata = FetchMediaFireFileMetadata(urls[j].Substring(31, 15), false);
												url = metadata.downloadUrl.ToString();
											}
											else
											{
												url = urls[j];
											}
										
											await PartialZipDownloader.DownloadFile(url, corrupted_files[i], path);
											Dispatcher.Invoke(() => {ProgressText.Text = string.Format(App.TextStrings["progresstext_verifying_file"], i + 1, corrupted_files.Count);});
											if(!File.Exists(path) || BpUtility.CalculateMD5(path) != corrupted_file_hashes[i])
											{
												Log($"ERROR: Failed to repair file {corrupted_files[i]}", true, 1);
											}
											else
											{
												Log($"Repaired file {corrupted_files[i]}");
												repaired_files++;
											}
										}
										catch(Exception ex)
										{
											if(j == urls.Length - 1)
											{
												Status = LauncherStatus.Error;
												Log($"ERROR: Failed to download file [{corrupted_files[i]}] ({url}): {ex.Message}\nNo more mirrors available!", true, 1);
												Dispatcher.Invoke(() =>
												{
													new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
													LaunchButton.Content = App.TextStrings["button_launch"];
												});
												Status = LauncherStatus.Ready;
												abort = true;
												return;
											}
											else
											{
												Log($"WARNING: Failed to download file [{corrupted_files[i]}] ({url}): {ex.Message}\nAttempting to download from another mirror...", true, 2);
											}
										}
									}
								}
							});
							Dispatcher.Invoke(() =>
							{
								LaunchButton.Content = App.TextStrings["button_launch"];
								ProgressText.Text = string.Empty;
								ProgressBar.Visibility = Visibility.Hidden;
								TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
							});
							if(!abort)
							{
								if(repaired_files == corrupted_files.Count)
								{
									Log($"Successfully repaired {repaired_files} file(s)");
									Dispatcher.Invoke(() =>
									{
										new DialogWindow(App.TextStrings["contextmenu_repair"], string.Format(App.TextStrings["msgbox_repair_4_msg"], repaired_files)).ShowDialog();
									});
								}
								else
								{
									int skipped_files = corrupted_files.Count - repaired_files;
									if(repaired_files > 0)
									{
										Log($"Successfully repaired {repaired_files} files, failed to repair {skipped_files} files");
									}
									Dispatcher.Invoke(() =>
									{
										new DialogWindow(App.TextStrings["contextmenu_repair"], string.Format(App.TextStrings["msgbox_repair_5_msg"], skipped_files)).ShowDialog();
									});
								}
							}
						}
					}
					else
					{
						Log("Finished verifying files, no files need repair");
						Dispatcher.Invoke(() =>
						{
							ProgressText.Text = string.Empty;
							ProgressBar.Visibility = Visibility.Hidden;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
						});
						new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_repair_2_msg"]).ShowDialog();
					}
					Status = LauncherStatus.Ready;
				}

				if(OnlineRepairInfo.game_version != LocalVersionInfo.game_info.version)
				{
					if(AdvancedFeatures)
					{
						if(new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_repair_8_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
						{
							return;
						}
					}
				}
				RepairBox.Visibility = Visibility.Collapsed;
				Status = LauncherStatus.Working;
				ProgressText.Text = App.TextStrings["progresstext_fetching_hashes"];
				ProgressBar.IsIndeterminate = false;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
				await Verify();
			}
			catch(Exception ex)
			{
				LaunchButton.Content = App.TextStrings["button_launch"];
				Status = LauncherStatus.Error;
				Log($"ERROR:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
			}
		}

		private async void RepairBoxGenerateButton_Click(object sender, RoutedEventArgs e)
		{
			async Task Generate()
			{
				string server;
				if(Server == HI3Server.Global)
				{
					server = "global";
				}
				else
				{
					server = "os";
				}
				var dialog = new SaveFileDialog
				{
					InitialDirectory = App.LauncherRootPath,
					Filter = "JSON|*.json",
					FileName = $"bh3_files_{server}.json"
				};
				if(dialog.ShowDialog() == true)
				{
					try
					{
						Status = LauncherStatus.Working;
						ProgressBar.IsIndeterminate = false;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
						Log("Generating game file hashes...");
						var files = new DirectoryInfo(GameInstallPath).GetFiles("*", SearchOption.AllDirectories).Where(x => !x.Attributes.HasFlag(FileAttributes.Hidden) && x.Name != "config.ini" && x.Name != "UniFairy.sys" && x.Name != "Version.txt" && x.Name != "blockVerifiedVersion.txt" && !x.Name.Contains("Blocks_") && !x.Name.Contains("AUDIO_DLC") && !x.Name.Contains("AUDIO_EVENT") && !x.Name.Contains("AUDIO_BGM") && !x.Name.Contains("AUDIO_Main") && !x.Name.Contains("AUDIO_Ex") && !x.Name.Contains("AUDIO_Dialog") && !x.Name.Contains("AUDIO_Avatar") && !x.DirectoryName.Contains("Video") && !x.DirectoryName.Contains("webCaches") && x.Extension != ".log").ToList();
						dynamic json = new ExpandoObject();
						json.repair_info = new ExpandoObject();
						json.repair_info.game_version = miHoYoVersionInfo.game.latest.version;
						json.repair_info.mirrors = string.Empty;
						json.repair_info.zip_urls = Array.Empty<string>();
						json.repair_info.files = new ExpandoObject();
						json.repair_info.files.names = new dynamic[files.Count];
						json.repair_info.files.hashes = new dynamic[files.Count];
						json.repair_info.files.sizes = new dynamic[files.Count];
						await Task.Run(() =>
						{
							for(int i = 0; i < files.Count; i++)
							{
								json.repair_info.files.names[i] = files[i].FullName.Replace($"{GameInstallPath}\\", string.Empty).Replace(@"\", "/");
								json.repair_info.files.hashes[i] = BpUtility.CalculateMD5(files[i].FullName);
								json.repair_info.files.sizes[i] = files[i].Length;
								Dispatcher.Invoke(() =>
								{
									ProgressText.Text = string.Format(App.TextStrings["progresstext_generating_hash"], i + 1, files.Count);
									var progress = (i + 1f) / files.Count;
									ProgressBar.Value = progress;
									TaskbarItemInfo.ProgressValue = progress;
								});
							}
							File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(json));
							Log("success!", false);
							Log($"Saved JSON: {dialog.FileName}");
						});
						ProgressText.Text = string.Empty;
						ProgressBar.Visibility = Visibility.Hidden;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
						if(new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_repair_7_msg"], DialogWindow.DialogType.Question).ShowDialog() == true)
						{
							await Task.Run(() =>
							{
								Log("Creating ZIP file...");
								var zip_name = dialog.FileName.Replace(".json", ".zip");
								DeleteFile(zip_name);
								using(var archive = ZipFile.Open(zip_name, ZipArchiveMode.Create))
								{
									for(int i = 0; i < files.Count; i++)
									{
										archive.CreateEntryFromFile(files[i].FullName, files[i].FullName.Replace($"{GameInstallPath}\\", string.Empty));
										Dispatcher.Invoke(() =>
										{
											ProgressText.Text = string.Format(App.TextStrings["progresstext_zipping"], i + 1, files.Count);
											var progress = (i + 1f) / files.Count;
											ProgressBar.Value = progress;
											TaskbarItemInfo.ProgressValue = progress;
										});
									}
								}
								Log("success!", false);
								Log($"Saved ZIP: {zip_name}");
							});
						}
						Status = LauncherStatus.Ready;
					}
					catch(Exception ex)
					{
						Status = LauncherStatus.Error;
						Log($"ERROR:\n{ex}", true, 1);
						Status = LauncherStatus.Ready;
					}
				}
			}

			if(new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_repair_6_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
			{
				return;
			}
			RepairBox.Visibility = Visibility.Collapsed;
			await Generate();
		}

		private void RepairBoxCloseButton_Click(object sender, RoutedEventArgs e)
		{
			RepairBox.Visibility = Visibility.Collapsed;
		}

		private void FPSInputBoxOKButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				CombatFPSInputBoxTextBox.Text = string.Concat(CombatFPSInputBoxTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
				MenuFPSInputBoxTextBox.Text = string.Concat(MenuFPSInputBoxTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
				if(string.IsNullOrEmpty(CombatFPSInputBoxTextBox.Text) || string.IsNullOrEmpty(MenuFPSInputBoxTextBox.Text))
				{
					new DialogWindow(App.TextStrings["contextmenu_custom_fps"], App.TextStrings["msgbox_custom_fps_1_msg"]).ShowDialog();
					return;
				}
				int fps_combat = int.Parse(CombatFPSInputBoxTextBox.Text);
				int fps_menu = int.Parse(MenuFPSInputBoxTextBox.Text);
				if(fps_combat < 1 || fps_menu < 1)
				{
					new DialogWindow(App.TextStrings["contextmenu_custom_fps"], App.TextStrings["msgbox_custom_fps_2_msg"]).ShowDialog();
					return;
				}
				else if(fps_combat < 30 || fps_menu < 30)
				{
					if(new DialogWindow(App.TextStrings["contextmenu_custom_fps"], App.TextStrings["msgbox_custom_fps_3_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						return;
					}
				}
				GameGraphicSettings.IsUserDefinedGrade = false;
				GameGraphicSettings.IsUserDefinedVolatile = true;
				GameGraphicSettings.TargetFrameRateForInLevel = fps_combat;
				GameGraphicSettings.TargetFrameRateForOthers = fps_menu;
				var value_after = Encoding.UTF8.GetBytes($"{JsonConvert.SerializeObject(GameGraphicSettings)}\0");
				var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
				key.SetValue("GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411", value_after, RegistryValueKind.Binary);
				key.Close();
				FPSInputBox.Visibility = Visibility.Collapsed;
				Log($"Set in-game FPS to {fps_combat}, menu FPS to {fps_menu}");
				new DialogWindow(App.TextStrings["contextmenu_custom_fps"], string.Format(App.TextStrings["msgbox_custom_fps_4_msg"], fps_combat, fps_menu)).ShowDialog();
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private void FPSInputBoxCancelButton_Click(object sender, RoutedEventArgs e)
		{
			FPSInputBox.Visibility = Visibility.Collapsed;
		}

		private void ResolutionInputBoxOKButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				ResolutionInputBoxHeightTextBox.Text = string.Concat(ResolutionInputBoxHeightTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
				ResolutionInputBoxWidthTextBox.Text = string.Concat(ResolutionInputBoxWidthTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
				if(string.IsNullOrEmpty(ResolutionInputBoxHeightTextBox.Text) || string.IsNullOrEmpty(ResolutionInputBoxWidthTextBox.Text))
				{
					new DialogWindow(App.TextStrings["contextmenu_custom_resolution"], App.TextStrings["msgbox_custom_fps_1_msg"]).ShowDialog();
					return;
				}
				bool fullscreen = (bool)ResolutionInputBoxFullscreenCheckbox.IsChecked;
				int height = int.Parse(ResolutionInputBoxHeightTextBox.Text);
				int width = int.Parse(ResolutionInputBoxWidthTextBox.Text);
				if(height < 1 || width < 1)
				{
					new DialogWindow(App.TextStrings["contextmenu_custom_resolution"], App.TextStrings["msgbox_custom_fps_2_msg"]).ShowDialog();
					return;
				}
				else if(height > width)
				{

					if(new DialogWindow(App.TextStrings["contextmenu_custom_resolution"], App.TextStrings["msgbox_custom_resolution_1_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						return;
					}
				}
				GameScreenSettings.height = height;
				GameScreenSettings.width = width;
				GameScreenSettings.isfullScreen = fullscreen;
				var value_after = Encoding.UTF8.GetBytes($"{JsonConvert.SerializeObject(GameScreenSettings)}\0");
				var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
				key.SetValue("GENERAL_DATA_V2_ScreenSettingData_h1916288658", value_after, RegistryValueKind.Binary);
				string sm_fullscreen = "Screenmanager Is Fullscreen mode_h3981298716";
				string sm_res_width = "Screenmanager Resolution Width_h182942802";
				string sm_res_height = "Screenmanager Resolution Height_h2627697771";
				if(key.GetValue(sm_fullscreen) != null)
				{
					key.SetValue(sm_fullscreen, fullscreen, RegistryValueKind.DWord);
				}
				if(key.GetValue(sm_res_width) != null)
				{
					key.SetValue(sm_res_width, width, RegistryValueKind.DWord);
				}
				if(key.GetValue(sm_res_height) != null)
				{
					key.SetValue(sm_res_height, height, RegistryValueKind.DWord);
				}
				key.Close();
				ResolutionInputBox.Visibility = Visibility.Collapsed;
				string is_fullscreen = fullscreen ? "enabled" : "disabled";
				Log($"Set game resolution to {width}x{height}, fullscreen {is_fullscreen}");
				is_fullscreen = fullscreen ? App.TextStrings["enabled"].ToLower() : App.TextStrings["disabled"].ToLower();
				new DialogWindow(App.TextStrings["contextmenu_custom_resolution"], string.Format(App.TextStrings["msgbox_custom_resolution_2_msg"], width, height, is_fullscreen)).ShowDialog();
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private void ResolutionInputBoxCancelButton_Click(object sender, RoutedEventArgs e)
		{
			ResolutionInputBox.Visibility = Visibility.Collapsed;
		}

		private void ChangelogBoxCloseButton_Click(object sender, RoutedEventArgs e)
		{
			ChangelogBox.Visibility = Visibility.Collapsed;
			ChangelogBoxMessageTextBlock.Visibility = Visibility.Collapsed;
		}

		private void ShowLogCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			LogBox.Visibility = Visibility.Visible;
			try
			{
				BpUtility.WriteToRegistry("ShowLog", 1, RegistryValueKind.DWord);
			}
			catch(Exception ex)
			{
				Log($"ERROR: Failed to write value with key ShowLog to registry:\n{ex}", true, 1);
			}
		}

		private void ShowLogCheckBox_Unchecked(object sender, RoutedEventArgs e)
		{
			LogBox.Visibility = Visibility.Collapsed;
			try
			{
				BpUtility.WriteToRegistry("ShowLog", 0, RegistryValueKind.DWord);
			}
			catch(Exception ex)
			{
				Log($"ERROR: Failed to write value with key ShowLog to registry:\n{ex}", true, 1);
			}
		}

		private void ShowLogCheckBox_Click(object sender, RoutedEventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Click);
		}

		private void AboutBoxGitHubButton_Click(object sender, RoutedEventArgs e)
		{
			AboutBox.Visibility = Visibility.Collapsed;
			BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher", null, App.LauncherRootPath, true);
		}

		private void AboutBoxCloseButton_Click(object sender, RoutedEventArgs e)
		{
			AboutBox.Visibility = Visibility.Collapsed;
		}

		private void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			if(Status == LauncherStatus.Downloading || Status == LauncherStatus.DownloadPaused)
			{
				if(download == null)
				{
					if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_1_msg"]}\n{App.TextStrings["msgbox_abort_2_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == true)
					{
						Status = LauncherStatus.CleaningUp;
						DeleteFile(GameArchivePath);
						DeleteFile(CacheArchivePath);
					}
					else
					{
						e.Cancel = true;
					}
				}
				else
				{
					if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_1_msg"]}\n{App.TextStrings["msgbox_abort_3_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == true)
					{
						download.Pause();
						WriteVersionInfo();
					}
					else
					{
						e.Cancel = true;
					}
				}
			}
			else if(Status == LauncherStatus.Preloading)
			{
				if(download != null)
				{
					download.Pause();
				}
			}
			else if(Status == LauncherStatus.Verifying || Status == LauncherStatus.Unpacking || Status == LauncherStatus.CleaningUp || Status == LauncherStatus.Uninstalling || Status == LauncherStatus.Working || Status == LauncherStatus.PreloadVerifying)
			{
				e.Cancel = true;
			}
		}

		private void OnGameExit()
		{
			Dispatcher.Invoke(() =>
			{
				LaunchButton.Content = App.TextStrings["button_launch"];
				Status = LauncherStatus.Ready;
				WindowState = WindowState.Normal;
			});
		}

		private string CheckForExistingGameDirectory(string path)
		{
			if(string.IsNullOrEmpty(path))
			{
				return string.Empty;
			}

			var path_variants = new List<string>(new string[]
			{
				path.Replace(@"\BH3_Data", string.Empty),
				Path.Combine(path, "Games"),
				Path.Combine(path, "Honkai Impact 3rd"),
				Path.Combine(path, "Honkai Impact 3"),
				Path.Combine(path, "Honkai Impact 3rd", "Games"),
				Path.Combine(path, "Honkai Impact 3", "Games"),
				Path.Combine(path, "Honkai Impact 3rd glb", "Games"),
				Path.Combine(path, "Honkai Impact 3 sea", "Games")
			});
			if(path.Length >= 16)
			{
				path_variants.Add(path.Substring(0, path.Length - 16));
			}
			if(path.Length >= 18)
			{
				path_variants.Add(path.Substring(0, path.Length - 18));
			}

			foreach(var variant in path_variants)
			{
				if(string.IsNullOrEmpty(variant))
				{
					continue;
				}

				if(File.Exists(Path.Combine(variant, GameExeName)))
				{
					return variant;
				}
			}
			return string.Empty;
		}

		private int CheckForExistingGameClientServer()
		{
			var path = Path.Combine(GameInstallPath, @"BH3_Data\app.info");
			if(File.Exists(path))
			{
				var game_title_line = File.ReadLines(path).Skip(1).Take(1).First();
				if(!string.IsNullOrEmpty(game_title_line))
				{
					if(game_title_line.Contains("Honkai Impact 3rd"))
					{
						return 0;
					}
					else if(game_title_line.Contains("Honkai Impact 3"))
					{
						return 1;
					}

				}
			}
			return -1;
		}

		private void ToggleContextMenuItems(bool val, bool leave_uninstall_enabled = false)
		{
			foreach(dynamic item in OptionsContextMenu.Items)
			{
				if(item.GetType() == typeof(MenuItem))
				{
					if(item.Header.ToString() == App.TextStrings["contextmenu_web_profile"] ||
					   item.Header.ToString() == App.TextStrings["contextmenu_feedback"] ||
					   item.Header.ToString() == App.TextStrings["contextmenu_changelog"] ||
					   item.Header.ToString() == App.TextStrings["contextmenu_language"] ||
					   item.Header.ToString() == App.TextStrings["contextmenu_custom_background"] ||
					   item.Header.ToString() == App.TextStrings["contextmenu_sounds"] ||
					   item.Header.ToString() == App.TextStrings["contextmenu_about"])
					{
						continue;
					}
				}
				if(!val && leave_uninstall_enabled)
				{
					if(item.GetType() == typeof(MenuItem) && item.Header.ToString() == App.TextStrings["contextmenu_uninstall"])
					{
						continue;
					}
				}
					
				item.IsEnabled = val;
			}
		}

		private Tuple<string, int, bool> GetCustomBackgroundFileInfo(string path)
		{
			string name = Path.GetFileName(path);
			int format = 0;
			bool is_recommended_resolution = false;
			bool check_media = false;
			if(!string.IsNullOrEmpty(path) && Path.HasExtension(path))
			{ 
				BitmapImage image;
				try
				{
					image = new BitmapImage(new Uri(path));
					if(Path.GetExtension(path) != ".gif")
					{
						format = 1;
					}
					else
					{
						format = 2;
					}
					if(image.Width == Grid.Width && image.Height == Grid.Height)
					{
						is_recommended_resolution = true;
					}
				}
				catch
				{
					check_media = true;
				}
				if(check_media)
				{ 
					try
					{
						int timeout = 3000;
						var old_source = BackgroundMedia.Source;
						BackgroundMedia.Source = new Uri(path);
						while(timeout > 0)
						{
							if(BackgroundMedia.NaturalDuration.HasTimeSpan && BackgroundMedia.NaturalDuration.TimeSpan.TotalMilliseconds > 0)
							{
								if(BackgroundMedia.HasVideo)
								{
									format = 3;
									if(BackgroundMedia.NaturalVideoWidth == Grid.Width && BackgroundMedia.NaturalVideoHeight == Grid.Height)
									{
										is_recommended_resolution = true;
									}
								}
								break;
							}
							timeout -= 100;
							Thread.Sleep(100);
						}
						BackgroundMedia.Source = old_source;
					}catch{}
				}
			}
			else
			{
				format = -1;
			}
			return Tuple.Create(name, format, is_recommended_resolution);
		}

		private void SetCustomBackgroundFile(string path, bool new_file = false)
		{
			try
			{
				var file_info = GetCustomBackgroundFileInfo(path);
				string name = file_info.Item1;
				int format = file_info.Item2;
				bool is_recommended_resolution = file_info.Item3;
				if(format > 0)
				{
					if(new_file)
					{
						if(!is_recommended_resolution)
						{
							if(new DialogWindow(App.TextStrings["contextmenu_custom_background"], App.TextStrings["msgbox_custom_background_3_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
							{
								return;
							}
						}
						if(LauncherRegKey.GetValue("CustomBackgroundName") != null)
						{
							DeleteFile(Path.Combine(App.LauncherBackgroundsPath, LauncherRegKey.GetValue("CustomBackgroundName").ToString()));
						}
						Log($"Setting custom background: {path}");
						File.Copy(path, Path.Combine(App.LauncherBackgroundsPath, name), true);
						BpUtility.WriteToRegistry("CustomBackgroundName", name);
					}
					BackgroundImage.Visibility = Visibility.Collapsed;
					BackgroundImage.Source = (BitmapImage)Resources["BackgroundImage"];
					BackgroundMedia.Visibility = Visibility.Collapsed;
					BackgroundMedia.Source = null;
				}
				switch(format)
				{
					case 0:
						if(new_file)
						{
							new DialogWindow(App.TextStrings["contextmenu_custom_background"], App.TextStrings["msgbox_custom_background_7_msg"]).ShowDialog();
						}
						else
						{
							throw new FileFormatException("File is in unsupported format");
						}
						break;
					case 1:
						BackgroundImage.Visibility = Visibility.Visible;
						BackgroundImage.Source = new BitmapImage(new Uri(path));
						break;
					case 2:
						BackgroundImage.Visibility = Visibility.Visible;
						XamlAnimatedGif.AnimationBehavior.SetSourceUri(BackgroundImage, new Uri(path));
						break;
					case 3:
						BackgroundMedia.Visibility = Visibility.Visible;
						BackgroundMedia.Source = new Uri(path);
						BackgroundMedia.MediaEnded += (object sender, RoutedEventArgs e) =>
						{
							BackgroundMedia.Position = TimeSpan.FromMilliseconds(1);
						};
						break;
					default:
						if(new_file)
						{
							new DialogWindow(App.TextStrings["contextmenu_custom_background"], App.TextStrings["msgbox_custom_background_8_msg"]).ShowDialog();
						}
						else
						{
							throw new FileLoadException("Could not load the file");
						}
						break;
				}
			}
			catch(Exception ex)
			{
				Log($"ERROR: Failed to set custom background:\n{ex}", true, 1);
			}
		}

		public void SetLanguage(string lang)
		{
			App.LauncherLanguage = "en";
			if(File.Exists(App.LauncherTranslationsFile))
			{
				try
				{
					var json = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(App.LauncherTranslationsFile));
					foreach(var kvp in json[lang])
					{
						App.TextStrings[kvp.Name] = kvp.Value.ToString();
					}
					App.LauncherLanguage = lang;
				}
				catch
				{
					BpUtility.DeleteFromRegistry("Language");
					DeleteFile(App.LauncherTranslationsFile, true);
				}
			}
			if(App.LauncherLanguage != "en")
			{
				Resources["Font"] = new FontFamily("Segoe UI Bold");
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
					color = Colors.Red;
					#if DEBUG
						ccolor = ConsoleColor.Red;
					#endif
					break;
				case 2:
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
				LogBoxScrollViewer.ScrollToEnd();
			});
			if(!DisableLogging)
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
					DisableLogging = true;
					Log("WARNING: Unable to write to log file", true, 2);
				}
			}
		}

		public void DeleteFile(string path, bool ignore_read_only = false)
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
			}
			catch
			{
				Log($"WARNING: Failed to delete {path}", true, 2);
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
	}
}