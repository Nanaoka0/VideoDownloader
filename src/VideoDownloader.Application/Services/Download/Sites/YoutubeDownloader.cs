using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Download;
using VideoDownloader.Domain.Services.Process;

namespace VideoDownloader.Application.Services.Download.Sites;

public class YoutubeDownloader : ISiteDownloader
{
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathResolver _toolPathResolver;
    private readonly IDownloadConfiguration _config;
    private readonly ILogger _logger;

    public YoutubeDownloader(
        IProcessRunner processRunner,
        IToolPathResolver toolPathResolver,
        IDownloadConfiguration config,
        ILogger logger)
    {
        _processRunner = processRunner;
        _toolPathResolver = toolPathResolver;
        _config = config;
        _logger = logger;
    }

    public bool CanHandle(string url) =>
        url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);

    public Task<string> ParseTitleAsync(string url, CancellationToken cancellationToken) =>
        SiteTitleResolver.ResolveAsync(_processRunner, _toolPathResolver, _config, _logger, url, "YouTube视频", cancellationToken);

    public Task<List<VideoFormatModel>> ParseFormatsAsync(string url, CancellationToken cancellationToken)
    {
        var formats = new List<VideoFormatModel>
        {
            new() { FormatId = "bestvideo[height<=2160]+bestaudio/best[height<=2160]", Resolution = "4K 2160p60", Width = 3840, Height = 2160, VideoCodec = "VP9", HasVideo = true, HasAudio = true, Extension = "webm" },
            new() { FormatId = "bestvideo[height<=1440]+bestaudio/best[height<=1440]", Resolution = "1440p60", Width = 2560, Height = 1440, VideoCodec = "VP9", HasVideo = true, HasAudio = true, Extension = "webm" },
            new() { FormatId = "bestvideo[height<=1080]+bestaudio/best[height<=1080]", Resolution = "1080p60", Width = 1920, Height = 1080, VideoCodec = "AVC", HasVideo = true, HasAudio = true, Extension = "mp4" },
            new() { FormatId = "bestvideo[height<=720]+bestaudio/best[height<=720]", Resolution = "720p60", Width = 1280, Height = 720, VideoCodec = "AVC", HasVideo = true, HasAudio = true, Extension = "mp4" },
            new() { FormatId = "bestaudio[ext=m4a]", Resolution = "仅音频 (AAC)", HasAudio = true, Extension = "m4a" },
            new() { FormatId = "bestaudio[ext=mp3]", Resolution = "仅音频 (MP3)", HasAudio = true, Extension = "mp3" }
        };
        return Task.FromResult(formats);
    }

    public string BuildDownloadArguments(string url, VideoFormatModel format, string outputPath)
    {
        var template = Path.Combine(outputPath, "%(title)s.%(ext)s");
        string formatArg;
        if (format.IsAudioOnly)
            formatArg = $"-f {format.FormatId}";
        else if (format.HasVideo && !format.HasAudio && !format.FormatId.Contains('+'))
            formatArg = DownloadFormatHelpers.BuildVideoPlusAudioArgument(format.FormatId, "mp4");
        else
            formatArg = $"-f {format.FormatId}";
        var merge = format.IsAudioOnly ? string.Empty : "--merge-output-format mp4";
        return $"{formatArg} {merge} -o \"{template}\" \"{url}\"".Trim();
    }
}