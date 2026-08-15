using VideoDownloader.Domain.Models;

namespace VideoDownloader.Domain.Services.Conversion;

public interface IVideoConversionService
{
    Task StartConversionAsync(VideoConversionTaskModel task, CancellationToken cancellationToken);
    Task StopConversionAsync(VideoConversionTaskModel task);
    Task CancelConversionAsync(VideoConversionTaskModel task);
}