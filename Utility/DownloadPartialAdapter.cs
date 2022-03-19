using System;
using System.Threading;
using System.Threading.Tasks;
using static BetterHI3Launcher.Utility.ParallelHttpClient;

namespace BetterHI3Launcher.Utility
{
	public class DownloadParallelAdapter : IDisposable
	{
		public struct DownloadProp
		{
			public string source, target;
		}

		public DownloadParallelAdapter(bool DisableCompression = false, int RetryMaxCount = 5, int RetryDelay = 1)
		{
			this.DisableCompression = DisableCompression;
			this.RetryMaxCount = RetryMaxCount;
			this.RetryDelay = RetryDelay;
		}

		private CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
		private CancellationToken cancelToken;
		private DownloadProp link;
		public ParallelHttpClient client;
		public event EventHandler<DownloadChangedProgress> DownloadProgress;
		public bool Paused;
		private bool IsCompleted;
		private bool DisableCompression;
		private int RetryMaxCount, RetryDelay;

		public void InitializeDownload(string source, string target) => link = new DownloadProp{source = source, target = target};

		public void Start()
		{
			Paused = false;
			IsCompleted = false;
			cancelToken = cancelTokenSource.Token;

			client = new ParallelHttpClient(DisableCompression, RetryMaxCount, RetryDelay * 1000);
			client.PartialProgressChanged += DownloadProgressAdapter;

			Task.Run(() =>
			{
				try
				{
					client.DownloadFileMultipleSession(link.source, link.target, "", App.ParallelDownloadSessions, cancelToken);
					IsCompleted = true;
				}
				catch(OperationCanceledException){}
				catch(Exception ex)
				{
					throw new Exception("", ex);
				}
			});
		}

		public async Task WaitForComplete(int refresh = 1)
		{
			while(!IsCompleted)
			{
				await Task.Delay(refresh * 1000);
			}

			if(cancelToken.IsCancellationRequested)
			{
				client.PartialProgressChanged -= DownloadProgressAdapter;
				throw new OperationCanceledException("Parallel downloader shutting down...");
			}

			client.PartialProgressChanged -= DownloadProgressAdapter;
		}

		public void Resume()
		{
			cancelTokenSource = new CancellationTokenSource();
			Start();
		}

		public void Pause()
		{
			Paused = true;
			cancelTokenSource.Cancel();
		}

		public void Stop()
		{
			Pause();
			IsCompleted = true;
		}

		public void Dispose()
		{
			Stop();
		}

		public void DownloadProgressAdapter(object sender, PartialDownloadProgressChanged e)
		{
			#if DEBUG
			Console.Write($"\r{e.ProgressPercentage}");
			#endif
			UpdateProgress(new DownloadChangedProgress
			{
				Status = client.Status,
				BytesReceived = e.BytesReceived,
				CurrentReceived = e.CurrentReceived,
				TotalBytesToReceive = e.TotalBytesToReceive,
				CurrentSpeed = e.CurrentSpeed,
				ProgressPercentage = e.ProgressPercentage,
				TimeLeft = e.TimeLeft
			});
		}

		protected virtual void UpdateProgress(DownloadChangedProgress e) => DownloadProgress?.Invoke(this, e);
	}

	public class DownloadChangedProgress : EventArgs
	{
		public ParallelHttpClientStatus Status {get; set;}
		public long CurrentReceived {get; set;}
		public long BytesReceived {get; set;}
		public long TotalBytesToReceive {get; set;}
		public float ProgressPercentage {get; set;}
		public long CurrentSpeed {get; set;}
		public TimeSpan TimeLeft {get; set;}
	}
}
