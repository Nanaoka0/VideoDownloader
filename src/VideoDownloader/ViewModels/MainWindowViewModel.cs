using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Models;
using VideoDownloader.Services;

namespace VideoDownloader.ViewModels;

public partial class MainWindowViewModel : ReactiveObject
{
    private readonly IEventMessenger _messenger;
    private readonly IDialogService _dialogService;

    [Reactive]
    private string _statusText = "就绪";

    [Reactive]
    private double _globalProgress;

    [Reactive]
    private double _globalProgressBarWidth;

    [Reactive]
    private string _proxyInfo = string.Empty;

    private readonly List<IDisposable> _subscriptions = new();

    public ObservableCollection<PageViewModel> Pages { get; } = new();

    private PageViewModel? _selected;

    public PageViewModel? Selected
    {
        get => _selected;
        set => this.RaiseAndSetIfChanged(ref _selected, value);
    }

    public ReactiveCommand<PageViewModel, Unit> SelectTabCommand { get; }

    private IBrush? _selectedFill;

    /// <summary>当前选中页的填充色，供内容卡片背景绑定（避免 XAML 空链路径）。</summary>
    public IBrush? SelectedFill
    {
        get => _selectedFill;
        private set => this.RaiseAndSetIfChanged(ref _selectedFill, value);
    }

    private IBrush? _selectedBorderBrush;

    /// <summary>当前选中页的边框色，供内容卡片边框绑定。</summary>
    public IBrush? SelectedBorderBrush
    {
        get => _selectedBorderBrush;
        private set => this.RaiseAndSetIfChanged(ref _selectedBorderBrush, value);
    }

    private double _tabWidth = 112;

    public double TabWidth
    {
        get => _tabWidth;
        set => this.RaiseAndSetIfChanged(ref _tabWidth, value);
    }

    public DownloadViewModel DownloadViewModel { get; }
    public VideoConversionViewModel VideoConversionViewModel { get; }

    public MainWindowViewModel(
        DownloadViewModel downloadViewModel,
        VideoConversionViewModel videoConversionViewModel,
        IEventMessenger messenger,
        IDialogService dialogService)
    {
        DownloadViewModel = downloadViewModel;
        VideoConversionViewModel = videoConversionViewModel;
        _messenger = messenger;
        _dialogService = dialogService;

        Pages.Add(new DownloadPageViewModel(downloadViewModel));
        Pages.Add(new ConversionPageViewModel(videoConversionViewModel));
        SelectTabCommand = ReactiveCommand.Create<PageViewModel>(SelectTab);
        SelectTab(Pages[0]);

        _subscriptions.Add(_messenger.Subscribe<DownloadTaskProgressMessage>(OnDownloadProgress));
        _subscriptions.Add(_messenger.Subscribe<DownloadTaskStatusChangedMessage>(OnDownloadStatusChanged));
        _subscriptions.Add(_messenger.Subscribe<DependencyStatusChangedMessage>(OnDependencyChanged));
        _subscriptions.Add(_messenger.Subscribe<UiPromptMessage>(OnUiPrompt));
        _subscriptions.Add(_messenger.Subscribe<StatusUpdateMessage>(OnStatusUpdate));
    }

    private void SelectTab(PageViewModel page)
    {
        Selected = page;
        SelectedFill = page.Fill;
        SelectedBorderBrush = page.BorderBrush;
        foreach (var p in Pages)
        {
            p.IsSelected = ReferenceEquals(p, page);
        }
    }

    [ReactiveCommand]
    public async Task OpenSettings()
    {
        await _dialogService.ShowSettingsDialogAsync();
    }

    [ReactiveCommand]
    public async Task OpenAbout()
    {
        await _dialogService.ShowAboutDialogAsync();
    }

    private void OnDownloadProgress(DownloadTaskProgressMessage msg)
    {
        UpdateGlobalProgress();
        StatusText = $"正在下载... {GlobalProgress:F1}%";
    }

    private void OnDownloadStatusChanged(DownloadTaskStatusChangedMessage msg)
    {
        UpdateGlobalProgress();
        switch (msg.Status)
        {
            case DownloadTaskStatus.Completed:
                var anyActive = DownloadViewModel.Tasks.Any(t =>
                    t.Task.Status is DownloadTaskStatus.Downloading or DownloadTaskStatus.Waiting);
                StatusText = anyActive ? $"正在下载... {GlobalProgress:F1}%" : "下载完成";
                break;
            case DownloadTaskStatus.Failed:
                StatusText = "下载失败";
                break;
            case DownloadTaskStatus.Cancelled:
                StatusText = "下载已取消";
                break;
            case DownloadTaskStatus.Paused:
                StatusText = "下载已暂停";
                break;
        }
    }

    /// <summary>左下角进度条：全部已开始任务（下载中/排队中/已暂停/已完成）按大小合计的全局进度；排队任务用 -F 预估大小计入，大小未知的任务不计入。</summary>
    private void UpdateGlobalProgress()
    {
        long totalBytes = 0;
        long downloadedBytes = 0;
        foreach (var item in DownloadViewModel.Tasks)
        {
            var task = item.Task;
            if (task.Status is DownloadTaskStatus.Cancelled or DownloadTaskStatus.Failed) continue;
            if (task.TotalBytes <= 0) continue;
            totalBytes += task.TotalBytes;
            downloadedBytes += task.Status == DownloadTaskStatus.Completed
                ? task.TotalBytes
                : Math.Clamp(task.DownloadedBytes, 0, task.TotalBytes);
        }

        var percent = totalBytes > 0 ? Math.Clamp(downloadedBytes * 100.0 / totalBytes, 0, 100) : 0;
        GlobalProgress = percent;
        GlobalProgressBarWidth = percent * 1.4;
    }

    private void OnDependencyChanged(DependencyStatusChangedMessage msg)
    {
        ProxyInfo = msg.Status.AllAvailable ? "所有依赖就绪" : "部分依赖缺失";
    }

    private void OnUiPrompt(UiPromptMessage msg)
    {
        _ = _dialogService.ShowMessageAsync(msg.Title, msg.Message);
    }

    private void OnStatusUpdate(StatusUpdateMessage msg)
    {
        StatusText = msg.Text;
    }
}