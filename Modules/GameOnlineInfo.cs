using BetterHI3Launcher.Config;
using BetterHI3Launcher.Utility;
using BetterHI3Launcher.Utility.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace BetterHI3Launcher
{
	public partial class MainWindow
	{
		private void FetchHYPGamePackageData()
		{
			string url = null;
			DynamicJson resource_info = OnlineVersionInfo["game_info"]["mirror"]["mihoyo"]["resource_info"];

			switch (Server)
			{
				case HI3Server.GLB:
					url = resource_info["global"];
					break;
				case HI3Server.SEA:
					url = resource_info["os"];
					break;
				case HI3Server.CN:
					url = resource_info["cn"];
					break;
				case HI3Server.TW:
					url = resource_info["tw"];
					break;
				case HI3Server.KR:
					url = resource_info["kr"];
					break;
				case HI3Server.JP:
					url = resource_info["jp"];
					break;
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
					catch(HttpRequestException)
					{
						throw;
					}
					catch
					{
						Log($"HYP API connection error, attempt №{i + 2}...", true, 2);
						timeout_add += 2500;
					}
				}
			}
			Dispatcher.Invoke(() =>
			{
				GameNameText.Text = GameFullName;
				GameVersionText.Text = HYPGamePackageData["main"]["major"]["version"];
			});
			return;

			void Get(int timeout)
			{
				using (Stream web_response_stream = Extension.GetHttpStreamResponse(url))
				{
					JsonNode HYPResourceDataResponse = JsonNode.Parse(web_response_stream);
					if (HYPResourceDataResponse?["retcode"]?.GetValue<int>() == 0)
					{
						if(HYPResourceDataResponse["data"] != null)
						{
							if(HYPResourceDataResponse["data"]["game_packages"]?.AsArray().Count > 0)
							{
								HYPGamePackageData = DynamicJson.Parse(HYPResourceDataResponse["data"]["game_packages"][0]?.ToJsonString() ?? "");
								if (!(HYPGamePackageData["game"]["biz"].ToString()?.Contains("bh3") ?? false))
								{
									throw new HttpRequestException($"HYP response does not contain data about Honkai Impact 3rd, got biz: {HYPGamePackageData["game"]["biz"].ToString()}");
								}
								if(HYPGamePackageData["main"]["major"]["game_pkgs"].Node?.AsArray().Count == 0)
								{
									throw new HttpRequestException("HYP game archive data is missing in response");
								}
								GameArchiveName = BpUtility.GetFileNameFromUrl(HYPGamePackageData["main"]["major"]["game_pkgs"][0]["url"].ToString());
							}
							else
							{
								throw new HttpRequestException("HYP game data is missing in response");
							}
						}
						else
						{
							throw new HttpRequestException("HYP data is missing in response");
						}
					}
					else
					{
						throw new HttpRequestException($"HYP response error: {HYPResourceDataResponse?["message"]}");
					}
				}

				using (var web_response =
                       Extension.CreateHttpRequest(HYPGamePackageData["main"]["major"]["game_pkgs"][0]["url"].ToString() ?? "",
                                                  HttpMethod.Head,
                                                  timeout))
				{
					HYPGamePackageData["main"]["major"]["game_pkgs"][0].TrySetValue("last_modified", web_response.Content.Headers.LastModified ?? default);
				}
			}
		}

		private FileMetadata FetchFileMetadata(string url)
		{
			if(string.IsNullOrEmpty(url))
			{
				throw new ArgumentNullException();
			}

			try
			{
				using HttpResponseMessage webResponse = Extension.CreateHttpRequest(url, HttpMethod.Head);
				webResponse.EnsureSuccessStatusCode();

				return new FileMetadata
				{
					DownloadUrl = url,
					ModifiedDate = webResponse.Content.Headers.LastModified ?? default,
					FileSize = webResponse.Content.Headers.ContentLength ?? 0,
					Title = BpUtility.GetFileNameFromUrl(url)
				};
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