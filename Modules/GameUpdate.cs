using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using BetterHI3Launcher.Config;
using BetterHI3Launcher.Utility;
using BetterHI3Launcher.Utility.Json;
using Hi3Helper.Http;
using IniParser;
using IniParser.Model;
using Microsoft.Win32;
using SevenZip;
using JsonSerializerNew = System.Text.Json.JsonSerializer;

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

					if(App.LauncherRegKey.GetValue(RegistryVersionInfo) is byte[] registryLocalVersionInfoBytes)
					{
						LocalVersionInfo = JsonSerializerNew.Deserialize(registryLocalVersionInfoBytes, JsonParseContext.Default.LocalVersionInfo);
						GameInstallPath = LocalVersionInfo.GameInfo?.InstallPath ?? string.Empty;
						if(!string.IsNullOrEmpty(GameInstallPath) &&
						   Path.Combine(GameInstallPath, "config.ini") is var game_config_ini_file &&
						   File.Exists(game_config_ini_file))
						{
							var parser = new FileIniDataParser();
							parser.Parser.Configuration.AllowDuplicateKeys = true;
							parser.Parser.Configuration.AllowDuplicateSections = true;
							parser.Parser.Configuration.CaseInsensitive = true;
							parser.Parser.Configuration.OverrideDuplicateKeys = true;
							var data = parser.ReadFile(game_config_ini_file);
							if(data["General"]["game_version"] != null)
							{
								if(data["General"]["game_version"] == HYPGamePackageData["main"]["major"]["version"].ToString())
								{
									(LocalVersionInfo.GameInfo ??= new GameInfo()).IsInstalled = true;
								}
								(LocalVersionInfo.GameInfo ??= new GameInfo()).Version = new StructVersion(data["General"]["game_version"]);
							}
						}
						var local_game_version = new StructVersion(LocalVersionInfo.GameInfo?.Version.ToString() ?? "0.0.0");
						game_needs_update = GameVersionUpdateCheck(local_game_version);
						GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
						GameExePath = Path.Combine(GameInstallPath, GameExeName);

						Log($"Game version: {local_game_version}");
						Log($"Game directory: {GameInstallPath}");
						if(string.IsNullOrEmpty(GameInstallPath) || new DirectoryInfo(GameInstallPath).Parent == null)
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
								var url = HYPGamePackageData["main"]["patches"][PatchDownloadInt]["game_pkgs"][0]["url"].ToString();
								GameArchiveName = BpUtility.GetFileNameFromUrl(url);
								GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
								PatchDownload = true;
							}
							Log("The game requires an update!");
							Status = LauncherStatus.UpdateAvailable;
						}
						else if(!(LocalVersionInfo.GameInfo?.IsInstalled ?? false))
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
							if(!(LocalVersionInfo.GameInfo?.IsInstalled ?? false))
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
								if(HYPGamePackageData["pre_download"]["major"] != default)
								{
									var path = Path.Combine(GameInstallPath, BpUtility.GetFileNameFromUrl(HYPGamePackageData["pre_download"]["major"]["game_pkgs"][0]["url"].ToString()));
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
							FetchHYPGamePackageData();
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
					App.Starting = false;
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

		private int GameVersionUpdateCheck(StructVersion local_game_version)
		{
			if(LocalVersionInfo != null)
			{
				if(!App.Starting)
				{
					FetchHYPGamePackageData();
				}

				string online_game_version_str = HYPGamePackageData["main"]["major"]["version"].ToString();
				StructVersion online_game_version = new(online_game_version_str);
				if(local_game_version != online_game_version &&
				   local_game_version < online_game_version)
				{
					for(var i = 0; i < HYPGamePackageData["main"]["patches"].Node?.AsArray().Count; i++)
					{
						StructVersion onlineVersion = HYPGamePackageData["main"]["patches"][i]["version"];
						if (onlineVersion == local_game_version)
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
						url = string.Format(OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["launcher_content"]["global"], lang);
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
						url = string.Format(OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["launcher_content"]["os"], lang);
						break;
					case HI3Server.CN:
						url = OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["launcher_content"]["cn"];
						break;
					case HI3Server.TW:
						url = OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["launcher_content"]["tw"];
						break;
					case HI3Server.KR:
						url = OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["launcher_content"]["kr"];
						break;
					case HI3Server.JP:
						url = OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["launcher_content"]["jp"];
						break;
				}
				Directory.CreateDirectory(App.LauncherBackgroundsPath);
				string background_image_url;
				string background_image_md5;
				using(Stream data = Extension.GetHttpStreamResponse(url ?? "", 30000))
				{
					DynamicJson json = DynamicJson.Parse(data);
					if (json["retcode"].ToInt() == 0)
					{
						if(json["data"] != default && json["data"]["game_info_list"] != default && json["data"]["game_info_list"].Node?.AsArray().Count > 0)
						{
							background_image_url = json["data"]["game_info_list"][0]["backgrounds"][0]["background"]["url"];
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
						Log($"Failed to fetch background image info: {json["message"]}", true, 2);
						BackgroundImageDownloading = false;
						return;
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
                            Extension.DownloadFile(background_image_url, background_image_path);
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
				Path.Combine(path, "Honkai Impact 3rd game", "Games"),
				Path.Combine(path, "Honkai Impact 3 game", "Games"),
				Path.Combine(path, "Honkai Impact 3rd asia game", "Games"),
				Path.Combine(path, "Honkai Impact 3rd kr game", "Games"),
				Path.Combine(path, "Houkai3rd game", "Games")
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
					
					if(!PatchDownload)
					{
						url = HYPGamePackageData["main"]["major"]["game_pkgs"][0]["url"];
						md5 = HYPGamePackageData["main"]["major"]["game_pkgs"][0]["md5"];
					}
					else
					{
						url = HYPGamePackageData["main"]["patches"][PatchDownloadInt]["game_pkgs"][0]["url"];
						md5 = HYPGamePackageData["main"]["patches"][PatchDownloadInt]["game_pkgs"][0]["md5"];
					}
				}
				else
				{
					FileMetadata metadata = null;
					DynamicJson game_archive = OnlineVersionInfo["game_info"]["mirror"]["bpnetwork"]["game_archive"];

					switch (Server)
					{
						case HI3Server.GLB:
							metadata = FetchFileMetadata(game_archive["global"]);
							break;
						case HI3Server.SEA:
							metadata = FetchFileMetadata(game_archive["os"]);
							break;
						case HI3Server.CN:
							metadata = FetchFileMetadata(game_archive["cn"]);
							break;
						case HI3Server.TW:
							metadata = FetchFileMetadata(game_archive["tw"]);
							break;
						case HI3Server.KR:
							metadata = FetchFileMetadata(game_archive["kr"]);
							break;
						case HI3Server.JP:
							metadata = FetchFileMetadata(game_archive["jp"]);
							break;
					}
					if(metadata == null)
					{
						return;
					}
					title = metadata.DownloadUrl;
					url = metadata.DownloadUrl;
					md5 = HYPGamePackageData["main"]["major"]["game_pkgs"][0]["md5"].ToString();
					if(metadata.ModifiedDate < HYPGamePackageData["main"]["major"]["game_pkgs"][0]["last_modified"])
					{
						Status = LauncherStatus.Error;
						Log("The selected mirror is outdated! Please use HoYoverse mirror for the time being.", true, 1);
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

					Log("Validating game archive...");
					Status = LauncherStatus.Verifying;
					string actual_md5 = await BpUtility.CalculateMD5Async(GameArchiveTempPath);
					if (actual_md5 == md5)
					{
						if (!File.Exists(GameArchivePath))
						{
							File.Move(GameArchiveTempPath, GameArchivePath);
						}
						else if (File.Exists(GameArchivePath) && size != 0 && new FileInfo(GameArchivePath).Length != size)
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
						Dispatcher.Invoke(() => { new DialogWindow(App.TextStrings["msgbox_verify_error_title"], App.TextStrings["msgbox_verify_error_1_msg"]).ShowDialog(); });
						Status = LauncherStatus.Ready;
						GameUpdateCheck();
					}
					if (abort)
					{
						return;
					}
					uint skipped_files = 0;
					using (var archive = new SevenZipExtractor(GameArchivePath))
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
							await archive.ExtractArchiveAsync(GameInstallPath);
						}
						catch (IOException)
						{
							throw;
						}
						catch (Exception ex)
						{
							Log($"Failed to unpack file №{unpacked_count + 1}: {ex.Message}", true, 1);
							skipped_files++;
							total_count--;
						}
					}
					if (skipped_files > 0)
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
				ini_parser.Parser.Configuration.AllowDuplicateKeys = true;
				ini_parser.Parser.Configuration.AllowDuplicateSections = true;
				ini_parser.Parser.Configuration.CaseInsensitive = true;
				ini_parser.Parser.Configuration.OverrideDuplicateKeys = true;
				if(File.Exists(game_config_ini_file))
				{
					game_config_ini_data = ini_parser.ReadFile(game_config_ini_file);
				}

				LocalVersionInfo version_info = LocalVersionInfo ??= new LocalVersionInfo();
				GameInfo game_info = LocalVersionInfo.GameInfo ??= new GameInfo();

				if(!PatchDownload)
				{
					string verStr = HYPGamePackageData["main"]["major"]["version"].ToString();
					game_info.Version = verStr;
				}
				else
				{
					game_info.Version = LocalVersionInfo.GameInfo.Version.ToString();
				}
				game_info.InstallPath = GameInstallPath;
				game_info.IsInstalled = is_installed;

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
							game_info.Version = game_config_ini_data["General"]["game_version"].AsSpan();
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
							game_info.Version = default;
						}
					}
					if(key != null)
					{
						key.Close();
					}
				}
				Log("Writing game version info...");
				BpUtility.WriteToRegistry(RegistryVersionInfo, Encoding.UTF8.GetBytes(JsonSerializerNew.Serialize(version_info, JsonParseContext.Default.LocalVersionInfo)), RegistryValueKind.Binary);
				if(is_installed)
				{
					try
					{
						if(game_config_ini_data == null)
						{
							game_config_ini_data = new IniData();
						}
						game_config_ini_data.Configuration.AssigmentSpacer = string.Empty;
						game_config_ini_data["General"]["game_version"] = game_info.Version.ToString("N");
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
							case HI3Server.GLB:
								// If this file exists, then it must be the Epic version
								if(File.Exists(Path.Combine(GameInstallPath, "sdk_pkg_version")))
								{
									hyp_registry_path = @"SOFTWARE\Cognosphere\HYP\standalone\1_3\bh3_global\ACQazS79kX";
									break;
								}
								goto default;
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