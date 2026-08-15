using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services.FileSystem;

namespace VideoDownloader.Infrastructure.Services.FileSystem;

public class FileCleanupService : IFileCleanupService
{
    private readonly ILogger _logger;

    public FileCleanupService(ILogger logger)
    {
        _logger = logger;
    }

    public void CleanTempFiles(DownloadTaskModel task)
    {
        try
        {
            var dir = task.OutputPath;
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, $"*{task.Title}*.part", SearchOption.TopDirectoryOnly))
                File.Delete(f);
            foreach (var f in Directory.GetFiles(dir, $"*{task.Title}*.tmp", SearchOption.TopDirectoryOnly))
                File.Delete(f);
            foreach (var f in Directory.GetFiles(dir, $"{task.Id}*"))
                File.Delete(f);
        }
        catch (Exception ex) { _logger.LogException("FileSystem", "清理临时文件失败", ex); }
    }
}