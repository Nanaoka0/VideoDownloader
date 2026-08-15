using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Process;

namespace VideoDownloader.Application.Services.Download.Sites;

internal static class SiteTitleResolver
{
    public static async Task<string> ResolveAsync(
        IProcessRunner processRunner,
        IToolPathResolver toolPathResolver,
        IDownloadConfiguration config,
        ILogger logger,
        string url,
        string fallback,
        CancellationToken cancellationToken)
    {
        var ytDlpPath = toolPathResolver.ResolveToolPath("yt-dlp.exe");
        if (string.IsNullOrEmpty(ytDlpPath)) return fallback;

        var proxyArg = config.ShouldBypassProxy(url) ? string.Empty : config.GetProxyArgument();
        var cookieArg = GetCookieArgument(config);
        var arguments = $"--skip-download --no-playlist --no-warnings --print title {proxyArg} {cookieArg} \"{url}\"";
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));
            var output = await processRunner.RunProcessAndReadOutputAsync(ytDlpPath, arguments, timeoutCts.Token);
            var title = output?.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
            logger.LogWarning("Title", $"取标题: url={url} len={output?.Length ?? 0} title='{title ?? "(null)"}'");
            return title ?? fallback;
        }
        catch (OperationCanceledException)
        {
            return fallback;
        }
    }

    private static string GetCookieArgument(IDownloadConfiguration config)
    {
        var browser = config.CookieBrowser;
        if (string.IsNullOrEmpty(browser) || browser == "None")
            return string.Empty;
        return $"--cookies-from-browser {browser}";
    }
}