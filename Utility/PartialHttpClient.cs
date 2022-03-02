using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;

namespace BetterHI3Launcher.Utility
{
	public class ParallelHttpClient
	{
		readonly HttpClient httpClient;
		protected Stream localStream;
		protected Stream remoteStream;
		public event EventHandler<DownloadStatusChanged> ResumablityChanged;
		public event EventHandler<DownloadProgressChanged> ProgressChanged;
		public event EventHandler<PartialDownloadProgressChanged> PartialProgressChanged;
		public event EventHandler<DownloadProgressCompleted> Completed;
		public bool merging = false;
		private bool stop = true; // by default stop is true
		/* Declare download buffer
		 * by default: 16 KiB (16384 bytes)
		*/
		readonly long bufflength = 16384;

		int downloadThread;
		long downloadPartialExistingSize = 0,
			 downloadPartialSize = 0;
		string downloadPartialOutputPath, downloadPartialInputPath;
		CancellationToken downloadPartialToken;

		public ParallelHttpClient(bool IgnoreCompression = false)
		{
			httpClient = new HttpClient(
			new HttpClientHandler()
			{
				AutomaticDecompression = IgnoreCompression ?
				DecompressionMethods.None :
				DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.None,
				UseCookies = true,
				MaxConnectionsPerServer = 16,
				AllowAutoRedirect = true,
			});

			httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", App.UserAgent);
		}

		DownloadStatusChanged resumabilityStatus;

		public bool DownloadFile(string input, string output, string customMessage = "", long startOffset = -1, long endOffset = -1, CancellationToken token = new CancellationToken())
		{
			if(string.IsNullOrEmpty(customMessage)) customMessage = $"Downloading {Path.GetFileName(output)}";

			bool ret;

			while(!(ret = GetRemoteStreamResponse(input, output, startOffset, endOffset, customMessage, token, false)))
			{
				#if DEBUG
				Console.WriteLine("Retrying...");
				#endif
				Thread.Sleep(1000);
			}

			return ret;
		}

		public bool DownloadStream(string input, MemoryStream output, CancellationToken token = new CancellationToken(), long startOffset = -1, long endOffset = -1, string customMessage = "")
		{
			if(string.IsNullOrEmpty(customMessage)) customMessage = $"Downloading to stream";

			bool ret;
			localStream = output;

			while(!(ret = GetRemoteStreamResponse(input, @"buffer", startOffset, endOffset, customMessage, token, true)))
			{
				#if DEBUG
				Console.WriteLine("Retrying...");
				#endif
				Thread.Sleep(1000);
			}

			return ret;
		}

		List<SegmentDownloadProperties> segmentDownloadProperties = new List<SegmentDownloadProperties>();
		List<Task> segmentDownloadTask;

		public class ChunkRanges
		{
			public long Start {get; set;}
			public long End {get; set;}
		}

		public void DownloadFileMultipleSession(string input, string output, string customMessage = "", int threads = 1, CancellationToken token = new CancellationToken())
		{
			downloadThread = threads;
			downloadPartialToken = token;
			downloadPartialInputPath = input;
			downloadPartialOutputPath = output;

			OnCompleted(new DownloadProgressCompleted(){DownloadCompleted = false});
			downloadPartialSize = GetContentLength(downloadPartialInputPath) ?? 0;
			long startContent, endContent;
			segmentDownloadTask = new List<Task>();
			segmentDownloadProperties = new List<SegmentDownloadProperties>();

			List<ChunkRanges> chunkRanges = new List<ChunkRanges>();

			long partitionSize = (long)Math.Ceiling((double)downloadPartialSize / downloadThread);

			for(int i = 0; i < downloadThread; i++)
			{
				startContent = i * (downloadPartialSize / downloadThread);
				endContent = i + 1 == downloadThread ? downloadPartialSize : ((i + 1) * (downloadPartialSize / downloadThread)) - 1;

				segmentDownloadProperties.Add(new SegmentDownloadProperties(0, 0, 0, 0)
				{CurrentReceived = 0, StartRange = startContent, EndRange = endContent, PartRange = i});
			}

			OnResumabilityChanged(new DownloadStatusChanged(true));
			#if DEBUG
			Console.WriteLine($"\r\nStarting Partial Download!\r\n\tTotal Size: {BpUtility.ToBytesCount(downloadPartialSize)} ({downloadPartialSize} bytes)\r\n\tThreads/Chunks: {downloadThread}");
			#endif

			stop = false;

			try
			{
				foreach(SegmentDownloadProperties j in segmentDownloadProperties)
				{
					segmentDownloadTask.Add(Task.Run(async () =>
					{
						while(!await GetPartialSessionStream(j))
						{
							#if DEBUG
							Console.WriteLine($"Retrying to connect for chunk no: {j.PartRange + 1}...");
							#endif
							Thread.Sleep(1000);
						}
					}, downloadPartialToken));
				}

				Task.Run(() => GetPartialDownloadEvents());
				Task.WhenAll(segmentDownloadTask).GetAwaiter().GetResult();
			}
			catch(Exception ex)
			{
				#if DEBUG
				Console.WriteLine($"{ex}");
				#endif
			}

			if(!downloadPartialToken.IsCancellationRequested)
			{
				try
				{
					MergePartialChunks();
				}
				catch(OperationCanceledException ex)
				{
					#if DEBUG
					Console.WriteLine("Merging cancelled!");
					#endif
					throw new OperationCanceledException("", ex);
				}
			}
			else
			{
				segmentDownloadTask.Clear();
				#if DEBUG
				Console.WriteLine("Download cancelled!");
				#endif
				throw new OperationCanceledException();
			}

			segmentDownloadTask.Clear();
			#if DEBUG
			Console.WriteLine(" Done!");
			#endif
			stop = true;
		}

