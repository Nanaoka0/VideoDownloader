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
using VideoDownloader.Domain.Extensions;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services.Conversion;

namespace VideoDownloader.ViewModels;

public partial class VideoConversionItemViewModel : ReactiveObject, IDisposable
{
    private readonly IVideoConversionService _conversionService;
    private readonly IVideoEncoderCatalog _encoderCatalog;
    private readonly IEventMessenger _messenger;
    private readonly ILogger _logger;
    private readonly Action<VideoConversionItemViewModel> _onRemove;
    private CancellationTokenSource? _cts;
    private readonly List<IDisposable> _subscriptions = new();

    [Reactive]
    private VideoConversionTaskModel _task;

    [Reactive]
    private bool _isRunning;

    [Reactive]
    private string _statusDisplay = "待转换";

    [Reactive]
    private string _progressDisplay = "-";

    [Reactive]
    private VideoContainer _selectedContainer;

    [Reactive]
    private VideoCodec _selectedVideoCodec;

    [Reactive]
    private AudioCodec _selectedAudioCodec;

    [Reactive]
    private VideoEncoderInfo? _selectedEncoder;

    public ObservableCollection<VideoContainer> AvailableContainers { get; } = new(VideoContainerExtensions.AllContainers);
    public ObservableCollection<VideoCodec> AvailableVideoCodecs { get; private set; } = new();
    public ObservableCollection<AudioCodec> AvailableAudioCodecs { get; private set; } = new();
    public ObservableCollection<VideoEncoderInfo> AvailableEncoders { get; private set; } = new();

    public VideoConversionItemViewModel(
        VideoConversionTaskModel task,
        IVideoConversionService conversionService,
        IVideoEncoderCatalog encoderCatalog,
        IEventMessenger messenger,
        ILogger logger,
        Action<VideoConversionItemViewModel> onRemove)
    {
        _task = task;
        _conversionService = conversionService;
        _encoderCatalog = encoderCatalog;
        _messenger = messenger;
        _logger = logger;
        _onRemove = onRemove;

        _selectedContainer = task.Container;

        // Populate lists before setting selections to avoid ComboBox null binding
        UpdateVideoCodecs();
        UpdateAudioCodecs();

        _selectedVideoCodec = AvailableVideoCodecs.Contains(task.VideoCodec) ? task.VideoCodec : AvailableVideoCodecs[0];
        _selectedAudioCodec = AvailableAudioCodecs.Contains(task.AudioCodec) ? task.AudioCodec : AvailableAudioCodecs[0];
        UpdateEncoders();

        _subscriptions.Add(this.WhenAnyValue(x => x.SelectedContainer)
            .Subscribe(container =>
            {
                Task.Container = container;
                UpdateVideoCodecs();
                UpdateAudioCodecs();
            }));
        _subscriptions.Add(this.WhenAnyValue(x => x.SelectedVideoCodec)
            .Subscribe(codec =>
            {
                Task.VideoCodec = codec;
                UpdateEncoders();
            }));
        _subscriptions.Add(this.WhenAnyValue(x => x.SelectedAudioCodec)
            .Subscribe(codec => Task.AudioCodec = codec));
        _subscriptions.Add(this.WhenAnyValue(x => x.SelectedEncoder)
            .Subscribe(enc =>
            {
                if (enc != null) Task.VideoEncoderName = enc.Name;
            }));
        _subscriptions.Add(_messenger.Subscribe<ConversionTaskProgressMessage>(OnProgress));
        _subscriptions.Add(_messenger.Subscribe<ConversionTaskStatusChangedMessage>(OnStatusChanged));
    }

    private void UpdateVideoCodecs()
    {
        var newCodecs = new List<VideoCodec>();
        if (SelectedContainer == VideoContainer.WebM)
        {
            newCodecs.Add(VideoCodec.VP9);
            newCodecs.Add(VideoCodec.AV1);
        }
        else
        {
            newCodecs.Add(VideoCodec.H264);
            newCodecs.Add(VideoCodec.H265);
            newCodecs.Add(VideoCodec.VP9);
            newCodecs.Add(VideoCodec.AV1);
        }

        if (!newCodecs.Contains(SelectedVideoCodec))
            SelectedVideoCodec = newCodecs[0];

        AvailableVideoCodecs = new ObservableCollection<VideoCodec>(newCodecs);
        this.RaisePropertyChanged(nameof(AvailableVideoCodecs));
    }

