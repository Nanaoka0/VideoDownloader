using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        GlobalProgress = msg.Progress;
        StatusText = $"正在下载... {msg.Progress:F1}%";
    }

    private void OnDownloadStatusChanged(DownloadTaskStatusChangedMessage msg)
    {
        switch (msg.Status)
        {
            case DownloadTaskStatus.Completed:
                GlobalProgress = 100;
                StatusText = "下载完成";
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