		long CheckExistingPartialChunksSize()
		{
			for(int i = 0; i < downloadThread; i++)
			{
				try{downloadPartialExistingSize += new FileInfo(string.Format("{0}.{1:000}", downloadPartialOutputPath, i + 1)).Length;}catch{}
			}
			return downloadPartialExistingSize;
		}

		public void MergePartialChunks()
		{
			if(CheckExistingPartialChunksSize() != downloadPartialSize)
			{
				#if DEBUG
				Console.WriteLine("Download is not completed yet! Please do DownloadFileMultipleSession() and wait it for finish!");
				#endif
				return;
			}

			if(downloadPartialToken.IsCancellationRequested)
			{
				#if DEBUG
				Console.WriteLine("Cannot do merging since token is cancelled!");
				#endif
				return;
			}

			stop = false;
			Task.Run(() => GetPartialDownloadEvents());

			string chunkFileName;
			FileStream chunkFile;
			byte[] buffer = new byte[67108864];

			int read;
			long totalRead = 0;
			var sw = Stopwatch.StartNew();
			merging = true;

			FileInfo fileInfo = new FileInfo($"{downloadPartialOutputPath}_tmp");

			using(FileStream fs = fileInfo.Create())
			{
				for(int i = 0; i < downloadThread; i++)
				{
					chunkFileName = string.Format("{0}.{1:000}", downloadPartialOutputPath, i + 1);
					using(chunkFile = new FileStream(chunkFileName, FileMode.Open, FileAccess.Read))
					{
						while((read = chunkFile.Read(buffer, 0, buffer.Length)) > 0)
						{
							downloadPartialToken.ThrowIfCancellationRequested();
							fs.Write(buffer, 0, read);
							totalRead += read;
							PartialOnProgressChanged(new PartialDownloadProgressChanged(totalRead, 0, downloadPartialSize, sw.Elapsed.TotalSeconds){Message = "Merging Chunks", CurrentReceived = read});
						}
						chunkFile.Dispose();
					}

					File.Delete(chunkFileName);
				}
			}

			if(File.Exists(downloadPartialOutputPath))
			{
				File.Delete(downloadPartialOutputPath);
			}

			fileInfo.MoveTo(downloadPartialOutputPath);

			sw.Stop();
			stop = true;
		}

		public void GetPartialDownloadEvents()
		{
			var sw = Stopwatch.StartNew();
			List<SegmentDownloadProperties> i;

			long BytesReceived = 0,
					 CurrentReceived = 0,
					 LastBytesReceived = 0,
					 nowBytesReceived = 0;

			while(!stop)
			{
				// Prevent List from getting throw while ReadPartialRemoteStream() is modifying the list
				i = new List<SegmentDownloadProperties>(segmentDownloadProperties);

				BytesReceived = i.Sum(x => x.BytesReceived);

				if(LastBytesReceived != BytesReceived)
				{
					CurrentReceived = i.Sum(x => x.CurrentReceived);
					nowBytesReceived = i.Sum(x => x.NowBytesReceived);
					PartialOnProgressChanged(new PartialDownloadProgressChanged(BytesReceived, nowBytesReceived, downloadPartialSize, sw.Elapsed.TotalSeconds){Message = "Downloading", CurrentReceived = CurrentReceived});

					LastBytesReceived = BytesReceived;
				}

				Thread.Sleep(1000);
				i.Clear();
			}
			sw.Stop();
			return;
		}

