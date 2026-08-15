namespace VideoDownloader.Application.Services.Download.Sites;

internal static class DownloadFormatHelpers
{
    public static string BuildVideoPlusAudioArgument(string videoFormatId, string containerExt)
    {
        var ext = string.IsNullOrWhiteSpace(containerExt) ? "mp4" : containerExt.ToLowerInvariant();
        var audio = ext switch
        {
            "mp4" => "bestaudio[ext=m4a]/bestaudio",
            "webm" => "bestaudio[ext=webm]/bestaudio",
            _ => "bestaudio"
        };
        return $"-f {videoFormatId}+{audio}";
    }
}