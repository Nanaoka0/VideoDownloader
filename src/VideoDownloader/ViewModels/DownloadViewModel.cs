using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using VideoDownloader.Application.Services.UI;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.FileSystem;
using VideoDownloader.Domain.Services.Download;

namespace VideoDownloader.ViewModels;

public partial class DownloadViewModel : ReactiveObject
{
    private readonly IDownloadService _downloadService;
    private readonly IEventMessenger _messenger;
    private readonly IDownloadConfiguration _config;
    private readonly IUrlAnalysisService _urlAnalysisService;
    private readonly IFileCleanupService _fileCleanupService;
    private readonly ILogger _logger;

    [Reactive]
    private string _urlInput = string.Empty;

    [Reactive]
    private string _outputDirectory = string.Empty;

    [Reactive]
    private int _maxConcurrentDownloads = 1;

    [Reactive]
    private bool _preferHdr;

    [Reactive]
    private bool _openOutputFolder = true;

    [Reactive]
    private string _globalResolution = "批量设置分辨率";

    public IReadOnlyList<string> GlobalResolutionItems { get; } =
        new[] { "批量设置分辨率", "最高可用", "4K120fps", "4K60fps", "4K30fps", "4K24fps", "2K120fps", "2K60fps", "2K30fps", "2K24fps",
                "1080P120fps", "1080P60fps", "1080P30fps", "1080P24fps",
                "720P120fps", "720P60fps", "720P30fps", "720P24fps", "仅音频" };

    public IReadOnlyList<int> MaxConcurrentOptions { get; } =
        new[] { 1, 2, 3, 4, 5, 6, 7, 8 };

    public ObservableCollection<DownloadItemViewModel> Tasks { get; } = new();

    public Interaction<Unit, string?> PickOutputFolder { get; } = new();

    private SemaphoreSlim _concurrencySemaphore;

    public DownloadViewModel(
        IDownloadService downloadService,
        IEventMessenger messenger,
        IDownloadConfiguration config,
        IUrlAnalysisService urlAnalysisService,
        IFileCleanupService fileCleanupService,
        ILogger logger)
    {
        _downloadService = downloadService;
        _messenger = messenger;
        _config = config;
        _urlAnalysisService = urlAnalysisService;
        _fileCleanupService = fileCleanupService;
        _logger = logger;

        OutputDirectory = _config.OutputDirectory;
        MaxConcurrentDownloads = _config.MaxConcurrentDownloads;
        PreferHdr = _config.PreferHdr;
        _concurrencySemaphore = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);

        this.WhenAnyValue(x => x.GlobalResolution)
            .Where(r => !string.IsNullOrWhiteSpace(r) && r != "批量设置分辨率")
            .Subscribe(ApplyGlobalResolution);

        this.WhenAnyValue(x => x.OutputDirectory, x => x.MaxConcurrentDownloads, x => x.PreferHdr)
            .Throttle(TimeSpan.FromMilliseconds(400))
            .Subscribe(_ => SaveConfig());

        this.WhenAnyValue(x => x.MaxConcurrentDownloads)
            .Subscribe(v => _concurrencySemaphore = new SemaphoreSlim(v, v));