		public async Task<bool> GetPartialSessionStream(SegmentDownloadProperties j)
		{
			Stream stream;
			try
			{
				string partialOutput = string.Format("{0}.{1:000}", downloadPartialOutputPath, j.PartRange + 1);
				FileInfo fileinfo = new FileInfo(partialOutput);
				#if DEBUG
				Console.WriteLine($"\tPart {j.PartRange + 1} > Start: {j.StartRange}{(j.StartRange == 0 ? "\t" : "")}\tEnd: {j.EndRange}\tSize: {BpUtility.ToBytesCount(j.EndRange - j.StartRange)}");
				#endif

				long existingLength = fileinfo.Exists ? fileinfo.Length : 0;

				long fileSize = (j.EndRange - j.StartRange) + 1;

				if(existingLength > fileSize)
				{
					fileinfo.Create();
				}

				if(existingLength != fileSize)
				{
					using(HttpClient client = new HttpClient())
					{
						HttpRequestMessage request = new HttpRequestMessage() { RequestUri = new Uri(downloadPartialInputPath) };
						request.Headers.TryAddWithoutValidation("User-Agent", App.UserAgent);
						request.Headers.Range = new RangeHeaderValue(j.StartRange + existingLength, j.EndRange);

						HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, downloadPartialToken);

						using(stream = fileinfo.Open(FileMode.Append, FileAccess.Write))
						{
							try
							{
								await ReadPartialRemoteStream(response, stream, existingLength, j.PartRange, j.EndRange - j.StartRange);
							}
							catch(OperationCanceledException)
							{
								stream.Dispose();
								throw new OperationCanceledException();
							}
						}
					}
				}
				else if(existingLength == fileSize)
				{
					segmentDownloadProperties[j.PartRange] = new SegmentDownloadProperties(fileSize, 0, fileSize, new TimeSpan().TotalSeconds) { CurrentReceived = 0 };
				}
			}
			catch(OperationCanceledException)
			{
				#if DEBUG
				Console.WriteLine($"Download cancelled for part {j.PartRange + 1}");
				#endif
				return true;
			}
			catch(Exception ex)
			{
				#if DEBUG
				Console.WriteLine($"{ex}");
				#endif
				return false;
			}
			return true;
		}

		async Task ReadPartialRemoteStream(
		   HttpResponseMessage response,
		   Stream localStream,
		   long existingLength,
		   int threadNumber,
		   long contentLength)
		{
			int byteSize = 0;
			long totalReceived = byteSize + existingLength,
				 nowReceived = 0;
			byte[] buffer = new byte[bufflength];
			#if(NETCOREAPP)
			using(Stream remoteStream = await response.Content.ReadAsStreamAsync())
			#else
			using(Stream remoteStream = await response.Content.ReadAsStreamAsync())
			#endif
			{
				var sw = Stopwatch.StartNew();
				while((byteSize = await remoteStream.ReadAsync(buffer, 0, buffer.Length, downloadPartialToken)) > 0)
				{
					downloadPartialToken.ThrowIfCancellationRequested();
					localStream.Write(buffer, 0, byteSize);
					totalReceived += byteSize;
					nowReceived += byteSize;

					segmentDownloadProperties[threadNumber] = new SegmentDownloadProperties(totalReceived, nowReceived, contentLength, sw.Elapsed.TotalSeconds){CurrentReceived = byteSize};
				}
				sw.Stop();
			}
		}

