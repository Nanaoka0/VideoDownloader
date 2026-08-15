using VideoDownloader.Domain.Models;

namespace VideoDownloader.Domain.Services.FileSystem;

public interface IFileCleanupService
{
    void CleanTempFiles(DownloadTaskModel task);
}