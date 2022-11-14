using Newtonsoft.Json;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;

namespace BetterHI3Launcher
{
	public partial class MainWindow
	{
		private readonly string[] CacheRegionalCheckName = new string[]{"sprite"};
		private enum CacheType {Data, Event, Ai, Unknown}
		private string ReturnCacheTypeEnum(CacheType enumName)
		{
			switch(enumName)
			{
				case CacheType.Ai:
					return "ai";
				case CacheType.Data:
					return "data";
				case CacheType.Event:
					return "event";
				default:
					throw new Exception("Unknown cache file data type");
			}
		}

		/*
		 * N			-> Name of the necessary file
		 * CRC			-> Expected MD5 hash of the file
		 * CS			-> Size of the file
		 * IsNecessary	-> The file is necessary on "Updating settings" screen
		 */
		private class CacheDataProperties
		{
			public string N {get; set;}
			public string CRC {get; set;}
			public long CS {get; set;}
			public bool IsNecessary {get; set;}
			public CacheType Type {get; set;}
		}

		/* Filter Region Type of Cache File
		 * 0 -> the file is a regional file but outside user region.
		 * 1 -> the file is a regional file but inside user region and downloadable.
		 * 2 -> the file is not a regional file and downloadable.
		 */
		private byte FilterRegion(string input, string regionName)
		{
			foreach(string word in CacheRegionalCheckName)
			{
				if(input.Contains(word))
				{
					if(input.Contains($"{word}_{regionName}"))
					{
						return 1;
					}
					else
					{
						return 0;
					}
				}
			}
			return 2;
		}

		// Normalize Unix path (/) to Windows path (\)
		private string NormalizePath(string i) => i.Replace('/', '\\');

