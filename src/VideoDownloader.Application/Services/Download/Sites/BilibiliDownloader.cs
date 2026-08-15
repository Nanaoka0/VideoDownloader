using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Download;
using VideoDownloader.Domain.Services.Process;

namespace VideoDownloader.Application.Services.Download.Sites;

public class BilibiliDownloader : ISiteDownloader
{
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathResolver _toolPathResolver;
    private readonly IDownloadConfiguration _config;
    private readonly ILogger _logger;

    public BilibiliDownloader(
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
        url.Contains("bilibili.com", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("b23.tv", StringComparison.OrdinalIgnoreCase);

    public Task<string> ParseTitleAsync(string url, CancellationToken cancellationToken) =>
        SiteTitleResolver.ResolveAsync(_processRunner, _toolPathResolver, _config, _logger, url, "Bilibili视频", cancellationToken);

    public Task<List<VideoFormatModel>> ParseFormatsAsync(string url, CancellationToken cancellationToken)
    {
        var formats = new List<VideoFormatModel>
        {
            new() { FormatId = "bestvideo+bestaudio", Resolution = "4K 2160p", Width = 3840, Height = 2160, VideoCodec = "HEVC", HasVideo = true, HasAudio = true, Extension = "mp4" },
            new() { FormatId = "1080p+bestaudio", Resolution = "1080p", Width = 1920, Height = 1080, VideoCodec = "AVC", HasVideo = true, HasAudio = true, Extension = "mp4" },
            new() { FormatId = "720p+bestaudio", Resolution = "720p", Width = 1280, Height = 720, VideoCodec = "AVC", HasVideo = true, HasAudio = true, Extension = "mp4" },
            new() { FormatId = "bestaudio", Resolution = "仅音频", HasAudio = true, Extension = "m4a" }
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
            formatArg = DownloadFormatHelpers.BuildVideoPlusAudioArgument(format.FormatId, format.Extension);
        else
            formatArg = $"-f {format.FormatId}";
        var merge = format.IsAudioOnly ? string.Empty : $"--merge-output-format {format.Extension}";
        return $"{formatArg} {merge} -o \"{template}\" \"{url}\"".Trim();
    }
}