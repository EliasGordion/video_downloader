using System.Globalization;

namespace Clip.Services;

public sealed class LocalizationService
{
    public const string RussianCode = "ru";
    public const string EnglishCode = "en";

    private static AppText _currentText = AppText.Russian;
    private string _languageCode;

    public LocalizationService(string? languageCode)
    {
        _languageCode = NormalizeLanguage(languageCode);
        _currentText = GetText(_languageCode);
    }

    public string LanguageCode => _languageCode;
    public AppText Text => GetText(_languageCode);
    public static AppText CurrentText => _currentText;

    public void SetLanguage(string? languageCode)
    {
        _languageCode = NormalizeLanguage(languageCode);
        _currentText = GetText(_languageCode);
    }

    public static string NormalizeLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return RussianCode;
        }

        return languageCode.Trim().ToLowerInvariant() switch
        {
            EnglishCode or "eng" or "english" => EnglishCode,
            RussianCode or "rus" or "russian" => RussianCode,
            _ => RussianCode
        };
    }

    public static AppText GetText(string? languageCode)
    {
        return NormalizeLanguage(languageCode) == EnglishCode
            ? AppText.English
            : AppText.Russian;
    }

    public static string StatusText(string status)
    {
        return CurrentText.StatusText(status);
    }
}

public sealed class AppText
{
    public static AppText Russian { get; } = new()
    {
        Ready = "Готово",
        PasteSupportedUrlFirst = "Сначала вставь поддерживаемую ссылку на видео.",
        AnalyzingVideo = "Анализирую видео...",
        PreviewReady = "Превью готово.",
        AnalysisCancelled = "Анализ отменён.",
        ClipboardHasNoText = "В буфере обмена нет текста.",
        UrlPasted = "Ссылка вставлена.",
        CouldNotReadClipboard = "Не удалось прочитать буфер обмена.",
        CouldNotReadDroppedText = "Не удалось прочитать перетащенный текст.",
        PasteValidVideoUrlBeforeDownloading = "Перед скачиванием вставь корректную ссылку на видео.",
        ChooseSaveFolderFirst = "Сначала выбери папку для сохранения.",
        CouldNotUseSaveFolderPrefix = "Не удалось использовать папку: ",
        AddedToDownloads = "Добавлено в загрузки.",
        DownloadedFileCouldNotBeOpened = "Не удалось открыть скачанный файл.",
        DownloadFolderCouldNotBeOpened = "Не удалось открыть папку загрузки.",
        RemovedFromHistory = "Удалено из истории. Файл остался на месте.",
        CouldNotUpdateHistory = "Не удалось обновить историю.",
        DownloadCompleteTitle = "Скачивание завершено",
        DownloadFailedTitle = "Ошибка скачивания",
        VideoPreviewReadyToast = "Превью видео готово.",
        AppRunningTooltip = "Clip работает",
        VideoFallbackTitle = "Видео",

        PasteLinkTitle = "Вставь ссылку",
        PasteLinkDescription = "YouTube, X, Instagram, TikTok, Reddit и другие сайты yt-dlp.",
        PasteVideoUrlPlaceholder = "Вставь ссылку на видео",
        PasteAction = "Вставить",
        AnalyzeAction = "Анализ",
        PasteTooltip = "Вставить ссылку",
        ClearUrlTooltip = "Очистить ссылку",
        AnalyzeTooltip = "Анализировать ссылку",
        LinkNotDetected = "Ссылка не распознана",

        PreviewEyebrow = "ПРЕВЬЮ",
        PreviewPlaceholderTitle = "Здесь появится превью",
        PreviewPlaceholderSubtitle = "Вставь ссылку и запусти анализ, чтобы увидеть название, автора и длительность.",
        UnknownAuthor = "Автор неизвестен",
        Waiting = "Ожидание",

        ClipRangeTitle = "Фрагмент видео",
        ClipRangeDescription = "Выбери начало и конец для скачиваемого фрагмента.",
        UseRangeHeader = "Фрагмент",
        FullVideo = "Всё видео",
        ClipEnabled = "Фрагмент включён",
        StartLabel = "НАЧАЛО",
        EndLabel = "КОНЕЦ",
        StartTimeLabel = "Время начала",
        EndTimeLabel = "Время конца",

        OutputTitle = "Файл",
        OutputDescription = "Выбери формат и максимальное качество.",
        FormatLabel = "ФОРМАТ",
        ResolutionLabel = "КАЧЕСТВО",
        TargetSizeHeader = "Размер файла",
        OriginalQuality = "Исходное качество",
        CustomLimit = "Задать лимит",
        MbHeader = "МБ",

        SaveLocationTitle = "Папка сохранения",
        SaveLocationDescription = "Файлы будут сохраняться сюда, если не выбрать другую папку.",
        ChooseFolderTooltip = "Выбрать папку",

        ReadyToDownloadTitle = "Готово к скачиванию?",
        ReadyToDownloadDescription = "Файл будет добавлен в очередь загрузок.",
        AddToDownloads = "Добавить в загрузки",
        ClearCompletedTooltip = "Убрать завершённые",

        DownloadsTab = "Загрузки",
        HistoryTab = "История",
        SettingsTab = "Настройки",
        NoDownloadsTitle = "Загрузок пока нет",
        NoDownloadsDescription = "Вставь ссылку выше, выбери формат и добавь файл в очередь.",
        ProgressLabel = "ПРОГРЕСС",
        CancelTooltip = "Отменить",
        RetryTooltip = "Повторить",
        OpenFileTooltip = "Открыть файл",
        OpenFolderTooltip = "Открыть папку",

        HistoryEmptyTitle = "История пуста",
        HistoryEmptyDescription = "Завершённые и неудачные загрузки появятся здесь.",
        RemoveFromHistoryTooltip = "Удалить из истории",

        LanguageSectionTitle = "Язык",
        LanguageSectionDescription = "Выбери язык интерфейса. Настройка сохранится после перезапуска.",
        DownloadDefaultsTitle = "Настройки загрузки",
        DownloadDefaultsDescription = "Эти параметры будут выбраны для новых загрузок.",
        DefaultFormatHeader = "Формат по умолчанию",
        DefaultResolutionHeader = "Качество по умолчанию",
        ThemeHeader = "Тема",
        ConcurrentDownloadsHeader = "Одновременные загрузки",
        CookiesBrowserHeader = "Браузер для cookies",
        AppBehaviorTitle = "Поведение приложения",
        AppBehaviorDescription = "Буфер обмена, уведомления и работа в фоне.",
        ClipboardMonitoringTitle = "Отслеживание буфера",
        ClipboardMonitoringDescription = "Находить скопированные ссылки, пока Clip запущен.",
        AutoAnalyzeTitle = "Авто-анализ ссылок",
        AutoAnalyzeDescription = "Открывать превью сразу после копирования поддерживаемой ссылки.",
        BrowserCookiesTitle = "Cookies браузера",
        BrowserCookiesDescription = "Использовать браузерную сессию для видео с ограниченным доступом.",
        NotificationsTitle = "Уведомления",
        NotificationsDescription = "Показывать уведомление Windows после завершения скачивания.",
        KeepRunningTitle = "Оставлять в трее",
        KeepRunningDescription = "Закрытие окна не остановит активные загрузки.",

        UpdateAvailableTitle = "Доступно обновление",
        UpdateAvailableMessage = "Есть более новая сборка.",

        TrayShowClip = "Показать Clip",
        TrayPasteUrl = "Вставить ссылку",
        TrayDownloads = "Загрузки",
        TraySettings = "Настройки",
        TrayQuit = "Выйти",

        QueueNoDownloadsYet = "Загрузок пока нет",
        QueueNoActiveDownloads = "Нет активных загрузок",
        QueueSummaryFormat = "{0} выполняется, {1} в очереди",

        StatusQueued = "В очереди",
        StatusStarting = "Запуск...",
        StatusDownloading = "Скачивание...",
        StatusComplete = "Готово",
        StatusFailed = "Ошибка",
        StatusCancelled = "Отменено",
        StatusCancelling = "Отмена...",
        StatusCompressing = "Сжатие...",
        StatusPreparingDownload = "Подготовка загрузки...",
        StatusPreparingClip = "Подготовка фрагмента...",
        StatusPreparingRange = "Подготовка выбранного фрагмента...",
        StatusTryingFormat = "Пробую другой доступный формат...",
        StatusMergingStreams = "Собираю видео и звук...",
        StatusExtractingAudio = "Извлекаю аудио...",
        StatusRemuxingVideo = "Перепаковываю видео...",
        StatusFinalizingFile = "Завершаю файл...",
        StatusFileFitsTarget = "Файл уже подходит по размеру.",
        StatusNeedsAccessTryingCookiesFormat = "Нужен доступ. Пробую cookies {0}..."
    };

