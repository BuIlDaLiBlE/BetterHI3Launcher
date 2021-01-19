using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        public static readonly Version localLauncherVersion = new Version("1.0.20210121.0");
        public static readonly string rootPath = Directory.GetCurrentDirectory();
        public static readonly string localLowPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}Low";
        public static readonly string backgroundImagePath = Path.Combine(localLowPath, @"Bp\Better HI3 Launcher");
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
        TimedWebClient webClient = new TimedWebClient{Encoding = Encoding.UTF8, Timeout = 10000};
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
                        ToggleContextMenuItems(val);
                    }
                    void ToggleProgressBar(dynamic val)
                    {
                        ProgressBar.Visibility = val;
                        ProgressBar.IsIndeterminate = true;
                    }

                    _status = value;
                    WindowState = WindowState.Normal;
                    switch(_status)
                    {
                        case LauncherStatus.Ready:
                        case LauncherStatus.DownloadPaused:
                            ProgressText.Text = String.Empty;
                            ToggleUI(true);
                            ToggleProgressBar(Visibility.Hidden);
                            break;
                        case LauncherStatus.Error:
                            ProgressText.Text = textStrings["progresstext_error"];
                            ToggleUI(false);
                            ToggleProgressBar(Visibility.Hidden);
                            ShowLogCheckBox.IsChecked = true;
                            break;
                        case LauncherStatus.CheckingUpdates:
                            ProgressText.Text = textStrings["progresstext_checkingupdate"];
                            ToggleUI(false);
                            ToggleProgressBar(Visibility.Visible);
                            break;
                        case LauncherStatus.Downloading:
                            LaunchButton.Content = textStrings["button_downloading"];
                            ProgressText.Text = textStrings["progresstext_initiating_download"];
                            ToggleUI(false);
                            ToggleProgressBar(Visibility.Visible);
                            ProgressBar.IsIndeterminate = false;
                            break;
                        case LauncherStatus.Working:
                            ToggleUI(false);
                            ToggleProgressBar(Visibility.Visible);
                            break;
                        case LauncherStatus.Verifying:
                            ProgressText.Text = textStrings["progresstext_verifying"];
                            ToggleUI(false);
                            ToggleProgressBar(Visibility.Visible);
                            break;
                        case LauncherStatus.Unpacking:
                            ProgressText.Text = textStrings["progresstext_unpacking_1"];
                            ToggleProgressBar(Visibility.Visible);
                            ProgressBar.IsIndeterminate = false;
                            break;
                        case LauncherStatus.CleaningUp:
                            ProgressText.Text = textStrings["progresstext_cleaningup"];
                            break;
                        case LauncherStatus.UpdateAvailable:
                            ToggleUI(true);
                            ToggleProgressBar(Visibility.Hidden);
                            ToggleContextMenuItems(false);
                            break;
                        case LauncherStatus.Uninstalling:
                            ProgressText.Text = textStrings["progresstext_uninstalling"];
                            ToggleUI(false);
                            ToggleProgressBar(Visibility.Visible);
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
                switch(_gameserver)
                {
                    case HI3Server.Global:
                        RegistryVersionInfo = "VersionInfoGlobal";
                        GameRegistryPath = @"SOFTWARE\miHoYo\Honkai Impact 3rd";
                        GameRegistryLocalVersionRegValue = "USD_V2_201880924_LocalVersion_h3429574199";
                        gameWebProfileURL = "https://global.user.honkaiimpact3.com";
                        break;
                    case HI3Server.SEA:
                        RegistryVersionInfo = "VersionInfoSEA";
                        GameRegistryPath = @"SOFTWARE\miHoYo\Honkai Impact 3";
                        GameRegistryLocalVersionRegValue = "USD_V2_18149666_LocalVersion_h2804958440";
                        gameWebProfileURL = "https://asia.user.honkaiimpact3.com";
                        break;
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
                switch(lang)
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
            switch(OSLanguage)
            {
                case "ru-RU":
                case "uk-UA":
                case "be-BY":
                    LauncherLanguage = "ru";
                    break;
            }
            var LanguageRegValue = LauncherRegKey.GetValue("Language");
            if(LanguageRegValue != null)
            {
                if(LauncherRegKey.GetValueKind("Language") == RegistryValueKind.String)
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
            ChangelogBoxTitleTextBlock.Text = textStrings["changelogbox_title"];
            ChangelogBoxMessageTextBlock.Text = textStrings["changelogbox_msg"];
            ChangelogBoxOKButton.Content = textStrings["button_ok"];
            AboutBoxTitleTextBlock.Text = textStrings["contextmenu_about"];
            AboutBoxMessageTextBlock.Text = $"{textStrings["aboutbox_msg"]}\nMade by Bp (BuIlDaLiBlE production).\nDiscord: BuIlDaLiBlE#3202";
            AboutBoxGitHubButton.Content = textStrings["button_github"];
            AboutBoxOKButton.Content = textStrings["button_ok"];
            ShowLogLabel.Text = textStrings["label_log"];

            Grid.MouseLeftButtonDown += delegate{DragMove();};

            try
            {
                FetchOnlineVersionInfo();
                FetchmiHoYoVersionInfo();
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                if(MessageBox.Show($"{textStrings["msgbox_neterror_msg"]}:\n{ex}", textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }

            OptionsContextMenu.Items.Clear();
            var CMDownloadCache = new MenuItem{Header = textStrings["contextmenu_downloadcache"]};
            CMDownloadCache.Click += async (sender, e) => await CM_DownloadCache_Click(sender, e);
            OptionsContextMenu.Items.Add(CMDownloadCache);
            var CMUninstall = new MenuItem{Header = textStrings["contextmenu_uninstall"]};
            CMUninstall.Click += async (sender, e) => await CM_Uninstall_Click(sender, e);
            OptionsContextMenu.Items.Add(CMUninstall);
            OptionsContextMenu.Items.Add(new Separator());
            var CMFixSubtitles = new MenuItem{Header = textStrings["contextmenu_fixsubs"]};
            CMFixSubtitles.Click += async (sender, e) => await CM_FixSubtitles_Click(sender, e);
            OptionsContextMenu.Items.Add(CMFixSubtitles);
            var CMFixUpdateLoop = new MenuItem{Header = textStrings["contextmenu_download_type"]};
            CMFixUpdateLoop.Click += (sender, e) => CM_FixUpdateLoop_Click(sender, e);
            OptionsContextMenu.Items.Add(CMFixUpdateLoop);
            var CMCustomFPS = new MenuItem{Header = textStrings["contextmenu_customfps"]};
            CMCustomFPS.Click += (sender, e) => CM_CustomFPS_Click(sender, e);
            OptionsContextMenu.Items.Add(CMCustomFPS);
            var CMResetGameSettings = new MenuItem{Header = textStrings["contextmenu_resetgamesettings"]};
            CMResetGameSettings.Click += (sender, e) => CM_ResetGameSettings_Click(sender, e);
            OptionsContextMenu.Items.Add(CMResetGameSettings);
            OptionsContextMenu.Items.Add(new Separator());
            var CMWebProfile = new MenuItem{Header = textStrings["contextmenu_web_profile"]};
            CMWebProfile.Click += (sender, e) => CM_WebProfile_Click(sender, e);
            OptionsContextMenu.Items.Add(CMWebProfile);
            var CMFeedback = new MenuItem{Header = textStrings["contextmenu_feedback"]};
            CMFeedback.Click += (sender, e) => CM_Feedback_Click(sender, e);
            OptionsContextMenu.Items.Add(CMFeedback);
            var CMChangelog = new MenuItem{Header = textStrings["contextmenu_changelog"]};
            CMChangelog.Click += (sender, e) => CM_Changelog_Click(sender, e);
            OptionsContextMenu.Items.Add(CMChangelog);
            var CMAbout = new MenuItem{Header = textStrings["contextmenu_about"]};
            CMAbout.Click += (sender, e) => CM_About_Click(sender, e);
            OptionsContextMenu.Items.Add(CMAbout);
            ToggleContextMenuItems(false);

            try
            {
                var LastSelectedServerRegValue = LauncherRegKey.GetValue("LastSelectedServer");
                if(LastSelectedServerRegValue != null)
                {
                    if(LauncherRegKey.GetValueKind("LastSelectedServer") == RegistryValueKind.DWord)
                    {
                        if((int)LastSelectedServerRegValue == 0)
                            Server = HI3Server.Global;
                        else if((int)LastSelectedServerRegValue == 1)
                            Server = HI3Server.SEA;
                    }
                }
                else
                {
                    Server = HI3Server.Global;
                }
                ServerDropdown.SelectedIndex = (int)Server;
                var LastSelectedMirrorRegValue = LauncherRegKey.GetValue("LastSelectedMirror");
                if(LastSelectedMirrorRegValue != null)
                {
                    if(LauncherRegKey.GetValueKind("LastSelectedMirror") == RegistryValueKind.DWord)
                    {
                        if((int)LastSelectedMirrorRegValue == 0)
                            Mirror = HI3Mirror.miHoYo;
                        else if((int)LastSelectedMirrorRegValue == 1)
                            Mirror = HI3Mirror.MediaFire;
                        else if((int)LastSelectedMirrorRegValue == 2)
                            Mirror = HI3Mirror.GoogleDrive;
                    }
                }
                else
                {
                    Mirror = HI3Mirror.miHoYo;
                }
                MirrorDropdown.SelectedIndex = (int)Mirror;
                var ShowLogRegValue = LauncherRegKey.GetValue("ShowLog");
                if(ShowLogRegValue != null)
                {
                    if(LauncherRegKey.GetValueKind("ShowLog") == RegistryValueKind.DWord)
                    {
                        if((int)ShowLogRegValue == 1)
                            ShowLogCheckBox.IsChecked = true;
                    }
                }
                Log($"Using server: {((ComboBoxItem)ServerDropdown.SelectedItem).Content as string}");
                Log($"Using mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");
                string backgroundImageName = miHoYoVersionInfo.bg_file_name.ToString();
                var BackgroundImageNameRegValue = LauncherRegKey.GetValue("BackgroundImageName");
                if(BackgroundImageNameRegValue != null && File.Exists(Path.Combine(backgroundImagePath, backgroundImageName)) && backgroundImageName == BackgroundImageNameRegValue.ToString())
                {
                    Log($"Background image {backgroundImageName} exists, using it");
                    BackgroundImage.Source = new BitmapImage(new Uri(Path.Combine(backgroundImagePath, backgroundImageName)));
                }
                else
                {
                    Log($"Background image doesn't exist, attempting to download: {backgroundImageName}");
                    try
                    {
                        webClient.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
                        if(!Directory.Exists(backgroundImagePath))
                            Directory.CreateDirectory(backgroundImagePath);
                        webClient.DownloadFile(new Uri($"{miHoYoVersionInfo.download_url.ToString()}/{miHoYoVersionInfo.bg_file_name.ToString()}"), Path.Combine(backgroundImagePath, backgroundImageName));
                        BackgroundImage.Source = new BitmapImage(new Uri(Path.Combine(backgroundImagePath, backgroundImageName)));
                        LauncherRegKey.SetValue("BackgroundImageName", backgroundImageName);
                        LauncherRegKey.Close();
                        LauncherRegKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Bp\Better HI3 Launcher", true);
                        Log($"Downloaded background image: {backgroundImageName}");
                    }
                    catch(Exception ex)
                    {
                        Log("An error occurred while downloading background image!");
                        Status = LauncherStatus.Error;
                        if(MessageBox.Show($"{textStrings["msgbox_neterror_msg"]}:\n{ex}", textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        {
                            return;
                        }
                    }
                }
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
        }

        private void FetchOnlineVersionInfo()
        {
            #if DEBUG
                var version_info_url = new[]{"https://bpnet.host/bh3_debug.json"};
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
            if(onlineVersionInfo.status == "success")
            {
                onlineVersionInfo = onlineVersionInfo.launcherstatus;
                launcherExeName = onlineVersionInfo.launcher_info.name;
                launcherPath = Path.Combine(rootPath, launcherExeName);
                launcherArchivePath = Path.Combine(rootPath, onlineVersionInfo.launcher_info.url.ToString().Substring(onlineVersionInfo.launcher_info.url.ToString().LastIndexOf('/') + 1));
                Dispatcher.Invoke(() =>
                {
                    LauncherVersionText.Text = $"{textStrings["launcher_version"]}: v{localLauncherVersion}";
                    ChangelogBoxTextBox.Text += onlineVersionInfo.launcher_info.changelog[LauncherLanguage];
                });
            }
            else
            {
                if(MessageBox.Show($"{textStrings["msgbox_neterror_msg"]}.", textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
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
            if(Server == HI3Server.Global)
                url = onlineVersionInfo.game_info.mirror.mihoyo.version_info.global.ToString();
            else
                url = onlineVersionInfo.game_info.mirror.mihoyo.version_info.os.ToString();
            var webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.UserAgent = userAgent;
            webRequest.Timeout = 30000;
            using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                using(var data = new MemoryStream())
                {
                    webResponse.GetResponseStream().CopyTo(data);
                    miHoYoVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                    miHoYoVersionInfo.last_modified = webResponse.LastModified.ToString();
                }
            }
            gameArchiveName = miHoYoVersionInfo.full_version_file.name.ToString();
            webRequest = (HttpWebRequest)WebRequest.Create($"{miHoYoVersionInfo.download_url}/{gameArchiveName}");
            webRequest.Method = "HEAD";
            webRequest.UserAgent = userAgent;
            webRequest.Timeout = 30000;
            using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
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
            if(Server == HI3Server.Global)
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
                for(int i = 0; i < url.Length; i++)
                {
                    var webRequest = (HttpWebRequest)WebRequest.Create(url[i]);
                    webRequest.Method = "HEAD";
                    webRequest.UserAgent = userAgent;
                    webRequest.Timeout = 30000;
                    using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                    {
                        time[i] = webResponse.LastModified;
                    }
                }
                if(DateTime.Compare(time[0], time[1]) >= 0)
                    return time[0];
                else
                    return time[1];
            }
            catch
            {
                return new DateTime(0);
            }
        }

        private dynamic FetchGDFileMetadata(string id)
        {
            string key = "AIzaSyBM8587dCDzMYAN0Y5LcGS-NiZ2TTRZelA";
            string url = $"https://www.googleapis.com/drive/v2/files/{id}?key={key}";

            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(url);
                webRequest.UserAgent = userAgent;
                webRequest.Timeout = 30000;
                using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    using(var data = new MemoryStream())
                    {
                        webResponse.GetResponseStream().CopyTo(data);
                        var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                        return json;
                    }
                }
            }
            catch(WebException ex)
            {
                Status = LauncherStatus.Error;
                if(ex.Response != null)
                {
                    using(var data = new MemoryStream())
                    {
                        ex.Response.GetResponseStream().CopyTo(data);
                        var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                        string message;
                        if(json.error != null)
                            message = json.error.errors[0].message;
                        else
                            message = ex.Message;
                        Log($"ERROR: Failed to fetch Google Drive file metadata:\n{message}");
                        Dispatcher.Invoke(() => { MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], message), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error); });
                        Status = LauncherStatus.Ready;
                    }
                }
                return null;
            }
        }

        private dynamic FetchMediaFireFileMetadata(string id, bool numeric)
        {
            string url = $"https://www.mediafire.com/file/{id}";

            if(String.IsNullOrEmpty(id))
                throw new ArgumentNullException();
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(url);
                webRequest.Method = "HEAD";
                webRequest.UserAgent = userAgent;
                webRequest.Timeout = 30000;
                using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    dynamic metadata = new ExpandoObject();
                    metadata.title = webResponse.Headers["Content-Disposition"].Replace("attachment; filename=", String.Empty).Replace("\"", String.Empty);
                    metadata.downloadUrl = url;
                    metadata.fileSize = webResponse.ContentLength;
                    if(!numeric)
                    {
                        if(Server == HI3Server.Global)
                            metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache.global.md5.ToString();
                        else
                            metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache.os.md5.ToString();
                    }
                    else
                    {
                        if(Server == HI3Server.Global)
                            metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.md5.ToString();
                        else
                            metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.md5.ToString();
                    }
                    return metadata;
                }
            }
            catch(WebException ex)
            {
                Status = LauncherStatus.Error;
                if(ex.Response != null)
                {
                    string message = ex.Message;
                    Log($"ERROR: Failed to fetch MediaFire file metadata:\n{message}");
                    Dispatcher.Invoke(() => {MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], message), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
                    Status = LauncherStatus.Ready;
                }
                return null;
            }
        }

        private bool LauncherUpdateCheck()
        {
            Version onlineLauncherVersion = new Version(onlineVersionInfo.launcher_info.version.ToString());
            if(localLauncherVersion.IsDifferentThan(onlineLauncherVersion))
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
                    if(Mirror == HI3Mirror.miHoYo)
                    {
                        // space_usage is probably when archive is unpacked, here I get the download size instead
                        //download_size = miHoYoVersionInfo.space_usage;
                        download_size = miHoYoVersionInfo.size;
                    }
                    else if(Mirror == HI3Mirror.MediaFire)
                    {
                        dynamic mediafire_metadata;
                        if(Server == HI3Server.Global)
                            mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString(), false);
                        else
                            mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString(), false);
                        if(mediafire_metadata == null)
                            return;
                        download_size = mediafire_metadata.fileSize;
                    }
                    else if(Mirror == HI3Mirror.GoogleDrive)
                    {
                        dynamic gd_metadata;
                        if(Server == HI3Server.Global)
                            gd_metadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
                        else
                            gd_metadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
                        if(gd_metadata == null)
                            return;
                        download_size = gd_metadata.fileSize;
                    }
                    if(LauncherRegKey.GetValue(RegistryVersionInfo) != null)
                    {
                        localVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])LauncherRegKey.GetValue(RegistryVersionInfo)));
                        GameVersion localGameVersion = new GameVersion(localVersionInfo.game_info.version.ToString());
                        gameInstallPath = localVersionInfo.game_info.install_path.ToString();
                        gameArchivePath = Path.Combine(gameInstallPath, gameArchiveName);
                        gameExePath = Path.Combine(gameInstallPath, "BH3.exe");
                        game_needs_update = GameUpdateCheckSimple();
                        Log($"Game version: {localGameVersion}");

                        if(game_needs_update)
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
                                ToggleContextMenuItems(true);
                            });
                        }
                        if(File.Exists(gameArchivePath))
                        {
                            DownloadPaused = true;
                            var remaining_size = download_size - new FileInfo(gameArchivePath).Length;
                            Dispatcher.Invoke(() =>
                            {
                                if(remaining_size <= 0)
                                {
                                    LaunchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                }
                                else
                                {
                                    LaunchButton.Content = textStrings["button_resume"];
                                    ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {BpUtility.ToBytesCount(remaining_size)}";
                                }
                            });
                        }
                    }
                    else
                    {
                        Log("Game is not installed :^(");
                        if(serverChanged)
                            await FetchmiHoYoVersionInfoAsync();
                        Status = LauncherStatus.Ready;
                        Dispatcher.Invoke(() =>
                        {
                            LaunchButton.Content = textStrings["button_download"];
                            ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {BpUtility.ToBytesCount(download_size)}";
                            var path = CheckForExistingGameDirectory(rootPath);
                            if(!String.IsNullOrEmpty(path))
                            {
                                if(MessageBox.Show(string.Format(textStrings["msgbox_installexisting_msg"], path), textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                                {
                                    Log($"Existing install directory selected: {path}");
                                    gameInstallPath = path;
                                    var server = CheckForExistingGameClientServer();
                                    if(server >= 0)
                                    {
                                        if((int)Server == server)
                                            GameUpdateCheck(false);
                                        else
                                            ServerDropdown.SelectedIndex = server;
                                        WriteVersionInfo();
                                    }
                                    else
                                    {
                                        Status = LauncherStatus.Error;
                                        Log($"ERROR: Directory {gameInstallPath} doesn't contain a valid installation of the game. This launcher supports only Global and SEA clients!");
                                        if(MessageBox.Show(string.Format(textStrings["msgbox_installexistinginvalid_msg"]), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                                        {
                                            Status = LauncherStatus.Ready;
                                            return;
                                        }
                                    }
                                }
                            }
                        });
                    }
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Checking for game update failed:\n{ex}");
                    Dispatcher.Invoke(() =>
                    {
                        if(MessageBox.Show(string.Format(textStrings["msgbox_updatecheckerror_msg"], ex), textStrings["msgbox_updatecheckerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        {
                            Application.Current.Shutdown();
                            return;
                        }
                    });
                }
            });
        }

        private bool GameUpdateCheckSimple()
        {
            if(localVersionInfo != null)
            {
                FetchmiHoYoVersionInfo();
                GameVersion localGameVersion = new GameVersion(localVersionInfo.game_info.version.ToString());
                GameVersion onlineGameVersion = new GameVersion(miHoYoVersionInfo.cur_version.ToString());
                if(onlineGameVersion.IsDifferentThan(localGameVersion))
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
                using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    using(var data = new MemoryStream())
                    {
                        webResponse.GetResponseStream().CopyTo(data);
                        data.Seek(0, SeekOrigin.Begin);
                        using(FileStream file = new FileStream(launcherArchivePath, FileMode.Create))
                        {
                            data.CopyTo(file);
                            file.Flush();
                        }
                    }
                }
                Log("Launcher update download OK");
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Dispatcher.Invoke(() =>
                {
                    if(MessageBox.Show(string.Format(textStrings["msgbox_launcherdownloaderror_msg"], ex), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                });
            }
        }

        private async Task DownloadGameFile()
        {
            try
            {
                string download_url;
                string md5;
                bool abort = false;
                if(Mirror == HI3Mirror.miHoYo)
                {
                    download_url = $"{miHoYoVersionInfo.download_url.ToString()}/{gameArchiveName}";
                    md5 = miHoYoVersionInfo.full_version_file.md5.ToString();
                }
                else if(Mirror == HI3Mirror.MediaFire)
                {
                    dynamic mediafire_metadata;
                    if(Server == HI3Server.Global)
                        mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString(), false);
                    else
                        mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString(), false);
                    if(mediafire_metadata == null)
                        return;
                    download_url = mediafire_metadata.downloadUrl.ToString();
                    md5 = mediafire_metadata.md5Checksum.ToString();
                    if(!mediafire_metadata.title.Contains(miHoYoVersionInfo.cur_version.ToString().Substring(0, 5)))
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Mirror is outdated!");
                        MessageBox.Show(textStrings["msgbox_gamedownloadmirrorold_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                    try
                    {
                        var webRequest = (HttpWebRequest)WebRequest.Create(download_url);
                        webRequest.UserAgent = userAgent;
                        webRequest.Timeout = 30000;
                        var webResponse = (HttpWebResponse)webRequest.GetResponse();
                    }
                    catch(WebException ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to download from MediaFire:\n{ex}");
                        MessageBox.Show(string.Format(textStrings["msgbox_gamedownloadmirrorerror_msg"], ex), textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }
                else
                {
                    dynamic gd_metadata;
                    if(Server == HI3Server.Global)
                        gd_metadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
                    else
                        gd_metadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
                    if(gd_metadata == null)
                        return;
                    download_url = gd_metadata.downloadUrl.ToString();
                    md5 = gd_metadata.md5Checksum.ToString();
                    if(DateTime.Compare(DateTime.Parse(miHoYoVersionInfo.last_modified.ToString()), DateTime.Parse(gd_metadata.modifiedDate.ToString())) > 0)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Mirror is outdated!");
                        MessageBox.Show(textStrings["msgbox_gamedownloadmirrorold_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                    try
                    {
                        var webRequest = (HttpWebRequest)WebRequest.Create(download_url);
                        webRequest.UserAgent = userAgent;
                        webRequest.Timeout = 30000;
                        var webResponse = (HttpWebResponse)webRequest.GetResponse();
                    }
                    catch(WebException ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to download from Google Drive:\n{ex}");
                        MessageBox.Show(string.Format(textStrings["msgbox_gamedownloadmirrorerror_msg"], ex), textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }

                Log($"Starting to download game archive {gameArchiveName}");
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
                    download = new DownloadPauseable(download_url, gameArchivePath);
                    download.Start();
                    while(download != null && !download.Done)
                    {
                        if(DownloadPaused)
                            continue;
                        tracker.SetProgress(download.BytesWritten, download.ContentLength);
                        eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = tracker.GetProgress() * 100;
                            ProgressText.Text = $"{string.Format(textStrings["progresstext_downloaded"], BpUtility.ToBytesCount(download.BytesWritten), BpUtility.ToBytesCount(download.ContentLength), tracker.GetBytesPerSecondString())}\n{string.Format(textStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"))}";
                        });
                        Thread.Sleep(100);
                    }
                    if(download == null)
                    {
                        abort = true;
                        return;
                    }
                    download = null;
                    Log("Game archive download OK");
                    while(BpUtility.IsFileLocked(new FileInfo(gameArchivePath)))
                        Thread.Sleep(10);
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = String.Empty;
                        LaunchButton.Content = textStrings["button_launch"];
                    });
                });
                try
                {
                    if(abort)
                        return;
                    await Task.Run(() =>
                    {
                        Log("Validating game archive");
                        Status = LauncherStatus.Verifying;
                        string actual_md5 = BpUtility.CalculateMD5(gameArchivePath);
                        if(actual_md5 != md5.ToUpper())
                        {
                            Status = LauncherStatus.Error;
                            Log($"ERROR: Validation failed. Supposed MD5: {md5}, actual MD5: {actual_md5}");
                            Dispatcher.Invoke(() =>
                            {
                                if(MessageBox.Show(textStrings["msgbox_verifyerror_2_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                                {
                                    if(File.Exists(gameArchivePath))
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
                        if(abort)
                            return;
                        var skippedFiles = new List<string>();
                        using(var archive = ArchiveFactory.Open(gameArchivePath))
                        {
                            int unpackedFiles = 0;
                            int fileCount = 0;

                            Log("Unpacking game archive...");
                            Status = LauncherStatus.Unpacking;
                            foreach(var entry in archive.Entries)
                            {
                                if(!entry.IsDirectory)
                                    fileCount++;
                            }
                            var reader = archive.ExtractAllEntries();
                            while(reader.MoveToNextEntry())
                            {
                                try
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        ProgressText.Text = string.Format(textStrings["progresstext_unpacking_2"], unpackedFiles + 1, fileCount);
                                        ProgressBar.Value = (unpackedFiles + 1f) / fileCount * 100;
                                    });
                                    reader.WriteEntryToDirectory(gameInstallPath, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
                                    if(!reader.Entry.IsDirectory)
                                        unpackedFiles++;
                                }
                                catch
                                {
                                    if(!reader.Entry.IsDirectory)
                                    {
                                        skippedFiles.Add(reader.Entry.ToString());
                                        fileCount--;
                                        Log($"Unpack ERROR: {reader.Entry}");
                                    }
                                }
                            }
                        }
                        if(skippedFiles.Count > 0)
                        {
                            Dispatcher.Invoke(() => {MessageBox.Show(textStrings["msgbox_extractskip_msg"], textStrings["msgbox_extractskip_title"], MessageBoxButton.OK, MessageBoxImage.Warning);});
                        }
                        Log("Game archive unpack OK");
                        File.Delete(gameArchivePath);
                        Dispatcher.Invoke(() => 
                        {
                            WriteVersionInfo();
                            Log("Game install OK");
                            GameUpdateCheck(false);
                        });
                    });
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install the game:\n{ex}");
                    Dispatcher.Invoke(() =>
                    {
                        if(MessageBox.Show(string.Format(textStrings["msgbox_installerror_msg"], ex), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        {
                            Dispatcher.Invoke(() => {LaunchButton.Content = textStrings["button_download"];});
                            Status = LauncherStatus.Ready;
                        }
                    });
                }
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download the game:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_gamedownloaderror_msg"], ex), textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Dispatcher.Invoke(() => {LaunchButton.Content = textStrings["button_download"];});
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void WriteVersionInfo()
        {
            try
            {
                dynamic versionInfo = new ExpandoObject();
                versionInfo.game_info = new ExpandoObject();
                versionInfo.game_info.version = miHoYoVersionInfo.cur_version.ToString();
                versionInfo.game_info.install_path = gameInstallPath;
                RegistryKey key;
                key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                if(LauncherRegKey.GetValue(RegistryVersionInfo) == null && (key != null && key.GetValue(GameRegistryLocalVersionRegValue) != null && key.GetValueKind(GameRegistryLocalVersionRegValue) == RegistryValueKind.Binary))
                {
                    var version = Encoding.UTF8.GetString((byte[])key.GetValue(GameRegistryLocalVersionRegValue)).TrimEnd('\u0000');
                    if(!miHoYoVersionInfo.cur_version.ToString().Contains(version))
                        versionInfo.game_info.version = version;
                }

                Log("Writing game version info to registry...");
                LauncherRegKey.SetValue(RegistryVersionInfo, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(versionInfo)), RegistryValueKind.Binary);
                LauncherRegKey.Close();
                LauncherRegKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Bp\Better HI3 Launcher", true);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_registryerror_msg"], ex), textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }
        }

        private void DeleteGameFiles(bool DeleteGame)
        {
            if(DeleteGame)
            {
                if(Directory.Exists(gameInstallPath))
                    Directory.Delete(gameInstallPath, true);
            }
            try{LauncherRegKey.DeleteValue(RegistryVersionInfo);}catch{}
            LaunchButton.Content = textStrings["button_download"];
        }

        private async void DownloadGameCache(bool FullCache)
        {
            try
            {
                string title;
                string download_url;
                string md5;
                bool abort = false;
                if(FullCache)
                {
                    title = gameCacheMetadata.title.ToString();
                    download_url = gameCacheMetadata.downloadUrl.ToString();
                    md5 = gameCacheMetadata.md5Checksum.ToString();
                }
                else
                {
                    title = gameCacheMetadataNumeric.title.ToString();
                    download_url = gameCacheMetadataNumeric.downloadUrl.ToString();
                    md5 = gameCacheMetadataNumeric.md5Checksum.ToString();
                }
                cacheArchivePath = Path.Combine(miHoYoPath, title);

                var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(cacheArchivePath) && x.IsReady).FirstOrDefault();
                if(gameInstallDrive == null)
                {
                    Dispatcher.Invoke(() => {MessageBox.Show(textStrings["msgbox_install_wrong_drive_type_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
                    return;
                }
                else if(gameInstallDrive.TotalFreeSpace < 2147483648)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if(MessageBox.Show(textStrings["msgbox_install_little_space_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                            return;
                    });
                }
                try
                {
                    var webRequest = (HttpWebRequest)WebRequest.Create(download_url);
                    webRequest.UserAgent = userAgent;
                    webRequest.Timeout = 30000;
                    var webResponse = (HttpWebResponse)webRequest.GetResponse();
                }
                catch(WebException ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to download cache from mirror:\n{ex}");
                    MessageBox.Show(string.Format(textStrings["msgbox_gamedownloadmirrorerror_msg"], ex.Message), textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    Status = LauncherStatus.Ready;
                    return;
                }

                Log($"Starting to download game cache {title}");
                Status = LauncherStatus.Downloading;
                await Task.Run(() =>
                {
                    tracker.NewFile();
                    var eta_calc = new ETACalculator(1, 1);
                    var download = new DownloadPauseable(download_url, cacheArchivePath);
                    download.Start();
                    while(!download.Done)
                    {
                        tracker.SetProgress(download.BytesWritten, download.ContentLength);
                        eta_calc.Update((float)download.BytesWritten / (float)download.ContentLength);
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = tracker.GetProgress() * 100;
                            ProgressText.Text = $"{string.Format(textStrings["progresstext_downloaded"], BpUtility.ToBytesCount(download.BytesWritten), BpUtility.ToBytesCount(download.ContentLength), tracker.GetBytesPerSecondString())}\n{string.Format(textStrings["progresstext_eta"], eta_calc.ETR.ToString("hh\\:mm\\:ss"))}";
                        });
                        Thread.Sleep(100);
                    }
                    Log("Game cache download OK");
                    while(BpUtility.IsFileLocked(new FileInfo(cacheArchivePath)))
                        Thread.Sleep(10);
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = String.Empty;
                        LaunchButton.Content = textStrings["button_launch"];
                    });
                });
                try
                {
                    if(abort)
                        return;
                    await Task.Run(() =>
                    {
                        Log("Validating game cache");
                        Status = LauncherStatus.Verifying;
                        string actual_md5 = BpUtility.CalculateMD5(cacheArchivePath);
                        if(actual_md5 != md5.ToUpper())
                        {
                            Status = LauncherStatus.Error;
                            Log($"ERROR: Validation failed. Supposed MD5: {md5}, actual MD5: {actual_md5}");
                            Dispatcher.Invoke(() =>
                            {
                                if(MessageBox.Show(textStrings["msgbox_verifyerror_2_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                                {
                                    if(File.Exists(cacheArchivePath))
                                        File.Delete(cacheArchivePath);
                                    abort = true;
                                    GameUpdateCheck(false);
                                }
                            });
                        }
                        else
                        {
                            Log("Validation OK");
                        }
                        if(abort)
                            return;
                        var skippedFiles = new List<string>();
                        using(var archive = ArchiveFactory.Open(cacheArchivePath))
                        {
                            int unpackedFiles = 0;
                            int fileCount = 0;

                            Log("Unpacking game cache...");
                            Status = LauncherStatus.Unpacking;
                            foreach(var entry in archive.Entries)
                            {
                                if(!entry.IsDirectory)
                                    fileCount++;
                            }
                            if(!Directory.Exists(miHoYoPath))
                            {
                                Directory.CreateDirectory(miHoYoPath);
                            }
                            var reader = archive.ExtractAllEntries();
                            while(reader.MoveToNextEntry())
                            {
                                try
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        ProgressText.Text = string.Format(textStrings["progresstext_unpacking_2"], unpackedFiles + 1, fileCount);
                                        ProgressBar.Value = (unpackedFiles + 1f) / fileCount * 100;
                                    });
                                    reader.WriteEntryToDirectory(miHoYoPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true, PreserveFileTime = true });
                                    if(!reader.Entry.IsDirectory)
                                        unpackedFiles++;
                                }
                                catch
                                {
                                    if(!reader.Entry.IsDirectory)
                                    {
                                        skippedFiles.Add(reader.Entry.ToString());
                                        fileCount--;
                                        Log($"Unpack ERROR: {reader.Entry}");
                                    }
                                }
                            }
                        }
                        if(skippedFiles.Count > 0)
                        {
                            MessageBox.Show(textStrings["msgbox_extractskip_msg"], textStrings["msgbox_extractskip_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        Log("Game cache unpack OK");
                        File.Delete(cacheArchivePath);
                    });
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install game cache:\n{ex}");
                    MessageBox.Show(string.Format(textStrings["msgbox_installerror_msg"], ex), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download game cache:\n{ex}");
                MessageBox.Show(string.Format(textStrings["msgbox_gamedownloaderror_msg"], ex), textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Dispatcher.Invoke(() => {LaunchButton.Content = textStrings["button_launch"];});
            Status = LauncherStatus.Ready;
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
            
            if(LauncherRegKey.GetValue("RanOnce") != null && (LauncherRegKey.GetValue("LauncherVersion") != null && LauncherRegKey.GetValue("LauncherVersion").ToString() != localLauncherVersion.ToString()))
            {
                ChangelogBox.Visibility = Visibility.Visible;
                ChangelogBoxMessageTextBlock.Visibility = Visibility.Visible;
                ChangelogBoxScrollViewer.Height = 305;
            }
            try
            {
                if(LauncherRegKey.GetValue("RanOnce") == null)
                    LauncherRegKey.SetValue("RanOnce", 1, RegistryValueKind.DWord);
                LauncherRegKey.SetValue("LauncherVersion", localLauncherVersion);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to write critical registry info:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_registryerror_msg"], ex), textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
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
            if(Status == LauncherStatus.Ready || Status == LauncherStatus.Error)
            {
                if(DownloadPaused)
                {
                    DownloadPaused = false;
                    await DownloadGameFile();
                    return;
                }

                if(localVersionInfo != null)
                {
                    if(!File.Exists(gameExePath))
                    {
                        MessageBox.Show(textStrings["msgbox_noexe_msg"], textStrings["msgbox_noexe_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    try
                    {
                        BpUtility.StartProcess(gameExePath, null, gameInstallPath, true);
                        Close();
                    }
                    catch(Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to start the game:\n{ex}");
                        MessageBox.Show(string.Format(textStrings["msgbox_process_start_error_msg"], ex), textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
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

                            if(dialog.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                if(Server == HI3Server.Global)
                                    gameInstallPath = Path.Combine(dialog.FileName, "Honkai Impact 3rd");
                                else
                                    gameInstallPath = Path.Combine(dialog.FileName, "Honkai Impact 3");
                            }
                            else
                            {
                                gameInstallPath = null;
                            }

                            if(String.IsNullOrEmpty(gameInstallPath))
                            {
                                return String.Empty;
                            }
                            else
                            {
                                var path = CheckForExistingGameDirectory(gameInstallPath);
                                if(!String.IsNullOrEmpty(path))
                                {
                                    if(MessageBox.Show(string.Format(textStrings["msgbox_installexisting_msg"], path), textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                                    {
                                        Log($"Existing install directory selected: {path}");
                                        gameInstallPath = path;
                                        var server = CheckForExistingGameClientServer();
                                        if(server >= 0)
                                        {
                                            if((int)Server == server)
                                                GameUpdateCheck(false);
                                            else
                                                ServerDropdown.SelectedIndex = server;
                                            WriteVersionInfo();
                                        }
                                        else
                                        {
                                            Status = LauncherStatus.Error;
                                            Log($"ERROR: Directory {gameInstallPath} doesn't contain a valid installation of the game. This launcher supports only Global and SEA clients!");
                                            if(MessageBox.Show(string.Format(textStrings["msgbox_installexistinginvalid_msg"]), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                                            {
                                                Status = LauncherStatus.Ready;
                                            }
                                        }
                                    }
                                    return String.Empty;
                                }
                                return gameInstallPath;
                            }
                        }
                        if(String.IsNullOrEmpty(SelectGameInstallDirectory()))
                            return;
                        while(MessageBox.Show(string.Format(textStrings["msgbox_install_msg"], gameInstallPath), textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                        {
                            if(String.IsNullOrEmpty(SelectGameInstallDirectory()))
                                return;
                        }
                        var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(gameInstallPath) && x.IsReady).FirstOrDefault();
                        if(gameInstallDrive == null || gameInstallDrive.DriveType == DriveType.CDRom)
                        {
                            MessageBox.Show(textStrings["msgbox_install_wrong_drive_type_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        else if(gameInstallDrive.TotalFreeSpace < 24696061952)
                        {
                            if(MessageBox.Show(textStrings["msgbox_install_little_space_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                                return;
                        }
                        if(!Directory.Exists(gameInstallPath))
                        {
                            Directory.CreateDirectory(gameInstallPath);
                        }
                        gameArchivePath = Path.Combine(gameInstallPath, gameArchiveName);
                        gameExePath = Path.Combine(gameInstallPath, "BH3.exe");
                        Log($"Install dir selected: {gameInstallPath}");
                        await DownloadGameFile();
                    }
                    catch(Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to select game install directory:\n{ex}");
                        MessageBox.Show(string.Format(textStrings["msgbox_installdirerror_msg"], ex), textStrings["msgbox_installdirerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }
            }
            else if(Status == LauncherStatus.UpdateAvailable)
            {
                var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(gameInstallPath) && x.IsReady).FirstOrDefault();
                if(gameInstallDrive.TotalFreeSpace < 24696061952)
                {
                    if(MessageBox.Show(textStrings["msgbox_install_little_space_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                if(!Directory.Exists(gameInstallPath))
                {
                    Directory.CreateDirectory(gameInstallPath);
                }
                await DownloadGameFile();
            }
            else if(Status == LauncherStatus.Downloading || Status == LauncherStatus.DownloadPaused)
            {
                if(!DownloadPaused)
                {
                    download.Pause();
                    Status = LauncherStatus.DownloadPaused;
                    DownloadPaused = true;
                    LaunchButton.Content = textStrings["button_resume"];
                }
                else
                {
                    Status = LauncherStatus.Downloading;
                    DownloadPaused = false;
                    LaunchButton.IsEnabled = true;
                    LaunchButton.Content = textStrings["button_pause"];
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
            if(Status != LauncherStatus.Ready)
                return;

            Status = LauncherStatus.CheckingUpdates;
            Dispatcher.Invoke(() => {ProgressText.Text = textStrings["progresstext_mirror_connect"];});
            Log("Fetching mirror metadata...");
            
            try
            {
                await Task.Run(async () =>
                {
                    await FetchOnlineVersionInfoAsync();
                    if(Server == HI3Server.Global)
                    {
                        if(Mirror == HI3Mirror.MediaFire)
                        {
                            gameCacheMetadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache.global.id.ToString(), false);
                            gameCacheMetadataNumeric = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.id.ToString(), true);
                        }
                        else
                        {
                            gameCacheMetadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache.global.ToString());
                            gameCacheMetadataNumeric = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache_numeric.global.ToString());
                        }
                    }
                    else
                    {
                        if(Mirror == HI3Mirror.MediaFire)
                        {
                            gameCacheMetadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache.os.id.ToString(), false);
                            gameCacheMetadataNumeric = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.id.ToString(), true);
                        }
                        else
                        {
                            gameCacheMetadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache.os.ToString());
                            gameCacheMetadataNumeric = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache_numeric.os.ToString());
                        }
                    }
                });
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to fetch cache file metadata:\n{ex}");
                Dispatcher.Invoke(() => {MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], ex), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
                Status = LauncherStatus.Ready;
                return;
            }
            if(gameCacheMetadata == null || gameCacheMetadataNumeric == null)
            {
                Status = LauncherStatus.Ready;
                return;
            }
            Dispatcher.Invoke(() =>
            {
                DownloadCacheBox.Visibility = Visibility.Visible;
                if(Mirror == HI3Mirror.MediaFire)
                    DownloadCacheBoxMessageTextBlock.Text = string.Format(textStrings["downloadcachebox_msg"], "MediaFire", textStrings["shrug"], onlineVersionInfo.game_info.mirror.maintainer.ToString());
                else
                {
                    string time;
                    if(DateTime.Compare(FetchmiHoYoResourceVersionDateModified(), DateTime.Parse(gameCacheMetadataNumeric.modifiedDate.ToString())) >= 0)
                        time = $"{DateTime.Parse(gameCacheMetadataNumeric.modifiedDate.ToString()).ToLocalTime().ToString()} ({textStrings["outdated"].ToLower()})";
                    else
                        time = DateTime.Parse(gameCacheMetadataNumeric.modifiedDate.ToString()).ToLocalTime().ToString();
                    DownloadCacheBoxMessageTextBlock.Text = string.Format(textStrings["downloadcachebox_msg"], "Google Drive", time, onlineVersionInfo.game_info.mirror.maintainer.ToString());
                }
                Status = LauncherStatus.Ready;
            });
        }

        private async Task CM_Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if((Status == LauncherStatus.Ready || Status == LauncherStatus.Error) && !String.IsNullOrEmpty(gameInstallPath))
            {
                if(rootPath.Contains(gameInstallPath))
                {
                    MessageBox.Show(textStrings["msgbox_uninstall_4_msg"], textStrings["msgbox_uninstall_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if(MessageBox.Show(textStrings["msgbox_uninstall_1_msg"], textStrings["msgbox_uninstall_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                    return;
                if(MessageBox.Show(textStrings["msgbox_uninstall_2_msg"], textStrings["msgbox_uninstall_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    return;

                Status = LauncherStatus.Uninstalling;
                Log($"Deleting game files...");
                await Task.Run(() =>
                {
                    try
                    {
                        DeleteGameFiles(true);
                        if(MessageBox.Show(textStrings["msgbox_uninstall_3_msg"], textStrings["msgbox_uninstall_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            string path;
                            if(Server == HI3Server.Global)
                                path = Path.Combine(miHoYoPath, "Honkai Impact 3rd");
                            else
                                path = Path.Combine(miHoYoPath, "Honkai Impact 3");
                            Log("Deleting game cache and registry settings...");
                            if(Directory.Exists(path))
                                Directory.Delete(path, true);
                            if(Registry.CurrentUser.OpenSubKey(GameRegistryPath) != null)
                                Registry.CurrentUser.DeleteSubKeyTree(GameRegistryPath, true);
                        }
                        Log("Game uninstall OK");
                        GameUpdateCheck(false);
                    }
                    catch(Exception ex)
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
            if(Status != LauncherStatus.Ready)
                return;

            if(MessageBox.Show(textStrings["msgbox_download_type_1_msg"], textStrings["contextmenu_download_type"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;

            try
            {
                RegistryKey key;
                key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                string value = "GENERAL_DATA_V2_ResourceDownloadType_h2238376574";
                if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.DWord)
                {
                    if(key != null)
                        key.DeleteValue(value);
                    if(MessageBox.Show(textStrings["msgbox_registryempty_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        return;
                    }
                }
                var valueBefore = key.GetValue(value);
                int valueAfter;
                if((int)valueBefore == 3)
                    valueAfter = 2;
                else if((int)valueBefore == 2)
                    valueAfter = 1;
                else
                    valueAfter = 3;
                key.SetValue(value, valueAfter, RegistryValueKind.DWord);
                key.Close();
                Log($"Changed ResourceDownloadType from {valueBefore} to {valueAfter}");
                MessageBox.Show(string.Format(textStrings["msgbox_download_type_2_msg"], valueBefore, valueAfter), textStrings["contextmenu_download_type"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_registryerror_msg"], ex), textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private async Task CM_FixSubtitles_Click(object sender, RoutedEventArgs e)
        {
            if(Status != LauncherStatus.Ready)
                return;

            if(MessageBox.Show(textStrings["msgbox_fixsubs_1_msg"], textStrings["contextmenu_fixsubs"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;

            try
            {
                Status = LauncherStatus.Working;
                var GameVideoDirectory = Path.Combine(gameInstallPath, @"BH3_Data\StreamingAssets\Video");
                if(Directory.Exists(GameVideoDirectory))
                {
                    var SubtitleArchives = Directory.EnumerateFiles(GameVideoDirectory, "*.zip", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith(".zip", StringComparison.CurrentCultureIgnoreCase)).ToList();
                    Dispatcher.Invoke(() => {ProgressBar.IsIndeterminate = false;});
                    if(SubtitleArchives.Count > 0)
                    {
                        int filesUnpacked = 0;
                        await Task.Run(() =>
                        {
                            var skippedFiles = new List<string>();
                            var skippedFilePaths = new List<string>();
                            foreach(var SubtitleArchive in SubtitleArchives)
                            {
                                Log($"Unpacking archive {Path.GetFileName(SubtitleArchive)}");
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressText.Text = string.Format(textStrings["msgbox_fixsubs_2_msg"], filesUnpacked + 1, SubtitleArchives.Count);
                                    ProgressBar.Value = (filesUnpacked + 1f) / SubtitleArchives.Count * 100;
                                });
                                using(var archive = ArchiveFactory.Open(SubtitleArchive))
                                {
                                    var reader = archive.ExtractAllEntries();
                                    while(reader.MoveToNextEntry())
                                    {
                                        try
                                        {
                                            var entryPath = Path.Combine(GameVideoDirectory, reader.Entry.ToString());
                                            if(File.Exists(entryPath))
                                                File.SetAttributes(entryPath, File.GetAttributes(entryPath) & ~FileAttributes.ReadOnly);
                                            reader.WriteEntryToDirectory(GameVideoDirectory, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
                                            Log($"Unpack OK: {SubtitleArchive}");
                                        }
                                        catch
                                        {
                                            skippedFiles.Add($"[{Path.GetFileName(SubtitleArchive)}] {reader.Entry}");
                                            skippedFilePaths.Add(SubtitleArchive);
                                            Log($"Unpack ERROR: {SubtitleArchive}");
                                        }
                                    }
                                }
                                File.SetAttributes(SubtitleArchive, File.GetAttributes(SubtitleArchive) & ~FileAttributes.ReadOnly);
                                if(!skippedFilePaths.Contains(SubtitleArchive))
                                    File.Delete(SubtitleArchive);
                                filesUnpacked++;
                            }
                            Dispatcher.Invoke(() =>
                            { 
                                if(skippedFiles.Count > 0)
                                    MessageBox.Show(textStrings["msgbox_extractskip_msg"], textStrings["msgbox_extractskip_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                            });
                            Log($"Unpacked {filesUnpacked} archives");
                        });
                    }
                    Dispatcher.Invoke(() => {ProgressBar.IsIndeterminate = true;});
                    var SubtitleFiles = Directory.EnumerateFiles(GameVideoDirectory, "*.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith(".srt", StringComparison.CurrentCultureIgnoreCase)).ToList();
                    var subsFixed = new List<string>();
                    Dispatcher.Invoke(() => {ProgressBar.IsIndeterminate = false;});
                    if(SubtitleFiles.Count > 0)
                    {
                        int subtitlesParsed = 0;
                        await Task.Run(() =>
                        {
                            for(int i = SubtitleFiles.Count - 1; i >= 0; i--)
                            {
                                //Log($"Reading subtitle {Path.GetFileName(SubtitleFiles[i])}");
                                var fileLines = File.ReadAllLines(SubtitleFiles[i]);
                                var lineCount = fileLines.Length;
                                int linesReplaced = 0;
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressText.Text = string.Format(textStrings["msgbox_fixsubs_3_msg"], subtitlesParsed + 1, SubtitleFiles.Count);
                                    ProgressBar.Value = (subtitlesParsed + 1f) / SubtitleFiles.Count * 100;
                                });
                                File.SetAttributes(SubtitleFiles[i], File.GetAttributes(SubtitleFiles[i]) & ~FileAttributes.ReadOnly);
                                if(new FileInfo(SubtitleFiles[i]).Length == 0)
                                {
                                    // commented out, zero length subs are needed for CG playback (such as gacha video) for some reason
                                    //Log($"Deleting zero length file {Path.GetFileName(SubtitleFiles[i])}");
                                    //File.Delete(SubtitleFiles[i]);
                                    //SubtitleFiles.Remove(SubtitleFiles[i]);
                                    subtitlesParsed++;
                                    continue;
                                }
                                for(int line = 1; line < lineCount; line++)
                                {
                                    var timecodeLine = File.ReadLines(SubtitleFiles[i]).Skip(line).Take(1).First();
                                    if(String.IsNullOrEmpty(timecodeLine) || timecodeLine[0] != '0')
                                        continue;
                                    if(timecodeLine.Contains("."))
                                    {
                                        //Log($"Fixed line {1 + line}: {timecodeLine}");
                                        fileLines[line] = timecodeLine.Replace(".", ",");
                                        linesReplaced++;
                                        if(!subsFixed.Contains(SubtitleFiles[i]))
                                        {
                                            subsFixed.Add(SubtitleFiles[i]);
                                            Log($"Subtitle fixed: {SubtitleFiles[i]}\n");
                                        }
                                    }
                                }
                                if(linesReplaced > 0)
                                    File.WriteAllLines(SubtitleFiles[i], fileLines);
                                subtitlesParsed++;
                            }
                        });
                        Log($"Parsed {subtitlesParsed} subtitles, fixed {subsFixed.Count} of them");
                    }

                    Dispatcher.Invoke(() =>
                    {
                        if(SubtitleArchives.Count > 0 && subsFixed.Count == 0)
                            MessageBox.Show(string.Format(textStrings["msgbox_fixsubs_4_msg"], SubtitleArchives.Count), textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                        else if(SubtitleArchives.Count == 0 && subsFixed.Count > 0)
                            MessageBox.Show(string.Format(textStrings["msgbox_fixsubs_5_msg"], subsFixed.Count), textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                        else if(SubtitleArchives.Count > 0 && subsFixed.Count > 0)
                            MessageBox.Show($"{string.Format(textStrings["msgbox_fixsubs_4_msg"], SubtitleArchives.Count)}\n{string.Format(textStrings["msgbox_fixsubs_5_msg"], subsFixed.Count)}", textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                        else
                            MessageBox.Show(string.Format(textStrings["msgbox_fixsubs_6_msg"]), textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                else
                {
                    Status = LauncherStatus.Error;
                    Log("ERROR: No CG directory!");
                    MessageBox.Show(string.Format(textStrings["msgbox_novideodir_msg"]), textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Status = LauncherStatus.Ready;
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_genericerror_msg"], ex), textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void CM_CustomFPS_Click(object sender, RoutedEventArgs e)
        {
            if(Status != LauncherStatus.Ready)
                return;

            try
            {
                RegistryKey key;
                key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                string value = "GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411";
                if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.Binary)
                {
                    if(key != null)
                        key.DeleteValue(value);
                    if(MessageBox.Show(textStrings["msgbox_registryempty_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        return;
                }
                var valueBefore = key.GetValue(value);
                var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])valueBefore));
                if(json == null)
                {
                    if(MessageBox.Show(textStrings["msgbox_registryempty_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        return;
                }
                FPSInputBox.Visibility = Visibility.Visible;
                FPSInputBoxTitleTextBlock.Text = textStrings["fpsinputbox_title"];
                if(json.TargetFrameRateForInLevel != null)
                    FPSInputBoxTextBox.Text = json.TargetFrameRateForInLevel;
                else
                    FPSInputBoxTextBox.Text = "60";
                gameGraphicSettings = json;
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_registryerror_msg"], ex), textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void CM_ResetGameSettings_Click(object sender, RoutedEventArgs e)
        {
            if(Status != LauncherStatus.Ready)
                return;

            if(MessageBox.Show(textStrings["msgbox_resetgamesettings_1_msg"], textStrings["contextmenu_resetgamesettings"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            if(MessageBox.Show(textStrings["msgbox_resetgamesettings_2_msg"], textStrings["contextmenu_resetgamesettings"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return;

            try
            {
                RegistryKey key;
                key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                if(key == null)
                {
                    Log("ERROR: No game registry key!");
                    if(MessageBox.Show(textStrings["msgbox_registryempty_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        return;
                    }
                }
                Registry.CurrentUser.DeleteSubKeyTree(GameRegistryPath, true);
                Log("Game settings reset OK");
                MessageBox.Show(textStrings["msgbox_resetgamesettings_3_msg"], textStrings["contextmenu_resetgamesettings"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_registryerror_msg"], ex), textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
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
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to execute process:\n{ex}");
                MessageBox.Show(string.Format(textStrings["msgbox_process_start_error_msg"], ex), textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                Status = LauncherStatus.Ready;
            }
        }

        private void CM_Feedback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new/choose", null, rootPath, true);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to execute process:\n{ex}");
                MessageBox.Show(string.Format(textStrings["msgbox_process_start_error_msg"], ex), textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
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
            if((int)Server == index)
                return;
            if(DownloadPaused)
            {
                if(MessageBox.Show(textStrings["msgbox_gamedownloadpaused_msg"], textStrings["msgbox_notice_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    ServerDropdown.SelectedIndex = (int)Server;
                    return;
                }
                if(File.Exists(gameArchivePath))
                    File.Delete(gameArchivePath);
                download = null;
                DownloadPaused = false;
                DeleteGameFiles(false);
            }
            switch(index)
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
            catch(Exception ex)
            {
                Log($"ERROR: Failed to write value with key LastSelectedServer to registry:\n{ex}");
            }
            Log($"Switched server to {((ComboBoxItem)ServerDropdown.SelectedItem).Content as string}");
            GameUpdateCheck(true);
        }

        private void MirrorDropdown_Changed(object sender, SelectionChangedEventArgs e)
        {
            var index = MirrorDropdown.SelectedIndex;
            if((int)Mirror == index)
                return;
            if(DownloadPaused)
            {
                if(MessageBox.Show(textStrings["msgbox_gamedownloadpaused_msg"], textStrings["msgbox_notice_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    MirrorDropdown.SelectedIndex = (int)Mirror;
                    return;
                }
                if(File.Exists(gameArchivePath))
                    File.Delete(gameArchivePath);
                download = null;
                DownloadPaused = false;
                DeleteGameFiles(false);
            }
            if(Mirror == HI3Mirror.miHoYo && index != 0)
            {
                if(MessageBox.Show(textStrings["msgbox_mirrorinfo_msg"], textStrings["msgbox_notice_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                {
                    MirrorDropdown.SelectedIndex = 0;
                    return;
                }
            }
            switch(index)
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
            catch(Exception ex)
            {
                Log($"ERROR: Failed to write value with key LastSelectedMirror to registry:\n{ex}");
            }
            GameUpdateCheck(false);
            Log($"Selected mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");
        }
        
        private void FPSInputBoxTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.Any(x => Char.IsDigit(x));
        }

        // https://stackoverflow.com/q/1268552/7570821
        private void FPSInputBoxTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            Boolean IsTextAllowed(String text)
            {
                return Array.TrueForAll<Char>(text.ToCharArray(),
                    delegate (Char c){return Char.IsDigit(c) || Char.IsControl(c);});
            }

            if(e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if(!IsTextAllowed(text))
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
            if(MessageBox.Show($"{textStrings["msgbox_download_cache_1_msg"]}\n{string.Format(textStrings["msgbox_download_cache_3_msg"], BpUtility.ToBytesCount((long)gameCacheMetadata.fileSize))}", textStrings["contextmenu_downloadcache"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            DownloadCacheBox.Visibility = Visibility.Collapsed;
            DownloadGameCache(true);
        }

        private void DownloadCacheBoxNumericFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show($"{textStrings["msgbox_download_cache_2_msg"]}\n{string.Format(textStrings["msgbox_download_cache_3_msg"], BpUtility.ToBytesCount((long)gameCacheMetadataNumeric.fileSize))}", textStrings["contextmenu_downloadcache"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
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
                FPSInputBoxTextBox.Text = String.Concat(FPSInputBoxTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
                if(String.IsNullOrEmpty(FPSInputBoxTextBox.Text))
                {
                    MessageBox.Show(textStrings["msgbox_customfps_1_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                int fps = Int32.Parse(FPSInputBoxTextBox.Text);
                if(fps < 1)
                {
                    MessageBox.Show(textStrings["msgbox_customfps_2_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else if(fps < 30)
                {
                    if(MessageBox.Show(textStrings["msgbox_customfps_3_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                gameGraphicSettings.IsUserDefinedGrade = false;
                gameGraphicSettings.IsUserDefinedVolatile = true;
                gameGraphicSettings.TargetFrameRateForInLevel = fps;
                gameGraphicSettings.TargetFrameRateForOthers = fps;
                var valueAfter = Encoding.UTF8.GetBytes($"{JsonConvert.SerializeObject(gameGraphicSettings)}\0");
                RegistryKey key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                key.SetValue("GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411", valueAfter, RegistryValueKind.Binary);
                key.Close();
                FPSInputBox.Visibility = Visibility.Collapsed;
                Log($"Set custom FPS to {fps} OK");
                MessageBox.Show(string.Format(textStrings["msgbox_customfps_4_msg"], fps), textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_genericerror_msg"], ex), textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
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
            catch(Exception ex)
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
            catch(Exception ex)
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
            if(Status == LauncherStatus.Downloading || Status == LauncherStatus.DownloadPaused)
            {
                try
                {
                    if(download == null)
                    {
                        if(MessageBox.Show($"{textStrings["msgbox_abort_1_msg"]}\n{textStrings["msgbox_abort_2_msg"]}", textStrings["msgbox_abort_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            Status = LauncherStatus.CleaningUp;
                            if(File.Exists(gameArchivePath))
                                File.Delete(gameArchivePath);
                            if(File.Exists(cacheArchivePath))
                                File.Delete(cacheArchivePath);
                        }
                        else
                        {
                            e.Cancel = true;
                        }
                    }
                    else
                    {
                        if(MessageBox.Show($"{textStrings["msgbox_abort_1_msg"]}\n{textStrings["msgbox_abort_3_msg"]}", textStrings["msgbox_abort_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            download.Pause();
                            WriteVersionInfo();
                        }
                        else
                        {
                            e.Cancel = true;
                        }
                    }
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install the game:\n{ex}");
                    if(MessageBox.Show(string.Format(textStrings["msgbox_installerror_msg"], ex), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }
            }
            else if(Status == LauncherStatus.Verifying || Status == LauncherStatus.Unpacking || Status == LauncherStatus.CleaningUp || Status == LauncherStatus.Uninstalling || Status == LauncherStatus.Working)
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
                path.Replace(@"\BH3_Data\Honkai Impact 3rd", String.Empty),
                path.Replace(@"\BH3_Data\Honkai Impact 3", String.Empty),
                Path.Combine(path, "Games"),
                Path.Combine(path, "Honkai Impact 3rd"),
                Path.Combine(path, "Honkai Impact 3"),
                Path.Combine(path, "Honkai Impact 3rd", "Games"),
                Path.Combine(path, "Honkai Impact 3", "Games")
            });
            if(path.Length >= 16)
            {
                pathVariants.Add(path.Substring(0, path.Length - 16));
            }
            if(path.Length >= 18)
            {
                pathVariants.Add(path.Substring(0, path.Length - 18));
            }

            if(File.Exists(Path.Combine(path, gameExeName)))
            {
                return path;
            }
            else
            {
                foreach(var variant in pathVariants)
                {
                    if(File.Exists(Path.Combine(variant, gameExeName)))
                        return variant;
                }
                return String.Empty;
            }
        }

        private int CheckForExistingGameClientServer()
        {
            var path = Path.Combine(gameInstallPath, @"BH3_Data\app.info");
            if(File.Exists(path))
            {
                var gameTitleLine = File.ReadLines(path).Skip(1).Take(1).First();
                if(!String.IsNullOrEmpty(gameTitleLine))
                {
                    if(gameTitleLine.Contains("Honkai Impact 3rd"))
                        return 0;
                    else if(gameTitleLine.Contains("Honkai Impact 3"))
                        return 1;
                }
            }
            return -1;
        }

        private void ToggleContextMenuItems(bool val)
        {
            foreach(dynamic item in OptionsContextMenu.Items)
            {
                if(item.GetType() == typeof(MenuItem) && (item.Header.ToString() == textStrings["contextmenu_changelog"] || item.Header.ToString() == textStrings["contextmenu_about"]))
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
                if(_versionStrings.Length != 4)
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
                if(major != _otherVersion.major)
                {
                    return true;
                }
                else if(minor != _otherVersion.minor)
                {
                    return true;
                }
                else if(date != _otherVersion.date)
                {
                    return true;
                }
                else if(hotfix != _otherVersion.hotfix)
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
            internal static GameVersion zero = new GameVersion(0, 0, 0, String.Empty);

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
                if(_versionStrings.Length != 4)
                {
                    major = 0;
                    minor = 0;
                    patch = 0;
                    build = String.Empty;
                    return;
                }

                major = int.Parse(_versionStrings[0]);
                minor = int.Parse(_versionStrings[1]);
                patch = int.Parse(_versionStrings[2]);
                build = _versionStrings[3];
            }

            internal bool IsDifferentThan(GameVersion _otherVersion)
            {
                if(major != _otherVersion.major)
                {
                    return true;
                }
                else if(minor != _otherVersion.minor)
                {
                    return true;
                }
                else if(patch != _otherVersion.patch)
                {
                    return true;
                }
                else if(build != _otherVersion.build)
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