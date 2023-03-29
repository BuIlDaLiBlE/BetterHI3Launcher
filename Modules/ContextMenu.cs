using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace BetterHI3Launcher
{
	public partial class MainWindow
	{
		private void CM_Screenshots_Click(object sender, RoutedEventArgs e)
		{
			var path = $@"{GameInstallPath}\ScreenShot";
			if(Directory.Exists(path))
			{
				BpUtility.StartProcess(path, null, GameInstallPath, true);
			}
			else
			{
				new DialogWindow(App.TextStrings["contextmenu_open_screenshots_folder"], App.TextStrings["msgbox_no_screenshot_dir_msg"]).ShowDialog();
			}
		}

		private void CM_DownloadCache_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			if(LegacyBoxActive)
			{
				return;
			}
			if(Mirror == HI3Mirror.Hi3Mirror && Server != HI3Server.GLB && Server != HI3Server.SEA)
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
				game_language_int = 0;
			}
			else
			{
				game_language_int = (int)key.GetValue(value);
			}
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

			string dialog_message;
			if(Mirror == HI3Mirror.miHoYo)
			{
				dialog_message = App.TextStrings["msgbox_download_cache_msg"];
			}
			else
			{
				dialog_message = string.Format(App.TextStrings["msgbox_download_cache_hi3mirror_msg"], OnlineVersionInfo.game_info.mirror.hi3mirror.maintainer.ToString());
			}
			if(new DialogWindow(App.TextStrings["contextmenu_download_cache"], dialog_message, DialogWindow.DialogType.Question).ShowDialog() == false)
			{
				return;
			}
			Status = LauncherStatus.CheckingUpdates;
			Dispatcher.Invoke(() => {ProgressText.Text = App.TextStrings["progresstext_fetching_hashes"];});
			Log("Fetching cache data...");
			DownloadGameCache(game_language);
		}

		private async Task CM_Repair_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			if(LegacyBoxActive)
			{
				return;
			}
			if(Server != HI3Server.GLB && Server != HI3Server.SEA)
			{
				new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_feature_not_available_msg"]).ShowDialog();
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
					OnlineRepairInfo = JsonConvert.DeserializeObject<dynamic>(web_client.DownloadString($"{OnlineVersionInfo.launcher_info.repair_url.ToString()}={server}"));
				});
				if(OnlineRepairInfo.status == "success")
				{
					Log("success!", false);
					OnlineRepairInfo = OnlineRepairInfo.repair_info;
					if(OnlineRepairInfo.game_version != LocalVersionInfo.game_info.version && !App.AdvancedFeatures)
					{
						ProgressText.Text = string.Empty;
						ProgressBar.Visibility = Visibility.Collapsed;
						FlashMainWindow();
						new DialogWindow(App.TextStrings["contextmenu_repair"], App.TextStrings["msgbox_repair_1_msg"]).ShowDialog();
					}
					else
					{
						Dispatcher.Invoke(() =>
						{
							RepairBox.Visibility = Visibility.Visible;
							RepairBoxMessageTextBlock.Text = string.Format(App.TextStrings["repairbox_msg"], OnlineRepairInfo.mirrors, OnlineVersionInfo.game_info.mirror.maintainer.ToString());
						});
						LegacyBoxActive = true;
					}
				}
				else
				{
					Status = LauncherStatus.Error;
					Log($"Failed to fetch repair data: {OnlineRepairInfo.status_message}", true, 1);
					new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_net_error_msg"], OnlineRepairInfo.status_message)).ShowDialog();
				}
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to fetch repair data:\n{ex}", true, 1);
				Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_net_error_msg"], ex.Message)).ShowDialog();});
			}
			Status = LauncherStatus.Ready;
		}

		private async Task CM_Move_Click(object sender, RoutedEventArgs e)
		{
			if(Status != LauncherStatus.Ready)
			{
				return;
			}
			if(LegacyBoxActive)
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
				bool is_destination_drive_the_same = Directory.GetDirectoryRoot(GameInstallPath) == Directory.GetDirectoryRoot(path);
				if($@"{path}\".Contains($@"{GameInstallPath}\"))
				{
					new DialogWindow(App.TextStrings["msgbox_move_error_title"], App.TextStrings["msgbox_move_3_msg"]).ShowDialog();
					continue;
				}
				var game_move_to_drive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(path) && x.IsReady).FirstOrDefault();
				if(game_move_to_drive == null)
				{
					new DialogWindow(App.TextStrings["msgbox_move_error_title"], App.TextStrings["msgbox_move_wrong_drive_type_msg"]).ShowDialog();
					continue;
				}
				if(!is_destination_drive_the_same && game_move_to_drive.TotalFreeSpace < new DirectoryInfo(GameInstallPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(x => x.Length))
				{
					if(new DialogWindow(App.TextStrings["msgbox_move_title"], App.TextStrings["msgbox_move_little_space_msg"], DialogWindow.DialogType.Question).ShowDialog() == false)
					{
						continue;
					}
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
				if(new DialogWindow(App.TextStrings["msgbox_move_title"], string.Format(App.TextStrings["msgbox_move_2_msg"], path), DialogWindow.DialogType.Question).ShowDialog() == false)
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
						if(is_destination_drive_the_same)
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
								Log($"Failed to delete old game directory, you may want to do it manually: {GameInstallPath}", true, 2);
							}
						}
						GameInstallPath = path;
						WriteVersionInfo(false, true);
						Log("Successfully moved game files");
						Dispatcher.Invoke(() => {FlashMainWindow();});
						GameUpdateCheck();
					}
					catch(Exception ex)
					{
						Status = LauncherStatus.Error;
						Log($"Failed to move the game:\n{ex}", true, 1);
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
			if(LegacyBoxActive)
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
						ProgressBar.Visibility = Visibility.Collapsed;
						TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
						WindowState = WindowState.Normal;
						FlashMainWindow();
						new DialogWindow(App.TextStrings["msgbox_uninstall_title"], App.TextStrings["msgbox_uninstall_6_msg"] + delete_list).ShowDialog();
					});
					GameUpdateCheck();
				});
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to uninstall the game:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_uninstall_error_title"], App.TextStrings["msgbox_uninstall_error_msg"]).ShowDialog();
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
				string value = "GENERAL_DATA_V2_PersonalGraphicsSettingV2_h3480068519";
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
				LegacyBoxActive = true;
			}
			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to access registry:\n{ex}", true, 1);
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
				LegacyBoxActive = true;
			}

			catch(Exception ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to access registry:\n{ex}", true, 1);
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
				Log($"Failed to access registry:\n{ex}", true, 1);
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
				Log($"Failed to access registry:\n{ex}", true, 1);
				new DialogWindow(App.TextStrings["msgbox_registry_error_title"], App.TextStrings["msgbox_registry_error_msg"]).ShowDialog();
				Status = LauncherStatus.Ready;
				return;
			}
		}

		private void CM_Changelog_Click(object sender, RoutedEventArgs e)
		{
			if(LegacyBoxActive)
			{
				return;
			}

			LegacyBoxActive = true;
			ChangelogBox.Visibility = Visibility.Visible;
			ChangelogBoxScrollViewer.ScrollToHome();
			FetchChangelog();
		}

		private void CM_CustomBackground_Click(object sender, RoutedEventArgs e)
		{
			if(LegacyBoxActive)
			{
				return;
			}

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
						if(dialog.FileName.Contains(App.LauncherBackgroundsPath))
						{
							new DialogWindow(App.TextStrings["contextmenu_custom_background"], string.Format(App.TextStrings["msgbox_custom_background_4_msg"], BpUtility.ToBytesCount(file_size_limit))).ShowDialog();
							continue;
						}
						if(file_size > file_size_limit)
						{
							new DialogWindow(App.TextStrings["contextmenu_custom_background"], string.Format(App.TextStrings["msgbox_custom_background_5_msg"], BpUtility.ToBytesCount(file_size_limit))).ShowDialog();
							continue;
						}
						if(launcher_data_drive.TotalFreeSpace < file_size)
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
				Log($"Failed to set custom background:\n{ex}", true, 1);
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
				Log($"Failed to write value with key Sounds to registry:\n{ex}", true, 1);
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
				DeleteFile(GameArchiveTempPath);
			}

			try
			{
				if(lang == App.TextStrings["contextmenu_language_system"])
				{
					try{BpUtility.DeleteFromRegistry("Language");} catch{}
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
					else if(lang == App.TextStrings["contextmenu_language_italian"])
					{
						App.LauncherLanguage = "it";
					}
					else if(lang == App.TextStrings["contextmenu_language_czech"])
					{
						App.LauncherLanguage = "cs";
					}
					else if(lang == App.TextStrings["contextmenu_language_chinese_simplified"])
					{
						App.LauncherLanguage = "zh-CN";
					}
					else
					{
						Log($"Translation for {lang} does not exist", true, 1);
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
				Log($"Failed to set language:\n{ex}", true, 1);
				Status = LauncherStatus.Ready;
			}
		}

		private void CM_About_Click(object sender, RoutedEventArgs e)
		{
			if(LegacyBoxActive)
			{
				return;
			}

			LegacyBoxActive = true;
			AboutBox.Visibility = Visibility.Visible;
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
			BpUtility.GetMenuItem(OptionsContextMenu.Items, App.TextStrings["contextmenu_show_log"]).IsChecked = val;
			try
			{
				BpUtility.WriteToRegistry("ShowLog", val ? 1 : 0, RegistryValueKind.DWord);
			}
			catch(Exception ex)
			{
				Log($"Failed to write value with key ShowLog to registry:\n{ex}", true, 1);
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
				catch(Exception ex)
				{
					Log($"Failed to load translations:\n{ex}", true, 1);
					MessageBox.Show(App.TextStrings["msgbox_translations_download_error_msg"], App.TextStrings["msgbox_generic_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
					DeleteFile(App.LauncherTranslationsFile, true);
					BpUtility.DeleteFromRegistry("Language");
					Array.Resize(ref App.CommandLineArgs, App.CommandLineArgs.Length + 1);
					App.CommandLineArgs[App.CommandLineArgs.Length - 1] = "NOTRANSLATIONS";
					BpUtility.RestartApp();
				}
			}
			if(App.LauncherLanguage != "en" && App.LauncherLanguage != "zh-CN")
			{
				Resources["Font"] = new FontFamily("Segoe UI Bold");
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
				Log($"Failed to set custom background:\n{ex}", true, 1);
			}
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
	}
}