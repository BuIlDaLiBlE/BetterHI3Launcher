using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Shell;
using Hi3Helper.Http;

namespace BetterHI3Launcher
{
	public partial class MainWindow : Window
	{
		float RefreshRate = 250f;
		Stopwatch LastTimeSpan = Stopwatch.StartNew();
		private void DownloadStatusChanged(object sender, DownloadEvent e)
		{
			if(LastTimeSpan.Elapsed.TotalMilliseconds >= RefreshRate)
			{
				Dispatcher.Invoke(() =>
				{
					double DownloadPercentage = Math.Round(double.IsInfinity(e.ProgressPercentage) ? 0 : e.ProgressPercentage, 2);
					DownloadProgressBar.Value = DownloadPercentage / 100;
					TaskbarItemInfo.ProgressValue = DownloadPercentage / 100;
					DownloadETAText.Text = string.Format(App.TextStrings["progresstext_eta"], string.Format("{0:hh\\:mm\\:ss}", e.TimeLeft));
					if(e.State == DownloadState.Merging)
					{
						DownloadProgressText.Text = $"{string.Format(App.TextStrings["label_merged"], $"{DownloadPercentage:0.00}")} ({BpUtility.ToBytesCount(e.SizeDownloaded)}/{BpUtility.ToBytesCount(e.SizeToBeDownloaded)})";
						DownloadSpeedText.Text = $"{App.TextStrings["label_merge_speed"]} {BpUtility.ToBytesCount(e.Speed)}{App.TextStrings["bytes_per_second"].Substring(1)}";
						DownloadPauseButton.Visibility = Visibility.Collapsed;
					}
					else
					{
						DownloadProgressText.Text = $"{string.Format(App.TextStrings["label_downloaded_1"], $"{DownloadPercentage:0.00}")} ({BpUtility.ToBytesCount(e.SizeDownloaded)}/{BpUtility.ToBytesCount(e.SizeToBeDownloaded)})";
						DownloadSpeedText.Text = $"{App.TextStrings["label_download_speed"]} {BpUtility.ToBytesCount(e.Speed)}{App.TextStrings["bytes_per_second"].Substring(1)}";
					}
				});
				LastTimeSpan = Stopwatch.StartNew();
			}
		}
		private void PreloadDownloadStatusChanged(object sender, DownloadEvent e)
		{
			if(LastTimeSpan.Elapsed.TotalMilliseconds >= RefreshRate)
			{
				Dispatcher.Invoke(() =>
				{
					double DownloadPercentage = Math.Round(double.IsInfinity(e.ProgressPercentage) ? 0 : e.ProgressPercentage, 2);
					PreloadCircleProgressBar.Value = DownloadPercentage / 100;
					TaskbarItemInfo.ProgressValue = DownloadPercentage / 100;
					PreloadStatusTopRightText.Text = $"{BpUtility.ToBytesCount(e.SizeDownloaded)}/{BpUtility.ToBytesCount(e.SizeToBeDownloaded)}";
					PreloadStatusMiddleRightText.Text = string.Format("{0:hh\\:mm\\:ss}", e.TimeLeft);
					PreloadStatusBottomRightText.Text = $"{BpUtility.ToBytesCount(e.Speed)}{App.TextStrings["bytes_per_second"].Substring(1)}";
					if(e.State == DownloadState.Merging)
					{
						PreloadPauseButton.IsEnabled = false;
						PreloadBottomText.Text = string.Format(App.TextStrings["label_merged"], $"{DownloadPercentage:0.00}");
						PreloadStatusTopLeftText.Text = $"{App.TextStrings["label_merged"].Split(' ')[0]}:";
						PreloadStatusBottomLeftText.Text = App.TextStrings["label_merge_speed"];
					}
					else
					{
						PreloadBottomText.Text = string.Format(App.TextStrings["label_downloaded_1"], $"{DownloadPercentage:0.00}");
						PreloadStatusTopLeftText.Text = App.TextStrings["label_downloaded_2"];
						PreloadStatusBottomLeftText.Text = App.TextStrings["label_download_speed"];
					}
				});
				LastTimeSpan = Stopwatch.StartNew();
			}
		}
	}
}