        _messenger.Subscribe<DownloadTaskCompletedMessage>(OnTaskCompleted);
    }

    [ReactiveCommand]
    public async Task AddUrls()
    {
        if (string.IsNullOrWhiteSpace(UrlInput)) return;

        _messenger.Send(new StatusUpdateMessage("正在解析链接..."));

        var inputUrls = UrlInput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .ToList();

        var allUrls = new List<string>();
        foreach (var url in inputUrls)
        {
            if (_urlAnalysisService.IsPlaylistUrl(url))
            {
                try
                {
                    var playlistUrls = await _downloadService.ParsePlaylistUrlsAsync(url, CancellationToken.None);
                    allUrls.AddRange(playlistUrls);
                }
                catch
                {
                    allUrls.Add(url);
                }
            }
            else
            {
                allUrls.Add(url);
            }
        }

        foreach (var url in allUrls)
        {
            var task = new DownloadTaskModel
            {
                Url = url,
                OutputPath = OutputDirectory,
                Status = DownloadTaskStatus.Pending,
                SiteName = new Uri(url).Host,
                Title = string.Empty
            };
            var item = new DownloadItemViewModel(task, _downloadService, _messenger, RemoveTask, _fileCleanupService, _logger, _concurrencySemaphore);
            Tasks.Add(item);
            _ = FetchTitleAsync(item, url);
        }

        UrlInput = string.Empty;
        _messenger.Send(new StatusUpdateMessage($"已添加 {allUrls.Count} 个任务"));
    }

    private async Task FetchTitleAsync(DownloadItemViewModel item, string url)
    {
        try
        {
            var title = await _downloadService.GetTitleAsync(url, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(title))
            {
                item.TaskTitle = title;
                item.Task.Title = title;
            }
        }
        catch (Exception ex)
        {
            _logger.LogException("Download", $"获取标题失败: {url}", ex);
            _messenger.Send(new StatusUpdateMessage($"获取标题失败: {ex.Message}"));
        }
    }

    [ReactiveCommand]
    public async Task FetchAllResolutions()
    {
        _messenger.Send(new StatusUpdateMessage("正在获取所有任务的分辨率..."));
        foreach (var item in Tasks.Where(t => t.Task.Status == DownloadTaskStatus.Pending))
            await item.FetchResolutionsCommand.Execute(Unit.Default).ToTask();
        _messenger.Send(new StatusUpdateMessage("分辨率获取完成"));
    }

    public void RemoveTask(DownloadItemViewModel item)
    {
        Tasks.Remove(item);
        item.Dispose();
    }

    [ReactiveCommand]
    public async Task BrowseOutputDirectory()
    {
        var folder = await PickOutputFolder.Handle(Unit.Default).ToTask();
        if (!string.IsNullOrWhiteSpace(folder))
            OutputDirectory = folder;
    }

    private void ApplyGlobalResolution(string filter)
    {
        foreach (var item in Tasks.Where(t => t.Task.Status == DownloadTaskStatus.Pending))
        {
            if (item.AvailableFormats.Count == 0) continue;
            var pick = filter switch
            {
                "仅音频" => item.AvailableFormats.FirstOrDefault(f => f.IsAudioOnly),
                "最高可用" => PickBest(item.AvailableFormats, PreferHdr),
                _ => PickNearest(item.AvailableFormats, filter, PreferHdr)
            };
            if (pick != null)
                item.SelectedFormat = pick;
        }
    }

    private static int EffectiveHeight(VideoFormatModel f) =>
        f.Width > 0 && f.Height > 0 ? Math.Min(f.Width, f.Height) : f.Height;

    private static VideoFormatModel? PickBest(IEnumerable<VideoFormatModel> formats, bool preferHdr)
    {
        var videoFormats = formats.Where(f => f.HasVideo).ToList();
        if (videoFormats.Count == 0) return formats.FirstOrDefault();

        var bestHeight = videoFormats.Max(EffectiveHeight);
        var candidates = videoFormats.Where(f => EffectiveHeight(f) == bestHeight).ToList();
        return preferHdr
            ? candidates.FirstOrDefault(f => f.IsHdr) ?? candidates.First()
            : candidates.FirstOrDefault(f => !f.IsHdr) ?? candidates.First();
    }

    private static VideoFormatModel? PickNearest(IEnumerable<VideoFormatModel> formats, string filter, bool preferHdr)
    {
        var (targetHeight, targetFps) = ParseFilter(filter);
        if (targetHeight == 0) return null;

        var videoFormats = formats.Where(f => f.HasVideo).ToList();
        if (videoFormats.Count == 0) return null;

        // Try exact height match (supports both landscape and portrait)
        var exact = videoFormats.Where(f => EffectiveHeight(f) == targetHeight).ToList();
        if (exact.Count > 0)
            return PickBestByFpsAndHdr(exact, targetFps, preferHdr);

        // Fall back to nearest lower height
        var lower = videoFormats.Where(f => EffectiveHeight(f) < targetHeight)
            .OrderByDescending(EffectiveHeight).ToList();
        if (lower.Count == 0) return null;
        var bestLowerHeight = EffectiveHeight(lower.First());
        var bestLower = lower.Where(f => EffectiveHeight(f) == bestLowerHeight).ToList();
        return PickBestByFpsAndHdr(bestLower, targetFps, preferHdr);
    }

    private static (int height, int fps) ParseFilter(string filter)
    {
        // Parse fps from end (e.g. "120fps" -> 120)
        var fps = 0;
        var fpsMatch = Regex.Match(filter, @"(\d+)fps$", RegexOptions.IgnoreCase);
        if (fpsMatch.Success) int.TryParse(fpsMatch.Groups[1].Value, out fps);

        // Parse height
        if (filter.StartsWith("4K", StringComparison.OrdinalIgnoreCase)) return (2160, fps);
        if (filter.StartsWith("2K", StringComparison.OrdinalIgnoreCase)) return (1440, fps);
        var heightMatch = Regex.Match(filter, @"(\d+)\s*P", RegexOptions.IgnoreCase);
        if (heightMatch.Success && int.TryParse(heightMatch.Groups[1].Value, out var h))
            return (h, fps);
        return (0, 0);
    }

    private static VideoFormatModel? PickBestByFpsAndHdr(IReadOnlyList<VideoFormatModel> candidates, int targetFps, bool preferHdr)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        // Try matching fps first
        if (targetFps > 0)
        {
            var fpsMatch = candidates.Where(f => f.Fps == targetFps).ToList();
            if (fpsMatch.Count > 0)
                return preferHdr
                    ? fpsMatch.FirstOrDefault(f => f.IsHdr) ?? fpsMatch.First()
                    : fpsMatch.FirstOrDefault(f => !f.IsHdr) ?? fpsMatch.First();
        }

        // Fall back to HDR preference
        return preferHdr
            ? candidates.FirstOrDefault(f => f.IsHdr) ?? candidates.First()
            : candidates.FirstOrDefault(f => !f.IsHdr) ?? candidates.First();
    }

    [ReactiveCommand]
    public async Task StartAllDownloads()
    {
        _messenger.Send(new StatusUpdateMessage("正在开始下载..."));
        var pending = Tasks.Where(t => t.Task.Status == DownloadTaskStatus.Pending).ToList();
        if (pending.Count == 0) return;
        foreach (var item in pending)
            _ = RunTaskAsync(item);
    }

    [ReactiveCommand]
    public void PauseAllTasks()
    {
        _messenger.Send(new StatusUpdateMessage("正在暂停所有任务..."));
        foreach (var item in Tasks.Where(t => t.Task.Status == DownloadTaskStatus.Downloading))
            _ = item.PauseCommand.Execute(Unit.Default).Subscribe(_ => { }, ex => _logger.LogException("Download", "暂停任务失败", ex));
        _messenger.Send(new StatusUpdateMessage("已暂停所有下载"));
    }

    [ReactiveCommand]
    public void DeleteAll()
    {
        _messenger.Send(new StatusUpdateMessage("正在删除所有任务..."));
        foreach (var item in Tasks.ToList())
            _ = item.DeleteCommand.Execute(Unit.Default).Subscribe(_ => { }, ex => _logger.LogException("Download", "删除任务失败", ex));
        _messenger.Send(new StatusUpdateMessage("已删除所有任务"));
    }

    private async Task RunTaskAsync(DownloadItemViewModel item)
    {
        await item.StartCommand.Execute(Unit.Default).ToTask();
    }

    public void SaveConfig()
    {
        _config.OutputDirectory = OutputDirectory;
        _config.MaxConcurrentDownloads = MaxConcurrentDownloads;
        _config.PreferHdr = PreferHdr;
        _config.Save();
    }

    private void OnTaskCompleted(DownloadTaskCompletedMessage msg)
    {
        if (msg.Success && OpenOutputFolder)
        {
            var anyRunning = Tasks.Any(t => t.Task.Status == DownloadTaskStatus.Downloading
                || t.Task.Status == DownloadTaskStatus.Waiting
                || t.Task.Status == DownloadTaskStatus.Parsing);
            if (!anyRunning)
            {
                var task = Tasks.FirstOrDefault(t => t.Task.Id == msg.TaskId);
                if (task != null)
                {
                    try { Process.Start("explorer.exe", task.Task.OutputPath); }
                    catch { }
                }
            }
        }
    }
}