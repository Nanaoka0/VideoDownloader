using VideoDownloader.Domain.Models;

namespace VideoDownloader.Domain.Services.Download;

public interface ISiteDownloader
{
    bool CanHandle(string url);
    Task<string> ParseTitleAsync(string url, CancellationToken cancellationToken);
    Task<List<VideoFormatModel>> ParseFormatsAsync(string url, CancellationToken cancellationToken);
    string BuildDownloadArguments(string url, VideoFormatModel format, string outputPath);
}