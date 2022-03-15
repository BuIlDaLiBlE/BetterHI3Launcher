using System;
using System.Windows;
using System.Windows.Shell;
using BetterHI3Launcher.Utility;
using static BetterHI3Launcher.Utility.ParallelHttpClient;

namespace BetterHI3Launcher
{
	public partial class MainWindow : Window
	{
		private void DownloadStatusChanged(object sender, DownloadChangedProgress e)
		{
			Dispatcher.Invoke(() =>
			{
				double DownloadPercentage = Math.Round(e.ProgressPercentage, 2);
				DownloadProgressBar.Value = DownloadPercentage / 100;
				TaskbarItemInfo.ProgressValue = DownloadPercentage / 100;
				DownloadETAText.Text = string.Format(App.TextStrings["progresstext_eta"], string.Format("{0:hh\\:mm\\:ss}", e.TimeLeft));
				if(e.Status == ParallelHttpClientStatus.Downloading)
				{
					DownloadProgressText.Text = $"{string.Format(App.TextStrings["label_downloaded_1"], DownloadPercentage)} ({BpUtility.ToBytesCount(e.BytesReceived)}/{BpUtility.ToBytesCount(e.TotalBytesToReceive)})";
					DownloadSpeedText.Text = $"{App.TextStrings["label_download_speed"]} {BpUtility.ToBytesCount(e.CurrentSpeed)}{App.TextStrings["bytes_per_second"].Substring(1)}";
				}
				else
				{
					DownloadProgressText.Text = $"{string.Format(App.TextStrings["label_merged"], DownloadPercentage)} ({BpUtility.ToBytesCount(e.BytesReceived)}/{BpUtility.ToBytesCount(e.TotalBytesToReceive)})";
					DownloadSpeedText.Text = $"{App.TextStrings["label_merge_speed"]} {BpUtility.ToBytesCount(e.CurrentSpeed)}{App.TextStrings["bytes_per_second"].Substring(1)}";
				}
			});
		}
		private void PreloadDownloadStatusChanged(object sender, DownloadChangedProgress e)
		{
			Dispatcher.Invoke(() =>
			{
				double DownloadPercentage = Math.Round(e.ProgressPercentage, 2);
				PreloadCircleProgressBar.Value = DownloadPercentage / 100;
				TaskbarItemInfo.ProgressValue = DownloadPercentage / 100;
				PreloadStatusTopRightText.Text = $"{BpUtility.ToBytesCount(e.BytesReceived)}/{BpUtility.ToBytesCount(e.TotalBytesToReceive)}";
				PreloadStatusMiddleRightText.Text = string.Format("{0:hh\\:mm\\:ss}", e.TimeLeft);
				PreloadStatusBottomRightText.Text = $"{BpUtility.ToBytesCount(e.CurrentSpeed)}{App.TextStrings["bytes_per_second"].Substring(1)}";
				if(e.Status == ParallelHttpClientStatus.Downloading)
				{
					PreloadBottomText.Text = string.Format(App.TextStrings["label_downloaded_1"], DownloadPercentage);
					PreloadStatusTopLeftText.Text = App.TextStrings["label_downloaded_2"];
					PreloadStatusBottomLeftText.Text = App.TextStrings["label_download_speed"];
				}
				else
				{
					PreloadBottomText.Text = string.Format(App.TextStrings["label_merged"], DownloadPercentage);
					PreloadStatusTopLeftText.Text = $"{App.TextStrings["label_merged"].Split(' ')[0]}:";
					PreloadStatusBottomLeftText.Text = App.TextStrings["label_merge_speed"];
				}
			});
		}
	}
}
