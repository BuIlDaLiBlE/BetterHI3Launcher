using IniParser;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using PartialZip;
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
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace BetterHI3Launcher
{
    enum LauncherStatus
    {
        Ready, Error, CheckingUpdates, Downloading, Updating, Verifying, Unpacking, CleaningUp, UpdateAvailable, Uninstalling, Working, DownloadPaused, Running
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
        public static readonly Version LocalLauncherVersion = new Version("1.1.20210426.0");
        public static readonly string RootPath = Directory.GetCurrentDirectory();
        public static readonly string LocalLowPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}Low";
        public static readonly string LauncherDataPath = Path.Combine(LocalLowPath, @"Bp\Better HI3 Launcher");
        public static readonly string LauncherLogFile = Path.Combine(LauncherDataPath, "BetterHI3Launcher-latest.log");
        public static readonly string miHoYoPath = Path.Combine(LocalLowPath, "miHoYo");
        public static readonly string GameExeName = "BH3.exe";
        public static readonly string OSLanguage = CultureInfo.CurrentUICulture.ToString();
        public static string UserAgent = $"BetterHI3Launcher v{LocalLauncherVersion}";
        public static string LauncherLanguage;
        public static string GameInstallPath, GameArchivePath, GameArchiveName, GameExePath, CacheArchivePath, LauncherExeName, LauncherPath, LauncherArchivePath;
        public static string RegistryVersionInfo;
        public static string GameRegistryPath, GameRegistryLocalVersionRegValue, GameWebProfileURL, GameFullName;
        public static string[] CommandLineArgs = Environment.GetCommandLineArgs();
        public static bool FirstLaunch = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Bp\Better HI3 Launcher") == null ? true : false;
        public static bool DisableAutoUpdate, DisableLogging, AdvancedFeatures, DownloadPaused, PatchDownload;
        public static Dictionary<string, string> textStrings = new Dictionary<string, string>();
        public dynamic LocalVersionInfo, OnlineVersionInfo, OnlineRepairInfo, miHoYoVersionInfo, GameGraphicSettings, GameScreenSettings, GameCacheMetadata, GameCacheMetadataNumeric;
        LauncherStatus _status;
        HI3Server _gameserver;
        HI3Mirror _downloadmirror;
        RegistryKey LauncherRegKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Bp\Better HI3 Launcher");
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
                    void ToggleProgressBar(bool val)
                    {
                        ProgressBar.Visibility = val ? Visibility.Visible : Visibility.Hidden;
                        ProgressBar.IsIndeterminate = true;
                        TaskbarItemInfo.ProgressState = val ? TaskbarItemProgressState.Indeterminate : TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    }

                    _status = value;
                    WindowState = WindowState.Normal;
                    switch(_status)
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
                            DownloadPaused = false;
                            ProgressText.Text = textStrings["progresstext_initiating_download"];
                            LaunchButton.Content = textStrings["button_downloading"];
                            ToggleUI(false);
                            ToggleProgressBar(true);
                            ProgressBar.IsIndeterminate = false;
                            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                            break;
                        case LauncherStatus.DownloadPaused:
                            DownloadPaused = true;
                            ProgressText.Text = string.Empty;
                            ToggleUI(true);
                            ToggleProgressBar(false);
                            ToggleContextMenuItems(false);
                            break;
                        case LauncherStatus.Working:
                            ToggleUI(false);
                            ToggleProgressBar(true);
                            break;
                        case LauncherStatus.Running:
                            ProgressText.Text = string.Empty;
                            LaunchButton.Content = textStrings["button_running"];
                            ToggleUI(false);
                            OptionsButton.IsEnabled = true;
                            ToggleProgressBar(false);
                            ToggleContextMenuItems(false);
                            break;
                        case LauncherStatus.Verifying:
                            ProgressText.Text = textStrings["progresstext_verifying_files"];
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
                switch(_gameserver)
                {
                    case HI3Server.Global:
                        RegistryVersionInfo = "VersionInfoGlobal";
                        GameRegistryPath = @"SOFTWARE\miHoYo\Honkai Impact 3rd";
                        GameFullName = "Honkai Impact 3rd";
                        GameWebProfileURL = "https://global.user.honkaiimpact3.com";
                        break;
                    case HI3Server.SEA:
                        RegistryVersionInfo = "VersionInfoSEA";
                        GameRegistryPath = @"SOFTWARE\miHoYo\Honkai Impact 3";
                        GameFullName = "Honkai Impact 3";
                        GameWebProfileURL = "https://asia.user.honkaiimpact3.com";
                        break;
                }
                GameRegistryLocalVersionRegValue = null;
                var regKey = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                if(regKey != null)
                {
                    var versionCandidates = new List<string>();
                    var versionCandidateValues = new List<string>();
                    foreach(string regValue in regKey.GetValueNames())
                    {
                        if(regValue.Contains("LocalVersion_h"))
                        {
                            versionCandidates.Add(regValue);
                            versionCandidateValues.Add(Encoding.UTF8.GetString((byte[])regKey.GetValue(regValue)));
                        }
                    }
                    if(versionCandidates.Count > 0)
                    {
                        string versionCandidate = null;
                        int versionCandidateMajor = 0;
                        int versionCandidateMinor = 0;
                        int versionCandidatePatch = 0;
                        for(int i = 0; i < versionCandidates.Count; i++)
                        {
                            if(versionCandidateValues[i].Length < 5)
                                continue;
                            int major = (int)char.GetNumericValue(versionCandidateValues[i][0]);
                            int minor = (int)char.GetNumericValue(versionCandidateValues[i][2]);
                            int patch = (int)char.GetNumericValue(versionCandidateValues[i][4]);
                            if(versionCandidateMajor < major)
                            {
                                versionCandidateMajor = major;
                                versionCandidate = versionCandidates[i];
                            }
                            if(versionCandidateMinor < minor)
                            {
                                versionCandidateMinor = minor;
                                versionCandidate = versionCandidates[i];
                            }
                            if(versionCandidatePatch < patch)
                            {
                                versionCandidatePatch = patch;
                                versionCandidate = versionCandidates[i];
                            }
                        }
                        GameRegistryLocalVersionRegValue = versionCandidate;
                    }
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
                UserAgent += " [DEBUG]";
            #endif

            InitializeComponent();
            var args = new List<string>();
            for(int i = 1; i < CommandLineArgs.Length; i++)
            {
                args.Add(CommandLineArgs[i].ToUpper());
            }
            if(args.Contains("NOLOG"))
            {
                DisableLogging = true;
            }
            if(!DisableLogging)
            {
                try
                {
                    if(File.Exists(LauncherLogFile))
                    {
                        string old_log_path_1 = Path.Combine(LauncherDataPath, $"BetterHI3Launcher-old1.log");
                        for(int i = 9; i > 0; i--)
                        {
                            string old_log_path_2 = Path.Combine(LauncherDataPath, $"BetterHI3Launcher-old{i}.log");
                            if(File.Exists(old_log_path_2))
                            {
                                string old_log_path_3 = Path.Combine(LauncherDataPath, $"BetterHI3Launcher-old{i + 1}.log");
                                string old_log_path_4 = Path.Combine(LauncherDataPath, $"BetterHI3Launcher-old10.log");
                                if(File.Exists(old_log_path_4))
                                {
                                    File.Delete(old_log_path_4);
                                }
                                File.Move(old_log_path_2, old_log_path_3);
                            }
                        }
                        File.Move(LauncherLogFile, old_log_path_1);
                    }
                }
                catch
                {
                    Log($"WARNING: Unable to rename log files", true, 2);
                }
            }
            DeleteFile(LauncherLogFile, true);
            Log(UserAgent, false);
            Log($"Working directory: {RootPath}");
            Log($"OS language: {OSLanguage}");
            SetLanguage(null);
            switch(OSLanguage)
            {
                case "de-AT":
                case "de-CH":
                case "de-DE":
                case "de-LI":
                case "de-LU":
                    LauncherLanguage = "de";
                    break;
                case "es-AR":
                case "es-BO":
                case "es-CL":
                case "es-CO":
                case "es-CR":
                case "es-DO":
                case "es-EC":
                case "es-ES":
                case "es-GT":
                case "es-HN":
                case "es-MX":
                case "es-NI":
                case "es-PA":
                case "es-PE":
                case "es-PR":
                case "es-PY":
                case "es-SV":
                case "es-US":
                case "es-UY":
                    LauncherLanguage = "es";
                    break;
                case "pt-BR":
                case "pt-PT":
                    LauncherLanguage = "pt";
                    break;
                case "ru-RU":
                case "uk-UA":
                case "be-BY":
                    LauncherLanguage = "ru";
                    break;
                case "sr-Cyrl-BA":
                case "sr-Cyrl-CS":
                case "sr-Cyrl-ME":
                case "sr-Cyrl-RS":
                case "sr-Latn-BA":
                case "sr-Latn-CS":
                case "sr-Latn-ME":
                case "sr-Latn-RS":
                    LauncherLanguage = "sr";
                    break;
                case "vi-VN":
                    LauncherLanguage = "vi";
                    break;
                default:
                    LauncherLanguage = "en";
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
            UserAgent += $" [{LauncherLanguage}]";

            #if !DEBUG
            if(args.Contains("NOUPDATE"))
            {
                DisableAutoUpdate = true;
                UserAgent += " [NOUPDATE]";
                Log("Auto-update disabled");
            }
            #endif
            if(args.Contains("NOSHADOW"))
            {
                Border.Effect = null;
                Application.Current.MainWindow.Width = Grid.Width;
                Application.Current.MainWindow.Height = Grid.Height;
                Log("DropShadowEffect disabled");
            }
            if(args.Contains("NOLOG"))
            {
                Log("Logging disabled");
            }
            if(args.Contains("ADVANCED"))
            {
                AdvancedFeatures = true;
                UserAgent += " [ADVANCED]";
                Log("Advanced features enabled");
            }
            else
            {
                RepairBoxGenerateButton.Visibility = Visibility.Collapsed;
            }

            LaunchButton.Content = textStrings["button_download"];
            OptionsButton.Content = textStrings["button_options"];
            ServerLabel.Text = $"{textStrings["label_server"]}:";
            MirrorLabel.Text = $"{textStrings["label_mirror"]}:";
            IntroBoxTitleTextBlock.Text = textStrings["introbox_title"];
            IntroBoxImportantMessageTextBlock.Text = textStrings["introbox_msg_1"];
            IntroBoxMessageTextBlock.Text = textStrings["introbox_msg_2"];
            IntroBoxOKButton.Content = textStrings["button_ok"];
            DownloadCacheBoxTitleTextBlock.Text = textStrings["contextmenu_downloadcache"];
            DownloadCacheBoxFullCacheButton.Content = textStrings["downloadcachebox_button_full_cache"];
            DownloadCacheBoxNumericFilesButton.Content = textStrings["downloadcachebox_button_numeric_files"];
            DownloadCacheBoxCancelButton.Content = textStrings["button_cancel"];
            RepairBoxTitleTextBlock.Text = textStrings["contextmenu_repair"];
            RepairBoxYesButton.Content = textStrings["button_yes"];
            RepairBoxNoButton.Content = textStrings["button_no"];
            RepairBoxGenerateButton.Content = textStrings["button_generate"];
            FPSInputBoxTitleTextBlock.Text = textStrings["fpsinputbox_title"];
            CombatFPSInputBoxTextBlock.Text = textStrings["fpsinputbox_label_combatfps"];
            MenuFPSInputBoxTextBlock.Text = textStrings["fpsinputbox_label_menufps"];
            FPSInputBoxOKButton.Content = textStrings["button_confirm"];
            FPSInputBoxCancelButton.Content = textStrings["button_cancel"];
            ResolutionInputBoxTitleTextBlock.Text = textStrings["resolutioninputbox_title"];
            ResolutionInputBoxWidthTextBlock.Text = $"{textStrings["resolutioninputbox_label_width"]}:";
            ResolutionInputBoxHeightTextBlock.Text = $"{textStrings["resolutioninputbox_label_height"]}:";
            ResolutionInputBoxFullscreenTextBlock.Text = $"{textStrings["resolutioninputbox_label_fullscreen"]}:";
            ResolutionInputBoxOKButton.Content = textStrings["button_confirm"];
            ResolutionInputBoxCancelButton.Content = textStrings["button_cancel"];
            ChangelogBoxTitleTextBlock.Text = textStrings["changelogbox_title"];
            ChangelogBoxMessageTextBlock.Text = textStrings["changelogbox_1_msg"];
            ChangelogBoxOKButton.Content = textStrings["button_ok"];
            AboutBoxTitleTextBlock.Text = textStrings["contextmenu_about"];
            AboutBoxMessageTextBlock.Text = $"{textStrings["aboutbox_msg"]}\n\nMade by Bp (BuIlDaLiBlE production).";
            AboutBoxGitHubButton.Content = textStrings["button_github"];
            AboutBoxOKButton.Content = textStrings["button_ok"];
            ShowLogLabel.Text = textStrings["label_log"];

            Grid.MouseLeftButtonDown += delegate{DragMove();};
            LogBox.Visibility = Visibility.Collapsed;
            LogBoxRichTextBox.Document.PageWidth = LogBox.Width;
            IntroBox.Visibility = Visibility.Collapsed;
            RepairBox.Visibility = Visibility.Collapsed;
            FPSInputBox.Visibility = Visibility.Collapsed;
            ResolutionInputBox.Visibility = Visibility.Collapsed;
            DownloadCacheBox.Visibility = Visibility.Collapsed;
            ChangelogBox.Visibility = Visibility.Collapsed;
            AboutBox.Visibility = Visibility.Collapsed;

            OptionsContextMenu.Items.Clear();
            var CMDownloadCache = new MenuItem{Header = textStrings["contextmenu_downloadcache"]};
            CMDownloadCache.Click += async (sender, e) => await CM_DownloadCache_Click(sender, e);
            OptionsContextMenu.Items.Add(CMDownloadCache);
            var CMRepair = new MenuItem{Header = textStrings["contextmenu_repair"]};
            CMRepair.Click += async (sender, e) => await CM_Repair_Click(sender, e);
            OptionsContextMenu.Items.Add(CMRepair);
            var CMMove = new MenuItem{Header = textStrings["contextmenu_move"]};
            CMMove.Click += async (sender, e) => await CM_Move_Click(sender, e);
            OptionsContextMenu.Items.Add(CMMove);
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
            var CMGameSettings = new MenuItem{Header = textStrings["contextmenu_game_settings"]};
            var CMCustomFPS = new MenuItem{Header = textStrings["contextmenu_customfps"]};
            CMCustomFPS.Click += (sender, e) => CM_CustomFPS_Click(sender, e);
            CMGameSettings.Items.Add(CMCustomFPS);
            var CMCustomResolution = new MenuItem{Header = textStrings["contextmenu_customresolution"]};
            CMCustomResolution.Click += (sender, e) => CM_CustomResolution_Click(sender, e);
            CMGameSettings.Items.Add(CMCustomResolution);
            var CMResetGameSettings = new MenuItem{Header = textStrings["contextmenu_resetgamesettings"]};
            CMResetGameSettings.Click += (sender, e) => CM_ResetGameSettings_Click(sender, e);
            CMGameSettings.Items.Add(CMResetGameSettings);
            OptionsContextMenu.Items.Add(CMGameSettings);
            OptionsContextMenu.Items.Add(new Separator());
            var CMWebProfile = new MenuItem{Header = textStrings["contextmenu_web_profile"]};
            CMWebProfile.Click += (sender, e) => BpUtility.StartProcess(GameWebProfileURL, null, RootPath, true);
            OptionsContextMenu.Items.Add(CMWebProfile);
            var CMFeedback = new MenuItem{Header = textStrings["contextmenu_feedback"]};
            CMFeedback.Click += (sender, e) => BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher/issues/new/choose", null, RootPath, true);
            OptionsContextMenu.Items.Add(CMFeedback);
            var CMChangelog = new MenuItem{Header = textStrings["contextmenu_changelog"]};
            CMChangelog.Click += (sender, e) => CM_Changelog_Click(sender, e);
            OptionsContextMenu.Items.Add(CMChangelog);
            var CMImportantInfo = new MenuItem {Header = textStrings["contextmenu_important_info"]};
            CMImportantInfo.Click += (sender, e) => IntroBox.Visibility = Visibility.Visible;
            OptionsContextMenu.Items.Add(CMImportantInfo);
            var CMLanguage = new MenuItem{Header = textStrings["contextmenu_language"]};
            var CMLanguageSystem = new MenuItem{Header = textStrings["contextmenu_language_system"]};
            CMLanguageSystem.Click += (sender, e) => CM_Language_Click(sender, e);
            CMLanguage.Items.Add(CMLanguageSystem);
            var CMLanguageEnglish = new MenuItem{Header = textStrings["contextmenu_language_english"]};
            CMLanguageEnglish.Click += (sender, e) => CM_Language_Click(sender, e);
            CMLanguage.Items.Add(CMLanguageEnglish);
            var CMLanguageRussian = new MenuItem{Header = textStrings["contextmenu_language_russian"]};
            CMLanguageRussian.Click += (sender, e) => CM_Language_Click(sender, e);
            CMLanguage.Items.Add(CMLanguageRussian);
            var CMLanguageSpanish = new MenuItem{Header = textStrings["contextmenu_language_spanish"]};
            CMLanguageSpanish.Click += (sender, e) => CM_Language_Click(sender, e);
            CMLanguage.Items.Add(CMLanguageSpanish);
            var CMLanguagePortuguese = new MenuItem{Header = textStrings["contextmenu_language_portuguese"]};
            CMLanguagePortuguese.Click += (sender, e) => CM_Language_Click(sender, e);
            CMLanguage.Items.Add(CMLanguagePortuguese);
            var CMLanguageGerman = new MenuItem{Header = textStrings["contextmenu_language_german"]};
            CMLanguageGerman.Click += (sender, e) => CM_Language_Click(sender, e);
            CMLanguage.Items.Add(CMLanguageGerman);
            var CMLanguageVietnamese = new MenuItem{Header = textStrings["contextmenu_language_vietnamese"]};
            CMLanguageVietnamese.Click += (sender, e) => CM_Language_Click(sender, e);
            CMLanguage.Items.Add(CMLanguageVietnamese);
            var CMLanguageSerbian = new MenuItem{Header = textStrings["contextmenu_language_serbian"]};
            CMLanguageSerbian.Click += (sender, e) => CM_Language_Click(sender, e);
            CMLanguage.Items.Add(CMLanguageSerbian);
            CMLanguage.Items.Add(new Separator());
            var CMLanguageContribute = new MenuItem{Header = textStrings["contextmenu_language_contribute"]};
            CMLanguageContribute.Click += (sender, e) => BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher#contibuting-translations", null, RootPath, true);
            CMLanguage.Items.Add(CMLanguageContribute);
            OptionsContextMenu.Items.Add(CMLanguage);
            var CMAbout = new MenuItem{Header = textStrings["contextmenu_about"]};
            CMAbout.Click += (sender, e) => CM_About_Click(sender, e);
            OptionsContextMenu.Items.Add(CMAbout);
            if(LanguageRegValue == null)
            {
                CMLanguageSystem.IsChecked = true;
            }
            else
            {
                switch(LanguageRegValue.ToString())
                {
                    case "ru":
                        CMLanguageRussian.IsChecked = true;
                        break;
                    case "es":
                        CMLanguageSpanish.IsChecked = true;
                        break;
                    case "pt":
                        CMLanguagePortuguese.IsChecked = true;
                        break;
                    case "de":
                        CMLanguageGerman.IsChecked = true;
                        break;
                    case "vi":
                        CMLanguageVietnamese.IsChecked = true;
                        break;
                    case "sr":
                        CMLanguageSerbian.IsChecked = true;
                        break;
                    default:
                        CMLanguageEnglish.IsChecked = true;
                        break;
                }
            }

            var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            if(key == null || (int)key.GetValue("Release") < 394254)
            {
                if(MessageBox.Show(textStrings["msgbox_net_version_old_msg"], textStrings["msgbox_starterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }

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

                try
                {
                    FetchOnlineVersionInfo();
                }
                catch(Exception ex)
                {
                    if(Status == LauncherStatus.Error)
                        return;
                    Status = LauncherStatus.Error;
                    if(MessageBox.Show($"{textStrings["msgbox_conn_bp_error_msg"]}\n{ex}", textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }
                try
                {
                    FetchmiHoYoVersionInfo();
                }
                catch(Exception ex)
                {
                    if(Status == LauncherStatus.Error)
                        return;
                    Status = LauncherStatus.Error;
                    if(MessageBox.Show($"{textStrings["msgbox_conn_mihoyo_error_msg"]}\n{ex}", textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }
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
                DownloadBackgroundImage();
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
                var version_info_url = new[]{"https://bpnet.host/bh3?launcherstatus_debug"};
            #else
                var version_info_url = new[]{"https://bpnet.host/bh3?launcherstatus", "https://serioussam.ucoz.ru/bbh3l_prod.json"};
            #endif
            string version_info;
            var webClient = new BpWebClient();
            try
            {
                version_info = webClient.DownloadString(version_info_url[0]);
            }
            catch
            {
                version_info = webClient.DownloadString(version_info_url[1]);
            }
            OnlineVersionInfo = JsonConvert.DeserializeObject<dynamic>(version_info);
            if(OnlineVersionInfo.status == "success")
            {
                OnlineVersionInfo = OnlineVersionInfo.launcherstatus;
                LauncherExeName = OnlineVersionInfo.launcher_info.name;
                LauncherPath = Path.Combine(RootPath, LauncherExeName);
                LauncherArchivePath = Path.Combine(RootPath, OnlineVersionInfo.launcher_info.url.ToString().Substring(OnlineVersionInfo.launcher_info.url.ToString().LastIndexOf('/') + 1));
                Dispatcher.Invoke(() =>
                {
                    LauncherVersionText.Text = $"{textStrings["launcher_version"]}: v{LocalLauncherVersion}";
                    ShowLogStackPanel.Margin = new Thickness((double)OnlineVersionInfo.launcher_info.ui.ShowLogStackPanel_Margin.left, 0, 0, (double)OnlineVersionInfo.launcher_info.ui.ShowLogStackPanel_Margin.bottom);
                    LogBox.Margin = new Thickness((double)OnlineVersionInfo.launcher_info.ui.LogBox_Margin.left, (double)OnlineVersionInfo.launcher_info.ui.LogBox_Margin.top, (double)OnlineVersionInfo.launcher_info.ui.LogBox_Margin.right, (double)OnlineVersionInfo.launcher_info.ui.LogBox_Margin.bottom);
                });
            }
            else
            {
                Status = LauncherStatus.Error;
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(string.Format(textStrings["msgbox_neterror_msg"], OnlineVersionInfo.status_message), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                });
            }
        }

        private async void FetchChangelog()
        {
            if(ChangelogBoxTextBox.Text != string.Empty)
                return;

            string changelog;
            var webClient = new BpWebClient();

            Dispatcher.Invoke(() => {ChangelogBoxTextBox.Text = textStrings["changelogbox_2_msg"];});
            await Task.Run(() =>
            {
                try
                {
                    if(LauncherLanguage == "ru")
                        changelog = webClient.DownloadString(OnlineVersionInfo.launcher_info.changelog_url.ru.ToString());
                    else
                        changelog = webClient.DownloadString(OnlineVersionInfo.launcher_info.changelog_url.en.ToString());
                }
                catch
                {
                    changelog = textStrings["changelogbox_3_msg"];
                }
                Dispatcher.Invoke(() => {ChangelogBoxTextBox.Text = changelog;});
            });
        }

        private void FetchmiHoYoVersionInfo()
        {
            string url;
            if(Server == HI3Server.Global)
                url = OnlineVersionInfo.game_info.mirror.mihoyo.version_info.global.ToString();
            else
                url = OnlineVersionInfo.game_info.mirror.mihoyo.version_info.os.ToString();

            var webRequest = BpUtility.CreateWebRequest(url);
            using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                using(var data = new MemoryStream())
                {
                    webResponse.GetResponseStream().CopyTo(data);
                    miHoYoVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(data.ToArray()));
                    miHoYoVersionInfo.last_modified = webResponse.LastModified.ToUniversalTime().ToString();
                }
            }
            GameArchiveName = miHoYoVersionInfo.full_version_file.name.ToString();
            webRequest = BpUtility.CreateWebRequest($"{miHoYoVersionInfo.download_url}/{GameArchiveName}", "HEAD");
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

        private DateTime FetchmiHoYoResourceVersionDateModified()
        {
            var url = new string[2];
            var time = new DateTime[2];
            if(Server == HI3Server.Global)
            {
                url[0] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.global[0].ToString();
                url[1] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.global[1].ToString();
            }
            else
            {
                url[0] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.os[0].ToString();
                url[1] = OnlineVersionInfo.game_info.mirror.mihoyo.resource_version.os[1].ToString();
            }
            try
            {
                for(int i = 0; i < url.Length; i++)
                {
                    var webRequest = BpUtility.CreateWebRequest(url[i], "HEAD");
                    using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                    {
                        time[i] = webResponse.LastModified.ToUniversalTime();
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

        private dynamic FetchMediaFireFileMetadata(string id, bool numeric)
        {
            if(string.IsNullOrEmpty(id))
                throw new ArgumentNullException();

            string url = $"https://www.mediafire.com/file/{id}";
            try
            {
                var webRequest = BpUtility.CreateWebRequest(url, "HEAD");
                using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    dynamic metadata = new ExpandoObject();
                    metadata.title = webResponse.Headers["Content-Disposition"].Replace("attachment; filename=", string.Empty).Replace("\"", string.Empty);
                    metadata.modifiedDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                    metadata.downloadUrl = url;
                    metadata.fileSize = webResponse.ContentLength;
                    if(!numeric)
                    {
                        if(Server == HI3Server.Global)
                            metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache.global.md5.ToString();
                        else
                            metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache.os.md5.ToString();
                    }
                    else
                    {
                        if(Server == HI3Server.Global)
                            metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.md5.ToString();
                        else
                            metadata.md5Checksum = OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.md5.ToString();
                    }
                    return metadata;
                }
            }
            catch(WebException ex)
            {
                Status = LauncherStatus.Error;
                if(ex.Response != null)
                {
                    string msg = ex.Message;
                    Log($"ERROR: Failed to fetch MediaFire file metadata:\n{msg}", true, 1);
                    Dispatcher.Invoke(() => {MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], msg), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
                    Status = LauncherStatus.Ready;
                    Dispatcher.Invoke(() => {LaunchButton.IsEnabled = false;});
                }
                return null;
            }
        }

        private dynamic FetchGDFileMetadata(string id)
        {
            if(string.IsNullOrEmpty(id))
                throw new ArgumentNullException();

            string url = $"https://www.googleapis.com/drive/v2/files/{id}?key={OnlineVersionInfo.launcher_info.gd_key}";
            try
            {
                var webRequest = BpUtility.CreateWebRequest(url);
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
                        string msg;
                        if(json.error != null)
                            msg = json.error.errors[0].message;
                        else
                            msg = ex.Message;
                        Log($"ERROR: Failed to fetch Google Drive file metadata:\n{msg}", true, 1);
                        Dispatcher.Invoke(() => {MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], msg), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
                        Status = LauncherStatus.Ready;
                        Dispatcher.Invoke(() => {LaunchButton.IsEnabled = false;});
                    }
                }
                return null;
            }
        }

        private bool LauncherUpdateCheck()
        {
            Version OnlineLauncherVersion = new Version(OnlineVersionInfo.launcher_info.version.ToString());
            if(LocalLauncherVersion.IsDifferentThan(OnlineLauncherVersion))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async void GameUpdateCheck(bool serverChanged = false)
        {
            if(Status == LauncherStatus.Error)
                return;

            Status = LauncherStatus.CheckingUpdates;
            Log("Checking for game update...");
            LocalVersionInfo = null;
            await Task.Run(() =>
            {
                FetchOnlineVersionInfo();
                try
                {
                    int game_needs_update;
                    long download_size = 0;
                    if(Mirror == HI3Mirror.miHoYo)
                    {
                        // space_usage is probably when archive is unpacked, here I get the download size instead
                        // download_size = miHoYoVersionInfo.space_usage;
                        download_size = miHoYoVersionInfo.size;
                    }
                    else if(Mirror == HI3Mirror.MediaFire)
                    {
                        dynamic mediafire_metadata;
                        if(Server == HI3Server.Global)
                            mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString(), false);
                        else
                            mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString(), false);
                        if(mediafire_metadata == null)
                            return;
                        download_size = mediafire_metadata.fileSize;
                    }
                    else if(Mirror == HI3Mirror.GoogleDrive)
                    {
                        dynamic gd_metadata;
                        if(Server == HI3Server.Global)
                            gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
                        else
                            gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
                        if(gd_metadata == null)
                            return;
                        download_size = gd_metadata.fileSize;
                    }
                    if(LauncherRegKey.GetValue(RegistryVersionInfo) != null)
                    {
                        LocalVersionInfo = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])LauncherRegKey.GetValue(RegistryVersionInfo)));
                        var LocalGameVersion = new GameVersion(LocalVersionInfo.game_info.version.ToString());
                        GameInstallPath = LocalVersionInfo.game_info.install_path.ToString();
                        game_needs_update = GameUpdateCheckSimple();
                        GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
                        GameExePath = Path.Combine(GameInstallPath, "BH3.exe");

                        Log($"Game version: {LocalGameVersion}");
                        Log($"Game directory: {GameInstallPath}");
                        if(game_needs_update != 0)
                        {
                            PatchDownload = false;
                            if(game_needs_update == 2 && Mirror == HI3Mirror.miHoYo)
                            {
                                var webRequest = BpUtility.CreateWebRequest($"{miHoYoVersionInfo.download_url}/{miHoYoVersionInfo.patch_list[LocalVersionInfo.game_info.version.ToString()].name.ToString()}", "HEAD");
                                using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                                {
                                    download_size = webResponse.ContentLength;
                                }
                                GameArchiveName = miHoYoVersionInfo.patch_list[LocalGameVersion.ToString()].name.ToString();
                                GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
                                PatchDownload = true;
                            }
                            Log("Game requires an update!");
                            Status = LauncherStatus.UpdateAvailable;
                        }
                        else if(LocalVersionInfo.game_info.installed == false)
                        {
                            DownloadPaused = true;
                            Status = LauncherStatus.UpdateAvailable;
                        }
                        else if(!File.Exists(GameExePath))
                        {
                            Log($"WARNING: Game executable is missing, resetting game version info...", true, 2);
                            DeleteGameFiles();
                            GameUpdateCheck();
                            return;
                        }
                        else
                        {
                            var process = Process.GetProcessesByName("BH3");
                            if(process.Length > 0)
                            {
                                process[0].EnableRaisingEvents = true;
                                process[0].Exited += new EventHandler((object s, EventArgs ea) => {OnGameExit();});
                                Status = LauncherStatus.Running;
                            }
                            else
                            {
                                Status = LauncherStatus.Ready;
                                Dispatcher.Invoke(() => {LaunchButton.Content = textStrings["button_launch"];});
                            }
                        }
                        if(Status == LauncherStatus.UpdateAvailable)
                        {
                            if(File.Exists(GameArchivePath))
                            {
                                DownloadPaused = true;
                                var remaining_size = download_size - new FileInfo(GameArchivePath).Length;
                                Dispatcher.Invoke(() =>
                                {
                                    if(remaining_size > 0)
                                        ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {BpUtility.ToBytesCount(remaining_size)}";
                                    else
                                        ProgressText.Text = String.Empty;
                                    LaunchButton.Content = textStrings["button_update"];
                                });
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    LaunchButton.Content = textStrings["button_update"];
                                    ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {BpUtility.ToBytesCount(download_size)}";
                                });
                            }
                        }
                    }
                    else
                    {
                        Log("Game is not installed :^(");
                        if(serverChanged)
                            FetchmiHoYoVersionInfo();
                        Status = LauncherStatus.Ready;
                        Dispatcher.Invoke(() =>
                        {
                            LaunchButton.Content = textStrings["button_download"];
                            ProgressText.Text = $"{textStrings["progresstext_downloadsize"]}: {BpUtility.ToBytesCount(download_size)}";
                            ToggleContextMenuItems(false);
                            var path = CheckForExistingGameDirectory(RootPath);
                            if(string.IsNullOrEmpty(path))
                            {
                                path = CheckForExistingGameDirectory(Environment.ExpandEnvironmentVariables("%ProgramW6432%"));
                            }
                            if(path.Length < 4)
                            {
                                path = string.Empty;
                            }
                            if(!string.IsNullOrEmpty(path))
                            {
                                if(MessageBox.Show(string.Format(textStrings["msgbox_installexisting_msg"], path), textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                                {
                                    Log($"Existing install directory selected: {path}");
                                    GameInstallPath = path;
                                    var server = CheckForExistingGameClientServer();
                                    if(server >= 0)
                                    {
                                        if((int)Server != server)
                                            ServerDropdown.SelectedIndex = server;
                                        WriteVersionInfo(true, true);
                                        GameUpdateCheck();
                                    }
                                    else
                                    {
                                        Status = LauncherStatus.Error;
                                        Log($"ERROR: Directory {GameInstallPath} doesn't contain a valid installation of the game.\nThis launcher only supports Global and SEA clients!", true, 1);
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
                    if(serverChanged)
                        DownloadBackgroundImage();
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Checking for game update failed:\n{ex}", true, 1);
                    Dispatcher.Invoke(() =>
                    {
                        if(MessageBox.Show(textStrings["msgbox_updatecheckerror_msg"], textStrings["msgbox_updatecheckerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        {
                            return;
                        }
                    });
                }
            });
        }

        private int GameUpdateCheckSimple()
        {
            if(LocalVersionInfo != null)
            {
                FetchmiHoYoVersionInfo();
                var LocalGameVersion = new GameVersion(LocalVersionInfo.game_info.version.ToString());
                var onlineGameVersion = new GameVersion(miHoYoVersionInfo.cur_version.ToString());
                if(onlineGameVersion.IsDifferentThan(LocalGameVersion))
                {
                    if(miHoYoVersionInfo.patch_list[LocalGameVersion.ToString()] != null)
                    {
                        return 2;
                    }
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }

        private void DownloadLauncherUpdate()
        {
            try
            {
                var webRequest = BpUtility.CreateWebRequest(OnlineVersionInfo.launcher_info.url.ToString());
                using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    using(var data = new MemoryStream())
                    {
                        webResponse.GetResponseStream().CopyTo(data);
                        data.Seek(0, SeekOrigin.Begin);
                        using(FileStream file = new FileStream(LauncherArchivePath, FileMode.Create))
                        {
                            data.CopyTo(file);
                            file.Flush();
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download launcher update:\n{ex}", true, 1);
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

        private void DownloadBackgroundImage()
        {
            string backgroundImageName = miHoYoVersionInfo.bg_file_name.ToString();
            string backgroundImagePath = Path.Combine(LauncherDataPath, backgroundImageName);
            if(File.Exists(backgroundImagePath))
            {
                Log($"Background image {backgroundImageName} exists, using it");
                Dispatcher.Invoke(() => {BackgroundImage.Source = new BitmapImage(new Uri(backgroundImagePath));});
            }
            else
            {
                Log($"Background image {backgroundImageName} doesn't exist, downloading...");
                try
                {
                    var webClient = new BpWebClient();
                    Directory.CreateDirectory(LauncherDataPath);
                    webClient.DownloadFile(new Uri($"{miHoYoVersionInfo.download_url.ToString()}/{miHoYoVersionInfo.bg_file_name.ToString()}"), backgroundImagePath);
                    Dispatcher.Invoke(() => {BackgroundImage.Source = new BitmapImage(new Uri(backgroundImagePath));});
                    Log("success!", false);
                }
                catch(Exception ex)
                {
                    Log($"ERROR: Failed to download background image:\n{ex}", true, 1);
                }
            }
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
                if(Mirror == HI3Mirror.miHoYo)
                {
                    title = GameArchiveName;
                    time = -1;
                    url = $"{miHoYoVersionInfo.download_url.ToString()}/{GameArchiveName}";
                    if(PatchDownload)
                        md5 = miHoYoVersionInfo.patch_list[LocalVersionInfo.game_info.version.ToString()].md5.ToString();
                    else
                        md5 = miHoYoVersionInfo.full_version_file.md5.ToString();
                }
                else if(Mirror == HI3Mirror.MediaFire)
                {
                    dynamic mediafire_metadata;
                    if(Server == HI3Server.Global)
                        mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.global.id.ToString(), false);
                    else
                        mediafire_metadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_archive.os.id.ToString(), false);
                    if(mediafire_metadata == null)
                        return;
                    title = mediafire_metadata.title.ToString();
                    time = ((DateTimeOffset)mediafire_metadata.modifiedDate).ToUnixTimeSeconds();
                    url = mediafire_metadata.downloadUrl.ToString();
                    md5 = mediafire_metadata.md5Checksum.ToString();
                    GameArchivePath = Path.Combine(GameInstallPath, title);
                    if(!mediafire_metadata.title.Contains(miHoYoVersionInfo.cur_version.ToString().Substring(0, 5)))
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Mirror is outdated!", true, 1);
                        MessageBox.Show(textStrings["msgbox_gamedownloadmirrorold_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                    try
                    {
                        var webRequest = BpUtility.CreateWebRequest(url);
                        var webResponse = (HttpWebResponse)webRequest.GetResponse();
                    }
                    catch(WebException ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to download from MediaFire:\n{ex}", true, 1);
                        MessageBox.Show(textStrings["msgbox_gamedownloadmirrorerror_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }
                else
                {
                    dynamic gd_metadata;
                    if(Server == HI3Server.Global)
                        gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.global.ToString());
                    else
                        gd_metadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_archive.os.ToString());
                    if(gd_metadata == null)
                        return;
                    title = gd_metadata.title.ToString();
                    time = ((DateTimeOffset)gd_metadata.modifiedDate).ToUnixTimeSeconds();
                    url = gd_metadata.downloadUrl.ToString();
                    md5 = gd_metadata.md5Checksum.ToString();
                    GameArchivePath = Path.Combine(GameInstallPath, title);
                    if(DateTime.Compare(DateTime.Parse(miHoYoVersionInfo.last_modified.ToString()), DateTime.Parse(gd_metadata.modifiedDate.ToString())) > 0)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Mirror is outdated!", true, 1);
                        MessageBox.Show(textStrings["msgbox_gamedownloadmirrorold_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                    try
                    {
                        var webRequest = BpUtility.CreateWebRequest(url);
                        var webResponse = (HttpWebResponse)webRequest.GetResponse();
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
                                string msg;
                                if(json.error != null)
                                    msg = json.error.errors[0].message;
                                else
                                    msg = ex.Message;
                                Log($"ERROR: Failed to download from Google Drive:\n{msg}", true, 1);
                                MessageBox.Show(textStrings["msgbox_gamedownloadmirrorerror_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                                Status = LauncherStatus.Ready;
                            }
                        }
                        return;
                    }
                }

                Log($"Starting to download game archive: {title} ({url})");
                Status = LauncherStatus.Downloading;
                await Task.Run(() =>
                {
                    tracker.NewFile();
                    var eta_calc = new ETACalculator(1, 1);
                    download = new DownloadPauseable(url, GameArchivePath);
                    download.Start();
                    Dispatcher.Invoke(() =>
                    {
                        LaunchButton.IsEnabled = true;
                        LaunchButton.Content = textStrings["button_pause"];
                    });
                    while(download != null && !download.Done)
                    {
                        if(DownloadPaused)
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
                    if(download == null)
                    {
                        abort = true;
                        return;
                    }
                    download = null;
                    Log("Successfully downloaded game archive");
                    while(BpUtility.IsFileLocked(new FileInfo(GameArchivePath)))
                        Thread.Sleep(10);
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = string.Empty;
                        LaunchButton.Content = textStrings["button_launch"];
                    });
                });
                try
                {
                    if(abort)
                        return;
                    await Task.Run(() =>
                    {
                        Log("Validating game archive...");
                        Status = LauncherStatus.Verifying;
                        string actual_md5 = BpUtility.CalculateMD5(GameArchivePath);
                        if(actual_md5 != md5.ToUpper())
                        {
                            Status = LauncherStatus.Error;
                            Log($"ERROR: Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
                            Dispatcher.Invoke(() =>
                            {
                                if(MessageBox.Show(textStrings["msgbox_verifyerror_2_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                                {
                                    DeleteFile(GameArchivePath);
                                    abort = true;
                                    Status = LauncherStatus.Ready;
                                    GameUpdateCheck();
                                }
                            });
                        }
                        else
                        {
                            Log("success!", false);
                        }
                        if(abort)
                            return;
                        if(!PatchDownload)
                        {
                            try
                            {
                                foreach(var file in Directory.GetFiles(Path.Combine(GameInstallPath, @"BH3_Data\StreamingAssets\Asb\pc"), "*.wmv"))
                                {
                                    DeleteFile(file);
                                }
                            }catch{}
                        }
                        var skippedFiles = new List<string>();
                        using(var archive = ArchiveFactory.Open(GameArchivePath))
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
                                        var progress = (unpackedFiles + 1f) / fileCount;
                                        ProgressBar.Value = progress;
                                        TaskbarItemInfo.ProgressValue = progress;
                                    });
                                    reader.WriteEntryToDirectory(GameInstallPath, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
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
                            DeleteFile(GameArchivePath);
                            throw new ArchiveException("Game archive is corrupt");
                        }
                        Log("success!", false);
                        DeleteFile(GameArchivePath);
                        Dispatcher.Invoke(() => 
                        {
                            PatchDownload = false;
                            WriteVersionInfo(false, true);
                            Log("Successfully installed the game");
                            GameUpdateCheck();
                        });
                        if(time != -1)
                            SendStatistics(title, time);
                    });
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install the game:\n{ex}", true, 1);
                    Dispatcher.Invoke(() =>
                    {
                        if(MessageBox.Show(textStrings["msgbox_installerror_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                        {
                            Status = LauncherStatus.Ready;
                            GameUpdateCheck();
                        }
                    });
                }
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download the game:\n{ex}", true, 1);
                if(MessageBox.Show(textStrings["msgbox_gamedownloaderror_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    GameUpdateCheck();
                }
            }
        }

        private void WriteVersionInfo(bool CheckForLocalVersion = false, bool IsInstalled = false)
        {
            try
            {
                var config_ini_file = Path.Combine(GameInstallPath, "config.ini");
                var ini_parser = new FileIniDataParser();
                ini_parser.Parser.Configuration.AssigmentSpacer = string.Empty;
                dynamic versionInfo = new ExpandoObject();
                versionInfo.game_info = new ExpandoObject();
                if(!PatchDownload)
                    versionInfo.game_info.version = miHoYoVersionInfo.cur_version.ToString();
                else
                    versionInfo.game_info.version = LocalVersionInfo.game_info.version.ToString();
                versionInfo.game_info.install_path = GameInstallPath;
                versionInfo.game_info.installed = IsInstalled;

                if(GameInstallPath.Length < 4)
                {
                    throw new Exception("Install path can't be on a root drive");
                }
                if(CheckForLocalVersion)
                {
                    var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                    if(File.Exists(config_ini_file))
                    {
                        var data = ini_parser.ReadFile(config_ini_file);
                        if(data["General"]["game_version"].Length == 17)
                            versionInfo.game_info.version = data["General"]["game_version"];
                    }
                    else if(LauncherRegKey.GetValue(RegistryVersionInfo) == null && (key != null && key.GetValue(GameRegistryLocalVersionRegValue) != null && key.GetValueKind(GameRegistryLocalVersionRegValue) == RegistryValueKind.Binary))
                    {
                        var version = Encoding.UTF8.GetString((byte[])key.GetValue(GameRegistryLocalVersionRegValue)).TrimEnd('\u0000');
                        if(!miHoYoVersionInfo.cur_version.ToString().Contains(version))
                            versionInfo.game_info.version = $"{version}_xxxxxxxxxx";
                    }
                    else
                    {
                        if(MessageBox.Show(textStrings["msgbox_install_existing_no_local_version_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        {
                            versionInfo.game_info.version = "0.0.0_xxxxxxxxxx";
                        }
                    }
                    if(key != null)
                        key.Close();
                }
                Log("Writing game version info...");
                LauncherRegKey.SetValue(RegistryVersionInfo, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(versionInfo)), RegistryValueKind.Binary);
                LauncherRegKey.Close();
                LauncherRegKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Bp\Better HI3 Launcher", true);
                if(IsInstalled)
                {
                    if(File.Exists(config_ini_file))
                    {
                        var data = ini_parser.ReadFile(config_ini_file);
                        data["General"]["game_version"] = versionInfo.game_info.version;
                        ini_parser.WriteFile(config_ini_file, data);
                    }
                }
                Log("success!", false);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to write version info:\n{ex}", true, 1);
                MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteGameFiles(bool DeleteGame = false)
        {
            if(DeleteGame)
            {
                if(Directory.Exists(GameInstallPath))
                    Directory.Delete(GameInstallPath, true);
            }
            try{LauncherRegKey.DeleteValue(RegistryVersionInfo);}catch{}
            Dispatcher.Invoke(() => {LaunchButton.Content = textStrings["button_download"];});
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
                if(FullCache)
                {
                    title = GameCacheMetadata.title.ToString();
                    time = ((DateTimeOffset)GameCacheMetadata.modifiedDate).ToUnixTimeSeconds();
                    url = GameCacheMetadata.downloadUrl.ToString();
                    md5 = GameCacheMetadata.md5Checksum.ToString();
                }
                else
                {
                    title = GameCacheMetadataNumeric.title.ToString();
                    time = ((DateTimeOffset)GameCacheMetadataNumeric.modifiedDate).ToUnixTimeSeconds();
                    url = GameCacheMetadataNumeric.downloadUrl.ToString();
                    md5 = GameCacheMetadataNumeric.md5Checksum.ToString();
                }
                CacheArchivePath = Path.Combine(miHoYoPath, title);

                var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(CacheArchivePath) && x.IsReady).FirstOrDefault();
                if(gameInstallDrive == null)
                {
                    MessageBox.Show(textStrings["msgbox_install_wrong_drive_type_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else if(gameInstallDrive.TotalFreeSpace < 2147483648)
                {
                    if(MessageBox.Show(textStrings["msgbox_install_little_space_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                try
                {
                    var webRequest = BpUtility.CreateWebRequest(url);
                    var webResponse = (HttpWebResponse)webRequest.GetResponse();
                }
                catch(WebException ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to download cache from mirror:\n{ex}", true, 1);
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
                    var download = new DownloadPauseable(url, CacheArchivePath);
                    download.Start();
                    while(!download.Done)
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
                    Log("Successfully downloaded game cache");
                    while(BpUtility.IsFileLocked(new FileInfo(CacheArchivePath)))
                        Thread.Sleep(10);
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = string.Empty;
                        LaunchButton.Content = textStrings["button_launch"];
                    });
                });
                try
                {
                    if(abort)
                        return;
                    await Task.Run(() =>
                    {
                        Log("Validating game cache...");
                        Status = LauncherStatus.Verifying;
                        string actual_md5 = BpUtility.CalculateMD5(CacheArchivePath);
                        if(actual_md5 != md5.ToUpper())
                        {
                            Status = LauncherStatus.Error;
                            Log($"ERROR: Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
                            Dispatcher.Invoke(() =>
                            {
                                if(MessageBox.Show(textStrings["msgbox_verifyerror_2_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                                {
                                    DeleteFile(CacheArchivePath);
                                    abort = true;
                                    Status = LauncherStatus.Ready;
                                    GameUpdateCheck();
                                }
                            });
                        }
                        else
                        {
                            Log("success!", false);
                        }
                        if(abort)
                            return;
                        try
                        {
                            foreach(var file in Directory.GetFiles(Path.Combine(miHoYoPath, $@"{GameFullName}\Data\data"), "*.unity3d"))
                            {
                                DeleteFile(file);
                            }
                        }catch{}
                        var skippedFiles = new List<string>();
                        using(var archive = ArchiveFactory.Open(CacheArchivePath))
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
                            Directory.CreateDirectory(miHoYoPath);
                            var reader = archive.ExtractAllEntries();
                            while(reader.MoveToNextEntry())
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
                                    reader.WriteEntryToDirectory(miHoYoPath, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
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
                            throw new ArchiveException("Cache archive is corrupt");
                        }
                        Log("success!", false);
                        DeleteFile(CacheArchivePath);
                        SendStatistics(title, time);
                    });
                }
                catch(Exception ex)
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to install game cache:\n{ex}", true, 1);
                    MessageBox.Show(textStrings["msgbox_installerror_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to download game cache:\n{ex}", true, 1);
                MessageBox.Show(textStrings["msgbox_gamedownloaderror_msg"], textStrings["msgbox_gamedownloaderror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Dispatcher.Invoke(() => {LaunchButton.Content = textStrings["button_launch"];});
            Status = LauncherStatus.Ready;
        }

        private void SendStatistics(string file, long time)
        {
            if(string.IsNullOrEmpty(file))
                throw new ArgumentNullException();

            string server = (int)Server == 0 ? "global" : "os";
            string mirror = (int)Mirror == 2 ? "gd" : "mediafire";
            try
            {
                var data = Encoding.ASCII.GetBytes($"save_stats={server}&mirror={mirror}&file={file}&time={time}");
                var webRequest = BpUtility.CreateWebRequest(OnlineVersionInfo.launcher_info.stat_url.ToString(), "POST", 3000);
                webRequest.ContentType = "application/x-www-form-urlencoded";
                webRequest.ContentLength = data.Length;
                using(var stream = webRequest.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                using(var webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    var responseData = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();
                    if(!string.IsNullOrEmpty(responseData))
                    {
                        var json = JsonConvert.DeserializeObject<dynamic>(responseData);
                        if(json.status != "success")
                        {
                            Log($"WARNING: Failed to send download stat of {file}", true, 2);
                        }
                    }
                }
            }
            catch
            {
                Log($"WARNING: Failed to send download stat of {file}", true, 2);
            }
        }

        private async void Window_ContentRendered(object sender, EventArgs e)
        {
            #if DEBUG
                DisableAutoUpdate = true;
            #endif
            try
            {
                string exeName = Process.GetCurrentProcess().MainModule.ModuleName;
                string oldExeName = $"{Path.GetFileNameWithoutExtension(LauncherPath)}_old.exe";
                bool needsUpdate = LauncherUpdateCheck();

                if(Process.GetCurrentProcess().MainModule.ModuleName != LauncherExeName)
                {
                    Status = LauncherStatus.Error;
                    DeleteFile(LauncherPath, true);
                    File.Move(Path.Combine(RootPath, exeName), LauncherPath);
                    BpUtility.StartProcess(LauncherExeName, string.Join(" ", CommandLineArgs), RootPath, true);
                    Dispatcher.Invoke(() => {Application.Current.Shutdown();});
                    return;
                }
                DeleteFile(Path.Combine(RootPath, oldExeName), true);
                await Task.Run(() =>
                {
                    if(DisableAutoUpdate)
                        return;
                    if(needsUpdate)
                    {
                        Log("A newer version of the launcher is available!");
                        Log("Downloading update...");
                        Status = LauncherStatus.Working;
                        Dispatcher.Invoke(() => {ProgressText.Text = textStrings["progresstext_updating_launcher"];});
                        DownloadLauncherUpdate();
                        Log("success!", false);
                        Log("Validating update...");
                        string md5 = OnlineVersionInfo.launcher_info.md5.ToString().ToUpper();
                        string actual_md5 = BpUtility.CalculateMD5(LauncherArchivePath);
                        if(actual_md5 != md5)
                        {
                            Status = LauncherStatus.Error;
                            Log($"ERROR: Validation failed. Expected MD5: {md5}, got MD5: {actual_md5}", true, 1);
                            DeleteFile(LauncherArchivePath, true);
                            if(MessageBox.Show(textStrings["msgbox_verifyerror_1_msg"], textStrings["msgbox_verifyerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                            {
                                Dispatcher.Invoke(() => {Application.Current.Shutdown();});
                                return;
                            }
                        }
                        Log("success!", false);
                        Log("Performing update...");
                        File.Move(Path.Combine(RootPath, exeName), Path.Combine(RootPath, oldExeName));
                        using(var archive = ArchiveFactory.Open(LauncherArchivePath))
                        {
                            var reader = archive.ExtractAllEntries();
                            while(reader.MoveToNextEntry())
                            {
                                reader.WriteEntryToDirectory(RootPath, new ExtractionOptions(){ExtractFullPath = true, Overwrite = true, PreserveFileTime = true});
                            }
                        }
                        Log("success!", false);
                        BpUtility.StartProcess(LauncherExeName, string.Join(" ", CommandLineArgs), RootPath, true);
                        Dispatcher.Invoke(() => {Application.Current.Shutdown();});
                        return;
                    }
                    else
                    {
                        DeleteFile(LauncherArchivePath, true);
                        DeleteFile(Path.Combine(RootPath, "BetterHI3Launcher.7z"), true); // legacy name
                        if(!File.Exists(LauncherPath))
                        {
                            File.Copy(Path.Combine(RootPath, exeName), LauncherPath, true);
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

            if(FirstLaunch)
            {
                IntroBox.Visibility = Visibility.Visible;
            }
            if(LauncherRegKey != null && LauncherRegKey.GetValue("LauncherVersion") != null && LauncherRegKey.GetValue("LauncherVersion").ToString() != LocalLauncherVersion.ToString())
            {
                ChangelogBox.Visibility = Visibility.Visible;
                ChangelogBoxMessageTextBlock.Visibility = Visibility.Visible;
                ChangelogBoxScrollViewer.Height -= 20;
                FetchChangelog();
            }
            try
            {
                if(LauncherRegKey.GetValue("LauncherVersion") == null || LauncherRegKey.GetValue("LauncherVersion") != null && LauncherRegKey.GetValue("LauncherVersion").ToString() != LocalLauncherVersion.ToString())
                    LauncherRegKey.SetValue("LauncherVersion", LocalLauncherVersion);
                // legacy values
                if(LauncherRegKey.GetValue("RanOnce") != null)
                    LauncherRegKey.DeleteValue("RanOnce");
                if(LauncherRegKey.GetValue("BackgroundImageName") != null)
                    LauncherRegKey.DeleteValue("BackgroundImageName");
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to write critical registry info:\n{ex}", true, 1);
                if(MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                    return;
                }
            }
            if(!FirstLaunch)
                GameUpdateCheck();
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
            if(Status == LauncherStatus.Ready)
            {
                if(DownloadPaused)
                {
                    DownloadPaused = false;
                    await DownloadGameFile();
                    return;
                }

                if(LocalVersionInfo != null)
                {
                    if(!File.Exists(GameExePath))
                    {
                        MessageBox.Show(textStrings["msgbox_noexe_msg"], textStrings["msgbox_noexe_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    try
                    {
                        var processes = Process.GetProcessesByName("BH3");
                        if(processes.Length > 0)
                        {
                            processes[0].EnableRaisingEvents = true;
                            processes[0].Exited += new EventHandler((object s, EventArgs ea) => {OnGameExit();});
                            Status = LauncherStatus.Running;
                            return;
                        }
                        var startInfo = new ProcessStartInfo(GameExePath);
                        startInfo.WorkingDirectory = GameInstallPath;
                        startInfo.UseShellExecute = true;
                        var process = Process.Start(startInfo);
                        process.EnableRaisingEvents = true;
                        process.Exited += new EventHandler((object s1, EventArgs ea1) =>
                        {
                            processes = Process.GetProcessesByName("BH3");
                            if(processes.Length > 0)
                            {
                                processes[0].EnableRaisingEvents = true;
                                processes[0].Exited += new EventHandler((object s2, EventArgs ea2) => {OnGameExit();});
                            }
                            else
                            {
                                OnGameExit();
                            }
                        });
                        Status = LauncherStatus.Running;
                        WindowState = WindowState.Minimized;
                    }
                    catch(Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to start the game:\n{ex}", true, 1);
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

                            if(dialog.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                GameInstallPath = Path.Combine(dialog.FileName, GameFullName);
                            }
                            else
                            {
                                GameInstallPath = null;
                            }

                            if(string.IsNullOrEmpty(GameInstallPath))
                            {
                                return string.Empty;
                            }
                            else
                            {
                                var path = CheckForExistingGameDirectory(GameInstallPath);
                                if(path.Length < 4)
                                {
                                    path = string.Empty;
                                }
                                if(!string.IsNullOrEmpty(path))
                                {
                                    if(MessageBox.Show(string.Format(textStrings["msgbox_installexisting_msg"], path), textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                                    {
                                        Log($"Existing install directory selected: {path}");
                                        GameInstallPath = path;
                                        var server = CheckForExistingGameClientServer();
                                        if(server >= 0)
                                        {
                                            if((int)Server != server)
                                                ServerDropdown.SelectedIndex = server;
                                            WriteVersionInfo(true, true);
                                            GameUpdateCheck();
                                        }
                                        else
                                        {
                                            Status = LauncherStatus.Error;
                                            Log($"ERROR: Directory {GameInstallPath} doesn't contain a valid installation of the game. This launcher only supports Global and SEA clients.", true, 1);
                                            if(MessageBox.Show(string.Format(textStrings["msgbox_installexistinginvalid_msg"]), textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                                            {
                                                Status = LauncherStatus.Ready;
                                            }
                                        }
                                    }
                                    return string.Empty;
                                }
                                return GameInstallPath;
                            }
                        }
                        if(string.IsNullOrEmpty(SelectGameInstallDirectory()))
                            return;
                        while(MessageBox.Show(string.Format(textStrings["msgbox_install_msg"], GameInstallPath), textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                        {
                            if(string.IsNullOrEmpty(SelectGameInstallDirectory()))
                                return;
                        }
                        var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(GameInstallPath) && x.IsReady).FirstOrDefault();
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
                        Directory.CreateDirectory(GameInstallPath);
                        GameArchivePath = Path.Combine(GameInstallPath, GameArchiveName);
                        GameExePath = Path.Combine(GameInstallPath, "BH3.exe");
                        Log($"Install dir selected: {GameInstallPath}");
                        await DownloadGameFile();
                    }
                    catch(Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to select game install directory:\n{ex}", true, 1);
                        MessageBox.Show(string.Format(textStrings["msgbox_installdirerror_msg"], ex), textStrings["msgbox_installdirerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }
            }
            else if(Status == LauncherStatus.UpdateAvailable)
            {
                var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(GameInstallPath) && x.IsReady).FirstOrDefault();
                if(gameInstallDrive.TotalFreeSpace < 24696061952)
                {
                    if(MessageBox.Show(textStrings["msgbox_install_little_space_msg"], textStrings["msgbox_install_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                if(!PatchDownload)
                    Directory.CreateDirectory(GameInstallPath);
                await DownloadGameFile();
            }
            else if(Status == LauncherStatus.Downloading || Status == LauncherStatus.DownloadPaused)
            {
                if(!DownloadPaused)
                {
                    download.Pause();
                    Status = LauncherStatus.DownloadPaused;
                    LaunchButton.Content = textStrings["button_resume"];
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
                }
                else
                {
                    Status = LauncherStatus.Downloading;
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
            if(Status != LauncherStatus.Ready)
                return;

            Status = LauncherStatus.CheckingUpdates;
            Dispatcher.Invoke(() => {ProgressText.Text = textStrings["progresstext_mirror_connect"];});
            Log("Fetching mirror data...");
            try
            {
                await Task.Run(() =>
                {
                    FetchOnlineVersionInfo();
                    if(Server == HI3Server.Global)
                    {
                        if(Mirror == HI3Mirror.GoogleDrive)
                        {
                            GameCacheMetadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_cache.global.ToString());
                            if(GameCacheMetadata != null)
                                GameCacheMetadataNumeric = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_cache_numeric.global.ToString());
                        }
                        else
                        {
                            GameCacheMetadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache.global.id.ToString(), false);
                            if(GameCacheMetadata != null)
                                GameCacheMetadataNumeric = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.global.id.ToString(), true);
                        }
                    }
                    else
                    {
                        if(Mirror == HI3Mirror.GoogleDrive)
                        {
                            GameCacheMetadata = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_cache.os.ToString());
                            if(GameCacheMetadata != null)
                                GameCacheMetadataNumeric = FetchGDFileMetadata(OnlineVersionInfo.game_info.mirror.gd.game_cache_numeric.os.ToString());
                        }
                        else
                        {
                            GameCacheMetadata = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache.os.id.ToString(), false);
                            if(GameCacheMetadata != null)
                                GameCacheMetadataNumeric = FetchMediaFireFileMetadata(OnlineVersionInfo.game_info.mirror.mediafire.game_cache_numeric.os.id.ToString(), true);
                        }
                    }
                });
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to fetch cache metadata:\n{ex}", true, 1);
                Dispatcher.Invoke(() => {MessageBox.Show(string.Format(textStrings["msgbox_mirror_error_msg"], ex), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
                Status = LauncherStatus.Ready;
                return;
            }
            if(GameCacheMetadata == null || GameCacheMetadataNumeric == null)
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
                if(Mirror == HI3Mirror.GoogleDrive)
                {
                    mirror = "Google Drive";
                    time = GameCacheMetadataNumeric.modifiedDate.ToString();
                }
                else
                {
                    mirror = "MediaFire";
                    time = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((double)OnlineVersionInfo.game_info.mirror.mediafire.last_updated).ToString();
                }
                if(DateTime.Compare(FetchmiHoYoResourceVersionDateModified(), DateTime.Parse(time)) >= 0)
                    last_updated = $"{DateTime.Parse(time).ToLocalTime()} ({textStrings["outdated"].ToLower()})";
                else
                    last_updated = DateTime.Parse(time).ToLocalTime().ToString();
                DownloadCacheBoxMessageTextBlock.Text = string.Format(textStrings["downloadcachebox_msg"], mirror, last_updated, OnlineVersionInfo.game_info.mirror.maintainer.ToString());
                Log("success!", false);
                Status = LauncherStatus.Ready;
            });
        }

        private async Task CM_Repair_Click(object sender, RoutedEventArgs e)
        {
            if(Status != LauncherStatus.Ready)
                return;

            Status = LauncherStatus.CheckingUpdates;
            Dispatcher.Invoke(() => {ProgressText.Text = textStrings["progresstext_fetching_hashes"];});
            Log("Fetching repair data...");
            try
            {
                string server = (int)Server == 0 ? "global" : "os";
                var webClient = new BpWebClient();
                await Task.Run(() =>
                {
                    OnlineRepairInfo = JsonConvert.DeserializeObject<dynamic>(webClient.DownloadString($"https://bpnet.host/bh3?launcher_repair={server}"));
                });
                if(OnlineRepairInfo.status == "success")
                {
                    OnlineRepairInfo = OnlineRepairInfo.repair_info;
                    if(OnlineRepairInfo.game_version != LocalVersionInfo.game_info.version && !AdvancedFeatures)
                    {
                        ProgressText.Text = string.Empty;
                        ProgressBar.Visibility = Visibility.Hidden;
                        MessageBox.Show(textStrings["msgbox_repair_1_msg"], textStrings["contextmenu_repair"], MessageBoxButton.OK, MessageBoxImage.Warning);
                        Status = LauncherStatus.Ready;
                        return;
                    }
                }
                else
                {
                    Status = LauncherStatus.Error;
                    Log($"ERROR: Failed to fetch repair data: {OnlineRepairInfo.status_message}", true, 1);
                    MessageBox.Show(string.Format(textStrings["msgbox_neterror_msg"], OnlineRepairInfo.status_message), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to fetch repair data:\n{ex}", true, 1);
                Dispatcher.Invoke(() => {MessageBox.Show(string.Format(textStrings["msgbox_neterror_msg"], ex), textStrings["msgbox_neterror_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
                Status = LauncherStatus.Ready;
                return;
            }
            Dispatcher.Invoke(() =>
            {
                RepairBox.Visibility = Visibility.Visible;
                RepairBoxMessageTextBlock.Text = string.Format(textStrings["repairbox_msg"], OnlineRepairInfo.mirrors, OnlineVersionInfo.game_info.mirror.maintainer.ToString());
                Log("success!", false);
                Status = LauncherStatus.Ready;
            });
        }

        private async Task CM_Move_Click(object sender, RoutedEventArgs e)
        {
            if(Status != LauncherStatus.Ready)
                return;
            if(!Directory.Exists(GameInstallPath))
            {
                MessageBox.Show(textStrings["msgbox_nodir_msg"], textStrings["msgbox_nodir_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if(RootPath.Contains(GameInstallPath))
            {
                MessageBox.Show(textStrings["msgbox_move_3_msg"], textStrings["msgbox_move_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                string path = Path.Combine(dialog.FileName, GameFullName);
                if(!path.Contains(GameInstallPath))
                {
                    var gameInstallDrive = DriveInfo.GetDrives().Where(x => x.Name == Path.GetPathRoot(path) && x.IsReady).FirstOrDefault();
                    if(gameInstallDrive == null)
                    {
                        MessageBox.Show(textStrings["msgbox_move_wrong_drive_type_msg"], textStrings["msgbox_move_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    else if(gameInstallDrive.TotalFreeSpace < 24696061952)
                    {
                        if(MessageBox.Show(textStrings["msgbox_move_little_space_msg"], textStrings["msgbox_move_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                            return;
                    }
                    if(MessageBox.Show(string.Format(textStrings["msgbox_move_1_msg"], path), textStrings["msgbox_move_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        Status = LauncherStatus.Working;
                        ProgressText.Text = textStrings["progresstext_moving_files"];
                        Log($"Moving game files to: {path}");
                        await Task.Run(() =>
                        {
                            try
                            {
                                if(Directory.GetDirectoryRoot(GameInstallPath) == Directory.GetDirectoryRoot(path))
                                {
                                    Directory.Move(GameInstallPath, path);
                                }
                                else
                                {
                                    Directory.CreateDirectory(path);
                                    Directory.SetCreationTime(path, Directory.GetCreationTime(GameInstallPath));
                                    Directory.SetLastWriteTime(path, Directory.GetLastWriteTime(GameInstallPath));
                                    string[] files = Directory.GetFiles(GameInstallPath);
                                    foreach(string file in files)
                                    {
                                        string name = Path.GetFileName(file);
                                        string dest = Path.Combine(path, name);
                                        new FileInfo(file).Attributes &= ~FileAttributes.ReadOnly;
                                        File.Copy(file, dest, true);
                                        File.SetCreationTime(dest, File.GetCreationTime(file));
                                    }
                                    string[] dirs = Directory.GetDirectories(GameInstallPath, "*", SearchOption.AllDirectories);
                                    foreach(string dir in dirs)
                                    {
                                        string name = dir.Replace(GameInstallPath, string.Empty);
                                        string dest = $"{path}{name}";
                                        new DirectoryInfo(dir).Attributes &= ~FileAttributes.ReadOnly;
                                        Directory.CreateDirectory(dest);
                                        Directory.SetCreationTime(dest, Directory.GetCreationTime(dir));
                                        Directory.SetLastWriteTime(dest, Directory.GetLastWriteTime(dir));
                                        string[] nestedFiles = Directory.GetFiles(dir);
                                        foreach(string nestedFile in nestedFiles)
                                        {
                                            string nestedName = Path.GetFileName(nestedFile);
                                            string nestedDest = Path.Combine(dest, nestedName);
                                            new FileInfo(nestedFile).Attributes &= ~FileAttributes.ReadOnly;
                                            File.Copy(nestedFile, nestedDest, true);
                                            File.SetCreationTime(nestedDest, File.GetCreationTime(nestedFile));
                                        }
                                    }
                                    try
                                    {
                                        new DirectoryInfo(GameInstallPath).Attributes &= ~FileAttributes.ReadOnly;
                                        Directory.Delete(GameInstallPath, true);
                                    }
                                    catch
                                    {
                                        Log($"WARNING: Failed to delete old game directory, you may want to do it manually: {GameInstallPath}", true, 2);
                                    }
                                }
                                GameInstallPath = path;
                                WriteVersionInfo(false, true);
                                Log("Successfully moved game files");
                                GameUpdateCheck();
                            }
                            catch(Exception ex)
                            {
                                Status = LauncherStatus.Error;
                                Log($"ERROR: Failed to move the game:\n{ex}", true, 1);
                                Dispatcher.Invoke(() => {MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_move_error_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
                                Status = LauncherStatus.Ready;
                                return;
                            }
                        });
                    }
                }
                else
                {
                    MessageBox.Show(textStrings["msgbox_move_2_msg"], textStrings["msgbox_move_title"], MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task CM_Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if((Status == LauncherStatus.Ready || Status == LauncherStatus.UpdateAvailable || Status == LauncherStatus.DownloadPaused) && !string.IsNullOrEmpty(GameInstallPath))
            {
                if(RootPath.Contains(GameInstallPath))
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
                        Dispatcher.Invoke(() =>
                        {
                            if(MessageBox.Show(textStrings["msgbox_uninstall_3_msg"], textStrings["msgbox_uninstall_title"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                            {
                                string path = Path.Combine(miHoYoPath, GameFullName);
                                Log("Deleting game cache and registry settings...");
                                if(Directory.Exists(path))
                                    Directory.Delete(path, true);
                                var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                                if(key != null)
                                {
                                    Registry.CurrentUser.DeleteSubKeyTree(GameRegistryPath, true);
                                    key.Close();
                                }
                            }
                        });
                        Log("Sucessfully uninstalled the game");
                        GameUpdateCheck();
                    }
                    catch(Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR: Failed to uninstall the game:\n{ex}", true, 1);
                        Dispatcher.Invoke(() => {MessageBox.Show(string.Format(textStrings["msgbox_uninstallerror_msg"], ex), textStrings["msgbox_uninstallerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);});
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
                var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                string value = "GENERAL_DATA_V2_ResourceDownloadType_h2238376574";
                if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.DWord)
                {
                    if(key.GetValue(value) != null)
                        key.DeleteValue(value);
                    MessageBox.Show($"{textStrings["msgbox_registryempty_1_msg"]}\n{textStrings["msgbox_registryempty_2_msg"]}", textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var valueBefore = key.GetValue(value);
                int valueAfter;
                if((int)valueBefore != 0)
                {
                    valueAfter = 0;
                }
                else
                {
                    MessageBox.Show(textStrings["msgbox_download_type_3_msg"], textStrings["contextmenu_download_type"], MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                key.SetValue(value, valueAfter, RegistryValueKind.DWord);
                key.Close();
                Log($"Changed ResourceDownloadType from {valueBefore} to {valueAfter}");
                MessageBox.Show(textStrings["msgbox_download_type_2_msg"], textStrings["contextmenu_download_type"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}", true, 1);
                if(MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
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
                Log("Starting to fix subtitles...");
                var GameVideoDirectory = Path.Combine(GameInstallPath, @"BH3_Data\StreamingAssets\Video");
                if(Directory.Exists(GameVideoDirectory))
                {
                    var SubtitleArchives = Directory.EnumerateFiles(GameVideoDirectory, "*.zip", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith(".zip", StringComparison.CurrentCultureIgnoreCase)).ToList();
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.IsIndeterminate = false;
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                    });
                    if(SubtitleArchives.Count > 0)
                    {
                        int filesUnpacked = 0;
                        await Task.Run(() =>
                        {
                            var skippedFiles = new List<string>();
                            var skippedFilePaths = new List<string>();
                            foreach(var SubtitleArchive in SubtitleArchives)
                            {
                                bool unpack_ok = true;
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressText.Text = string.Format(textStrings["msgbox_fixsubs_2_msg"], filesUnpacked + 1, SubtitleArchives.Count);
                                    var progress = (filesUnpacked + 1f) / SubtitleArchives.Count;
                                    ProgressBar.Value = progress;
                                    TaskbarItemInfo.ProgressValue = progress;
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
                                        }
                                        catch
                                        {
                                            unpack_ok = false;
                                            skippedFiles.Add($"{reader.Entry} ({Path.GetFileName(SubtitleArchive)})");
                                            skippedFilePaths.Add(SubtitleArchive);
                                            Log($"ERROR: Failed to unpack {SubtitleArchive} ({reader.Entry})", true, 1);
                                            if(reader.Entry.ToString() == "CG_09_mux_1_en.srt")
                                                Log("The above one line of error is normal, miHoYo somehow messed up the file");
                                        }
                                    }
                                }
                                if(unpack_ok)
                                    Log($"Unpacked {SubtitleArchive}");
                                File.SetAttributes(SubtitleArchive, File.GetAttributes(SubtitleArchive) & ~FileAttributes.ReadOnly);
                                if(!skippedFilePaths.Contains(SubtitleArchive))
                                {
                                    try
                                    {
                                        File.Delete(SubtitleArchive);
                                    }
                                    catch
                                    {
                                        Log($"WARNING: Failed to delete {SubtitleArchive}", true, 2);
                                    }
                                }
                                filesUnpacked++;
                            }
                            Dispatcher.Invoke(() =>
                            {
                                if(skippedFiles.Count > 0)
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
                    if(SubtitleFiles.Count > 0)
                    {
                        int subtitlesParsed = 0;
                        await Task.Run(() =>
                        {
                            foreach(var SubtitleFile in SubtitleFiles)
                            {
                                var subLines = File.ReadAllLines(SubtitleFile);
                                var subLinesToRemove = new List<int>();
                                bool subFixed = false;
                                int lineCount = subLines.Length;
                                int linesReplaced = 0;
                                int linesRemoved = 0;
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressText.Text = string.Format(textStrings["msgbox_fixsubs_3_msg"], subtitlesParsed + 1, SubtitleFiles.Count);
                                    var progress = (subtitlesParsed + 1f) / SubtitleFiles.Count;
                                    ProgressBar.Value = progress;
                                    TaskbarItemInfo.ProgressValue = progress;
                                });
                                File.SetAttributes(SubtitleFile, File.GetAttributes(SubtitleFile) & ~FileAttributes.ReadOnly);
                                if(new FileInfo(SubtitleFile).Length == 0)
                                {
                                    subtitlesParsed++;
                                    continue;
                                }
                                for(int atLine = 1; atLine < lineCount; atLine++)
                                {
                                    var line = File.ReadLines(SubtitleFile).Skip(atLine).Take(1).First();
                                    if(string.IsNullOrEmpty(line) || new Regex(@"^\d+$").IsMatch(line))
                                        continue;

                                    bool lineFixed = false;
                                    void LogLine()
                                    {
                                        if(lineFixed)
                                            return;

                                        linesReplaced++;
                                        lineFixed = true;
                                        if(AdvancedFeatures)
                                            Log($"Fixed line {1 + atLine}: {line}");
                                    }

                                    if(line.Contains("-->"))
                                    {
                                        if(line.Contains("."))
                                        {
                                            subLines[atLine] = line.Replace(".", ",");
                                            LogLine();
                                        }
                                        if(line.Contains(" ,"))
                                        {
                                            subLines[atLine] = line.Replace(" ,", ",");
                                            LogLine();
                                        }
                                        if(line.Contains("  "))
                                        {
                                            subLines[atLine] = line.Replace("  ", " ");
                                            LogLine();
                                        }
                                        if(atLine + 1 < lineCount && string.IsNullOrEmpty(subLines[atLine + 1]))
                                        {
                                            subLinesToRemove.Add(atLine + 1);
                                        }
                                    }
                                    else
                                    {
                                        if(line.Contains(" ,"))
                                        {
                                            subLines[atLine] = line.Replace(" ,", ",");
                                            LogLine();
                                        }
                                    }
                                }
                                foreach(var line in subLinesToRemove)
                                {
                                    subLines = subLines.Where((source, index) => index != line - linesRemoved).ToArray();
                                    linesRemoved++;
                                }
                                if(linesReplaced > 0 || linesRemoved > 0)
                                {
                                    File.WriteAllLines(SubtitleFile, subLines);
                                    subFixed = true;
                                }
                                var subLine = File.ReadAllText(SubtitleFile);
                                if(subLine.Contains($"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}"))
                                {
                                    subLine = subLine.Replace($"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}", $"{Environment.NewLine}{Environment.NewLine}");
                                    File.WriteAllText(SubtitleFile, subLine);
                                    subFixed = true;
                                }
                                if(subFixed && !subsFixed.Contains(SubtitleFile))
                                {
                                    subsFixed.Add(SubtitleFile);
                                    Log($"Subtitle fixed: {SubtitleFile}");
                                }
                                subtitlesParsed++;
                            }
                        });
                        Log($"Parsed {subtitlesParsed} subtitles, fixed {subsFixed.Count} of them");
                    }
                    if(Server == HI3Server.Global)
                    {
                        ProgressBar.IsIndeterminate = true;
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
                        SubtitleFiles = Directory.EnumerateFiles(GameVideoDirectory, "*id.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("id.srt", StringComparison.CurrentCultureIgnoreCase)).ToList();
                        SubtitleFiles.AddRange(SubtitleFiles = Directory.EnumerateFiles(GameVideoDirectory, "*th.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("th.srt", StringComparison.CurrentCultureIgnoreCase)).ToList());
                        SubtitleFiles.AddRange(SubtitleFiles = Directory.EnumerateFiles(GameVideoDirectory, "*vn.srt", SearchOption.TopDirectoryOnly).Where(x => x.EndsWith("vn.srt", StringComparison.CurrentCultureIgnoreCase)).ToList());
                        if(SubtitleFiles.Count > 0)
                        {
                            int deletedSubs = 0;
                            await Task.Run(() =>
                            {
                                foreach(var SubtitleFile in SubtitleFiles)
                                {
                                    try
                                    {
                                        if(File.Exists(SubtitleFile))
                                            File.Delete(SubtitleFile);
                                        deletedSubs++;
                                    }
                                    catch
                                    {
                                        Log($"WARNING: Failed to delete {SubtitleFile}", true, 2);
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
                    Log("ERROR: No CG directory!", true, 1);
                    MessageBox.Show(string.Format(textStrings["msgbox_novideodir_msg"]), textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Status = LauncherStatus.Ready;
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR:\n{ex}", true, 1);
                if(MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
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
                var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
                string value = "GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411";
                if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.Binary)
                {
                    if(key.GetValue(value) != null)
                        key.DeleteValue(value);
                    MessageBox.Show($"{textStrings["msgbox_registryempty_1_msg"]}\n{textStrings["msgbox_registryempty_3_msg"]}", textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var valueBefore = key.GetValue(value);
                var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])valueBefore));
                if(json == null)
                {
                    MessageBox.Show($"{textStrings["msgbox_registryempty_1_msg"]}\n{textStrings["msgbox_registryempty_3_msg"]}", textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                key.Close();
                FPSInputBox.Visibility = Visibility.Visible;
                if(json.TargetFrameRateForInLevel != null)
                    CombatFPSInputBoxTextBox.Text = json.TargetFrameRateForInLevel;
                else
                    CombatFPSInputBoxTextBox.Text = "60";
                if(json.TargetFrameRateForOthers != null)
                    MenuFPSInputBoxTextBox.Text = json.TargetFrameRateForOthers;
                else
                    MenuFPSInputBoxTextBox.Text = "60";
                GameGraphicSettings = json;
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}", true, 1);
                if(MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void CM_CustomResolution_Click(object sender, RoutedEventArgs e)
        {
            if(Status != LauncherStatus.Ready)
                return;

            try
            {
                var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                string value = "GENERAL_DATA_V2_ScreenSettingData_h1916288658";
                if(key == null || key.GetValue(value) == null || key.GetValueKind(value) != RegistryValueKind.Binary)
                {
                    if(key.GetValue(value) != null)
                        key.DeleteValue(value);
                    MessageBox.Show($"{textStrings["msgbox_registryempty_1_msg"]}\n{textStrings["msgbox_registryempty_3_msg"]}", textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var valueBefore = key.GetValue(value);
                var json = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString((byte[])valueBefore));
                if(json == null)
                {
                    MessageBox.Show($"{textStrings["msgbox_registryempty_1_msg"]}\n{textStrings["msgbox_registryempty_3_msg"]}", textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                key.Close();
                ResolutionInputBox.Visibility = Visibility.Visible;

                if(json.width != null)
                    ResolutionInputBoxWidthTextBox.Text = json.width;
                else
                    ResolutionInputBoxWidthTextBox.Text = "720";
                if(json.height != null)
                    ResolutionInputBoxHeightTextBox.Text = json.height;
                else
                    ResolutionInputBoxHeightTextBox.Text = "480";
                if(json.isfullScreen != null)
                    ResolutionInputBoxFullscreenCheckbox.IsChecked = json.isfullScreen;
                else
                    ResolutionInputBoxFullscreenCheckbox.IsChecked = false;
                GameScreenSettings = json;
            }

            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}", true, 1);
                if(MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
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
                var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                if(key == null)
                {
                    Log("ERROR: No game registry key!", true, 1);
                    MessageBox.Show($"{textStrings["msgbox_registryempty_1_msg"]}\n{textStrings["msgbox_registryempty_2_msg"]}", textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                Registry.CurrentUser.DeleteSubKeyTree(GameRegistryPath, true);
                key.Close();
                Log("Successfully reset game settings");
                MessageBox.Show(textStrings["msgbox_resetgamesettings_3_msg"], textStrings["contextmenu_resetgamesettings"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to access registry:\n{ex}", true, 1);
                if(MessageBox.Show(textStrings["msgbox_registryerror_msg"], textStrings["msgbox_registryerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    Status = LauncherStatus.Ready;
                    return;
                }
            }
        }

        private void CM_Changelog_Click(object sender, RoutedEventArgs e)
        {
            ChangelogBox.Visibility = Visibility.Visible;
            ChangelogBoxScrollViewer.ScrollToHome();
            FetchChangelog();
        }

        private void CM_Language_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuItem;
            if(item.IsChecked)
                return;

            string lang = item.Header.ToString();
            string msg;
            if(LauncherLanguage != "en" && LauncherLanguage != "de" && LauncherLanguage != "vi")
                msg = string.Format(textStrings["msgbox_language_msg"], lang.ToLower());
            else
                msg = string.Format(textStrings["msgbox_language_msg"], lang);
            if(LauncherLanguage == "vi")
                msg = string.Format(textStrings["msgbox_language_msg"], char.ToLower(lang[0]) + lang.Substring(1));
            if(MessageBox.Show(msg, textStrings["contextmenu_language"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            if(Status == LauncherStatus.DownloadPaused)
            {
                if(MessageBox.Show($"{textStrings["msgbox_abort_1_msg"]}\n{textStrings["msgbox_abort_2_msg"]}", textStrings["msgbox_abort_title"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    return;
                Status = LauncherStatus.CleaningUp;
                try
                {
                    if(File.Exists(GameArchivePath))
                        File.Delete(GameArchivePath);
                }
                catch
                {
                    Log($"WARNING: Failed to delete {GameArchivePath}", true, 2);
                }
                try
                {
                    if(File.Exists(CacheArchivePath))
                        File.Delete(CacheArchivePath);
                }
                catch
                {
                    Log($"WARNING: Failed to delete {CacheArchivePath}", true, 2);
                }
            }

            try
            {
                if(lang == textStrings["contextmenu_language_system"])
                {
                    try{LauncherRegKey.DeleteValue("Language");}catch{}
                }
                else if(lang == textStrings["contextmenu_language_english"])
                {
                    LauncherLanguage = "en";
                }
                else if(lang == textStrings["contextmenu_language_russian"])
                {
                    LauncherLanguage = "ru";
                }
                else if(lang == textStrings["contextmenu_language_spanish"])
                {
                    LauncherLanguage = "es";
                }
                else if(lang == textStrings["contextmenu_language_portuguese"])
                {
                    LauncherLanguage = "pt";
                }
                else if(lang == textStrings["contextmenu_language_german"])
                {
                    LauncherLanguage = "de";
                }
                else if(lang == textStrings["contextmenu_language_vietnamese"])
                {
                    LauncherLanguage = "vi";
                }
                else if(lang == textStrings["contextmenu_language_serbian"])
                {
                    LauncherLanguage = "sr";
                }
                else
                {
                    Log($"ERROR: Translation for {lang} doesn't exist", true, 1);
                    return;
                }
                if(lang != textStrings["contextmenu_language_system"])
                {
                    SetLanguage(LauncherLanguage);
                    LauncherRegKey.SetValue("Language", LauncherLanguage);
                }
                var parent = (MenuItem)item.Parent;
                foreach(dynamic i in parent.Items)
                {
                    if(i.GetType() != typeof(MenuItem) || i == item)
                        continue;
                    i.IsChecked = false;
                }
                item.IsChecked = true;
                Log($"Set language to {lang}");
                BpUtility.StartProcess(LauncherExeName, string.Join(" ", CommandLineArgs), RootPath, true);
                Application.Current.Shutdown();
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR: Failed to set language:\n{ex}", true, 1);
                Status = LauncherStatus.Ready;
            }
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
                download = null;
                DownloadPaused = false;
                DeleteFile(GameArchivePath);
                if(!PatchDownload)
                    DeleteGameFiles();
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
                Log($"ERROR: Failed to write value with key LastSelectedServer to registry:\n{ex}", true, 1);
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
                download = null;
                DownloadPaused = false;
                DeleteFile(GameArchivePath);
                if(!PatchDownload)
                    DeleteGameFiles();
            }
            else if(Mirror == HI3Mirror.miHoYo && index != 0)
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
                Log($"ERROR: Failed to write value with key LastSelectedMirror to registry:\n{ex}", true, 1);
            }
            GameUpdateCheck();
            Log($"Selected mirror: {((ComboBoxItem)MirrorDropdown.SelectedItem).Content as string}");
        }
        
        private void FPSInputBoxTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.Any(x => char.IsDigit(x));
        }

        // https://stackoverflow.com/q/1268552/7570821
        private void FPSInputBoxTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            bool IsTextAllowed(string text)
            {
                return Array.TrueForAll(text.ToCharArray(), delegate (char c){return char.IsDigit(c) || char.IsControl(c);});
            }

            if(e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
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

        private void IntroBoxCloseButton_Click(object sender, RoutedEventArgs e)
        {
            IntroBox.Visibility = Visibility.Collapsed;
            if(FirstLaunch)
                GameUpdateCheck();
        }

        private void DownloadCacheBoxFullCacheButton_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show($"{textStrings["msgbox_download_cache_1_msg"]}\n{string.Format(textStrings["msgbox_download_cache_3_msg"], BpUtility.ToBytesCount((long)GameCacheMetadata.fileSize))}", textStrings["contextmenu_downloadcache"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            DownloadCacheBox.Visibility = Visibility.Collapsed;
            DownloadGameCache(true);
        }

        private void DownloadCacheBoxNumericFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show($"{textStrings["msgbox_download_cache_2_msg"]}\n{string.Format(textStrings["msgbox_download_cache_3_msg"], BpUtility.ToBytesCount((long)GameCacheMetadataNumeric.fileSize))}", textStrings["contextmenu_downloadcache"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;
            DownloadCacheBox.Visibility = Visibility.Collapsed;
            DownloadGameCache(false);
        }

        private void DownloadCacheBoxCloseButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadCacheBox.Visibility = Visibility.Collapsed;
        }

        private async void RepairBoxYesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                async Task Verify()
                {
                    var corruptedFiles = new List<string>();
                    var corruptedFileHashes = new List<string>();
                    long corruptedFilesTotalSize = 0;

                    Log("Verifying game files...");
                    await Task.Run(() =>
                    {
                        for(int i = 0; i < OnlineRepairInfo.files.names.Count; i++)
                        {
                            string name = OnlineRepairInfo.files.names[i].ToString().Replace("/", @"\");
                            string md5 = OnlineRepairInfo.files.hashes[i].ToString().ToUpper();
                            long size = OnlineRepairInfo.files.sizes[i];
                            string path = Path.Combine(GameInstallPath, name);

                            Dispatcher.Invoke(() =>
                            {
                                ProgressText.Text = string.Format(textStrings["progresstext_verifying_file"], i + 1, OnlineRepairInfo.files.names.Count);
                                var progress = (i + 1f) / OnlineRepairInfo.files.names.Count;
                                ProgressBar.Value = progress;
                                TaskbarItemInfo.ProgressValue = progress;
                            });
                            if(!File.Exists(path) || BpUtility.CalculateMD5(path) != md5)
                            {
                                if(File.Exists(path))
                                    Log($"File corrupted: {name}");
                                else
                                    Log($"File missing: {name}");
                                corruptedFiles.Add(name);
                                corruptedFileHashes.Add(md5);
                                corruptedFilesTotalSize += size;
                            }
                            else
                            {
                                if(AdvancedFeatures)
                                    Log($"File OK: {name}");
                            }
                        }
                    });
                    ProgressText.Text = string.Empty;
                    ProgressBar.Visibility = Visibility.Hidden;
                    ProgressBar.Value = 0;
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    TaskbarItemInfo.ProgressValue = 0;
                    WindowState = WindowState.Normal;
                    if(corruptedFiles.Count > 0)
                    {
                        Log($"Finished verifying files, found corrupted/missing files: {corruptedFiles.Count}");
                        if(MessageBox.Show(string.Format(textStrings["msgbox_repair_3_msg"], corruptedFiles.Count, BpUtility.ToBytesCount(corruptedFilesTotalSize)), textStrings["contextmenu_repair"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            string[] urls = OnlineRepairInfo.zip_urls.ToObject<string[]>();
                            int repairedFiles = 0;
                            bool abort = false;

                            Status = LauncherStatus.Downloading;
                            await Task.Run(async () =>
                            {
                                if(urls.Length == 0)
                                {
                                    throw new InvalidOperationException("No download URLs are present in repair data.");
                                }
                                for(int i = 0; i < corruptedFiles.Count; i++)
                                {
                                    string path = Path.Combine(GameInstallPath, corruptedFiles[i]);

                                    Dispatcher.Invoke(() =>
                                    {
                                        ProgressText.Text = string.Format(textStrings["progresstext_downloading_file"], i + 1, corruptedFiles.Count);
                                        var progress = (i + 1f) / corruptedFiles.Count;
                                        ProgressBar.Value = progress;
                                        TaskbarItemInfo.ProgressValue = progress;
                                    });
                                    for(int j = 0; j < urls.Length; j++)
                                    {
                                        string url = null;

                                        try
                                        {
                                            if(string.IsNullOrEmpty(urls[j]))
                                            {
                                                throw new NullReferenceException($"Download URL with index {j} is empty.");
                                            }
                                            else if(urls[j].Contains("www.mediafire.com"))
                                            {
                                                var metadata = FetchMediaFireFileMetadata(urls[j].Substring(31, 15), false);
                                                url = metadata.downloadUrl.ToString();
                                            }
                                            else
                                            {
                                                url = urls[j];
                                            }
                                        
                                            await PartialZipDownloader.DownloadFile(url, corruptedFiles[i], path);
                                            Dispatcher.Invoke(() => {ProgressText.Text = string.Format(textStrings["progresstext_verifying_file"], i + 1, corruptedFiles.Count);});
                                            if(!File.Exists(path) || BpUtility.CalculateMD5(path) != corruptedFileHashes[i])
                                            {
                                                Log($"ERROR: Failed to repair file {corruptedFiles[i]}", true, 1);
                                            }
                                            else
                                            {
                                                Log($"Repaired file {corruptedFiles[i]}");
                                                repairedFiles++;
                                            }
                                        }
                                        catch(Exception ex)
                                        {
                                            if(j == urls.Length - 1)
                                            {
                                                Status = LauncherStatus.Error;
                                                Log($"ERROR: Failed to download file [{corruptedFiles[i]}] ({url}): {ex.Message}\nNo more mirrors available!", true, 1);
                                                Dispatcher.Invoke(() =>
                                                {
                                                    MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                                                    LaunchButton.Content = textStrings["button_launch"];
                                                });
                                                Status = LauncherStatus.Ready;
                                                abort = true;
                                                return;
                                            }
                                            else
                                            {
                                                Log($"WARNING: Failed to download file [{corruptedFiles[i]}] ({url}): {ex.Message}\nAttempting to download from another mirror...", true, 2);
                                            }
                                        }
                                    }
                                }
                            });
                            Dispatcher.Invoke(() =>
                            {
                                LaunchButton.Content = textStrings["button_launch"];
                                ProgressText.Text = string.Empty;
                                ProgressBar.Visibility = Visibility.Hidden;
                                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                            });
                            if(!abort)
                            {
                                if(repairedFiles == corruptedFiles.Count)
                                {
                                    Log($"Successfully repaired {repairedFiles} file(s)");
                                    Dispatcher.Invoke(() =>
                                    {
                                        MessageBox.Show(string.Format(textStrings["msgbox_repair_4_msg"], repairedFiles), textStrings["contextmenu_repair"], MessageBoxButton.OK, MessageBoxImage.Information);
                                    });
                                }
                                else
                                {
                                    int skippedFiles = corruptedFiles.Count - repairedFiles;
                                    if(repairedFiles > 0)
                                        Log($"Successfully repaired {repairedFiles} files, failed to repair {skippedFiles} files");
                                    Dispatcher.Invoke(() =>
                                    {
                                        MessageBox.Show(string.Format(textStrings["msgbox_repair_5_msg"], skippedFiles), textStrings["contextmenu_repair"], MessageBoxButton.OK, MessageBoxImage.Warning);
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        Log($"Finished verifying files, no files need repair");
                        Dispatcher.Invoke(() =>
                        {
                            ProgressText.Text = string.Empty;
                            ProgressBar.Visibility = Visibility.Hidden;
                            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                        });
                        MessageBox.Show(textStrings["msgbox_repair_2_msg"], textStrings["contextmenu_repair"], MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    Status = LauncherStatus.Ready;
                }
                RepairBox.Visibility = Visibility.Collapsed;
                Status = LauncherStatus.Working;
                ProgressText.Text = textStrings["progresstext_fetching_hashes"];
                ProgressBar.IsIndeterminate = false;
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                await Verify();
            }
            catch(Exception ex)
            {
                LaunchButton.Content = textStrings["button_launch"];
                Status = LauncherStatus.Error;
                Log($"ERROR:\n{ex}", true, 1);
                MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error);
                Status = LauncherStatus.Ready;
            }
        }

        private async void RepairBoxGenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show(textStrings["msgbox_repair_6_msg"], textStrings["contextmenu_repair"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                return;

            async Task Generate()
            {
                string server;
                if(Server == HI3Server.Global)
                    server = "global";
                else
                    server = "os";
                var dialog = new SaveFileDialog
                {
                    InitialDirectory = RootPath,
                    Filter = "JSON (*.json)|*.json",
                    FileName = $"bh3_files_{server}.json"
                };
                if(dialog.ShowDialog() == true)
                {
                    try
                    {
                        Status = LauncherStatus.Working;
                        ProgressBar.IsIndeterminate = false;
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                        Log("Generating game file hashes...");
                        var files = new DirectoryInfo(GameInstallPath).GetFiles("*", SearchOption.AllDirectories).Where(x => !x.Attributes.HasFlag(FileAttributes.Hidden) && x.Name != "config.ini" && x.Name != "UniFairy.sys" && x.Name != "Version.txt" && x.Name != "blockVerifiedVersion.txt" && !x.Name.Contains("Blocks_") && !x.Name.Contains("AUDIO_DLC") && !x.Name.Contains("AUDIO_EVENT") && !x.DirectoryName.Contains("Video") && !x.DirectoryName.Contains("webCaches") && x.Extension != ".log").ToList();
                        dynamic json = new ExpandoObject();
                        json.repair_info = new ExpandoObject();
                        json.repair_info.game_version = miHoYoVersionInfo.cur_version;
                        json.repair_info.mirrors = string.Empty;
                        json.repair_info.zip_urls = Array.Empty<string>();
                        json.repair_info.files = new ExpandoObject();
                        json.repair_info.files.names = new dynamic[files.Count];
                        json.repair_info.files.hashes = new dynamic[files.Count];
                        json.repair_info.files.sizes = new dynamic[files.Count];
                        await Task.Run(() =>
                        {
                            for(int i = 0; i < files.Count; i++)
                            {
                                json.repair_info.files.names[i] = files[i].FullName.Replace($"{GameInstallPath}\\", string.Empty).Replace(@"\", "/");
                                json.repair_info.files.hashes[i] = BpUtility.CalculateMD5(files[i].FullName);
                                json.repair_info.files.sizes[i] = files[i].Length;
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressText.Text = string.Format(textStrings["progresstext_generating_hash"], i + 1, files.Count);
                                    var progress = (i + 1f) / files.Count;
                                    ProgressBar.Value = progress;
                                    TaskbarItemInfo.ProgressValue = progress;
                                });
                            }
                            File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(json));
                            Log("success!", false);
                            Log($"Saved JSON: {dialog.FileName}");
                        });
                        ProgressText.Text = string.Empty;
                        ProgressBar.Visibility = Visibility.Hidden;
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                        if(MessageBox.Show(textStrings["msgbox_repair_7_msg"], textStrings["contextmenu_repair"], MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            await Task.Run(() =>
                            {
                                Log("Creating ZIP file...");
                                var zipName = dialog.FileName.Replace(".json", ".zip");
                                if(File.Exists(zipName))
                                    File.Delete(zipName);
                                using(var archive = ZipFile.Open(zipName, ZipArchiveMode.Create))
                                {
                                    for(int i = 0; i < files.Count; i++)
                                    {
                                        archive.CreateEntryFromFile(files[i].FullName, files[i].FullName.Replace($"{GameInstallPath}\\", string.Empty));
                                        Dispatcher.Invoke(() =>
                                        {
                                            ProgressText.Text = string.Format(textStrings["progresstext_zipping"], i + 1, files.Count);
                                            var progress = (i + 1f) / files.Count;
                                            ProgressBar.Value = progress;
                                            TaskbarItemInfo.ProgressValue = progress;
                                        });
                                    }
                                }
                                Log("success!", false);
                                Log($"Saved ZIP: {zipName}");
                            });
                        }
                        Status = LauncherStatus.Ready;
                    }
                    catch(Exception ex)
                    {
                        Status = LauncherStatus.Error;
                        Log($"ERROR:\n{ex}", true, 1);
                        Status = LauncherStatus.Ready;
                    }
                }
            }
            RepairBox.Visibility = Visibility.Collapsed;
            await Generate();
        }

        private void RepairBoxCloseButton_Click(object sender, RoutedEventArgs e)
        {
            RepairBox.Visibility = Visibility.Collapsed;
        }

        private void FPSInputBoxOKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CombatFPSInputBoxTextBox.Text = string.Concat(CombatFPSInputBoxTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
                MenuFPSInputBoxTextBox.Text = string.Concat(MenuFPSInputBoxTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
                if(string.IsNullOrEmpty(CombatFPSInputBoxTextBox.Text) || string.IsNullOrEmpty(MenuFPSInputBoxTextBox.Text))
                {
                    MessageBox.Show(textStrings["msgbox_customfps_1_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                int fpsCombat = int.Parse(CombatFPSInputBoxTextBox.Text);
                int fpsMenu = int.Parse(MenuFPSInputBoxTextBox.Text);
                if(fpsCombat < 1 || fpsMenu < 1)
                {
                    MessageBox.Show(textStrings["msgbox_customfps_2_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else if(fpsCombat < 30 || fpsMenu < 30)
                {
                    if(MessageBox.Show(textStrings["msgbox_customfps_3_msg"], textStrings["contextmenu_customfps"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                GameGraphicSettings.IsUserDefinedGrade = false;
                GameGraphicSettings.IsUserDefinedVolatile = true;
                GameGraphicSettings.TargetFrameRateForInLevel = fpsCombat;
                GameGraphicSettings.TargetFrameRateForOthers = fpsMenu;
                var valueAfter = Encoding.UTF8.GetBytes($"{JsonConvert.SerializeObject(GameGraphicSettings)}\0");
                var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                key.SetValue("GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411", valueAfter, RegistryValueKind.Binary);
                key.Close();
                FPSInputBox.Visibility = Visibility.Collapsed;
                Log($"Set in-game FPS to {fpsCombat}, menu FPS to {fpsMenu}");
                MessageBox.Show(string.Format(textStrings["msgbox_customfps_4_msg"], fpsCombat, fpsMenu), textStrings["contextmenu_customfps"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR:\n{ex}", true, 1);
                if(MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
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
                ResolutionInputBoxHeightTextBox.Text = string.Concat(ResolutionInputBoxHeightTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
                ResolutionInputBoxWidthTextBox.Text = string.Concat(ResolutionInputBoxWidthTextBox.Text.Where(c => !char.IsWhiteSpace(c)));
                if(string.IsNullOrEmpty(ResolutionInputBoxHeightTextBox.Text) || string.IsNullOrEmpty(ResolutionInputBoxWidthTextBox.Text))
                {
                    MessageBox.Show(textStrings["msgbox_customfps_1_msg"], textStrings["contextmenu_customresolution"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                bool fullscreen = (bool)ResolutionInputBoxFullscreenCheckbox.IsChecked;
                int height = int.Parse(ResolutionInputBoxHeightTextBox.Text);
                int width = int.Parse(ResolutionInputBoxWidthTextBox.Text);
                if(height < 1 || width < 1)
                {
                    MessageBox.Show(textStrings["msgbox_customfps_2_msg"], textStrings["contextmenu_customresolution"], MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else if(height > width)
                {
                    if(MessageBox.Show(textStrings["msgbox_customresolution_1_msg"], textStrings["contextmenu_customresolution"], MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                GameScreenSettings.height = height;
                GameScreenSettings.width = width;
                GameScreenSettings.isfullScreen = fullscreen;
                var valueAfter = Encoding.UTF8.GetBytes($"{JsonConvert.SerializeObject(GameScreenSettings)}\0");
                var key = Registry.CurrentUser.OpenSubKey(GameRegistryPath, true);
                key.SetValue("GENERAL_DATA_V2_ScreenSettingData_h1916288658", valueAfter, RegistryValueKind.Binary);
                string iniFullscreen = "Screenmanager Is Fullscreen mode_h3981298716";
                string iniWidth = "Screenmanager Resolution Width_h182942802";
                string iniHeight = "Screenmanager Resolution Height_h2627697771";
                if(key.GetValue(iniFullscreen) != null)
                    key.SetValue(iniFullscreen, fullscreen, RegistryValueKind.DWord);
                if(key.GetValue(iniWidth) != null)
                    key.SetValue(iniWidth, width, RegistryValueKind.DWord);
                if(key.GetValue(iniHeight) != null)
                    key.SetValue(iniHeight, height, RegistryValueKind.DWord);
                key.Close();
                ResolutionInputBox.Visibility = Visibility.Collapsed;
                string isFullscreen = fullscreen ? "enabled" : "disabled";
                Log($"Set game resolution to {width}x{height}, fullscreen {isFullscreen}");
                isFullscreen = fullscreen ? textStrings["enabled"].ToLower() : textStrings["disabled"].ToLower();
                MessageBox.Show(string.Format(textStrings["msgbox_customresolution_2_msg"], width, height, isFullscreen), textStrings["contextmenu_customresolution"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                Status = LauncherStatus.Error;
                Log($"ERROR:\n{ex}", true, 1);
                if(MessageBox.Show(textStrings["msgbox_genericerror_msg"], textStrings["msgbox_genericerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
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
            ChangelogBox.Visibility = Visibility.Collapsed;
            ChangelogBoxMessageTextBlock.Visibility = Visibility.Collapsed;
            ChangelogBoxScrollViewer.Height = 325;
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
                Log($"ERROR: Failed to write value with key ShowLog to registry:\n{ex}", true, 1);
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
                Log($"ERROR: Failed to write value with key ShowLog to registry:\n{ex}", true, 1);
            }
        }

        private void AboutBoxGitHubButton_Click(object sender, RoutedEventArgs e)
        {
            AboutBox.Visibility = Visibility.Collapsed;
            BpUtility.StartProcess("https://github.com/BuIlDaLiBlE/BetterHI3Launcher", null, RootPath, true);
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
                            try
                            {
                                if(File.Exists(GameArchivePath))
                                    File.Delete(GameArchivePath);
                            }
                            catch
                            {
                                Log($"WARNING: Failed to delete {GameArchivePath}", true, 2);
                            }
                            try
                            {
                                if(File.Exists(CacheArchivePath))
                                    File.Delete(CacheArchivePath);
                            }
                            catch
                            {
                                Log($"WARNING: Failed to delete {CacheArchivePath}", true, 2);
                            }
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
                    Log($"ERROR: Failed to install the game:\n{ex}", true, 1);
                    if(MessageBox.Show(textStrings["msgbox_installerror_msg"], textStrings["msgbox_installerror_title"], MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                    {
                        return;
                    }
                }
            }
            else if(Status == LauncherStatus.Verifying || Status == LauncherStatus.Unpacking || Status == LauncherStatus.CleaningUp || Status == LauncherStatus.Uninstalling || Status == LauncherStatus.Working)
            {
                e.Cancel = true;
            }
        }

        private void OnGameExit()
        {
            Dispatcher.Invoke(() =>
            {
                LaunchButton.Content = textStrings["button_launch"];
                Status = LauncherStatus.Ready;
                WindowState = WindowState.Normal;
            });
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
            if(path.Length >= 16)
            {
                pathVariants.Add(path.Substring(0, path.Length - 16));
            }
            if(path.Length >= 18)
            {
                pathVariants.Add(path.Substring(0, path.Length - 18));
            }

            if(File.Exists(Path.Combine(path, GameExeName)))
            {
                return path;
            }
            else
            {
                foreach(var variant in pathVariants)
                {
                    if(File.Exists(Path.Combine(variant, GameExeName)))
                        return variant;
                }
                return string.Empty;
            }
        }

        private int CheckForExistingGameClientServer()
        {
            var path = Path.Combine(GameInstallPath, @"BH3_Data\app.info");
            if(File.Exists(path))
            {
                var gameTitleLine = File.ReadLines(path).Skip(1).Take(1).First();
                if(!string.IsNullOrEmpty(gameTitleLine))
                {
                    if(gameTitleLine.Contains("Honkai Impact 3rd"))
                        return 0;
                    else if(gameTitleLine.Contains("Honkai Impact 3"))
                        return 1;

                }
            }
            return -1;
        }

        private void ToggleContextMenuItems(bool val, bool leaveUninstallEnabled = false)
        {
            foreach(dynamic item in OptionsContextMenu.Items)
            {
                if(item.GetType() == typeof(MenuItem))
                { 
                    if(item.Header.ToString() == textStrings["contextmenu_web_profile"] ||
                       item.Header.ToString() == textStrings["contextmenu_feedback"] ||
                       item.Header.ToString() == textStrings["contextmenu_changelog"] ||
                       item.Header.ToString() == textStrings["contextmenu_important_info"] ||
                       item.Header.ToString() == textStrings["contextmenu_language"] ||
                       item.Header.ToString() == textStrings["contextmenu_about"])
                        continue;
                }
                if(!val && leaveUninstallEnabled)
                {
                    if(item.GetType() == typeof(MenuItem) && item.Header.ToString() == textStrings["contextmenu_uninstall"])
                        continue;
                }
                    
                item.IsEnabled = val;
            }
        }

        public void SetLanguage(string lang)
        {
            switch(lang)
            {
                case "de":
                    LauncherLanguage = lang;
                    TextStrings_German();
                    break;
                case "es":
                    LauncherLanguage = lang;
                    TextStrings_Spanish();
                    break;
                case "pt":
                    LauncherLanguage = lang;
                    TextStrings_Portuguese();
                    break;
                case "ru":
                    LauncherLanguage = lang;
                    TextStrings_Russian();
                    break;
                case "sr":
                    LauncherLanguage = lang;
                    TextStrings_Serbian();
                    break;
                case "vi":
                    LauncherLanguage = lang;
                    TextStrings_Vietnamese();
                    break;
                default:
                    LauncherLanguage = "en";
                    TextStrings_English();
                    break;
            }
            if(LauncherLanguage != "en")
            {
                var IntroBoxGrid = VisualTreeHelper.GetChild(IntroBox, 1) as Grid;
                var ChangelogBoxGrid = VisualTreeHelper.GetChild(ChangelogBox, 1) as Grid;
                var AboutBoxGrid = VisualTreeHelper.GetChild(AboutBox, 1) as Grid;
                IntroBoxMessageTextBlock.Height -= 5;
                RepairBoxTitleTextBlock.Height -= 5;
                ChangelogBoxGrid.Height += 7;
                DownloadCacheBoxMessageTextBlock.Height -= 5;
                Resources["Font"] = new FontFamily("Segoe UI Bold");
                if(LauncherLanguage == "de")
                {
                    IntroBoxGrid.Height += 40;
                    IntroBoxMessageTextBlock.Height += 40;
                    AboutBoxGrid.Height += 10;
                    AboutBoxMessageTextBlock.Height += 5;
                }
                else if(LauncherLanguage == "es" || LauncherLanguage == "pt" || LauncherLanguage == "sr" || LauncherLanguage == "vi")
                {
                    RepairBoxMessageTextBlock.Height -= 5;
                    AboutBoxGrid.Height += 10;
                }
                else if(LauncherLanguage == "ru")
                {
                    RepairBoxMessageTextBlock.Height -= 5;
                    AboutBoxMessageTextBlock.Height -= 10;
                }
            }
        }

        public void Log(string msg, bool newline = true, int type = 0)
        {
            if(string.IsNullOrEmpty(msg))
                return;

            Color color;
            #if DEBUG
                ConsoleColor ccolor;
            #endif
            switch(type)
            {
                case 1:
                    color = Colors.Red;
                    #if DEBUG
                        ccolor = ConsoleColor.Red;
                    #endif
                    break;
                case 2:
                    color = Colors.Yellow;
                     #if DEBUG
                        ccolor = ConsoleColor.Yellow;
                    #endif
                    break;
                default:
                    color = Colors.White;
                     #if DEBUG
                        ccolor = ConsoleColor.Gray;
                    #endif
                    break;
            }
            #if DEBUG
                Console.ForegroundColor = ccolor;
                if(newline)
                    Console.Write('\n' + msg);
                else
                    Console.Write(msg);
            #endif
            Dispatcher.Invoke(() =>
            {
                if(newline)
                {
                    var brush = new SolidColorBrush(color);
                    var run = new Run()
                    {
                        Text = msg,
                        Foreground = brush
                    };
                    var para = new Paragraph(run)
                    {
                        Margin = new Thickness(0)
                    };
                    LogBoxRichTextBox.Document.Blocks.Add(para);
                }
                else
                {
                    LogBoxRichTextBox.AppendText(msg);
                }
                LogBoxScrollViewer.ScrollToEnd();
            });
            if(!DisableLogging)
            {
                try
                {
                    if(File.Exists(LauncherLogFile))
                        File.SetAttributes(LauncherLogFile, File.GetAttributes(LauncherLogFile) & ~FileAttributes.ReadOnly);
                    if(newline)
                    {
                        File.AppendAllText(LauncherLogFile, '\n' + msg);
                    }
                    else
                    {
                        File.AppendAllText(LauncherLogFile, msg);
                    }
                }
                catch
                {
                    Log($"WARNING: Unable to write to log file", true, 2);
                }
            }
        }

        public void DeleteFile(string path, bool ignorereadonly = false)
        {
            try
            {
                if(File.Exists(path))
                {
                    if(ignorereadonly)
                    {
                        File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                    }
                    File.Delete(path);
                }
            }
            catch
            {
                Log($"WARNING: Failed to delete {path}", true, 2);
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
                if(_versionStrings.Length != 4)
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
    }
}