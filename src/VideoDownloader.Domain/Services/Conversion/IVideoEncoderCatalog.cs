using VideoDownloader.Domain.Models;

namespace VideoDownloader.Domain.Services.Conversion;

public interface IVideoEncoderCatalog
{
    bool IsReady { get; }
    Task RefreshAsync(CancellationToken cancellationToken);
    IReadOnlyList<VideoEncoderInfo> GetAvailableEncoders(VideoCodec codec);
    VideoEncoderInfo? GetDefaultEncoder(VideoCodec codec);
}