    public static AppText English { get; } = new()
    {
        Ready = "Ready",
        PasteSupportedUrlFirst = "Paste a supported video URL first.",
        AnalyzingVideo = "Analyzing video...",
        PreviewReady = "Preview ready.",
        AnalysisCancelled = "Analysis cancelled.",
        ClipboardHasNoText = "Clipboard does not contain text.",
        UrlPasted = "URL pasted.",
        CouldNotReadClipboard = "Could not read the clipboard.",
        CouldNotReadDroppedText = "Could not read the dropped text.",
        PasteValidVideoUrlBeforeDownloading = "Paste a valid video URL before downloading.",
        ChooseSaveFolderFirst = "Choose a save folder first.",
        CouldNotUseSaveFolderPrefix = "Could not use the save folder: ",
        AddedToDownloads = "Added to downloads.",
        DownloadedFileCouldNotBeOpened = "The downloaded file could not be opened.",
        DownloadFolderCouldNotBeOpened = "The download folder could not be opened.",
        RemovedFromHistory = "Removed from history. The file was kept.",
        CouldNotUpdateHistory = "Could not update history.",
        DownloadCompleteTitle = "Download complete",
        DownloadFailedTitle = "Download failed",
        VideoPreviewReadyToast = "Video preview is ready.",
        AppRunningTooltip = "Clip is running",
        VideoFallbackTitle = "Video",

        PasteLinkTitle = "Paste a link",
        PasteLinkDescription = "YouTube, X, Instagram, TikTok, Reddit, and other yt-dlp sites.",
        PasteVideoUrlPlaceholder = "Paste a video URL",
        PasteAction = "Paste",
        AnalyzeAction = "Analyze",
        PasteTooltip = "Paste URL",
        ClearUrlTooltip = "Clear URL",
        AnalyzeTooltip = "Analyze URL",
        LinkNotDetected = "Link not detected",

        PreviewEyebrow = "PREVIEW",
        PreviewPlaceholderTitle = "Your preview will appear here",
        PreviewPlaceholderSubtitle = "Paste a link and analyze it to see the title, author, and duration.",
        UnknownAuthor = "Unknown author",
        Waiting = "Waiting",

        ClipRangeTitle = "Clip range",
        ClipRangeDescription = "Set the start and end points for the downloaded clip.",
        UseRangeHeader = "Use range",
        FullVideo = "Full video",
        ClipEnabled = "Clip enabled",
        StartLabel = "START",
        EndLabel = "END",
        StartTimeLabel = "Start time",
        EndTimeLabel = "End time",

        OutputTitle = "Output",
        OutputDescription = "Choose the file type and maximum resolution.",
        FormatLabel = "FORMAT",
        ResolutionLabel = "RESOLUTION",
        TargetSizeHeader = "Target size",
        OriginalQuality = "Original quality",
        CustomLimit = "Custom limit",
        MbHeader = "MB",

        SaveLocationTitle = "Save location",
        SaveLocationDescription = "Files are saved here unless you change the folder.",
        ChooseFolderTooltip = "Choose folder",

        ReadyToDownloadTitle = "Ready to download?",
        ReadyToDownloadDescription = "The file will be added to the download queue.",
        AddToDownloads = "Add to downloads",
        ClearCompletedTooltip = "Clear completed",

        DownloadsTab = "Downloads",
        HistoryTab = "History",
        SettingsTab = "Settings",
        NoDownloadsTitle = "No downloads yet",
        NoDownloadsDescription = "Paste a link above, choose the output, and add it to the queue.",
        ProgressLabel = "PROGRESS",
        CancelTooltip = "Cancel",
        RetryTooltip = "Retry",
        OpenFileTooltip = "Open file",
        OpenFolderTooltip = "Open folder",

        HistoryEmptyTitle = "History is empty",
        HistoryEmptyDescription = "Completed and failed downloads will appear here.",
        RemoveFromHistoryTooltip = "Remove from history",

        LanguageSectionTitle = "Language",
        LanguageSectionDescription = "Choose the interface language. This setting is saved after restart.",
        DownloadDefaultsTitle = "Download defaults",
        DownloadDefaultsDescription = "These choices are preselected for new downloads.",
        DefaultFormatHeader = "Default format",
        DefaultResolutionHeader = "Default resolution",
        ThemeHeader = "Theme",
        ConcurrentDownloadsHeader = "Concurrent downloads",
        CookiesBrowserHeader = "Cookies browser",
        AppBehaviorTitle = "App behavior",
        AppBehaviorDescription = "Control clipboard detection, notifications, and background behavior.",
        ClipboardMonitoringTitle = "Clipboard monitoring",
        ClipboardMonitoringDescription = "Detect copied video links while Clip is running.",
        AutoAnalyzeTitle = "Auto-analyze links",
        AutoAnalyzeDescription = "Open a preview as soon as a supported link is copied.",
        BrowserCookiesTitle = "Browser cookies",
        BrowserCookiesDescription = "Use your signed-in browser session for restricted videos.",
        NotificationsTitle = "Notifications",
        NotificationsDescription = "Show a Windows notification when a download finishes.",
        KeepRunningTitle = "Keep running in tray",
        KeepRunningDescription = "Closing the window keeps active downloads running.",

        UpdateAvailableTitle = "Update available",
        UpdateAvailableMessage = "A newer build is available.",

        TrayShowClip = "Show Clip",
        TrayPasteUrl = "Paste URL",
        TrayDownloads = "Downloads",
        TraySettings = "Settings",
        TrayQuit = "Quit",

        QueueNoDownloadsYet = "No downloads yet",
        QueueNoActiveDownloads = "No active downloads",
        QueueSummaryFormat = "{0} running, {1} queued",

        StatusQueued = "Queued",
        StatusStarting = "Starting...",
        StatusDownloading = "Downloading...",
        StatusComplete = "Complete",
        StatusFailed = "Failed",
        StatusCancelled = "Cancelled",
        StatusCancelling = "Cancelling...",
        StatusCompressing = "Compressing...",
        StatusPreparingDownload = "Preparing download...",
        StatusPreparingClip = "Preparing clip...",
        StatusPreparingRange = "Preparing selected range...",
        StatusTryingFormat = "Trying another available format...",
        StatusMergingStreams = "Merging streams...",
        StatusExtractingAudio = "Extracting audio...",
        StatusRemuxingVideo = "Remuxing video...",
        StatusFinalizingFile = "Finalizing file...",
        StatusFileFitsTarget = "The file already fits the target size.",
        StatusNeedsAccessTryingCookiesFormat = "This video needs access. Trying {0} cookies..."
    };

