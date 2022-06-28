using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Http;

namespace BetterHI3Launcher.Utility
{
	public class DownloadParallelAdapter : Http
	{
		public struct DownloadProp
		{
			public string source, target;
		}

		private CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
		private CancellationToken cancelToken;
		private DownloadProp link;
		public event EventHandler<DownloadChangedProgress> DownloadProgress;
		public bool Paused;
		private bool IsCompleted;
		private bool IsCanceled;
		private Stopwatch InnerProgressStopwatch;
		private TimeSpan LastTimeSpan;

		public void InitializeDownload(string source, string target) => link = new DownloadProp{source = source, target = target};

		public void Start()
		{
			Paused = false;
			IsCompleted = false;
			IsCanceled = false;
			cancelToken = cancelTokenSource.Token;
			
			base.DownloadProgress += DownloadProgressAdapter;

			Task.Run(async () =>
			{
				try
				{
					InnerProgressStopwatch = Stopwatch.StartNew();
					LastTimeSpan = InnerProgressStopwatch.Elapsed;
					await DownloadMultisession(link.source, link.target, false, (byte)App.ParallelDownloadSessions, cancelToken);
					IsCompleted = true;
				}
				catch(OperationCanceledException)
				{
					IsCanceled = true;
				}
				catch(Exception ex)
				{
					throw new Exception("", ex);
				}
			});
		}
		
		public async Task WaitForComplete(int refresh = 1)
		{
			while(!(IsCompleted || IsCanceled))
			{
				await Task.Delay(refresh * 100);
			}

			if(cancelToken.IsCancellationRequested)
			{
				base.DownloadProgress -= DownloadProgressAdapter;
				throw new OperationCanceledException("Parallel downloader shutting down...");
			}
			base.DownloadProgress -= DownloadProgressAdapter;
		}

		public void Resume()
		{
			cancelTokenSource = new CancellationTokenSource();
			Start();
		}

		public async Task ResumeAndWait()
        {
			Resume();
			while (SessionState != MultisessionState.Downloading)
				await Task.Delay(125);
		}

		public void Pause()
		{
			Paused = true;
			cancelTokenSource.Cancel();
		}

		public void Stop()
		{
			Pause();
		}

		public async Task StopAndWait()
        {
			Stop();
			try
            {
				await WaitForComplete();
			}
			catch (OperationCanceledException) { }
        }

		public void Dispose()
		{
			Stop();
		}

		public async Task DisposeAndWait()
		{
			cancelTokenSource.Cancel();
			await WaitForComplete();
		}

		private void DownloadProgressAdapter(object sender, DownloadEvent e)
		{
			UpdateProgress(new DownloadChangedProgress
			{
				Status = e.State,
				BytesReceived = e.SizeDownloaded,
				CurrentReceived = e.Read,
				TotalBytesToReceive = e.SizeToBeDownloaded,
				CurrentSpeed = e.Speed,
				ProgressPercentage = e.ProgressPercentage,
				TimeLeft = GetLastTimeSpan(InnerProgressStopwatch, e.TimeLeft)
			});
		}

		private TimeSpan GetLastTimeSpan(Stopwatch sw, TimeSpan ts)
		{
			if (sw.ElapsedMilliseconds >= 2000)
			{
				InnerProgressStopwatch = Stopwatch.StartNew();
				LastTimeSpan = ts;
			}
			return LastTimeSpan;
		}

		protected virtual void UpdateProgress(DownloadChangedProgress e) => DownloadProgress?.Invoke(this, e);
	}

	public class DownloadChangedProgress : EventArgs
	{
		public MultisessionState Status {get; set;}
		public long CurrentReceived {get; set;}
		public long BytesReceived {get; set;}
		public long TotalBytesToReceive {get; set;}
		public double ProgressPercentage {get; set;}
		public long CurrentSpeed {get; set;}
		public TimeSpan TimeLeft {get; set;}
	}
}
