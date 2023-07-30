using Hi3Helper.Http;
using Microsoft.Win32;
using SevenZip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
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
using System.Windows.Shell;

namespace BetterHI3Launcher
{
	public partial class MainWindow : Window
	{
		public static readonly string miHoYoPath = Path.Combine(App.LocalLowPath, "miHoYo");
		public static string GameInstallPath, GameCachePath, GameRegistryPath, GameArchivePath, GameArchiveTempPath, GameExePath;
		public static string RegistryVersionInfo;
		public static string GameWebProfileURL, GameFullName, GameArchiveName, GameExeName, GameInstallRegistryName;
		public static bool DownloadPaused, PatchDownload, PreloadDownload, BackgroundImageDownloading, LegacyBoxActive, ActionAbort;
		public static int PatchDownloadInt;
		public static RoutedCommand DownloadCacheCommand = new RoutedCommand();
		public static RoutedCommand RepairGameCommand = new RoutedCommand();
		public static RoutedCommand MoveGameCommand = new RoutedCommand();
		public static RoutedCommand UninstallGameCommand = new RoutedCommand();
		public static RoutedCommand WebProfileCommand = new RoutedCommand();
		public static RoutedCommand FeedbackCommand = new RoutedCommand();
		public static RoutedCommand ChangelogCommand = new RoutedCommand();
		public static RoutedCommand CustomBackgroundCommand = new RoutedCommand();
		public static RoutedCommand ToggleLogCommand = new RoutedCommand();
		public static RoutedCommand ToggleSoundsCommand = new RoutedCommand();
		public static RoutedCommand AboutCommand = new RoutedCommand();
		public dynamic LocalVersionInfo, OnlineVersionInfo, OnlineRepairInfo, miHoYoVersionInfo;
		public dynamic GameGraphicSettings, GameScreenSettings;
		LauncherStatus _status;
		HI3Server _gameserver;
		HI3Mirror _downloadmirror;
		Http httpclient;
		HttpProp httpprop;
		CancellationTokenSource token;
		DownloadProgressTracker tracker = new DownloadProgressTracker(50, TimeSpan.FromMilliseconds(500));

		internal LauncherStatus Status
		{
			get => _status;
			set => Dispatcher.Invoke(() =>
			{
				void ToggleUI(bool val)
				{
					LaunchButton.IsEnabled = val;
					OptionsButton.IsEnabled = val;
					ServerDropdown.IsEnabled = val;
					MirrorDropdown.IsEnabled = val;
					ToggleContextMenuItems(val);
					DownloadProgressBarStackPanel.Visibility = Visibility.Collapsed;
					DownloadETAText.Visibility = Visibility.Visible;
					DownloadSpeedText.Visibility = Visibility.Visible;
					DownloadPauseButton.Visibility = Visibility.Visible;
					DownloadResumeButton.Visibility = Visibility.Collapsed;
				}
				void ToggleProgressBar(bool val)
				{
					ProgressBar.Visibility = val ? Visibility.Visible : Visibility.Collapsed;
					ProgressBar.IsIndeterminate = true;
					TaskbarItemInfo.ProgressState = val ? TaskbarItemProgressState.Indeterminate : TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
				}

				_status = value;
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
						ToggleLog(true);
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
						FlashMainWindow();
						break;
					case LauncherStatus.CheckingUpdates:
						ProgressText.Text = App.TextStrings["progresstext_checking_update"];
						ToggleUI(false);
						ToggleProgressBar(true);
						break;
					case LauncherStatus.Downloading:
						DownloadPaused = false;
						ProgressText.Text = App.TextStrings["progresstext_initiating_download"];
						LaunchButton.Content = App.TextStrings["button_downloading"];
						ToggleUI(false);
						OptionsButton.IsEnabled = true;
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
						PreloadPauseButton.IsEnabled = false;
						PreloadPauseButton.Visibility = Visibility.Visible;
						PreloadPauseButton.Background = (ImageBrush)Resources["PreloadPauseButton"];
						PreloadCircle.Visibility = Visibility.Visible;
						PreloadCircleProgressBar.Visibility = Visibility.Visible;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
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
						OptionsButton.IsEnabled = true;
						ToggleProgressBar(true);
						break;
					case LauncherStatus.Unpacking:
						ProgressText.Text = string.Empty;
						ToggleProgressBar(false);
						DownloadProgressBarStackPanel.Visibility = Visibility.Visible;
						DownloadProgressText.Text = App.TextStrings["progresstext_unpacking_1"];
						DownloadPauseButton.Visibility = Visibility.Collapsed;
						DownloadETAText.Visibility = Visibility.Collapsed;
						DownloadSpeedText.Visibility = Visibility.Collapsed;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
						break;
					case LauncherStatus.UpdateAvailable:
						ProgressText.Text = string.Empty;
						ToggleUI(true);
						ToggleProgressBar(false);
						ToggleContextMenuItems(false, true);
						ProgressBar.IsIndeterminate = false;
						break;
					case LauncherStatus.Uninstalling:
						ProgressText.Text = App.TextStrings["progresstext_uninstalling"];
						ToggleUI(false);
						OptionsButton.IsEnabled = true;
						ToggleProgressBar(true);
						break;
				}
			});
		}