    public string Ready { get; init; } = "";
    public string PasteSupportedUrlFirst { get; init; } = "";
    public string AnalyzingVideo { get; init; } = "";
    public string PreviewReady { get; init; } = "";
    public string AnalysisCancelled { get; init; } = "";
    public string ClipboardHasNoText { get; init; } = "";
    public string UrlPasted { get; init; } = "";
    public string CouldNotReadClipboard { get; init; } = "";
    public string CouldNotReadDroppedText { get; init; } = "";
    public string PasteValidVideoUrlBeforeDownloading { get; init; } = "";
    public string ChooseSaveFolderFirst { get; init; } = "";
    public string CouldNotUseSaveFolderPrefix { get; init; } = "";
    public string AddedToDownloads { get; init; } = "";
    public string DownloadedFileCouldNotBeOpened { get; init; } = "";
    public string DownloadFolderCouldNotBeOpened { get; init; } = "";
    public string RemovedFromHistory { get; init; } = "";
    public string CouldNotUpdateHistory { get; init; } = "";
    public string DownloadCompleteTitle { get; init; } = "";
    public string DownloadFailedTitle { get; init; } = "";
    public string VideoPreviewReadyToast { get; init; } = "";
    public string AppRunningTooltip { get; init; } = "";
    public string VideoFallbackTitle { get; init; } = "";

