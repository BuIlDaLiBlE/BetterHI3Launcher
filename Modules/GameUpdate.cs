using Hi3Helper.Http;
using IniParser;
using IniParser.Model;
using Microsoft.Win32;
using Newtonsoft.Json;
using SevenZip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace BetterHI3Launcher
{
	public partial class MainWindow
	{
		private async void GameUpdateCheck(bool server_changed = false)
		{
			if(Status == LauncherStatus.Error)
			{
				return;
			}
			Log("Checking for game update...");
			Status = LauncherStatus.CheckingUpdates;
			LocalVersionInfo = null;
			await Task.Run(() =>
			{
				try
				{
					int game_needs_update;

					Dispatcher.Invoke(() => {PreloadGrid.Visibility = Visibility.Collapsed;});
					if(!App.Starting)
					{
						FetchOnlineVersionInfo();
					}
					App.Starting = false;

					if(App.LauncherRegKey.GetValue(RegistryVersionInfo) != null)
					{
						LocalVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])App.LauncherRegKey.GetValue(RegistryVersionInfo)));
						GameInstallPath = LocalVersionInfo.game_info.install_path.ToString();
						var game_config_ini_file = Path.Combine(GameInstallPath, "config.ini");
						if(File.Exists(game_config_ini_file))
						{
							var data = new FileIniDataParser().ReadFile(game_config_ini_file);
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
							Log("Game directory is unsafe, resetting version info...", true, 2);
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
								GameArchiveName = BpUtility.GetFileNameFromUrl(url);
								GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
								PatchDownload = true;
							}
							Log("The game requires an update!");
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
							Log("The game version is the latest");
						}
						if(Status == LauncherStatus.UpdateAvailable)
						{
							if(!(bool)LocalVersionInfo.game_info.installed)
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
									var path = Path.Combine(GameInstallPath, BpUtility.GetFileNameFromUrl(miHoYoVersionInfo.pre_download_game.latest.path.ToString()));
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
						Log("Ready to install the game");
						if(server_changed)
						{
							FetchmiHoYoVersionInfo();
						}
						DownloadPaused = false;
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
					Log($"Checking for game update failed:\n{ex}", true, 1);
					Dispatcher.Invoke(() =>
					{
						if(new DialogWindow(App.TextStrings["msgbox_update_check_error_title"], App.TextStrings["msgbox_update_check_error_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
						{
							Status = LauncherStatus.CheckingUpdates;
							ProgressText.Visibility = Visibility.Collapsed;
							ProgressBar.Visibility = Visibility.Collapsed;
							ServerDropdown.IsEnabled = true;
						}
						else
						{
							Status = LauncherStatus.Ready;
							GameUpdateCheck();
						}
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

		private void DownloadBackgroundImage()
		{
			BackgroundImageDownloading = true;
			try
			{
				string url = null;
				switch(Server)
				{
					case HI3Server.GLB:
						string lang;
						switch(App.LauncherLanguage)
						{
							case "de":
								lang = "de-de";
								break;
							case "fr":
								lang = "fr-fr";
								break;
							case "zh-CN":
								lang = "zh-cn";
								break;
							default:
								lang = "en-us";
								break;
						}
						url = string.Format(OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.global.ToString(), lang);
						break;
					case HI3Server.SEA:
						switch(App.LauncherLanguage)
						{
							case "id":
								lang = "id-id";
								break;
							case "th":
								lang = "th-th";
								break;
							case "vn":
								lang = "vi-vn";
								break;
							case "zh-CN":
								lang = "zh-cn";
								break;
							default:
								lang = "en-us";
								break;
						}
						url = string.Format(OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.os.ToString(), lang);
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
					case HI3Server.JP:
						url = OnlineVersionInfo.game_info.mirror.mihoyo.launcher_content.jp.ToString();
						break;
				}
				Directory.CreateDirectory(App.LauncherBackgroundsPath);
				string background_image_url;
				string background_image_md5;
				var web_request = BpUtility.CreateWebRequest(url, "GET", 30000);
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
								Log("Background image info is missing!", true, 2);
								BackgroundImageDownloading = false;
								return;
							}
						}
						else
						{
							Log($"Failed to fetch background image info: {json.message.ToString()}", true, 2);
							BackgroundImageDownloading = false;
							return;
						}
					}
				}
				string background_image_name = BpUtility.GetFileNameFromUrl(background_image_url);
				string background_image_path = Path.Combine(App.LauncherBackgroundsPath, background_image_name);
				background_image_md5 = background_image_name.Split('_')[0].ToUpper();
				bool Validate()
				{
					if(File.Exists(background_image_path))
					{
						string actual_md5 = BpUtility.CalculateMD5(background_image_path);
						if(actual_md5 != background_image_md5)
						{
							Log($"Background image validation failed. Expected MD5: {background_image_md5}, got MD5: {actual_md5}", true, 2);
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
								throw new CryptographicException("Verification failed");
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
				Log($"Failed to download background image: {ex.Message}", true, 2);
			}
			BackgroundImageDownloading = false;
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
				Path.Combine(path, "崩壊3rd"),
				Path.Combine(path, "붕괴3rd"),
				Path.Combine(path, "Honkai Impact 3rd", "Games"),
				Path.Combine(path, "Honkai Impact 3", "Games"),
				Path.Combine(path, "Honkai Impact 3rd glb", "Games"),
				Path.Combine(path, "Honkai Impact 3 sea", "Games"),
				Path.Combine(path, "Honkai Impact 3rd tw", "Games"),
				Path.Combine(path, "Honkai Impact 3rd kr", "Games"),
				Path.Combine(path, "Houkai3rd", "Games")
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
						case "崩壊3rd":
							// hack to determine whether it's JP or not
							if(path.Contains("Houkai3rd"))
							{
								if(App.LauncherRegKey.GetValue("VersionInfoJP") == null)
								{
									return 5;
								}
							}
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

		private async Task DownloadGameFile()
		{
			try
			{
				string title;
				long size = 0;
				string url;
				string md5;
				bool abort = false;
				if(Mirror == HI3Mirror.miHoYo)
				{
					title = GameArchiveName;
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
				else
				{
					dynamic metadata = null;
					switch(Server)
					{
						case HI3Server.GLB:
							metadata = FetchFileMetadata(OnlineVersionInfo.game_info.mirror.bpnetwork.game_archive.global.ToString());
							break;
						case HI3Server.SEA:
							metadata = FetchFileMetadata(OnlineVersionInfo.game_info.mirror.bpnetwork.game_archive.os.ToString());
							break;
						case HI3Server.CN:
							metadata = FetchFileMetadata(OnlineVersionInfo.game_info.mirror.bpnetwork.game_archive.cn.ToString());
							break;
						case HI3Server.TW:
							metadata = FetchFileMetadata(OnlineVersionInfo.game_info.mirror.bpnetwork.game_archive.tw.ToString());
							break;
						case HI3Server.KR:
							metadata = FetchFileMetadata(OnlineVersionInfo.game_info.mirror.bpnetwork.game_archive.kr.ToString());
							break;
						case HI3Server.JP:
							metadata = FetchFileMetadata(OnlineVersionInfo.game_info.mirror.bpnetwork.game_archive.jp.ToString());
							break;
					}
					if(metadata == null)
					{
						return;
					}
					title = metadata.downloadUrl;
					url = metadata.downloadUrl;
					md5 = miHoYoVersionInfo.game.latest.md5.ToString();
					if((DateTimeOffset)metadata.modifiedDate < (DateTimeOffset)miHoYoVersionInfo.last_modified)
					{
						Status = LauncherStatus.Error;
						Log("The seleted mirror is outdated! Please use HoYoverse mirror for the time being.", true, 1);
						new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_mirror_old_msg"]).ShowDialog();
						Status = LauncherStatus.Ready;
						GameUpdateCheck();
						return;
					}
				}
				md5 = md5.ToUpper();
				GameArchiveTempPath = $"{GameArchivePath}_tmp";
				Status = LauncherStatus.Downloading;
				ProgressBar.IsIndeterminate = true;
				if(File.Exists(GameArchivePath))
				{
					File.Move(GameArchivePath, GameArchiveTempPath);
				}
				if(!File.Exists(GameArchiveTempPath))
				{
					Log($"Starting to download game archive: {title} ({url})");
					try
					{
						using(httpclient = new Http(true, 5, 1000, App.UserAgent))
						{
							httpprop = new HttpProp(url, GameArchiveTempPath);
							token = new CancellationTokenSource();
							httpclient.DownloadProgress += DownloadStatusChanged;
							Dispatcher.Invoke(() =>
							{
								ProgressText.Text = string.Empty;
								ProgressBar.Visibility = Visibility.Collapsed;
								DownloadProgressBarStackPanel.Visibility = Visibility.Visible;
								LaunchButton.IsEnabled = true;
								LaunchButton.Content = App.TextStrings["button_cancel"];
							});
							await AssignAndRunHttpTaskOrThrow(httpclient.Download(httpprop.URL, httpprop.Out, httpprop.Thread, false, token.Token));
							await AssignAndRunHttpTaskOrThrow(httpclient.Merge(token.Token));
							httpclient.DownloadProgress -= DownloadStatusChanged;
							Log("Successfully downloaded game archive");
						}
						Dispatcher.Invoke(() =>
						{
							ProgressText.Text = string.Empty;
							DownloadProgressBarStackPanel.Visibility = Visibility.Collapsed;
							LaunchButton.Content = App.TextStrings["button_launch"];
						});
					}
					catch(OperationCanceledException)
					{
						httpclient.DownloadProgress -= DownloadStatusChanged;
						return;
					}
				}

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
						string actual_md5 = BpUtility.CalculateMD5(GameArchiveTempPath);
						if(actual_md5 == md5)
						{
							if(!File.Exists(GameArchivePath))
							{
								File.Move(GameArchiveTempPath, GameArchivePath);
							}
							else if(File.Exists(GameArchivePath) && size != 0 && new FileInfo(GameArchivePath).Length != size)
							{
								DeleteFile(GameArchivePath);
								File.Move(GameArchiveTempPath, GameArchivePath);
							}
							Log("success!", false);
						}
						else
						{
							Status = LauncherStatus.Error;
							Log($"Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}.\nThis is most likely caused by a corrupted download. Please check your storage device for errors and use a stable Internet connection.", true, 1);
							DeleteFile(GameArchiveTempPath);
							abort = true;
							Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_verify_error_title"], App.TextStrings["msgbox_verify_error_1_msg"]).ShowDialog();});
							Status = LauncherStatus.Ready;
							GameUpdateCheck();
						}
						if(abort)
						{
							return;
						}
						uint skipped_files = 0;
						using(var archive = new SevenZipExtractor(GameArchivePath))
						{
							uint unpacked_count = 0;
							uint total_count = archive.FilesCount;

							Log("Unpacking game archive...");
							Status = LauncherStatus.Unpacking;
						    archive.FileExtractionFinished += (sender, args) =>
							{
								double progress = (unpacked_count + 1f) / total_count;
								unpacked_count++;
								Dispatcher.Invoke(() =>
								{
									DownloadProgressText.Text = string.Format(App.TextStrings["progresstext_unpacking_2"], unpacked_count, total_count, $"{progress * 100:0.00}");
									DownloadProgressBar.Value = progress;
									TaskbarItemInfo.ProgressValue = progress;
								});
							};
							try
							{
								archive.ExtractArchive(GameInstallPath);
							}
							catch(IOException)
							{
								throw;
							}
							catch(Exception ex)
							{
								Log($"Failed to unpack file №{unpacked_count + 1}: {ex.Message}", true, 1);
								skipped_files++;
								total_count--;
							}
						}
						if(skipped_files > 0)
						{
							DeleteFile(GameArchivePath);
							throw new SevenZipArchiveException("Game archive is corrupted, please download again");
						}
						Log("success!", false);
						DeleteFile(GameArchivePath);
						Dispatcher.Invoke(() => 
						{
							PatchDownload = false;
							WriteVersionInfo(false, true);
							Log("Successfully installed the game");
							FlashMainWindow();
							GameUpdateCheck();
						});
					});
				}
				catch(Exception ex)
				{
					Status = LauncherStatus.Error;
					Log($"Failed to install the game:\n{ex}", true, 1);
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
				Log($"Failed to download the game:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_game_download_error_title"], App.TextStrings["msgbox_game_download_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				GameUpdateCheck();
			}
		}

		private void WriteVersionInfo(bool check_for_local_version = false, bool is_installed = false)
		{
			try
			{
				string game_config_ini_file = Path.Combine(GameInstallPath, "config.ini");
				IniData game_config_ini_data = null;
				var ini_parser = new FileIniDataParser();
				if(File.Exists(game_config_ini_file))
				{
					game_config_ini_data = ini_parser.ReadFile(game_config_ini_file);
				}
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
						if(game_config_ini_data["General"]["game_version"] != null)
						{
							version_info.game_info.version = game_config_ini_data["General"]["game_version"];
						}
						else if(game_config_ini_data["general"]["game_version"] != null)
						{
							version_info.game_info.version = game_config_ini_data["general"]["game_version"];
						}
						else
						{
							throw new NullReferenceException();
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
					try
					{
						if(game_config_ini_data == null)
						{
							game_config_ini_data = new IniData();
						}
						game_config_ini_data.Configuration.AssigmentSpacer = string.Empty;
						game_config_ini_data["General"]["game_version"] = version_info.game_info.version;
						ini_parser.WriteFile(game_config_ini_file, game_config_ini_data, new UTF8Encoding(false));
					}
					catch(Exception ex)
					{
						Log($"Failed to write version info to game config.ini: {ex.Message}", true, 2);
					}
					try
					{
						string hyp_registry_path = null;
						switch(Server)
						{
							case HI3Server.CN:
								hyp_registry_path = @"SOFTWARE\miHoYo\HYP\1_1";
								break;
							default:
								hyp_registry_path = @"SOFTWARE\Cognosphere\HYP\1_0";
								break;
						}
						var key = Registry.CurrentUser.OpenSubKey($@"{hyp_registry_path}\{GameInstallRegistryName}", true);
						if(key != null)
						{
							key.SetValue("GameInstallPath", GameInstallPath);
							key.Close();
						}
					}
					catch(Exception ex)
					{
						Log($"Failed to write installation path to HoYoPlay registry: {ex.Message}", true, 2);
					}
				}
				Log("success!", false);
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to write version info:\n{ex}", true, 1);
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
	}
}