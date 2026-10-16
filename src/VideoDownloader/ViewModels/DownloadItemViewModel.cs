using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Services.Download;
using VideoDownloader.Domain.Services.FileSystem;

namespace VideoDownloader.ViewModels;

public partial class DownloadItemViewModel : ReactiveObject, IDisposable
{
    private readonly IDownloadService _downloadService;
    private readonly IEventMessenger _messenger;
    private readonly IFileCleanupService _fileCleanupService;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private CancellationTokenSource? _cts;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly Action<DownloadItemViewModel> _onRemove;

    [Reactive]
    private DownloadTaskModel _task;

    [Reactive]
    private bool _isRunning;

    [Reactive]
    private string _statusDisplay = "待添加";

    [Reactive]
    private double _progress;

    [Reactive]
    private string _taskTitle = string.Empty;

    [Reactive]
    private string _progressDisplay = "-";

    [Reactive]
    private string _sizeDisplay = "- / -";

    [Reactive]
    private string _speedDisplay = "-";

    [Reactive]
    private VideoFormatModel? _selectedFormat;

    [Reactive]
    private string _startActionTip = "开始";

    public ObservableCollection<VideoFormatModel> AvailableFormats { get; } = new();

    public DownloadItemViewModel(
        DownloadTaskModel task,
        IDownloadService downloadService,
        IEventMessenger messenger,
        Action<DownloadItemViewModel> onRemove,
        IFileCleanupService fileCleanupService,
        ILogger logger,
        SemaphoreSlim concurrencySemaphore)
    {
        _task = task;
        _downloadService = downloadService;
        _messenger = messenger;
        _onRemove = onRemove;
        _fileCleanupService = fileCleanupService;
        _logger = logger;
        _concurrencySemaphore = concurrencySemaphore;
        _selectedFormat = task.SelectedFormat;
        _taskTitle = task.Title;

        _subscriptions.Add(this.WhenAnyValue(x => x.SelectedFormat)
            .Where(f => f != null)
            .Subscribe(f =>
            {
                Task.SelectedFormat = f;
                ApplyEstimatedSize();
            }));
        _subscriptions.Add(_messenger.Subscribe<DownloadTaskProgressMessage>(OnProgress));
        _subscriptions.Add(_messenger.Subscribe<DownloadTaskStatusChangedMessage>(OnStatusChanged));
    }

    [ReactiveCommand]
    public async Task FetchResolutions()
    {
        if (IsRunning) return;
        await FetchResolutionsCoreAsync();
    }

    private async Task FetchResolutionsCoreAsync()
    {
        _messenger.Send(new StatusUpdateMessage("正在获取分辨率..."));

        Task.Status = DownloadTaskStatus.Parsing;
        UpdateStatusDisplay();
        try
        {
            var parsed = await _downloadService.ParseUrlAsync(Task.Url, Task.OutputPath, CancellationToken.None, Task.Title);
            AvailableFormats.Clear();
            foreach (var f in Enumerable.Reverse(parsed.AvailableFormats))
                AvailableFormats.Add(f);
            Task.AvailableFormats = parsed.AvailableFormats;
            SelectedFormat = null;
            Task.Title = parsed.Title;
            TaskTitle = parsed.Title;
            Task.Status = DownloadTaskStatus.Pending;
            _messenger.Send(new StatusUpdateMessage("分辨率获取完成"));
        }
        catch (TimeoutException ex)
        {
            Task.Status = DownloadTaskStatus.Pending;
            _messenger.Send(new UiPromptMessage("获取分辨率超时", $"{Task.Title}\n{ex.Message}"));
        }
        catch (Exception ex)
        {
            Task.Status = DownloadTaskStatus.Pending;
            _messenger.Send(new UiPromptMessage("获取分辨率失败", $"{Task.Title}\n{ex.Message}"));
        }
        UpdateStatusDisplay();
    }

