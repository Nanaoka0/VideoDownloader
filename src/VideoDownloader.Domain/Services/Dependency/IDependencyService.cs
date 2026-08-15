using VideoDownloader.Domain.Models;

namespace VideoDownloader.Domain.Services.Dependency;

public interface IDependencyService
{
    Task<DependencyStatusModel> CheckDependenciesAsync(CancellationToken cancellationToken);
    Task<bool> DownloadToolAsync(string toolName, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken);
    Task<string?> GetToolVersionAsync(string executablePath);

    /// <summary>按当前显卡/驱动说明将下载的 ffmpeg 版本（用于界面显示）。</summary>
    string DescribeFfmpegSource();
}