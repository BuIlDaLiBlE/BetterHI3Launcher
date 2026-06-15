using AssetsTools.NET.Extra;
using BetterHI3Launcher.Utility.Json;
using Hi3Helper.EncTool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;
using BetterHI3Launcher.Utility;

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

		private class CacheDataProperties
		{
			public string N {get; set;}
			public long CS {get; set;}
			public string CRC {get; set;}
			public int DLM {get; set;}
			public CacheType Type {get; set;}
		}

		/*
		 * N			-> Name of the necessary file
		 * CRC			-> Expected MD5 hash of the file
		 * CS			-> Size of the file
		 * IsNecessary	-> The file is necessary on "Updating settings" screen
		 */
		private class CacheDataPropertiesHi3Mirror
		{
			public string N {get; set;}
			public long CS {get; set;}
			public string CRC {get; set;}
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

		private string GetPackageVersion(Stream stream)
		{
			var manager = new AssetsManager();
			var asset_bundle = manager.LoadBundleFile(stream, ".");
			var assets = manager.LoadAssetsFileFromBundle(asset_bundle, 0);
			var asset = assets.table.GetAssetInfo("PackageVersion");
			return manager.GetTypeInstance(assets, asset).GetBaseField().Get("m_Script").GetValue().AsString();
		}

		private async Task<string> CalculateCRCAsync(string path, string hash_salt, CancellationToken token = default)
		{
			byte[] salt = new mhyEncTool(hash_salt, OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["master_key"]).GetSalt();

			using HMACSHA1 hasher = new(salt);
			using FileStream stream = File.OpenRead(path);

			return await hasher.CalculateHashAsyncCore(stream, token);
		}

		private async void DownloadGameCache(string game_language)
		{
			string hash_salt = string.Empty;
			string data_url;

			List<CacheDataPropertiesHi3Mirror> cache_files, bad_files;
			CacheType cache_type;

			try
			{
				DynamicJson game_cache = OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["game_cache"];
				DynamicJson game_cache_info = OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["game_cache_info"];
				switch ((int)Server)
				{
					case 0:
						data_url = game_cache["global"];
						break;
					case 1:
						data_url = game_cache["os"];
						break;
					case 2:
						data_url = game_cache["cn"];
						break;
					case 3:
						data_url = game_cache["tw"];
						break;
					case 4:
						data_url = game_cache["kr"];
						break;
					case 5:
						data_url = game_cache["jp"];
						break;
					default:
						throw new NotSupportedException("This server is not supported.");
				}

				cache_files = new List<CacheDataPropertiesHi3Mirror>();
				bad_files = new List<CacheDataPropertiesHi3Mirror>();

				for (int i = 0; i < 3; i++)
				{
					// Classify data type as per i
					// 0 or _	: Data and AI/Btree cache
					// 1		: Resources/Event cache
					// 2		: Btree/Ai cache
					switch (i)
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

					string data_info_url;

					switch ((int)Server)
					{
						case 0:
							data_info_url = game_cache_info["global"][i].ToString();
							break;
						case 1:
							data_info_url = game_cache_info["os"][i].ToString();
							break;
						case 2:
							data_info_url = game_cache_info["cn"][i].ToString();
							break;
						case 3:
							data_info_url = game_cache_info["tw"][i].ToString();
							break;
						case 4:
							data_info_url = game_cache_info["kr"][i].ToString();
							break;
						case 5:
							data_info_url = game_cache_info["jp"][i].ToString();
							break;
						default:
							throw new NotSupportedException("This server is not supported.");
					}

					using var remoteStream = await Extension.GetHttpStreamResponseAsync(data_info_url);
					using var stream = new MemoryStream();
					using var xor_stream = new XORStream(stream);
					await remoteStream.CopyToAsync(stream);
					stream.Position = 0;

					var data_lines = GetPackageVersion(xor_stream).Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
					var data_entries = new List<DynamicJson>();
					foreach (string line in data_lines)
					{
						if (line.StartsWith("{") && line.EndsWith("}"))
						{
							var json = DynamicJson.Parse(line);
							data_entries.Add(json);
						}
					}

					if (cache_type == CacheType.Data) hash_salt = data_lines.FirstOrDefault();

					foreach (DynamicJson file in data_entries)
					{
						string filename = file["N"];
						if (FilterRegion(filename, game_language) > 0)
						{
							cache_files.Add(new CacheDataPropertiesHi3Mirror
							{
								N = filename,
								CRC = file["CRC"],
								CS = file["CS"],
								IsNecessary = file["DLM"].ToInt() == 1,
								Type = cache_type
							});
						}
					}
				}
				Log("success!", false);
			}
			catch(WebException ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to fetch cache data:\n{ex}", true, 1);
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

                // Run in parallel (CPU goes boom)
                await cache_files.Index().ParallelForeachAsync(CacheFileCheckWorkerAsync);

				async ValueTask CacheFileCheckWorkerAsync((int Index, CacheDataPropertiesHi3Mirror Item) ctx, CancellationToken localToken)
                {
                    string path;
                    CacheDataPropertiesHi3Mirror cacheFile = ctx.Item;
					int index = ctx.Index;

					string name = $"{NormalizePath(cacheFile.N)}_{cacheFile.CRC}.unity3d";

					// Combine Path and assign their own path
					// If none of them assigned as Unknown type, throw an exception.
					switch (cacheFile.Type)
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

					Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = string.Format(App.TextStrings["progresstext_verifying_file"], index + 1, cache_files.Count);
                        float progress = (index + 1f) / cache_files.Count;
                        ProgressBar.Value = progress;
                        TaskbarItemInfo.ProgressValue = progress;
                    });

					if (File.Exists(path))
					{
						if (await CalculateCRCAsync(path, hash_salt, localToken) == cacheFile.CRC)
						{
							if (App.AdvancedFeatures) Log($"File OK: {path}");
						}
						else
						{
                            lock (bad_files)
                            {
                                bad_files.Add(cacheFile);
                            }
							Log($"File corrupted: {path}");
						}

                        lock (useless_files)
                        {
                            useless_files.RemoveAll(x => x.FullName == path);
                        }
					}
					else
					{
						if (cacheFile.IsNecessary)
                        {
                            lock (bad_files)
                            {
                                bad_files.Add(cacheFile);
                            }
							Log($"File missing: {path}");
						}
					}
				}

				foreach (var useless_file in useless_files)
				{
					Log($"Useless file: {useless_file.FullName}");
				}

				bad_files_size = bad_files.Sum(x => x.CS);

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
					FlashMainWindow();
					if(new DialogWindow(App.TextStrings["contextmenu_download_cache"], string.Format(App.TextStrings["msgbox_repair_3_msg"], bad_files.Count, BpUtility.ToBytesCount(bad_files_size)), DialogWindow.DialogType.Question).ShowDialog() == true)
					{
						int downloaded_files = 0;

						Status = LauncherStatus.Working;
						LaunchButton.IsEnabled = true;
						LaunchButton.Content = App.TextStrings["button_cancel"];
						ProgressBar.IsIndeterminate = false;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

                        using CancellationTokenSource cts = new();

                        // Run in parallel (CPU goes boom)
                        try
                        {
                            await cache_files.Index().ParallelForeachAsync(CacheFileRepairWorkerAsync, token: cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        async ValueTask CacheFileRepairWorkerAsync(
                            (int Index, CacheDataPropertiesHi3Mirror Item) ctx,
                            CancellationToken localToken)
                        {
                            CacheDataPropertiesHi3Mirror cacheFile = ctx.Item;
                            int index = ctx.Index;

                            if (cts.IsCancellationRequested)
                            {
                                return;
                            }

                            if (Volatile.Read(ref ActionAbort))
                            {
                                if (!cts.IsCancellationRequested)
                                {
									cts.Cancel();
                                }

                                Log("Task cancelled");
								Volatile.Write(ref ActionAbort, false);
                                return;
                            }

                            string path = $"{NormalizePath(cacheFile.N)}_{cacheFile.CRC}.unity3d";
                            switch (cacheFile.Type)
                            {
                                case CacheType.Data:
                                    path = Path.Combine(GameCachePath, "Data", path);
                                    break;
                                case CacheType.Ai:
                                case CacheType.Event:
                                    path = Path.Combine(GameCachePath, "Resources", path);
                                    break;
                            }

                            string url = string.Format(data_url, ReturnCacheTypeEnum(cacheFile.Type), $"{cacheFile.N}_{cacheFile.CRC}");
                            Dispatcher.Invoke(() =>
                            {
                                ProgressText.Text = string.Format(App.TextStrings["progresstext_downloading_file"], index + 1, bad_files.Count);
                                var progress = (index + 1f) / bad_files.Count;
                                ProgressBar.Value = progress;
                                TaskbarItemInfo.ProgressValue = progress;
                            });

                            try
                            {
                                await Extension.DownloadFileAsync(url, path, token: localToken);
                                var md5 = await CalculateCRCAsync(path, hash_salt, localToken);
                                if (File.Exists(path) && md5 != cacheFile.CRC)
                                {
                                    throw new CryptographicException("Verification failed");
                                }

                                Log($"Downloading from {url}...success!");
                                Interlocked.Increment(ref downloaded_files);
                            }
                            catch (OperationCanceledException)
                            {
								// ignore
                            }
                            catch (Exception ex)
                            {
                                Log($"Failed to download file [{cacheFile.N}_{cacheFile.CRC}] ({url}): {ex.Message}", true, 1);
                            }
                        }

						Dispatcher.Invoke(() =>
						{
							LaunchButton.Content = App.TextStrings["button_launch"];
							ProgressText.Text = string.Empty;
							ProgressBar.Visibility = Visibility.Collapsed;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
						});

						FlashMainWindow();
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
					FlashMainWindow();
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