    public string PasteLinkTitle { get; init; } = "";
    public string PasteLinkDescription { get; init; } = "";
    public string PasteVideoUrlPlaceholder { get; init; } = "";
    public string PasteAction { get; init; } = "";
    public string AnalyzeAction { get; init; } = "";
    public string PasteTooltip { get; init; } = "";
    public string ClearUrlTooltip { get; init; } = "";
    public string AnalyzeTooltip { get; init; } = "";
    public string LinkNotDetected { get; init; } = "";

    public string PreviewEyebrow { get; init; } = "";
    public string PreviewPlaceholderTitle { get; init; } = "";
    public string PreviewPlaceholderSubtitle { get; init; } = "";
    public string UnknownAuthor { get; init; } = "";
    public string Waiting { get; init; } = "";

    public string ClipRangeTitle { get; init; } = "";
    public string ClipRangeDescription { get; init; } = "";
    public string UseRangeHeader { get; init; } = "";
    public string FullVideo { get; init; } = "";
    public string ClipEnabled { get; init; } = "";
    public string StartLabel { get; init; } = "";
    public string EndLabel { get; init; } = "";
    public string StartTimeLabel { get; init; } = "";
    public string EndTimeLabel { get; init; } = "";

    public string OutputTitle { get; init; } = "";
    public string OutputDescription { get; init; } = "";
    public string FormatLabel { get; init; } = "";
    public string ResolutionLabel { get; init; } = "";
    public string TargetSizeHeader { get; init; } = "";
    public string OriginalQuality { get; init; } = "";
    public string CustomLimit { get; init; } = "";
    public string MbHeader { get; init; } = "";

