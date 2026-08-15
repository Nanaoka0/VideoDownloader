using VideoDownloader.Domain.Models;

namespace VideoDownloader.Domain.Services.Download;

public interface IDownloadService
{
    Task<DownloadTaskModel> ParseUrlAsync(string url, string outputPath, CancellationToken cancellationToken, string? existingTitle = null);
    Task<List<string>> ParsePlaylistUrlsAsync(string url, CancellationToken cancellationToken);
    Task<string> GetTitleAsync(string url, CancellationToken cancellationToken);
    Task StartDownloadAsync(DownloadTaskModel task, CancellationToken cancellationToken);
    Task PauseDownloadAsync(DownloadTaskModel task);
    Task ResumeDownloadAsync(DownloadTaskModel task, CancellationToken cancellationToken);
    Task CancelDownloadAsync(DownloadTaskModel task);
}