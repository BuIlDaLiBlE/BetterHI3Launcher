﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;

namespace BetterHI3Launcher.Utility
{
	public partial class HttpClientHelper
	{
		private int _ThreadNumber;
		private CancellationToken _ThreadToken;
		private int _ThreadRetryAttempt = 1;
		private float _ThreadRetryDelay = 1f;
		private int _ThreadMaxRetry = 5;
		private bool _ThreadSingleMode = false;
		private IEnumerable<_ThreadProperty> _ThreadProperties;

		private class _ThreadProperty
		{
			public int ThreadID { get; set; }
			public long? StartOffset { get; set; }
			public long? EndOffset { get; set; }
			public int CurrentRetry { get; set; }
			public HttpResponseMessage HttpMessage { get; set; }
			public Stream LocalStream { get; set; }
			public Stream RemoteStream { get; set; }
		}

		private async Task<IEnumerable<Task>> StartThreads(long? StartOffset, long? EndOffset)
		{
			ICollection<Task> _out = new List<Task>();
			if ((StartOffset ?? 0) < 0 || (EndOffset ?? 0) < 0)
				throw new ArgumentOutOfRangeException($"StartOffset or EndOffset cannot be < 0!");

			_ThreadProperties = await GetThreadProperties(StartOffset, EndOffset);

			foreach (_ThreadProperty ThreadProperty in _ThreadProperties)
			{
				_out.Add(Task.Run(async () =>
				{
					using (ThreadProperty.HttpMessage)
					{
						using (ThreadProperty.RemoteStream = await GetRemoteStream(ThreadProperty.HttpMessage))
						{
							bool IsRetryLastChance = false;
							while (!await TryRunRetryableTask(ThreadProperty, IsRetryLastChance))
							{
								if (ThreadProperty.CurrentRetry > _ThreadMaxRetry - 1)
									IsRetryLastChance = true;

								Console.WriteLine($"Retrying for threadID: {ThreadProperty.ThreadID} (Retry Attempt: {ThreadProperty.CurrentRetry}/{_ThreadMaxRetry})...");
								await Task.Delay((int)(_ThreadRetryDelay * 1000), _ThreadToken);
								ThreadProperty.CurrentRetry++;
							}

							if (_DisposeStream)
								ThreadProperty.LocalStream.Dispose();
						}
					}
				}));
			}

			return _out;
		}

		private async Task<IEnumerable<_ThreadProperty>> GetThreadProperties(long? StartOffset, long? EndOffset)
		{
			if (!_ThreadSingleMode)
				return await GetMultipleThreadProperties();

			return await GetSingleThreadProperties(StartOffset, EndOffset);
		}

		private async Task<IList<_ThreadProperty>> GetSingleThreadProperties(long? StartOffset, long? EndOffset)
		{
			bool IsIgnore = false;
			long LocalLength = this._UseStreamOutput ? 0 : _OutputStream.Length;

			HttpRequestMessage RequestMessage = new HttpRequestMessage() { RequestUri = new Uri(_InputURL) };
			_ThreadProperty ThreadProperty = new _ThreadProperty()
			{
				ThreadID = 0,
				CurrentRetry = 1,
				LocalStream = this._UseStreamOutput ? _OutputStream : SeekStreamToEnd(_OutputStream),
				StartOffset = this._UseStreamOutput ? StartOffset ?? 0 : (StartOffset == null ? LocalLength : LocalLength + StartOffset),
				EndOffset = EndOffset
			};

			_DownloadedSize += LocalLength;

			if (ThreadProperty.EndOffset <= ThreadProperty.StartOffset)
			{
				ThreadProperty.StartOffset = StartOffset;
				ThreadProperty.EndOffset = EndOffset;
				IsIgnore = true;
			}

			RequestMessage.Headers.Range = new RangeHeaderValue(ThreadProperty.StartOffset, ThreadProperty.EndOffset);

			ThreadProperty.HttpMessage = CheckResponseStatusCode(await SendAsync(RequestMessage, HttpCompletionOption.ResponseHeadersRead, _ThreadToken));

			_TotalSizeToDownload += IsIgnore ? ThreadProperty.HttpMessage.Content.Headers.ContentLength ?? 0 :
				(ThreadProperty.HttpMessage.Content.Headers.ContentLength ?? 0) + LocalLength;

			if (IsIgnore)
				return new List<_ThreadProperty>();

			return new List<_ThreadProperty> { ThreadProperty };
		}

