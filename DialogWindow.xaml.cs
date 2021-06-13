using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BetterHI3Launcher
{
	public partial class DialogWindow : Window
	{
		public enum DialogType
		{
			Confirmation, Question, Uninstall, CustomBackground
		}

		public DialogWindow(string title, string message, DialogType type = DialogType.Confirmation)
		{
			Owner = Application.Current.MainWindow;
			Left = Application.Current.MainWindow.Left + 10;
			Top = Application.Current.MainWindow.Top + 10;
			InitializeComponent();
			DialogTitle.Text = title;
			DialogMessage.Text = message;
			ConfirmButton.Content = App.TextStrings["button_confirm"];
			CancelButton.Content = App.TextStrings["button_cancel"];
			switch(type)
			{
				case DialogType.Confirmation:
					CancelButton.Visibility = Visibility.Collapsed;
					break;
				case DialogType.Question:
					ConfirmButton.Margin = new Thickness(0, 0, 25, 0);
					ConfirmButton.Content = App.TextStrings["button_yes"];
					CancelButton.Content = App.TextStrings["button_no"];
					break;
				case DialogType.Uninstall:
					ConfirmButton.Margin = new Thickness(0, 0, 25, 0);
					DialogMessageScrollViewer.Margin = new Thickness(0, 0, 0, 100);
					DialogMessageScrollViewer.Height = 50;
					UninstallStackPanel.Visibility = Visibility.Visible;
					UninstallGameFilesLabel.Text = App.TextStrings["msgbox_uninstall_game_files"];
					UninstallGameCacheLabel.Text = App.TextStrings["msgbox_uninstall_game_cache"];
					UninstallGameSettingsLabel.Text = App.TextStrings["msgbox_uninstall_game_settings"];
					if(!Directory.Exists(MainWindow.GameCachePath))
					{
						UninstallGameCacheCheckBox.IsChecked = false;
						UninstallGameCacheCheckBox.IsEnabled = false;
					}
					if(Registry.CurrentUser.OpenSubKey(MainWindow.GameRegistryPath) == null)
					{
						UninstallGameSettingsCheckBox.IsChecked = false;
						UninstallGameSettingsCheckBox.IsEnabled = false;
					}
					break;
				case DialogType.CustomBackground:
					ConfirmButton.Margin = new Thickness(0, 0, 25, 0);
					DialogMessageScrollViewer.Margin = new Thickness(0, 0, 0, 75);
					DialogMessageScrollViewer.Height = 80;
					CustomBackgroundStackPanel.Visibility = Visibility.Visible;
					CustomBackgroundEditLabel.Text = App.TextStrings["msgbox_custom_background_edit"];
					CustomBackgroundDeleteLabel.Text = App.TextStrings["msgbox_custom_background_delete"];
					break;
			}
			if(App.LauncherLanguage != "en")
			{
				Resources["Font"] = new FontFamily("Segoe UI Bold");
			}
			Application.Current.MainWindow.WindowState = WindowState.Normal;
			BpUtility.PlaySound(Properties.Resources.Window_Open);
		}

		private void ConfirmButton_Click(object sender, RoutedEventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Click);
			DialogResult = true;
			Close();
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Click);
			Close();
		}

		private void UninstallCheckBox_Click(object sender, RoutedEventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Click);
			if(!(bool)UninstallGameFilesCheckBox.IsChecked && !(bool)UninstallGameCacheCheckBox.IsChecked && !(bool)UninstallGameSettingsCheckBox.IsChecked)
			{
				ConfirmButton.IsEnabled = false;
			}
			else
			{
				ConfirmButton.IsEnabled = true;
			}
		}

		private void CustomBackgroundRadioButton_Click(object sender, RoutedEventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Click);
		}

		private void DialogWindow_Closing(object sender, CancelEventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Window_Close);   
		}
	}
}