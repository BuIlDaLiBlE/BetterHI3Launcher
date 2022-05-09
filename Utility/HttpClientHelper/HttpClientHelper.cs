﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Net.Http;

namespace BetterHI3Launcher.Utility
{
	public partial class HttpClientHelper : HttpClient
	{
		private Stopwatch _Stopwatch;
		private string _InputURL = "", _OutputPath = "";
		public DownloadState _DownloadState;
		private bool _IsFileAlreadyCompleted = false;

		public HttpClientHelper(bool IgnoreCompression = false, int maxRetryCount = 5, float maxRetryTimeout = 1)
			: base(new HttpClientHandler
		{
			AllowAutoRedirect = true,
			UseCookies = true,
			MaxConnectionsPerServer = 32,
			AutomaticDecompression = IgnoreCompression ? DecompressionMethods.None : DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.None
		})
		{
			this._Stopwatch = new Stopwatch();
			this._OutputStream = new MemoryStream();
			this._ThreadMaxRetry = maxRetryCount;
			this._ThreadRetryDelay = maxRetryTimeout;
			this._DownloadState = DownloadState.Idle;
		}

		public async Task DownloadFileAsync(string Input, string Output, CancellationToken Token, long? StartOffset = null, long? EndOffset = null)
		{
			this._UseStreamOutput = false;
			await InternalDownloadFileAsync(Input, new FileStream(Output, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write), Token, StartOffset, EndOffset, true);
		}

		public async Task DownloadFileAsync(string Input, Stream Output, CancellationToken Token, long? StartOffset = null, long? EndOffset = null, bool DisposeStream = true)
		{
			this._UseStreamOutput = true;
			await InternalDownloadFileAsync(Input, Output, Token, StartOffset, EndOffset, DisposeStream);
		}

		private async Task InternalDownloadFileAsync(string Input, Stream Output, CancellationToken Token, long? StartOffset, long? EndOffset, bool DisposeStream)
		{
			this._InputURL = Input;
			this._OutputStream = Output;
			this._ThreadToken = Token;
			this._ThreadSingleMode = true;
			this._Stopwatch = Stopwatch.StartNew();
			this._LastContinuedSize = 0;
			this._DownloadState = DownloadState.Downloading;
			this._IsFileAlreadyCompleted = false;
			this._DisposeStream = DisposeStream;

			try
			{
				await Task.WhenAll(await StartThreads(StartOffset, EndOffset));
				this._DownloadState = DownloadState.Completed;
				UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));

				_Stopwatch.Stop();
			}
			catch (TaskCanceledException ex)
			{
				_Stopwatch.Stop();
				this._DownloadState = DownloadState.Cancelled;
				UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));
				throw new TaskCanceledException($"Cancellation for {Input} has been fired!", ex);
			}
		}

		public async Task DownloadFileAsync(string Input, string Output, int DownloadThread, CancellationToken Token)
		{
			this._InputURL = Input;
			this._OutputPath = Output;
			this._ThreadNumber = DownloadThread;
			this._ThreadToken = Token;
			this._ThreadSingleMode = false;
			this._Stopwatch = Stopwatch.StartNew();
			this._LastContinuedSize = 0;
			this._DownloadState = DownloadState.Downloading;
			this._IsFileAlreadyCompleted = false;
			this._UseStreamOutput = false;
			this._DisposeStream = true;

			try
			{
				await Task.WhenAll(await StartThreads(null, null));

				// HACK: Round the size after multidownload finished
				_DownloadedSize += _TotalSizeToDownload - _DownloadedSize;
				UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, (int)_SelfReadFileSize, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));

				if (!_IsFileAlreadyCompleted)
				{
					MergeSlices();
				}
				this._DownloadState = DownloadState.Completed;
				UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));

				_Stopwatch.Stop();
			}
			catch (TaskCanceledException ex)
			{
				_Stopwatch.Stop();
				this._DownloadState = DownloadState.Cancelled;
				UpdateProgress(new _DownloadProgress(_DownloadedSize, _TotalSizeToDownload, 0, _LastContinuedSize, _Stopwatch.Elapsed, _DownloadState));
				throw new TaskCanceledException($"Cancellation for {Input} has been fired!", ex);
			}
		}

		public void DownloadFile(string Input, Stream Output, CancellationToken Token, long? StartOffset = null, long? EndOffset = null, bool DisposeStream = true) =>
			DownloadFileAsync(Input, Output, Token, StartOffset, EndOffset, DisposeStream).GetAwaiter().GetResult();
		public void DownloadFile(string Input, string Output, CancellationToken Token, long? StartOffset = null, long? EndOffset = null) =>
			DownloadFileAsync(Input, Output, Token, StartOffset, EndOffset).GetAwaiter().GetResult();
		public void DownloadFile(string Input, string Output, int DownloadThread, CancellationToken Token) =>
			DownloadFileAsync(Input, Output, DownloadThread, Token).GetAwaiter().GetResult();
	}
}
