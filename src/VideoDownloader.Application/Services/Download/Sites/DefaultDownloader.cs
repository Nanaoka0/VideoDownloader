using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Download;
using VideoDownloader.Domain.Services.Process;

namespace VideoDownloader.Application.Services.Download.Sites;

public class DefaultDownloader : ISiteDownloader
{
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathResolver _toolPathResolver;
    private readonly IDownloadConfiguration _config;
    private readonly ILogger _logger;

    public DefaultDownloader(
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

    public bool CanHandle(string url) => true;

    public async Task<string> ParseTitleAsync(string url, CancellationToken cancellationToken) =>
        await SiteTitleResolver.ResolveAsync(_processRunner, _toolPathResolver, _config, _logger, url, $"Download from {new Uri(url).Host}", cancellationToken);

    public Task<List<VideoFormatModel>> ParseFormatsAsync(string url, CancellationToken cancellationToken)
    {
        var formats = new List<VideoFormatModel>
        {
            new() { FormatId = "best", Resolution = "最佳质量", Width = 1920, Height = 1080, VideoCodec = "H.264", HasVideo = true, HasAudio = true, Extension = "mp4" },
            new() { FormatId = "bestvideo+bestaudio", Resolution = "最佳视频+音频", Width = 1920, Height = 1080, VideoCodec = "H.264", HasVideo = true, HasAudio = true, Extension = "mkv" },
            new() { FormatId = "bestaudio", Resolution = "仅音频", VideoCodec = "AAC", HasAudio = true, Extension = "m4a" }
        };
        return Task.FromResult(formats);
    }

    public string BuildDownloadArguments(string url, VideoFormatModel format, string outputPath)
    {
        var template = Path.Combine(outputPath, "%(title)s.%(ext)s");
        string formatArg;
        if (format.IsAudioOnly)
            formatArg = "-f bestaudio";
        else if (format.HasVideo && !format.HasAudio && !format.FormatId.Contains('+'))
            formatArg = DownloadFormatHelpers.BuildVideoPlusAudioArgument(format.FormatId, format.Extension);
        else
            formatArg = $"-f {format.FormatId}";
        var merge = format.IsAudioOnly ? string.Empty : $"--merge-output-format {format.Extension}";
        return $"{formatArg} {merge} -o \"{template}\" \"{url}\"".Trim();
    }
}