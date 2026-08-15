using System.Text.RegularExpressions;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Conversion;
using VideoDownloader.Domain.Services.Process;

namespace VideoDownloader.Application.Services.Conversion;

public class VideoConversionService : IVideoConversionService
{
    private readonly IProcessRunner _processRunner;
    private readonly IEventMessenger _messenger;
    private readonly IToolPathResolver _toolPathResolver;

    private static readonly Dictionary<AudioCodec, string> AudioCodecMap = new()
    {
        [AudioCodec.AAC] = "aac",
        [AudioCodec.MP3] = "libmp3lame",
        [AudioCodec.Opus] = "libopus",
        [AudioCodec.FLAC] = "flac",
        [AudioCodec.Vorbis] = "libvorbis",
        [AudioCodec.PCM] = "pcm_s16le"
    };

    private static readonly Dictionary<VideoContainer, string> ContainerExtensionMap = new()
    {
        [VideoContainer.Mp4] = "mp4",
        [VideoContainer.Mkv] = "mkv",
        [VideoContainer.WebM] = "webm",
        [VideoContainer.Mov] = "mov"
    };

    public VideoConversionService(IProcessRunner processRunner, IEventMessenger messenger, IToolPathResolver toolPathResolver)
    {
        _processRunner = processRunner;
        _messenger = messenger;
        _toolPathResolver = toolPathResolver;
    }

