﻿using System.Windows;

namespace BetterHI3Launcher
{
    public partial class MainWindow : Window
    {
        private void TextStrings_Russian()
        {
            textStrings["version"] = "Версия";
            textStrings["launcher_version"] = "Версия лаунчера";
            textStrings["binary_prefixes"] = "КМГТПЭЗЙ";
            textStrings["binary_prefix_byte"] = "б";
            textStrings["bytes_per_second"] = "Б/с";
            textStrings["outdated"] = "Устарел";
            textStrings["button_download"] = "Загрузить";
            textStrings["button_downloading"] = "Загрузка";
            textStrings["button_update"] = "Обновить";
            textStrings["button_pause"] = "Приостановить";
            textStrings["button_launch"] = "Запустить";
            textStrings["button_options"] = "Опции";
            textStrings["button_resume"] = "Возобновить";
            textStrings["button_confirm"] = "Принять";
            textStrings["button_cancel"] = "Отмена";
            textStrings["label_server"] = "Сервер";
            textStrings["label_mirror"] = "Зеркало";
            textStrings["label_log"] = "Показать лог";
            textStrings["contextmenu_downloadcache"] = "Загрузить кэш";
            textStrings["contextmenu_uninstall"] = "Удалить игру";
            textStrings["contextmenu_fixupdateloop"] = "Исправить цикл загрузки";
            textStrings["contextmenu_fixsubs"] = "Исправить субтитры";
            textStrings["contextmenu_customfps"] = "Своё ограничение FPS";
            textStrings["contextmenu_resetgamesettings"] = "Сбросить настройки игры";
            textStrings["contextmenu_changelog"] = "Показать историю изменений";
            textStrings["contextmenu_about"] = "О лаунчере";
            textStrings["progresstext_error"] = "Ошибочка вышла :^(";
            textStrings["progresstext_verifying"] = "Проверка игровых файлов...";
            textStrings["progresstext_cleaningup"] = "Убираюсь за собой...";
            textStrings["progresstext_checkingupdate"] = "Проверка на наличие обновления...";
            textStrings["progresstext_downloadsize"] = "Размер загрузки";
            textStrings["progresstext_downloaded"] = "Загружено {0}/{1} ({2})";
            textStrings["progresstext_eta"] = "Расчётное время: {0}";
            textStrings["progresstext_unpacking_1"] = "Распаковка игровых файлов...";
            textStrings["progresstext_unpacking_2"] = "Распаковка игрового файла {0}/{1}";
            textStrings["progresstext_writinginfo"] = "Запись информации о версии...";
            textStrings["progresstext_uninstalling"] = "Удаление игры...";
            textStrings["progresstext_mirror_connect"] = "Подключение к зеркалу...";
            textStrings["progresstext_initiating_download"] = "Загрузка начинается...";
            textStrings["inputbox_customfps_title"] = "Введите максимальное значение FPS";
            textStrings["changelogbox_title"] = "История изменений";
            textStrings["changelogbox_msg"] = "Better HI3 Launcher только что стал ещё лучше. Вот что произошло:";
            textStrings["downloadcachebox_msg"] = "Здесь вы можете загрузить игровой кэш.\nВыберите \"Полный кэш\", если игра застревает на \"Updating event resources\".\nВыберите \"Числовые файлы\", если игра застревает на \"Updating settings\".\nУчтите, что на данный момент нет способа автоматически загружать новейший кэш, а потому его нужно загружать вручную с зеркала.\nИспользуемое зеркало: {0}.\nДата обновления кэша: {1}\nОтветственный за зеркало: {2}.";
            textStrings["downloadcachebox_button_full_cache"] = "Полный кэш";
            textStrings["downloadcachebox_button_numeric_files"] = "Числовые файлы";
            textStrings["msgbox_download_cache_1_msg"] = "Сейчас начнётся загрузка полного кэша.";
            textStrings["msgbox_download_cache_2_msg"] = "Сейчас начнётся загрузка кэша числовых файлов.";
            textStrings["msgbox_download_cache_3_msg"] = "Приблизительный размер: {0}.\nПродолжить?";
            textStrings["msgbox_genericerror_title"] = "Ошибка";
            textStrings["msgbox_genericerror_msg"] = "Произошла ошибка:\n{0}";
            textStrings["msgbox_neterror_title"] = "Сетевая ошибка";
            textStrings["msgbox_neterror_msg"] = "Произошла ошибка подключения к серверу";
            textStrings["msgbox_verifyerror_title"] = "Ошибка проверки целостности файла";
            textStrings["msgbox_verifyerror_msg"] = "Произошла ошибка при загрузке, файл может быть повреждён.\nВсё равно продолжить?";
            textStrings["msgbox_starterror_title"] = "Ошибка запуска";
            textStrings["msgbox_starterror_msg"] = "Произошла ошибка запуска лаунчера:\n{0}";
            textStrings["msgbox_launcherdownloaderror_msg"] = "Произошла ошибка загрузки лаунчера:\n{0}";
            textStrings["msgbox_gamedownloaderror_title"] = "Ошибка загрузки игровых файлов";
            textStrings["msgbox_gamedownloaderror_msg"] = "Произошла ошибка загрузки игровых файлов:\n{0}";
            textStrings["msgbox_installerror_msg"] = "Произошла ошибка установки игровых файлов:\n{0}";
            textStrings["msgbox_installerror_title"] = "Ошибка установки";
            textStrings["msgbox_startgameerror_msg"] = "Произошла ошибка запуска игры:\n{0}";
            textStrings["msgbox_update_msg"] = "Доступна версия новее, необходимо обновиться.";
            textStrings["msgbox_update_title"] = "Уведомление об обновлении";
            textStrings["msgbox_install_msg"] = "Игра будет установлена по пути:\n{0}\nПродолжить установку?";
            textStrings["msgbox_install_title"] = "Уведомление об установке";
            textStrings["msgbox_installdirerror_msg"] = "Произошла ошибка выбора игрового пути:\n{0}";
            textStrings["msgbox_installdirerror_title"] = "Неверный путь установки";
            textStrings["msgbox_abort_1_msg"] = "Вы точно уверены, что хотите отменить загрузку и выйти?";
            textStrings["msgbox_abort_2_msg"] = "Прогресс не будет сохранён.";
            textStrings["msgbox_abort_3_msg"] = "Прогресс будет сохранён.";
            textStrings["msgbox_abort_title"] = "Запрос на отмену";
            textStrings["msgbox_registryerror_msg"] = "Произошла ошибка доступа к реестру:\n{0}";
            textStrings["msgbox_registryerror_title"] = "Ошибка реестра";
            textStrings["msgbox_registryempty_msg"] = "Нужное значение в реестре отсутствует. Вы уже запускали игру?";
            textStrings["msgbox_uninstall_1_msg"] = "Вы уверены, что хотите удалить игру?";
            textStrings["msgbox_uninstall_2_msg"] = "Вы точно уверены, что хотите удалить игру? :^(";
            textStrings["msgbox_uninstall_3_msg"] = "Удалить также и настройки игры с кэшем?";
            textStrings["msgbox_uninstall_4_msg"] = "Нельзя удалить игру, пока лаунчер находится внутри игровой папки. Переместите лаунчер из папки и попробуйте снова.";
            textStrings["msgbox_uninstall_title"] = "Удаление";
            textStrings["msgbox_uninstallerror_msg"] = "Произошла ошибка удаления игры:\n{0}";
            textStrings["msgbox_uninstallerror_title"] = "Ошибка удаления";
            textStrings["msgbox_fixupdateloop_1_msg"] = "Будет произведена попытка исправить печально известную проблему с бесконечной загрузкой в игре.\nЕсли с первого раза не помогает, попробуйте ещё раз.\nПродолжить?";
            textStrings["msgbox_fixupdateloop_2_msg"] = "Значение ResourceDownloadType до: {0}.\nЗначение ResourceDownloadType после: {1}.";
            textStrings["msgbox_fixsubs_1_msg"] = "Будет произведена попытка исправить субтитры к видео (и гача баннер). Убедитесь, что все видеофайлы были загружены в игре.\nПродолжить?";
            textStrings["msgbox_fixsubs_2_msg"] = "Распаковка субтитров к видео {0}/{1}...";
            textStrings["msgbox_fixsubs_3_msg"] = "Проверка файла субтитров {0}/{1}...";
            textStrings["msgbox_fixsubs_4_msg"] = "Субтитры распакованы к {0} файлам видео.";
            textStrings["msgbox_fixsubs_5_msg"] = "Исправлено {0} неправильных файлов субтитров.";
            textStrings["msgbox_fixsubs_6_msg"] = "Не был исправлен ни один файл субтитров. Субтитры либо ещё не загружены, либо уже исправлены.";
            textStrings["msgbox_customfps_1_msg"] = "Значение не должно быть пустым.";
            textStrings["msgbox_customfps_2_msg"] = "Значение не должно равняться нулю или быть отрицательным.";
            textStrings["msgbox_customfps_3_msg"] = "Значения ниже 30 не рекомендуются. Продолжить?";
            textStrings["msgbox_customfps_4_msg"] = "Максимальное значение FPS установлено на {0}.";
            textStrings["msgbox_resetgamesettings_1_msg"] = "Будут сброшены все настройки игры, сохранённые в реестре.\nИспользуйте это только в том случае, если возникли проблемы с игрой!\nПродолжить?";
            textStrings["msgbox_resetgamesettings_2_msg"] = "Эта операция необратима. Вы уверены, что хотите продолжить?";
            textStrings["msgbox_resetgamesettings_3_msg"] = "Настройки игры были сброшены.";
            textStrings["msgbox_about_msg"] = "Better Honkai Impact 3rd Launcher :^)\nНу он и правда лучше же, да?\nЛюбой отзыв глубоко приветствуется.\n\nMade by Bp (BuIlDaLiBlE production).\nDiscord: BuIlDaLiBlE#3202";
            textStrings["msgbox_extractskip_title"] = "Уведомление о пропущенных файлах";
            textStrings["msgbox_extractskip_msg"] = "Распаковка завершена, однако некоторые файлы не получилось распаковать. Возможно, придётся сделать это вручную.\nДля дополнительной информации посмотрите лог.";
            textStrings["msgbox_noexe_title"] = "Нет исполняемого файла игры";
            textStrings["msgbox_noexe_msg"] = "Исполняемый файл игры не может быть найден :^(\nПопробуйте переустановить игру.";
            textStrings["msgbox_installexisting_msg"] = "Похоже, что игра уже была установлена по пути:\n{0}\nИспользовать этот путь?";
            textStrings["msgbox_installexistinginvalid_msg"] = "По данному пути не найдено верного клиента игры. Этот лаунчер поддерживает только клиенты Global и SEA серверов.";
            textStrings["msgbox_notice_title"] = "Уведомление";
            textStrings["msgbox_novideodir_msg"] = "Папка с видео не может быть найдена.\nПопробуйте переустановить игру.";
            textStrings["msgbox_mirrorinfo_msg"] = "Используйте зеркало только в том случае, если не получается загрузить игру с официальных серверов miHoYo.\nЗаметьте, что зеркало обновляется вручную.\nПродолжить?";
            textStrings["msgbox_updatecheckerror_msg"] = "Произошла ошибка проверки обновления:\n{0}";
            textStrings["msgbox_updatecheckerror_title"] = "Ошибка проверки обновления";
            textStrings["msgbox_gamedownloadmirrorold_msg"] = "Похоже, что версия игры на серверах miHoYo новее той, что загружена на зеркало.\nЗагружать старую версию нет смысла, попросите автора загрузить новую версию на зеркало.";
            textStrings["msgbox_gamedownloadpaused_msg"] = "Игра ещё не была загружена до конца. Изменение сервера или зеркала приведёт к сбросу прогресса загрузки.\nПродолжить?";
            textStrings["msgbox_gamedownloadmirrorerror_msg"] = "Произошла ошибка загрузки с зеркала.\n{0}";
            textStrings["msgbox_install_little_space_msg"] = "Свободного пространства на устройстве по данному пути очень мало, установка может завершиться неудачей.\nПродолжить?";
            textStrings["msgbox_install_wrong_drive_type_msg"] = "На данное устройство установить нельзя.";
            textStrings["msgbox_mirror_error_msg"] = "Ошибка зеркала. Попросите ответственного за зеркало разобраться с этим.\nСообщение: {0}";
        }
    }
}