		internal HI3Server Server
		{
			get => _gameserver;
			set
			{
				_gameserver = value;
				switch(_gameserver)
				{
					case HI3Server.GLB:
						RegistryVersionInfo = "VersionInfoGlobal";
						GameFullName = "Honkai Impact 3rd";
						GameInstallRegistryName = GameFullName;
						GameWebProfileURL = "https://account.hoyoverse.com";
						break;
					case HI3Server.SEA:
						RegistryVersionInfo = "VersionInfoSEA";
						GameFullName = "Honkai Impact 3";
						GameInstallRegistryName = GameFullName;
						GameWebProfileURL = "https://account.hoyoverse.com";
						break;
					case HI3Server.CN:
						RegistryVersionInfo = "VersionInfoCN";
						GameFullName = "崩坏3";
						GameInstallRegistryName = GameFullName;
						GameWebProfileURL = "https://user.mihoyo.com";
						break;
					case HI3Server.TW:
						RegistryVersionInfo = "VersionInfoTW";
						GameFullName = "崩壊3rd";
						GameInstallRegistryName = "崩壞3rd";
						GameWebProfileURL = "https://account.hoyoverse.com";
						break;
					case HI3Server.KR:
						RegistryVersionInfo = "VersionInfoKR";
						GameFullName = "붕괴3rd";
						GameInstallRegistryName = GameFullName;
						GameWebProfileURL = "https://account.hoyoverse.com";
						break;
					case HI3Server.JP:
						RegistryVersionInfo = "VersionInfoJP";
						GameFullName = "崩壊3rd";
						GameInstallRegistryName = GameFullName;
						GameWebProfileURL = "https://account.hoyoverse.com";
						break;
				}
				GameRegistryPath = $@"SOFTWARE\miHoYo\{GameFullName}";
				GameCachePath = Path.Combine(miHoYoPath, GameFullName);
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
			for(int i = 1; i < App.CommandLineArgs.Length; i++)
			{
				args.Add(App.CommandLineArgs[i].ToUpper());
			}
			if(args.Contains("NOLOG"))
			{
				App.DisableLogging = true;
			}
			if(!App.DisableLogging)
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
					Log("Unable to rename log files", true, 2);
				}
			}
			DeleteFile(App.LauncherLogFile, true);

			Log($"BetterHI3Launcher v{App.LocalLauncherVersion}", false);
			Log($"Working directory: {App.LauncherRootPath}");
			Log($"OS version: {App.OSVersion}");
			Log($"OS language: {App.OSLanguage}");
			#if !DEBUG
			if(args.Contains("NOUPDATE"))
			{
				App.DisableAutoUpdate = true;
				App.UserAgentComment.Add("NOUPDATE");
				Log("Auto-update disabled");
			}
			#endif
			if(args.Contains("NOLOG"))
			{
				App.UserAgentComment.Add("NOLOG");
				Log("Logging to file disabled");
			}
			if(args.Contains("NOTRANSLATIONS"))
			{
				App.DisableTranslations = true;
				App.LauncherLanguage = "en";
				App.UserAgentComment.Add("NOTRANSLATIONS");
				Log("Translations disabled, only English will be available");
			}
			if(args.Contains("ADVANCED"))
			{
				App.AdvancedFeatures = true;
				App.UserAgentComment.Add("ADVANCED");
				Log("Advanced features enabled");
			}
			else
			{
				RepairBoxGenerateButton.Visibility = Visibility.Collapsed;
			}