    public async Task StartConversionAsync(VideoConversionTaskModel task, CancellationToken cancellationToken)
    {
        var ffmpegPath = _toolPathResolver.ResolveToolPath("ffmpeg.exe");
        if (ffmpegPath == null)
        {
            task.Status = ConversionTaskStatus.Failed;
            task.ErrorMessage = "ffmpeg 未找到";
            _messenger.Send(new ConversionTaskStatusChangedMessage(task.Id, task.Status));
            _messenger.Send(new ConversionTaskCompletedMessage(task.Id, false, task.ErrorMessage));
            return;
        }

        task.Status = ConversionTaskStatus.Converting;
        var outputExt = ContainerExtensionMap[task.Container];
        task.OutputFilePath = Path.ChangeExtension(task.InputFilePath, outputExt);
        task.OutputFilePath = GetUniqueOutputPath(task.OutputFilePath);

        if (string.IsNullOrWhiteSpace(task.VideoEncoderName))
        {
            task.Status = ConversionTaskStatus.Failed;
            task.ErrorMessage = "未选择视频编码器，请在任务中选择编码器";
            _messenger.Send(new ConversionTaskStatusChangedMessage(task.Id, task.Status));
            _messenger.Send(new ConversionTaskCompletedMessage(task.Id, false, task.ErrorMessage));
            return;
        }

        task.DurationSeconds = await ProbeDurationAsync(task);
        var arguments = BuildFfmpegArguments(task);
        _messenger.Send(new ConversionTaskStatusChangedMessage(task.Id, task.Status));

        var errorLines = new List<string>();
        var progress = new Progress<string>(line =>
        {
            if (line.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Invalid", StringComparison.OrdinalIgnoreCase))
            {
                errorLines.Add(line);
            }
            ParseProgress(line, task);
            _messenger.Send(new ConversionTaskProgressMessage(task.Id, task.Progress));
        });

        var exitCode = await _processRunner.RunProcessAsync(ffmpegPath, arguments,
            Path.GetDirectoryName(task.InputFilePath) ?? string.Empty, progress, cancellationToken,
            pid => task.ProcessId = pid);

        if (cancellationToken.IsCancellationRequested)
        {
            task.Status = ConversionTaskStatus.Cancelled;
        }
        else if (task.Status != ConversionTaskStatus.Converting)
        {
            // 已被 停止/删除 中断（停止置 Pending、删除置 Cancelled），保留其状态，不再覆盖为失败
        }
        else if (exitCode == 0)
        {
            task.Status = ConversionTaskStatus.Completed;
            task.Progress = 100;
        }
        else
        {
            task.Status = ConversionTaskStatus.Failed;
            var errorDetail = errorLines.Count > 0
                ? string.Join("\n", errorLines.TakeLast(3))
                : $"进程退出码: {exitCode}";
            task.ErrorMessage = MapFfmpegError(errorDetail, task);
        }

        _messenger.Send(new ConversionTaskStatusChangedMessage(task.Id, task.Status));
        _messenger.Send(new ConversionTaskCompletedMessage(task.Id, task.Status == ConversionTaskStatus.Completed, task.ErrorMessage));
    }

    /// <summary>停止：终止 ffmpeg 进程并删除未完成的输出文件，任务保留（状态回到已停止，可重新开始）。</summary>
    public Task StopConversionAsync(VideoConversionTaskModel task)
    {
        if (task.Status == ConversionTaskStatus.Converting)
        {
            _processRunner.KillProcess(task.ProcessId);
            DeleteUnfinishedOutput(task);
            task.Status = ConversionTaskStatus.Stopped;
            task.Progress = 0;
            _messenger.Send(new ConversionTaskStatusChangedMessage(task.Id, task.Status));
        }
        return Task.CompletedTask;
    }

    /// <summary>删除：仅当仍在转换时终止 ffmpeg 并删除未完成输出；已完成任务保留成品文件（任务由界面移除）。</summary>
    public Task CancelConversionAsync(VideoConversionTaskModel task)
    {
        if (task.Status == ConversionTaskStatus.Converting)
        {
            _processRunner.KillProcess(task.ProcessId);
            DeleteUnfinishedOutput(task);
        }
        task.Status = ConversionTaskStatus.Cancelled;
        task.Progress = 0;
        _messenger.Send(new ConversionTaskStatusChangedMessage(task.Id, task.Status));
        _messenger.Send(new ConversionTaskCompletedMessage(task.Id, false, "用户取消"));
        return Task.CompletedTask;
    }

    private static void DeleteUnfinishedOutput(VideoConversionTaskModel task)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(task.OutputFilePath) && File.Exists(task.OutputFilePath))
                File.Delete(task.OutputFilePath);
        }
        catch
        {
        }
    }

    /// <summary>把常见的 ffmpeg 错误翻译成可读提示（如编码器不受当前 GPU/驱动支持）。</summary>
    private static string MapFfmpegError(string detail, VideoConversionTaskModel task)
    {
        if (detail.Contains("minimum required Nvidia driver", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("does not support the required nvenc API version", StringComparison.OrdinalIgnoreCase))
            return $"所选编码器（{task.VideoEncoderName}）需要更新的 NVIDIA 驱动（610.00 或更高），请更新显卡驱动后再试";

        if (detail.Contains("not implemented", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("error code:-40", StringComparison.OrdinalIgnoreCase))
            return $"当前 GPU/驱动不支持所选编码器（{task.VideoEncoderName}），请换用其他编码器";

        return detail;
    }

    private string BuildFfmpegArguments(VideoConversionTaskModel task)
    {
        var videoCodec = task.VideoEncoderName;
        var audioCodec = AudioCodecMap[task.AudioCodec];

        var input = $"\"{task.InputFilePath}\"";
        var output = $"\"{task.OutputFilePath}\"";

        var videoArgs = $"-c:v {videoCodec}";
        var audioArgs = $"-c:a {audioCodec}";

        if (task.Container is VideoContainer.WebM or VideoContainer.Mov)
        {
            if (task.Container == VideoContainer.WebM)
                videoArgs = $"-c:v {videoCodec} -b:v 0 -crf 30";
            if (task.Container == VideoContainer.Mov)
                audioArgs = "-c:a pcm_s16le";
        }

        return $"-i {input} {videoArgs} {audioArgs} -progress pipe: -y {output}";
    }

    private static string GetUniqueOutputPath(string outputPath)
    {
        if (!File.Exists(outputPath)) return outputPath;

        var directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(outputPath);
        var ext = Path.GetExtension(outputPath);
        var counter = 1;
        while (File.Exists(Path.Combine(directory, $"{fileName}（{counter}）{ext}")))
        {
            counter++;
        }
        return Path.Combine(directory, $"{fileName}（{counter}）{ext}");
    }

    /// <summary>用 ffprobe 探测输入媒体时长（秒）；失败或超时返回 0（进度退化为不精确估算）。</summary>
    private async Task<double> ProbeDurationAsync(VideoConversionTaskModel task)
    {
        var ffprobePath = _toolPathResolver.ResolveToolPath("ffprobe.exe");
        if (ffprobePath == null)
            return 0;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var output = await _processRunner.RunProcessAndReadOutputAsync(ffprobePath,
                $"-v error -show_entries format=duration -of csv=p=0 \"{task.InputFilePath}\"", cts.Token);
            if (double.TryParse(output?.Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var duration) && duration > 0)
                return duration;
        }
        catch
        {
            // ffprobe 失败不影响转换本身
        }
        return 0;
    }

    private static void ParseProgress(string line, VideoConversionTaskModel task)
    {
        var timeMatch = Regex.Match(line, @"out_time_us=(\d+)");
        if (timeMatch.Success && long.TryParse(timeMatch.Groups[1].Value, out var outTimeUs))
        {
            var outSeconds = outTimeUs / 10_000_000.0;
            if (task.DurationSeconds > 0)
                task.Progress = Math.Min(outSeconds / task.DurationSeconds * 100, 99.9);
            else
                task.Progress = Math.Min(outSeconds % 100, 99.9);
        }

        var progressMatch = Regex.Match(line, @"progress=(\w+)");
        if (progressMatch.Success && progressMatch.Groups[1].Value == "end")
            task.Progress = 100;
    }
}