		bool GetRemoteStreamResponse(string input, string output, long startOffset, long endOffset, string customMessage, CancellationToken token, bool isStream)
		{
			bool returnValue = true;
			OnCompleted(new DownloadProgressCompleted(){DownloadCompleted = false});

			try
			{
				UseStream(input, output, startOffset, endOffset, customMessage, token, isStream);
			}
			#if(NETCOREAPP)
			catch(HttpRequestException e)
			{
				returnValue = ThrowWebExceptionAsBool(e);
			}
			#endif
			catch(TaskCanceledException e)
			{
				returnValue = true;
				throw new TaskCanceledException(e.ToString());
			}
			catch(OperationCanceledException e)
			{
				returnValue = true;
				throw new TaskCanceledException(e.ToString());
			}
			catch(NullReferenceException e)
			{
				#if DEBUG
				Console.WriteLine($"This file {input} has 0 byte in size.\r\nTraceback: {e}");
				#endif
				returnValue = false;
			}
			catch(ObjectDisposedException e)
			{
				#if DEBUG
				Console.WriteLine($"Connection is getting repeated while Stream has been disposed on {Path.GetFileName(output)}\r\nTraceback: {e}");
				#endif
				returnValue = true;
			}
			catch(Exception e)
			{
				#if DEBUG
				Console.WriteLine($"An error occured while downloading {Path.GetFileName(output)}\r\nTraceback: {e}");
				#endif
				returnValue = false;
			}
			finally
			{
				if(returnValue)
				{
					if(!isStream) localStream?.Dispose();
					remoteStream?.Dispose();
				}
			}

			OnCompleted(new DownloadProgressCompleted(){DownloadCompleted = true});

			return returnValue;
		}

		public long? GetContentLength(string input, CancellationToken token = new CancellationToken())
		{
			HttpResponseMessage response = httpClient
				.SendAsync(new HttpRequestMessage(){RequestUri = new Uri(input)}, HttpCompletionOption.ResponseHeadersRead, token)
				.GetAwaiter().GetResult();

			return response.Content.Headers.ContentLength;
		}

		void UseStream(string input, string output, long startOffset, long endOffset, string customMessage, CancellationToken token, bool isStream)
		{
			token.ThrowIfCancellationRequested();
			long contentLength;
			FileInfo fileinfo = new FileInfo(output);

			long existingLength = isStream ? localStream.Length : fileinfo.Exists ? fileinfo.Length : 0;

			HttpRequestMessage request = new HttpRequestMessage(){RequestUri = new Uri(input)};
			request.Headers.TryAddWithoutValidation("User-Agent", App.UserAgent);
			request.Headers.Range = (startOffset != -1 && endOffset != -1) ?
									new RangeHeaderValue(startOffset, endOffset):
									new RangeHeaderValue(existingLength, null);

			HttpResponseMessage response = httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).GetAwaiter().GetResult();