    public string SaveLocationTitle { get; init; } = "";
    public string SaveLocationDescription { get; init; } = "";
    public string ChooseFolderTooltip { get; init; } = "";

    public string ReadyToDownloadTitle { get; init; } = "";
    public string ReadyToDownloadDescription { get; init; } = "";
    public string AddToDownloads { get; init; } = "";
    public string ClearCompletedTooltip { get; init; } = "";

    public string DownloadsTab { get; init; } = "";
    public string HistoryTab { get; init; } = "";
    public string SettingsTab { get; init; } = "";
    public string NoDownloadsTitle { get; init; } = "";
    public string NoDownloadsDescription { get; init; } = "";
    public string ProgressLabel { get; init; } = "";
    public string CancelTooltip { get; init; } = "";
    public string RetryTooltip { get; init; } = "";
    public string OpenFileTooltip { get; init; } = "";
    public string OpenFolderTooltip { get; init; } = "";

    public string HistoryEmptyTitle { get; init; } = "";
    public string HistoryEmptyDescription { get; init; } = "";
    public string RemoveFromHistoryTooltip { get; init; } = "";

    public string LanguageSectionTitle { get; init; } = "";
    public string LanguageSectionDescription { get; init; } = "";
    public string DownloadDefaultsTitle { get; init; } = "";
    public string DownloadDefaultsDescription { get; init; } = "";
    public string DefaultFormatHeader { get; init; } = "";
    public string DefaultResolutionHeader { get; init; } = "";
    public string ThemeHeader { get; init; } = "";
    public string ConcurrentDownloadsHeader { get; init; } = "";
    public string CookiesBrowserHeader { get; init; } = "";
    public string AppBehaviorTitle { get; init; } = "";
    public string AppBehaviorDescription { get; init; } = "";
    public string ClipboardMonitoringTitle { get; init; } = "";
    public string ClipboardMonitoringDescription { get; init; } = "";
    public string AutoAnalyzeTitle { get; init; } = "";
    public string AutoAnalyzeDescription { get; init; } = "";
    public string BrowserCookiesTitle { get; init; } = "";
    public string BrowserCookiesDescription { get; init; } = "";
    public string NotificationsTitle { get; init; } = "";
    public string NotificationsDescription { get; init; } = "";
    public string KeepRunningTitle { get; init; } = "";
    public string KeepRunningDescription { get; init; } = "";

