using Newtonsoft.Json;
using System;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

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
				GameArchiveName = BpUtility.GetFileNameFromUrl(miHoYoVersionInfo.game.latest.path.ToString());
				web_request = BpUtility.CreateWebRequest(miHoYoVersionInfo.game.latest.path.ToString(), "HEAD", timeout);
				using(var web_response = (HttpWebResponse)web_request.GetResponse())
				{
					miHoYoVersionInfo.size = web_response.ContentLength;
					miHoYoVersionInfo.last_modified = (DateTimeOffset)web_response.LastModified;
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
				GameNameText.Text = GameFullName;
				GameVersionText.Text = miHoYoVersionInfo.game.latest.version.ToString();
			});
		}

		private dynamic FetchFileMetadata(string url)
		{
			if(string.IsNullOrEmpty(url))
			{
				throw new ArgumentNullException();
			}

			try
			{
				var web_request = BpUtility.CreateWebRequest(url, "HEAD");
				web_request.AllowAutoRedirect = false;
				using(var web_response = (HttpWebResponse)web_request.GetResponse())
				{
					dynamic metadata = new ExpandoObject();
					bool is_redirect = false;
					switch(web_response.StatusCode)
					{
						case HttpStatusCode.Moved:
						case HttpStatusCode.Found:
						case HttpStatusCode.SeeOther:
						case HttpStatusCode.TemporaryRedirect:
							is_redirect = true;
							metadata.downloadUrl = web_response.Headers["Location"].ToString();
							break;
						default:
							metadata.downloadUrl = url;
							metadata.modifiedDate = web_response.LastModified;
							metadata.fileSize = web_response.ContentLength;
							break;
					}
					metadata.title = BpUtility.GetFileNameFromUrl(metadata.downloadUrl);
					if(is_redirect)
					{
						var web_request_redirect = BpUtility.CreateWebRequest(metadata.downloadUrl, "HEAD");
						using(var web_response_redirect = (HttpWebResponse)web_request_redirect.GetResponse())
						{
							metadata.modifiedDate = web_response_redirect.LastModified;
							metadata.fileSize = web_response_redirect.ContentLength;
						}
					}
					return metadata;
				}
			}
			catch(WebException ex)
			{
				Status = LauncherStatus.Error;
				Log($"Failed to fetch file metadata:\n{ex}", true, 1);
				Dispatcher.Invoke(() => {new DialogWindow(App.TextStrings["msgbox_net_error_title"], string.Format(App.TextStrings["msgbox_mirror_error_msg"], ex.Message)).ShowDialog();});
			}
			return null;
		}
	}
}