			if(!((response.Content.Headers.ContentRange.Length ?? 0) == existingLength))
			{
				using(ThrowUnacceptableStatusCode(response))
				{
					contentLength = (startOffset != -1 && endOffset != -1) ?
									endOffset - startOffset :
									existingLength + (response.Content.Headers.ContentRange.Length - response.Content.Headers.ContentRange.From) ?? 0;

					resumabilityStatus = new DownloadStatusChanged((int)response.StatusCode == 206);

					if(!isStream)
						localStream = fileinfo.Open(resumabilityStatus.ResumeSupported ? FileMode.Append : FileMode.Create, FileAccess.Write);

					OnResumabilityChanged(resumabilityStatus);

					ReadRemoteStream(
						response,
						localStream,
						existingLength,
						contentLength,
						customMessage,
						token
						);
					response.Dispose();
				}
			}
			else
			{
				#if DEBUG
				Console.WriteLine($"File download for {input} is already completed! Skipping...");
				#endif
			}
		}

		HttpResponseMessage ThrowUnacceptableStatusCode(HttpResponseMessage input)
		{
			#if(NETCOREAPP)
			if(!input.IsSuccessStatusCode)
			{
				throw new HttpRequestException($"an Error occured while doing request to {input.RequestMessage.RequestUri} with error code {(int)input.StatusCode} ({input.StatusCode})",
					null,
					input.StatusCode);
			}
			#else
			if(!input.IsSuccessStatusCode)
			{ 
				throw new HttpRequestException($"an Error occured while doing request to {input.RequestMessage.RequestUri} with error code {(int)input.StatusCode} ({input.StatusCode})");
			}
			#endif

			return input;
		}

		void ReadRemoteStream(
		   HttpResponseMessage response,
		   Stream localStream,
		   long existingLength,
		   long contentLength,
		   string customMessage,
		   CancellationToken token)
		{
			int byteSize = 0;
			long totalReceived = byteSize + existingLength;
			byte[] buffer = new byte[bufflength];
			#if(NETCOREAPP)
			using(remoteStream = response.Content.ReadAsStream(token))
			#else
			using(remoteStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
			#endif
			{
				var sw = Stopwatch.StartNew();
				while((byteSize = remoteStream.Read(buffer, 0, buffer.Length)) > 0)
				{
					token.ThrowIfCancellationRequested();
					localStream.Write(buffer, 0, byteSize);
					totalReceived += byteSize;

					OnProgressChanged(new DownloadProgressChanged(totalReceived, contentLength, sw.Elapsed.TotalSeconds){Message = customMessage, CurrentReceived = byteSize});
				}
				sw.Stop();
			}
		}
		#if(NETCOREAPP)
		bool ThrowWebExceptionAsBool(HttpRequestException e)
		{
			switch (GetStatusCodeResponse(e))
			{
				// Always ignore 416 code
				case 416:
					return true;
				default:
					LogWriteLine(e.Message, LogType.Error, true);
					return false;
			}
		}

		protected virtual short GetStatusCodeResponse(HttpRequestException e) => (short)e.StatusCode;
		#endif
		protected virtual void OnResumabilityChanged(DownloadStatusChanged e) => ResumablityChanged?.Invoke(this, e);
		protected virtual void OnProgressChanged(DownloadProgressChanged e) => ProgressChanged?.Invoke(this, e);
		protected virtual void PartialOnProgressChanged(PartialDownloadProgressChanged e) => PartialProgressChanged?.Invoke(this, e);
		protected virtual void OnCompleted(DownloadProgressCompleted e) => Completed?.Invoke(this, e);
	}

	public class SegmentDownloadProperties
	{
		public SegmentDownloadProperties(long totalReceived, long continueReceived, long fileSize, double totalSecond)
		{
			BytesReceived = totalReceived;
			TotalBytesToReceive = fileSize;
			NowBytesReceived = continueReceived;
		}
		public long CurrentReceived {get; set;}
		public long BytesReceived {get; private set;}
		public long NowBytesReceived {get; set;}
		public long TotalBytesToReceive {get; private set;}
		public int PartRange {get; set;}
		public long StartRange {get; set;}
		public long EndRange {get; set;}
	}

	public class DownloadStatusChanged : EventArgs
	{
		public DownloadStatusChanged(bool canResume) => ResumeSupported = canResume;
		public bool ResumeSupported {get; private set;}
	}

	public class DownloadProgressCompleted : EventArgs
	{
		public bool DownloadCompleted {get; set;}
	}

	public class PartialDownloadProgressChanged : EventArgs
	{
		public PartialDownloadProgressChanged(long totalReceived, long continueReceived, long fileSize, double totalSecond)
		{
			BytesReceived = totalReceived;
			TotalBytesToReceive = fileSize;
			CurrentSpeed = (long)((continueReceived == 0 ? totalReceived : continueReceived) / totalSecond);
		}
		public string Message {get; set;}
		public long CurrentReceived {get; set;}
		public long BytesReceived {get; private set;}
		public long TotalBytesToReceive {get; private set;}
		public float ProgressPercentage => ((float)BytesReceived / (float)TotalBytesToReceive) * 100;
		public long CurrentSpeed {get; private set;}
		public TimeSpan TimeLeft => TimeSpan.FromSeconds((TotalBytesToReceive - BytesReceived) / CurrentSpeed);
	}

	public class DownloadProgressChanged : EventArgs
	{
		public DownloadProgressChanged(long totalReceived, long fileSize, double totalSecond)
		{
			BytesReceived = totalReceived;
			TotalBytesToReceive = fileSize;
			CurrentSpeed = (long)(totalReceived / totalSecond);
		}
		public string Message {get; set;}
		public long CurrentReceived {get; set;}
		public long BytesReceived {get; private set;}
		public long TotalBytesToReceive {get; private set;}
		public float ProgressPercentage => ((float)BytesReceived / (float)TotalBytesToReceive) * 100;
		public long CurrentSpeed {get; private set;}
		public TimeSpan TimeLeft => TimeSpan.FromSeconds((TotalBytesToReceive - BytesReceived) / CurrentSpeed);
	}
}