		private async void DownloadGameCache(string game_language)
		{
			string data;
			string path;
			string data_url = OnlineVersionInfo.game_info.mirror.hi3mirror.game_cache.ToString();
			string hi3mirror_api_url = OnlineVersionInfo.game_info.mirror.hi3mirror.api.ToString();

			List<CacheDataProperties> cache_files, bad_files;
			CacheType cache_type;
			var web_client = new BpWebClient();

			try
			{
				int server;
				switch((int)Server)
				{
					case 0:
						server = 1;
						break;
					case 1:
						server = 0;
						break;
					case 2:
						server = 2;
						break;
					default:
						throw new NotSupportedException("This server is not supported.");
				}

				cache_files = new List<CacheDataProperties>();
				bad_files = new List<CacheDataProperties>();

				await Task.Run(() =>
				{
					for(int i = 0; i < 3; i++)
					{
						// Classify data type as per i
						// 0 or _	: Data and AI/Btree cache
						// 1		: Resources/Event cache
						// 2		: Btree/Ai cache
						switch(i)
						{
							case 0:
								cache_type = CacheType.Data;
								break;
							case 1:
								cache_type = CacheType.Event;
								break;
							case 2:
								cache_type = CacheType.Ai;
								break;
							default:
								cache_type = CacheType.Unknown;
								break;
						}

						// Get URL and API data
						var url = string.Format(hi3mirror_api_url, i, server);
						data = web_client.DownloadString(url);

						// Do Elimination Process
						// Deserialize string and make it to Object as List<CacheDataProperties>
						foreach(CacheDataProperties file in JsonConvert.DeserializeObject<List<CacheDataProperties>>(data))
						{
							// Do check whenever the file is included regional language as game_language defined
							// Then add it to cache_files list
							if(FilterRegion(file.N, game_language) > 0)
							{
								// Do add if the Filter passed.
								cache_files.Add(new CacheDataProperties
								{
									N = file.N,
									CRC = file.CRC,
									CS = file.CS,
									IsNecessary = file.IsNecessary,
									Type = cache_type
								});
							}
						}
					}
				});
				Log("success!", false);
			}
			catch(WebException ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to connect to Hi3Mirror:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_net_error_msg"], ex.Message)).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}

			try
			{
				Directory.CreateDirectory(GameCachePath);
				var existing_files = new DirectoryInfo(GameCachePath).GetFiles("*", SearchOption.AllDirectories).Where(x => x.DirectoryName.Contains(@"Data\data") || x.DirectoryName.Contains("Resources")).ToList();
				var useless_files = existing_files;
				long bad_files_size = 0;
				long empty_directories_count = 0;

				Status = LauncherStatus.Working;
				OptionsButton.IsEnabled = true;
				ProgressBar.IsIndeterminate = false;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
				Log("Verifying game cache...");
				await Task.Run(() =>
				{
					for(int i = 0; i < cache_files.Count; i++)
					{
						var name = $"{NormalizePath(cache_files[i].N)}.unity3d";

						// Combine Path and assign their own path
						// If none of them assigned as Unknown type, throw an exception.
						switch(cache_files[i].Type)
						{
							case CacheType.Data:
								path = Path.Combine(GameCachePath, "Data", name);
								break;
							case CacheType.Ai:
							case CacheType.Event:
								path = Path.Combine(GameCachePath, "Resources", name);
								break;
							default:
								throw new Exception("Unknown cache file data type");
						}
						var file = new FileInfo(path);

						Dispatcher.Invoke(() =>
						{
							ProgressText.Text = string.Format(App.TextStrings["progresstext_verifying_file"], i + 1, cache_files.Count);
							var progress = (i + 1f) / cache_files.Count;
							ProgressBar.Value = progress;
							TaskbarItemInfo.ProgressValue = progress;
						});

						if(file.Exists)
						{
							if(BpUtility.CalculateMD5(file.FullName) == cache_files[i].CRC)
							{
								if(App.AdvancedFeatures) Log($"File OK: {path}");
							}
							else
							{
								bad_files.Add(cache_files[i]);
								Log($"File corrupted: {path}");
							}
							useless_files.RemoveAll(x => x.FullName == path);
						}
						else
						{
							if(cache_files[i].IsNecessary)
							{
								bad_files.Add(cache_files[i]);
								Log($"File missing: {path}");
							}
						}
					}

					foreach(var useless_file in useless_files)
					{
						Log($"Useless file: {useless_file.FullName}");
					}

					bad_files_size = bad_files.Sum(x => x.CS);
				});

				ProgressText.Text = string.Empty;
				ProgressBar.Visibility = Visibility.Collapsed;
				ProgressBar.Value = 0;
				TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
				TaskbarItemInfo.ProgressValue = 0;
				WindowState = WindowState.Normal;

				if(useless_files.Count > 0)
				{
					foreach(var file in useless_files)
					{
						DeleteFile(file.FullName, true);
					}
					Log($"Deleted {useless_files.Count} useless files");
				}

				foreach(var dir in Directory.GetDirectories(GameCachePath, "*", SearchOption.AllDirectories).Reverse())
				{
					if(dir.Contains("Crashes"))
					{
						continue;
					}
					try
					{
						if(Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length == 0)
						{
							Log($"Empty directory: {dir}");
							Directory.Delete(dir);
							empty_directories_count++;
						}
					}catch{}
				}
				if(empty_directories_count > 0)
				{
					Log($"Deleted {empty_directories_count} empty directories");
				}

				if(bad_files.Count > 0)
				{
					Log($"Finished verifying files, found corrupted/missing files: {bad_files.Count}");
					if(new DialogWindow(App.TextStrings["contextmenu_download_cache"], string.Format(App.TextStrings["msgbox_repair_3_msg"], bad_files.Count, BpUtility.ToBytesCount(bad_files_size)), DialogWindow.DialogType.Question).ShowDialog() == true)
					{
						string server;
						switch((int)Server)
						{
							case 0:
								server = "global";
								if(Mirror == HI3Mirror.miHoYo) data_url = OnlineVersionInfo.game_info.mirror.mihoyo.game_cache.global.ToString();
								break;
							case 1:
								server = "sea";
								if(Mirror == HI3Mirror.miHoYo) data_url = OnlineVersionInfo.game_info.mirror.mihoyo.game_cache.os.ToString();
								break;
							case 2:
								server = "cn";
								if(Mirror == HI3Mirror.miHoYo) data_url = OnlineVersionInfo.game_info.mirror.mihoyo.game_cache.cn.ToString();
								break;
							default:
								throw new NotSupportedException("This server is not supported.");
						}

						int downloaded_files = 0;
						Status = LauncherStatus.Downloading;

						await Task.Run(async () =>
						{
							for(int i = 0; i < bad_files.Count; i++)
							{
								path = $"{NormalizePath(bad_files[i].N)}.unity3d";
								switch(bad_files[i].Type)
								{
									case CacheType.Data:
										path = Path.Combine(GameCachePath, "Data", path);
										break;
									case CacheType.Ai:
									case CacheType.Event:
										path = Path.Combine(GameCachePath, "Resources", path);
										break;
								}

								var url = string.Format(data_url, server, ReturnCacheTypeEnum(bad_files[i].Type), bad_files[i].N);
								if(Mirror == HI3Mirror.miHoYo) url = string.Format(data_url, ReturnCacheTypeEnum(bad_files[i].Type), bad_files[i].N);
								Log($"Downloading from {url}...");
								Dispatcher.Invoke(() =>
								{
									ProgressText.Text = string.Format(App.TextStrings["progresstext_downloading_file"], i + 1, bad_files.Count);
									var progress = (i + 1f) / bad_files.Count;
									ProgressBar.Value = progress;
									TaskbarItemInfo.ProgressValue = progress;
								});

								try
								{
									Directory.CreateDirectory(Path.GetDirectoryName(path));
									await web_client.DownloadFileTaskAsync(new Uri(url), path);
									var md5 = BpUtility.CalculateMD5(path);
									if(File.Exists(path) && md5 != bad_files[i].CRC)
									{
										throw new CryptographicException("Verification failed");
									}
									else
									{
										Log("success!", false);
										downloaded_files++;
									}
								}
								catch(Exception ex)
								{
									Log($"Failed to download file [{bad_files[i].N}] ({url}): {ex.Message}", true, 1);
								}
							}
						});

						Dispatcher.Invoke(() =>
						{
							LaunchButton.Content = App.TextStrings["button_launch"];
							ProgressText.Text = string.Empty;
							ProgressBar.Visibility = Visibility.Collapsed;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
						});

						if(downloaded_files == bad_files.Count)
						{
							Log($"Successfully downloaded {downloaded_files} file(s)");
							Dispatcher.Invoke(() =>
							{
								new DialogWindow(App.TextStrings["contextmenu_download_cache"], string.Format(App.TextStrings["msgbox_repair_4_msg"], downloaded_files)).ShowDialog();
							});
						}
						else
						{
							int skipped_files = bad_files.Count - downloaded_files;
							if(downloaded_files > 0)
							{
								Log($"Successfully downloaded {downloaded_files} files, failed to download {skipped_files} files");
							}
							
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
						ProgressBar.Visibility = Visibility.Collapsed;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
					});
					new DialogWindow(App.TextStrings["contextmenu_download_cache"], App.TextStrings["msgbox_repair_2_msg"]).ShowDialog();
				}
				Status = LauncherStatus.Ready;

			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
			}
		}
	}
}