		private async Task<IList<_ThreadProperty>> GetMultipleThreadProperties()
		{
			IList<_ThreadProperty> _out = new List<_ThreadProperty>();

			_TotalSizeToDownload = TryGetContentLength();
			for (int i = 0; i < (_ThreadSingleMode ? 1 : _ThreadNumber); i++)
			{
				HttpRequestMessage _RequestMessage = new HttpRequestMessage() { RequestUri = new Uri(_InputURL) };
				_ThreadProperty ThreadProperty = new _ThreadProperty()
				{
					ThreadID = i,
					CurrentRetry = 1,
					LocalStream = SeekStreamToEnd(new FileStream(string.Format("{0}.{1:000}", _OutputPath, i + 1), FileMode.OpenOrCreate, FileAccess.Write))
				};
				_DownloadedSize += ThreadProperty.LocalStream.Length;
				ThreadProperty.StartOffset = i * (_TotalSizeToDownload / _ThreadNumber) + ThreadProperty.LocalStream.Length;
				ThreadProperty.EndOffset = ((i + 1) * (_TotalSizeToDownload / _ThreadNumber)) - 1;

				if ((ThreadProperty.EndOffset + 1) - ThreadProperty.StartOffset > 0)
				{
					_RequestMessage.Headers.Range = new RangeHeaderValue(ThreadProperty.StartOffset, ThreadProperty.EndOffset);
					ThreadProperty.HttpMessage = CheckResponseStatusCode(await SendAsync(_RequestMessage, HttpCompletionOption.ResponseHeadersRead, _ThreadToken));

					_out.Add(ThreadProperty);
				}
				else
				{
					ThreadProperty.LocalStream.Dispose();
				}
			}
			return _out;
		}

		private long TryGetContentLength()
		{
			while (true)
			{
				try
				{
					return GetContentLength(_InputURL, _ThreadToken) ?? 0;
				}
				catch (HttpRequestException ex)
				{
					if (_ThreadRetryAttempt > _ThreadMaxRetry)
						throw new HttpRequestException(ex.ToString(), ex);

					Console.WriteLine($"Error while fetching File Size (Retry Attempt: {_ThreadRetryAttempt})...");
					Task.Delay((int)(_ThreadRetryDelay * 1000), _ThreadToken).GetAwaiter().GetResult();
					_ThreadRetryAttempt++;
				}
			}
		}

		private async Task<bool> TryRunRetryableTask(_ThreadProperty ThreadProperty, bool IsLastRetry)
		{
			try
			{
				Console.WriteLine($"ThreadID: {ThreadProperty.ThreadID}, Start: {ThreadProperty.StartOffset}, EndOffset: {ThreadProperty.EndOffset}, Size: {ThreadProperty.EndOffset - ThreadProperty.StartOffset}");
				await ReadStreamAsync(ThreadProperty);
			}
			catch (IOException ex)
			{
				Console.WriteLine($"I/O Error on ThreadID: {ThreadProperty.ThreadID}\r\n{ex}");
				if (IsLastRetry)
					throw new IOException($"ThreadID: {ThreadProperty.ThreadID} has exceeded Max. Retry: {ThreadProperty.CurrentRetry - 1}/{_ThreadMaxRetry}. CANCELLING!!", ex);

				return false;
			}
			catch (HttpRequestException ex)
			{
				Console.WriteLine($"Error on ThreadID: {ThreadProperty.ThreadID}\r\n{ex}");
				if (IsLastRetry)
					throw new HttpRequestException($"ThreadID: {ThreadProperty.ThreadID} has exceeded Max. Retry: {ThreadProperty.CurrentRetry - 1}/{_ThreadMaxRetry}. CANCELLING!!", ex);

				return false;
			}
			catch (ArgumentOutOfRangeException ex)
			{
				ThreadProperty.LocalStream.Dispose();
				Console.WriteLine($"Cancel: ThreadID: {ThreadProperty.ThreadID} has been shutdown!\r\nChunk of this thread is already completed. Ignoring!!\r\n{ex}");
				return true;
			}
			catch (InvalidDataException ex)
			{
				ThreadProperty.LocalStream.Dispose();
				Console.WriteLine($"Cancel: ThreadID: {ThreadProperty.ThreadID} has been shutdown!\r\n{ex}");
				return true;
			}
			catch (TaskCanceledException ex)
			{
				ThreadProperty.LocalStream.Dispose();
				Console.WriteLine($"Cancel: ThreadID: {ThreadProperty.ThreadID} has been shutdown!");
				throw new TaskCanceledException(ex.ToString(), ex);
			}
			catch (OperationCanceledException ex)
			{
				ThreadProperty.LocalStream.Dispose();
				Console.WriteLine($"Cancel: ThreadID: {ThreadProperty.ThreadID} has been shutdown!");
				throw new TaskCanceledException(ex.ToString(), ex);
			}
			catch (Exception ex)
			{
				ThreadProperty.LocalStream.Dispose();
				Console.WriteLine($"Unknown Error on ThreadID: {ThreadProperty.ThreadID}\r\n{ex}");
				throw new Exception(ex.ToString(), ex);
			}

			return true;
		}

		public long? GetContentLength(string input, CancellationToken token = new CancellationToken())
		{
			HttpResponseMessage response = SendAsync(new HttpRequestMessage() { RequestUri = new Uri(input) }, HttpCompletionOption.ResponseHeadersRead, token)
				.GetAwaiter().GetResult();

			return response.Content.Headers.ContentLength;
		}
	}
}