    private void UpdateAudioCodecs()
    {
        List<AudioCodec> newCodecs;
        AudioCodec fallback;
        switch (SelectedContainer)
        {
            case VideoContainer.WebM:
                newCodecs = new() { AudioCodec.Opus, AudioCodec.Vorbis };
                fallback = AudioCodec.Opus;
                break;
            case VideoContainer.Mp4:
                newCodecs = new() { AudioCodec.AAC, AudioCodec.MP3, AudioCodec.Opus };
                fallback = AudioCodec.AAC;
                break;
            case VideoContainer.Mov:
                newCodecs = new() { AudioCodec.AAC, AudioCodec.PCM };
                fallback = AudioCodec.AAC;
                break;
            default:
                newCodecs = new() { AudioCodec.AAC, AudioCodec.MP3, AudioCodec.Opus, AudioCodec.FLAC, AudioCodec.Vorbis, AudioCodec.PCM };
                fallback = AudioCodec.AAC;
                break;
        }

        if (!newCodecs.Contains(SelectedAudioCodec))
            SelectedAudioCodec = fallback;

        AvailableAudioCodecs = new ObservableCollection<AudioCodec>(newCodecs);
        this.RaisePropertyChanged(nameof(AvailableAudioCodecs));
    }

    public void RefreshEncoders()
    {
        UpdateEncoders();
    }

    private void UpdateEncoders()
    {
        var list = _encoderCatalog.GetAvailableEncoders(SelectedVideoCodec);
        var name = _selectedEncoder?.Name ?? Task.VideoEncoderName;

        AvailableEncoders = new ObservableCollection<VideoEncoderInfo>(list);
        this.RaisePropertyChanged(nameof(AvailableEncoders));

        VideoEncoderInfo? chosen;
        if (!string.IsNullOrEmpty(name) && list.Any(e => e.Name == name))
        {
            chosen = list.First(e => e.Name == name);
        }
        else
        {
            chosen = _encoderCatalog.GetDefaultEncoder(SelectedVideoCodec) ?? list.FirstOrDefault();
        }

        SelectedEncoder = chosen;
        if (chosen != null)
            Task.VideoEncoderName = chosen.Name;
    }

    [ReactiveCommand]
    public async Task StartConversion()
    {
        if (IsRunning) return;
        IsRunning = true;
        _cts = new CancellationTokenSource();

        try
        {
            await _conversionService.StartConversionAsync(Task, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Task.Status = ConversionTaskStatus.Cancelled;
        }
        catch (Exception ex)
        {
            Task.Status = ConversionTaskStatus.Failed;
            Task.ErrorMessage = ex.Message;
        }

        IsRunning = false;
        UpdateStatusDisplay();
    }

    /// <summary>停止：终止 ffmpeg 并删除未完成文件，任务保留（回到待转换，可重新开始）。</summary>
    [ReactiveCommand]
    public async Task Stop()
    {
        if (Task.Status != ConversionTaskStatus.Converting) return;
        await _conversionService.StopConversionAsync(Task);
        IsRunning = false;
        UpdateStatusDisplay();
    }

    /// <summary>删除：终止 ffmpeg、删除未完成文件并移除任务。</summary>
    [ReactiveCommand]
    public async Task Cancel()
    {
        _cts?.Cancel();
        await _conversionService.CancelConversionAsync(Task);
        IsRunning = false;
        _onRemove(this);
    }

    private void OnProgress(ConversionTaskProgressMessage msg)
    {
        if (msg.TaskId != Task.Id) return;
        ProgressDisplay = $"{msg.Progress:F1}%";
    }

    private void OnStatusChanged(ConversionTaskStatusChangedMessage msg)
    {
        if (msg.TaskId != Task.Id) return;
        Task.Status = msg.Status;
        UpdateStatusDisplay();
    }

    private void UpdateStatusDisplay()
    {
        StatusDisplay = Task.Status switch
        {
            ConversionTaskStatus.Pending => "待转换",
            ConversionTaskStatus.Converting => "转换中...",
            ConversionTaskStatus.Stopped => "已停止",
            ConversionTaskStatus.Completed => "已完成",
            ConversionTaskStatus.Cancelled => "已取消",
            ConversionTaskStatus.Failed => $"失败: {Task.ErrorMessage}",
            _ => "未知"
        };
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
    }
}