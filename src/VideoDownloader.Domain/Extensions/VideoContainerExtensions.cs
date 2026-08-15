using VideoDownloader.Domain.Models;

namespace VideoDownloader.Domain.Extensions;

public static class VideoContainerExtensions
{
    public static readonly VideoContainer[] AllContainers = [VideoContainer.Mp4, VideoContainer.Mkv, VideoContainer.WebM, VideoContainer.Mov];
}
