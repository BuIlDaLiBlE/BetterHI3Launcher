using System.Windows;

namespace BetterHI3Launcher
{
    public partial class MainWindow : Window
    {
        bool LanguageEnglishAdded = false;

        private void TextStrings_English()
        {
            if(LanguageEnglishAdded)
                return;

            LanguageEnglishAdded = true;
            textStrings.Add("version", "Version");
            textStrings.Add("launcher_version", "Launcher version");
            textStrings.Add("binary_prefixes", "KMGTPEZY");
            textStrings.Add("binary_prefix_byte", "b");
            textStrings.Add("bytes_per_second", "B/s");
            textStrings.Add("shrug", "¯\\_(ツ)_/¯");
            textStrings.Add("outdated", "Outdated");
            textStrings.Add("button_download", "Download");
            textStrings.Add("button_downloading", "Downloading");
            textStrings.Add("button_update", "Update");
            textStrings.Add("button_pause", "Pause");
            textStrings.Add("button_launch", "Start");
            textStrings.Add("button_options", "Options");
            textStrings.Add("button_resume", "Resume");
            textStrings.Add("button_ok", "OK");
            textStrings.Add("button_confirm", "Confirm");
            textStrings.Add("button_cancel", "Cancel");
            textStrings.Add("button_github", "Go to GitHub repository");
            textStrings.Add("label_server", "Server");
            textStrings.Add("label_mirror", "Mirror");
            textStrings.Add("label_log", "Show log");
            textStrings.Add("contextmenu_downloadcache", "Download cache");
            textStrings.Add("contextmenu_uninstall", "Uninstall game");
            textStrings.Add("contextmenu_fixupdateloop", "Fix update loop");
            textStrings.Add("contextmenu_fixsubs", "Fix subtitles");
            textStrings.Add("contextmenu_customfps", "Set custom FPS cap");
            textStrings.Add("contextmenu_resetgamesettings", "Reset game settings");
            textStrings.Add("contextmenu_web_profile", "Go to web profile");
            textStrings.Add("contextmenu_feedback", "Send feedback");
            textStrings.Add("contextmenu_changelog", "Show changelog");
            textStrings.Add("contextmenu_about", "About");
            textStrings.Add("progresstext_error", "Mistakes were made :^(");
            textStrings.Add("progresstext_verifying", "Verifying game files...");
            textStrings.Add("progresstext_cleaningup", "Cleaning up...");
            textStrings.Add("progresstext_checkingupdate", "Checking for update...");
            textStrings.Add("progresstext_downloadsize", "Download size");
            textStrings.Add("progresstext_downloaded", "Downloaded {0}/{1} ({2})");
            textStrings.Add("progresstext_eta", "Estimated time: {0}");
            textStrings.Add("progresstext_unpacking_1", "Unpacking game files...");
            textStrings.Add("progresstext_unpacking_2", "Unpacking game file {0}/{1}");
            textStrings.Add("progresstext_uninstalling", "Uninstalling the game...");
            textStrings.Add("progresstext_mirror_connect", "Connecting to mirror...");
            textStrings.Add("progresstext_initiating_download", "Initiating download...");
            textStrings.Add("progresstext_updating_launcher", "Updating launcher...");
            textStrings.Add("downloadcachebox_msg", "Select whether to download full cache package or just numeric files.\nChoose \"Full cache\" if you have a problem updating event resources.\nChoose \"Numeric files\" if you have a problem updating settings.\nPlease note that there is currently no way to automatically retrieve latest cache and we have to upload it manually to a mirror.\nUsing mirror: {0}.\nCache last updated: {1}\nCurrent mirror maintainer is {2}.");
            textStrings.Add("downloadcachebox_button_full_cache", "Full cache");
            textStrings.Add("downloadcachebox_button_numeric_files", "Numeric files");
            textStrings.Add("fpsinputbox_title", "Enter max FPS cap");
            textStrings.Add("changelogbox_title", "Changelog");
            textStrings.Add("changelogbox_msg", "Better Honkai Impact 3rd Launcher has just become even better. Here's what happened:");
            textStrings.Add("aboutbox_msg", "Well it is much more advanced, isn't it? :^)\nThis project was made with hope for many captains to have a better experience with the game.\nIt is not affiliated with miHoYo and is completely open source.\nAny feedback is greatly appreciated.");
            textStrings.Add("msgbox_genericerror_title", "Error");
            textStrings.Add("msgbox_genericerror_msg", "An error occurred:\n{0}");
            textStrings.Add("msgbox_neterror_title", "Network error");
            textStrings.Add("msgbox_neterror_msg", "An error occurred while connecting to server");
            textStrings.Add("msgbox_verifyerror_title", "File validation error");
            textStrings.Add("msgbox_verifyerror_1_msg", "An error occurred while downloading. Please try again.");
            textStrings.Add("msgbox_verifyerror_2_msg", "An error occurred while downloading. File may be corrupt.\nContinue regardless?");
            textStrings.Add("msgbox_starterror_title", "Startup error");
            textStrings.Add("msgbox_starterror_msg", "An error occurred while starting the launcher:\n{0}");
            textStrings.Add("msgbox_launcherdownloaderror_msg", "An error occurred while downloading the launcher:\n{0}");
            textStrings.Add("msgbox_gamedownloaderror_title", "Error downloading game files");
            textStrings.Add("msgbox_gamedownloaderror_msg", "An error occurred while downloading game files:\n{0}");
            textStrings.Add("msgbox_installerror_msg", "An error occurred while installing game files:\n{0}");
            textStrings.Add("msgbox_installerror_title", "Installation error");
            textStrings.Add("msgbox_process_start_error_msg", "An error occurred while starting the process:\n{0}");
            textStrings.Add("msgbox_update_title", "Update notice");
            textStrings.Add("msgbox_install_msg", "The game is going to be installed to:\n{0}\nContinue installation?");
            textStrings.Add("msgbox_install_title", "Installation notice");
            textStrings.Add("msgbox_installdirerror_msg", "An error occurred while selecting game installation directory:\n{0}");
            textStrings.Add("msgbox_installdirerror_title", "Invalid directory");
            textStrings.Add("msgbox_abort_1_msg", "Are you sure you want to cancel the download and close the launcher?");
            textStrings.Add("msgbox_abort_2_msg", "Progress will not be saved.");
            textStrings.Add("msgbox_abort_3_msg", "Progress will be saved.");
            textStrings.Add("msgbox_abort_title", "Abort request");
            textStrings.Add("msgbox_registryerror_msg", "An error occurred while accessing registry:\n{0}");
            textStrings.Add("msgbox_registryerror_title", "Registry error");
            textStrings.Add("msgbox_registryempty_msg", "No value to be tweaked in registry exists. Did you already run the game?");
            textStrings.Add("msgbox_download_cache_1_msg", "Full cache is about to be downloaded.");
            textStrings.Add("msgbox_download_cache_2_msg", "Numeric file cache is about to be downloaded.");
            textStrings.Add("msgbox_download_cache_3_msg", "Download size: {0}.\nContinue?");
            textStrings.Add("msgbox_uninstall_1_msg", "Are you sure you want to uninstall the game?");
            textStrings.Add("msgbox_uninstall_2_msg", "Are you really sure you want to uninstall the game? :^(");
            textStrings.Add("msgbox_uninstall_3_msg", "Remove game cache files and settings as well?");
            textStrings.Add("msgbox_uninstall_4_msg", "Cannot uninstall the game while the launcher is inside game directory. Move launcher outside the directory and try again.");
            textStrings.Add("msgbox_uninstall_title", "Uninstall");
            textStrings.Add("msgbox_uninstallerror_msg", "An error occurred while uninstalling the game:\n{0}");
            textStrings.Add("msgbox_uninstallerror_title", "Uninstallation error");
            textStrings.Add("msgbox_fixupdateloop_1_msg", "This will attempt to fix the infamous update loop which doesn't let you enter the game.\nIf this doesn't fix the problem, try again.\nContinue?");
            textStrings.Add("msgbox_fixupdateloop_2_msg", "ResourceDownloadType value before: {0}.\nResourceDownloadType value after: {1}.");
            textStrings.Add("msgbox_fixsubs_1_msg", "This will attempt to fix CG subtitles (and gacha banners). Make sure you have already downloaded all CGs in the game.\nContinue?");
            textStrings.Add("msgbox_fixsubs_2_msg", "Unpacking subtitle file {0}/{1}...");
            textStrings.Add("msgbox_fixsubs_3_msg", "Checking subtitle file {0}/{1}...");
            textStrings.Add("msgbox_fixsubs_4_msg", "Unpacked subtitles for {0} CGs.");
            textStrings.Add("msgbox_fixsubs_5_msg", "Fixed {0} wrong subtitle files.");
            textStrings.Add("msgbox_fixsubs_6_msg", "No subtitle files were fixed. They are either not downloaded yet or already fixed.");
            textStrings.Add("msgbox_customfps_1_msg", "Value must not be empty.");
            textStrings.Add("msgbox_customfps_2_msg", "Value must not be zero or negative.");
            textStrings.Add("msgbox_customfps_3_msg", "Values lower than 30 are not recommended. Continue?");
            textStrings.Add("msgbox_customfps_4_msg", "FPS cap successfully set to {0}.");
            textStrings.Add("msgbox_resetgamesettings_1_msg", "This will wipe all game settings stored in registry.\nOnly use this if you are having problems with the game!\nContinue?");
            textStrings.Add("msgbox_resetgamesettings_2_msg", "This action is irreversible. Are you sure you want to do this?");
            textStrings.Add("msgbox_resetgamesettings_3_msg", "Game settings have been wiped from registry.");
            textStrings.Add("msgbox_extractskip_title", "File skip notice");
            textStrings.Add("msgbox_extractskip_msg", "Unpacking finished, but some files failed to be unpacked. You might want to unpack them manually.\nFor more information take a look at the log.");
            textStrings.Add("msgbox_noexe_title", "No game executable");
            textStrings.Add("msgbox_noexe_msg", "Game executable cannot be found :^(\nTry reinstalling the game.");
            textStrings.Add("msgbox_installexisting_msg", "The game appears to have already been installed to:\n{0}\nUse this directory?");
            textStrings.Add("msgbox_installexistinginvalid_msg", "The selected directory doesn't contain a valid installation of the game. This launcher only supports Global and SEA clients.");
            textStrings.Add("msgbox_notice_title", "Notice");
            textStrings.Add("msgbox_novideodir_msg", "Video folder cannot be found.\nTry reinstalling the game.");
            textStrings.Add("msgbox_mirrorinfo_msg", "Use this mirror only if you cannot download the game via official miHoYo servers.\nPlease note that it is updated manually.\nContinue?");
            textStrings.Add("msgbox_updatecheckerror_msg", "An error occurred while checking for update:\n{0}");
            textStrings.Add("msgbox_updatecheckerror_title", "Update check error");
            textStrings.Add("msgbox_gamedownloadmirrorold_msg", "It seems like the game version on miHoYo servers is newer than the one on the mirror.\nThere is no reason to download an outdated version, ask the mirror maintainer to upload a new version.");
            textStrings.Add("msgbox_gamedownloadpaused_msg", "The game is not downloaded entirely yet. Changing server or mirror will reset the download progress.\nContinue?");
            textStrings.Add("msgbox_gamedownloadmirrorerror_msg", "An error occurred while downloading from the mirror.\n{0}");
            textStrings.Add("msgbox_install_little_space_msg", "There is not enough free space on selected device, installation may result in failure.\nContinue?");
            textStrings.Add("msgbox_install_wrong_drive_type_msg", "Cannot install on selected device.");
            textStrings.Add("msgbox_mirror_error_msg", "There's an error with the mirror. Ask the mirror maintainer to get to the bottom of this.\nMessage: {0}");
        }
    }
}