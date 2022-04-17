﻿using System;
using System.Threading;
using System.Threading.Tasks;
using static BetterHI3Launcher.Utility.HttpClientHelper;

namespace BetterHI3Launcher.Utility
{
	public class DownloadParallelAdapter : IDisposable
	{
		public struct DownloadProp
		{
			public string source, target;
		}

		private CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
		private CancellationToken cancelToken;
		private DownloadProp link;
		public HttpClientHelper client;
		public event EventHandler<DownloadChangedProgress> DownloadProgress;
		public bool Paused;
		private bool IsCompleted;
		private bool IsCanceled;

		public void InitializeDownload(string source, string target) => link = new DownloadProp{source = source, target = target};

		public void Start()
		{
			Paused = false;
			IsCompleted = false;
			IsCanceled = false;
			cancelToken = cancelTokenSource.Token;

			client = new HttpClientHelper(false);
            client.DownloadProgress += DownloadProgressAdapter;

			Task.Run(() =>
			{
				try
				{
					client.DownloadFile(link.source, link.target, 8, cancelToken);
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
				client.DownloadProgress -= DownloadProgressAdapter;
				throw new OperationCanceledException("Parallel downloader shutting down...");
			}
			client.DownloadProgress -= DownloadProgressAdapter;
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

		public async Task DisposeAndWait()
		{
			Stop();
			await WaitForComplete();
		}

		private void DownloadProgressAdapter(object sender, _DownloadProgress e)
		{
			UpdateProgress(new DownloadChangedProgress
			{
				Status = e.DownloadState,
				BytesReceived = e.DownloadedSize,
				CurrentReceived = e.CurrentRead,
				TotalBytesToReceive = e.TotalSizeToDownload,
				CurrentSpeed = e.CurrentSpeed,
				ProgressPercentage = e.ProgressPercentage,
				TimeLeft = e.TimeLeft
			});
		}

		protected virtual void UpdateProgress(DownloadChangedProgress e) => DownloadProgress?.Invoke(this, e);
	}

	public class DownloadChangedProgress : EventArgs
	{
		public DownloadState Status {get; set;}
		public long CurrentReceived {get; set;}
		public long BytesReceived {get; set;}
		public long TotalBytesToReceive {get; set;}
		public double ProgressPercentage {get; set;}
		public long CurrentSpeed {get; set;}
		public TimeSpan TimeLeft {get; set;}
	}
}
