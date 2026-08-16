using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services.Conversion;

namespace VideoDownloader.ViewModels;

public partial class VideoConversionViewModel : ReactiveObject
{
    private readonly IVideoConversionService _conversionService;
    private readonly IVideoEncoderCatalog _encoderCatalog;
    private readonly IEventMessenger _messenger;
    private readonly ILogger _logger;

    public ObservableCollection<VideoConversionItemViewModel> Tasks { get; } = new();

    public Interaction<Unit, string[]> PickVideoFiles { get; } = new();

    public VideoConversionViewModel(
        IVideoConversionService conversionService,
        IVideoEncoderCatalog encoderCatalog,
        IEventMessenger messenger,
        ILogger logger)
    {
        _conversionService = conversionService;
        _encoderCatalog = encoderCatalog;
        _messenger = messenger;
        _logger = logger;
    }

    [ReactiveCommand]
    public async Task AddFiles()
    {
        var files = await PickVideoFiles.Handle(Unit.Default).ToTask();
        if (files is null) return;
        foreach (var file in files)
            AddFile(file);
    }

    public void AddFile(string filePath)
    {
        var task = new VideoConversionTaskModel
        {
            InputFilePath = filePath,
            FileName = Path.GetFileName(filePath),
            OutputFilePath = Path.ChangeExtension(filePath, ".mp4"),
            Status = ConversionTaskStatus.Pending
        };

        var item = new VideoConversionItemViewModel(task, _conversionService, _encoderCatalog, _messenger, _logger, RemoveItem);
        Tasks.Add(item);
    }

    public async Task RefreshEncodersAsync()
    {
        await _encoderCatalog.RefreshAsync(CancellationToken.None);
        foreach (var item in Tasks)
            item.RefreshEncoders();
    }

    public void RemoveItem(VideoConversionItemViewModel item)
    {
        item.Dispose();
        Tasks.Remove(item);
    }

    [ReactiveCommand]
    public async Task ConvertSelected()
    {
        _messenger.Send(new StatusUpdateMessage("正在转换选中任务..."));
        var selected = Tasks.Where(t => t.Task.IsSelected).ToList();
        foreach (var item in selected)
        {
            await item.StartConversionCommand.Execute(Unit.Default).ToTask();
        }
        _messenger.Send(new StatusUpdateMessage("转换完成"));
    }

    [ReactiveCommand]
    public void CancelAll()
    {
        _messenger.Send(new StatusUpdateMessage("正在取消所有任务..."));
        foreach (var item in Tasks)
            _ = item.CancelCommand.Execute(Unit.Default).Subscribe(_ => { }, ex => _logger.LogException("Conversion", "取消任务失败", ex));
        _messenger.Send(new StatusUpdateMessage("已取消所有任务"));
    }
}