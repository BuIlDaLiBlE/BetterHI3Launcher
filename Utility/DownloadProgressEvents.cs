using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;
using BetterHI3Launcher.Utility;

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
				DownloadProgressText.Text = $"{string.Format(App.TextStrings["label_downloaded_1"], DownloadPercentage)} ({BpUtility.ToBytesCount(e.BytesReceived)}/{BpUtility.ToBytesCount(e.TotalBytesToReceive)})";
				DownloadETAText.Text = string.Format(App.TextStrings["progresstext_eta"], string.Format("{0:hh\\:mm\\:ss}", e.TimeLeft));
				DownloadSpeedText.Text = $"{App.TextStrings["label_speed"]} {BpUtility.ToBytesCount(e.CurrentSpeed)}{App.TextStrings["bytes_per_second"].Substring(1)}";
			});
		}
		private void PreloadDownloadStatusChanged(object sender, DownloadChangedProgress e)
		{
			Dispatcher.Invoke(() =>
			{
				double DownloadPercentage = Math.Round(e.ProgressPercentage, 2);
				PreloadCircleProgressBar.Value = DownloadPercentage / 100;
				TaskbarItemInfo.ProgressValue = DownloadPercentage / 100;
				PreloadBottomText.Text = string.Format(App.TextStrings["label_downloaded_1"], DownloadPercentage);
				PreloadStatusTopRightText.Text = $"{BpUtility.ToBytesCount(e.BytesReceived)}/{BpUtility.ToBytesCount(e.TotalBytesToReceive)}";
				PreloadStatusMiddleRightText.Text = string.Format("{0:hh\\:mm\\:ss}", e.TimeLeft);
				PreloadStatusBottomRightText.Text = $"{BpUtility.ToBytesCount(e.CurrentSpeed)}{App.TextStrings["bytes_per_second"].Substring(1)}";
			});
		}
	}
}
