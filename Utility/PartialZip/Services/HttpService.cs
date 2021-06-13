using BetterHI3Launcher;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace PartialZip.Services
{
	internal class HttpService
	{
		private string _url;

		private Task<WebHeaderCollection> _contentHeaders;

		internal HttpService(string url)
		{
			_url = url;
			_contentHeaders = GetContentInfo();
		}

		internal async Task<ulong> GetContentLength()
		{
			WebHeaderCollection headers = await _contentHeaders;
			return ulong.Parse(headers.Get("Content-Length"));
		}

		internal async Task<bool> SupportsPartialZip()
		{
			WebHeaderCollection headers = await _contentHeaders;
			return headers.Get("Accept-Ranges") == "bytes";
		}

		private async Task<WebHeaderCollection> GetContentInfo()
		{
			HttpWebRequest request = WebRequest.CreateHttp(_url);
			request.AllowAutoRedirect = true;
			request.KeepAlive = true;
			request.Method = "HEAD";
			request.UserAgent = App.UserAgent;

			using(WebResponse response = await request.GetResponseAsync())
			{
				return response.Headers;
			}
		}

		internal async Task<byte[]> GetRange(ulong startBytes, ulong endBytes)
		{
			if(startBytes < endBytes)
			{
				HttpWebRequest request = WebRequest.CreateHttp(_url);
				request.AllowAutoRedirect = true;
				request.KeepAlive = true;
				request.AddRange((long)startBytes, (long)endBytes);
				request.Method = "GET";
				request.UserAgent = App.UserAgent;

				using(WebResponse response = await request.GetResponseAsync())
				{
					using(Stream responseStream = response.GetResponseStream())
					{
						using(MemoryStream output = new MemoryStream())
						{
							await responseStream.CopyToAsync(output);
							return output.ToArray();
						}
					}
				}
			}
			else
			{
				throw new Exception("Invalid byte range.");
			}
		}
	}
}