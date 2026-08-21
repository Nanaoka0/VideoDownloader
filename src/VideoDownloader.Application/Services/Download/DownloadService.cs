using System.Text.RegularExpressions;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Download;
using VideoDownloader.Domain.Services.Process;

namespace VideoDownloader.Application.Services.Download;

public class DownloadService : IDownloadService
{
    private readonly IEnumerable<ISiteDownloader> _siteDownloaders;
    private readonly IProcessRunner _processRunner;
    private readonly IEventMessenger _messenger;
    private readonly IToolPathResolver _toolPathResolver;
    private readonly IDownloadConfiguration _config;
    private readonly ILogger _logger;

    public DownloadService(
        IEnumerable<ISiteDownloader> siteDownloaders,
        IProcessRunner processRunner,
        IEventMessenger messenger,
        IToolPathResolver toolPathResolver,
        IDownloadConfiguration config,
        ILogger logger)
    {
        _siteDownloaders = siteDownloaders;
        _processRunner = processRunner;
        _messenger = messenger;
        _toolPathResolver = toolPathResolver;
        _config = config;
        _logger = logger;
    }

    public async Task<List<string>> ParsePlaylistUrlsAsync(string url, CancellationToken cancellationToken)
    {
        var ytDlpPath = _toolPathResolver.ResolveToolPath("yt-dlp.exe");
        if (string.IsNullOrEmpty(ytDlpPath)) return new List<string> { url };

        var urls = new List<string>();
        var proxyArg = _config.ShouldBypassProxy(url) ? string.Empty : _config.GetProxyArgument();
        var arguments = $"--flat-playlist --dump-json --no-warnings {proxyArg} {GetUserAgentArgument()} \"{url}\"";
        var progress = new Progress<string>(line =>
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(line);
if (json.RootElement.TryGetProperty("url", out var u))
                    {
                        var videoUrl = u.GetString();
                        if (!string.IsNullOrEmpty(videoUrl))
                        {
                            var baseUri = new Uri(url);
                            var fullUrl = videoUrl.StartsWith("http") ? videoUrl : $"{baseUri.Scheme}://{baseUri.Host}{videoUrl}";
                            urls.Add(fullUrl);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException("Download", "解析播放列表条目失败", ex);
                }
        });

        await _processRunner.RunProcessAsync(ytDlpPath, arguments, string.Empty, progress, cancellationToken);
        return urls.Count > 0 ? urls : new List<string> { url };
    }

    public async Task<DownloadTaskModel> ParseUrlAsync(string url, string outputPath, CancellationToken cancellationToken, string? existingTitle = null)
    {
        var downloader = _siteDownloaders.FirstOrDefault(s => s.CanHandle(url)) ?? _siteDownloaders.First();
        var title = !string.IsNullOrWhiteSpace(existingTitle) ? existingTitle : await downloader.ParseTitleAsync(url, cancellationToken);
        var formats = await ParseFormatsWithYtDlpAsync(url, cancellationToken);
        if (formats.Count == 0)
            throw new InvalidOperationException("无法获取视频格式，请检查网络连接或代理配置，或尝试在设置-选择 Cookie 来源后重试");

        var task = new DownloadTaskModel
        {
            Url = url,
            Title = title,
            OutputPath = outputPath,
            AvailableFormats = formats,
            SelectedFormat = formats.FirstOrDefault(),
            Status = DownloadTaskStatus.Parsing,
            SiteName = new Uri(url).Host
        };

        return task;
    }

    public async Task<string> GetTitleAsync(string url, CancellationToken cancellationToken)
    {
        var downloader = _siteDownloaders.FirstOrDefault(s => s.CanHandle(url)) ?? _siteDownloaders.First();
        return await downloader.ParseTitleAsync(url, cancellationToken);
    }

