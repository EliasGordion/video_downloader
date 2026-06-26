using System.Collections.ObjectModel;
using Clip.Models;
using Clip.Services;
using Microsoft.UI.Dispatching;

namespace Clip.ViewModels;

public sealed class DownloadViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly SettingsViewModel _settings;
    private readonly YTDLPService _ytdlpService;
    private readonly FFmpegService _ffmpegService;
    private readonly SettingsService _settingsService;
    private readonly FileSystemService _fileSystemService;
    private readonly ToastService _toastService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly object _queueLock = new();
    private readonly SemaphoreSlim _historySaveLock = new(1, 1);
    private int _activeCount;
    private string _queueSummary;

    public DownloadViewModel(
        MainViewModel main,
        SettingsViewModel settings,
        YTDLPService ytdlpService,
        FFmpegService ffmpegService,
        SettingsService settingsService,
        FileSystemService fileSystemService,
        ToastService toastService,
        DispatcherQueue dispatcherQueue)
    {
        _main = main;
        _settings = settings;
        _ytdlpService = ytdlpService;
        _ffmpegService = ffmpegService;
        _settingsService = settingsService;
        _fileSystemService = fileSystemService;
        _toastService = toastService;
        _dispatcherQueue = dispatcherQueue;
        _queueSummary = Text.QueueNoDownloadsYet;
        Downloads.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDownloads));
        History.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasHistory));
        _settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.Text))
            {
                OnPropertyChanged(nameof(Text));
                RefreshLocalizedItems();
                UpdateQueueSummary();
            }
        };

        EnqueueCurrentCommand = new RelayCommand(_ => EnqueueCurrent());
        CancelCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is DownloadItem item)
                {
                    Cancel(item);
                }
            },
            parameter => parameter is DownloadItem { CanCancel: true });
        RetryCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is DownloadItem item)
                {
                    Retry(item);
                }
            },
            parameter => parameter is DownloadItem { CanRetry: true });
        OpenFileCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is DownloadItem item)
                {
                    var resolvedPath = ResolveItemOutputPath(item);
                    if (!_fileSystemService.OpenFile(resolvedPath))
                    {
                        item.ErrorMessage = Text.DownloadedFileCouldNotBeOpened;
                    }
                }
            },
            parameter => parameter is DownloadItem { CanOpenFile: true });
        OpenFolderCommand = new RelayCommand(parameter =>
            {
                if (parameter is DownloadItem item)
                {
                    var resolvedPath = ResolveItemOutputPath(item);
                    if (!_fileSystemService.OpenContainingFolder(resolvedPath, item.SaveFolder))
                    {
                        item.ErrorMessage = Text.DownloadFolderCouldNotBeOpened;
                    }
                }
            });
        ClearCompletedCommand = new RelayCommand(ClearCompleted);
        RemoveHistoryItemCommand = new AsyncRelayCommand(
            async parameter =>
            {
                if (parameter is DownloadItem item)
                {
                    await RemoveHistoryItemAsync(item);
                }
            },
            parameter => parameter is DownloadItem);
    }

    public ObservableCollection<DownloadItem> Downloads { get; } = [];
    public ObservableCollection<DownloadItem> History { get; } = [];
    public bool HasDownloads => Downloads.Count > 0;
    public bool HasHistory => History.Count > 0;
    public AppText Text => _settings.Text;

    public RelayCommand EnqueueCurrentCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand RetryCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand ClearCompletedCommand { get; }
    public AsyncRelayCommand RemoveHistoryItemCommand { get; }

    public string QueueSummary
    {
        get => _queueSummary;
        set => SetProperty(ref _queueSummary, value);
    }

    public async Task InitializeAsync()
    {
        var history = await DownloadHistory.LoadAsync(_settingsService.HistoryPath);
        var historyChanged = false;

        foreach (var item in history.Items.OrderByDescending(item => item.CreatedAt).Take(200))
        {
            item.IsRunning = false;
            item.IsCancelled = false;

            var resolvedPath = ResolveItemOutputPath(item);
            if (!string.IsNullOrWhiteSpace(resolvedPath) &&
                item.Status == "Failed" &&
                item.ErrorMessage.Contains(
                    "finished without reporting the output file",
                    StringComparison.OrdinalIgnoreCase))
            {
                item.OutputPath = resolvedPath;
                item.Progress = 100;
                item.Status = "Complete";
                item.IsCompleted = true;
                item.CompletedAt ??= new FileInfo(resolvedPath).LastWriteTime;
                item.ErrorMessage = "";
                historyChanged = true;
            }

            History.Add(item);
        }

        if (historyChanged)
        {
            try
            {
                await new DownloadHistory { Items = History.ToList() }
                    .SaveAtomicAsync(_settingsService.HistoryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        UpdateQueueSummary();
    }

    public void EnqueueCurrent()
    {
        if (!_main.TryGetCurrentVideoUrl(out var normalizedUrl, out var detectedPlatform))
        {
            _main.StatusMessage = Text.PasteValidVideoUrlBeforeDownloading;
            return;
        }

        var metadata = _main.Metadata;
        var item = new DownloadItem
        {
            Url = normalizedUrl,
            Title = string.IsNullOrWhiteSpace(metadata?.Title) ? Text.VideoFallbackTitle : metadata.Title,
            Platform = metadata?.Platform ?? detectedPlatform,
            SaveFolder = _settings.DefaultSaveFolder,
            Format = _main.SelectedFormat.Extension,
            Resolution = _main.SelectedResolution,
            TargetSizeMb = _main.IsCustomTargetSize ? _main.CustomTargetSizeMb : null,
            UseBrowserCookies = _settings.UseBrowserCookies,
            PreferredBrowser = _settings.PreferredBrowser,
            IsClip = _main.IsClipEnabled,
            ClipRange = _main.IsClipEnabled ? _main.ClipRange.Clone() : null,
            Status = "Queued"
        };

        try
        {
            if (string.IsNullOrWhiteSpace(item.SaveFolder))
            {
                throw new DirectoryNotFoundException(Text.ChooseSaveFolderFirst);
            }

            Directory.CreateDirectory(item.SaveFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   ArgumentException or NotSupportedException)
        {
            _main.StatusMessage = $"{Text.CouldNotUseSaveFolderPrefix}{ex.Message}";
            return;
        }

        Downloads.Insert(0, item);
        _main.StatusMessage = Text.AddedToDownloads;
        StartNextQueued();
    }

    public void Cancel(DownloadItem item)
    {
        if (!item.CanCancel)
        {
            return;
        }

        item.IsCancelled = true;
        item.ErrorMessage = "";

        if (!item.IsRunning)
        {
            item.Status = "Cancelled";
            item.Progress = 0;
            StartNextQueued();
            return;
        }

        item.Status = "Cancelling...";
        item.Cancellation?.Cancel();
    }

    public void Retry(DownloadItem item)
    {
        if (!item.CanRetry)
        {
            return;
        }

        var retry = item.CloneForRetry();
        retry.Status = "Queued";
        retry.Progress = 0;
        Downloads.Insert(0, retry);
        StartNextQueued();
    }

    public void ClearCompleted()
    {
        var completed = Downloads.Where(item => !item.IsRunning && item.IsCompleted).ToList();
        foreach (var item in completed)
        {
            Downloads.Remove(item);
        }

        UpdateQueueSummary();
    }

    public void CancelAll()
    {
        foreach (var item in Downloads.Where(item => !item.IsRunning && item.Status == "Queued"))
        {
            item.IsCancelled = true;
            item.Status = "Cancelled";
            item.Progress = 0;
        }

        foreach (var item in Downloads.Where(item => item.IsRunning))
        {
            item.IsCancelled = true;
            item.Status = "Cancelling...";
            item.Cancellation?.Cancel();
        }

        UpdateQueueSummary();
    }

    private void StartNextQueued()
    {
        var toStart = new List<DownloadItem>();

        lock (_queueLock)
        {
            var max = Math.Clamp(_settings.MaxConcurrentDownloads, 1, ClipConstants.MaxConcurrentDownloadsLimit);
            while (_activeCount < max)
            {
                var next = Downloads.FirstOrDefault(item => item.Status == "Queued");
                if (next is null)
                {
                    break;
                }

                next.Status = "Starting...";
                next.IsRunning = true;
                _activeCount++;
                toStart.Add(next);
            }
        }

        foreach (var item in toStart)
        {
            _ = RunDownloadAsync(item);
        }

        UpdateQueueSummary();
    }

    private async Task RunDownloadAsync(DownloadItem item)
    {
        item.Cancellation = new CancellationTokenSource();

        try
        {
            item.Cancellation.Token.ThrowIfCancellationRequested();
            if (item.IsCancelled)
            {
                throw new OperationCanceledException(item.Cancellation.Token);
            }

            await RunOnUiAsync(() =>
            {
                item.Status = "Downloading...";
                item.Progress = 0;
            });

            var outputPath = await _ytdlpService.DownloadAsync(
                item,
                progress => _dispatcherQueue.TryEnqueue(() =>
                {
                    item.Progress = progress.Percent;
                    item.Status = progress.Message;
                    if (!string.IsNullOrWhiteSpace(progress.OutputPath))
                    {
                        item.OutputPath = progress.OutputPath;
                    }
                }),
                item.Cancellation.Token);

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                item.OutputPath = outputPath;
            }

            if (item.TargetSizeMb is > 0 && File.Exists(item.OutputPath))
            {
                await RunOnUiAsync(() => item.Status = "Compressing...");
                item.OutputPath = await _ffmpegService.CompressToTargetSizeAsync(
                    item.OutputPath,
                    item.TargetSizeMb.Value,
                    message => _dispatcherQueue.TryEnqueue(() => item.Status = message),
                    item.Cancellation.Token);
            }

            await RunOnUiAsync(() =>
            {
                item.Progress = 100;
                item.Status = "Complete";
                item.IsCompleted = true;
                item.CompletedAt = DateTimeOffset.Now;
            });

            await AddHistoryItemAsync(item);

            if (_settings.ShowToastNotifications)
            {
                _toastService.Show(Text.DownloadCompleteTitle, item.Title);
            }
        }
        catch (OperationCanceledException)
        {
            await RunOnUiAsync(() =>
            {
                item.Status = "Cancelled";
                item.ErrorMessage = "";
                item.Progress = 0;
            });
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() =>
            {
                item.Status = "Failed";
                item.ErrorMessage = ex.Message;
            });

            await AddHistoryItemAsync(item);

            if (_settings.ShowToastNotifications)
            {
                _toastService.Show(Text.DownloadFailedTitle, ex.Message);
            }
        }
        finally
        {
            item.Cancellation?.Dispose();
            item.Cancellation = null;

            await RunOnUiAsync(() =>
            {
                item.IsRunning = false;
                lock (_queueLock)
                {
                    _activeCount = Math.Max(0, _activeCount - 1);
                }

                StartNextQueued();
            });
        }
    }

    private async Task AddHistoryItemAsync(DownloadItem item)
    {
        await _historySaveLock.WaitAsync();
        try
        {
            List<DownloadItem>? items = null;
            await RunOnUiAsync(() =>
            {
                var existing = History.FirstOrDefault(historyItem => historyItem.Id == item.Id);
                if (existing is not null)
                {
                    History.Remove(existing);
                }

                History.Insert(0, item);
                while (History.Count > 200)
                {
                    History.RemoveAt(History.Count - 1);
                }

                items = History.ToList();
            });

            var history = new DownloadHistory { Items = items ?? [] };
            try
            {
                await history.SaveAtomicAsync(_settingsService.HistoryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        finally
        {
            _historySaveLock.Release();
        }
    }

    private async Task RemoveHistoryItemAsync(DownloadItem item)
    {
        await _historySaveLock.WaitAsync();
        try
        {
            List<DownloadItem>? items = null;
            var removed = false;

            await RunOnUiAsync(() =>
            {
                var existing = History.FirstOrDefault(historyItem =>
                    ReferenceEquals(historyItem, item) ||
                    string.Equals(historyItem.Id, item.Id, StringComparison.Ordinal));

                if (existing is null)
                {
                    items = History.ToList();
                    return;
                }

                History.Remove(existing);
                items = History.ToList();
                removed = true;
            });

            if (!removed)
            {
                return;
            }

            var history = new DownloadHistory { Items = items ?? [] };
            try
            {
                await history.SaveAtomicAsync(_settingsService.HistoryPath);
                _main.StatusMessage = Text.RemovedFromHistory;
            }
            catch (IOException)
            {
                _main.StatusMessage = Text.CouldNotUpdateHistory;
            }
            catch (UnauthorizedAccessException)
            {
                _main.StatusMessage = Text.CouldNotUpdateHistory;
            }
        }
        finally
        {
            _historySaveLock.Release();
        }
    }

    private string? ResolveItemOutputPath(DownloadItem item)
    {
        var resolvedPath = _fileSystemService.ResolveDownloadedFile(
            item.OutputPath,
            item.SaveFolder,
            createdAt: item.CreatedAt);

        if (!string.IsNullOrWhiteSpace(resolvedPath) &&
            !string.Equals(item.OutputPath, resolvedPath, StringComparison.Ordinal))
        {
            item.OutputPath = resolvedPath;
        }

        return resolvedPath;
    }

    private Task RunOnUiAsync(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var source = new TaskCompletionSource();
        if (!_dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                source.SetResult();
            }
            catch (Exception ex)
            {
                source.SetException(ex);
            }
        }))
        {
            source.SetException(new InvalidOperationException("The application window is no longer available."));
        }

        return source.Task;
    }

    private void UpdateQueueSummary()
    {
        var running = Downloads.Count(item => item.IsRunning);
        var queued = Downloads.Count(item => item.Status == "Queued");
        QueueSummary = Text.QueueSummary(running, queued);
    }

    private void RefreshLocalizedItems()
    {
        foreach (var item in Downloads)
        {
            item.RefreshLocalizedText();
        }

        foreach (var item in History)
        {
            item.RefreshLocalizedText();
        }
    }
}
