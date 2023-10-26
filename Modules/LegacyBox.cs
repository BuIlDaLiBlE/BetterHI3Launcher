using Microsoft.Win32;
using Newtonsoft.Json;
using PartialZip;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace BetterHI3Launcher
{
	public partial class MainWindow
	{
		private void FPSInputBoxTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			e.Handled = !e.Text.Any(x => char.IsDigit(x));
		}

		// https://stackoverflow.com/q/1268552
		private void FPSInputBoxTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
		{
			bool IsTextAllowed(string text)
			{
				return Array.TrueForAll(text.ToCharArray(), delegate (char c) {return char.IsDigit(c) || char.IsControl(c);});
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
			FetchAnnouncements();
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
					if(App.AdvancedFeatures) Log($"Repair data game version: {OnlineRepairInfo.game_version}");
					await Task.Run(() =>
					{
						for(int i = 0; i < OnlineRepairInfo.files.names.Count; i++)
						{
							string name = OnlineRepairInfo.files.names[i].ToString().Replace("/", "\\");
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
								if(App.AdvancedFeatures) Log($"File OK: {name}");
							}
						}
					});
					ProgressText.Text = string.Empty;
					ProgressBar.Visibility = Visibility.Collapsed;
					ProgressBar.Value = 0;
					TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
					TaskbarItemInfo.ProgressValue = 0;
					WindowState = WindowState.Normal;
					if(corrupted_files.Count > 0)
					{
						Log($"Finished verifying files, found corrupted/missing files: {corrupted_files.Count}");
						FlashMainWindow();
						if(new DialogWindow(App.TextStrings["contextmenu_repair"], string.Format(App.TextStrings["msgbox_repair_3_msg"], corrupted_files.Count, BpUtility.ToBytesCount(corrupted_files_size)), DialogWindow.DialogType.Question).ShowDialog() == true)
						{
							string[] urls = OnlineRepairInfo.zip_urls.ToObject<string[]>();
							int repaired_files = 0;
							bool abort = false;

							Status = LauncherStatus.Working;
							LaunchButton.IsEnabled = true;
							LaunchButton.Content = App.TextStrings["button_cancel"];
							ProgressBar.IsIndeterminate = false;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

							await Task.Run(async () =>
							{
								if(urls.Length == 0)
								{
									throw new InvalidOperationException("No download URLs are present in repair data.");
								}
								for(int i = 0; i < corrupted_files.Count; i++)
								{
									string path = Path.Combine(GameInstallPath, corrupted_files[i]);

									if(ActionAbort)
									{
										Log("Task cancelled");
										ActionAbort = false;
										break;
									}
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
												var metadata = FetchFileMetadata(urls[j]);
												url = metadata.downloadUrl.ToString();
											}
											else
											{
												url = urls[j];
											}

											Directory.CreateDirectory(Path.GetDirectoryName(path));
											await PartialZipDownloader.DownloadFile(url, corrupted_files[i], path);
											Dispatcher.Invoke(() => {ProgressText.Text = string.Format(App.TextStrings["progresstext_verifying_file"], i + 1, corrupted_files.Count);});
											if(!File.Exists(path) || BpUtility.CalculateMD5(path) != corrupted_file_hashes[i])
											{
												Log($"Failed to repair file {corrupted_files[i]}", true, 1);
											}
											else
											{
												Log($"Repaired file {corrupted_files[i]}");
												repaired_files++;
											}
											break;
										}
										catch(Exception ex)
										{
											if(j == urls.Length - 1)
											{
												Status = LauncherStatus.Error;
												Log($"Failed to download file [{corrupted_files[i]}] ({url}): {ex.Message}\nNo more mirrors available!", true, 1);
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
												Log($"Failed to download file [{corrupted_files[i]}] ({url}): {ex.Message}\nAttempting to download from another mirror...", true, 2);
											}
										}
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
							if(!abort)
							{
								FlashMainWindow();
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
							ProgressBar.Visibility = Visibility.Collapsed;
							TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
							FlashMainWindow();
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
				Log($"{ex}", true, 1);
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
						x.Extension != ".bat" &&
						x.Extension != ".dmp" &&
						x.Extension != ".log" &&
						x.Extension != ".zip" &&
						x.Extension != ".7z" &&
						!x.Extension.Contains("_tmp") &&
						x.Name != "ACE-BASE.sys" &&
						x.Name != "blockVerifiedVersion.txt" &&
						x.Name != "config.ini" &&
						x.Name != "manifest.m" &&
						x.Name != "sdk_pkg_version" &&
						x.Name != "UniFairy.sys" &&
						x.Name != "Version.txt" &&
						!x.Name.Contains("AUDIO_Avatar") &&
						!x.Name.Contains("AUDIO_BGM") &&
						!x.Name.Contains("AUDIO_Dialog") &&
						!x.Name.Contains("AUDIO_DLC") &&
						!x.Name.Contains("AUDIO_Event") &&
						!x.Name.Contains("AUDIO_Ex") &&
						!x.Name.Contains("AUDIO_HOT_FIX") &&
						!x.Name.Contains("AUDIO_Main") &&
						!x.Name.Contains("AUDIO_Story") &&
						!x.Name.Contains("AUDIO_Vanilla") &&
						!x.Name.Contains("Blocks_") &&
						!x.Name.Contains("EOSSDK") &&
						!x.DirectoryName.Contains("Cache") &&
						!x.DirectoryName.Contains("Predownload") &&
						!x.DirectoryName.Contains("ScreenShot") &&
						!x.DirectoryName.Contains("ThirdPartyNotice") &&
						!x.DirectoryName.Contains("Video")
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
								json.repair_info.files.names[i] = files[i].FullName.Replace($"{GameInstallPath}\\", string.Empty).Replace("\\", "/");
								json.repair_info.files.hashes[i] = BpUtility.CalculateMD5(files[i].FullName);
								json.repair_info.files.sizes[i] = files[i].Length;
								Dispatcher.Invoke(() =>
								{
									ProgressText.Text = string.Format(App.TextStrings["progresstext_generating_hash"], i + 1, files.Count);
									var progress = (i + 1f) / files.Count;
									ProgressBar.Value = progress;
									TaskbarItemInfo.ProgressValue = progress;
								});
								Log($"Added: {json.repair_info.files.names[i]}");
							}
							File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(json));
							Log($"Saved JSON: {dialog.FileName}");
						});
						ProgressText.Text = string.Empty;
						ProgressBar.Visibility = Visibility.Collapsed;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
						FlashMainWindow();
						if(new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_repair_7_msg"], DialogWindow.DialogType.Question).ShowDialog() == true)
						{
							ProgressBar.Visibility = Visibility.Visible;
							await Task.Run(() =>
							{
								Log("Zipping, this will take a while...");
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
						Log($"{ex}", true, 1);
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
				if(fps_combat < 30 || fps_menu < 30)
				{
					if(new DialogWindow(App.TextStrings["contextmenu_custom_fps"], App.TextStrings["msgbox_custom_fps_3_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						return;
					}
				}
				Log($"Setting in-game FPS to {fps_combat}, menu FPS to {fps_menu}...");
				GameGraphicSettings.TargetFrameRateForInLevel = fps_combat;
				GameGraphicSettings.TargetFrameRateForOthers = fps_menu;
				var value_after = Encoding.UTF8.GetBytes($"{JsonConvert.SerializeObject(GameGraphicSettings)}\0");
				var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
				key.SetValue("GENERAL_DATA_V2_PersonalGraphicsSettingV2_h3480068519", value_after, RegistryValueKind.Binary);
				key.Close();
				FPSInputBox.Visibility = Visibility.Collapsed;
				LegacyBoxActive = false;
				Log("success!", false);
				new DialogWindow(App.TextStrings["contextmenu_custom_fps"], string.Format(App.TextStrings["msgbox_custom_fps_4_msg"], fps_combat, fps_menu)).ShowDialog();
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private void FPSInputBoxCancelButton_Click(object sender, RoutedEventArgs e)
		{
			FPSInputBox.Visibility = Visibility.Collapsed;
			LegacyBoxActive = false;
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
				if(height > width)
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
				LegacyBoxActive = false;
				Log("success!", false);
				new DialogWindow(App.TextStrings["contextmenu_custom_resolution"], string.Format(App.TextStrings["msgbox_custom_resolution_2_msg"], width, height, is_fullscreen)).ShowDialog();
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_generic_error_title"], App.TextStrings["msgbox_generic_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private void ResolutionInputBoxCancelButton_Click(object sender, RoutedEventArgs e)
		{
			ResolutionInputBox.Visibility = Visibility.Collapsed;
			LegacyBoxActive = false;
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

		private void AnnouncementBoxCloseButton_Click(object sender, RoutedEventArgs e)
		{
			LegacyBoxActive = false;
			AnnouncementBox.Visibility = Visibility.Collapsed;
			bool do_not_show_next_time = (bool)AnnouncementBoxDoNotShowCheckbox.IsChecked;
			if(do_not_show_next_time)
			{
				try
				{
					App.SeenAnnouncements.Add(App.Announcements.First["id"].ToString());
					BpUtility.WriteToRegistry("SeenAnnouncements", string.Join(",", App.SeenAnnouncements), RegistryValueKind.String);
				}
				catch(Exception ex)
				{
					Log($"Failed to write value with key SeenAnnouncements to registry:\n{ex}", true, 1);
				}
			}
			AnnouncementBoxDoNotShowCheckbox.IsChecked = false;
			App.Announcements.Remove(App.Announcements.First);
			if(App.Announcements.Count > 0)
			{
				ShowAnnouncement(App.Announcements.First);
			}
			else
			{
				LauncherLocalVersionCheck();
			}
		}
	}
}