    public string UpdateAvailableTitle { get; init; } = "";
    public string UpdateAvailableMessage { get; init; } = "";

    public string TrayShowClip { get; init; } = "";
    public string TrayPasteUrl { get; init; } = "";
    public string TrayDownloads { get; init; } = "";
    public string TraySettings { get; init; } = "";
    public string TrayQuit { get; init; } = "";

    public string QueueNoDownloadsYet { get; init; } = "";
    public string QueueNoActiveDownloads { get; init; } = "";
    public string QueueSummaryFormat { get; init; } = "";

    public string StatusQueued { get; init; } = "";
    public string StatusStarting { get; init; } = "";
    public string StatusDownloading { get; init; } = "";
    public string StatusComplete { get; init; } = "";
    public string StatusFailed { get; init; } = "";
    public string StatusCancelled { get; init; } = "";
    public string StatusCancelling { get; init; } = "";
    public string StatusCompressing { get; init; } = "";
    public string StatusPreparingDownload { get; init; } = "";
    public string StatusPreparingClip { get; init; } = "";
    public string StatusPreparingRange { get; init; } = "";
    public string StatusTryingFormat { get; init; } = "";
    public string StatusMergingStreams { get; init; } = "";
    public string StatusExtractingAudio { get; init; } = "";
    public string StatusRemuxingVideo { get; init; } = "";
    public string StatusFinalizingFile { get; init; } = "";
    public string StatusFileFitsTarget { get; init; } = "";
    public string StatusNeedsAccessTryingCookiesFormat { get; init; } = "";

    public string QueueSummary(int running, int queued)
    {
        return running == 0 && queued == 0
            ? QueueNoActiveDownloads
            : string.Format(CultureInfo.CurrentCulture, QueueSummaryFormat, running, queued);
    }

    public string StatusText(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return status;
        }

        var exact = status switch
        {
            "Queued" => StatusQueued,
            "Starting..." => StatusStarting,
            "Downloading..." => StatusDownloading,
            "Complete" => StatusComplete,
            "Failed" => StatusFailed,
            "Cancelled" => StatusCancelled,
            "Cancelling..." => StatusCancelling,
            "Compressing..." => StatusCompressing,
            "Preparing download..." => StatusPreparingDownload,
            "Preparing clip..." => StatusPreparingClip,
            "Preparing selected range..." => StatusPreparingRange,
            "Trying another available format..." => StatusTryingFormat,
            "Merging streams..." => StatusMergingStreams,
            "Extracting audio..." => StatusExtractingAudio,
            "Remuxing video..." => StatusRemuxingVideo,
            "Finalizing file..." => StatusFinalizingFile,
            "The file already fits the target size." => StatusFileFitsTarget,
            _ => null
        };

        if (exact is not null)
        {
            return exact;
        }

        if (status.StartsWith("Downloading...", StringComparison.OrdinalIgnoreCase))
        {
            return status.Replace("Downloading...", StatusDownloading, StringComparison.OrdinalIgnoreCase);
        }

        const string accessPrefix = "This video needs access. Trying ";
        const string accessSuffix = " cookies...";
        if (status.StartsWith(accessPrefix, StringComparison.OrdinalIgnoreCase) &&
            status.EndsWith(accessSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var browser = status[accessPrefix.Length..^accessSuffix.Length];
            return string.Format(CultureInfo.CurrentCulture, StatusNeedsAccessTryingCookiesFormat, browser);
        }

        return status;
    }
}