			string language_log_msg = "Launcher language: {0}";
			var language_reg = App.LauncherRegKey.GetValue("Language");
			if(!App.DisableTranslations)
			{
				if(language_reg != null)
				{
					if(App.LauncherRegKey.GetValueKind("Language") == RegistryValueKind.String)
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
			language_log_msg = string.Format(language_log_msg, App.LauncherLanguage);
			Log(language_log_msg);
			App.UserAgentComment.Add(App.LauncherLanguage);
			App.UserAgentComment.Add(App.OSLanguage);
			App.UserAgentComment.Add(App.OSVersion);
			App.UserAgent += $" ({string.Join("; ", App.UserAgentComment)})";

			LaunchButton.Content = App.TextStrings["button_download"];
			ServerLabel.Text = $"{App.TextStrings["label_server"]}:";
			MirrorLabel.Text = $"{App.TextStrings["label_mirror"]}:";
			IntroBoxTitleTextBlock.Text = App.TextStrings["introbox_title"];
			IntroBoxMessageTextBlock.Text = App.TextStrings["introbox_msg"];
			IntroBoxOKButton.Content = App.TextStrings["button_ok"];
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
			AnnouncementBoxOKButton.Content = App.TextStrings["button_ok"];
			AnnouncementBoxDoNotShowCheckbox.Content = App.TextStrings["announcementbox_do_not_show"];
			PreloadTopText.Text = App.TextStrings["label_pre_install"];
			PreloadStatusMiddleLeftText.Text = App.TextStrings["label_eta"];

			TitleBar.MouseLeftButtonDown += delegate{DragMove();};
			PreloadGrid.Visibility = Visibility.Collapsed;
			DownloadProgressBarStackPanel.Visibility = Visibility.Collapsed;
			LogBox.Visibility = Visibility.Collapsed;
			LogBoxRichTextBox.Document.PageWidth = LogBox.Width;
			IntroBox.Visibility = Visibility.Collapsed;
			RepairBox.Visibility = Visibility.Collapsed;
			FPSInputBox.Visibility = Visibility.Collapsed;
			ResolutionInputBox.Visibility = Visibility.Collapsed;
			ChangelogBox.Visibility = Visibility.Collapsed;
			AboutBox.Visibility = Visibility.Collapsed;
			AnnouncementBox.Visibility = Visibility.Collapsed;

			OptionsContextMenu.Items.Clear();
			var CM_Screenshots = new MenuItem {Header = App.TextStrings["contextmenu_open_screenshots_dir"], InputGestureText = "Ctrl+S"};
			CM_Screenshots.Click += (sender, e) => CM_Screenshots_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Screenshots);
			var CM_Download_Cache = new MenuItem{Header = App.TextStrings["contextmenu_download_cache"], InputGestureText = "Ctrl+D"};
			CM_Download_Cache.Click += (sender, e) => CM_DownloadCache_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Download_Cache);
			var CM_Repair = new MenuItem{Header = App.TextStrings["contextmenu_repair"], InputGestureText = "Ctrl+R"};
			CM_Repair.Click += async (sender, e) => await CM_Repair_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Repair);
			var CM_Move = new MenuItem{Header = App.TextStrings["contextmenu_move"], InputGestureText = "Ctrl+M"};
			CM_Move.Click += async (sender, e) => await CM_Move_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Move);
			var CM_Uninstall = new MenuItem{Header = App.TextStrings["contextmenu_uninstall"], InputGestureText = "Ctrl+U"};
			CM_Uninstall.Click += async (sender, e) => await CM_Uninstall_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Uninstall);
			var CM_Game_Settings = new MenuItem{Header = App.TextStrings["contextmenu_game_settings"]};
			var CM_Custom_FPS = new MenuItem{Header = App.TextStrings["contextmenu_custom_fps"]};
			CM_Custom_FPS.Click += (sender, e) => CM_CustomFPS_Click(sender, e);
			CM_Game_Settings.Items.Add(CM_Custom_FPS);
			var CM_Custom_Resolution = new MenuItem{Header = App.TextStrings["contextmenu_custom_resolution"]};
			CM_Custom_Resolution.Click += (sender, e) => CM_CustomResolution_Click(sender, e);
			CM_Game_Settings.Items.Add(CM_Custom_Resolution);
			var CM_Custom_Launch_Options = new MenuItem{Header = App.TextStrings["contextmenu_custom_launch_options"]};
			CM_Custom_Launch_Options.Click += (sender, e) => CM_CustomLaunchOptions_Click(sender, e);
			CM_Game_Settings.Items.Add(CM_Custom_Launch_Options);
			var CM_Download_Type = new MenuItem{Header = App.TextStrings["contextmenu_reset_download_type"]};
			CM_Download_Type.Click += (sender, e) => CM_ResetDownloadType_Click(sender, e);
			CM_Game_Settings.Items.Add(CM_Download_Type);
			OptionsContextMenu.Items.Add(CM_Game_Settings);
			OptionsContextMenu.Items.Add(new Separator());
			var CM_Web_Profile = new MenuItem{Header = App.TextStrings["contextmenu_web_profile"], InputGestureText = "Ctrl+P"};
			CM_Web_Profile.Click += (sender, e) => BpUtility.StartProcess(GameWebProfileURL, null, App.LauncherRootPath, true);
			OptionsContextMenu.Items.Add(CM_Web_Profile);
			var CM_Feedback = new MenuItem{Header = App.TextStrings["contextmenu_feedback"], InputGestureText = "Ctrl+F"};
			CM_Feedback.Click += (sender, e) => BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new/choose", null, App.LauncherRootPath, true);
			OptionsContextMenu.Items.Add(CM_Feedback);
			var CM_Changelog = new MenuItem{Header = App.TextStrings["contextmenu_changelog"], InputGestureText = "Ctrl+C"};
			CM_Changelog.Click += (sender, e) => CM_Changelog_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Changelog);
			var CM_Custom_Background = new MenuItem{Header = App.TextStrings["contextmenu_custom_background"], InputGestureText = "Ctrl+B"};
			CM_Custom_Background.Click += (sender, e) => CM_CustomBackground_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Custom_Background);
			var CM_ShowLog = new MenuItem{Header = App.TextStrings["contextmenu_show_log"], InputGestureText = "Ctrl+L"};
			CM_ShowLog.Click += (sender, e) => CM_ShowLog_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_ShowLog);
			var CM_Sounds = new MenuItem{Header = App.TextStrings["contextmenu_sounds"], InputGestureText = "Ctrl+S", IsChecked = true};
			CM_Sounds.Click += (sender, e) => CM_Sounds_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Sounds);
			var CM_Language = new MenuItem{Header = App.TextStrings["contextmenu_language"]};
			var CM_Language_System = new MenuItem{Header = App.TextStrings["contextmenu_language_system"]};
			CM_Language_System.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_System);
			var CM_Language_Chinese_Simplified = new MenuItem {Header = App.TextStrings["contextmenu_language_chinese_simplified"]};
			CM_Language_Chinese_Simplified.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Chinese_Simplified);
			var CM_Language_Czech = new MenuItem {Header = App.TextStrings["contextmenu_language_czech"]};
			CM_Language_Czech.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Czech);
			var CM_Language_English = new MenuItem{Header = App.TextStrings["contextmenu_language_english"]};
			CM_Language_English.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_English);
			var CM_Language_French = new MenuItem{Header = App.TextStrings["contextmenu_language_french"]};
			CM_Language_French.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_French);
			var CM_Language_German = new MenuItem{Header = App.TextStrings["contextmenu_language_german"]};
			CM_Language_German.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_German);
			var CM_Language_Indonesian = new MenuItem{Header = App.TextStrings["contextmenu_language_indonesian"]};
			CM_Language_Indonesian.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Indonesian);
			var CM_Language_Italian = new MenuItem {Header = App.TextStrings["contextmenu_language_italian"]};
			CM_Language_Italian.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Italian);
			var CM_Language_Portuguese_Brazil = new MenuItem{Header = App.TextStrings["contextmenu_language_portuguese_brazil"]};
			CM_Language_Portuguese_Brazil.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Portuguese_Brazil);
			var CM_Language_Portuguese_Portugal = new MenuItem{Header = App.TextStrings["contextmenu_language_portuguese_portugal"]};
			CM_Language_Portuguese_Portugal.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Portuguese_Portugal);
			var CM_Language_Russian = new MenuItem{Header = App.TextStrings["contextmenu_language_russian"]};
			CM_Language_Russian.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Russian);
			var CM_Language_Serbian = new MenuItem{Header = App.TextStrings["contextmenu_language_serbian"]};
			CM_Language_Serbian.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Serbian);
			var CM_Language_Spanish = new MenuItem{Header = App.TextStrings["contextmenu_language_spanish"]};
			CM_Language_Spanish.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Spanish);
			var CM_Language_Thai = new MenuItem{Header = App.TextStrings["contextmenu_language_thai"]};
			CM_Language_Thai.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Thai);
			var CM_Language_Vietnamese = new MenuItem{Header = App.TextStrings["contextmenu_language_vietnamese"]};
			CM_Language_Vietnamese.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_Vietnamese);
			CM_Language.Items.Add(new Separator());
			var CM_Language_Contribute = new MenuItem{Header = App.TextStrings["contextmenu_language_contribute"]};
			CM_Language_Contribute.Click += (sender, e) => BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher#how-can-i-contribute", null, App.LauncherRootPath, true);
			CM_Language.Items.Add(CM_Language_Contribute);
			OptionsContextMenu.Items.Add(CM_Language);
			var CM_About = new MenuItem{Header = App.TextStrings["contextmenu_about"], InputGestureText = "Ctrl+A"};
			CM_About.Click += (sender, e) => CM_About_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_About);

			if(!App.DisableTranslations)
			{
				if(language_reg == null)
				{
					CM_Language_System.IsChecked = true;
				}
				else
				{
					switch(language_reg.ToString())
					{
						case "cs":
							CM_Language_Czech.IsChecked = true;
							break;
						case "fr":
							CM_Language_French.IsChecked = true;
							break;
						case "de":
							CM_Language_German.IsChecked = true;
							break;
						case "id":
							CM_Language_Indonesian.IsChecked = true;
							break;
						case "it":
							CM_Language_Italian.IsChecked = true;
							break;
						case "pt-BR":
							CM_Language_Portuguese_Brazil.IsChecked = true;
							break;
						case "pt-PT":
							CM_Language_Portuguese_Portugal.IsChecked = true;
							break;
						case "ru":
							CM_Language_Russian.IsChecked = true;
							break;
						case "sr":
							CM_Language_Serbian.IsChecked = true;
							break;
						case "es":
							CM_Language_Spanish.IsChecked = true;
							break;
						case "th":
							CM_Language_Thai.IsChecked = true;
							break;
						case "vi":
							CM_Language_Vietnamese.IsChecked = true;
							break;
						case "zh-CN":
							CM_Language_Chinese_Simplified.IsChecked = true;
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
			if(key == null || (int)key.GetValue("Release") < 394802)
			{
				MessageBox.Show(App.TextStrings["msgbox_net_version_old_msg"], App.TextStrings["msgbox_start_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
				Application.Current.Shutdown();
				return;
			}

			Server = HI3Server.GLB;
			try
			{
				var last_selected_server_reg = App.LauncherRegKey.GetValue("LastSelectedServer");
				if(last_selected_server_reg != null)
				{
					if(App.LauncherRegKey.GetValueKind("LastSelectedServer") == RegistryValueKind.DWord)
					{
						switch((int)last_selected_server_reg)
						{
							case 0:
								Server = HI3Server.GLB;
								break;
							case 1:
								Server = HI3Server.SEA;
								break;
							case 2:
								Server = HI3Server.CN;
								break;
							case 3:
								Server = HI3Server.TW;
								break;
							case 4:
								Server = HI3Server.KR;
								break;
							case 5:
								Server = HI3Server.JP;
								break;
						}
					}
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
					MessageBox.Show($"{App.TextStrings["msgbox_conn_bp_error_msg"]}\n{ex.Message}", App.TextStrings["msgbox_net_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
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
					MessageBox.Show($"{App.TextStrings["msgbox_conn_mihoyo_error_msg"]}\n{ex.Message}", App.TextStrings["msgbox_net_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
					Application.Current.Shutdown();
					return;
				}

				Mirror = HI3Mirror.miHoYo;
				var last_selected_mirror_reg = App.LauncherRegKey.GetValue("LastSelectedMirror");
				if(last_selected_mirror_reg != null)
				{
					if(App.LauncherRegKey.GetValueKind("LastSelectedMirror") == RegistryValueKind.DWord)
					{
						if((int)last_selected_mirror_reg == 0)
						{
							Mirror = HI3Mirror.miHoYo;
						}
						else if((int)last_selected_mirror_reg == 1)
						{
							Mirror = HI3Mirror.BpNetwork;
						}
					}
				}
				if(Server != HI3Server.GLB && Server != HI3Server.SEA)
				{
					Mirror = HI3Mirror.miHoYo;
				}
				MirrorDropdown.SelectedIndex = (int)Mirror;

				var seen_announcements_reg = App.LauncherRegKey.GetValue("SeenAnnouncements");
				if(seen_announcements_reg != null)
				{
					if(App.LauncherRegKey.GetValueKind("SeenAnnouncements") == RegistryValueKind.String)
					{
						App.SeenAnnouncements = seen_announcements_reg.ToString().Split(',').ToList();
					}
				}

				var show_log_reg = App.LauncherRegKey.GetValue("ShowLog");
				if(show_log_reg != null)
				{
					if(App.LauncherRegKey.GetValueKind("ShowLog") == RegistryValueKind.DWord)
					{
						if((int)show_log_reg == 1)
						{
							ToggleLog(true);
							CM_ShowLog.IsChecked = true;
						}
					}
				}

				var sounds_reg = App.LauncherRegKey.GetValue("Sounds");
				if(sounds_reg != null)
				{
					if(App.LauncherRegKey.GetValueKind("Sounds") == RegistryValueKind.DWord)
					{
						if((int)sounds_reg == 0)
						{
							App.DisableSounds = true;
							CM_Sounds.IsChecked = false;
						}
					}
				}

				DownloadCacheCommand.InputGestures.Add(new KeyGesture(Key.D, ModifierKeys.Control));
				RepairGameCommand.InputGestures.Add(new KeyGesture(Key.R, ModifierKeys.Control));
				MoveGameCommand.InputGestures.Add(new KeyGesture(Key.M, ModifierKeys.Control));
				UninstallGameCommand.InputGestures.Add(new KeyGesture(Key.U, ModifierKeys.Control));
				WebProfileCommand.InputGestures.Add(new KeyGesture(Key.P, ModifierKeys.Control));
				FeedbackCommand.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));
				ChangelogCommand.InputGestures.Add(new KeyGesture(Key.C, ModifierKeys.Control));
				CustomBackgroundCommand.InputGestures.Add(new KeyGesture(Key.B, ModifierKeys.Control));
				ToggleLogCommand.InputGestures.Add(new KeyGesture(Key.L, ModifierKeys.Control));
				ToggleSoundsCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
				AboutCommand.InputGestures.Add(new KeyGesture(Key.A, ModifierKeys.Control));

				App.NeedsUpdate = LauncherUpdateCheck();
				if(!App.DisableTranslations)
				{
					if(!App.DisableAutoUpdate && !App.NeedsUpdate)
					{
						DownloadLauncherTranslations();
					}
				}

				Log($"Using server: {((ComboBoxItem)ServerDropdown.SelectedItem).Content as string}");
				Log($"Using mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");

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

		private async void Window_ContentRendered(object sender, EventArgs e)
		{
			#if DEBUG
			App.DisableAutoUpdate = true;
			#endif
			try
			{
				string exe_name = Process.GetCurrentProcess().MainModule.ModuleName;
				string old_exe_name = $"{Path.GetFileNameWithoutExtension(App.LauncherPath)}_old.exe";

				if(Process.GetCurrentProcess().MainModule.ModuleName != App.LauncherExeName)
				{
					Status = LauncherStatus.Error;
					DeleteFile(App.LauncherPath, true);
					File.Move(Path.Combine(App.LauncherRootPath, exe_name), App.LauncherPath);
					BpUtility.RestartApp();
					return;
				}
				DeleteFile(Path.Combine(App.LauncherRootPath, old_exe_name), true);
				await Task.Run(() =>
				{
					if(App.DisableAutoUpdate)
					{
						return;
					}

					if(!App.NeedsUpdate)
					{
						if(BpUtility.CalculateMD5(App.LauncherPath) != OnlineVersionInfo.launcher_info.exe_md5.ToString().ToUpper())
						{
							Log($"Launcher integrity error, attempting self-repair...", true, 1);
							App.NeedsUpdate = true;
						}
					}
					if(App.NeedsUpdate)
					{
						Log("A newer version of the launcher is available!");
						Status = LauncherStatus.Working;
						DownloadLauncherUpdate();
						Log("Validating update...");
						string md5 = OnlineVersionInfo.launcher_info.md5.ToString().ToUpper();
						string actual_md5 = BpUtility.CalculateMD5(App.LauncherArchivePath);
						if(actual_md5 != md5)
						{
							Status = LauncherStatus.Error;
							Log($"Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
							DeleteFile(App.LauncherArchivePath, true);
							Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_verify_error_title"], App.TextStrings["msgbox_verify_error_1_msg"]).ShowDialog();});
							return;
						}
						Log("success!", false);
						Log("Performing update...");
						File.Move(Path.Combine(App.LauncherRootPath, exe_name), Path.Combine(App.LauncherRootPath, old_exe_name));
						using(var archive = new SevenZipExtractor(App.LauncherArchivePath))
						{
							archive.ExtractArchive(App.LauncherRootPath);
						}
						Log("success!", false);
						Dispatcher.Invoke(() => {BpUtility.RestartApp();});
						return;
					}
					else
					{
						DeleteFile(App.LauncherArchivePath, true);
						if(!File.Exists(App.LauncherPath))
						{
							File.Copy(Path.Combine(App.LauncherRootPath, exe_name), App.LauncherPath, true);
						}
					}
				});
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to start the launcher:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_start_error_title"], string.Format(App.TextStrings["msgbox_start_error_msg"], ex.Message)).ShowDialog();
				Application.Current.Shutdown();
				return;
			}


			if(App.FirstLaunch)
			{
				LegacyBoxActive = true;
				IntroBox.Visibility = Visibility.Visible;
				ProgressBar.Visibility = Visibility.Collapsed;
			}
			else
			{
				FetchAnnouncements();
			}
			var custom_background_name_reg = App.LauncherRegKey.GetValue("CustomBackgroundName");
			if(custom_background_name_reg != null)
			{
				if(App.LauncherRegKey.GetValueKind("CustomBackgroundName") == RegistryValueKind.String)
				{
					string path = Path.Combine(App.LauncherBackgroundsPath, custom_background_name_reg.ToString());
					if(File.Exists(path))
					{
						SetCustomBackgroundFile(path);
					}
					else
					{
						Log("Custom background file cannot be found, resetting to official...", true, 2);
						BpUtility.DeleteFromRegistry("CustomBackgroundName");
					}
				}
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			if(LegacyBoxActive)
			{
				return;
			}

			Close();
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			if(LegacyBoxActive)
			{
				return;
			}

			WindowState = WindowState.Minimized;
		}

		private async void LaunchButton_Click(object sender, RoutedEventArgs e)
		{
			if(LegacyBoxActive)
			{
				return;
			}

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
						if(new DialogWindow(App.TextStrings["msgbox_no_game_exe_title"], App.TextStrings["msgbox_no_game_exe_msg"], DialogWindow.DialogType.Question).ShowDialog() == true)
						{
							ResetVersionInfo();
							GameUpdateCheck();
						}
						return;
					}
					try
					{
						var processes = Process.GetProcessesByName("BH3");
						if(processes.Length > 0)
						{
							processes[0].EnableRaisingEvents = true;
							processes[0].Exited += new EventHandler((object s, EventArgs ea) => {OnGameExit();});
							if(PreloadDownload)
							{
								Dispatcher.Invoke(() =>
								{
									LaunchButton.Content = App.TextStrings["button_running"];
									LaunchButton.IsEnabled = false;
								});
							}
							else
							{
								Status = LauncherStatus.Running;
							}
							return;
						}
						var start_info = new ProcessStartInfo(GameExePath);
						start_info.WorkingDirectory = GameInstallPath;
						start_info.UseShellExecute = true;
						try
						{
							start_info.Arguments = LocalVersionInfo.launch_options.ToString();
						}catch{}
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
						if(PreloadDownload)
						{
							Dispatcher.Invoke(() =>
							{
								LaunchButton.Content = App.TextStrings["button_running"];
								LaunchButton.IsEnabled = false;
							});
						}
						else
						{
							Status = LauncherStatus.Running;
						}
						WindowState = WindowState.Minimized;
					}
					catch(Exception ex)
					{
						Status = LauncherStatus.Error;
						Log($"Failed to start the game:\n{ex}", true, 1);
						new DialogWindow(App.TextStrings["msgbox_start_error_title"], App.TextStrings["msgbox_process_start_error_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
					}
				}
				else
				{
					try
					{
						var possible_paths = new List<string>();
						possible_paths.Add(App.LauncherRootPath);
						possible_paths.Add(Environment.ExpandEnvironmentVariables("%ProgramW6432%"));
						string[] game_reg_names = {"Honkai Impact 3rd", "Honkai Impact 3", "崩坏3", "崩壞3rd", "붕괴3rd", "崩壊3rd"};
						foreach(string game_reg_name in game_reg_names)
						{
							try
							{
								var path = CheckForExistingGameDirectory(Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{game_reg_name}").GetValue("InstallPath").ToString());
								if(!string.IsNullOrEmpty(path))
								{
									possible_paths.Add(path);
								}
							}catch{}
						}
						foreach(string path in possible_paths)
						{
							if(!string.IsNullOrEmpty(CheckForExistingGameDirectory(path)))
							{
								var server = CheckForExistingGameClientServer(path);
								if(server >= 0)
								{
									if(new DialogWindow(App.TextStrings["msgbox_install_title"], string.Format(App.TextStrings["msgbox_install_existing_dir_msg"], path), DialogWindow.DialogType.Question).ShowDialog() == true)
									{
										Log($"Existing installation directory selected: {path}");
										GameInstallPath = path;
										if((int)Server != server)
										{
											ServerDropdown.SelectedIndex = server;
										}
										WriteVersionInfo(true, true);
										GameUpdateCheck();
										return;
									}
								}
							}
						}
					}catch{}

					while(true)
					{
						try
						{
							var dialog = new DialogWindow(App.TextStrings["msgbox_install_title"], App.TextStrings["msgbox_install_1_msg"], DialogWindow.DialogType.Install);
							dialog.InstallPathTextBox.Text = Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramW6432%"), GameFullName);
							if(dialog.ShowDialog() == false)
							{
								return;
							}
							GameInstallPath = dialog.InstallPathTextBox.Text;
							string path = CheckForExistingGameDirectory(GameInstallPath);
							if(!string.IsNullOrEmpty(path))
							{
								var server = CheckForExistingGameClientServer(path);
								if(server >= 0)
								{
									if(new DialogWindow(App.TextStrings["msgbox_install_title"], string.Format(App.TextStrings["msgbox_install_existing_dir_msg"], path), DialogWindow.DialogType.Question).ShowDialog() == true)
									{
										Log($"Existing installation directory selected: {path}");
										GameInstallPath = path;
										if((int)Server != server)
										{
											ServerDropdown.SelectedIndex = server;
										}
										WriteVersionInfo(true, true);
										GameUpdateCheck();
										return;
									}
									else
									{
										continue;
									}
								}
							}

							var game_install_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(GameInstallPath) && x.IsReady).FirstOrDefault();
							if(game_install_drive == null || game_install_drive.DriveType == DriveType.CDRom)
							{
								new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_wrong_drive_type_msg"]).ShowDialog();
								continue;
							}

							try
							{
								if(!Directory.Exists(GameInstallPath))
								{
									Directory.CreateDirectory(GameInstallPath);
								}
								if(new DirectoryInfo(GameInstallPath).Parent == null)
								{
									throw new Exception("Installation directory cannot be drive root");
								}
							}
							catch(Exception ex)
							{
								new DialogWindow(App.TextStrings["msgbox_install_dir_error_title"], ex.Message).ShowDialog();
								continue;
							}

							long free_space_recommended = (long)miHoYoVersionInfo.size + (long)miHoYoVersionInfo.game.latest.size;
							string install_message = $"{string.Format(App.TextStrings["msgbox_install_2_msg"], BpUtility.ToBytesCount((long)miHoYoVersionInfo.size))}" +
								$"\n{string.Format(App.TextStrings["msgbox_install_3_msg"], BpUtility.ToBytesCount(free_space_recommended), BpUtility.ToBytesCount(game_install_drive.TotalFreeSpace))}" +
								$"\n{string.Format(App.TextStrings["msgbox_install_4_msg"], GameInstallPath)}";
							if(new DialogWindow(App.TextStrings["msgbox_install_title"], install_message, DialogWindow.DialogType.Question).ShowDialog() == false)
							{
								try{Directory.Delete(GameInstallPath);}catch{}
								continue;
							}
							if(game_install_drive.TotalFreeSpace < free_space_recommended)
							{
								if(new DialogWindow(App.TextStrings["msgbox_install_title"], App.TextStrings["msgbox_install_little_space_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
								{
									continue;
								}
							}
							Directory.CreateDirectory(GameInstallPath);
							GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
							GameExePath = Path.Combine(GameInstallPath, GameExeName);
							Log($"Installation directory selected: {GameInstallPath}");
							await DownloadGameFile();
							return;
						}
						catch(Exception ex)
						{
							Status = LauncherStatus.Error;
							Log($"Failed to select game installation directory:\n{ex}", true, 1);
							new DialogWindow(App.TextStrings["msgbox_install_dir_error_title"], App.TextStrings["msgbox_install_dir_error_msg"]).ShowDialog();
							Status = LauncherStatus.Ready;
							return;
						}
					}
				}
			}
			else if(Status == LauncherStatus.UpdateAvailable)
			{
				if(!File.Exists(GameExePath))
				{
					if(new DialogWindow(App.TextStrings["msgbox_no_game_exe_title"], App.TextStrings["msgbox_no_game_exe_msg"], DialogWindow.DialogType.Question).ShowDialog() == true)
					{
						ResetVersionInfo();
						GameUpdateCheck();
					}
					return;
				}
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
				if(httpclient.DownloadState == DownloadState.Merging)
				{
					LaunchButton.IsEnabled = false;
					return;
				}
				if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_2_msg"]}\n{App.TextStrings["msgbox_abort_3_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == true)
				{
					token.Cancel();
					await httpclient.WaitUntilInstanceDisposed();
					httpclient.DeleteMultisessionFiles(httpprop.Out, httpprop.Thread);
					try{Directory.Delete(Path.GetDirectoryName(GameArchiveTempPath));}catch{}
					DownloadPaused = false;
					Log("Download cancelled");
					Status = LauncherStatus.Ready;
					GameUpdateCheck();
				}
			}
			else if(Status == LauncherStatus.Working)
			{
				LaunchButton.IsEnabled = false;
				ActionAbort = true;
			}
		}

		private void OptionsButton_Click(object sender, RoutedEventArgs e)
		{
			if(LegacyBoxActive)
			{
				return;
			}

			var button = sender as Button;
			OptionsContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
			OptionsContextMenu.PlacementTarget = LaunchButton;
			OptionsContextMenu.IsOpen = true;
			BpUtility.PlaySound(Properties.Resources.Click);
		}

		private async void DownloadPauseButton_Click(object sender, RoutedEventArgs e)
		{
			if(LegacyBoxActive)
			{
				return;
			}

			if(!DownloadPaused)
			{
				token.Cancel();
				await httpclient.WaitUntilInstanceDisposed();
				Status = LauncherStatus.DownloadPaused;
				DownloadProgressBarStackPanel.Visibility = Visibility.Visible;
				DownloadETAText.Visibility = Visibility.Hidden;
				DownloadSpeedText.Visibility = Visibility.Hidden;
				DownloadPauseButton.Visibility = Visibility.Collapsed;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
				DownloadResumeButton.Visibility = Visibility.Visible;
				Log("Download paused");
			}
			else
			{
				Status = LauncherStatus.Downloading;
				ProgressBar.IsIndeterminate = true;
				DownloadPauseButton.Visibility = Visibility.Collapsed;
				DownloadProgressBarStackPanel.Visibility = Visibility.Collapsed;
				try
				{
					ProgressText.Text = string.Empty;
					ProgressBar.Visibility = Visibility.Collapsed;
					DownloadPauseButton.Visibility = Visibility.Visible;
					DownloadProgressBarStackPanel.Visibility = Visibility.Visible;
					LaunchButton.IsEnabled = true;
					LaunchButton.Content = App.TextStrings["button_cancel"];
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
					Log("Download resumed");

					using(httpclient = new Http(true, 5, 1000, App.UserAgent))
					{
						token = new CancellationTokenSource();
						httpclient.DownloadProgress += DownloadStatusChanged;
						await httpclient.Download(httpprop.URL, httpprop.Out, httpprop.Thread, false, token.Token);
						await httpclient.Merge();
						httpclient.DownloadProgress -= DownloadStatusChanged;
						await DownloadGameFile();
					}
				}
				catch(TaskCanceledException){}
				catch(OperationCanceledException){}
				catch(Exception ex)
				{
					Status = LauncherStatus.Error;
					Log($"Failed to download the game:\n{ex}", true, 1);
					new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_error_msg"]).ShowDialog();
					Status = LauncherStatus.Ready;
					GameUpdateCheck();
				}
			}
		}

		private async void PreloadButton_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready && Status != LauncherStatus.Preloading && Status != LauncherStatus.Running)
			{
				return;
			}
			if(LegacyBoxActive)
			{
				return;
			}

			try
			{
				string url = miHoYoVersionInfo.pre_download_game.latest.path.ToString();
				string title = BpUtility.GetFileNameFromUrl(url);
				long size;
				string md5 = miHoYoVersionInfo.pre_download_game.latest.md5.ToString();
				string path = Path.Combine(GameInstallPath, title);
				string tmp_path = $"{path}_tmp";

				var web_request = BpUtility.CreateWebRequest(url, "HEAD");
				using(var web_response = (HttpWebResponse) web_request.GetResponse())
				{
					size = web_response.ContentLength;
				}
				if(Directory.GetFiles(GameInstallPath, $"{title}_tmp.*").Length == 0)
				{
					var game_install_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(GameInstallPath) && x.IsReady).FirstOrDefault();
					string pre_install_message = $"{App.TextStrings["msgbox_pre_install_msg"]}" +
						$"\n{string.Format(App.TextStrings["msgbox_install_2_msg"], BpUtility.ToBytesCount(size))}" +
						$"\n{string.Format(App.TextStrings["msgbox_install_3_msg"], BpUtility.ToBytesCount((long)miHoYoVersionInfo.game.latest.size), BpUtility.ToBytesCount(game_install_drive.TotalFreeSpace))}";
					if(new DialogWindow(App.TextStrings["label_pre_install"], pre_install_message, DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						return;
					}
					if(game_install_drive.TotalFreeSpace < (long)miHoYoVersionInfo.pre_download_game.latest.size)
					{
						if(new DialogWindow(App.TextStrings["msgbox_install_title"], App.TextStrings["msgbox_install_little_space_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
						{
							return;
						}
					}
					Log($"Starting to pre-download game: {title} ({url})");
				}
				else
				{
					Log("Pre-download resumed");
				}
				PreloadDownload = true;
				Status = LauncherStatus.Preloading;
				if(File.Exists(path))
				{
					File.Move(path, tmp_path);
				}
				if(!File.Exists(tmp_path))
				{
					try
					{
						using(httpclient = new Http(true, 5, 1000, App.UserAgent))
						{
							token = new CancellationTokenSource();
							httpprop = new HttpProp(url, tmp_path);
							httpclient.DownloadProgress += PreloadDownloadStatusChanged;
							PreloadPauseButton.IsEnabled = true;
							await httpclient.Download(httpprop.URL, httpprop.Out, httpprop.Thread, false, token.Token);
							await httpclient.Merge();
							httpclient.DownloadProgress -= PreloadDownloadStatusChanged;
							Log("Downloaded pre-download archive");
						}
					}
					catch(OperationCanceledException)
					{
						httpclient.DownloadProgress -= PreloadDownloadStatusChanged;
						return;
					}
				}
				Status = LauncherStatus.PreloadVerifying;
				try
				{
					await Task.Run(() =>
					{
						Log("Validating pre-download archive...");
						string actual_md5 = BpUtility.CalculateMD5(tmp_path);
						if(actual_md5 == md5.ToUpper())
						{
							Log("success!", false);
							if(!File.Exists(path))
							{
								File.Move(tmp_path, path);
							}
							else if(File.Exists(path) && new FileInfo(path).Length != size)
							{
								DeleteFile(path, true);
								File.Move(tmp_path, path);
							}
							Log("Successfully pre-downloaded the game");
							PreloadDownload = false;
							GameUpdateCheck();
						}
						else
						{
							Status = LauncherStatus.Error;
							Log($"Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
							DeleteFile(tmp_path);
							Dispatcher.Invoke(() =>
							{
								PreloadButton.Visibility = Visibility.Visible;
								PreloadPauseButton.Visibility = Visibility.Collapsed;
								PreloadCircle.Visibility = Visibility.Collapsed;
								PreloadCircleProgressBar.Visibility = Visibility.Collapsed;
								PreloadCircleProgressBar.Value = 0;
								PreloadBottomText.Text = App.TextStrings["label_retry"];
							});
							Status = LauncherStatus.Ready;
						}
					});
				}
				catch(Exception ex)
				{
					Status = LauncherStatus.Error;
					Log($"Failed to pre-download the game:\n{ex}", true, 1);
					new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_error_msg"]).ShowDialog();
					Status = LauncherStatus.Ready;
					GameUpdateCheck();
				}
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to download game pre-download archive:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				GameUpdateCheck();
			}
			TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
			WindowState = WindowState.Normal;
		}

		private async void PreloadPauseButton_Click(object sender, RoutedEventArgs e)
		{
			if(LegacyBoxActive)
			{
				return;
			}

			if(PreloadDownload)
			{
				PreloadPauseButton.IsEnabled = false;
				token.Cancel();
				await httpclient.WaitUntilInstanceDisposed();
				Log("Pre-download paused");
				PreloadDownload = false;
				PreloadPauseButton.IsEnabled = true;
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
					Log($"Failed to resume pre-download:\n{ex}", true, 1);
				}
			}
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
			if(BackgroundImageDownloading || LegacyBoxActive || PreloadDownload)
			{
				ServerDropdown.SelectedIndex = (int)Server;
				return;
			}

			if(DownloadPaused)
			{
				if(new DialogWindow(App.TextStrings["msgbox_notice_title"], App.TextStrings["msgbox_game_download_paused_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					ServerDropdown.SelectedIndex = (int)Server;
					return;
				}
				DownloadPaused = false;
				DeleteFile(GameArchiveTempPath);
				if(LocalVersionInfo != null && LocalVersionInfo.game_info.installed == false)
				{
					ResetVersionInfo();
				}
			}
			switch(index)
			{
				case 0:
					Server = HI3Server.GLB;
					break;
				case 1:
					Server = HI3Server.SEA;
					break;
				case 2:
					Server = HI3Server.CN;
					break;
				case 3:
					Server = HI3Server.TW;
					break;
				case 4:
					Server = HI3Server.KR;
					break;
				case 5:
					Server = HI3Server.JP;
					break;
			}
			try
			{
				BpUtility.WriteToRegistry("LastSelectedServer", index, RegistryValueKind.DWord);
			}
			catch(Exception ex)
			{
				Log($"Failed to write value with key LastSelectedServer to registry:\n{ex}", true, 1);
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
			if(LegacyBoxActive || PreloadDownload)
			{
				MirrorDropdown.SelectedIndex = (int)Mirror;
				return;
			}
			if(!(bool)OnlineVersionInfo.game_info.mirror.bpnetwork.available && index == 1)
			{
				MirrorDropdown.SelectedIndex = 0;
				new DialogWindow(App.TextStrings["label_mirror"], App.TextStrings["msgbox_feature_not_available_msg"]).ShowDialog();
				return;
			}

			if(DownloadPaused)
			{
				if(new DialogWindow(App.TextStrings["msgbox_notice_title"], App.TextStrings["msgbox_game_download_paused_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					MirrorDropdown.SelectedIndex = (int)Mirror;
					return;
				}
				DownloadPaused = false;
				DeleteFile(GameArchiveTempPath);
				if(LocalVersionInfo.game_info.installed == false)
				{
					ResetVersionInfo();
				}
			}
			else if(Mirror == HI3Mirror.miHoYo && index != 0)
			{
				string msg = App.TextStrings["msgbox_mirror_info_msg"];
				if(index == 1)
				{
					int newline_1 = msg.IndexOf('\n');
					int newline_2 = msg.IndexOf('\n', newline_1 + 1);
					msg = msg.Remove(newline_1 + 1, newline_2 - newline_1);
				}
				if(new DialogWindow(App.TextStrings["msgbox_notice_title"], msg, DialogWindow.DialogType.Question).ShowDialog() == false)
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
					Mirror = HI3Mirror.BpNetwork;
					break;
			}
			try
			{
				BpUtility.WriteToRegistry("LastSelectedMirror", index, RegistryValueKind.DWord);
			}
			catch(Exception ex)
			{
				Log($"Failed to write value with key LastSelectedMirror to registry:\n{ex}", true, 1);
			}
			GameUpdateCheck();
			Log($"Selected mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");
		}

		private void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			if(Status == LauncherStatus.Downloading || Status == LauncherStatus.DownloadPaused || Status == LauncherStatus.Preloading)
			{
				if(httpclient == null || httpclient.DownloadState == DownloadState.Idle)
				{
					if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_1_msg"]}\n{App.TextStrings["msgbox_abort_3_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						e.Cancel = true;
					}
				}
				else
				{
					if(httpclient != null && httpclient.DownloadState == DownloadState.Merging)
					{
						e.Cancel = true;
						return;
					}
					if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_1_msg"]}\n{App.TextStrings["msgbox_abort_4_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == true)
					{
						if(httpclient != null && httpclient.DownloadState == DownloadState.Downloading || httpclient.DownloadState == DownloadState.CancelledDownloading)
						{
							try
							{
								token.Cancel();
							}catch(OperationCanceledException){}
						}
						else
						{
							e.Cancel = true;
						}
						if(Status != LauncherStatus.Preloading)
						{
							WriteVersionInfo();
						}
					}
					else
					{
						e.Cancel = true;
					}
				}
			}
			else if(Status == LauncherStatus.Verifying || Status == LauncherStatus.Unpacking || Status == LauncherStatus.Uninstalling || Status == LauncherStatus.Working || Status == LauncherStatus.PreloadVerifying)
			{
				e.Cancel = true;
			}
		}
	}
}