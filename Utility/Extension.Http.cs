using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace BetterHI3Launcher.Utility;

public static partial class Extension
{
	public static HttpClient SharedHttpClient = new(new HttpClientHandler { AllowAutoRedirect = true }, false);
	public static HttpClient SharedHttpClientNoRedirect = new(new HttpClientHandler { AllowAutoRedirect = false }, false);

	public static async Task<HttpResponseMessage> CreateHttpRequestAsync(
		string            url,
		HttpMethod?       method        = null,
		int               timeoutMs     = 10000,
		bool              allowRedirect = true,
		RangeHeaderValue? range         = null,
		CancellationToken token         = default)
	{
		try
		{
			method ??= HttpMethod.Get;
			HttpRequestMessage request = new(method, url);
			request.Headers.TryAddWithoutValidation("User-Agent", App.UserAgent);
			request.Headers.TryAddWithoutValidation("Accept-Language", App.LauncherLanguage);
			if (range != null)
			{
				request.Headers.Range = range;
			}

			using CancellationTokenSource cts = new(timeoutMs);
			using CancellationTokenSource coopCts = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token);
			HttpClient client = allowRedirect ? SharedHttpClient : SharedHttpClientNoRedirect;
			HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, coopCts.Token);
			response.EnsureSuccessStatusCode();

			return response;
		}
		catch (HttpRequestException httpException)
		{
			throw new HttpRequestException($"Error occurred while creating web request to {url}: {httpException.Message}", httpException);
		}
	}

	public static HttpResponseMessage CreateHttpRequest(
		string url,
		HttpMethod? method = null,
		int timeoutMs = 10000,
		bool allowRedirect = true,
		RangeHeaderValue? range = null)
	{
		try
		{
			method ??= HttpMethod.Get;
			HttpRequestMessage request = new(method, url);
			request.Headers.TryAddWithoutValidation("User-Agent", App.UserAgent);
			request.Headers.TryAddWithoutValidation("Accept-Language", App.LauncherLanguage);
			if (range != null)
			{
				request.Headers.Range = range;
			}

			using CancellationTokenSource cts = new(timeoutMs);
			HttpClient client = allowRedirect ? SharedHttpClient : SharedHttpClientNoRedirect;
			HttpResponseMessage response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).WaitResult();
			response.EnsureSuccessStatusCode();

			return response;
		}
		catch (HttpRequestException httpException)
		{
			throw new HttpRequestException($"Error occurred while creating web request to {url}: {httpException.Message}", httpException);
		}
	}

	public static async Task<string?> GetHttpStringResponseAsync(
		string url,
		int timeoutMs = 10000,
		RangeHeaderValue? range = null,
		CancellationToken token = default)
	{
		using HttpResponseMessage response = await CreateHttpRequestAsync(url, null, timeoutMs, true, range, token);
		return await response.Content.ReadAsStringAsync();
	}

	public static string? GetHttpStringResponse(
		string url,
		int timeoutMs = 10000,
		RangeHeaderValue? range = null)
	{
		HttpResponseMessage response = CreateHttpRequest(url, null, timeoutMs, true, range);
		return response.Content.ReadAsStringAsync().WaitResult();
	}

	public static async Task<Stream> GetHttpStreamResponseAsync(
		string url,
		int timeoutMs = 10000,
		RangeHeaderValue? range = null,
		CancellationToken token = default)
	{
		HttpResponseMessage response = await CreateHttpRequestAsync(url, null, timeoutMs, true, range, token);
		return await response.Content.ReadAsStreamAsync();
	}

	public static Stream GetHttpStreamResponse(
		string url,
		int timeoutMs = 10000,
		RangeHeaderValue? range = null)
	{
		HttpResponseMessage response = CreateHttpRequest(url, null, timeoutMs, true, range);
		return response.Content.ReadAsStreamAsync().WaitResult();
	}

	public static async Task DownloadFileAsync(
		string url,
		string path,
		int timeoutMs = 10000,
		RangeHeaderValue? range = null,
		CancellationToken token = default)
	{
		if (Path.GetDirectoryName(path) is { } pathDir)
		{
			Directory.CreateDirectory(pathDir);
		}

		using Stream responseStream = await GetHttpStreamResponseAsync(url, timeoutMs, range, token);
		using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
		await responseStream.CopyToAsync(fileStream, 81920, token);
	}

	public static void DownloadFile(
		string url,
		string path,
		int timeoutMs = 10000,
		RangeHeaderValue? range = null)
	{
		if (Path.GetDirectoryName(path) is { } pathDir)
		{
			Directory.CreateDirectory(pathDir);
		}

		using Stream responseStream = GetHttpStreamResponse(url, timeoutMs, range);
		using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
		responseStream.CopyTo(fileStream);
	}
}
