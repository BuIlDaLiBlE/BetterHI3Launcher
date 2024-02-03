using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BetterHI3Launcher
{
	public partial class DialogWindow : Window
	{
		public enum DialogType
		{
			Confirmation, Question, Install, Uninstall, CustomLaunchOptions, CustomBackground
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
					ConfirmButton.Content = App.TextStrings["button_ok"];
					CancelButton.Visibility = Visibility.Collapsed;
					break;
				case DialogType.Question:
					ConfirmButton.Content = App.TextStrings["button_yes"];
					CancelButton.Content = App.TextStrings["button_no"];
					break;
				case DialogType.Install:
					DialogMessageScrollViewer.Margin = new Thickness(0, 0, 0, 75);
					DialogMessageScrollViewer.Height = 100;
					InstallStackPanel.Visibility = Visibility.Visible;
					break;
				case DialogType.Uninstall:
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
				case DialogType.CustomLaunchOptions:
					DialogMessageScrollViewer.Margin = new Thickness(0, 0, 0, 75);
					DialogMessageScrollViewer.Height = 100;
					CustomLaunchOptionsStackPanel.Visibility = Visibility.Visible;
					break;
				case DialogType.CustomBackground:
					DialogMessageScrollViewer.Margin = new Thickness(0, 0, 0, 75);
					DialogMessageScrollViewer.Height = 80;
					CustomBackgroundStackPanel.Visibility = Visibility.Visible;
					CustomBackgroundEditLabel.Text = App.TextStrings["msgbox_custom_background_edit"];
					CustomBackgroundDeleteLabel.Text = App.TextStrings["msgbox_custom_background_delete"];
					break;
			}
			switch(App.LauncherLanguage)
			{
				case "en":
				case "zh-CN":
					break;
				case "ja":
					Resources["Font"] = new FontFamily("Meiryo UI Bold");
					break;
				default:
					Resources["Font"] = new FontFamily("Segoe UI Bold");
					break;
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

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Click);
			var dialog = new CommonOpenFileDialog
			{
				IsFolderPicker = true,
				Multiselect = false,
				DefaultDirectory = InstallPathTextBox.Text,
				AddToMostRecentlyUsedList = false,
				AllowNonFileSystemItems = false,
				EnsurePathExists = true,
				EnsureReadOnly = false,
				EnsureValidNames = true
			};
			if(dialog.ShowDialog(this) == CommonFileDialogResult.Ok)
			{
				InstallPathTextBox.Text = Path.Combine(dialog.FileName, MainWindow.GameFullName);
				string[] game_full_names = {"Honkai Impact 3rd", "Honkai Impact 3", "崩坏3", "崩壞3", "붕괴3rd", "Honkai Impact 3rd glb", "Honkai Impact 3 sea", "Honkai Impact 3rd tw", "Honkai Impact 3rd kr"};
				foreach(string game_full_name in game_full_names)
				{
					if(dialog.FileName.Contains(game_full_name))
					{
						InstallPathTextBox.Text = dialog.FileName;
						break;
					}
				}
			}
		}

		private void InstallPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if(string.IsNullOrEmpty(InstallPathTextBox.Text))
			{
				ConfirmButton.IsEnabled = false;
			}
			else
			{
				ConfirmButton.IsEnabled = true;
			}
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

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if(Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Space)
			{
				e.Handled = true;
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		private void DialogWindow_Closing(object sender, CancelEventArgs e)
		{
			BpUtility.PlaySound(Properties.Resources.Window_Close);   
		}
	}
}