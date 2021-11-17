﻿using IniParser;
using Microsoft.Win32;
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
		GLB, SEA, CN, TW, KR
	}
	enum HI3Mirror
	{
		miHoYo, Hi3Mirror, MediaFire, GoogleDrive
	}

	public partial class MainWindow : Window
	{
		public static readonly string miHoYoPath = Path.Combine(App.LocalLowPath, "miHoYo");
		public static string GameInstallPath, GameCachePath, GameRegistryPath, GameArchivePath, GameArchiveName, GameExeName, GameExePath, CacheArchivePath;
		public static string RegistryVersionInfo;
		public static string GameWebProfileURL, GameFullName;
		public static bool DownloadPaused, PatchDownload, PreloadDownload, CacheDownload, BackgroundImageDownloading, LegacyBoxActive;
		public static int PatchDownloadInt;
		public static RoutedCommand DownloadCacheCommand = new RoutedCommand();
		public static RoutedCommand FixSubtitlesCommand = new RoutedCommand();
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
		public dynamic LocalVersionInfo, OnlineVersionInfo, OnlineRepairInfo, miHoYoVersionInfo, GameGraphicSettings, GameScreenSettings, GameCacheMetadata, GameCacheMetadataNumeric;
		LauncherStatus _status;
		HI3Server _gameserver;
		HI3Mirror _downloadmirror;
		DownloadPauseable download;
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
					if(Server != HI3Server.GLB && Server != HI3Server.SEA)
					{
						MirrorDropdown.IsEnabled = false;
					}
					else
					{
						MirrorDropdown.IsEnabled = val;
					}
					ToggleContextMenuItems(val);
					DownloadProgressBarStackPanel.Visibility = Visibility.Collapsed;
					DownloadETAText.Visibility = Visibility.Visible;
					DownloadSpeedText.Visibility = Visibility.Visible;
					DownloadPauseButton.Visibility = Visibility.Visible;
					DownloadResumeButton.Visibility = Visibility.Collapsed;
				}
				void ToggleProgressBar(bool val)
				{
					ProgressBar.Visibility = val ? Visibility.Visible : Visibility.Hidden;
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
					case LauncherStatus.CleaningUp:
						ProgressText.Text = App.TextStrings["progresstext_cleaning_up"];
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
						GameWebProfileURL = "https://global.user.honkaiimpact3.com";
						break;
					case HI3Server.SEA:
						RegistryVersionInfo = "VersionInfoSEA";
						GameFullName = "Honkai Impact 3";
						GameWebProfileURL = "https://asia.user.honkaiimpact3.com";
						break;
					case HI3Server.CN:
						RegistryVersionInfo = "VersionInfoCN";
						GameFullName = "崩坏3";
						GameWebProfileURL = "https://user.mihoyo.com";
						break;
					case HI3Server.TW:
						RegistryVersionInfo = "VersionInfoTW";
						GameFullName = "崩壞3";
						GameWebProfileURL = "https://tw-user.bh3.com";
						break;
					case HI3Server.KR:
						RegistryVersionInfo = "VersionInfoKR";
						GameFullName = "붕괴3rd";
						GameWebProfileURL = "https://kr.user.honkaiimpact3.com";
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
				App.DisableAutoUpdate = true;
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
				App.DisableTranslations = true;
				App.LauncherLanguage = "en";
				App.UserAgent += " [NOTRANSLATIONS]";
				Log("Translations disabled, only English will be available");
			}
			if(args.Contains("ADVANCED"))
			{
				App.AdvancedFeatures = true;
				App.UserAgent += " [ADVANCED]";
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
			PreloadTopText.Text = App.TextStrings["label_pre_install"];
			PreloadStatusTopLeftText.Text = App.TextStrings["label_downloaded_2"];
			PreloadStatusMiddleLeftText.Text = App.TextStrings["label_eta"];
			PreloadStatusBottomLeftText.Text = App.TextStrings["label_speed"];

			Grid.MouseLeftButtonDown += delegate{DragMove();};
			PreloadGrid.Visibility = Visibility.Collapsed;
			DownloadProgressBarStackPanel.Visibility = Visibility.Collapsed;
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
			var CM_Download_Cache = new MenuItem{Header = App.TextStrings["contextmenu_download_cache"], InputGestureText = "Ctrl+D"};
			CM_Download_Cache.Click += async (sender, e) => await CM_DownloadCache_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Download_Cache);
			var CM_Fix_Subtitles = new MenuItem{Header = App.TextStrings["contextmenu_fix_subtitles"], InputGestureText = "Ctrl+S"};
			CM_Fix_Subtitles.Click += async (sender, e) => await CM_FixSubtitles_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Fix_Subtitles);
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
			var CM_Sounds = new MenuItem{Header = App.TextStrings["contextmenu_sounds"], InputGestureText = "Ctrl+Shift+S", IsChecked = true};
			CM_Sounds.Click += (sender, e) => CM_Sounds_Click(sender, e);
			OptionsContextMenu.Items.Add(CM_Sounds);
			var CM_Language = new MenuItem{Header = App.TextStrings["contextmenu_language"]};
			var CM_Language_System = new MenuItem{Header = App.TextStrings["contextmenu_language_system"]};
			CM_Language_System.Click += (sender, e) => CM_Language_Click(sender, e);
			CM_Language.Items.Add(CM_Language_System);
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
			CM_Language_Contribute.Click += (sender, e) => BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher#contributing-translations", null, App.LauncherRootPath, true);
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
						case "fr":
							CM_Language_French.IsChecked = true;
							break;
						case "de":
							CM_Language_German.IsChecked = true;
							break;
						case "id":
							CM_Language_Indonesian.IsChecked = true;
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
							Mirror = HI3Mirror.MediaFire;
						}
						else if((int)last_selected_mirror_reg == 2)
						{
							Mirror = HI3Mirror.GoogleDrive;
						}
					}
				}
				if(Server != HI3Server.GLB && Server != HI3Server.SEA)
				{
					Mirror = HI3Mirror.miHoYo;
				}
				MirrorDropdown.SelectedIndex = (int)Mirror;

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
				FixSubtitlesCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
				RepairGameCommand.InputGestures.Add(new KeyGesture(Key.R, ModifierKeys.Control));
				MoveGameCommand.InputGestures.Add(new KeyGesture(Key.M, ModifierKeys.Control));
				UninstallGameCommand.InputGestures.Add(new KeyGesture(Key.U, ModifierKeys.Control));
				WebProfileCommand.InputGestures.Add(new KeyGesture(Key.P, ModifierKeys.Control));
				FeedbackCommand.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));
				ChangelogCommand.InputGestures.Add(new KeyGesture(Key.C, ModifierKeys.Control));
				CustomBackgroundCommand.InputGestures.Add(new KeyGesture(Key.B, ModifierKeys.Control));
				ToggleLogCommand.InputGestures.Add(new KeyGesture(Key.L, ModifierKeys.Control));
				ToggleSoundsCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift));
				AboutCommand.InputGestures.Add(new KeyGesture(Key.A, ModifierKeys.Control));

				App.NeedsUpdate = LauncherUpdateCheck();
				if(!App.DisableTranslations || !App.DisableTranslations && App.DisableAutoUpdate && !App.NeedsUpdate)
				{
					DownloadLauncherTranslations();
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

		private void FetchOnlineVersionInfo()
		{
			#if DEBUG
			var version_info_url = "https://bpnet.host/bh3?launcher_status=debug";
			#else
			var version_info_url = "https://bpnet.host/bh3?launcher_status=prod";
			#endif
			string version_info = null;
			void Get(int timeout)
			{
				var web_client = new BpWebClient{Timeout = timeout};
				version_info = web_client.DownloadString(version_info_url);
			}
			int attempts = 6;
			int timeout_add = 2500;
			for(int i = 0; i < attempts; i++)
			{
				if(i == attempts - 1)
				{
					Get(timeout_add);
				}
				else
				{
					try
					{
						Get(timeout_add);
						break;
					}
					catch
					{
						Log($"WARNING: Bp Network connection timeout, attempt №{i + 2}...", true, 2);
						timeout_add += 2500;
						#if !DEBUG
						if(i == 3)
						{
							// fallback server with basic information needed to start the launcher
							version_info_url = "https://serioussam.ucoz.ru/bbh3l_prod.json";
						}
						#endif
					}
				}
			}
			OnlineVersionInfo = JsonConvert.DeserializeObject<dynamic>(version_info);
			if(OnlineVersionInfo.status == "success")
			{
				OnlineVersionInfo = OnlineVersionInfo.launcher_status;
				App.LauncherExeName = OnlineVersionInfo.launcher_info.name;
				App.LauncherPath = Path.Combine(App.LauncherRootPath, App.LauncherExeName);
				App.LauncherArchivePath = Path.Combine(App.LauncherRootPath, OnlineVersionInfo.launcher_info.url.ToString().Substring(OnlineVersionInfo.launcher_info.url.ToString().LastIndexOf('/') + 1));
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

			string changelog = null;
			Dispatcher.Invoke(() => {ChangelogBoxTextBox.Text = App.TextStrings["changelogbox_2_msg"];});
			await Task.Run(() =>
			{
				void Get(int timeout)
				{
					var web_client = new BpWebClient{Timeout = timeout};
					if(App.LauncherLanguage == "ru")
					{
						changelog = web_client.DownloadString(OnlineVersionInfo.launcher_info.changelog_url.ru.ToString());
					}
					else
					{
						changelog = web_client.DownloadString(OnlineVersionInfo.launcher_info.changelog_url.en.ToString());
					}
				}
				try
				{
					int attempts = 6;
					int timeout_add = 2500;
					for(int i = 0; i < attempts; i++)
					{
						if(i == attempts - 1)
						{
							Get(timeout_add);
						}
						else
						{
							try
							{
								Get(timeout_add);
								break;
							}
							catch
							{
								Log($"WARNING: Bp Network connection timeout, attempt №{i + 2}...", true, 2);
								timeout_add += 2500;
							}
						}
					}
				}
				catch
				{
					Log($"WARNING: Bp Network connection timeout, giving up...", true, 2);
					changelog = App.TextStrings["changelogbox_3_msg"];
				}
				Dispatcher.Invoke(() => {ChangelogBoxTextBox.Text = changelog;});
			});
		}

		private void FetchmiHoYoVersionInfo()
		{
			string url = null;
			switch(Server)
			{
				case HI3Server.GLB:
					url = OnlineVersionInfo.game_info.mirror.mihoyo.resource_info.global.ToString();
					break;
				case HI3Server.SEA:
					url = OnlineVersionInfo.game_info.mirror.mihoyo.resource_info.os.ToString();
					break;
				case HI3Server.CN:
					url = OnlineVersionInfo.game_info.mirror.mihoyo.resource_info.cn.ToString();
					break;
				case HI3Server.TW:
					url = OnlineVersionInfo.game_info.mirror.mihoyo.resource_info.tw.ToString();
					break;
				case HI3Server.KR:
					url = OnlineVersionInfo.game_info.mirror.mihoyo.resource_info.kr.ToString();
					break;
			}
			void Get(int timeout)
			{
				var web_request = BpUtility.CreateWebRequest(url, "GET", timeout);
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
				GameExeName = miHoYoVersionInfo.game.latest.entry.ToString();
				GameArchiveName = Path.GetFileName(HttpUtility.UrlDecode(miHoYoVersionInfo.game.latest.path.ToString()));
				web_request = BpUtility.CreateWebRequest(miHoYoVersionInfo.game.latest.path.ToString(), "HEAD", timeout);
				using(var web_response = (HttpWebResponse)web_request.GetResponse())
				{
					miHoYoVersionInfo.size = web_response.ContentLength;
					miHoYoVersionInfo.last_modified = web_response.LastModified.ToUniversalTime().ToString();
				}
			}
			int attempts = 6;
			int timeout_add = 2500;
			for(int i = 0; i < attempts; i++)
			{
				if(i == attempts - 1)
				{
					Get(timeout_add);
				}
				else
				{
					try
					{
						Get(timeout_add);
						break;
					}
					catch
					{
						Log($"WARNING: miHoYo connection timeout, attempt №{i + 2}...", true, 2);
						timeout_add += 2500;
					}
				}
			}
			Dispatcher.Invoke(() =>
			{
				GameVersionText.Text = $"{App.TextStrings["version"]}: v{miHoYoVersionInfo.game.latest.version.ToString()}";
			});
		}

		private DateTime FetchmiHoYoResourceVersionDateModified()
		{
			var url = new string[3];
			var time = new DateTime[3];
			switch(Server)
			{
				case HI3Server.GLB:
					url[0] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.global[0].ToString();
					url[1] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.global[1].ToString();
					url[2] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.global[2].ToString();
					break;
				case HI3Server.SEA:
					url[0] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.os[0].ToString();
					url[1] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.os[1].ToString();
					url[2] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.os[2].ToString();
					break;
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
				Array.Sort(time);
				return time[time.Length - 1];
			}
			catch
			{
				return new DateTime();
			}
		}

		private dynamic FetchMediaFireFileMetadata(string id, int type = 0)
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
					metadata.md5Checksum = null;
					switch(type)
					{
						case 0:
							switch(Server)
							{
								case HI3Server.GLB:
									metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_archive.global.md5;
									break;
								case HI3Server.SEA:
									metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_archive.os.md5;
									break;
							}
							break;
						case 1:
							switch(Server)
							{
								case HI3Server.GLB:
									metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache.global.md5.ToString();
									break;
								case HI3Server.SEA:
									metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache.os.md5.ToString();
									break;
							}
							break;
						case 2:
							switch(Server)
							{
								case HI3Server.GLB:
									metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.md5.ToString();
									break;
								case HI3Server.SEA:
									metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.md5.ToString();
									break;
							}
							break;
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
				try
				{
					int game_needs_update;

					FetchOnlineVersionInfo();
					if(Mirror == HI3Mirror.MediaFire)
					{
						dynamic mediafire_metadata = null;
						switch(Server)
						{
							case HI3Server.GLB:
								mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString());
								break;
							case HI3Server.SEA:
								mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString());
								break;
						}
						if(mediafire_metadata == null)
						{
							Log("WARNING: Failed to use the current mirror, switching back to miHoYo", true, 2);
							Status = LauncherStatus.Ready;
							Dispatcher.Invoke(() => {MirrorDropdown.SelectedIndex = 0;});
							return;
						}
					}
					else if(Mirror == HI3Mirror.GoogleDrive)
					{
						dynamic gd_metadata = null;
						switch(Server)
						{
							case HI3Server.GLB:
								gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
								break;
							case HI3Server.SEA:
								gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
								break;
						}
						if(gd_metadata == null)
						{
							Log("WARNING: Failed to use the current mirror, switching back to miHoYo", true, 2);
							Status = LauncherStatus.Ready;
							Dispatcher.Invoke(() => {MirrorDropdown.SelectedIndex = 0;});
							return;
						}
					}
					if(App.LauncherRegKey.GetValue(RegistryVersionInfo) != null)
					{
						LocalVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])App.LauncherRegKey.GetValue(RegistryVersionInfo)));
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
						GameExePath = Path.Combine(GameInstallPath, GameExeName);

						Log($"Game version: {local_game_version}");
						Log($"Game directory: {GameInstallPath}");
						if(new DirectoryInfo(GameInstallPath).Parent == null)
						{
							Log("WARNING: Game directory is unsafe, resetting version info...", true, 2);
							ResetVersionInfo();
							GameUpdateCheck();
							return;
						}
						else if(game_needs_update != 0)
						{
							PatchDownload = false;
							if(game_needs_update == 2 && Mirror == HI3Mirror.miHoYo)
							{
								var url = miHoYoVersionInfo.game.diffs[PatchDownloadInt].path.ToString();
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
						else
						{
							var process = Process.GetProcessesByName("BH3");
							if(process.Length > 0)
							{
								process[0].EnableRaisingEvents = true;
								process[0].Exited += new EventHandler((object s, EventArgs ea) => {OnGameExit();});
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
							}
							else
							{
								Status = LauncherStatus.Ready;
								Dispatcher.Invoke(() => {LaunchButton.Content = App.TextStrings["button_launch"];});
							}
						}
						if(Status == LauncherStatus.UpdateAvailable)
						{
							if(File.Exists($"{GameArchivePath}_tmp"))
							{
								DownloadPaused = true;
								Dispatcher.Invoke(() =>
								{
									LaunchButton.Content = App.TextStrings["button_resume"];
								});
							}
							else
							{
								Dispatcher.Invoke(() =>
								{
									LaunchButton.Content = App.TextStrings["button_update"];
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
							ToggleContextMenuItems(false);
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
					Status = LauncherStatus.Ready;
					GameUpdateCheck();
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
				ProgressText.Text = string.Empty;
				ProgressBar.Visibility = Visibility.Hidden;
				DownloadProgressBarStackPanel.Visibility = Visibility.Visible;
				DownloadPauseButton.Visibility = Visibility.Collapsed;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
			});
			try
			{
				tracker.NewFile();
				var eta_calc = new ETACalculator();
				var download = new DownloadPauseable(OnlineVersionInfo.launcher_info.url.ToString(), App.LauncherArchivePath);
				download.Start();
				while(!download.Done)
				{
					tracker.SetProgress(download.BytesWritten, download.ContentLength);
					eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
					Dispatcher.Invoke(() =>
					{
						var progress = tracker.GetProgress();
						DownloadProgressBar.Value = progress;
						TaskbarItemInfo.ProgressValue = progress;
						DownloadProgressText.Text = $"{App.TextStrings["progresstext_updating_launcher"].TrimEnd('.')} {Math.Round(progress * 100)}% ({BpUtility.ToBytesCount(download.BytesWritten)}/{BpUtility.ToBytesCount(download.ContentLength)})";
						DownloadETAText.Text = string.Format(App.TextStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"));
						DownloadSpeedText.Text = $"{App.TextStrings["label_speed"]} {tracker.GetBytesPerSecondString()}";
					});
					Thread.Sleep(500);
				}
				Log("success!", false);
				Dispatcher.Invoke(() =>
				{
					ProgressText.Text = App.TextStrings["progresstext_updating_launcher"];
					ProgressBar.Visibility = Visibility.Visible;
					ProgressBar.IsIndeterminate = true;
					DownloadProgressBarStackPanel.Visibility = Visibility.Collapsed;
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
				});
				while(BpUtility.IsFileLocked(new FileInfo(App.LauncherArchivePath)))
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
				string translations_md5 = OnlineVersionInfo.launcher_info.translations.md5.ToString().ToUpper();
				string translations_version = OnlineVersionInfo.launcher_info.translations.version;
				bool Validate()
				{
					if(File.Exists(App.LauncherTranslationsFile))
					{
						var translations_version_reg = App.LauncherRegKey.GetValue("TranslationsVersion");
						if(translations_version_reg != null)
						{
							if(App.LauncherRegKey.GetValueKind("TranslationsVersion") == RegistryValueKind.String)
							{
								if(translations_version == App.LauncherRegKey.GetValue("TranslationsVersion").ToString())
								{
									#if DEBUG
									return true;
									#else
									string actual_md5 = BpUtility.CalculateMD5(App.LauncherTranslationsFile);
									if(actual_md5 != translations_md5)
									{
										Log($"WARNING: Translations validation failed. Expected MD5: {translations_md5}, got MD5: {actual_md5}", true, 2);
									}
									else
									{
										return true;
									}
									#endif
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
								throw new CryptographicException("Failed to verify translations");
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
				Array.Resize(ref App.CommandLineArgs, App.CommandLineArgs.Length + 1);
				App.CommandLineArgs[App.CommandLineArgs.Length - 1] = "NOTRANSLATIONS";
				BpUtility.RestartApp();
			}
		}

		private void DownloadBackgroundImage()
		{
			BackgroundImageDownloading = true;
			try
			{
				string url = null;
				switch(Server)
				{
					case HI3Server.GLB:
						url = OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.global.ToString();
						break;
					case HI3Server.SEA:
						url = OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.os.ToString();
						break;
					case HI3Server.CN:
						url = OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.cn.ToString();
						break;
					case HI3Server.TW:
						url = OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.tw.ToString();
						break;
					case HI3Server.KR:
						url = OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.kr.ToString();
						break;
				}
				Directory.CreateDirectory(App.LauncherBackgroundsPath);
				string background_image_url;
				string background_image_md5;
				var web_request = BpUtility.CreateWebRequest(url);
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
								BackgroundImageDownloading = false;
								return;
							}
						}
						else
						{
							Log($"WARNING: Failed to fetch background image info: {json.message.ToString()}", true, 2);
							BackgroundImageDownloading = false;
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
							Dispatcher.Invoke(() => {Resources["BackgroundImage"] = new BitmapImage(new Uri(background_image_path));});
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
								throw new CryptographicException("Failed to verify background image");
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
			BackgroundImageDownloading = false;
		}

		private async Task DownloadGameFile()
		{
			try
			{
				string title;
				long size = 0;
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
				else if(Mirror == HI3Mirror.Hi3Mirror)
				{
					title = GameArchiveName;
					time = -1;
					url = OnlineVersionInfo.game_info.mirror.hi3mirror.game_archive.ToString() + title;
					md5 = miHoYoVersionInfo.game.latest.md5.ToString();
				}
				else if(Mirror == HI3Mirror.MediaFire)
				{
					dynamic mediafire_metadata = null;
					switch(Server)
					{
						case HI3Server.GLB:
							mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString());
							break;
						case HI3Server.SEA:
							mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString());
							break;
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
					dynamic gd_metadata = null;
					switch(Server)
					{
						case HI3Server.GLB:
							gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
							break;
						case HI3Server.SEA:
							gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
							break;
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
				md5 = md5.ToUpper();
				if(!File.Exists(GameArchivePath))
				{
					GameArchivePath = Path.Combine(GameInstallPath, $"{title}_tmp");
				}
				else if(new FileInfo(GameArchivePath).Length != size)
				{
					string tmp_path = Path.Combine(GameInstallPath, $"{title}_tmp");
					File.Move(GameArchivePath, tmp_path);
					GameArchivePath = tmp_path;
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
						ProgressText.Text = string.Empty;
						ProgressBar.Visibility = Visibility.Hidden;
						DownloadProgressBarStackPanel.Visibility = Visibility.Visible;
						LaunchButton.IsEnabled = true;
						LaunchButton.Content = App.TextStrings["button_cancel"];
					});
					while(download != null && !download.Done)
					{
						if(DownloadPaused)
						{
							continue;
						}
						size = download.ContentLength;
						tracker.SetProgress(download.BytesWritten, download.ContentLength);
						eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
						Dispatcher.Invoke(() =>
						{
							var progress = tracker.GetProgress();
							DownloadProgressBar.Value = progress;
							TaskbarItemInfo.ProgressValue = progress;
							DownloadProgressText.Text = $"{string.Format(App.TextStrings["label_downloaded_1"], Math.Round(progress * 100))} ({BpUtility.ToBytesCount(download.BytesWritten)}/{BpUtility.ToBytesCount(download.ContentLength)})";
							DownloadETAText.Text = string.Format(App.TextStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"));
							DownloadSpeedText.Text = $"{App.TextStrings["label_speed"]} {tracker.GetBytesPerSecondString()}";
						});
						Thread.Sleep(500);
					}
					if(download == null)
					{
						abort = true;
					}
					if(abort)
					{
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
						DownloadProgressBarStackPanel.Visibility = Visibility.Collapsed;
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
						if(actual_md5 == md5)
						{
							string new_path = GameArchivePath.Substring(0, GameArchivePath.Length - 4);
							if(!File.Exists(new_path))
							{
								File.Move(GameArchivePath, new_path);
							}
							else if(File.Exists(new_path) && size != 0 && new FileInfo(new_path).Length != size)
							{
								DeleteFile(new_path);
								File.Move(GameArchivePath, new_path);
							}
							GameArchivePath = new_path;
							Log("success!", false);
						}
						else
						{
							Status = LauncherStatus.Error;
							Log($"ERROR: Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
							DeleteFile(GameArchivePath);
							abort = true;
							Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_verify_error_title"], App.TextStrings["msgbox_verify_error_1_msg"]).ShowDialog();});
							Status = LauncherStatus.Ready;
							GameUpdateCheck();
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
										DownloadProgressText.Text = string.Format(App.TextStrings["progresstext_unpacking_2"], unpacked_files + 1, file_count);
										var progress = (unpacked_files + 1f) / file_count;
										DownloadProgressBar.Value = progress;
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
				var version_info = LocalVersionInfo;
				if(version_info == null)
				{
					version_info = new ExpandoObject();
					version_info.game_info = new ExpandoObject();
				}
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

				if(new DirectoryInfo(GameInstallPath).Parent == null)
				{
					throw new Exception("Installation directory cannot be drive root");
				}
				if(check_for_local_version)
				{
					var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
					try
					{
						if(File.Exists(config_ini_file))
						{
							var data = ini_parser.ReadFile(config_ini_file);
							if(data["General"]["game_version"] != null)
							{
								version_info.game_info.version = data["General"]["game_version"];
							}
							else
							{
								throw new NullReferenceException();
							}
						}
					}
					catch
					{
						if(new DialogWindow(App.TextStrings["msgbox_install_title"], App.TextStrings["msgbox_install_existing_no_local_version_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
						{
							version_info.game_info.version = new GameVersion().ToString();
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
						try
						{
							var data = ini_parser.ReadFile(config_ini_file);
							data["General"]["game_version"] = version_info.game_info.version;
							ini_parser.WriteFile(config_ini_file, data);
						}
						catch(Exception ex)
						{
							Log($"WARNING: Failed to write version info to config.ini: {ex.Message}", true, 2);
						}
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

		private void ResetVersionInfo(bool DeleteGame = false)
		{
			if(DeleteGame)
			{
				if(Directory.Exists(GameInstallPath))
				{
					Directory.Delete(GameInstallPath, true);
				}
			}
			try{App.LauncherRegKey.DeleteValue(RegistryVersionInfo);}catch{}
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
				md5 = md5.ToUpper();
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
				CacheDownload = true;
				Status = LauncherStatus.Downloading;
				await Task.Run(() =>
				{
					tracker.NewFile();
					var eta_calc = new ETACalculator();
					download = new DownloadPauseable(url, CacheArchivePath);
					download.Start();
					Dispatcher.Invoke(() =>
					{
						ProgressText.Text = string.Empty;
						ProgressBar.Visibility = Visibility.Hidden;
						DownloadProgressBarStackPanel.Visibility = Visibility.Visible;
						DownloadPauseButton.Visibility = Visibility.Collapsed;
						LaunchButton.IsEnabled = true;
						LaunchButton.Content = App.TextStrings["button_cancel"];
					});
					while(download != null && !download.Done)
					{
						tracker.SetProgress(download.BytesWritten, download.ContentLength);
						eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
						Dispatcher.Invoke(() =>
						{
							var progress = tracker.GetProgress();
							DownloadProgressBar.Value = progress;
							TaskbarItemInfo.ProgressValue = progress;
							DownloadProgressText.Text = $"{string.Format(App.TextStrings["label_downloaded_1"], Math.Round(progress * 100))} ({BpUtility.ToBytesCount(download.BytesWritten)}/{BpUtility.ToBytesCount(download.ContentLength)})";
							DownloadETAText.Text = string.Format(App.TextStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"));
							DownloadSpeedText.Text = $"{App.TextStrings["label_speed"]} {tracker.GetBytesPerSecondString()}";
						});
						Thread.Sleep(500);
					}
					if(download == null)
					{
						abort = true;
					}
					if(abort)
					{
						return;
					}
					Log("Successfully downloaded game cache");
					while(BpUtility.IsFileLocked(new FileInfo(CacheArchivePath)))
					{
						Thread.Sleep(10);
					}
					Dispatcher.Invoke(() =>
					{
						ProgressText.Text = string.Empty;
						DownloadProgressBarStackPanel.Visibility = Visibility.Collapsed;
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
						if(actual_md5 != md5)
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
										DownloadProgressText.Text = string.Format(App.TextStrings["progresstext_unpacking_2"], unpacked_files + 1, file_count);
										var progress = (unpacked_files + 1f) / file_count;
										DownloadProgressBar.Value = progress;
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
						CacheDownload = false;
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

		private readonly string[] CacheRegionalCheckName = new string[] { "TextMap", "RandomDialogData", "sprite" };
		private enum CacheType { Data, Event, Ai, Unknown }
		private string ReturnCacheTypeEnum(CacheType enumName)
		{
			switch (enumName)
			{
				case CacheType.Ai:
					return "ai";
				case CacheType.Data:
					return "data";
				case CacheType.Event:
					return "event";
				default:
					throw new Exception($"Data returns unknown type");
			}
		}

		/*
		 * N	-> Name of the necessary file
		 * CRC	-> Expected MD5 hash of the file
		 * CS	-> Size of the file
		 */
		private class CacheDataProperties
		{
			public string N { get; set; }
			public string CRC { get; set; }
			public long CS { get; set; }
			public CacheType Type { get; set; }
		}

		/* Filter Region Type of Cache File
		 * 0 -> the file is a regional file but outside user region.
		 * 1 -> the file is a regional file but inside user region and downloadable.
		 * 2 -> the file is not a regional file and downloadable.
		 */
		private byte FilterRegion(string input, string regionName)
		{
			foreach (string word in CacheRegionalCheckName)
				if (input.Contains(word))
					if (input.Contains($"{word}_{regionName}"))
						return 1;
					else
						return 0;

			return 2;
		}

		// Normalize Unix path (/) to Windows path (\)
		private string NormalizePath(string i) => i.Replace('/', '\\');

		private async void DownloadGameCacheHi3Mirror(string game_language)
		{
			string data, url, localMD5, name,
				path = string.Empty,
				preformatAPIUrl = OnlineVersionInfo.game_info.mirror.hi3mirror.api.ToString(),
				preformatDataUrl = OnlineVersionInfo.game_info.mirror.hi3mirror.game_cache.ToString();

			List<CacheDataProperties> CacheFiles, BadFiles;
			CacheType cacheType;
			BpWebClient web_client = new BpWebClient();
			FileInfo fileInfo;

			try
			{
				int server = (int)Server == 0 ? 1 : 0;

				CacheFiles = new List<CacheDataProperties>();
				BadFiles = new List<CacheDataProperties>();

				await Task.Run(() =>
				{
					for (int i = 0; i < 3; i++)
					{
						// Classify data type as per i
						// 0 or _	: Data and AI/Btree cache
						// 1		: Resources/Event cache
						// 2		: Btree/Ai cache
						switch (i)
						{
							case 0:
								cacheType = CacheType.Data;
								break;
							case 1:
								cacheType = CacheType.Event;
								break;
							case 2:
								cacheType = CacheType.Ai;
								break;
							default:
								cacheType = CacheType.Unknown;
								break;
						}

						// Get URL and API data
						url = string.Format(preformatAPIUrl, i, server);
						data = web_client.DownloadString(url);

						// Do Elimination Process
						// Deserialize string and make it to Object as List<CacheDataProperties>
						foreach (CacheDataProperties file in JsonConvert.DeserializeObject<List<CacheDataProperties>>(data))
							// Do check whenever the file is included regional language as game_language defined
							// Then add it to CacheFiles list
							if (FilterRegion(file.N, game_language) > 0)
								// Do add if the Filter passed.
								CacheFiles.Add(new CacheDataProperties
								{
									N = file.N,
									CRC = file.CRC,
									CS = file.CS,
									Type = cacheType
								});
					}
				});
				Log("success!", false);
			}
			catch(WebException ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to connect to Hi3Mirror:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_net_error_msg"], ex.Message)).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}

			try
			{
				List<FileInfo> existing_files = new DirectoryInfo(GameCachePath).GetFiles("*", SearchOption.AllDirectories).Where(x => x.DirectoryName.Contains(@"Data\data") || x.DirectoryName.Contains("Resources")).ToList();
				List<FileInfo> useless_files = existing_files;
				long bad_files_size = 0;

				Status = LauncherStatus.Working;
				OptionsButton.IsEnabled = true;
				ProgressBar.IsIndeterminate = false;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
				Log("Verifying game cache...");
				await Task.Run(() =>
				{

					for (int i = 0; i < CacheFiles.Count; i++)
					{
						name = $"{NormalizePath(CacheFiles[i].N)}.unity3d";

						// Combine Path and assign their own path
						// If none of them assigned as Unknown type, throw an exception.
						switch (CacheFiles[i].Type)
						{
							default:
								throw new Exception("Unknown cache file data type");
							case CacheType.Data:
								path = Path.Combine(GameCachePath, "Data", name);
								break;
							case CacheType.Ai:
							case CacheType.Event:
								path = Path.Combine(GameCachePath, "Resources", name);
								break;
						}

						fileInfo = new FileInfo(path);

						Dispatcher.Invoke(() =>
						{
							ProgressText.Text = string.Format(App.TextStrings["progresstext_verifying_file"], i + 1, CacheFiles.Count);
							var progress = (i + 1f) / CacheFiles.Count;
							ProgressBar.Value = progress;
							TaskbarItemInfo.ProgressValue = progress;
						});

						if (!fileInfo.Exists || BpUtility.CalculateMD5(fileInfo.FullName) != CacheFiles[i].CRC)
						{
							if (App.AdvancedFeatures)
								if (File.Exists(path))
									Log($"File corrupted: {path}");
								else
									Log($"File missing: {path}");

							BadFiles.Add(CacheFiles[i]);
						}
						else
						{
							useless_files.RemoveAll(x => x.FullName == path);
							if (App.AdvancedFeatures)
								Log($"File OK: {path}");
						}
					}

					foreach(var useless_file in useless_files)
						Log($"Useless file: {useless_file.FullName}");

					bad_files_size = BadFiles.Sum(x => x.CS);
				});

				ProgressText.Text = string.Empty;
				ProgressBar.Visibility = Visibility.Hidden;
				ProgressBar.Value = 0;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
				TaskbarItemInfo.ProgressValue = 0;
				WindowState = WindowState.Normal;

				if (useless_files.Count > 0)
				{
					Log($"Found useless files: {useless_files.Count}");
					if (new DialogWindow(App.TextStrings["contextmenu_download_cache"], string.Format("Found {0} useless files, wanna remove 'em?", useless_files.Count), DialogWindow.DialogType.Question).ShowDialog() == true)
						foreach (var file in useless_files)
							DeleteFile(file.FullName, true);
				}

				if (BadFiles.Count > 0)
				{
					Log($"Finished verifying files, found corrupted/missing files: {BadFiles.Count}");
					if (new DialogWindow(App.TextStrings["contextmenu_download_cache"], string.Format(App.TextStrings["msgbox_repair_3_msg"], BadFiles.Count, BpUtility.ToBytesCount(bad_files_size)), DialogWindow.DialogType.Question).ShowDialog() == true)
					{
						string server = Server == 0 ? "global" : "sea";

						int downloaded_files = 0;
						Status = LauncherStatus.Downloading;

						await Task.Run(async () =>
						{
							for (int i = 0; i < BadFiles.Count; i++)
							{
								path = $"{NormalizePath(BadFiles[i].N)}.unity3d";
								switch (BadFiles[i].Type)
								{
									case CacheType.Data:
										path = Path.Combine(GameCachePath, "Data", path);
										break;
									case CacheType.Event:
										path = Path.Combine(GameCachePath, "Resources", path);
										break;
									case CacheType.Ai:
										path = Path.Combine(GameCachePath, "Resources", path);
										break;
								}

								url = string.Format(preformatDataUrl, server, ReturnCacheTypeEnum(BadFiles[i].Type), BadFiles[i].N);

								Dispatcher.Invoke(() =>
								{
									ProgressText.Text = string.Format(App.TextStrings["progresstext_downloading_file"], i + 1, BadFiles.Count);
									var progress = (i + 1f) / BadFiles.Count;
									ProgressBar.Value = progress;
									TaskbarItemInfo.ProgressValue = progress;
								});

								try
								{
									if (!Directory.Exists(Path.GetDirectoryName(path)))
										Directory.CreateDirectory(Path.GetDirectoryName(path));

									await web_client.DownloadFileTaskAsync(new Uri(url), path);

									Dispatcher.Invoke(() => { ProgressText.Text = string.Format(App.TextStrings["progresstext_verifying_file"], i + 1, BadFiles.Count); });

									localMD5 = BpUtility.CalculateMD5(path);

									if (File.Exists(path) && localMD5 != BadFiles[i].CRC)
										Log($"ERROR: Failed to verify file [{path}]", true, 1);
									else
									{
										Log($"Downloaded file {BadFiles[i].N}");
										downloaded_files++;
									}
								}
								catch (Exception ex)
								{
									Log($"ERROR: Failed to download file [{BadFiles[i].N}] ({url}): {ex.Message}", true, 1);
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

						if (downloaded_files == BadFiles.Count)
						{
							Log($"Successfully downloaded {downloaded_files} file(s)");
							Dispatcher.Invoke(() =>
							{
								new DialogWindow(App.TextStrings["contextmenu_download_cache"], string.Format(App.TextStrings["msgbox_repair_4_msg"], downloaded_files)).ShowDialog();
							});
						}

						else
						{
							int skipped_files = BadFiles.Count - downloaded_files;
							if (downloaded_files > 0)
								Log($"Successfully downloaded {downloaded_files} files, failed to download {skipped_files} files");
							
							Dispatcher.Invoke(() =>
							{
								new DialogWindow(App.TextStrings["contextmenu_download_cache"], string.Format(App.TextStrings["msgbox_repair_5_msg"], skipped_files)).ShowDialog();
							});
						}
					}
				}
				else
				{
					Log("Finished verifying files, the cache is up-to-date");
					Dispatcher.Invoke(() =>
					{
						ProgressText.Text = string.Empty;
						ProgressBar.Visibility = Visibility.Hidden;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
					});
					new DialogWindow(App.TextStrings["contextmenu_download_cache"], App.TextStrings["msgbox_repair_2_msg"]).ShowDialog();
				}
				Status = LauncherStatus.Ready;

			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
			}
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
				var web_request = BpUtility.CreateWebRequest(OnlineVersionInfo.launcher_info.stat_url.ToString(), "POST");
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
							Log($"WARNING: Failed to send download statistics for {file}", true, 2);
						}
					}
				}
			}
			catch
			{
				Log($"WARNING: Failed to send download statistics for {file}", true, 2);
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
				DeleteFile(Path.Combine(App.LauncherRootPath, "BetterHI3Launcher.exe.bak"), true); // legacy name
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
							Log($"ERROR: Launcher integrity error, attempting self-repair...", true, 1);
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
							Log($"ERROR: Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
							DeleteFile(App.LauncherArchivePath, true);
							Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_verify_error_title"], App.TextStrings["msgbox_verify_error_1_msg"]).ShowDialog();});
							return;
						}
						Log("success!", false);
						Log("Performing update...");
						File.Move(Path.Combine(App.LauncherRootPath, exe_name), Path.Combine(App.LauncherRootPath, old_exe_name));
						using(var archive = ArchiveFactory.Open(App.LauncherArchivePath))
						{
							var reader = archive.ExtractAllEntries();
							while(reader.MoveToNextEntry())
							{
								reader.WriteEntryToDirectory(App.LauncherRootPath, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
							}
						}
						Log("success!", false);
						Dispatcher.Invoke(() => {BpUtility.RestartApp();});
						return;
					}
					else
					{
						DeleteFile(App.LauncherArchivePath, true);
						DeleteFile(Path.Combine(App.LauncherRootPath, "BetterHI3Launcher.7z"), true); // legacy name
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
				Log($"ERROR: Failed to start the launcher:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_start_error_title"], string.Format(App.TextStrings["msgbox_start_error_msg"], ex.Message)).ShowDialog();
				return;
			}

			
			if(App.FirstLaunch)
			{
				LegacyBoxActive = true;
				IntroBox.Visibility = Visibility.Visible;
			}
			#if !DEBUG
			if(App.LauncherRegKey != null && App.LauncherRegKey.GetValue("LauncherVersion") != null)
			{
				if(new App.LauncherVersion(App.LocalLauncherVersion.ToString()).IsNewerThan(new App.LauncherVersion(App.LauncherRegKey.GetValue("LauncherVersion").ToString())))
				{
					LegacyBoxActive = true;
					ChangelogBox.Visibility = Visibility.Visible;
					ChangelogBoxMessageTextBlock.Visibility = Visibility.Visible;
					FetchChangelog();
				}
			}
			#endif
			try
			{
				if(App.LauncherRegKey.GetValue("LauncherVersion") == null || App.LauncherRegKey.GetValue("LauncherVersion") != null && App.LauncherRegKey.GetValue("LauncherVersion").ToString() != App.LocalLauncherVersion.ToString())
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
						Log("WARNING: Custom background file cannot be found, resetting to official...", true, 2);
						BpUtility.DeleteFromRegistry("CustomBackgroundName");
					}
				}
			}
			if(!App.FirstLaunch)
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
						Log($"ERROR: Failed to start the game:\n{ex}", true, 1);
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
						string[] game_reg_names = {"Honkai Impact 3rd", "Honkai Impact 3", "崩坏3", "崩壞3rd", "붕괴3rd"};
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
							var dialog = new DialogWindow(App.TextStrings["msgbox_install_title"], $"{App.TextStrings["msgbox_install_1_msg"]}\n{string.Format(App.TextStrings["msgbox_install_2_msg"], BpUtility.ToBytesCount((long)miHoYoVersionInfo.size))}\n{string.Format(App.TextStrings["msgbox_install_3_msg"], BpUtility.ToBytesCount((long)miHoYoVersionInfo.game.latest.size))}", DialogWindow.DialogType.Install);
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
							try
							{
								path = Directory.CreateDirectory(GameInstallPath).FullName;
								Directory.Delete(path);
							}
							catch(Exception ex)
							{
								new DialogWindow(App.TextStrings["msgbox_install_dir_error_title"], ex.Message).ShowDialog();
								continue;
							}
							if(new DialogWindow(App.TextStrings["msgbox_install_title"], string.Format(App.TextStrings["msgbox_install_4_msg"], GameInstallPath), DialogWindow.DialogType.Question).ShowDialog() == false)
							{
								continue;
							}
							var game_install_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(GameInstallPath) && x.IsReady).FirstOrDefault();
							if(game_install_drive == null || game_install_drive.DriveType == DriveType.CDRom)
							{
								new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_wrong_drive_type_msg"]).ShowDialog();
								continue;
							}
							else if(game_install_drive.TotalFreeSpace < (long)miHoYoVersionInfo.game.latest.size)
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
							Log($"ERROR: Failed to select game installation directory:\n{ex}", true, 1);
							new DialogWindow(App.TextStrings["msgbox_install_dir_error_title"], App.TextStrings["msgbox_install_dir_error_msg"]).ShowDialog();
							Status = LauncherStatus.Ready;
							return;
						}
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
				if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_2_msg"]}\n{App.TextStrings["msgbox_abort_3_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == true)
				{
					download.Pause();
					download = null;
					if(!CacheDownload)
					{
						if(!string.IsNullOrEmpty(GameArchivePath))
						{
							while(BpUtility.IsFileLocked(new FileInfo(GameArchivePath)))
							{
								Thread.Sleep(10);
							}
							DeleteFile(GameArchivePath, true);
						}
					}
					else
					{
						if(!string.IsNullOrEmpty(CacheArchivePath))
						{
							while(BpUtility.IsFileLocked(new FileInfo(CacheArchivePath)))
							{
								Thread.Sleep(10);
							}
							DeleteFile(CacheArchivePath, true);
						}
						CacheDownload = false;
					}
					DownloadPaused = false;
					Log("Download cancelled");
					Status = LauncherStatus.Ready;
					GameUpdateCheck();
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

		private async void DownloadPauseButton_Click(object sender, RoutedEventArgs e)
		{
			if(!DownloadPaused)
			{
				download.Pause();
				Status = LauncherStatus.DownloadPaused;
				DownloadProgressBarStackPanel.Visibility = Visibility.Visible;
				DownloadETAText.Visibility = Visibility.Hidden;
				DownloadSpeedText.Visibility = Visibility.Hidden;
				DownloadPauseButton.Visibility = Visibility.Collapsed;
				DownloadResumeButton.Visibility = Visibility.Visible;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
			}
			else
			{
				Status = LauncherStatus.Downloading;
				ProgressText.Text = string.Empty;
				ProgressBar.Visibility = Visibility.Hidden;
				DownloadProgressBarStackPanel.Visibility = Visibility.Visible;
				LaunchButton.IsEnabled = true;
				LaunchButton.Content = App.TextStrings["button_cancel"];
				DownloadPauseButton.Visibility = Visibility.Visible;
				DownloadResumeButton.Visibility = Visibility.Collapsed;
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
					GameUpdateCheck();
				}
			}
		}

		private async void PreloadButton_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready && Status != LauncherStatus.Running)
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
					if(new DialogWindow(App.TextStrings["label_pre_install"], $"{App.TextStrings["msgbox_pre_install_msg"]}\n{string.Format(App.TextStrings["msgbox_install_2_msg"], BpUtility.ToBytesCount(size))}", DialogWindow.DialogType.Question).ShowDialog() == false)
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
					Log($"Starting to pre-download game: {title} ({url})");
				}
				else
				{
					Log("Pre-download resumed");
				}
				PreloadDownload = true;
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
						Thread.Sleep(500);
					}
					if(download == null)
					{
						abort = true;
						Status = LauncherStatus.Ready;
						return;
					}
					Log("Downloaded pre-download archive");
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
						Log("Validating pre-download archive...");
						string actual_md5 = BpUtility.CalculateMD5(path);
						if(actual_md5 == md5.ToUpper())
						{
							Log("success!", false);
							string new_path = path.Substring(0, path.Length - 4);
							if(!File.Exists(new_path))
							{
								File.Move(path, new_path);
							}
							else if(File.Exists(new_path) && new FileInfo(new_path).Length != size)
							{
								DeleteFile(new_path, true);
								File.Move(path, new_path);
							}
							Log("Successfully pre-downloaded the game");
							PreloadDownload = false;
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
					Log($"ERROR: Failed to pre-download the game:\n{ex}", true, 1);
					new DialogWindow(App.TextStrings["msgbox_install_error_title"], App.TextStrings["msgbox_install_error_msg"]).ShowDialog();
					Status = LauncherStatus.Ready;
				}
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"ERROR: Failed to download game pre-download archive:\n{ex}", true, 1);
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
				Log("Pre-download paused");
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
					Log($"ERROR: Failed to resume pre-download:\n{ex}", true, 1);
				}
			}
		}

		private async Task CM_DownloadCache_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			if(Server != HI3Server.GLB && Server != HI3Server.SEA)
			{
				new DialogWindow(App.TextStrings["contextmenu_download_cache"], App.TextStrings["msgbox_feature_not_available_msg"]).ShowDialog();
				return;
			}
			int game_language_int;
			string game_language;
			var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
			string value = "multi_language_h2498394913";
			if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.DWord)
			{
				new DialogWindow(App.TextStrings["msgbox_registry_error_title"], $"{App.TextStrings["msgbox_registry_empty_1_msg"]}\n{App.TextStrings["msgbox_registry_empty_2_msg"]}").ShowDialog();
				return;
			}
			game_language_int = (int)key.GetValue(value);
			switch(game_language_int)
			{
				case 0:
					game_language = "cn";
					break;
				case 1:
					game_language = "en";
					break;
				case 2:
					if(Server == HI3Server.SEA)
					{
						game_language = "vn";
						break;
					}
					goto case 0;
				case 3:
					if(Server == HI3Server.SEA)
					{
						game_language = "th";
						break;
					}
					goto case 0;
				case 4:
					if(Server == HI3Server.GLB)
					{
						game_language = "fr";
						break;
					}
					goto case 0;
				case 5:
					if(Server == HI3Server.GLB)
					{
						game_language = "de";
						break;
					}
					goto case 0;
				case 6:
					if(Server == HI3Server.SEA)
					{
						game_language = "id";
						break;
					}
					goto case 0;
				default:
					goto case 0;
			}

			if(Mirror == HI3Mirror.miHoYo || Mirror == HI3Mirror.Hi3Mirror)
			{
				if(new DialogWindow(App.TextStrings["contextmenu_download_cache"], string.Format(App.TextStrings["msgbox_download_cache_5_msg"], OnlineVersionInfo.game_info.mirror.hi3mirror.maintainer.ToString()), DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					return;
				}
				Status = LauncherStatus.CheckingUpdates;
				Dispatcher.Invoke(() => {ProgressText.Text = App.TextStrings["progresstext_mirror_connect"];});
				Log("Connecting to Hi3Mirror...");
				DownloadGameCacheHi3Mirror(game_language);
			}
			else
			{
				LegacyBoxActive = true;
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
						switch(Server)
						{
							case HI3Server.GLB:
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
									GameCacheMetadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache.global.id.ToString(), 1);
									if(GameCacheMetadata != null)
									{
										GameCacheMetadataNumeric = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.id.ToString(), 2);
									}
								}
								break;
							case HI3Server.SEA:
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
									GameCacheMetadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache.os.id.ToString(), 1);
									if(GameCacheMetadata != null)
									{
										GameCacheMetadataNumeric = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.id.ToString(), 2);
									}
								}
								break;
						}
						if(GameCacheMetadata == null || GameCacheMetadataNumeric == null)
						{
							LegacyBoxActive = false;
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
					LegacyBoxActive = false;
					Status = LauncherStatus.Error;
					Log($"ERROR: Failed to fetch cache metadata:\n{ex}", true, 1);
					Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_mirror_error_msg"], ex.Message)).ShowDialog();});
					Status = LauncherStatus.Ready;
					return;
				}
			}
		}

		private async Task CM_Repair_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			if(Server != HI3Server.GLB && Server != HI3Server.SEA)
			{
				new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_feature_not_available_msg"]).ShowDialog();
				return;
			}

			LegacyBoxActive = true;
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
					if(OnlineRepairInfo.game_version != LocalVersionInfo.game_info.version && !App.AdvancedFeatures)
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
				LegacyBoxActive = false;
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
			if(App.LauncherRootPath.Contains($@"{GameInstallPath}\"))
			{
				new DialogWindow(App.TextStrings["msgbox_move_error_title"], App.TextStrings["msgbox_move_4_msg"]).ShowDialog();
				return;
			}

			while(true)
			{
				var dialog = new DialogWindow(App.TextStrings["msgbox_move_title"], App.TextStrings["msgbox_move_1_msg"], DialogWindow.DialogType.Install);
				dialog.InstallPathTextBox.Text = GameInstallPath;
				if(dialog.ShowDialog() == false)
				{
					return;
				}
				string path = dialog.InstallPathTextBox.Text;
				if($@"{path}\".Contains($@"{GameInstallPath}\"))
				{
					new DialogWindow(App.TextStrings["msgbox_move_error_title"], App.TextStrings["msgbox_move_3_msg"]).ShowDialog();
					continue;
				}
				try
				{
					path = Directory.CreateDirectory(path).FullName;
					Directory.Delete(path);
				}
				catch(Exception ex)
				{
					new DialogWindow(App.TextStrings["msgbox_install_dir_error_title"], ex.Message).ShowDialog();
					continue;
				}
				var game_move_to_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(path) && x.IsReady).FirstOrDefault();
				if(game_move_to_drive == null)
				{
					new DialogWindow(App.TextStrings["msgbox_move_error_title"], App.TextStrings["msgbox_move_wrong_drive_type_msg"]).ShowDialog();
					continue;
				}
				else if(game_move_to_drive.TotalFreeSpace < new DirectoryInfo(GameInstallPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(x => x.Length))
				{
					if(new DialogWindow(App.TextStrings["msgbox_move_title"], App.TextStrings["msgbox_move_little_space_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						continue;
					}
				}
				else if(new DialogWindow(App.TextStrings["msgbox_move_title"], string.Format(App.TextStrings["msgbox_move_2_msg"], path), DialogWindow.DialogType.Question).ShowDialog() == false)
				{
					continue;
				}
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
					}
				});
				return;
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
						ResetVersionInfo(true);
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
			if(Server != HI3Server.GLB && Server != HI3Server.SEA)
			{
				new DialogWindow(App.TextStrings["contextmenu_fix_subtitles"], App.TextStrings["msgbox_feature_not_available_msg"]).ShowDialog();
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
									ToggleLog(true);
									TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
									new DialogWindow(App.TextStrings["msgbox_extract_skip_title"], App.TextStrings["msgbox_extract_skip_msg"]).ShowDialog();
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
										if(App.AdvancedFeatures)
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
					if(Server == HI3Server.GLB)
					{
						ProgressBar.IsIndeterminate = true;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
						subtitle_files = Directory.EnumerateFiles(game_video_path, "*id.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("id.srt", StringComparison.CurrentCultureIgnoreCase)).ToList();
						subtitle_files.AddRange(subtitle_files = Directory.EnumerateFiles(game_video_path, "*th.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("th.srt", StringComparison.CurrentCultureIgnoreCase)).ToList());
						subtitle_files.AddRange(subtitle_files = Directory.EnumerateFiles(game_video_path, "*vn.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("vn.srt", StringComparison.CurrentCultureIgnoreCase)).ToList());
						if(subtitle_files.Count > 0)
						{
							int deleted_subs = 0;
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
										deleted_subs++;
									}
									catch
									{
										Log($"WARNING: Failed to delete {subtitle_file}", true, 2);
									}
								}
							});
							Log($"Deleted {deleted_subs} useless subtitles");
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
					try
					{
						if(key.GetValue(value) != null)
						{
							key.DeleteValue(value);
						}
					}catch{}
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
					try
					{
						if(key.GetValue(value) != null)
						{
							key.DeleteValue(value);
						}
					}catch{}
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

		private void CM_CustomLaunchOptions_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			try
			{
				var dialog = new DialogWindow(App.TextStrings["contextmenu_custom_launch_options"], App.TextStrings["msgbox_custom_launch_options_msg"], DialogWindow.DialogType.CustomLaunchOptions);
				try
				{
					dialog.CustomLaunchOptionsTextBox.Text = LocalVersionInfo.launch_options.ToString().Trim();
				}catch{}
				if(dialog.ShowDialog() == false)
				{
					return;
				}
				string launch_options = dialog.CustomLaunchOptionsTextBox.Text.Trim();
				if(string.IsNullOrEmpty(launch_options))
				{
					LocalVersionInfo.Remove("launch_options");
				}
				else
				{
					LocalVersionInfo.launch_options = launch_options;
				}
				Log("Saving launch options...");
				BpUtility.WriteToRegistry(RegistryVersionInfo, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(LocalVersionInfo)), RegistryValueKind.Binary);
				Log("success!", false);
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
				string[] values = {"GENERAL_DATA_V2_ResourceDownloadType_h2238376574", "GENERAL_DATA_V2_ResourceDownloadVersion_h1528433916"};
				foreach(string value in values)
				{
					if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.DWord)
					{
						try
						{
							if(key.GetValue(value) != null)
							{
								key.DeleteValue(value);
							}
						}catch{}
					}
				}
				var value_before = key.GetValue(values[0]);
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
				key.SetValue(values[0], value_after, RegistryValueKind.DWord);
				key.Close();
				Log("Download type has been reset");
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

		private void CM_Changelog_Click(object sender, RoutedEventArgs e)
		{
			LegacyBoxActive = true;
			ChangelogBox.Visibility = Visibility.Visible;
			ChangelogBoxScrollViewer.ScrollToHome();
			FetchChangelog();
		}

		private void CM_CustomBackground_Click(object sender, RoutedEventArgs e)
		{
			bool first_time = App.LauncherRegKey.GetValue("CustomBackgroundName") == null ? true : false;
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
					string custom_background_path = Path.Combine(App.LauncherBackgroundsPath, App.LauncherRegKey.GetValue("CustomBackgroundName").ToString());
					BpUtility.DeleteFromRegistry("CustomBackgroundName");
					if(DeleteFile(custom_background_path))
					{
						Log("success!", false);
					}
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

		private void CM_ShowLog_Click(object sender, RoutedEventArgs e)
		{
			var item = sender as MenuItem;
			item.IsChecked = !item.IsChecked;
			ToggleLog(item.IsChecked);
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
			App.DisableSounds = item.IsChecked;
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
			if(App.LauncherLanguage != "en" && App.LauncherLanguage != "de" && App.LauncherLanguage != "id" && App.LauncherLanguage != "vi")
			{
				if(lang != App.TextStrings["contextmenu_language_portuguese_brazil"] && lang != App.TextStrings["contextmenu_language_portuguese_portugal"])
				{
					lang = lang.ToLower();
				}
				else
				{
					lang = char.ToLower(lang[0]) + lang.Substring(1);
				}
			}
			if(App.LauncherLanguage == "id" || App.LauncherLanguage == "vi")
			{
				lang = char.ToLower(lang[0]) + lang.Substring(1);
			}
			msg = string.Format(App.TextStrings["msgbox_language_msg"], lang);
			lang = item.Header.ToString();
			if(new DialogWindow(App.TextStrings["contextmenu_language"], msg, DialogWindow.DialogType.Question).ShowDialog() == false)
			{
				return;
			}
			if(Status == LauncherStatus.DownloadPaused)
			{
				if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_1_msg"]}\n{App.TextStrings["msgbox_abort_3_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == false)
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
					else if(lang == App.TextStrings["contextmenu_language_portuguese_brazil"])
					{
						App.LauncherLanguage = "pt-BR";
					}
					else if(lang == App.TextStrings["contextmenu_language_portuguese_portugal"])
					{
						App.LauncherLanguage = "pt-PT";
					}
					else if(lang == App.TextStrings["contextmenu_language_german"])
					{
						App.LauncherLanguage = "de";
					}
					else if(lang == App.TextStrings["contextmenu_language_indonesian"])
					{
						App.LauncherLanguage = "id";
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
					else if(lang == App.TextStrings["contextmenu_language_french"])
					{
						App.LauncherLanguage = "fr";
					}
					else if(lang == App.TextStrings["contextmenu_language_indonesian"])
					{
						App.LauncherLanguage = "id";
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
			LegacyBoxActive = true;
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
			if(BackgroundImageDownloading)
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
				download = null;
				DownloadPaused = false;
				DeleteFile(GameArchivePath);
				if(!PatchDownload)
				{
					ResetVersionInfo();
				}
			}
			PreloadDownload = false;
			CacheDownload = false;
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
			}
			if(Server != HI3Server.GLB && Server != HI3Server.SEA)
			{
				MirrorDropdown.SelectedIndex = 0;
				Mirror = HI3Mirror.miHoYo;
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
			if(Server != HI3Server.GLB && Server != HI3Server.SEA)
			{
				new DialogWindow(App.TextStrings["label_mirror"], App.TextStrings["msgbox_feature_not_available_msg"]).ShowDialog();
				return;
			}
		}

		private void MirrorDropdown_Changed(object sender, SelectionChangedEventArgs e)
		{
			var index = MirrorDropdown.SelectedIndex;
			if((int)Mirror == index)
			{
				return;
			}
			if(Server != HI3Server.GLB && Server != HI3Server.SEA)
			{
				MirrorDropdown.SelectedIndex = 0;
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
					Mirror = HI3Mirror.Hi3Mirror;
					break;
				case 2:
					Mirror = HI3Mirror.MediaFire;
					break;
				case 3:
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
			LegacyBoxActive = false;
			IntroBox.Visibility = Visibility.Collapsed;
			if(App.FirstLaunch)
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
			LegacyBoxActive = false;
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
			LegacyBoxActive = false;
			DownloadCacheBox.Visibility = Visibility.Collapsed;
			DownloadGameCache(false);
		}

		private void DownloadCacheBoxCloseButton_Click(object sender, RoutedEventArgs e)
		{
			LegacyBoxActive = false;
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
					if(App.AdvancedFeatures)
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
								if(App.AdvancedFeatures)
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
												var metadata = FetchMediaFireFileMetadata(urls[j].Substring(31, 15));
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
					if(App.AdvancedFeatures)
					{
						if(new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_repair_8_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
						{
							return;
						}
					}
				}
				LegacyBoxActive = false;
				RepairBox.Visibility = Visibility.Collapsed;
				Status = LauncherStatus.Working;
				OptionsButton.IsEnabled = true;
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
				string server = null;
				if(Server == HI3Server.GLB)
				{
					server = "global";
				}
				else if(Server == HI3Server.SEA)
				{
					server = "os";
				}
				var dialog = new SaveFileDialog
				{
					InitialDirectory = App.LauncherRootPath,
					Filter = "JSON|*.json",
					FileName = $"bh3_files_{server}_{LocalVersionInfo.game_info.version}.json"
				};
				if(dialog.ShowDialog() == true)
				{
					try
					{
						Status = LauncherStatus.Working;
						OptionsButton.Visibility = Visibility.Visible;
						ProgressBar.IsIndeterminate = false;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
						Log("Generating game file hashes...");
						var files = new DirectoryInfo(GameInstallPath).GetFiles("*", SearchOption.AllDirectories).Where(x =>
						!x.Attributes.HasFlag(FileAttributes.Hidden) &&
						x.Extension != ".log" &&
						x.Extension != ".bat" &&
						x.Extension != ".zip" &&
						x.Name != "blockVerifiedVersion.txt" &&
						x.Name != "config.ini" &&
						x.Name != "manifest.m" &&
						x.Name != "UniFairy.sys" &&
						x.Name != "Version.txt" &&
						!x.Name.Contains("Blocks_") &&
						!x.Name.Contains("AUDIO_Avatar") &&
						!x.Name.Contains("AUDIO_BGM") &&
						!x.Name.Contains("AUDIO_Dialog") &&
						!x.Name.Contains("AUDIO_DLC") &&
						!x.Name.Contains("AUDIO_EVENT") &&
						!x.Name.Contains("AUDIO_Ex") &&
						!x.Name.Contains("AUDIO_HOT_FIX") &&
						!x.Name.Contains("AUDIO_Main") &&
						!x.Name.Contains("AUDIO_Story") &&
						!x.Name.Contains("AUDIO_Vanilla") &&
						!x.DirectoryName.Contains("Video") &&
						!x.DirectoryName.Contains("webCaches")
						).ToList();
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
			LegacyBoxActive = false;
			RepairBox.Visibility = Visibility.Collapsed;
			await Generate();
		}

		private void RepairBoxCloseButton_Click(object sender, RoutedEventArgs e)
		{
			LegacyBoxActive = false;
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
				Log($"Setting in-game FPS to {fps_combat}, menu FPS to {fps_menu}...");
				GameGraphicSettings.IsUserDefinedGrade = false;
				GameGraphicSettings.IsUserDefinedVolatile = true;
				GameGraphicSettings.TargetFrameRateForInLevel = fps_combat;
				GameGraphicSettings.TargetFrameRateForOthers = fps_menu;
				var value_after = Encoding.UTF8.GetBytes($"{JsonConvert.SerializeObject(GameGraphicSettings)}\0");
				var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
				key.SetValue("GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411", value_after, RegistryValueKind.Binary);
				key.Close();
				FPSInputBox.Visibility = Visibility.Collapsed;
				Log("success!", false);
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
				string is_fullscreen = fullscreen ? "enabled" : "disabled";
				is_fullscreen = fullscreen ? App.TextStrings["enabled"].ToLower() : App.TextStrings["disabled"].ToLower();
				Log($"Setting game resolution to {width}x{height}, fullscreen {is_fullscreen}...");
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
				Log("success!", false);
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
			LegacyBoxActive = false;
			ChangelogBox.Visibility = Visibility.Collapsed;
			ChangelogBoxMessageTextBlock.Visibility = Visibility.Collapsed;
		}

		private void AboutBoxGitHubButton_Click(object sender, RoutedEventArgs e)
		{
			AboutBox.Visibility = Visibility.Collapsed;
			BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher", null, App.LauncherRootPath, true);
		}

		private void AboutBoxCloseButton_Click(object sender, RoutedEventArgs e)
		{
			LegacyBoxActive = false;
			AboutBox.Visibility = Visibility.Collapsed;
		}

		private void DownloadCacheCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_download_cache"]);
			if(item.IsEnabled && !LegacyBoxActive)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void FixSubtitlesCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_fix_subtitles"]);
			if(item.IsEnabled)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void RepairGameCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_repair"]);
			if(item.IsEnabled && !LegacyBoxActive)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void MoveGameCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_move"]);
			if(item.IsEnabled)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void UninstallGameCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_uninstall"]);
			if(item.IsEnabled)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void WebProfileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_web_profile"]);
			if(item.IsEnabled)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void FeedbackCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_feedback"]);
			if(item.IsEnabled)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void ChangelogCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_changelog"]);
			if(item.IsEnabled && !LegacyBoxActive)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void CustomBackgroundCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_custom_background"]);
			if(item.IsEnabled)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void ToggleLogCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_show_log"]);
			if(item.IsEnabled)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void ToggleSoundsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_sounds"]);
			if(item.IsEnabled)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void AboutCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_about"]);
			if(item.IsEnabled && !LegacyBoxActive)
			{
				var peer = new MenuItemAutomationPeer(item);
				var inv_prov = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
				inv_prov.Invoke();
			}
		}

		private void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			if(Status == LauncherStatus.Downloading || Status == LauncherStatus.DownloadPaused)
			{
				if(download == null)
				{
					if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_1_msg"]}\n{App.TextStrings["msgbox_abort_3_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == true)
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
					if(new DialogWindow(App.TextStrings["msgbox_abort_title"], $"{App.TextStrings["msgbox_abort_1_msg"]}\n{App.TextStrings["msgbox_abort_4_msg"]}", DialogWindow.DialogType.Question).ShowDialog() == true)
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
				if(!PreloadDownload)
				{
					Status = LauncherStatus.Ready;
				}
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
				Path.Combine(path, "崩坏3"),
				Path.Combine(path, "崩壞3"),
				Path.Combine(path, "붕괴3rd"),
				Path.Combine(path, "Honkai Impact 3rd", "Games"),
				Path.Combine(path, "Honkai Impact 3", "Games"),
				Path.Combine(path, "Honkai Impact 3rd glb", "Games"),
				Path.Combine(path, "Honkai Impact 3 sea", "Games"),
				Path.Combine(path, "Honkai Impact 3rd tw", "Games"),
				Path.Combine(path, "Honkai Impact 3rd kr", "Games")
			});

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

		private int CheckForExistingGameClientServer(string path)
		{
			path = Path.Combine(path, @"BH3_Data\app.info");
			if(File.Exists(path))
			{
				var game_title_line = File.ReadLines(path).Skip(1).Take(1).First();
				if(!string.IsNullOrEmpty(game_title_line))
				{
					switch(game_title_line)
					{
						case "Honkai Impact 3rd":
							if(App.LauncherRegKey.GetValue("VersionInfoGlobal") == null)
							{
								return 0;
							}
							break;
						case "Honkai Impact 3":
							if(App.LauncherRegKey.GetValue("VersionInfoSEA") == null)
							{
								return 1;
							}
							break;
						case "崩坏3":
							if(App.LauncherRegKey.GetValue("VersionInfoCN") == null)
							{
								return 2;
							}
							break;
						case "崩壞3":
							if(App.LauncherRegKey.GetValue("VersionInfoTW") == null)
							{
								return 3;
							}
							break;
						case "붕괴3rd":
							if(App.LauncherRegKey.GetValue("VersionInfoKR") == null)
							{
								return 4;
							}
							break;
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
					   item.Header.ToString() == App.TextStrings["contextmenu_show_log"] ||
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

		private void ToggleLog(bool val)
		{
			LogBox.Visibility = val ? Visibility.Visible : Visibility.Collapsed;
			try
			{
				BpUtility.WriteToRegistry("ShowLog", val ? 1 : 0, RegistryValueKind.DWord);
			}
			catch(Exception ex)
			{
				Log($"ERROR: Failed to write value with key ShowLog to registry:\n{ex}", true, 1);
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
				try
				{
					var image = new BitmapImage();
					using(FileStream fs = new FileStream(path, FileMode.Open))
					{
						image.BeginInit();
						image.StreamSource = fs;
						image.CacheOption = BitmapCacheOption.OnLoad;
						image.EndInit();
					}
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
						if(App.LauncherRegKey.GetValue("CustomBackgroundName") != null)
						{
							DeleteFile(Path.Combine(App.LauncherBackgroundsPath, App.LauncherRegKey.GetValue("CustomBackgroundName").ToString()));
						}
						Log($"Setting custom background: {path}");
						string new_path = Path.Combine(App.LauncherBackgroundsPath, name);
						File.Copy(path, new_path, true);
						path = new_path;
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
						using(FileStream fs = new FileStream(path, FileMode.Open))
						{
							var image = new BitmapImage();
							image.BeginInit();
							image.StreamSource = fs;
							image.CacheOption = BitmapCacheOption.OnLoad;
							image.EndInit();
							BackgroundImage.Source = image;
						}
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
					BpUtility.RestartApp();
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
					Log("WARNING: Unable to write to log file, disabling writing to file...", true, 2);
				}
			}
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
				Log($"WARNING: Failed to delete {path}", true, 2);
				return false;
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