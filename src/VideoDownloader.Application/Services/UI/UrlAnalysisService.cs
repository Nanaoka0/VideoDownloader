namespace VideoDownloader.Application.Services.UI;

public class UrlAnalysisService : IUrlAnalysisService
{
    public bool IsPlaylistUrl(string url)
    {
        var uri = new Uri(url);
        var host = uri.Host.ToLowerInvariant();
        var query = uri.Query;

        if (query.Contains("list=")) return true;
        if (host.Contains("youtube.com") && uri.AbsolutePath.Contains("/playlist")) return true;
        if (host.Contains("youtube.com") && uri.AbsolutePath.Contains("/channel/")) return true;
        if (host.Contains("youtube.com") && uri.AbsolutePath.Contains("/user/")) return true;
        if (host.Contains("bilibili.com") && uri.AbsolutePath.Contains("/medialist/")) return true;
        if (host.Contains("bilibili.com") && uri.AbsolutePath.Contains("/collection/")) return true;
        if (host.Contains("bilibili.com") && uri.AbsolutePath.Contains("/space/")) return true;

        return false;
    }
}