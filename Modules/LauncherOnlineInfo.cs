using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace BetterHI3Launcher
{
	public partial class MainWindow
	{
		private void FetchOnlineVersionInfo()
		{
			#if DEBUG
			var version_info_url = "https://bpnet.work/bh3?launcher_status=debug";
			#else
			var version_info_url = "https://bpnet.work/bh3?launcher_status=prod";
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
						Log($"Bp Network connection error, attempt №{i + 2}...", true, 2);
						timeout_add += 2500;
					}
				}
			}
			OnlineVersionInfo = JsonConvert.DeserializeObject<dynamic>(version_info);
			if(OnlineVersionInfo.status == "success")
			{
				OnlineVersionInfo = OnlineVersionInfo.launcher_status;
				App.LauncherExeName = OnlineVersionInfo.launcher_info.name;
				App.LauncherPath = Path.Combine(App.LauncherRootPath, App.LauncherExeName);
				App.LauncherArchivePath = Path.Combine(App.LauncherRootPath, BpUtility.GetFileNameFromUrl(OnlineVersionInfo.launcher_info.url.ToString()));
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

		private async void FetchAnnouncements()
		{
			try
			{
				await Task.Run(() =>
				{
					var web_client = new BpWebClient();
					dynamic announcements;
					announcements = JsonConvert.DeserializeObject<dynamic>(web_client.DownloadString($"{OnlineVersionInfo.launcher_info.links.announcements.ToString()}&lang={App.LauncherLanguage}"));
					if(announcements.status == "success")
					{
						announcements = announcements.announcements;
						foreach(dynamic announcement in announcements)
						{
							string min_launcher_version = announcement.min_version.ToString();
							if(!new LauncherVersion(min_launcher_version).IsNewerThan(App.LocalLauncherVersion) && DateTime.Compare(DateTime.UtcNow, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((double)announcement.relevant_until)) < 0 && !App.SeenAnnouncements.Contains(announcement.id.ToString()))
							{
								App.Announcements.Add(announcement);
							}
						}
					}
					else
					{
						Log($"Failed to fetch announcements: {announcements.status_message}", true, 2);
					}
				});
			}
			catch(Exception ex)
			{
				Log($"Failed to fetch announcements:\n{ex}", true, 2);
			}
			if(App.Announcements.Count > 0)
			{
				Dispatcher.Invoke(() => {ShowAnnouncement(App.Announcements.First);});
			}
			else
			{
				LauncherLocalVersionCheck();
			}
		}

		private void ShowAnnouncement(dynamic announcement)
		{
			LegacyBoxActive = true;
			AnnouncementBoxTitleTextBlock.Text = announcement.content.title;
			TextBlockExt.SetFormattedText(AnnouncementBoxMessageTextBlock, announcement.content.text.ToString());
			AnnouncementBox.Visibility = Visibility.Visible;
			FlashMainWindow();
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
					var web_client = new BpWebClient {Timeout = timeout};
					if(App.LauncherLanguage == "ru")
					{
						changelog = web_client.DownloadString(OnlineVersionInfo.launcher_info.links.changelog.ru.ToString());
					}
					else
					{
						changelog = web_client.DownloadString(OnlineVersionInfo.launcher_info.links.changelog.en.ToString());
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
								Log($"Bp Network connection error, attempt №{i + 2}...", true, 2);
								timeout_add += 2500;
							}
						}
					}
				}
				catch
				{
					Log($"Bp Network connection error, giving up...", true, 2);
					changelog = App.TextStrings["changelogbox_3_msg"];
				}
				Dispatcher.Invoke(() => {ChangelogBoxTextBox.Text = changelog;});
			});
		}
	}
}