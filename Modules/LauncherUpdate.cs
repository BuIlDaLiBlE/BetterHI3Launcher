using Microsoft.Win32;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Shell;

namespace BetterHI3Launcher
{
	public partial class MainWindow
	{
		private bool LauncherUpdateCheck()
		{
			var OnlineLauncherVersion = new LauncherVersion(OnlineVersionInfo.launcher_info.version.ToString());
			if(OnlineLauncherVersion.IsNewerThan(App.LocalLauncherVersion))
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		private void LauncherLocalVersionCheck()
		{
			#if !DEBUG
			if(App.LauncherRegKey != null && App.LauncherRegKey.GetValue("LauncherVersion") != null)
			{
				if(new LauncherVersion(App.LocalLauncherVersion.ToString()).IsNewerThan(new LauncherVersion(App.LauncherRegKey.GetValue("LauncherVersion").ToString())))
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
				Log($"Failed to write critical registry info:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_registry_error_title"], App.TextStrings["msgbox_registry_error_msg"]).ShowDialog();
				return;
			}
			GameUpdateCheck();
		}

		private void DownloadLauncherUpdate()
		{
			Log("Downloading update...");
			Dispatcher.Invoke(() =>
			{
				ProgressText.Text = string.Empty;
				ProgressBar.Visibility = Visibility.Collapsed;
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
						DownloadSpeedText.Text = $"{App.TextStrings["label_download_speed"]} {tracker.GetBytesPerSecondString()}";
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
				Log($"Failed to download launcher update:\n{ex}", true, 1);
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
										Log($"Translations validation failed. Expected MD5: {translations_md5}, got MD5: {actual_md5}", true, 2);
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
								Log($"Failed to write critical registry info:\n{ex}", true, 1);
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
				Log($"Failed to download translations:\n{ex}", true, 1);
				MessageBox.Show(App.TextStrings["msgbox_translations_download_error_msg"], App.TextStrings["msgbox_generic_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
				DeleteFile(App.LauncherTranslationsFile, true);
				Array.Resize(ref App.CommandLineArgs, App.CommandLineArgs.Length + 1);
				App.CommandLineArgs[App.CommandLineArgs.Length - 1] = "NOTRANSLATIONS";
				BpUtility.RestartApp();
			}
		}
	}
}