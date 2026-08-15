using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services.Dependency;

namespace VideoDownloader.ViewModels;

public partial class DependencyViewModel : ReactiveObject
{
    private readonly IDependencyService _dependencyService;
    private readonly IEventMessenger _messenger;

    [Reactive]
    private DependencyStatusModel _status = new();

    [Reactive]
    private bool _isChecking;

    [Reactive]
    private bool _isDownloading;

    [Reactive]
    private double _downloadProgress;

    [Reactive]
    private string _downloadStatusText = string.Empty;

    private IObservable<bool> CanDownloadFfmpeg => this
        .WhenAnyValue(x => x.Status, s => s.Ffmpeg is not null && !s.Ffmpeg.IsAvailable);

    private IObservable<bool> CanDownloadYtDlp => this
        .WhenAnyValue(x => x.Status, s => s.YtDlp is not null && !s.YtDlp.IsAvailable);

    private IObservable<bool> CanCheckDependencies => this
        .WhenAnyValue(x => x.IsChecking, x => x.IsDownloading, (checking, downloading) => !checking && !downloading);

    public DependencyViewModel(
        IDependencyService dependencyService,
        IEventMessenger messenger)
    {
        _dependencyService = dependencyService;
        _messenger = messenger;
    }

    [ReactiveCommand(CanExecute = nameof(CanCheckDependencies))]
    public async Task CheckDependencies()
    {
        IsChecking = true;
        try
        {
            Status = await _dependencyService.CheckDependenciesAsync(CancellationToken.None);
        }
        finally
        {
            IsChecking = false;
        }
    }

    [ReactiveCommand(CanExecute = nameof(CanDownloadFfmpeg))]
    public async Task DownloadFfmpeg()
    {
        await DownloadToolAsync("ffmpeg", Status.Ffmpeg.ExecutablePath);
    }

    [ReactiveCommand(CanExecute = nameof(CanDownloadYtDlp))]
    public async Task DownloadYtDlp()
    {
        await DownloadToolAsync("yt-dlp", Status.YtDlp.ExecutablePath);
    }

    private async Task DownloadToolAsync(string toolName, string destinationPath)
    {
        IsDownloading = true;
        var note = toolName == "ffmpeg" ? $"（{_dependencyService.DescribeFfmpegSource()}）" : string.Empty;
        DownloadStatusText = $"正在下载 {toolName}{note}...";
        DownloadProgress = 0;

        var progress = new Progress<double>(p =>
        {
            DownloadProgress = p;
            DownloadStatusText = $"正在下载 {toolName}{note}... {p:F1}%";
        });

        try
        {
            var success = await _dependencyService.DownloadToolAsync(toolName, destinationPath, progress, CancellationToken.None);
            if (success)
            {
                DownloadStatusText = $"{toolName} 下载完成";
                _messenger.Send(new UiPromptMessage("下载完成", $"{toolName} 下载完成"));
                await CheckDependencies();
            }
            else
            {
                DownloadStatusText = $"{toolName} 下载失败";
                _messenger.Send(new UiPromptMessage("下载失败", $"{toolName} 下载失败"));
            }
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            DownloadStatusText = $"下载失败: {msg}";
            _messenger.Send(new UiPromptMessage("下载失败", $"{toolName}\n{msg}"));
        }
        finally
        {
            IsDownloading = false;
        }
    }
}