    [ReactiveCommand]
    public async Task Start()
    {
        if (IsRunning) return;
        if (Task.Status == DownloadTaskStatus.Paused)
        {
            await Resume();
            return;
        }

        IsRunning = true;
        Task.Status = DownloadTaskStatus.Waiting;
        if (SelectedFormat == null && AvailableFormats.Count > 0)
            SelectedFormat = Enumerable.FirstOrDefault(AvailableFormats, f => f.HasVideo) ?? AvailableFormats[0];
        ApplyEstimatedSize();
        UpdateStatusDisplay();
        _messenger.Send(new DownloadTaskStatusChangedMessage(Task.Id, Task.Status));

        await _concurrencySemaphore.WaitAsync();
        try
        {
            if (Task.Status != DownloadTaskStatus.Waiting) return;

            if (AvailableFormats.Count == 0)
            {
                await FetchResolutionsCoreAsync();
                Task.Status = DownloadTaskStatus.Waiting;
                if (SelectedFormat == null && AvailableFormats.Count > 0)
                    SelectedFormat = Enumerable.FirstOrDefault(AvailableFormats, f => f.HasVideo) ?? AvailableFormats[0];
                ApplyEstimatedSize();
                UpdateStatusDisplay();
            }

            if (SelectedFormat == null)
            {
                Task.Status = DownloadTaskStatus.Pending;
                _messenger.Send(new DownloadTaskStatusChangedMessage(Task.Id, Task.Status));
                return;
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            Progress = 0;
            ProgressDisplay = "-";
            SpeedDisplay = "-";
            Task.Progress = 0;
            Task.DownloadedBytes = 0;

            try
            {
                await _downloadService.StartDownloadAsync(Task, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Task.Status = DownloadTaskStatus.Cancelled;
            }
            catch (Exception ex)
            {
                Task.Status = DownloadTaskStatus.Pending;
                Task.ErrorMessage = ex.Message;
            }
        }
        finally
        {
            IsRunning = false;
            _concurrencySemaphore.Release();
            UpdateStatusDisplay();
        }
    }

    [ReactiveCommand]
    public async Task Pause()
    {
        await _downloadService.PauseDownloadAsync(Task);
        UpdateStatusDisplay();
    }

    [ReactiveCommand]
    public async Task Resume()
    {
        if (IsRunning) return;
        if (Task.Status != DownloadTaskStatus.Paused) return;

        IsRunning = true;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        Task.Status = DownloadTaskStatus.Waiting;
        UpdateStatusDisplay();
        _messenger.Send(new DownloadTaskStatusChangedMessage(Task.Id, Task.Status));

        await _concurrencySemaphore.WaitAsync();
        try
        {
            if (Task.Status != DownloadTaskStatus.Waiting) return;

            try
            {
                await _downloadService.ResumeDownloadAsync(Task, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Task.Status = DownloadTaskStatus.Cancelled;
            }
            catch (Exception ex)
            {
                Task.Status = DownloadTaskStatus.Pending;
                Task.ErrorMessage = ex.Message;
            }
        }
        finally
        {
            IsRunning = false;
            _concurrencySemaphore.Release();
            UpdateStatusDisplay();
        }
    }

    [ReactiveCommand]
    public async Task Delete()
    {
        try
        {
            _cts?.Cancel();
            await _downloadService.CancelDownloadAsync(Task);
        }
        catch (Exception ex)
        {
            _logger.LogException("Download", $"删除任务失败: {Task.Title}", ex);
        }
        finally
        {
            try { _fileCleanupService.CleanTempFiles(Task); }
            catch (Exception ex) { _logger.LogException("Download", "删除任务时清理临时文件失败", ex); }
            _onRemove(this);
        }
    }

    /// <summary>排队阶段把所选格式（视频流 + 自动搭配的最佳音频流）的 -F 大小合计预填到任务大小显示；下载开始后被真实值覆盖。</summary>
    private void ApplyEstimatedSize()
    {
        if (Task.Status != DownloadTaskStatus.Waiting) return;

        var (bytes, estimated) = EstimateSelectionSize();
        Task.TotalBytes = bytes;
        SizeDisplay = bytes > 0
            ? $"- / {(estimated ? "≈" : "")}{FormatBytes(bytes)}"
            : "- / -";
    }

    /// <summary>合计所选视频流与将自动搭配的音频流大小（搭配音频大小由 DownloadService 解析后预匹配到 CompanionAudioSize，规则与下载时一致）。</summary>
    private (long Bytes, bool Estimated) EstimateSelectionSize()
    {
        if (SelectedFormat == null || SelectedFormat.FileSize <= 0) return (0, false);

        return (SelectedFormat.FileSize + SelectedFormat.CompanionAudioSize,
            SelectedFormat.FileSizeEstimated || SelectedFormat.CompanionAudioEstimated);
    }

    private void OnProgress(DownloadTaskProgressMessage msg)
    {
        if (msg.TaskId != Task.Id) return;
        Progress = msg.Progress;
        ProgressDisplay = $"{msg.Progress:F1}%";
        SizeDisplay = $"{FormatBytes(msg.DownloadedBytes)} / {FormatBytes(msg.TotalBytes)}";
        SpeedDisplay = FormatSpeed(msg.Speed);
    }

    private void OnStatusChanged(DownloadTaskStatusChangedMessage msg)
    {
        if (msg.TaskId != Task.Id) return;
        Task.Status = msg.Status;
        if (msg.Status == DownloadTaskStatus.Completed)
        {
            Progress = 100;
            ProgressDisplay = "100.0%";
        }
        else if (msg.Status == DownloadTaskStatus.Cancelled)
        {
            Progress = 0;
            ProgressDisplay = "-";
        }
        UpdateStatusDisplay();
    }

    private void UpdateStatusDisplay()
    {
        StatusDisplay = Task.Status switch
        {
            DownloadTaskStatus.Pending => "就绪",
            DownloadTaskStatus.Parsing => "解析中...",
            DownloadTaskStatus.Waiting => "排队中",
            DownloadTaskStatus.Downloading => "下载中...",
            DownloadTaskStatus.Paused => "已暂停",
            DownloadTaskStatus.Completed => "已完成",
            DownloadTaskStatus.Cancelled => "已取消",
            DownloadTaskStatus.Failed => $"失败: {Task.ErrorMessage}",
            _ => "未知"
        };
        StartActionTip = Task.Status == DownloadTaskStatus.Paused ? "继续" : "开始";
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    private static string FormatSpeed(double bytesPerSecond) => bytesPerSecond switch
    {
        < 1024 => $"{bytesPerSecond:F1} B/s",
        < 1024 * 1024 => $"{bytesPerSecond / 1024.0:F1} KB/s",
        < 1024 * 1024 * 1024 => $"{bytesPerSecond / (1024.0 * 1024):F1} MB/s",
        _ => $"{bytesPerSecond / (1024.0 * 1024 * 1024):F2} GB/s"
    };

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
    }
}