    private async Task<List<VideoFormatModel>> ParseFormatsWithYtDlpAsync(string url, CancellationToken cancellationToken)
    {
        var ytDlpPath = _toolPathResolver.ResolveToolPath("yt-dlp.exe");
        if (string.IsNullOrEmpty(ytDlpPath))
            throw new InvalidOperationException("yt-dlp 未找到，请在设置-依赖管理中下载");

        var formats = new List<VideoFormatModel>();
        var errorLines = new List<string>();
        var proxyArg = _config.ShouldBypassProxy(url) ? string.Empty : _config.GetProxyArgument();
        var cookieArg = GetCookieArgument();
        var arguments = $"-F --no-warnings {proxyArg} {cookieArg} {GetUserAgentArgument()} \"{url}\"";
        var progress = new Progress<string>(line =>
        {
            try
            {
                if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    errorLines.Add(line);
                    return;
                }
                var format = ParseYtDlpFormatLine(line);
                if (format != null) formats.Add(format);
            }
            catch (Exception ex)
            {
                _logger.LogException("Download", "解析 yt-dlp 格式行失败", ex);
            }
        });

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await _processRunner.RunProcessAsync(ytDlpPath, arguments, string.Empty, progress, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("获取分辨率超时（30 秒）：网络可能不可用或代理配置不正确");
        }

        if (formats.Count == 0 && errorLines.Count > 0)
            throw new InvalidOperationException(errorLines[^1]);

