using Newtonsoft.Json;
using System;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Windows;

namespace BetterHI3Launcher
{
	public partial class MainWindow
	{
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
				case HI3Server.JP:
					url = OnlineVersionInfo.game_info.mirror.mihoyo.resource_info.jp.ToString();
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
						Log($"HoYoverse connection error, attempt №{i + 2}...", true, 2);
						timeout_add += 2500;
					}
				}
			}
			Dispatcher.Invoke(() =>
			{
				GameVersionText.Text = $"{App.TextStrings["version"]}: {miHoYoVersionInfo.game.latest.version.ToString()}";
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
		private dynamic FetchMediaFireFileMetadata(string id)
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
					metadata.downloadUrl = url;
					metadata.fileSize = web_response.ContentLength;
					return metadata;
				}
			}
			catch(WebException ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to fetch MediaFire file metadata:\n{ex}", true, 1);
				Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_mirror_error_msg"], ex.Message)).ShowDialog();});
			}
			return null;
		}
	}
}