using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace BetterHI3Launcher
{
    enum LauncherStatus
    {
        Ready, Error, CheckingUpdates, Downloading, Updating, Verifying, Unpacking, CleaningUp, UpdateAvailable, Uninstalling, Working, DownloadPaused
    }
    enum HI3Server
    {
        Global, SEA
    }
    enum HI3Mirror
    {
        miHoYo, MediaFire, GoogleDrive
    }

    public partial class MainWindow : Window
    {
        public static readonly Version localLauncherVersion = new Version("1.0.20210225.0");
        public static readonly string rootPath = Directory.GetCurrentDirectory();
        public static readonly string localLowPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}Low";
        public static readonly string launcherDataPath = Path.Combine(localLowPath, @"Bp\Better HI3 Launcher");
        public static readonly string miHoYoPath = Path.Combine(localLowPath, "miHoYo");
        public static readonly string gameExeName = "BH3.exe";
        public static readonly string OSLanguage = CultureInfo.CurrentUICulture.ToString();
        public static readonly string userAgent = $"BetterHI3Launcher v{localLauncherVersion}";
        public static string LauncherLanguage;
        public static string gameInstallPath, gameArchivePath, gameArchiveName, gameExePath, cacheArchivePath, launcherExeName, launcherPath, launcherArchivePath, gameWebProfileURL;
        public static string RegistryVersionInfo;
        public static string GameRegistryPath, GameRegistryLocalVersionRegValue;
        public static bool DownloadPaused = false;
        public static Dictionary<string, string> textStrings = new Dictionary<string, string>();
        public dynamic localVersionInfo, onlineVersionInfo, miHoYoVersionInfo, gameGraphicSettings, gameCacheMetadata, gameCacheMetadataNumeric;
        LauncherStatus _status;
        HI3Server _gameserver;
        HI3Mirror _downloadmirror;
        RegistryKey LauncherRegKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Bp\Better HI3 Launcher");
        TimedWebClient webClient = new TimedWebClient { Encoding = Encoding.UTF8, Timeout = 10000 };
        DownloadPauseable download;
        DownloadProgressTracker tracker = new DownloadProgressTracker(50, TimeSpan.FromMilliseconds(500));

        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                Dispatcher.Invoke(() =>
                {
                    void ToggleUI(bool val)
                    {
                        LaunchButton.IsEnabled = val;
                        OptionsButton.IsEnabled = val;
                        ServerDropdown.IsEnabled = val;
                        MirrorDropdown.IsEnabled = val;
                        ToggleContextMenuItems(val, false);
                    }
                    void ToggleProgressBar(bool val)
                    {
                        ProgressBar.Visibility = val ? Visibility.Visible : Visibility.Hidden;
                        ProgressBar.IsIndeterminate = true;
                        TaskbarItemInfo.ProgressState = val ? TaskbarItemProgressState.Indeterminate : TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    }

                    _status = value;
                    WindowState = WindowState.Normal;
                    switch (_status)
                    {
                        case LauncherStatus.Ready:
                            ProgressText.Text = string.Empty;
                            ToggleUI(true);
                            ToggleProgressBar(false);
                            break;
                        case LauncherStatus.Error:
                            ProgressText.Text = textStrings["progresstext_error"];
                            ToggleUI(false);
                            ToggleProgressBar(false);
                            ShowLogCheckBox.IsChecked = true;
                            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;
                            break;
                        case LauncherStatus.CheckingUpdates:
                            ProgressText.Text = textStrings["progresstext_checkingupdate"];
                            ToggleUI(false);
                            ToggleProgressBar(true);
                            break;
                        case LauncherStatus.Downloading:
                            LaunchButton.Content = textStrings["button_downloading"];
                            ProgressText.Text = textStrings["progresstext_initiating_download"];
                            ToggleUI(false);
                            ToggleProgressBar(true);
                            ProgressBar.IsIndeterminate = false;
                            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                            break;
                        case LauncherStatus.DownloadPaused:
                            ProgressText.Text = string.Empty;
                            ToggleUI(true);
                            ToggleProgressBar(false);
                            ToggleContextMenuItems(false, false);
                            break;
                        case LauncherStatus.Working:
                            ToggleUI(false);
                            ToggleProgressBar(true);
                            break;
                        case LauncherStatus.Verifying:
                            ProgressText.Text = textStrings["progresstext_verifying"];
                            ToggleUI(false);
                            ToggleProgressBar(true);
                            break;
                        case LauncherStatus.Unpacking:
                            ProgressText.Text = textStrings["progresstext_unpacking_1"];
                            ToggleProgressBar(true);
                            ProgressBar.IsIndeterminate = false;
                            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                            break;
                        case LauncherStatus.CleaningUp:
                            ProgressText.Text = textStrings["progresstext_cleaningup"];
                            break;
                        case LauncherStatus.UpdateAvailable:
                            ToggleUI(true);
                            ToggleProgressBar(false);
                            ToggleContextMenuItems(false, true);
                            break;
                        case LauncherStatus.Uninstalling:
                            ProgressText.Text = textStrings["progresstext_uninstalling"];
                            ToggleUI(false);
                            ToggleProgressBar(true);
                            break;
                    }
                });
            }
        }
        internal HI3Server Server
        {
            get => _gameserver;
            set
            {
                _gameserver = value;
                switch (_gameserver)
                {
                    case HI3Server.Global:
                        RegistryVersionInfo = "VersionInfoGlobal";
                        GameRegistryPath = @"SOFTWARE\miHoYo\Honkai Impact 3rd";
                        gameWebProfileURL = "https://global.user.honkaiimpact3.com";
                        break;
                    case HI3Server.SEA:
                        RegistryVersionInfo = "VersionInfoSEA";
                        GameRegistryPath = @"SOFTWARE\miHoYo\Honkai Impact 3";
                        gameWebProfileURL = "https://asia.user.honkaiimpact3.com";
                        break;
                }
                GameRegistryLocalVersionRegValue = null;
                var regkey = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                if (regkey != null)
                {
                    foreach (string regvalue in regkey.GetValueNames())
                    {
                        if (regvalue.Contains("LocalVersion_h"))
                        {
                            GameRegistryLocalVersionRegValue = regvalue;
                            break;
                        }
                    }
                    regkey.Close();
                }
            }
        }
        internal HI3Mirror Mirror
        {
            get => _downloadmirror;
            set
            {
                _downloadmirror = value;
            }
        }

        public MainWindow()
        {
            void SetLanguage(string lang)
            {
                switch (lang)
                {
                    case "ru":
                        LauncherLanguage = lang;
                        Resources["Font"] = new FontFamily("Arial Bold");
                        TextStrings_Russian();
                        break;
                    default:
                        LauncherLanguage = "en";
                        TextStrings_English();
                        break;
                }
            }
#if DEBUG
            WinConsole.Initialize();
#endif
            InitializeComponent();
            Log($"BetterHI3Launcher v{localLauncherVersion}");
            //Log($"Launcher exe name: {Process.GetCurrentProcess().MainModule.ModuleName}");
            //Log($"Launcher exe MD5: {BpUtility.CalculateMD5(Path.Combine(rootPath, Process.GetCurrentProcess().MainModule.ModuleName))}");
            Log($"Working directory: {rootPath}");
            Log($"OS language: {OSLanguage}");
            SetLanguage(null);
            switch (OSLanguage)
            {
                case "ru-RU":
                case "uk-UA":
                case "be-BY":
                    LauncherLanguage = "ru";
                    break;
            }
            var LanguageRegValue = LauncherRegKey.GetValue("Language");
            if (LanguageRegValue != null)
            {
                if (LauncherRegKey.GetValueKind("Language") == RegistryValueKind.String)
                {
                    SetLanguage(LanguageRegValue.ToString());
                }
            }
            else
            {
                SetLanguage(LauncherLanguage);
            }
            Log($"Launcher language: {LauncherLanguage}");
            LaunchButton.Content = textStrings["button_download"];
            OptionsButton.Content = textStrings["button_options"];
            ServerLabel.Text = $"{textStrings["label_server"]}:";
            MirrorLabel.Text = $"{textStrings["label_mirror"]}:";
            DownloadCacheBoxTitleTextBlock.Text = textStrings["contextmenu_downloadcache"];
            DownloadCacheBoxFullCacheButton.Content = textStrings["downloadcachebox_button_full_cache"];
            DownloadCacheBoxNumericFilesButton.Content = textStrings["downloadcachebox_button_numeric_files"];
            DownloadCacheBoxCancelButton.Content = textStrings["button_cancel"];
            FPSInputBoxOKButton.Content = textStrings["button_confirm"];
            FPSInputBoxCancelButton.Content = textStrings["button_cancel"];
            ResolutionInputBoxOKButton.Content = textStrings["button_confirm"];
            ResolutionInputBoxCancelButton.Content = textStrings["button_cancel"];
            ChangelogBoxTitleTextBlock.Text = textStrings["changelogbox_title"];
            ChangelogBoxMessageTextBlock.Text = textStrings["changelogbox_msg"];
            ChangelogBoxOKButton.Content = textStrings["button_ok"];
            AboutBoxTitleTextBlock.Text = textStrings["contextmenu_about"];
            AboutBoxMessageTextBlock.Text = $"{textStrings["aboutbox_msg"]}\nMade by Bp (BuIlDaLiBlE production).\nDiscord: BuIlDaLiBlE#3202";
            AboutBoxGitHubButton.Content = textStrings["button_github"];
            AboutBoxOKButton.Content = textStrings["button_ok"];
            ShowLogLabel.Text = textStrings["label_log"];

            Grid.MouseLeftButtonDown += delegate { DragMove(); };
            LogBox.Visibility = Visibility.Collapsed;
            FPSInputBox.Visibility = Visibility.Collapsed;
            ResolutionInputBox.Visibility = Visibility.Collapsed;
            DownloadCacheBox.Visibility = Visibility.Collapsed;
            ChangelogBox.Visibility = Visibility.Collapsed;
            AboutBox.Visibility = Visibility.Collapsed;

            OptionsContextMenu.Items.Clear();
            var CMDownloadCache = new MenuItem { Header = textStrings["contextmenu_downloadcache"] };
            CMDownloadCache.Click += async (sender, e) => await CM_DownloadCache_Click(sender, e);
            OptionsContextMenu.Items.Add(CMDownloadCache);
            var CMUninstall = new MenuItem { Header = textStrings["contextmenu_uninstall"] };
            CMUninstall.Click += async (sender, e) => await CM_Uninstall_Click(sender, e);
            OptionsContextMenu.Items.Add(CMUninstall);
            OptionsContextMenu.Items.Add(new Separator());
            var CMFixSubtitles = new MenuItem { Header = textStrings["contextmenu_fixsubs"] };
            CMFixSubtitles.Click += async (sender, e) => await CM_FixSubtitles_Click(sender, e);
            OptionsContextMenu.Items.Add(CMFixSubtitles);
            var CMFixUpdateLoop = new MenuItem { Header = textStrings["contextmenu_download_type"] };
            CMFixUpdateLoop.Click += (sender, e) => CM_FixUpdateLoop_Click(sender, e);
            OptionsContextMenu.Items.Add(CMFixUpdateLoop);
            var CMCustomFPS = new MenuItem { Header = textStrings["contextmenu_customfps"] };
            CMCustomFPS.Click += (sender, e) => CM_CustomFPS_Click(sender, e);
            OptionsContextMenu.Items.Add(CMCustomFPS);
            var CMCustomResolution = new MenuItem { Header = textStrings["contextmenu_customresolution"] };
            CMCustomResolution.Click += (sender, e) => CM_CustomResolution_Click(sender, e);
            OptionsContextMenu.Items.Add(CMCustomResolution);
            var CMResetGameSettings = new MenuItem { Header = textStrings["contextmenu_resetgamesettings"] };
            CMResetGameSettings.Click += (sender, e) => CM_ResetGameSettings_Click(sender, e);
            OptionsContextMenu.Items.Add(CMResetGameSettings);
            OptionsContextMenu.Items.Add(new Separator());
            var CMWebProfile = new MenuItem { Header = textStrings["contextmenu_web_profile"] };
            CMWebProfile.Click += (sender, e) => CM_WebProfile_Click(sender, e);
            OptionsContextMenu.Items.Add(CMWebProfile);
            var CMFeedback = new MenuItem { Header = textStrings["contextmenu_feedback"] };
            CMFeedback.Click += (sender, e) => CM_Feedback_Click(sender, e);
            OptionsContextMenu.Items.Add(CMFeedback);
            var CMChangelog = new MenuItem { Header = textStrings["contextmenu_changelog"] };
            CMChangelog.Click += (sender, e) => CM_Changelog_Click(sender, e);
            OptionsContextMenu.Items.Add(CMChangelog);
            var CMAbout = new MenuItem { Header = textStrings["contextmenu_about"] };
            CMAbout.Click += (sender, e) => CM_About_Click(sender, e);
            OptionsContextMenu.Items.Add(CMAbout);

            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            if (key == null || (int)key.GetValue("Release") < 393295)
            {
                if (MessageBox.Show(textStrings["msgbox_net_version_old_msg"], textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }

            try
            {
                var LastSelectedServerRegValue = LauncherRegKey.GetValue("LastSelectedServer");
                if (LastSelectedServerRegValue != null)
                {
                    if (LauncherRegKey.GetValueKind("LastSelectedServer") == RegistryValueKind.DWord)
                    {
                        if ((int)LastSelectedServerRegValue == 0)
                            Server = HI3Server.Global;
                        else if ((int)LastSelectedServerRegValue == 1)
                            Server = HI3Server.SEA;
                    }
                }
                else
                {
                    Server = HI3Server.Global;
                }
                ServerDropdown.SelectedIndex = (int)Server;

                try
                {
                    FetchOnlineVersionInfo();
                    FetchmiHoYoVersionInfo();
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.Error;
                    if (MessageBox.Show($"{textStrings["msgbox_neterror_msg"]}:\n{ex}", textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }
                var LastSelectedMirrorRegValue = LauncherRegKey.GetValue("LastSelectedMirror");
                if (LastSelectedMirrorRegValue != null)
                {
                    if (LauncherRegKey.GetValueKind("LastSelectedMirror") == RegistryValueKind.DWord)
                    {
                        if ((int)LastSelectedMirrorRegValue == 0)
                            Mirror = HI3Mirror.miHoYo;
                        else if ((int)LastSelectedMirrorRegValue == 1)
                            Mirror = HI3Mirror.MediaFire;
                        else if ((int)LastSelectedMirrorRegValue == 2)
                            Mirror = HI3Mirror.GoogleDrive;
                    }
                }
                else
                {
                    Mirror = HI3Mirror.miHoYo;
                }
                MirrorDropdown.SelectedIndex = (int)Mirror;

                var ShowLogRegValue = LauncherRegKey.GetValue("ShowLog");
                if (ShowLogRegValue != null)
                {
                    if (LauncherRegKey.GetValueKind("ShowLog") == RegistryValueKind.DWord)
                    {
                        if ((int)ShowLogRegValue == 1)
                            ShowLogCheckBox.IsChecked = true;
                    }
                }
                Log($"Using server: {((ComboBoxItem)ServerDropdown.SelectedItem).Content as string}");
                Log($"Using mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");
                DownloadBackgroundImage();
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                if (MessageBox.Show(string.Format(textStrings["msgbox_starterror_msg"], ex), textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }
        }

        private void FetchOnlineVersionInfo()
        {
#if DEBUG
            var version_info_url = new[] { "https://bpnet.host/bh3_debug.json" };
#else
                var version_info_url = new[]{"https://bpnet.host/bh3?launcherstatus", "https://serioussam.ucoz.ru/bbh3l_prod.json"};
#endif
            string version_info;
            webClient.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
            try
            {
                version_info = webClient.DownloadString(version_info_url[0]);
            }
            catch
            {
                version_info = webClient.DownloadString(version_info_url[1]);
            }
            onlineVersionInfo = JsonConvert.DeserializeObject<dynamic>(version_info);
            if (onlineVersionInfo.status == "success")
            {
                onlineVersionInfo = onlineVersionInfo.launcherstatus;
                launcherExeName = onlineVersionInfo.launcher_info.name;
                launcherPath = Path.Combine(rootPath, launcherExeName);
                launcherArchivePath = Path.Combine(rootPath, onlineVersionInfo.launcher_info.url.ToString().Substring(onlineVersionInfo.launcher_info.url.ToString().LastIndexOf('/') + 1));
                Dispatcher.Invoke(() =>
                {
                    LauncherVersionText.Text = $"{textStrings["launcher_version"]}: v{localLauncherVersion}";
                    ChangelogBoxTextBox.Text = onlineVersionInfo.launcher_info.changelog[LauncherLanguage];
                    ShowLogStackPanel.Margin = new Thickness((double)onlineVersionInfo.launcher_info.ui.ShowLogStackPanel_Margin.left, 0, 0, (double)onlineVersionInfo.launcher_info.ui.ShowLogStackPanel_Margin.bottom);
                    LogBox.Margin = new Thickness((double)onlineVersionInfo.launcher_info.ui.LogBox_Margin.left, (double)onlineVersionInfo.launcher_info.ui.LogBox_Margin.top, (double)onlineVersionInfo.launcher_info.ui.LogBox_Margin.right, (double)onlineVersionInfo.launcher_info.ui.LogBox_Margin.bottom);
                });
            }
            else
            {
                Status = LauncherStatus.Error;
                MessageBox.Show($"{textStrings["msgbox_neterror_msg"]}.", textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FetchOnlineVersionInfoAsync()
        {
            await Task.Run(() =>
            {
                FetchOnlineVersionInfo();
            });
        }

        private void FetchmiHoYoVersionInfo()
        {
            string url;
            if (Server == HI3Server.Global)
                url = onlineVersionInfo.game_info.mirror.mihoyo.version_info.global.ToString();
            else
                url = onlineVersionInfo.game_info.mirror.mihoyo.version_info.os.ToString();
            var webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.UserAgent = userAgent;
            webRequest.Timeout = 30000;
            using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                using (var data = new MemoryStream())
                {
                    webResponse.GetResponseStream().CopyTo(data);
                    miHoYoVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                    miHoYoVersionInfo.last_modified = webResponse.LastModified.ToUniversalTime().ToString();
                }
            }
            gameArchiveName = miHoYoVersionInfo.full_version_file.name.ToString();
            webRequest = (HttpWebRequest)WebRequest.Create($"{miHoYoVersionInfo.download_url}/{gameArchiveName}");
            webRequest.Method = "HEAD";
            webRequest.UserAgent = userAgent;
            webRequest.Timeout = 30000;
            using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                miHoYoVersionInfo.size = webResponse.ContentLength;
            }
            Dispatcher.Invoke(() =>
            {
                GameVersionText.Visibility = Visibility.Visible;
                GameVersionText.Text = $"{textStrings["version"]}: v{miHoYoVersionInfo.cur_version.ToString()}";
            });
        }

        private async Task FetchmiHoYoVersionInfoAsync()
        {
            await Task.Run(() =>
            {
                FetchmiHoYoVersionInfo();
            });
        }

        private DateTime FetchmiHoYoResourceVersionDateModified()
        {
            var url = new string[2];
            var time = new DateTime[2];
            if (Server == HI3Server.Global)
            {
                url[0] = onlineVersionInfo.game_info.mirror.mihoyo.resource_version.global[0].ToString();
                url[1] = onlineVersionInfo.game_info.mirror.mihoyo.resource_version.global[1].ToString();
            }
            else
            {
                url[0] = onlineVersionInfo.game_info.mirror.mihoyo.resource_version.os[0].ToString();
                url[1] = onlineVersionInfo.game_info.mirror.mihoyo.resource_version.os[1].ToString();
            }
            try
            {
                for (int i = 0; i < url.Length; i++)
                {
                    var webRequest = (HttpWebRequest)WebRequest.Create(url[i]);
                    webRequest.Method = "HEAD";
                    webRequest.UserAgent = userAgent;
                    webRequest.Timeout = 30000;
                    using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
                    {
                        time[i] = webResponse.LastModified.ToUniversalTime();
                    }
                }
                if (DateTime.Compare(time[0], time[1]) >= 0)
                    return time[0];
                else
                    return time[1];
            }
            catch
            {
                return new DateTime(0);
            }
        }

        private dynamic FetchMediaFireFileMetadata(string id, bool numeric)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException();

            string url = $"https://www.mediafire.com/file/{id}";
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(url);
                webRequest.Method = "HEAD";
                webRequest.UserAgent = userAgent;
                webRequest.Timeout = 30000;
                using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    dynamic metadata = new ExpandoObject();
                    metadata.title = webResponse.Headers["Content-Disposition"].Replace("attachment; filename=", string.Empty).Replace("\"", string.Empty);
                    metadata.modifiedDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                    metadata.downloadUrl = url;
                    metadata.fileSize = webResponse.ContentLength;
                    if (!numeric)
                    {
                        if (Server == HI3Server.Global)
                            metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache.global.md5.ToString();
                        else
                            metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache.os.md5.ToString();
                    }
                    else
                    {
                        if (Server == HI3Server.Global)
                            metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.md5.ToString();
                        else
                            metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.md5.ToString();
                    }
                    return metadata;
                }
            }
            catch (WebException ex)
            {
                Status = LauncherStatus.Error;
                if (ex.Response != null)
                {
                    string msg = ex.Message;
                    Log($"ERROR: Failed to fetch MediaFire file metadata:\n{msg}");
                    Dispatcher.Invoke(() => { MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], msg), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error); });
                    Status = LauncherStatus.Ready;
                }
                return null;
            }
        }

        private dynamic FetchGDFileMetadata(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException();

            string url = $"https://www.googleapis.com/drive/v2/files/{id}?key={onlineVersionInfo.launcher_info.gd_key}";
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(url);
                webRequest.UserAgent = userAgent;
                webRequest.Timeout = 30000;
                using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    using (var data = new MemoryStream())
                    {
                        webResponse.GetResponseStream().CopyTo(data);
                        var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                        return json;
                    }
                }
            }
            catch (WebException ex)
            {
                Status = LauncherStatus.Error;
                if (ex.Response != null)
                {
                    using (var data = new MemoryStream())
                    {
                        ex.Response.GetResponseStream().CopyTo(data);
                        var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                        string msg;
                        if (json.error != null)
                            msg = json.error.errors[0].message;
                        else
                            msg = ex.Message;
                        Log($"ERROR: Failed to fetch Google Drive file metadata:\n{msg}");
                        Dispatcher.Invoke(() => { MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], msg), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error); });
                        Status = LauncherStatus.Ready;
                    }
                }
                return null;
            }
        }

        private bool LauncherUpdateCheck()
        {
            Version onlineLauncherVersion = new Version(onlineVersionInfo.launcher_info.version.ToString());
            if (localLauncherVersion.IsDifferentThan(onlineLauncherVersion))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async void GameUpdateCheck(bool serverChanged)
        {
            if (Status == LauncherStatus.Error)
                return;

            Status = LauncherStatus.CheckingUpdates;
            Log("Checking for game update...");
            localVersionInfo = null;
            await Task.Run(async () =>
            {
                await FetchOnlineVersionInfoAsync();
                try
                {
                    bool game_needs_update = false;
                    long download_size = 0;
                    if (Mirror == HI3Mirror.miHoYo)
                    {
                        // space_usage is probably when archive is unpacked, here I get the download size instead
                        //download_size = miHoYoVersionInfo.space_usage;
                        download_size = miHoYoVersionInfo.size;
                    }
                    else if (Mirror == HI3Mirror.MediaFire)
                    {
                        dynamic mediafire_metadata;
                        if (Server == HI3Server.Global)
                            mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString(), false);
                        else
                            mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString(), false);
                        if (mediafire_metadata == null)
                            return;
                        download_size = mediafire_metadata.fileSize;
                    }
                    else if (Mirror == HI3Mirror.GoogleDrive)
                    {
                        dynamic gd_metadata;
                        if (Server == HI3Server.Global)
                            gd_metadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
                        else
                            gd_metadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
                        if (gd_metadata == null)
                            return;
                        download_size = gd_metadata.fileSize;
                    }
                    if (LauncherRegKey.GetValue(RegistryVersionInfo) != null)
                    {
                        localVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])LauncherRegKey.GetValue(RegistryVersionInfo)));
                        GameVersion localGameVersion = new GameVersion(localVersionInfo.game_info.version.ToString());
                        gameInstallPath = localVersionInfo.game_info.install_path.ToString();
                        gameArchivePath = Path.Combine(gameInstallPath, gameArchiveName);
                        gameExePath = Path.Combine(gameInstallPath, "BH3.exe");
                        game_needs_update = GameUpdateCheckSimple();
                        Log($"Game version: {localGameVersion}");

                        if (game_needs_update)
                        {
                            Log("Game requires an update!");
                            Status = LauncherStatus.UpdateAvailable;
                            Dispatcher.Invoke(() =>
                            {
                                LaunchButton.Content = textStrings["button_update"];
                                ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {BpUtility.ToBytesCount(download_size)}";
                            });
                        }
                        else
                        {
                            Status = LauncherStatus.Ready;
                            Dispatcher.Invoke(() =>
                            {
                                LaunchButton.Content = textStrings["button_launch"];
                                ToggleContextMenuItems(true, false);
                            });
                        }
                        if (File.Exists(gameArchivePath))
                        {
                            DownloadPaused = true;
                            var remaining_size = download_size - new FileInfo(gameArchivePath).Length;
                            Dispatcher.Invoke(() =>
                            {
                                if (remaining_size <= 0)
                                {
                                    LaunchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                }
                                else
                                {
                                    LaunchButton.Content = textStrings["button_resume"];
                                    ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {BpUtility.ToBytesCount(remaining_size)}";
                                    ToggleContextMenuItems(false, true);
                                }
                            });
                        }
                    }
                    else
                    {
                        Log("Game is not installed :^(");
                        if (serverChanged)
                            await FetchmiHoYoVersionInfoAsync();
                        Status = LauncherStatus.Ready;
                        Dispatcher.Invoke(() =>
                        {
                            LaunchButton.Content = textStrings["button_download"];
                            ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {BpUtility.ToBytesCount(download_size)}";
                            ToggleContextMenuItems(false, false);
                            var path = CheckForExistingGameDirectory(rootPath);
                            if (string.IsNullOrEmpty(path))
                                path = CheckForExistingGameDirectory(Environment.ExpandEnvironmentVariables("%ProgramW6432%"));
                            if (!string.IsNullOrEmpty(path))
                            {
                                if (MessageBox.Show(string.Format(textStrings["msgbox_installexisting_msg"], path), textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                                {
                                    Log($"Existing install directory selected: {path}");
                                    gameInstallPath = path;
                                    var server = CheckForExistingGameClientServer();
                                    if (server >= 0)
                                    {
                                        if ((int)Server != server)
                                            ServerDropdown.SelectedIndex = server;
                                        WriteVersionInfo(true);
                                        GameUpdateCheck(false);
                                    }
                                    else
                                    {
                                        Status = LauncherStatus.Error;
                                        Log($"ERROR: Directory {gameInstallPath} doesn't contain a valid installation of the game.\nThis launcher only supports Global and SEA clients!");
                                        if (MessageBox.Show(string.Format(textStrings["msgbox_installexistinginvalid_msg"]), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                                        {
                                            Status = LauncherStatus.Ready;
                                            return;
                                        }
                                    }
                                }
                            }
                        });
                    }
                    if (serverChanged)
                        await DownloadBackgroundImageAsync();
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Checking for game update failed:\n{ex}");
                    Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show(textStrings["msgbox_updatecheckerror_msg"], textStrings["msgbox_updatecheckerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        {
                            return;
                        }
                    });
                }
            });
        }

        private bool GameUpdateCheckSimple()
        {
            if (localVersionInfo != null)
            {
                FetchmiHoYoVersionInfo();
                GameVersion localGameVersion = new GameVersion(localVersionInfo.game_info.version.ToString());
                GameVersion onlineGameVersion = new GameVersion(miHoYoVersionInfo.cur_version.ToString());
                if (onlineGameVersion.IsDifferentThan(localGameVersion))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void DownloadLauncherUpdate()
        {
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(onlineVersionInfo.launcher_info.url.ToString());
                webRequest.UserAgent = userAgent;
                webRequest.Timeout = 30000;
                using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    using (var data = new MemoryStream())
                    {
                        webResponse.GetResponseStream().CopyTo(data);
                        data.Seek(0, SeekOrigin.Begin);
                        using (FileStream file = new FileStream(launcherArchivePath, FileMode.Create))
                        {
                            data.CopyTo(file);
                            file.Flush();
                        }
                    }
                }
                Log("Launcher update download OK");
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download launcher update:\n{ex}");
                Dispatcher.Invoke(() =>
                {
                    if (MessageBox.Show(string.Format(textStrings["msgbox_launcherdownloaderror_msg"], ex), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                });
            }
        }

        private void DownloadBackgroundImage()
        {
            string backgroundImageName = miHoYoVersionInfo.bg_file_name.ToString();
            string backgroundImagePath = Path.Combine(launcherDataPath, backgroundImageName);
            if (File.Exists(backgroundImagePath))
            {
                Log($"Background image {backgroundImageName} exists, using it");
                BackgroundImage.Source = new BitmapImage(new Uri(backgroundImagePath));
            }
            else
            {
                Log($"Background image {backgroundImageName} doesn't exist, downloading...");
                try
                {
                    webClient.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
                    Directory.CreateDirectory(launcherDataPath);
                    webClient.DownloadFile(new Uri($"{miHoYoVersionInfo.download_url.ToString()}/{miHoYoVersionInfo.bg_file_name.ToString()}"), backgroundImagePath);
                    BackgroundImage.Source = new BitmapImage(new Uri(backgroundImagePath));
                    Log($"Downloaded background image: {backgroundImagePath}");
                }
                catch (Exception ex)
                {
                    Log($"ERROR: Failed to download background image:\n{ex}");
                }
            }
        }

        private async Task DownloadBackgroundImageAsync()
        {
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() => { DownloadBackgroundImage(); });
            });
        }

        private async Task DownloadGameFile()
        {
            try
            {
                string title;
                long time;
                string url;
                string md5;
                bool abort = false;
                if (Mirror == HI3Mirror.miHoYo)
                {
                    title = gameArchiveName;
                    time = -1;
                    url = $"{miHoYoVersionInfo.download_url.ToString()}/{gameArchiveName}";
                    md5 = miHoYoVersionInfo.full_version_file.md5.ToString();
                }
                else if (Mirror == HI3Mirror.MediaFire)
                {
                    dynamic mediafire_metadata;
                    if (Server == HI3Server.Global)
                        mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString(), false);
                    else
                        mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString(), false);
                    if (mediafire_metadata == null)
                        return;
                    title = mediafire_metadata.title.ToString();
                    time = ((DateTimeOffset)mediafire_metadata.modifiedDate).ToUnixTimeSeconds();
                    url = mediafire_metadata.downloadUrl.ToString();
                    md5 = mediafire_metadata.md5Checksum.ToString();
                    gameArchivePath = Path.Combine(gameInstallPath, title);
                    if (!mediafire_metadata.title.Contains(miHoYoVersionInfo.cur_version.ToString().Substring(0, 5)))
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Mirror is outdated!");
                        MessageBox.Show(textStrings["msgbox_gamedownloadmirrorold_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                    try
                    {
                        var webRequest = (HttpWebRequest)WebRequest.Create(url);
                        webRequest.UserAgent = userAgent;
                        webRequest.Timeout = 30000;
                        var webResponse = (HttpWebResponse)webRequest.GetResponse();
                    }
                    catch (WebException ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to download from MediaFire:\n{ex}");
                        MessageBox.Show(textStrings["msgbox_gamedownloadmirrorerror_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }
                else
                {
                    dynamic gd_metadata;
                    if (Server == HI3Server.Global)
                        gd_metadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
                    else
                        gd_metadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
                    if (gd_metadata == null)
                        return;
                    title = gd_metadata.title.ToString();
                    time = ((DateTimeOffset)gd_metadata.modifiedDate).ToUnixTimeSeconds();
                    url = gd_metadata.downloadUrl.ToString();
                    md5 = gd_metadata.md5Checksum.ToString();
                    gameArchivePath = Path.Combine(gameInstallPath, title);
                    if (DateTime.Compare(DateTime.Parse(miHoYoVersionInfo.last_modified.ToString()), DateTime.Parse(gd_metadata.modifiedDate.ToString())) > 0)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Mirror is outdated!");
                        MessageBox.Show(textStrings["msgbox_gamedownloadmirrorold_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                    try
                    {
                        var webRequest = (HttpWebRequest)WebRequest.Create(url);
                        webRequest.UserAgent = userAgent;
                        webRequest.Timeout = 30000;
                        var webResponse = (HttpWebResponse)webRequest.GetResponse();
                    }
                    catch (WebException ex)
                    {
                        Status = LauncherStatus.Error;
                        if (ex.Response != null)
                        {
                            using (var data = new MemoryStream())
                            {
                                ex.Response.GetResponseStream().CopyTo(data);
                                var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                                string msg;
                                if (json.error != null)
                                    msg = json.error.errors[0].message;
                                else
                                    msg = ex.Message;
                                Log($"ERROR: Failed to download from Google Drive:\n{msg}");
                                MessageBox.Show(textStrings["msgbox_gamedownloadmirrorerror_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                                Status = LauncherStatus.Ready;
                            }
                        }
                        return;
                    }
                }

                Log($"Starting to download game archive: {title} ({url})");
                Status = LauncherStatus.Downloading;
                Dispatcher.Invoke(() =>
                {
                    LaunchButton.IsEnabled = true;
                    LaunchButton.Content = textStrings["button_pause"];
                });
                await Task.Run(() =>
                {
                    tracker.NewFile();
                    var eta_calc = new ETACalculator(1, 1);
                    download = new DownloadPauseable(url, gameArchivePath);
                    download.Start();
                    while (download != null && !download.Done)
                    {
                        if (DownloadPaused)
                            continue;
                        tracker.SetProgress(download.BytesWritten, download.ContentLength);
                        eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
                        Dispatcher.Invoke(() =>
                        {
                            var progress = tracker.GetProgress();
                            ProgressBar.Value = progress;
                            TaskbarItemInfo.ProgressValue = progress;
                            ProgressText.Text = $"{string.Format(textStrings["progresstext_downloaded"], BpUtility.ToBytesCount(download.BytesWritten), BpUtility.ToBytesCount(download.ContentLength), tracker.GetBytesPerSecondString())}\n{string.Format(textStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"))}";
                        });
                        Thread.Sleep(100);
                    }
                    if (download == null)
                    {
                        abort = true;
                        return;
                    }
                    download = null;
                    Log("Game archive download OK");
                    while (BpUtility.IsFileLocked(new FileInfo(gameArchivePath)))
                        Thread.Sleep(10);
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = string.Empty;
                        LaunchButton.Content = textStrings["button_launch"];
                    });
                });
                try
                {
                    if (abort)
                        return;
                    await Task.Run(() =>
                    {
                        Log("Validating game archive...");
                        Status = LauncherStatus.Verifying;
                        string actual_md5 = BpUtility.CalculateMD5(gameArchivePath);
                        if (actual_md5 != md5.ToUpper())
                        {
                            Status = LauncherStatus.Error;
                            Log($"ERROR: Validation failed. Supposed MD5: {md5}, actual MD5: {actual_md5}");
                            Dispatcher.Invoke(() =>
                            {
                                if (MessageBox.Show(textStrings["msgbox_verifyerror_2_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                                {
                                    if (File.Exists(gameArchivePath))
                                        File.Create(gameArchivePath).Dispose();
                                    abort = true;
                                    GameUpdateCheck(false);
                                }
                            });
                        }
                        else
                        {
                            Log("Validation OK");
                        }
                        if (abort)
                            return;
                        var skippedFiles = new List<string>();
                        using (var archive = ArchiveFactory.Open(gameArchivePath))
                        {
                            int unpackedFiles = 0;
                            int fileCount = 0;

                            Log("Unpacking game archive...");
                            Status = LauncherStatus.Unpacking;
                            foreach (var entry in archive.Entries)
                            {
                                if (!entry.IsDirectory)
                                    fileCount++;
                            }
                            var reader = archive.ExtractAllEntries();
                            while (reader.MoveToNextEntry())
                            {
                                try
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        ProgressText.Text = string.Format(textStrings["progresstext_unpacking_2"], unpackedFiles + 1, fileCount);
                                        var progress = (unpackedFiles + 1f) / fileCount;
                                        ProgressBar.Value = progress;
                                        TaskbarItemInfo.ProgressValue = progress;
                                    });
                                    reader.WriteEntryToDirectory(gameInstallPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true, PreserveFileTime = true });
                                    if (!reader.Entry.IsDirectory)
                                        unpackedFiles++;
                                }
                                catch
                                {
                                    if (!reader.Entry.IsDirectory)
                                    {
                                        skippedFiles.Add(reader.Entry.ToString());
                                        fileCount--;
                                        Log($"Unpack ERROR: {reader.Entry}");
                                    }
                                }
                            }
                        }
                        try
                        {
                            File.Delete(gameArchivePath);
                        }
                        catch
                        {
                            Log($"Delete ERROR: {gameArchivePath}");
                        }
                        if (skippedFiles.Count > 0)
                        {
                            throw new ArchiveException("Game archive is corrupt");
                        }
                        Log("Game archive unpack OK");
                        Dispatcher.Invoke(() =>
                        {
                            WriteVersionInfo(false);
                            Log("Game install OK");
                            GameUpdateCheck(false);
                        });
                        if (time != -1)
                            SendStatistics(title, time);
                    });
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install the game:\n{ex}");
                    Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show(textStrings["msgbox_installerror_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        {
                            GameUpdateCheck(false);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download the game:\n{ex}");
                if (MessageBox.Show(textStrings["msgbox_gamedownloaderror_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    GameUpdateCheck(false);
                }
            }
        }

        private void WriteVersionInfo(bool CheckForLocalVersion)
        {
            try
            {
                dynamic versionInfo = new ExpandoObject();
                versionInfo.game_info = new ExpandoObject();
                versionInfo.game_info.version = miHoYoVersionInfo.cur_version.ToString();
                versionInfo.game_info.install_path = gameInstallPath;
                if (CheckForLocalVersion)
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                    if (LauncherRegKey.GetValue(RegistryVersionInfo) == null && (key != null && key.GetValue(GameRegistryLocalVersionRegValue) != null && key.GetValueKind(GameRegistryLocalVersionRegValue) == RegistryValueKind.Binary))
                    {
                        var version = Encoding.UTF8.GetString((byte[])key.GetValue(GameRegistryLocalVersionRegValue)).TrimEnd('\u0000');
                        if (!miHoYoVersionInfo.cur_version.ToString().Contains(version))
                            versionInfo.game_info.version = $"{version}_xxxxxxxxxx";
                    }
                    else
                    {
                        if (MessageBox.Show(textStrings["msgbox_install_existing_no_local_version_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        {
                            versionInfo.game_info.version = "0.0.0_xxxxxxxxxx";
                        }
                    }
                    key.Close();
                }
                Log("Writing game version info...");
                LauncherRegKey.SetValue(RegistryVersionInfo, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(versionInfo)), RegistryValueKind.Binary);
                LauncherRegKey.Close();
                LauncherRegKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Bp\Better HI3 Launcher", true);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}");
                MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteGameFiles(bool DeleteGame)
        {
            if (DeleteGame)
            {
                if (Directory.Exists(gameInstallPath))
                    Directory.Delete(gameInstallPath, true);
            }
            try { LauncherRegKey.DeleteValue(RegistryVersionInfo); } catch { }
            Dispatcher.Invoke(() => { LaunchButton.Content = textStrings["button_download"]; });
        }

        private async void DownloadGameCache(bool FullCache)
        {
            try
            {
                string title;
                long time;
                string url;
                string md5;
                bool abort = false;
                if (FullCache)
                {
                    title = gameCacheMetadata.title.ToString();
                    time = ((DateTimeOffset)gameCacheMetadata.modifiedDate).ToUnixTimeSeconds();
                    url = gameCacheMetadata.downloadUrl.ToString();
                    md5 = gameCacheMetadata.md5Checksum.ToString();
                }
                else
                {
                    title = gameCacheMetadataNumeric.title.ToString();
                    time = ((DateTimeOffset)gameCacheMetadataNumeric.modifiedDate).ToUnixTimeSeconds();
                    url = gameCacheMetadataNumeric.downloadUrl.ToString();
                    md5 = gameCacheMetadataNumeric.md5Checksum.ToString();
                }
                cacheArchivePath = Path.Combine(miHoYoPath, title);

                var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(cacheArchivePath) && x.IsReady).FirstOrDefault();
                if (gameInstallDrive == null)
                {
                    Dispatcher.Invoke(() => { MessageBox.Show(textStrings["msgbox_install_wrong_drive_type_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error); });
                    return;
                }
                else if (gameInstallDrive.TotalFreeSpace < 2147483648)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show(textStrings["msgbox_install_little_space_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                            return;
                    });
                }
                try
                {
                    var webRequest = (HttpWebRequest)WebRequest.Create(url);
                    webRequest.UserAgent = userAgent;
                    webRequest.Timeout = 30000;
                    var webResponse = (HttpWebResponse)webRequest.GetResponse();
                }
                catch (WebException ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to download cache from mirror:\n{ex}");
                    MessageBox.Show(textStrings["msgbox_gamedownloadmirrorerror_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    Status = LauncherStatus.Ready;
                    return;
                }

                Log($"Starting to download game cache: {title} ({url})");
                Status = LauncherStatus.Downloading;
                await Task.Run(() =>
                {
                    tracker.NewFile();
                    var eta_calc = new ETACalculator(1, 1);
                    var download = new DownloadPauseable(url, cacheArchivePath);
                    download.Start();
                    while (!download.Done)
                    {
                        tracker.SetProgress(download.BytesWritten, download.ContentLength);
                        eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
                        Dispatcher.Invoke(() =>
                        {
                            var progress = tracker.GetProgress();
                            ProgressBar.Value = progress;
                            TaskbarItemInfo.ProgressValue = progress;
                            ProgressText.Text = $"{string.Format(textStrings["progresstext_downloaded"], BpUtility.ToBytesCount(download.BytesWritten), BpUtility.ToBytesCount(download.ContentLength), tracker.GetBytesPerSecondString())}\n{string.Format(textStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"))}";
                        });
                        Thread.Sleep(100);
                    }
                    Log("Game cache download OK");
                    while (BpUtility.IsFileLocked(new FileInfo(cacheArchivePath)))
                        Thread.Sleep(10);
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = string.Empty;
                        LaunchButton.Content = textStrings["button_launch"];
                    });
                });
                try
                {
                    if (abort)
                        return;
                    await Task.Run(() =>
                    {
                        Log("Validating game cache...");
                        Status = LauncherStatus.Verifying;
                        string actual_md5 = BpUtility.CalculateMD5(cacheArchivePath);
                        if (actual_md5 != md5.ToUpper())
                        {
                            Status = LauncherStatus.Error;
                            Log($"ERROR: Validation failed. Supposed MD5: {md5}, actual MD5: {actual_md5}");
                            Dispatcher.Invoke(() =>
                            {
                                if (MessageBox.Show(textStrings["msgbox_verifyerror_2_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                                {
                                    try
                                    {
                                        if (File.Exists(cacheArchivePath))
                                            File.Delete(cacheArchivePath);
                                    }
                                    catch
                                    {
                                        Log($"Delete ERROR: {cacheArchivePath}");
                                    }
                                    abort = true;
                                    GameUpdateCheck(false);
                                }
                            });
                        }
                        else
                        {
                            Log("Validation OK");
                        }
                        if (abort)
                            return;
                        var skippedFiles = new List<string>();
                        using (var archive = ArchiveFactory.Open(cacheArchivePath))
                        {
                            int unpackedFiles = 0;
                            int fileCount = 0;

                            Log("Unpacking game cache...");
                            Status = LauncherStatus.Unpacking;
                            foreach (var entry in archive.Entries)
                            {
                                if (!entry.IsDirectory)
                                    fileCount++;
                            }
                            Directory.CreateDirectory(miHoYoPath);
                            var reader = archive.ExtractAllEntries();
                            while (reader.MoveToNextEntry())
                            {
                                try
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        ProgressText.Text = string.Format(textStrings["progresstext_unpacking_2"], unpackedFiles + 1, fileCount);
                                        var progress = (unpackedFiles + 1f) / fileCount;
                                        ProgressBar.Value = progress;
                                        TaskbarItemInfo.ProgressValue = progress;
                                    });
                                    reader.WriteEntryToDirectory(miHoYoPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true, PreserveFileTime = true });
                                    if (!reader.Entry.IsDirectory)
                                        unpackedFiles++;
                                }
                                catch
                                {
                                    if (!reader.Entry.IsDirectory)
                                    {
                                        skippedFiles.Add(reader.Entry.ToString());
                                        fileCount--;
                                        Log($"Unpack ERROR: {reader.Entry}");
                                    }
                                }
                            }
                        }
                        try
                        {
                            if (File.Exists(cacheArchivePath))
                                File.Delete(cacheArchivePath);
                        }
                        catch
                        {
                            Log($"Delete ERROR: {cacheArchivePath}");
                        }
                        if (skippedFiles.Count > 0)
                        {
                            throw new ArchiveException("Cache archive is corrupt");
                        }
                        Log("Game cache unpack OK");
                        SendStatistics(title, time);
                    });
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install game cache:\n{ex}");
                    MessageBox.Show(textStrings["msgbox_installerror_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download game cache:\n{ex}");
                MessageBox.Show(textStrings["msgbox_gamedownloaderror_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Dispatcher.Invoke(() => { LaunchButton.Content = textStrings["button_launch"]; });
            Status = LauncherStatus.Ready;
        }

        private void SendStatistics(string file, long time)
        {
            if (string.IsNullOrEmpty(file))
                throw new ArgumentNullException();

            string server = (int)Server == 0 ? "global" : "os";
            string mirror = (int)Mirror == 2 ? "gd" : "mediafire";
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(onlineVersionInfo.launcher_info.stat_url.ToString());
                var data = Encoding.ASCII.GetBytes($"save_stats={server}&mirror={mirror}&file={file}&time={time}");
                webRequest.Method = "POST";
                webRequest.UserAgent = userAgent;
                webRequest.Timeout = 3000;
                webRequest.ContentType = "application/x-www-form-urlencoded";
                webRequest.ContentLength = data.Length;
                using (var stream = webRequest.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                using (var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    var responseData = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();
                    if (!string.IsNullOrEmpty(responseData))
                    {
                        var json = JsonConvert.DeserializeObject<dynamic>(responseData);
                        if (json.status != "success")
                        {
                            Log($"ERROR: Failed to send download stat of {file}");
                        }
                    }
                }
            }
            catch
            {
                Log($"ERROR: Failed to send download stat of {file}");
            }
        }

        private async void Window_ContentRendered(object sender, EventArgs e)
        {
#if !DEBUG
            try
            {
                await Task.Run(() =>
                {
                    bool needsUpdate = LauncherUpdateCheck();

                    if(Process.GetCurrentProcess().MainModule.ModuleName != launcherExeName)
                    {
                        Status = LauncherStatus.Error;
                        File.Move(Path.Combine(rootPath, Process.GetCurrentProcess().MainModule.ModuleName), launcherPath);
                        BpUtility.StartProcess(launcherExeName, null, rootPath, true);
                        Application.Current.Shutdown();
                        return;
                    }

                    if(File.Exists($"{Path.Combine(rootPath, Path.GetFileNameWithoutExtension(launcherPath))}_old.exe"))
                    {
                        File.Delete($"{Path.Combine(rootPath, Path.GetFileNameWithoutExtension(launcherPath))}_old.exe");
                    }

                    if(needsUpdate)
                    {
                        Log("There's a newer version of the launcher, attempting to download...");
                        Status = LauncherStatus.Working;
                        Dispatcher.Invoke(() => {ProgressText.Text = textStrings["progresstext_updating_launcher"];});
                        DownloadLauncherUpdate();
                        string md5 = onlineVersionInfo.launcher_info.md5.ToString().ToUpper();
                        string actual_md5 = BpUtility.CalculateMD5(launcherArchivePath);
                        if(actual_md5 != md5)
                        {
                            Status = LauncherStatus.Error;
                            Log($"ERROR: Validation failed. Supposed MD5: {md5}, actual MD5: {actual_md5}");
                            if(File.Exists(launcherArchivePath))
                            {
                                File.Delete(launcherArchivePath);
                            }
                            if(MessageBox.Show(textStrings["msgbox_verifyerror_1_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                            {
                                Application.Current.Shutdown();
                                return;
                            }
                        }
                        File.Move(Path.Combine(rootPath, Process.GetCurrentProcess().MainModule.ModuleName), $"{Path.Combine(rootPath, Path.GetFileNameWithoutExtension(launcherPath))}_old.exe");
                        using(var archive = ArchiveFactory.Open(launcherArchivePath))
                        {
                            var reader = archive.ExtractAllEntries();
                            while(reader.MoveToNextEntry())
                            {
                                reader.WriteEntryToDirectory(rootPath, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true});
                            }
                        }
                        Dispatcher.Invoke(() =>
                        {
                            BpUtility.StartProcess(launcherExeName, null, rootPath, true);
                            Application.Current.Shutdown();
                        });
                        return;
                    }
                    else
                    {
                        if(File.Exists(Path.Combine(rootPath, "BetterHI3Launcher.7z")))
                        {
                            File.Delete(Path.Combine(rootPath, "BetterHI3Launcher.7z"));
                        }
                        if(File.Exists(launcherArchivePath))
                        {
                            File.Delete(launcherArchivePath);
                        }
                        if(!File.Exists(launcherPath))
                        {
                            File.Copy(Path.Combine(rootPath, Process.GetCurrentProcess().MainModule.ModuleName), launcherPath, true);
                        }
                    }
                });
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                if(MessageBox.Show(string.Format(textStrings["msgbox_starterror_msg"], ex), textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }
            
            if(LauncherRegKey != null && (LauncherRegKey.GetValue("LauncherVersion") != null && LauncherRegKey.GetValue("LauncherVersion").ToString() != localLauncherVersion.ToString()))
            {
                ChangelogBox.Visibility = Visibility.Visible;
                ChangelogBoxMessageTextBlock.Visibility = Visibility.Visible;
                ChangelogBoxScrollViewer.Height = 305;
            }
            try
            {
                if(LauncherRegKey.GetValue("LauncherVersion") != null && LauncherRegKey.GetValue("LauncherVersion").ToString() != localLauncherVersion.ToString())
                    LauncherRegKey.SetValue("LauncherVersion", localLauncherVersion);
                if(LauncherRegKey.GetValue("RanOnce") != null)
                    LauncherRegKey.DeleteValue("RanOnce");
                if(LauncherRegKey.GetValue("BackgroundImageName") != null)
                    LauncherRegKey.DeleteValue("BackgroundImageName");
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to write critical registry info:\n{ex}");
                if(MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }
#endif
            GameUpdateCheck(false);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (Status == LauncherStatus.Ready)
            {
                if (DownloadPaused)
                {
                    DownloadPaused = false;
                    await DownloadGameFile();
                    return;
                }

                if (localVersionInfo != null)
                {
                    if (!File.Exists(gameExePath))
                    {
                        MessageBox.Show(textStrings["msgbox_noexe_msg"], textStrings["msgbox_noexe_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    try
                    {
                        BpUtility.StartProcess(gameExePath, null, gameInstallPath, true);
                        WindowState = WindowState.Minimized;
                    }
                    catch (Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to start the game:\n{ex}");
                        MessageBox.Show(textStrings["msgbox_process_start_error_msg"], textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                    }
                }
                else
                {
                    try
                    {
                        string SelectGameInstallDirectory()
                        {
                            // https://stackoverflow.com/a/17712949/7570821
                            var dialog = new CommonOpenFileDialog
                            {
                                IsFolderPicker = true,
                                InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                                AddToMostRecentlyUsedList = false,
                                AllowNonFileSystemItems = false,
                                DefaultDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                                EnsureFileExists = true,
                                EnsurePathExists = true,
                                EnsureReadOnly = false,
                                EnsureValidNames = true,
                                Multiselect = false,
                                ShowPlacesList = true
                            };

                            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                if (Server == HI3Server.Global)
                                    gameInstallPath = Path.Combine(dialog.FileName, "Honkai Impact 3rd");
                                else
                                    gameInstallPath = Path.Combine(dialog.FileName, "Honkai Impact 3");
                            }
                            else
                            {
                                gameInstallPath = null;
                            }

                            if (string.IsNullOrEmpty(gameInstallPath))
                            {
                                return string.Empty;
                            }
                            else
                            {
                                var path = CheckForExistingGameDirectory(gameInstallPath);
                                if (!string.IsNullOrEmpty(path))
                                {
                                    if (MessageBox.Show(string.Format(textStrings["msgbox_installexisting_msg"], path), textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                                    {
                                        Log($"Existing install directory selected: {path}");
                                        gameInstallPath = path;
                                        var server = CheckForExistingGameClientServer();
                                        if (server >= 0)
                                        {
                                            if ((int)Server != server)
                                                ServerDropdown.SelectedIndex = server;
                                            WriteVersionInfo(true);
                                            GameUpdateCheck(false);
                                        }
                                        else
                                        {
                                            Status = LauncherStatus.Error;
                                            Log($"ERROR: Directory {gameInstallPath} doesn't contain a valid installation of the game. This launcher supports only Global and SEA clients!");
                                            if (MessageBox.Show(string.Format(textStrings["msgbox_installexistinginvalid_msg"]), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                                            {
                                                Status = LauncherStatus.Ready;
                                            }
                                        }
                                    }
                                    return string.Empty;
                                }
                                return gameInstallPath;
                            }
                        }
                        if (string.IsNullOrEmpty(SelectGameInstallDirectory()))
                            return;
                        while (MessageBox.Show(string.Format(textStrings["msgbox_install_msg"], gameInstallPath), textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                        {
                            if (string.IsNullOrEmpty(SelectGameInstallDirectory()))
                                return;
                        }
                        var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(gameInstallPath) && x.IsReady).FirstOrDefault();
                        if (gameInstallDrive == null || gameInstallDrive.DriveType == DriveType.CDRom)
                        {
                            MessageBox.Show(textStrings["msgbox_install_wrong_drive_type_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        else if (gameInstallDrive.TotalFreeSpace < 24696061952)
                        {
                            if (MessageBox.Show(textStrings["msgbox_install_little_space_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                                return;
                        }
                        Directory.CreateDirectory(gameInstallPath);
                        gameArchivePath = Path.Combine(gameInstallPath, gameArchiveName);
                        gameExePath = Path.Combine(gameInstallPath, "BH3.exe");
                        Log($"Install dir selected: {gameInstallPath}");
                        await DownloadGameFile();
                    }
                    catch (Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to select game install directory:\n{ex}");
                        MessageBox.Show(string.Format(textStrings["msgbox_installdirerror_msg"], ex), textStrings["msgbox_installdirerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }
            }
            else if (Status == LauncherStatus.UpdateAvailable)
            {
                var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(gameInstallPath) && x.IsReady).FirstOrDefault();
                if (gameInstallDrive.TotalFreeSpace < 24696061952)
                {
                    if (MessageBox.Show(textStrings["msgbox_install_little_space_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                Directory.CreateDirectory(gameInstallPath);
                await DownloadGameFile();
            }
            else if (Status == LauncherStatus.Downloading || Status == LauncherStatus.DownloadPaused)
            {
                if (!DownloadPaused)
                {
                    download.Pause();
                    Status = LauncherStatus.DownloadPaused;
                    DownloadPaused = true;
                    LaunchButton.Content = textStrings["button_resume"];
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
                }
                else
                {
                    Status = LauncherStatus.Downloading;
                    DownloadPaused = false;
                    LaunchButton.IsEnabled = true;
                    LaunchButton.Content = textStrings["button_pause"];
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                    await download.Start();
                }
            }
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            OptionsContextMenu.PlacementTarget = sender as Button;
            OptionsContextMenu.IsOpen = true;
        }

        private async Task CM_DownloadCache_Click(object sender, RoutedEventArgs e)
        {
            if (Status != LauncherStatus.Ready)
                return;

            Status = LauncherStatus.CheckingUpdates;
            Dispatcher.Invoke(() => { ProgressText.Text = textStrings["progresstext_mirror_connect"]; });
            Log("Fetching mirror metadata...");

            try
            {
                await Task.Run(async () =>
                {
                    await FetchOnlineVersionInfoAsync();
                    if (Server == HI3Server.Global)
                    {
                        if (Mirror == HI3Mirror.GoogleDrive)
                        {
                            gameCacheMetadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache.global.ToString());
                            if (gameCacheMetadata != null)
                                gameCacheMetadataNumeric = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache_numeric.global.ToString());
                        }
                        else
                        {
                            gameCacheMetadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache.global.id.ToString(), false);
                            if (gameCacheMetadata != null)
                                gameCacheMetadataNumeric = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.id.ToString(), true);
                        }
                    }
                    else
                    {
                        if (Mirror == HI3Mirror.GoogleDrive)
                        {
                            gameCacheMetadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache.os.ToString());
                            if (gameCacheMetadata != null)
                                gameCacheMetadataNumeric = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache_numeric.os.ToString());
                        }
                        else
                        {
                            gameCacheMetadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache.os.id.ToString(), false);
                            if (gameCacheMetadata != null)
                                gameCacheMetadataNumeric = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.id.ToString(), true);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to fetch cache file metadata:\n{ex}");
                Dispatcher.Invoke(() => { MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], ex), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error); });
                Status = LauncherStatus.Ready;
                return;
            }
            if (gameCacheMetadata == null || gameCacheMetadataNumeric == null)
            {
                Status = LauncherStatus.Ready;
                return;
            }
            Dispatcher.Invoke(() =>
            {
                DownloadCacheBox.Visibility = Visibility.Visible;
                string mirror;
                string time;
                string last_updated;
                if (Mirror == HI3Mirror.GoogleDrive)
                {
                    mirror = "Google Drive";
                    time = gameCacheMetadataNumeric.modifiedDate.ToString();
                }
                else
                {
                    mirror = "MediaFire";
                    time = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((double)onlineVersionInfo.game_info.mirror.mediafire.last_updated).ToString();
                }
                if (DateTime.Compare(FetchmiHoYoResourceVersionDateModified(), DateTime.Parse(time)) >= 0)
                    last_updated = $"{DateTime.Parse(time).ToLocalTime()} ({textStrings["outdated"].ToLower()})";
                else
                    last_updated = DateTime.Parse(time).ToLocalTime().ToString();
                DownloadCacheBoxMessageTextBlock.Text = string.Format(textStrings["downloadcachebox_msg"], mirror, last_updated, onlineVersionInfo.game_info.mirror.maintainer.ToString());
                Status = LauncherStatus.Ready;
            });
        }

        private async Task CM_Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if ((Status == LauncherStatus.Ready || Status == LauncherStatus.UpdateAvailable || Status == LauncherStatus.DownloadPaused) && !string.IsNullOrEmpty(gameInstallPath))
            {
                if (rootPath.Contains(gameInstallPath))
                {
                    MessageBox.Show(textStrings["msgbox_uninstall_4_msg"], textStrings["msgbox_uninstall_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (MessageBox.Show(textStrings["msgbox_uninstall_1_msg"], textStrings["msgbox_uninstall_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                    return;
                if (MessageBox.Show(textStrings["msgbox_uninstall_2_msg"], textStrings["msgbox_uninstall_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    return;

                Status = LauncherStatus.Uninstalling;
                Log($"Deleting game files...");
                await Task.Run(() =>
                {
                    try
                    {
                        DeleteGameFiles(true);
                        Dispatcher.Invoke(() =>
                        {
                            if (MessageBox.Show(textStrings["msgbox_uninstall_3_msg"], textStrings["msgbox_uninstall_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                            {
                                string path;
                                if (Server == HI3Server.Global)
                                    path = Path.Combine(miHoYoPath, "Honkai Impact 3rd");
                                else
                                    path = Path.Combine(miHoYoPath, "Honkai Impact 3");
                                Log("Deleting game cache and registry settings...");
                                if (Directory.Exists(path))
                                    Directory.Delete(path, true);
                                var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                                if (key != null)
                                    Registry.CurrentUser.DeleteSubKeyTree(GameRegistryPath, true);
                                key.Close();
                            }
                        });
                        Log("Game uninstall OK");
                        GameUpdateCheck(false);
                    }
                    catch (Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to uninstall the game:\n{ex}");
                        MessageBox.Show(string.Format(textStrings["msgbox_uninstallerror_msg"], ex), textStrings["msgbox_uninstallerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                });
            }
        }

        private void CM_FixUpdateLoop_Click(object sender, RoutedEventArgs e)
        {
            if (Status != LauncherStatus.Ready)
                return;

            if (MessageBox.Show(textStrings["msgbox_download_type_1_msg"], textStrings["contextmenu_download_type"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                string value = "GENERAL_DATA_V2_ResourceDownloadType_h2238376574";
                if (key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.DWord)
                {
                    if (key.GetValue(value) != null)
                        key.DeleteValue(value);
                    if (MessageBox.Show(textStrings["msgbox_registryempty_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        return;
                    }
                }
                var valueBefore = key.GetValue(value);
                int valueAfter;
                if ((int)valueBefore == 3)
                    valueAfter = 2;
                else if ((int)valueBefore == 2)
                    valueAfter = 1;
                else
                    valueAfter = 3;
                key.SetValue(value, valueAfter, RegistryValueKind.DWord);
                key.Close();
                Log($"Changed ResourceDownloadType from {valueBefore} to {valueAfter}");
                MessageBox.Show(string.Format(textStrings["msgbox_download_type_2_msg"], valueBefore, valueAfter), textStrings["contextmenu_download_type"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}");
                if (MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private async Task CM_FixSubtitles_Click(object sender, RoutedEventArgs e)
        {
            if (Status != LauncherStatus.Ready)
                return;

            if (MessageBox.Show(textStrings["msgbox_fixsubs_1_msg"], textStrings["contextmenu_fixsubs"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;

            try
            {
                Status = LauncherStatus.Working;
                Log("Starting to fix subtitles...");
                var GameVideoDirectory = Path.Combine(gameInstallPath, @"BH3_Data\StreamingAssets\Video");
                if (Directory.Exists(GameVideoDirectory))
                {
                    var SubtitleArchives = Directory.EnumerateFiles(GameVideoDirectory, "*.zip", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith(".zip", StringComparison.CurrentCultureIgnoreCase)).ToList();
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.IsIndeterminate = false;
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                    });
                    if (SubtitleArchives.Count > 0)
                    {
                        int filesUnpacked = 0;
                        await Task.Run(() =>
                        {
                            var skippedFiles = new List<string>();
                            var skippedFilePaths = new List<string>();
                            foreach (var SubtitleArchive in SubtitleArchives)
                            {
                                bool unpack_ok = true;
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressText.Text = string.Format(textStrings["msgbox_fixsubs_2_msg"], filesUnpacked + 1, SubtitleArchives.Count);
                                    var progress = (filesUnpacked + 1f) / SubtitleArchives.Count;
                                    ProgressBar.Value = progress;
                                    TaskbarItemInfo.ProgressValue = progress;
                                });
                                using (var archive = ArchiveFactory.Open(SubtitleArchive))
                                {
                                    var reader = archive.ExtractAllEntries();
                                    while (reader.MoveToNextEntry())
                                    {
                                        try
                                        {
                                            var entryPath = Path.Combine(GameVideoDirectory, reader.Entry.ToString());
                                            if (File.Exists(entryPath))
                                                File.SetAttributes(entryPath, File.GetAttributes(entryPath) & ~FileAttributes.ReadOnly);
                                            reader.WriteEntryToDirectory(GameVideoDirectory, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true, PreserveFileTime = true });
                                        }
                                        catch
                                        {
                                            unpack_ok = false;
                                            skippedFiles.Add($"[{Path.GetFileName(SubtitleArchive)}] {reader.Entry}");
                                            skippedFilePaths.Add(SubtitleArchive);
                                            Log($"Unpack ERROR: [{SubtitleArchive}] {reader.Entry}");
                                        }
                                    }
                                }
                                if (unpack_ok)
                                    Log($"Unpack OK: {SubtitleArchive}");
                                File.SetAttributes(SubtitleArchive, File.GetAttributes(SubtitleArchive) & ~FileAttributes.ReadOnly);
                                if (!skippedFilePaths.Contains(SubtitleArchive))
                                {
                                    try
                                    {
                                        File.Delete(SubtitleArchive);
                                    }
                                    catch
                                    {
                                        Log($"Delete ERROR: {SubtitleArchive}");
                                    }
                                }
                                filesUnpacked++;
                            }
                            Dispatcher.Invoke(() =>
                            {
                                if (skippedFiles.Count > 0)
                                {
                                    ShowLogCheckBox.IsChecked = true;
                                    //TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
                                    //MessageBox.Show(textStrings["msgbox_extractskip_msg"], textStrings["msgbox_extractskip_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            });
                            Log($"Unpacked {filesUnpacked} archives");
                        });
                    }
                    ProgressBar.IsIndeterminate = true;
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
                    var SubtitleFiles = Directory.EnumerateFiles(GameVideoDirectory, "*.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith(".srt", StringComparison.CurrentCultureIgnoreCase)).ToList();
                    var subsFixed = new List<string>();
                    ProgressBar.IsIndeterminate = false;
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                    if (SubtitleFiles.Count > 0)
                    {
                        int subtitlesParsed = 0;
                        await Task.Run(() =>
                        {
                            foreach (var SubtitleFile in SubtitleFiles)
                            {
                                var fileLines = File.ReadAllLines(SubtitleFile);
                                int lineCount = fileLines.Length;
                                int linesReplaced = 0;
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressText.Text = string.Format(textStrings["msgbox_fixsubs_3_msg"], subtitlesParsed + 1, SubtitleFiles.Count);
                                    var progress = (subtitlesParsed + 1f) / SubtitleFiles.Count;
                                    ProgressBar.Value = progress;
                                    TaskbarItemInfo.ProgressValue = progress;
                                });
                                File.SetAttributes(SubtitleFile, File.GetAttributes(SubtitleFile) & ~FileAttributes.ReadOnly);
                                if (new FileInfo(SubtitleFile).Length == 0)
                                {
                                    subtitlesParsed++;
                                    continue;
                                }
                                for (int atLine = 1; atLine < lineCount; atLine++)
                                {
                                    var line = File.ReadLines(SubtitleFile).Skip(atLine).Take(1).First();
                                    if (string.IsNullOrEmpty(line) || new Regex(@"^\d+$").IsMatch(line))
                                        continue;

                                    bool lineFixed = false;
                                    void LogLine()
                                    {
                                        if (lineFixed)
                                            return;

                                        linesReplaced++;
                                        lineFixed = true;
                                        //Log($"Fixed line {1 + atLine}: {line}");
                                    }

                                    if (line.Contains("-->"))
                                    {
                                        if (line.Contains("."))
                                        {
                                            fileLines[atLine] = line.Replace(".", ",");
                                            LogLine();
                                        }
                                        if (line.Contains(" ,"))
                                        {
                                            fileLines[atLine] = line.Replace(" ,", ",");
                                            LogLine();
                                        }
                                        if (line.Contains("  "))
                                        {
                                            fileLines[atLine] = line.Replace("  ", " ");
                                            LogLine();
                                        }
                                    }
                                    else
                                    {
                                        if (line.Contains(" ,"))
                                        {
                                            fileLines[atLine] = line.Replace(" ,", ",");
                                            LogLine();
                                        }
                                    }
                                }
                                if (linesReplaced > 0)
                                {
                                    File.WriteAllLines(SubtitleFile, fileLines);
                                    if (!subsFixed.Contains(SubtitleFile))
                                    {
                                        subsFixed.Add(SubtitleFile);
                                        Log($"Subtitle fixed: {SubtitleFile}");
                                    }
                                }
                                subtitlesParsed++;
                            }
                        });
                        Log($"Parsed {subtitlesParsed} subtitles, fixed {subsFixed.Count} of them");
                    }
                    if (Server == HI3Server.Global)
                    {
                        ProgressBar.IsIndeterminate = true;
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
                        SubtitleFiles = Directory.EnumerateFiles(GameVideoDirectory, "*id.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("id.srt", StringComparison.CurrentCultureIgnoreCase)).ToList();
                        SubtitleFiles.AddRange(SubtitleFiles = Directory.EnumerateFiles(GameVideoDirectory, "*th.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("th.srt", StringComparison.CurrentCultureIgnoreCase)).ToList());
                        SubtitleFiles.AddRange(SubtitleFiles = Directory.EnumerateFiles(GameVideoDirectory, "*vn.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("vn.srt", StringComparison.CurrentCultureIgnoreCase)).ToList());
                        if (SubtitleFiles.Count > 0)
                        {
                            int deletedSubs = 0;
                            await Task.Run(() =>
                            {
                                foreach (var SubtitleFile in SubtitleFiles)
                                {
                                    try
                                    {
                                        if (File.Exists(SubtitleFile))
                                            File.Delete(SubtitleFile);
                                        deletedSubs++;
                                    }
                                    catch
                                    {
                                        Log($"Delete ERROR: {SubtitleFile}");
                                    }
                                }
                            });
                            Log($"Deleted {deletedSubs} useless subtitles");
                        }
                    }
                    ProgressText.Text = string.Empty;
                    ProgressBar.Visibility = Visibility.Hidden;
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    WindowState = WindowState.Normal;
                    if (SubtitleArchives.Count > 0 && subsFixed.Count == 0)
                        MessageBox.Show(string.Format(textStrings["msgbox_fixsubs_4_msg"], SubtitleArchives.Count), textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                    else if (SubtitleArchives.Count == 0 && subsFixed.Count > 0)
                        MessageBox.Show(string.Format(textStrings["msgbox_fixsubs_5_msg"], subsFixed.Count), textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                    else if (SubtitleArchives.Count > 0 && subsFixed.Count > 0)
                        MessageBox.Show($"{string.Format(textStrings["msgbox_fixsubs_4_msg"], SubtitleArchives.Count)}\n{string.Format(textStrings["msgbox_fixsubs_5_msg"], subsFixed.Count)}", textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show(string.Format(textStrings["msgbox_fixsubs_6_msg"]), textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Status = LauncherStatus.Error;
                    Log("ERROR: No CG directory!");
                    MessageBox.Show(string.Format(textStrings["msgbox_novideodir_msg"]), textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Status = LauncherStatus.Ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR:\n{ex}");
                if (MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void CM_CustomFPS_Click(object sender, RoutedEventArgs e)
        {
            //added an additional field and logic to support the menu FPS cap.
            if (Status != LauncherStatus.Ready)
                return;

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                string value = "GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411";
                if (key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.Binary)
                {
                    if (key.GetValue(value) != null)
                        key.DeleteValue(value);
                    if (MessageBox.Show(textStrings["msgbox_registryempty_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        return;
                }
                var valueBefore = key.GetValue(value);
                var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])valueBefore));
                if (json == null)
                {
                    if (MessageBox.Show(textStrings["msgbox_registryempty_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        return;
                }
                key.Close();
                FPSInputBox.Visibility = Visibility.Visible;
                FPSInputBoxTitleTextBlock.Text = textStrings["fpsinputbox_title"];
                if (json.TargetFrameRateForInLevel != null && json.TargetFrameRateForOthers != null)
                {
                    FPSInputBoxTextBoxCombat.Text = json.TargetFrameRateForInLevel;
                    FPSInputBoxTextBoxMenu.Text = json.TargetFrameRateForOthers;
                }
                else
                {
                    FPSInputBoxTextBoxCombat.Text = "60";
                    FPSInputBoxTextBoxMenu.Text = "60";
                }
                gameGraphicSettings = json;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}");
                if (MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)

                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }
        //Major Addtion - CM_CustomResolution_Click
        //context menu click function to show and propulate a custom Resolution menu.
        private void CM_CustomResolution_Click(object sender, RoutedEventArgs e)
        {
            if (Status != LauncherStatus.Ready)
                return;
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                string value = "GENERAL_DATA_V2_ScreenSettingData_h1916288658";
                if (key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.Binary)
                {
                    if (key.GetValue(value) != null)
                        key.DeleteValue(value);
                    if (MessageBox.Show(textStrings["msgbox_customresolution_2_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        return;
                }
                var valueBefore = key.GetValue(value);
                var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])valueBefore));
                if (json == null)
                {
                    if (MessageBox.Show(textStrings["msgbox_customresolution_2_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        return;
                }
                key.Close();
                //Log($"{json}");
                ResolutionInputBox.Visibility = Visibility.Visible;
                ResolutionInputBoxTitleTextBlock.Text = textStrings["resolutioninputbox_title"];
                ResolutionTextHeight.Text = textStrings["resolutionlabel_height"];
                ResolutionTextWidth.Text = textStrings["resolutionlabel_width"];
                ToggleFullscreenText.Text = textStrings["resolutionlabel_isfullscreen"];

                if (json.width != null && json.height != null)
                {
                    ResolutionInputBoxTextBoxW.Text = json.width;
                    ResolutionInputBoxTextBoxH.Text = json.height;
                    ToggleFullscreen.IsChecked = json.isfullScreen;
                }
                else
                {
                    ResolutionInputBoxTextBoxW.Text = "720";
                    ResolutionInputBoxTextBoxH.Text = "480";
                }
                gameGraphicSettings = json;
            }

            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n {ex}");
                if (MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK);
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
            
        }

        private void CM_ResetGameSettings_Click(object sender, RoutedEventArgs e)
        {
            if (Status != LauncherStatus.Ready)
                return;

            if (MessageBox.Show(textStrings["msgbox_resetgamesettings_1_msg"], textStrings["contextmenu_resetgamesettings"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            if (MessageBox.Show(textStrings["msgbox_resetgamesettings_2_msg"], textStrings["contextmenu_resetgamesettings"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return;

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                if (key == null)
                {
                    Log("ERROR: No game registry key!");
                    if (MessageBox.Show(textStrings["msgbox_registryempty_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        return;
                    }
                }
                Registry.CurrentUser.DeleteSubKeyTree(GameRegistryPath, true);
                key.Close();
                Log("Game settings reset OK");
                MessageBox.Show(textStrings["msgbox_resetgamesettings_3_msg"], textStrings["contextmenu_resetgamesettings"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}");
                if (MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void CM_WebProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BpUtility.StartProcess(gameWebProfileURL, null, rootPath, true);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to execute process:\n{ex}");
                MessageBox.Show(textStrings["msgbox_process_start_error_msg"], textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                Status = LauncherStatus.Ready;
            }
        }

        private void CM_Feedback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new/choose", null, rootPath, true);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to execute process:\n{ex}");
                MessageBox.Show(textStrings["msgbox_process_start_error_msg"], textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                Status = LauncherStatus.Ready;
            }
        }

        private void CM_Changelog_Click(object sender, RoutedEventArgs e)
        {
            ChangelogBox.Visibility = Visibility.Visible;
        }

        private void CM_About_Click(object sender, RoutedEventArgs e)
        {
            AboutBox.Visibility = Visibility.Visible;
        }

        private void ServerDropdown_Changed(object sender, SelectionChangedEventArgs e)
        {
            var index = ServerDropdown.SelectedIndex;
            if ((int)Server == index)
                return;
            if (DownloadPaused)
            {
                if (MessageBox.Show(textStrings["msgbox_gamedownloadpaused_msg"], textStrings["msgbox_notice_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    ServerDropdown.SelectedIndex = (int)Server;
                    return;
                }
                try
                {
                    if (File.Exists(gameArchivePath))
                        File.Delete(gameArchivePath);
                }
                catch
                {
                    Log($"Delete ERROR: {gameArchivePath}");
                }
                download = null;
                DownloadPaused = false;
                try { DeleteGameFiles(false); } catch { }
            }
            switch (index)
            {
                case 0:
                    Server = HI3Server.Global;
                    break;
                case 1:
                    Server = HI3Server.SEA;
                    break;
            }
            try
            {
                LauncherRegKey.SetValue("LastSelectedServer", index, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to write value with key LastSelectedServer to registry:\n{ex}");
            }
            Log($"Switched server to {((ComboBoxItem)ServerDropdown.SelectedItem).Content as string}");
            GameUpdateCheck(true);
        }

        private void MirrorDropdown_Changed(object sender, SelectionChangedEventArgs e)
        {
            var index = MirrorDropdown.SelectedIndex;
            if ((int)Mirror == index)
                return;
            if (DownloadPaused)
            {
                if (MessageBox.Show(textStrings["msgbox_gamedownloadpaused_msg"], textStrings["msgbox_notice_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    MirrorDropdown.SelectedIndex = (int)Mirror;
                    return;
                }
                try
                {
                    if (File.Exists(gameArchivePath))
                        File.Delete(gameArchivePath);
                }
                catch
                {
                    Log($"Delete ERROR: {gameArchivePath}");
                }
                download = null;
                DownloadPaused = false;
                DeleteGameFiles(false);
            }
            if (Mirror == HI3Mirror.miHoYo && index != 0)
            {
                if (MessageBox.Show(textStrings["msgbox_mirrorinfo_msg"], textStrings["msgbox_notice_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                {
                    MirrorDropdown.SelectedIndex = 0;
                    return;
                }
            }
            switch (index)
            {
                case 0:
                    Mirror = HI3Mirror.miHoYo;
                    break;
                case 1:
                    Mirror = HI3Mirror.MediaFire;
                    break;
                case 2:
                    Mirror = HI3Mirror.GoogleDrive;
                    break;
            }
            try
            {
                LauncherRegKey.SetValue("LastSelectedMirror", index, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to write value with key LastSelectedMirror to registry:\n{ex}");
            }
            GameUpdateCheck(false);
            Log($"Selected mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");
        }

        //removed FPS prefix
        private void InputBoxTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.Any(x => Char.IsDigit(x));
        }
        //removed FPS prefix
        // https://stackoverflow.com/q/1268552/7570821
        private void InputBoxTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            Boolean IsTextAllowed(String text)
            {
                return Array.TrueForAll<Char>(text.ToCharArray(),
                    delegate (Char c) { return Char.IsDigit(c) || Char.IsControl(c); });
            }

            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void DownloadCacheBoxFullCacheButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show($"{textStrings["msgbox_download_cache_1_msg"]}\n{string.Format(textStrings["msgbox_download_cache_3_msg"], BpUtility.ToBytesCount((long)gameCacheMetadata.fileSize))}", textStrings["contextmenu_downloadcache"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            DownloadCacheBox.Visibility = Visibility.Collapsed;
            DownloadGameCache(true);
        }

        private void DownloadCacheBoxNumericFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show($"{textStrings["msgbox_download_cache_2_msg"]}\n{string.Format(textStrings["msgbox_download_cache_3_msg"], BpUtility.ToBytesCount((long)gameCacheMetadataNumeric.fileSize))}", textStrings["contextmenu_downloadcache"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            DownloadCacheBox.Visibility = Visibility.Collapsed;
            DownloadGameCache(false);
        }

        private void DownloadCacheBoxCloseButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadCacheBox.Visibility = Visibility.Collapsed;
        }

        private void FPSInputBoxOKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FPSInputBoxTextBoxCombat.Text = string.Concat(FPSInputBoxTextBoxCombat.Text.Where(c => !char.IsWhiteSpace(c)));
                FPSInputBoxTextBoxMenu.Text = string.Concat(FPSInputBoxTextBoxMenu.Text.Where(c => !char.IsWhiteSpace(c)));
                if (string.IsNullOrEmpty(FPSInputBoxTextBoxCombat.Text) || string.IsNullOrEmpty(FPSInputBoxTextBoxMenu.Text))
                {
                    MessageBox.Show(textStrings["msgbox_customfps_1_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                int fpsC = Int32.Parse(FPSInputBoxTextBoxCombat.Text);
                int fpsM = Int32.Parse(FPSInputBoxTextBoxMenu.Text);
                if (fpsC < 1 || fpsM < 1)
                {
                    MessageBox.Show(textStrings["msgbox_customfps_2_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else if (fpsC < 30 || fpsM < 30)
                {
                    if (MessageBox.Show(textStrings["msgbox_customfps_3_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                //gameGraphicSettings.IsUserDefinedGrade = false;
                //gameGraphicSettings.IsUserDefinedVolatile = true;
                gameGraphicSettings.TargetFrameRateForInLevel = fpsC;
                gameGraphicSettings.TargetFrameRateForOthers = fpsM;
                var valueAfter = Encoding.UTF8.GetBytes($"{JsonConvert.SerializeObject(gameGraphicSettings)}\0");
                RegistryKey key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                key.SetValue("GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411", valueAfter, RegistryValueKind.Binary);
                key.Close();
                FPSInputBox.Visibility = Visibility.Collapsed;
                Log($"Set custom Combat FPS to {fpsC} OK");
                Log($"Set custom Menu FPS to {fpsM} OK");
                MessageBox.Show(string.Format(textStrings["msgbox_customfps_4_msg"], fpsC, fpsM), textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR:\n{ex}");
                if (MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void FPSInputBoxCancelButton_Click(object sender, RoutedEventArgs e)
        {
            FPSInputBox.Visibility = Visibility.Collapsed;
        }
        private void ResolutionInputBoxOKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResolutionInputBoxTextBoxH.Text = string.Concat(ResolutionInputBoxTextBoxH.Text.Where(c => !char.IsWhiteSpace(c)));
                ResolutionInputBoxTextBoxW.Text = string.Concat(ResolutionInputBoxTextBoxW.Text.Where(c => !char.IsWhiteSpace(c)));
                if (string.IsNullOrEmpty(ResolutionInputBoxTextBoxH.Text) || string.IsNullOrEmpty(ResolutionInputBoxTextBoxW.Text))
                {
                    MessageBox.Show(textStrings["msgbox_customfps_1_msg"], textStrings["contextmenu_customresolution"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                bool cFullscreen = (bool)ToggleFullscreen.IsChecked;
                int cBinFullscreen = cFullscreen ? 1 : 0;
                int cHeight = Int32.Parse(ResolutionInputBoxTextBoxH.Text);
                int cWidth = Int32.Parse(ResolutionInputBoxTextBoxW.Text);
                if (cHeight < 1 || cWidth < 1)
                {
                    MessageBox.Show(textStrings["msgbox_customfps_2_msg"], textStrings["contextmenu_customresolution"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else if (cHeight > cWidth)
                {
                    if (MessageBox.Show(textStrings["msgbox_customresolution_3_msg"], textStrings["contextmenu_customresolution"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                gameGraphicSettings.height = cHeight;
                gameGraphicSettings.width = cWidth;
                gameGraphicSettings.isfullScreen = cFullscreen;
                var valueAfter = Encoding.UTF8.GetBytes($"{JsonConvert.SerializeObject(gameGraphicSettings)}\0");
                RegistryKey key = Registry.CurrentUser.OpenSubKey(GameRegistryPath,true);
                key.SetValue("GENERAL_DATA_V2_ScreenSettingData_h1916288658", valueAfter, RegistryValueKind.Binary);

                string iniFullscreen = "Screenmanager Is Fullscreen mode_h3981298716";
                string iniWidth = "Screenmanager Resolution Width_h182942802";
                string iniHeight = "Screenmanager Resolution Height_h2627697771";

                //Tency Addition (Suspisious at best)
                //This section forces the game to start in the resolution set above.
                //This prevents strange behavior at launch and enables the cinimatic effect.
                if (key.GetValue(iniFullscreen) != null)
                    key.SetValue(iniFullscreen, cBinFullscreen, RegistryValueKind.DWord);
                if (key.GetValue(iniWidth) != null)
                    key.SetValue(iniWidth, cWidth, RegistryValueKind.DWord);
                if (key.GetValue(iniHeight) != null)
                    key.SetValue(iniHeight, cHeight, RegistryValueKind.DWord);
                key.Close();
                ResolutionInputBox.Visibility = Visibility.Collapsed;
                Log($"Set custom Resolution to {cWidth} x {cHeight} OK");
                MessageBox.Show(string.Format(textStrings["msgbox_customresolution_4_msg"], cWidth, cHeight, cFullscreen), textStrings["contextmenu_customresolution"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR:\n{ex}");
                if (MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void ResolutionInputBoxCancelButton_Click(object sender, RoutedEventArgs e)
        {
            ResolutionInputBox.Visibility = Visibility.Collapsed;
        }
        private void ChangelogBoxCloseButton_Click(object sender, RoutedEventArgs e)
        {
            ChangelogBoxMessageTextBlock.Visibility = Visibility.Collapsed;
            ChangelogBoxScrollViewer.Height = 325;
            ChangelogBox.Visibility = Visibility.Collapsed;
        }

        private void ShowLogCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            LogBox.Visibility = Visibility.Visible;
            try
            {
                LauncherRegKey.SetValue("ShowLog", 1, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to write value with key ShowLog to registry:\n{ex}");
            }
        }

        private void ShowLogCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            LogBox.Visibility = Visibility.Collapsed;
            try
            {
                LauncherRegKey.SetValue("ShowLog", 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Log($"ERROR: Failed to write value with key ShowLog to registry:\n{ex}");
            }
        }

        private void AboutBoxGitHubButton_Click(object sender, RoutedEventArgs e)
        {
            AboutBox.Visibility = Visibility.Collapsed;
            BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher", null, rootPath, true);
        }

        private void AboutBoxCloseButton_Click(object sender, RoutedEventArgs e)
        {
            AboutBox.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (Status == LauncherStatus.Downloading)
            {
                try
                {
                    if (download == null)
                    {
                        if (MessageBox.Show($"{textStrings["msgbox_abort_1_msg"]}\n{textStrings["msgbox_abort_2_msg"]}", textStrings["msgbox_abort_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            Status = LauncherStatus.CleaningUp;
                            try
                            {
                                if (File.Exists(gameArchivePath))
                                    File.Delete(gameArchivePath);
                            }
                            catch
                            {
                                Log($"Delete ERROR: {gameArchivePath}");
                            }
                            try
                            {
                                if (File.Exists(cacheArchivePath))
                                    File.Delete(cacheArchivePath);
                            }
                            catch
                            {
                                Log($"Delete ERROR: {cacheArchivePath}");
                            }
                        }
                        else
                        {
                            e.Cancel = true;
                        }
                    }
                    else
                    {
                        if (MessageBox.Show($"{textStrings["msgbox_abort_1_msg"]}\n{textStrings["msgbox_abort_3_msg"]}", textStrings["msgbox_abort_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            download.Pause();
                            WriteVersionInfo(false);
                        }
                        else
                        {
                            e.Cancel = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install the game:\n{ex}");
                    if (MessageBox.Show(textStrings["msgbox_installerror_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        return;
                    }
                }
            }
            else if (Status == LauncherStatus.Verifying || Status == LauncherStatus.Unpacking || Status == LauncherStatus.CleaningUp || Status == LauncherStatus.Uninstalling || Status == LauncherStatus.Working)
            {
                e.Cancel = true;
            }
        }

        private string CheckForExistingGameDirectory(string path)
        {
            var pathVariants = new List<string>(new string[]
            {
                path.Replace(@"Honkai Impact 3rd\Honkai Impact 3rd", @"Honkai Impact 3rd\Games"),
                path.Replace(@"Honkai Impact 3\Honkai Impact 3", @"Honkai Impact 3\Games"),
                path.Replace(@"Honkai Impact 3rd\Games\Honkai Impact 3rd", @"Honkai Impact 3rd\Games"),
                path.Replace(@"Honkai Impact 3\Games\Honkai Impact 3", @"Honkai Impact 3\Games"),
                path.Replace(@"\BH3_Data\Honkai Impact 3rd", string.Empty),
                path.Replace(@"\BH3_Data\Honkai Impact 3", string.Empty),
                Path.Combine(path, "Games"),
                Path.Combine(path, "Honkai Impact 3rd"),
                Path.Combine(path, "Honkai Impact 3"),
                Path.Combine(path, "Honkai Impact 3rd", "Games"),
                Path.Combine(path, "Honkai Impact 3", "Games")
            });
            if (path.Length >= 16)
            {
                pathVariants.Add(path.Substring(0, path.Length - 16));
            }
            if (path.Length >= 18)
            {
                pathVariants.Add(path.Substring(0, path.Length - 18));
            }

            if (File.Exists(Path.Combine(path, gameExeName)))
            {
                return path;
            }
            else
            {
                foreach (var variant in pathVariants)
                {
                    if (File.Exists(Path.Combine(variant, gameExeName)))
                        return variant;
                }
                return string.Empty;
            }
        }

        private int CheckForExistingGameClientServer()
        {
            var path = Path.Combine(gameInstallPath, @"BH3_Data\app.info");
            if (File.Exists(path))
            {
                var gameTitleLine = File.ReadLines(path).Skip(1).Take(1).First();
                if (!string.IsNullOrEmpty(gameTitleLine))
                {
                    if (gameTitleLine.Contains("Honkai Impact 3rd"))
                        return 0;
                    else if (gameTitleLine.Contains("Honkai Impact 3"))
                        return 1;

                }
            }
            return -1;
        }

        private void ToggleContextMenuItems(bool val, bool leaveUninstallEnabled)
        {
            foreach (dynamic item in OptionsContextMenu.Items)
            {
                if (item.GetType() == typeof(MenuItem) && (item.Header.ToString() == textStrings["contextmenu_changelog"] || item.Header.ToString() == textStrings["contextmenu_about"]))
                    continue;
                if (!val && leaveUninstallEnabled && (item.GetType() == typeof(MenuItem) && item.Header.ToString() == textStrings["contextmenu_uninstall"]))
                    continue;
                item.IsEnabled = val;
            }
        }

        public struct Version
        {
            internal static Version zero = new Version(0, 0, 0, 0);

            private int major, minor, date, hotfix;

            internal Version(int _major, int _minor, int _date, int _hotfix)
            {
                major = _major;
                minor = _minor;
                date = _date;
                hotfix = _hotfix;
            }

            internal Version(string _version)
            {
                string[] _versionStrings = _version.Split('.');
                if (_versionStrings.Length != 4)
                {
                    major = 0;
                    minor = 0;
                    date = 0;
                    hotfix = 0;
                    return;
                }

                major = int.Parse(_versionStrings[0]);
                minor = int.Parse(_versionStrings[1]);
                date = int.Parse(_versionStrings[2]);
                hotfix = int.Parse(_versionStrings[3]);
            }

            internal bool IsDifferentThan(Version _otherVersion)
            {
                if (major != _otherVersion.major)
                {
                    return true;
                }
                else if (minor != _otherVersion.minor)
                {
                    return true;
                }
                else if (date != _otherVersion.date)
                {
                    return true;
                }
                else if (hotfix != _otherVersion.hotfix)
                {
                    return true;
                }
                return false;
            }
            public override string ToString()
            {
                return $"{major}.{minor}.{date}.{hotfix}";
            }
        }

        public struct GameVersion
        {
            internal static GameVersion zero = new GameVersion(0, 0, 0, string.Empty);

            private int major, minor, patch;
            private string build;

            internal GameVersion(int _major, int _minor, int _patch, string _build)
            {
                major = _major;
                minor = _minor;
                patch = _patch;
                build = _build;
            }

            internal GameVersion(string _version)
            {
                string[] _versionStrings = _version.Split('.', '_');
                if (_versionStrings.Length != 4)
                {
                    major = 0;
                    minor = 0;
                    patch = 0;
                    build = string.Empty;
                    return;
                }

                major = int.Parse(_versionStrings[0]);
                minor = int.Parse(_versionStrings[1]);
                patch = int.Parse(_versionStrings[2]);
                build = _versionStrings[3];
            }

            internal bool IsDifferentThan(GameVersion _otherVersion)
            {
                if (major != _otherVersion.major)
                {
                    return true;
                }
                else if (minor != _otherVersion.minor)
                {
                    return true;
                }
                else if (patch != _otherVersion.patch)
                {
                    return true;
                }
                else if (build != _otherVersion.build)
                {
                    return true;
                }
                return false;
            }

            public override string ToString()
            {
                return $"{major}.{minor}.{patch}_{build}";
            }
        }

        public void Log(string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return;

#if DEBUG
            Console.WriteLine(msg);
#endif
            Dispatcher.Invoke(() =>
            {
                LogBoxTextBox.AppendText(msg + Environment.NewLine);
                LogBoxScrollViewer.ScrollToEnd();
            });
        }
    }
}
