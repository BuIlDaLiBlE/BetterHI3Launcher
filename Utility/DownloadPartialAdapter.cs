using System;
using System.Threading;
using System.Threading.Tasks;

namespace BetterHI3Launcher.Utility
{
	public class DownloadParallelAdapter : IDisposable
	{
		public struct DownloadProp
		{
			public string source, target;
		}

		CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
		CancellationToken cancelToken;

		public event EventHandler<DownloadChangedProgress> DownloadProgress;

		DownloadProp link;

		bool IsCompleted;
		ParallelHttpClient client;

		public void InitializeDownload(string source, string target) => link = new DownloadProp {source = source, target = target};

		public void Start()
		{
			IsCompleted = false;
			cancelToken = cancelTokenSource.Token;

			client = new ParallelHttpClient(false);
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
				throw new OperationCanceledException($"Parallel downloader has been shutdown!");
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
		public long CurrentReceived {get; set;}
		public long BytesReceived {get; set;}
		public long TotalBytesToReceive {get; set;}
		public float ProgressPercentage {get; set;}
		public long CurrentSpeed {get; set;}
		public TimeSpan TimeLeft {get; set;}
	}
}