        return formats;
    }

    private static VideoFormatModel? ParseYtDlpFormatLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        if (line.StartsWith("ID") || line.StartsWith("---") || line.Contains("has no formats")) return null;
        if (line.Contains("dubbed-auto", StringComparison.OrdinalIgnoreCase)) return null;

        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var knownExtensions = new[] { "mp4", "m4a", "webm", "mkv", "ts", "flv", "avi", "mov", "3gp", "mp3", "flac", "opus", "ogg", "wav", "aac" };
        if (!knownExtensions.Contains(parts[1])) return null;

        var format = new VideoFormatModel { FormatId = parts[0] };
        var fullLine = line.ToLowerInvariant();

        var dimsMatch = Regex.Match(line, @"(\d+)\s*x\s*(\d+)");
        if (dimsMatch.Success)
        {
            format.Resolution = dimsMatch.Value.Replace(" ", "");
            if (int.TryParse(dimsMatch.Groups[1].Value, out var w)) format.Width = w;
            if (int.TryParse(dimsMatch.Groups[2].Value, out var h)) format.Height = h;
        }

        if (string.IsNullOrEmpty(format.Resolution))
        {
            var resMatch = Regex.Match(line, @"\b(\d{3,4}p\d*)\b");
            if (resMatch.Success) format.Resolution = resMatch.Value;
        }

        // Parse FPS from resolution string (e.g. "1080p60" -> fps=60)
        if (!string.IsNullOrEmpty(format.Resolution) && format.Resolution.Contains('p'))
        {
            var fpsMatch = Regex.Match(format.Resolution, @"(\d+)$");
            if (fpsMatch.Success && int.TryParse(fpsMatch.Groups[1].Value, out var fps))
                format.Fps = fps;
        }
        // Fallback: parse FPS from the yt-dlp line (after resolution, before "|")
        if (format.Fps == 0 && !string.IsNullOrEmpty(format.Resolution) && format.Resolution != "仅音频")
        {
            var lineParts = line.Split('|');
            if (lineParts.Length > 0)
            {
                var firstPart = lineParts[0].Trim();
                var fields = firstPart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length >= 4 && int.TryParse(fields[3], out var fps3))
                    format.Fps = fps3;
            }
        }

        if (fullLine.Contains("audio only") || fullLine == "audio")
        {
            format.HasAudio = true;
            format.Resolution = "仅音频";
        }

        if (fullLine.Contains("video only") || fullLine == "video")
            format.HasVideo = true;

        if (fullLine.Contains("avc") || fullLine.Contains("h264") || fullLine.Contains("x264"))
        {
            format.VideoCodec = "H.264";
            format.HasVideo = true;
        }
        else if (fullLine.Contains("hev1") || fullLine.Contains("hvc1") || fullLine.Contains("hevc") || fullLine.Contains("h265") || fullLine.Contains("x265"))
        {
            format.VideoCodec = "H.265";
            format.HasVideo = true;
        }
        else if (fullLine.Contains("vp9") || fullLine.Contains("vp09"))
        {
            format.VideoCodec = "VP9";
            format.HasVideo = true;
        }
        else if (fullLine.Contains("av01") || fullLine.Contains("av1"))
        {
            format.VideoCodec = "AV1";
            format.HasVideo = true;
        }

        // HDR detection
        if (fullLine.Contains("hdr") || fullLine.Contains("hlg") || fullLine.Contains("dolby vision") ||
            fullLine.Contains("10-bit") || fullLine.Contains("pq"))
            format.IsHdr = true;

        if (fullLine.Contains("aac") || fullLine.Contains("mp4a"))
        {
            format.AudioCodec = "AAC";
            format.HasAudio = true;
        }
        else if (fullLine.Contains("mp3") || fullLine.Contains("mpga"))
        {
            format.AudioCodec = "MP3";
            format.HasAudio = true;
        }
        else if (fullLine.Contains("opus"))
        {
            format.AudioCodec = "Opus";
            format.HasAudio = true;
        }
        else if (fullLine.Contains("flac"))
        {
            format.AudioCodec = "FLAC";
            format.HasAudio = true;
        }
        else if (fullLine.Contains("vorbis"))
        {
            format.AudioCodec = "Vorbis";
            format.HasAudio = true;
        }

        // Parse bitrate: video -> "86k video only", audio -> "audio only <codec> 49k 22k"
        if (fullLine.Contains("video only"))
        {
            var brMatch = Regex.Match(line, @"(\d+)\s*k\s+video only");
            if (brMatch.Success && double.TryParse(brMatch.Groups[1].Value, out var br))
                format.Bitrate = br;
        }
        else if (fullLine.Contains("audio only"))
        {
            var brMatch = Regex.Match(line, @"audio only\s+\S+\s+(\d+)\s*k");
            if (brMatch.Success && double.TryParse(brMatch.Groups[1].Value, out var br))
                format.Bitrate = br;
        }

        format.IsHls = fullLine.Contains("m3u8");

        if (fullLine.Contains("webm")) format.Extension = "webm";
        else if (fullLine.Contains("m4a")) format.Extension = "m4a";
        else if (fullLine.Contains("mp4")) format.Extension = "mp4";
        else if (fullLine.Contains("mkv")) format.Extension = "mkv";
        else if (fullLine.Contains("3gp")) format.Extension = "3gp";
        else if (format.HasAudio && !format.HasVideo) format.Extension = "m4a";
        else format.Extension = "mp4";

        if (!string.IsNullOrEmpty(format.Resolution) && format.Resolution != "仅音频" && !format.HasVideo)
            format.HasVideo = true;

        if (!format.HasAudio && !format.HasVideo)
        {
            if (format.Extension is "m4a" or "mp3")
                format.HasAudio = true;
            else
                format.HasVideo = true;
        }

        return format;
    }

    public async Task StartDownloadAsync(DownloadTaskModel task, CancellationToken cancellationToken)
    {
        var downloader = _siteDownloaders.FirstOrDefault(s => s.CanHandle(task.Url)) ?? _siteDownloaders.First();
        var ytDlpPath = _toolPathResolver.ResolveToolPath("yt-dlp.exe");

        if (string.IsNullOrEmpty(ytDlpPath) || task.SelectedFormat == null)
        {
            task.Status = DownloadTaskStatus.Failed;
            task.ErrorMessage = "yt-dlp 未找到或未选择格式";
            _logger.LogWarning("Download", $"下载失败: {task.Title} - {task.ErrorMessage}");
            _messenger.Send(new DownloadTaskStatusChangedMessage(task.Id, task.Status));
            return;
        }

        task.Status = DownloadTaskStatus.Downloading;
        _logger.LogInfo("Download", $"开始下载: {task.Title} - 格式: {task.SelectedFormat!.DisplayName}");
        _messenger.Send(new DownloadTaskStatusChangedMessage(task.Id, task.Status));

        try { Directory.CreateDirectory(task.OutputPath); }
        catch (Exception ex) { _logger.LogException("Download", $"创建输出目录失败: {task.OutputPath}", ex); }

        var arguments = downloader.BuildDownloadArguments(task.Url, task.SelectedFormat, task.OutputPath);
        var proxyArg = _config.ShouldBypassProxy(task.Url) ? string.Empty : _config.GetProxyArgument();
        if (!string.IsNullOrEmpty(proxyArg))
            arguments = $"{proxyArg} {arguments}";
        var cookieArg = GetCookieArgument();
        var uaArg = GetUserAgentArgument();
        if (!string.IsNullOrEmpty(cookieArg))
            arguments = $"{arguments} {cookieArg}";
        if (!string.IsNullOrEmpty(uaArg))
            arguments = $"{arguments} {uaArg}";

        var errorMessages = new List<string>();
        var progress = new Progress<string>(line =>
        {
            if (line.IndexOf("error:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger.LogError("Download", $"yt-dlp 错误: {line}");
                errorMessages.Add(line);
            }
            ParseProgress(line, task);
            _messenger.Send(new DownloadTaskProgressMessage(task.Id, task.Progress, task.DownloadedBytes, task.TotalBytes, task.Speed));
        });

        var exitCode = await _processRunner.RunProcessAsync(ytDlpPath, arguments, task.OutputPath, progress, cancellationToken,
            pid => task.ProcessId = pid);

        if (cancellationToken.IsCancellationRequested)
        {
            task.Status = DownloadTaskStatus.Cancelled;
            _logger.LogInfo("Download", $"下载已取消: {task.Title}");
        }
        else if (exitCode == 0)
        {
            task.Status = DownloadTaskStatus.Completed;
            task.Progress = 100;
            _logger.LogInfo("Download", $"下载完成: {task.Title}");
        }
        else
        {
            task.Status = DownloadTaskStatus.Failed;
            var detail = errorMessages.Count > 0 ? errorMessages[0] : string.Empty;
            task.ErrorMessage = string.IsNullOrWhiteSpace(detail) ? $"进程退出码: {exitCode}" : detail;
            _logger.LogError("Download", $"下载失败: {task.Title} - {task.ErrorMessage}");
        }

        _messenger.Send(new DownloadTaskStatusChangedMessage(task.Id, task.Status));
        _messenger.Send(new DownloadTaskCompletedMessage(task.Id, task.Status == DownloadTaskStatus.Completed, task.ErrorMessage));
    }

    public Task PauseDownloadAsync(DownloadTaskModel task)
    {
        if (task.Status == DownloadTaskStatus.Downloading)
        {
            _processRunner.KillProcess(task.ProcessId);
            task.Status = DownloadTaskStatus.Paused;
            _messenger.Send(new DownloadTaskStatusChangedMessage(task.Id, task.Status));
        }
        return Task.CompletedTask;
    }

    public async Task ResumeDownloadAsync(DownloadTaskModel task, CancellationToken cancellationToken)
    {
        if (task.Status != DownloadTaskStatus.Paused)
            return;

        var downloader = _siteDownloaders.FirstOrDefault(s => s.CanHandle(task.Url)) ?? _siteDownloaders.First();
        var ytDlpPath = _toolPathResolver.ResolveToolPath("yt-dlp.exe");
        if (string.IsNullOrEmpty(ytDlpPath) || task.SelectedFormat == null)
            return;

        task.Status = DownloadTaskStatus.Downloading;
        _messenger.Send(new DownloadTaskStatusChangedMessage(task.Id, task.Status));

        try { Directory.CreateDirectory(task.OutputPath); }
        catch (Exception ex) { _logger.LogException("Download", $"创建输出目录失败: {task.OutputPath}", ex); }

        var arguments = downloader.BuildDownloadArguments(task.Url, task.SelectedFormat, task.OutputPath) + " -c";
        var proxyArg = _config.ShouldBypassProxy(task.Url) ? string.Empty : _config.GetProxyArgument();
        if (!string.IsNullOrEmpty(proxyArg))
            arguments = $"{proxyArg} {arguments}";
        var cookieArg = GetCookieArgument();
        var uaArg = GetUserAgentArgument();
        if (!string.IsNullOrEmpty(cookieArg))
            arguments = $"{arguments} {cookieArg}";
        if (!string.IsNullOrEmpty(uaArg))
            arguments = $"{arguments} {uaArg}";

        var errorMessages = new List<string>();
        var progress = new Progress<string>(line =>
        {
            if (line.IndexOf("error:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger.LogError("Download", $"yt-dlp 错误: {line}");
                errorMessages.Add(line);
            }
            ParseProgress(line, task);
            _messenger.Send(new DownloadTaskProgressMessage(task.Id, task.Progress, task.DownloadedBytes, task.TotalBytes, task.Speed));
        });

        await _processRunner.RunProcessAsync(ytDlpPath, arguments, task.OutputPath, progress, cancellationToken,
            pid => task.ProcessId = pid);
    }

    public Task CancelDownloadAsync(DownloadTaskModel task)
    {
        _processRunner.KillProcess(task.ProcessId);
        task.Status = DownloadTaskStatus.Cancelled;
        task.Progress = 0;
        task.DownloadedBytes = 0;
        _messenger.Send(new DownloadTaskStatusChangedMessage(task.Id, task.Status));
        _messenger.Send(new DownloadTaskCompletedMessage(task.Id, false, "用户取消"));
        return Task.CompletedTask;
    }

    private static void ParseProgress(string line, DownloadTaskModel task)
    {
        var progressMatch = Regex.Match(line, @"(\d+\.?\d*)%");
        if (progressMatch.Success && double.TryParse(progressMatch.Groups[1].Value, out var pct))
            task.Progress = pct;

        var speedMatch = Regex.Match(line, @"(\d+\.?\d*)([KMGT]?i?B)/s");
        if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value, out var speed))
        {
            var unit = speedMatch.Groups[2].Value;
            task.Speed = UnitToBytes(unit, speed);
        }

        var sizeMatch = Regex.Match(line, @"(\d+\.?\d*)([KMGT]?i?B)\s*/\s*(\d+\.?\d*)([KMGT]?i?B)");
        if (sizeMatch.Success)
        {
            double.TryParse(sizeMatch.Groups[1].Value, out var downloaded);
            double.TryParse(sizeMatch.Groups[3].Value, out var total);
            task.DownloadedBytes = (long)UnitToBytes(sizeMatch.Groups[2].Value, downloaded);
            task.TotalBytes = (long)UnitToBytes(sizeMatch.Groups[4].Value, total);
        }
        else
        {
            var ofMatch = Regex.Match(line, @"(\d+\.?\d*)%\s*of\s*~?\s*(\d+\.?\d*)([KMGT]?i?B)");
            if (ofMatch.Success
                && double.TryParse(ofMatch.Groups[1].Value, out var pctOf)
                && double.TryParse(ofMatch.Groups[2].Value, out var total))
            {
                task.TotalBytes = (long)UnitToBytes(ofMatch.Groups[3].Value, total);
                task.DownloadedBytes = (long)(task.TotalBytes * pctOf / 100.0);
            }
            else
            {
                var atMatch = Regex.Match(line, @"(\d+\.?\d*)([KMGT]?i?B)\s+at\s+");
                if (atMatch.Success && double.TryParse(atMatch.Groups[1].Value, out var downloaded))
                    task.DownloadedBytes = (long)UnitToBytes(atMatch.Groups[2].Value, downloaded);
            }
        }
    }

    private static double UnitToBytes(string unit, double value) => unit switch
    {
        "KiB" => value * 1024,
        "MiB" => value * 1024 * 1024,
        "GiB" => value * 1024 * 1024 * 1024,
        "kB" => value * 1000,
        "MB" => value * 1000 * 1000,
        "GB" => value * 1000 * 1000 * 1000,
        _ => value
    };

    /// <summary>根据配置的浏览器生成 yt-dlp 的 --cookies-from-browser 参数；未配置返回空。</summary>
    private string GetCookieArgument()
    {
        var browser = _config.CookieBrowser;
        if (string.IsNullOrEmpty(browser) || browser == "None")
            return string.Empty;
        return $"--cookies-from-browser {browser}";
    }

    private static string GetUserAgentArgument() =>
        "--user-agent \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36\"";
}