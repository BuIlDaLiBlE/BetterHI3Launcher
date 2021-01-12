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
using System.Security.Cryptography;
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
        Ready, Error, CheckingUpdates, Downloading, Updating, Verifying, Unpacking, CleaningUp, UpdateAvailable, Uninstalling, Working
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
        static readonly Version localLauncherVersion = new Version("1.0.20210110.0");
        static readonly string rootPath = Directory.GetCurrentDirectory();
        static readonly string localLowPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}Low";
        static readonly string backgroundImagePath = Path.Combine(localLowPath, @"Bp\Better HI3 Launcher");
        static readonly string miHoYoPath = Path.Combine(localLowPath, "miHoYo");
        static readonly string gameExeName = "BH3.exe";
        static readonly string OSLanguage = CultureInfo.CurrentUICulture.ToString();
        static readonly string userAgent = $"BetterHI3Launcher v{localLauncherVersion}";
        static string gameInstallPath, gameArchivePath, gameArchiveName, gameExePath, launcherExeName, launcherPath, launcherArchivePath;
        static string ChangelogLanguage = "en";
        static string GameRegistryPath;
        static string RegistryVersionInfo;
        static bool DownloadPaused = false;
        dynamic localVersionInfo, onlineVersionInfo, miHoYoVersionInfo, gameGraphicSettings, gameCacheMetadata, gameCacheMetadataNumeric;
        LauncherStatus _status;
        HI3Server _gameserver;
        HI3Mirror _downloadmirror;
        static Dictionary<string, string> textStrings = new Dictionary<string, string>();
        RegistryKey versionInfoKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Bp\Better HI3 Launcher");
        TimedWebClient webClient = new TimedWebClient{Encoding = Encoding.UTF8, Timeout = 10000};
        DownloadPauseable download;
        DownloadProgressTracker tracker = new DownloadProgressTracker(50, TimeSpan.FromMilliseconds(500));

        #if DEBUG
            // https://stackoverflow.com/a/48864902/7570821
            static class WinConsole
            {
                static public void Initialize(bool alwaysCreateNewConsole = true)
                {
                    bool consoleAttached = true;
                    if (alwaysCreateNewConsole
                        || (AttachConsole(ATTACH_PARRENT) == 0
                        && System.Runtime.InteropServices.Marshal.GetLastWin32Error() != ERROR_ACCESS_DENIED))
                    {
                        consoleAttached = AllocConsole() != 0;
                    }

                    if (consoleAttached)
                    {
                        InitializeOutStream();
                        InitializeInStream();
                    }
                }

                private static void InitializeOutStream()
                {
                    var fs = CreateFileStream("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, FileAccess.Write);
                    if (fs != null)
                    {
                        var writer = new StreamWriter(fs) { AutoFlush = true };
                        Console.SetOut(writer);
                        Console.SetError(writer);
                    }
                }

                private static void InitializeInStream()
                {
                    var fs = CreateFileStream("CONIN$", GENERIC_READ, FILE_SHARE_READ, FileAccess.Read);
                    if (fs != null)
                    {
                        Console.SetIn(new StreamReader(fs));
                    }
                }

                private static FileStream CreateFileStream(string name, uint win32DesiredAccess, uint win32ShareMode, FileAccess dotNetFileAccess)
                {
                    var file = new Microsoft.Win32.SafeHandles.SafeFileHandle(CreateFileW(name, win32DesiredAccess, win32ShareMode, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero), true);
                    if (!file.IsInvalid)
                    {
                        var fs = new FileStream(file, dotNetFileAccess);
                        return fs;
                    }
                    return null;
                }

                #region Win API Functions and Constants
                [System.Runtime.InteropServices.DllImport("kernel32.dll",
                    EntryPoint = "AllocConsole",
                    SetLastError = true,
                    CharSet = System.Runtime.InteropServices.CharSet.Auto,
                    CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
                private static extern int AllocConsole();

                [System.Runtime.InteropServices.DllImport("kernel32.dll",
                    EntryPoint = "AttachConsole",
                    SetLastError = true,
                    CharSet = System.Runtime.InteropServices.CharSet.Auto,
                    CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
                private static extern UInt32 AttachConsole(UInt32 dwProcessId);

                [System.Runtime.InteropServices.DllImport("kernel32.dll",
                    EntryPoint = "CreateFileW",
                    SetLastError = true,
                    CharSet = System.Runtime.InteropServices.CharSet.Auto,
                    CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
                private static extern IntPtr CreateFileW
                (
                      string lpFileName,
                      UInt32 dwDesiredAccess,
                      UInt32 dwShareMode,
                      IntPtr lpSecurityAttributes,
                      UInt32 dwCreationDisposition,
                      UInt32 dwFlagsAndAttributes,
                      IntPtr hTemplateFile
                );

                private const UInt32 GENERIC_WRITE = 0x40000000;
                private const UInt32 GENERIC_READ = 0x80000000;
                private const UInt32 FILE_SHARE_READ = 0x00000001;
                private const UInt32 FILE_SHARE_WRITE = 0x00000002;
                private const UInt32 OPEN_EXISTING = 0x00000003;
                private const UInt32 FILE_ATTRIBUTE_NORMAL = 0x80;
                private const UInt32 ERROR_ACCESS_DENIED = 5;

                private const UInt32 ATTACH_PARRENT = 0xFFFFFFFF;

                #endregion
            }
        #endif

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
                    switch(_status)
                    {
                        case LauncherStatus.Ready:
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
                        case LauncherStatus.Updating:
                        case LauncherStatus.Working:
                            ToggleUI(false);
                            ToggleProgressBar(Visibility.Visible);
                            break;
                        case LauncherStatus.Verifying:
                            ProgressText.Text = textStrings["progresstext_verifying"];
                            ToggleProgressBar(Visibility.Visible);
                            break;
                        case LauncherStatus.Unpacking:
                            ProgressText.Text = textStrings["progresstext_unpacking_1"];
                            ToggleProgressBar(Visibility.Visible);
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
                        GameRegistryPath = @"SOFTWARE\miHoYo\Honkai Impact 3rd";
                        RegistryVersionInfo = "VersionInfoGlobal";
                        break;
                    case HI3Server.SEA:
                        GameRegistryPath = @"SOFTWARE\miHoYo\Honkai Impact 3";
                        RegistryVersionInfo = "VersionInfoSEA";
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
            #if DEBUG
                WinConsole.Initialize();
            #endif
            InitializeComponent();
            Log($"BetterHI3Launcher v{localLauncherVersion}");
            Log($"Launcher exe name: {Process.GetCurrentProcess().MainModule.ModuleName}");
            Log($"Launcher exe MD5: {CalculateMD5(Path.Combine(rootPath, Process.GetCurrentProcess().MainModule.ModuleName))}");
            Log($"Working directory: {rootPath}");
            Log($"OS language: {OSLanguage}");
            TextStrings_English();
            switch(OSLanguage)
            {
                case "ru-RU":
                case "uk-UA":
                case "be-BY":
                    ChangelogLanguage = "ru";
                    Resources["Font"] = new FontFamily("Arial Bold");
                    TextStrings_Russian();
                    break;
            }
            LaunchButton.Content = textStrings["button_download"];
            OptionsButton.Content = textStrings["button_options"];
            ServerLabel.Text = $"{textStrings["label_server"]}:";
            MirrorLabel.Text = $"{textStrings["label_mirror"]}:";
            InputBoxOKButton.Content = textStrings["button_confirm"];
            InputBoxCancelButton.Content = textStrings["button_cancel"];
            ChangelogBoxTitleTextBlock.Text = textStrings["changelogbox_title"];
            ChangelogBoxMessageTextBlock.Text = textStrings["changelogbox_msg"];
            DownloadCacheBoxTitleTextBlock.Text = textStrings["contextmenu_downloadcache"];
            DownloadCacheBoxFullCacheButton.Content = textStrings["downloadcachebox_button_full_cache"];
            DownloadCacheBoxNumericFilesButton.Content = textStrings["downloadcachebox_button_numeric_files"];
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

            #if !DEBUG
            try
            {
                bool needsUpdate = LauncherUpdateCheck() ? true : false;
                ProcessStartInfo startInfo = new ProcessStartInfo(launcherExeName);
                startInfo.WorkingDirectory = rootPath;
                startInfo.UseShellExecute = true;

                if(Process.GetCurrentProcess().MainModule.ModuleName != launcherExeName)
                {
                    Status = LauncherStatus.Error;
                    File.Move(Path.Combine(rootPath, Process.GetCurrentProcess().MainModule.ModuleName), launcherPath);
                    Process.Start(startInfo);
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
                    DownloadLauncherUpdate();
                    if(CalculateMD5(launcherArchivePath) != onlineVersionInfo.launcher_info.md5.ToString().ToUpper())
                    {
                        Status = LauncherStatus.Error;
                        if(File.Exists(launcherArchivePath))
                        {
                            File.Delete(launcherArchivePath);
                        }
                        if(MessageBox.Show(textStrings["msgbox_verifyerror_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
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

                    Process.Start(startInfo);
                    Application.Current.Shutdown();
                    return;
                }
                else
                {
                    if(File.Exists(launcherArchivePath))
                    {
                        File.Delete(launcherArchivePath);
                    }
                    if(!File.Exists(launcherPath))
                    {
                        File.Copy(Path.Combine(rootPath, Process.GetCurrentProcess().MainModule.ModuleName), launcherPath, true);
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
            #endif

            OptionsContextMenu.Items.Clear();
            var CMDownloadCache = new MenuItem{Header = textStrings["contextmenu_downloadcache"]};
            CMDownloadCache.Click += async (sender, e) => await CM_DownloadCache_Click(sender, e);
            OptionsContextMenu.Items.Add(CMDownloadCache);
            var CMUninstall = new MenuItem{Header = textStrings["contextmenu_uninstall"]};
            CMUninstall.Click += (sender, e) => CM_Uninstall_Click(sender, e);
            OptionsContextMenu.Items.Add(CMUninstall);
            OptionsContextMenu.Items.Add(new Separator());
            var CMFixUpdateLoop = new MenuItem{Header = textStrings["contextmenu_fixupdateloop"]};
            CMFixUpdateLoop.Click += (sender, e) => CM_FixUpdateLoop_Click(sender, e);
            OptionsContextMenu.Items.Add(CMFixUpdateLoop);
            var CMFixSubtitles = new MenuItem{Header = textStrings["contextmenu_fixsubs"]};
            CMFixSubtitles.Click += async (sender, e) => await CM_FixSubtitles_Click(sender, e);
            OptionsContextMenu.Items.Add(CMFixSubtitles);
            var CMCustomFPS = new MenuItem{Header = textStrings["contextmenu_customfps"]};
            CMCustomFPS.Click += (sender, e) => CM_CustomFPS_Click(sender, e);
            OptionsContextMenu.Items.Add(CMCustomFPS);
            var CMResetGameSettings = new MenuItem{Header = textStrings["contextmenu_resetgamesettings"]};
            CMResetGameSettings.Click += (sender, e) => CM_ResetGameSettings_Click(sender, e);
            OptionsContextMenu.Items.Add(CMResetGameSettings);
            OptionsContextMenu.Items.Add(new Separator());
            var CMChangelog = new MenuItem{Header = textStrings["contextmenu_changelog"]};
            CMChangelog.Click += (sender, e) => CM_Changelog_Click(sender, e);
            OptionsContextMenu.Items.Add(CMChangelog);
            var CMAbout = new MenuItem{Header = textStrings["contextmenu_about"]};
            CMAbout.Click += (sender, e) => CM_About_Click(sender, e);
            OptionsContextMenu.Items.Add(CMAbout);
            ToggleContextMenuItems(false);
            try
            {
                var LastSelectedServerKey = versionInfoKey.GetValue("LastSelectedServer");
                if(LastSelectedServerKey != null)
                {
                    if(versionInfoKey.GetValueKind("LastSelectedServer") == RegistryValueKind.DWord)
                    {
                        if((int)LastSelectedServerKey == 0)
                            Server = HI3Server.Global;
                        else if((int)LastSelectedServerKey == 1)
                            Server = HI3Server.SEA;
                    }
                }
                else
                {
                    Server = HI3Server.Global;
                }
                ServerDropdown.SelectedIndex = (int)Server;
                var LastSelectedMirrorKey = versionInfoKey.GetValue("LastSelectedMirror");
                if(LastSelectedMirrorKey != null)
                {
                    if(versionInfoKey.GetValueKind("LastSelectedMirror") == RegistryValueKind.DWord)
                    {
                        if((int)LastSelectedMirrorKey == 0)
                            Mirror = HI3Mirror.miHoYo;
                        else if((int)LastSelectedMirrorKey == 1)
                            Mirror = HI3Mirror.MediaFire;
                        else if((int)LastSelectedMirrorKey == 2)
                            Mirror = HI3Mirror.GoogleDrive;
                    }
                }
                else
                {
                    Mirror = HI3Mirror.miHoYo;
                }
                MirrorDropdown.SelectedIndex = (int)Mirror;
                var ShowLogKey = versionInfoKey.GetValue("ShowLog");
                if(ShowLogKey != null)
                {
                    if(versionInfoKey.GetValueKind("ShowLog") == RegistryValueKind.DWord)
                    {
                        if((int)ShowLogKey == 1)
                            ShowLogCheckBox.IsChecked = true;
                    }
                }
                Log($"Using server: {((ComboBoxItem)ServerDropdown.SelectedItem).Content as string}");
                Log($"Using mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");

                string backgroundImageName = miHoYoVersionInfo.bg_file_name.ToString();
                var BackgroundImageNameKey = versionInfoKey.GetValue("BackgroundImageName");
                if(BackgroundImageNameKey != null && File.Exists(Path.Combine(backgroundImagePath, backgroundImageName)) && backgroundImageName == BackgroundImageNameKey.ToString())
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
                        versionInfoKey.SetValue("BackgroundImageName", backgroundImageName);
                        versionInfoKey.Close();
                        versionInfoKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Bp\Better HI3 Launcher", true);
                        Log($"Downloaded background image: {backgroundImageName}");
                    }
                    catch(Exception ex)
                    {
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
                launcherArchivePath = Path.Combine(rootPath, "BetterHI3Launcher.7z");
                Dispatcher.Invoke(() =>
                {
                    LauncherVersionText.Text = $"{textStrings["launcher_version"]}: v{localLauncherVersion}";
                    ChangelogBoxTextBox.Text += onlineVersionInfo.launcher_info.changelog[ChangelogLanguage];
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
            using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
            { 
                var data = new MemoryStream();
                webResponse.GetResponseStream().CopyTo(data);
                miHoYoVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                miHoYoVersionInfo.last_modified = webResponse.LastModified.ToString();
            }
            gameArchiveName = miHoYoVersionInfo.full_version_file.name.ToString();
            webRequest = (HttpWebRequest)WebRequest.Create($"{miHoYoVersionInfo.download_url}/{gameArchiveName}");
            webRequest.Method = "HEAD";
            webRequest.UserAgent = userAgent;
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
                using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    var data = new MemoryStream();
                    webResponse.GetResponseStream().CopyTo(data);
                    var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                    return json;
                }
            }
            catch(WebException ex)
            {
                Status = LauncherStatus.Error;
                if(ex.Response != null)
                {
                    var data = new MemoryStream();
                    ex.Response.GetResponseStream().CopyTo(data);
                    var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                    string message;
                    if(json.error != null)
                        message = json.error.errors[0].message;
                    else
                        message = ex.Message;
                    Log($"ERROR: Failed to fetch Google Drive file metadata:\n{message}");
                    MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], message), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    Status = LauncherStatus.Ready;
                }
                return null;
            }
        }

        private dynamic FetchMediaFireFileMetadata(string id)
        {
            string url = $"https://www.mediafire.com/file/{id}";

            if(String.IsNullOrEmpty(id))
                return null;
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(url);
                webRequest.Method = "HEAD";
                webRequest.UserAgent = userAgent;
                using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    dynamic metadata = new ExpandoObject();
                    metadata.title = webResponse.Headers["Content-Disposition"].Replace("attachment; filename=", String.Empty).Replace("\"", String.Empty);
                    metadata.downloadUrl = url;
                    metadata.fileSize = webResponse.ContentLength;
                    if(Server == HI3Server.Global)
                        metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache.global.md5.ToString();
                    else
                        metadata.md5Checksum = onlineVersionInfo.game_info.mirror.mediafire.game_cache.os.md5.ToString();
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
                    MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], message), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
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
                            mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString());
                        else
                            mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString());
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
                    if(versionInfoKey.GetValue(RegistryVersionInfo) != null)
                    {
                        localVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])versionInfoKey.GetValue(RegistryVersionInfo)));
                        GameVersion localGameVersion = new GameVersion(localVersionInfo.game_info.version.ToString());
                        gameInstallPath = localVersionInfo.game_info.install_path.ToString();
                        gameArchivePath = Path.Combine(gameInstallPath, gameArchiveName);
                        gameExePath = Path.Combine(gameInstallPath, "BH3.exe");
                        game_needs_update = GameUpdateCheckSimple();
                        Log($"Game version: {miHoYoVersionInfo.cur_version.ToString()}");

                        if(game_needs_update)
                        {
                            Log("Game requires an update!");
                            Status = LauncherStatus.UpdateAvailable;
                            Dispatcher.Invoke(() =>
                            {
                                LaunchButton.Content = textStrings["button_update"];
                                ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {ToBytesCount(download_size)}";
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
                                    ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {ToBytesCount(remaining_size)}";
                                }
                            });
                        }
                    }
                    else
                    {
                        Log("No game version info :^(");
                        if(serverChanged)
                            await FetchmiHoYoVersionInfoAsync();
                        Status = LauncherStatus.Ready;
                        Dispatcher.Invoke(() =>
                        {
                            LaunchButton.Content = textStrings["button_download"];
                            ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {ToBytesCount(download_size)}";
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
                                        WriteVersionInfo();
                                        if((int)Server == server)
                                            GameUpdateCheck(false);
                                        else
                                            ServerDropdown.SelectedIndex = server;
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
                webClient.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
                webClient.DownloadFile(new Uri(onlineVersionInfo.launcher_info.url.ToString()), launcherArchivePath);
                Log("Launcher update download OK");
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                if(MessageBox.Show(string.Format(textStrings["msgbox_launcherdownloaderror_msg"], ex), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }
        }

        private async Task DownloadGameFile(bool IsUpdate)
        {
            try
            {
                if(IsUpdate)
                    Status = LauncherStatus.Updating;
                else
                    Status = LauncherStatus.Downloading;

                string download_url;
                string md5;
                bool useGDDownloader = false;
                if(Mirror == HI3Mirror.miHoYo)
                {
                    download_url = $"{miHoYoVersionInfo.download_url.ToString()}/{gameArchiveName}";
                    md5 = miHoYoVersionInfo.full_version_file.md5.ToString();
                }
                else if(Mirror == HI3Mirror.MediaFire)
                {
                    dynamic mediafire_metadata;
                    if(Server == HI3Server.Global)
                        mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString());
                    else
                        mediafire_metadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString());
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
                Dispatcher.Invoke(() => {ProgressBar.IsIndeterminate = false;});
                await Task.Run(() =>
                {
                    tracker.NewFile();
                    if(Mirror == HI3Mirror.miHoYo || Mirror == HI3Mirror.MediaFire || (Mirror == HI3Mirror.GoogleDrive && !useGDDownloader))
                    {
                        download = new DownloadPauseable(download_url, gameArchivePath);
                        download.Start();
                        while(!download.Done)
                        {
                            tracker.SetProgress(download.BytesWritten, download.ContentLength);
                            Dispatcher.Invoke(() =>
                            {
                                ProgressBar.Value = tracker.GetProgress() * 100;
                                ProgressText.Text = string.Format(textStrings["progresstext_downloaded"], ToBytesCount(download.BytesWritten), ToBytesCount(download.ContentLength), tracker.GetBytesPerSecondString());
                            });
                            Thread.Sleep(100);
                        }
                        download = null;
                    }
                    else if(Mirror == HI3Mirror.GoogleDrive && useGDDownloader)
                    {
                        string gd_id;
                        if(Server == HI3Server.Global)
                            gd_id = onlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString();
                        else
                            gd_id = onlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString();
                        var download = new GoogleDriveFileDownloader();
                        download.DownloadProgressChanged += (sender, e) =>
                        {
                            tracker.SetProgress(e.BytesReceived, e.TotalBytesToReceive);
                            Dispatcher.Invoke(() =>
                            {
                                ProgressBar.Value = tracker.GetProgress() * 100;
                                ProgressText.Text = string.Format(textStrings["progresstext_downloaded"], ToBytesCount(e.BytesReceived), ToBytesCount(e.TotalBytesToReceive), tracker.GetBytesPerSecondString());
                            });
                        };
                        download.DownloadFile($"https://drive.google.com/uc?id={gd_id}", gameArchivePath);
                        while(tracker.GetProgress() != 1)
                        {
                            Thread.Sleep(100);
                        }
                    }
                });
                Log("Game archive download OK");
                while(IsFileLocked(new FileInfo(gameArchivePath)))
                    Thread.Sleep(10);
                Dispatcher.Invoke(() => {ProgressText.Text = String.Empty;});
                try
                {
                    Log("Validating game archive");
                    await Task.Run(() =>
                    {
                        Status = LauncherStatus.Verifying;
                        if(CalculateMD5(gameArchivePath) != md5.ToUpper())
                        {
                            Status = LauncherStatus.Error;
                            Log("ERROR: Validation failed. MD5 checksum is incorrect!");
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(textStrings["msgbox_verifyerror_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                            if(File.Exists(gameArchivePath))
                                File.Create(gameArchivePath).Dispose();
                            GameUpdateCheck(false);
                            return;
                        }
                        Log("Validation OK");
                        var skippedFiles = new List<string>();
                        using(var archive = ArchiveFactory.Open(gameArchivePath))
                        {
                            int unpackedFiles = 0;
                            int fileCount = 0;

                            Log("Unpacking game archive...");
                            Status = LauncherStatus.Unpacking;
                            Dispatcher.Invoke(() =>{ProgressBar.IsIndeterminate = false;});
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
                                        Log($"Unpack ERROR: {reader.Entry.ToString()}");
                                    }
                                }
                            }
                        }
                        if(skippedFiles.Count > 0)
                        {
                            MessageBox.Show(textStrings["msgbox_extractskip_msg"], textStrings["msgbox_extractskip_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    });
                    Log("Game archive unpack OK");
                    File.Delete(gameArchivePath);
                    Dispatcher.Invoke(() => 
                    {
                        WriteVersionInfo();
                        Log("Game install OK");
                        GameUpdateCheck(false);
                    });
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install the game:\n{ex}");
                    if(MessageBox.Show(string.Format(textStrings["msgbox_installerror_msg"], ex), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download the game:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_gamedownloaderror_msg"], ex), textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
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
                string value = "USD_V2_201880924_LocalVersion_h3429574199";
                if(versionInfoKey.GetValue(RegistryVersionInfo) == null && (key != null && key.GetValue(value) != null && key.GetValueKind(value) == RegistryValueKind.Binary))
                {
                    var version = Encoding.UTF8.GetString((byte[])key.GetValue(value)).TrimEnd('\u0000');
                    if(!miHoYoVersionInfo.cur_version.ToString().Contains(version))
                        versionInfo.game_info.version = version;
                }

                Log("Writing game version info to registry...");
                ProgressText.Text = textStrings["progresstext_writinginfo"];
                versionInfoKey.SetValue(RegistryVersionInfo, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(versionInfo)), RegistryValueKind.Binary);
                versionInfoKey.Close();
                versionInfoKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Bp\Better HI3 Launcher", true);
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

        private void DeleteGameFiles(bool deleteAll)
        {
            if(deleteAll)
            {
                if(Directory.Exists(gameInstallPath))
                    Directory.Delete(gameInstallPath, true);
                if(Registry.CurrentUser.OpenSubKey(GameRegistryPath) != null)
                    Registry.CurrentUser.DeleteSubKeyTree(GameRegistryPath, true);
            }
            versionInfoKey.DeleteValue(RegistryVersionInfo);
            LaunchButton.Content = textStrings["button_download"];
        }

        private async void DownloadGameCache(bool FullCache)
        {
            try
            {
                string title;
                string download_url;
                string md5;
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
                string cacheArchivePath;
                cacheArchivePath = Path.Combine(miHoYoPath, title);

                var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(cacheArchivePath) && x.IsReady).FirstOrDefault();
                if(gameInstallDrive == null)
                {
                    Dispatcher.Invoke(() => {MessageBox.Show(textStrings["msgbox_install_wrong_drive_type_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
                    return;
                }
                else if(gameInstallDrive.TotalFreeSpace < 1073741824)
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
                Dispatcher.Invoke(() => {ProgressBar.IsIndeterminate = false;});
                await Task.Run(() =>
                {
                    tracker.NewFile();
                    var download = new DownloadPauseable(download_url, cacheArchivePath);
                    download.Start();
                    while(!download.Done)
                    {
                        tracker.SetProgress(download.BytesWritten, download.ContentLength);
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = tracker.GetProgress() * 100;
                            ProgressText.Text = string.Format(textStrings["progresstext_downloaded"], ToBytesCount(download.BytesWritten), ToBytesCount(download.ContentLength), tracker.GetBytesPerSecondString());
                        });
                        Thread.Sleep(100);
                    }
                });
                Log("Game cache download OK");
                while(IsFileLocked(new FileInfo(cacheArchivePath)))
                    Thread.Sleep(10);
                Dispatcher.Invoke(() => {ProgressText.Text = String.Empty;});
                try
                {
                    Log("Validating game cache");
                    await Task.Run(() =>
                    {
                        Status = LauncherStatus.Verifying;
                        Dispatcher.Invoke(() => {ProgressBar.IsIndeterminate = false;});
                        if(CalculateMD5(cacheArchivePath) != md5.ToUpper())
                        {
                            Status = LauncherStatus.Error;
                            Log("ERROR: Validation failed. MD5 checksum is incorrect!");
                            MessageBox.Show(textStrings["msgbox_verifyerror_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                            if(File.Exists(cacheArchivePath))
                                File.Delete(cacheArchivePath);
                            return;
                        }
                        Log("Validation OK");
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
                                        Log($"Unpack ERROR: {reader.Entry.ToString()}");
                                    }
                                }
                            }
                        }
                        if(skippedFiles.Count > 0)
                        {
                            MessageBox.Show(textStrings["msgbox_extractskip_msg"], textStrings["msgbox_extractskip_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    });
                    Log("Game cache unpack OK");
                    File.Delete(cacheArchivePath);
                    Status = LauncherStatus.Ready;
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install game cache:\n{ex}");
                    if(MessageBox.Show(string.Format(textStrings["msgbox_installerror_msg"], ex), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download game cache:\n{ex}");
                if(MessageBox.Show(string.Format(textStrings["msgbox_gamedownloaderror_msg"], ex), textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            #if !DEBUG
            if(versionInfoKey.GetValue("RanOnce") != null && (versionInfoKey.GetValue("LauncherVersion") != null && versionInfoKey.GetValue("LauncherVersion").ToString() != localLauncherVersion.ToString()))
            {
                ChangelogBox.Visibility = Visibility.Visible;
                ChangelogBoxMessageTextBlock.Visibility = Visibility.Visible;
                ChangelogBoxScrollViewer.Height = 319;
            }
            try
            {
                if(versionInfoKey.GetValue("RanOnce") == null)
                    versionInfoKey.SetValue("RanOnce", 1, RegistryValueKind.DWord);
                versionInfoKey.SetValue("LauncherVersion", localLauncherVersion);
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

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if(Status == LauncherStatus.Ready || Status == LauncherStatus.Error)
            {
                if(DownloadPaused)
                {
                    DownloadPaused = false;
                    await DownloadGameFile(false);
                    return;
                }

                if(localVersionInfo != null)
                {
                    if(!File.Exists(gameExePath))
                    {
                        MessageBox.Show(textStrings["msgbox_noexe_msg"], textStrings["msgbox_noexe_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    // not really necessary, makes the launcher hang until it gets online data when you press the launch button
                    /*
                    else if(GameUpdateCheckSimple())
                    {
                        GameUpdateCheck(false);
                        MessageBox.Show(textStrings["msgbox_update_msg"], textStrings["msgbox_update_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    */
                    try
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo(gameExePath);
                        startInfo.WorkingDirectory = Path.Combine(gameInstallPath);
                        startInfo.UseShellExecute = true;
                        Process.Start(startInfo);
                        Close();
                    }
                    catch(Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to start the game:\n{ex}");
                        MessageBox.Show(string.Format(textStrings["msgbox_startgameerror_msg"], ex), textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
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
                            var dialog = new CommonOpenFileDialog();
                            dialog.IsFolderPicker = true;
                            dialog.InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
                            dialog.AddToMostRecentlyUsedList = false;
                            dialog.AllowNonFileSystemItems = false;
                            dialog.DefaultDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
                            dialog.EnsureFileExists = true;
                            dialog.EnsurePathExists = true;
                            dialog.EnsureReadOnly = false;
                            dialog.EnsureValidNames = true;
                            dialog.Multiselect = false;
                            dialog.ShowPlacesList = true;

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
                                            WriteVersionInfo();
                                            if((int)Server == server)
                                                GameUpdateCheck(false);
                                            else
                                                ServerDropdown.SelectedIndex = server;
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
                        else if(gameInstallDrive.TotalFreeSpace < 12884901888)
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
                        await DownloadGameFile(false);
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
                await DownloadGameFile(true);
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

            Status = LauncherStatus.Working;
            Dispatcher.Invoke(() => {ProgressText.Text = textStrings["progresstext_mirrorconnect"];});
            Log("Fetching mirror metadata...");
            await Task.Run(() =>
            {
                try
                {
                    if(Server == HI3Server.Global)
                    {
                        if(Mirror == HI3Mirror.MediaFire)
                        {
                            gameCacheMetadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache.global.id.ToString());
                            gameCacheMetadataNumeric = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.id.ToString());
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
                            gameCacheMetadata = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache.os.id.ToString());
                            gameCacheMetadataNumeric = FetchMediaFireFileMetadata(onlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.id.ToString());
                        }
                        else
                        {
                            gameCacheMetadata = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache.os.ToString());
                            gameCacheMetadataNumeric = FetchGDFileMetadata(onlineVersionInfo.game_info.mirror.gd.game_cache_numeric.os.ToString());
                        }
                    }
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to fetch cache file metadata:\n{ex}");
                    MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], ex), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    Status = LauncherStatus.Ready;
                    return;
                }
                DownloadCacheBox.Visibility = Visibility.Visible;
                if(Mirror == HI3Mirror.MediaFire)
                    DownloadCacheBoxMessageTextBlock.Text = string.Format(textStrings["downloadcachebox_msg"], ((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string, textStrings["shrug"], onlineVersionInfo.game_info.mirror.maintainer.ToString());
                else
                {
                    string time;
                    if(DateTime.Compare(FetchmiHoYoResourceVersionDateModified(), DateTime.Parse(gameCacheMetadataNumeric.modifiedDate.ToString()) >= 0))
                        time = $"{DateTime.Parse(gameCacheMetadataNumeric.modifiedDate.ToString()).ToLocalTime()} ({textStrings["outdated"].ToLower()})";
                    else
                        time = DateTime.Parse(gameCacheMetadataNumeric.modifiedDate.ToString()).ToLocalTime();
                    DownloadCacheBoxMessageTextBlock.Text = string.Format(textStrings["downloadcachebox_msg"], ((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string, time, onlineVersionInfo.game_info.mirror.maintainer.ToString());
                }
                Status = LauncherStatus.Ready;
            });
        }

        private void CM_Uninstall_Click(object sender, RoutedEventArgs e)
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
                Log("Deleting game files and version info from registry");
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
                        if(Directory.Exists(path))
                            Directory.Delete(path, true);
                        Log($"Deleting game cache from {path}");
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
            }
        }

        private void CM_FixUpdateLoop_Click(object sender, RoutedEventArgs e)
        {
            if(Status != LauncherStatus.Ready)
                return;

            if(MessageBox.Show(textStrings["msgbox_fixupdateloop_1_msg"], textStrings["contextmenu_fixupdateloop"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
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
                MessageBox.Show(string.Format(textStrings["msgbox_fixupdateloop_2_msg"], valueBefore, valueAfter), textStrings["contextmenu_fixupdateloop"], MessageBoxButton.OK, MessageBoxImage.Information);
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
                                Log($"Reading subtitle {Path.GetFileName(SubtitleFiles[i])}");
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
                                        Log($"Fixed line {1 + line}: {timecodeLine}");
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

                    if(SubtitleArchives.Count > 0 && subsFixed.Count == 0)
                        MessageBox.Show(string.Format(textStrings["msgbox_fixsubs_4_msg"], SubtitleArchives.Count), textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                    else if(SubtitleArchives.Count == 0 && subsFixed.Count > 0)
                        MessageBox.Show(string.Format(textStrings["msgbox_fixsubs_5_msg"], subsFixed.Count), textStrings["msgbox_notice_title"], MessageBoxButton.OK, MessageBoxImage.Information);
                    else if(SubtitleArchives.Count > 0 && subsFixed.Count > 0)
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
                InputBox.Visibility = Visibility.Visible;
                InputBoxTitleTextBlock.Text = textStrings["inputbox_customfps_title"];
                if(json.TargetFrameRateForInLevel != null)
                    InputBoxTextBox.Text = json.TargetFrameRateForInLevel;
                else
                    InputBoxTextBox.Text = "60";
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

        private void CM_Changelog_Click(object sender, RoutedEventArgs e)
        {
            ChangelogBox.Visibility = Visibility.Visible;
        }

        private void CM_About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(textStrings["msgbox_about_msg"], textStrings["contextmenu_about"], MessageBoxButton.OK, MessageBoxImage.Information);
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
                versionInfoKey.SetValue("LastSelectedServer", index, RegistryValueKind.DWord);
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
                versionInfoKey.SetValue("LastSelectedMirror", index, RegistryValueKind.DWord);
            }
            catch(Exception ex)
            {
                Log($"ERROR: Failed to write value with key LastSelectedMirror to registry:\n{ex}");
            }
            GameUpdateCheck(false);
            Log($"Selected mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");
        }

        // https://stackoverflow.com/q/1268552/7570821
        private void InputBoxTextBox_OnPreview(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            // Use SelectionStart property to find the caret position.
            // Insert the previewed text into the existing text in the textbox.
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            double val;
            // If parsing is successful, set Handled to false
            e.Handled = !double.TryParse(fullText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val);
        }
        private void InputBoxTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
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
        private void InputBoxOKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InputBoxTextBox.Text = String.Concat(InputBoxTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
                if(String.IsNullOrEmpty(InputBoxTextBox.Text))
                {
                    MessageBox.Show(textStrings["msgbox_customfps_1_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                int fps = Int32.Parse(InputBoxTextBox.Text);
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
                InputBox.Visibility = Visibility.Collapsed;
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

        private void ShowLogCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            LogBox.Visibility = Visibility.Visible;
            try
            {
                versionInfoKey.SetValue("ShowLog", 1, RegistryValueKind.DWord);
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
                versionInfoKey.SetValue("ShowLog", 1, RegistryValueKind.DWord);
            }
            catch(Exception ex)
            {
                Log($"ERROR: Failed to write value with key ShowLog to registry:\n{ex}");
            }
        }

        private void InputBoxCancelButton_Click(object sender, RoutedEventArgs e)
        {
            InputBox.Visibility = Visibility.Collapsed;
        }

        private void ChangelogBoxCloseButton_Click(object sender, RoutedEventArgs e)
        {
            ChangelogBoxMessageTextBlock.Visibility = Visibility.Collapsed;
            ChangelogBoxScrollViewer.Height = 339;
            ChangelogBox.Visibility = Visibility.Collapsed;
        }
        private void DownloadCacheBoxFullCacheButton_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show($"{textStrings["msgbox_download_cache_1_msg"]}\n{string.Format(textStrings["msgbox_download_cache_3_msg"], ToBytesCount(gameCacheMetadata.fileSize))}", textStrings["contextmenu_downloadcache"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            DownloadCacheBox.Visibility = Visibility.Collapsed;
            DownloadGameCache(true);
        }

        private void DownloadCacheBoxNumericFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show($"{textStrings["msgbox_download_cache_2_msg"]}\n{string.Format(textStrings["msgbox_download_cache_3_msg"], ToBytesCount(gameCacheMetadata.fileSize))}", textStrings["contextmenu_downloadcache"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            DownloadCacheBox.Visibility = Visibility.Collapsed;
            DownloadGameCache(false);
        }

        private void DownloadCacheBoxCloseButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadCacheBox.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if(Status == LauncherStatus.Downloading || Status == LauncherStatus.Updating)
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
            string[] pathVariants =
            {
                path.Replace(@"Honkai Impact 3rd\Honkai Impact 3rd", @"Honkai Impact 3rd\Games"),
                path.Replace(@"Honkai Impact 3\Honkai Impact 3", @"Honkai Impact 3\Games"),
                path.Replace(@"Honkai Impact 3rd\Games\Honkai Impact 3rd", @"Honkai Impact 3rd\Games"),
                path.Replace(@"Honkai Impact 3\Games\Honkai Impact 3", @"Honkai Impact 3\Games"),
                path.Substring(0, path.Length - 16),
                path.Substring(0, path.Length - 18),
                Path.Combine(path, "Games"),
                Path.Combine(path, "Honkai Impact 3rd"),
                Path.Combine(path, "Honkai Impact 3"),
                Path.Combine(path, "Honkai Impact 3rd", "Games"),
                Path.Combine(path, "Honkai Impact 3", "Games")
            };

            if(File.Exists(Path.Combine(path, gameExeName)))
            {
                return path;
            }
            else
            {
                for(int i = 0; i < pathVariants.Length; i++)
                {
                    if(File.Exists(Path.Combine(pathVariants[i], gameExeName)))
                        return pathVariants[i];
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

        // https://stackoverflow.com/a/10520086/7570821
        private static string CalculateMD5(string filename)
        {
            using(var md5 = MD5.Create())
            {
                using(var stream = File.OpenRead(filename))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", String.Empty);
                }
            }
        }

        private void Log(string msg)
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

        // https://stackoverflow.com/a/49535675/7570821
        private static string ToBytesCount(long bytes)
        {
            int unit = 1024;
            string unitStr = textStrings["binary_prefix_byte"];
            if(bytes < unit) return string.Format("{0} {1}", bytes, unitStr);
            else unitStr = unitStr.ToUpper();
            int exp = (int)(Math.Log(bytes) / Math.Log(unit));
            return string.Format("{0:##.##} {1}{2}", bytes / Math.Pow(unit, exp), textStrings["binary_prefixes"][exp - 1], unitStr);
        }

        private static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using(FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch(IOException)
            {
                return true;
            }

            return false;
        }

        struct Version
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

        struct GameVersion
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

        // https://stackoverflow.com/a/42725580/7570821
        public class DownloadProgressTracker
        {
            private long _totalFileSize;
            private readonly int _sampleSize;
            private readonly TimeSpan _valueDelay;

            private DateTime _lastUpdateCalculated;
            private long _previousProgress;

            private double _cachedSpeed;

            private Queue<Tuple<DateTime, long>> _changes = new Queue<Tuple<DateTime, long>>();

            public DownloadProgressTracker(int sampleSize, TimeSpan valueDelay)
            {
                _lastUpdateCalculated = DateTime.Now;
                _sampleSize = sampleSize;
                _valueDelay = valueDelay;
            }

            public void NewFile()
            {
                _previousProgress = 0;
            }

            public void SetProgress(long bytesReceived, long totalBytesToReceive)
            {
                _totalFileSize = totalBytesToReceive;

                long diff = bytesReceived - _previousProgress;
                if(diff <= 0)
                    return;

                _previousProgress = bytesReceived;

                _changes.Enqueue(new Tuple<DateTime, long>(DateTime.Now, diff));
                while(_changes.Count > _sampleSize)
                    _changes.Dequeue();
            }

            public double GetProgress()
            {
                return _previousProgress / (double) _totalFileSize;
            }

            public string GetProgressString()
            {
                return string.Format("{0:P0}", GetProgress());
            }

            public string GetBytesPerSecondString()
            {
                double speed = GetBytesPerSecond();
                string[] prefix;
                switch(OSLanguage)
                {
                    case "ru-RU":
                    case "uk-UA":
                    case "be-BY":
                        prefix = new[]{"", "К", "М", "Г"};
                        break;
                    default:
                        prefix = new[]{"", "K", "M", "G"};
                        break;
                }

                int index = 0;
                while(speed > 1024 && index < prefix.Length - 1)
                {
                    speed /= 1024;
                    index++;
                }

                int intLen = ((int) speed).ToString().Length;
                int decimals = 3 - intLen;
                if(decimals < 0)
                    decimals = 0;

                string format = string.Format("{{0:F{0}}}", decimals) + " {1}" + textStrings["bytes_per_second"];

                return string.Format(format, speed, prefix[index]);
            }

            public double GetBytesPerSecond()
            {
                if(DateTime.Now >= _lastUpdateCalculated + _valueDelay)
                {
                    _lastUpdateCalculated = DateTime.Now;
                    _cachedSpeed = GetRateInternal();
                }

                return _cachedSpeed;
            }

            private double GetRateInternal()
            {
                if(_changes.Count == 0)
                    return 0;

                TimeSpan timespan = _changes.Last().Item1 - _changes.First().Item1;
                long bytes = _changes.Sum(t => t.Item2);

                double rate = bytes / timespan.TotalSeconds;

                if(double.IsInfinity(rate) || double.IsNaN(rate))
                    return 0;

                return rate;
            }
        }

        // https://gist.github.com/yasirkula/d0ec0c07b138748e5feaecbd93b6223c
        public class GoogleDriveFileDownloader : IDisposable
        {
            private const string GOOGLE_DRIVE_DOMAIN = "drive.google.com";
            private const string GOOGLE_DRIVE_DOMAIN2 = "https://drive.google.com";

            // In the worst case, it is necessary to send 3 download requests to the Drive address
            //   1. an NID cookie is returned instead of a download_warning cookie
            //   2. download_warning cookie returned
            //   3. the actual file is downloaded
            private const int GOOGLE_DRIVE_MAX_DOWNLOAD_ATTEMPT = 3;

            public delegate void DownloadProgressChangedEventHandler(object sender, DownloadProgress progress);

            // Custom download progress reporting (needed for Google Drive)
            public class DownloadProgress
            {
                public long BytesReceived, TotalBytesToReceive;
                public object UserState;

                public int ProgressPercentage
                {
                    get
                    {
                        if(TotalBytesToReceive > 0L)
                            return (int)(((double)BytesReceived / TotalBytesToReceive) * 100);

                        return 0;
                    }
                }
            }

            // Web client that preserves cookies (needed for Google Drive)
            private class CookieAwareWebClient : WebClient
            {
                private class CookieContainer
                {
                    private readonly Dictionary<string, string> cookies = new Dictionary<string, string>();

                    public string this[Uri address]
                    {
                        get
                        {
                            string cookie;
                            if(cookies.TryGetValue(address.Host, out cookie))
                                return cookie;

                            return null;
                        }
                        set
                        {
                            cookies[address.Host] = value;
                        }
                    }
                }

                private readonly CookieContainer cookies = new CookieContainer();
                public DownloadProgress ContentRangeTarget;

                protected override WebRequest GetWebRequest(Uri address)
                {
                    WebRequest request = base.GetWebRequest(address);
                    if(request is HttpWebRequest)
                    {
                        string cookie = cookies[address];
                        if(cookie != null)
                            ((HttpWebRequest)request).Headers.Set("cookie", cookie);

                        if(ContentRangeTarget != null)
                            ((HttpWebRequest)request).AddRange(0);
                    }

                    return request;
                }

                protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
                {
                    return ProcessResponse(base.GetWebResponse(request, result));
                }

                protected override WebResponse GetWebResponse(WebRequest request)
                {
                    return ProcessResponse(base.GetWebResponse(request));
                }

                private WebResponse ProcessResponse(WebResponse response)
                {
                    string[] cookies = response.Headers.GetValues("Set-Cookie");
                    if(cookies != null && cookies.Length > 0)
                    {
                        int length = 0;
                        for(int i = 0; i < cookies.Length; i++)
                            length += cookies[i].Length;

                        StringBuilder cookie = new StringBuilder(length);
                        for(int i = 0; i < cookies.Length; i++)
                            cookie.Append(cookies[i]);

                        this.cookies[response.ResponseUri] = cookie.ToString();
                    }

                    if(ContentRangeTarget != null)
                    {
                        string[] rangeLengthHeader = response.Headers.GetValues("Content-Range");
                        if(rangeLengthHeader != null && rangeLengthHeader.Length > 0)
                        {
                            int splitIndex = rangeLengthHeader[0].LastIndexOf('/');
                            if(splitIndex >= 0 && splitIndex < rangeLengthHeader[0].Length - 1)
                            {
                                long length;
                                if(long.TryParse(rangeLengthHeader[0].Substring(splitIndex + 1), out length))
                                    ContentRangeTarget.TotalBytesToReceive = length;
                            }
                        }
                    }

                    return response;
                }
            }

            private readonly CookieAwareWebClient webClient;
            private readonly DownloadProgress downloadProgress;

            private Uri downloadAddress;
            private string downloadPath;

            private bool asyncDownload;
            private object userToken;

            private bool downloadingDriveFile;
            private int driveDownloadAttempt;

            public event DownloadProgressChangedEventHandler DownloadProgressChanged;
            public event AsyncCompletedEventHandler DownloadFileCompleted;

            public GoogleDriveFileDownloader()
            {
                webClient = new CookieAwareWebClient();
                webClient.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
                webClient.DownloadProgressChanged += DownloadProgressChangedCallback;
                webClient.DownloadFileCompleted += DownloadFileCompletedCallback;

                downloadProgress = new DownloadProgress();
            }

            public void DownloadFile(string address, string fileName)
            {
                DownloadFile(address, fileName, false, null);
            }

            public void DownloadFileAsync(string address, string fileName, object userToken = null)
            {
                DownloadFile(address, fileName, true, userToken);
            }

            private void DownloadFile(string address, string fileName, bool asyncDownload, object userToken)
            {
                downloadingDriveFile = address.StartsWith(GOOGLE_DRIVE_DOMAIN) || address.StartsWith(GOOGLE_DRIVE_DOMAIN2);
                if(downloadingDriveFile)
                {
                    address = GetGoogleDriveDownloadAddress(address);
                    driveDownloadAttempt = 1;

                    webClient.ContentRangeTarget = downloadProgress;
                }
                else
                    webClient.ContentRangeTarget = null;

                downloadAddress = new Uri(address);
                downloadPath = fileName;

                downloadProgress.TotalBytesToReceive = -1L;
                downloadProgress.UserState = userToken;

                this.asyncDownload = asyncDownload;
                this.userToken = userToken;

                DownloadFileInternal();
            }

            private void DownloadFileInternal()
            {
                if(!asyncDownload)
                {
                    webClient.DownloadFile(downloadAddress, downloadPath);

                    // This callback isn't triggered for synchronous downloads, manually trigger it
                    DownloadFileCompletedCallback(webClient, new AsyncCompletedEventArgs(null, false, null));
                }
                else if(userToken == null)
                    webClient.DownloadFileAsync(downloadAddress, downloadPath);
                else
                    webClient.DownloadFileAsync(downloadAddress, downloadPath, userToken);
            }

            private void DownloadProgressChangedCallback(object sender, DownloadProgressChangedEventArgs e)
            {
                if(DownloadProgressChanged != null)
                {
                    downloadProgress.BytesReceived = e.BytesReceived;
                    if(e.TotalBytesToReceive > 0L)
                        downloadProgress.TotalBytesToReceive = e.TotalBytesToReceive;

                    DownloadProgressChanged(this, downloadProgress);
                }
            }

            private void DownloadFileCompletedCallback(object sender, AsyncCompletedEventArgs e)
            {
                if(!downloadingDriveFile)
                {
                    if(DownloadFileCompleted != null)
                        DownloadFileCompleted(this, e);
                }
                else
                {
                    if(driveDownloadAttempt < GOOGLE_DRIVE_MAX_DOWNLOAD_ATTEMPT && !ProcessDriveDownload())
                    {
                        // Try downloading the Drive file again
                        driveDownloadAttempt++;
                        DownloadFileInternal();
                    }
                    else if(DownloadFileCompleted != null)
                        DownloadFileCompleted(this, e);
                }
            }

            // Downloading large files from Google Drive prompts a warning screen and requires manual confirmation
            // Consider that case and try to confirm the download automatically if warning prompt occurs
            // Returns true, if no more download requests are necessary
            private bool ProcessDriveDownload()
            {
                FileInfo downloadedFile = new FileInfo(downloadPath);
                if(downloadedFile == null)
                    return true;

                // Confirmation page is around 50KB, shouldn't be larger than 60KB
                if(downloadedFile.Length > 60000L)
                    return true;

                // Downloaded file might be the confirmation page, check it
                string content;
                using(var reader = downloadedFile.OpenText())
                {
                    // Confirmation page starts with <!DOCTYPE html>, which can be preceeded by a newline
                    char[] header = new char[20];
                    int readCount = reader.ReadBlock(header, 0, 20);
                    if(readCount < 20 || !(new string(header).Contains("<!DOCTYPE html>")))
                        return true;

                    content = reader.ReadToEnd();
                }

                int linkIndex = content.LastIndexOf("href=\"/uc?");
                if(linkIndex < 0)
                    return true;

                linkIndex += 6;
                int linkEnd = content.IndexOf('"', linkIndex);
                if(linkEnd < 0)
                    return true;

                downloadAddress = new Uri("https://drive.google.com" + content.Substring(linkIndex, linkEnd - linkIndex).Replace("&amp;", "&"));
                return false;
            }

            // Handles the following formats (links can be preceeded by https://):
            // - drive.google.com/open?id=FILEID
            // - drive.google.com/file/d/FILEID/view?usp=sharing
            // - drive.google.com/uc?id=FILEID&export=download
            private string GetGoogleDriveDownloadAddress(string address)
            {
                int index = address.IndexOf("id=");
                int closingIndex;
                if(index > 0)
                {
                    index += 3;
                    closingIndex = address.IndexOf('&', index);
                    if(closingIndex < 0)
                        closingIndex = address.Length;
                }
                else
                {
                    index = address.IndexOf("file/d/");
                    if(index < 0) // address is not in any of the supported forms
                        return string.Empty;

                    index += 7;

                    closingIndex = address.IndexOf('/', index);
                    if(closingIndex < 0)
                    {
                        closingIndex = address.IndexOf('?', index);
                        if(closingIndex < 0)
                            closingIndex = address.Length;
                    }
                }

                return string.Concat("https://drive.google.com/uc?id=", address.Substring(index, closingIndex - index), "&export=download");
            }

            public void Dispose()
            {
                webClient.Dispose();
            }
        }

        // https://stackoverflow.com/a/62039306/7570821
        public class DownloadPauseable
        {
            private volatile bool _allowedToRun;
            private readonly string _sourceUrl;
            private readonly string _destination;
            private readonly int _chunkSize;
            private readonly IProgress<double> _progress;
            private readonly Lazy<long> _contentLength;

            public long BytesWritten {get; private set;}
            public long ContentLength => _contentLength.Value;

            public bool Done => ContentLength == BytesWritten;

            public DownloadPauseable(string source, string destination, int chunkSizeInBytes = 8192, IProgress<double> progress = null)
            {
                if(string.IsNullOrEmpty(source))
                    throw new ArgumentNullException("source is empty");
                if(string.IsNullOrEmpty(destination))
                    throw new ArgumentNullException("destination is empty");

                _allowedToRun = true;
                _sourceUrl = source;
                _destination = destination;
                _chunkSize = chunkSizeInBytes;
                _contentLength = new Lazy<long>(GetContentLength);
                _progress = progress;

                if(!File.Exists(destination))
                    BytesWritten = 0;
                else
                {
                    try
                    {
                        BytesWritten = new FileInfo(destination).Length;
                    }
                    catch
                    {
                        BytesWritten = 0;
                    }
                }
            }

            private long GetContentLength()
            {
                var request = (HttpWebRequest)WebRequest.Create(_sourceUrl);
                request.Method = "HEAD";

                using(var response = request.GetResponse())
                    return response.ContentLength;
            }

            private async Task Start(long range)
            {
                if(!_allowedToRun)
                    throw new InvalidOperationException();

                if(Done)
                    //file has been found in folder destination and is already fully downloaded 
                    return;

                var request = (HttpWebRequest)WebRequest.Create(_sourceUrl);
                request.UserAgent = userAgent;
                request.AddRange(range);

                using(var response = await request.GetResponseAsync())
                {
                    using(var responseStream = response.GetResponseStream())
                    {
                        using(var fs = new FileStream(_destination, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        {
                            while(_allowedToRun)
                            {
                                var buffer = new byte[_chunkSize];
                                var bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                                if(bytesRead == 0) break;

                                await fs.WriteAsync(buffer, 0, bytesRead);
                                BytesWritten += bytesRead;
                                _progress?.Report((double)BytesWritten / ContentLength);
                            }

                            await fs.FlushAsync();
                        }
                    }
                }
            }

            public Task Start()
            {
                _allowedToRun = true;
                return Start(BytesWritten);
            }

            public void Pause()
            {
                _allowedToRun = false;
            }
        }

        // https://stackoverflow.com/a/12879118/7570821
        public class TimedWebClient : WebClient
        {
            // Timeout in milliseconds, default = 600,000 msec
            public int Timeout {get; set;}

            public TimedWebClient()
            {
                this.Timeout = 600000;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                var objWebRequest = base.GetWebRequest(address);
                objWebRequest.Timeout = this.Timeout;
                return objWebRequest;
            }
        }
    }
}
