namespace VideoDownloader.Domain.Services;

public interface IDownloadConfiguration
{
    string OutputDirectory { get; set; }
    int MaxConcurrentDownloads { get; set; }
    bool UseProxy { get; set; }
    string ProxyType { get; set; }
    string ProxyHttpHost { get; set; }
    int ProxyHttpPort { get; set; }
    string ProxySocks5Host { get; set; }
    int ProxySocks5Port { get; set; }
    bool PreferHdr { get; set; }
    bool SuppressFirstRunTips { get; set; }
    string Theme { get; set; }
    string CookieBrowser { get; set; }
    bool BypassProxyForBilibili { get; set; }
    bool BypassProxyForYoutube { get; set; }
    string GetProxyArgument();
    bool ShouldBypassProxy(string url);
    void Save();
}
