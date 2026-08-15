using System.IO.Compression;
using System.Net;
using System.Text.Json;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Messenger;
using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Dependency;
using VideoDownloader.Domain.Services.Process;

namespace VideoDownloader.Application.Services.Dependency;

public class DependencyService : IDependencyService
{
    private readonly IEventMessenger _messenger;
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathResolver _toolPathResolver;
    private readonly IDownloadConfiguration _config;
    private readonly IGpuInfoService _gpuInfoService;
    private readonly ILogger _logger;

    private const string FfmpegLatestUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private const string FfmpegCompatUrl = "https://github.com/GyanD/codexffmpeg/releases/download/7.1.1/ffmpeg-7.1.1-essentials_build.zip";
    private const string FfmpegCompatMirrorUrl = "https://ghfast.top/https://github.com/GyanD/codexffmpeg/releases/download/7.1.1/ffmpeg-7.1.1-essentials_build.zip";

    /// <summary>ffmpeg 的 NVENC 要求驱动 ≥ 610.00（NVENC API 13.1），旧驱动需使用 7.1.1。</summary>
    private const double NvencMinDriverVersion = 610.00;

    private static readonly Dictionary<string, string> DownloadUrls = new()
    {
        ["yt-dlp"] = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
    };

    public DependencyService(IEventMessenger messenger, IProcessRunner processRunner, IToolPathResolver toolPathResolver, IDownloadConfiguration config, IGpuInfoService gpuInfoService, ILogger logger)
    {
        _messenger = messenger;
        _processRunner = processRunner;
        _toolPathResolver = toolPathResolver;
        _config = config;
        _gpuInfoService = gpuInfoService;
        _logger = logger;
    }

    /// <summary>按当前显卡/驱动说明将下载的 ffmpeg 版本，如 "ffmpeg 7.1.1（兼容 NVIDIA 驱动 591.86）"。</summary>
    public string DescribeFfmpegSource()
    {
        var gpu = _gpuInfoService.Detect();
        if (gpu.Vendor == GpuVendor.Nvidia && gpu.DriverVersion > 0 && gpu.DriverVersion < NvencMinDriverVersion)
            return $"ffmpeg 7.1.1（兼容 NVIDIA 驱动 {gpu.DriverVersionText}）";
        return "ffmpeg 最新版";
    }

    public async Task<DependencyStatusModel> CheckDependenciesAsync(CancellationToken cancellationToken)
    {
        var result = new DependencyStatusModel();

        result.Ffmpeg = await CheckToolAsync("ffmpeg", "ffmpeg.exe", cancellationToken);
        result.Ffprobe = await CheckToolAsync("ffprobe", "ffprobe.exe", cancellationToken);
        result.YtDlp = await CheckToolAsync("yt-dlp", "yt-dlp.exe", cancellationToken);

        if (result.Ffmpeg.IsAvailable && result.Ffmpeg.ExecutablePath != null)
            await VerifyNvencAsync(result.Ffmpeg.ExecutablePath, notifyUser: false);

        _messenger.Send(new DependencyStatusChangedMessage(result));
        return result;
    }

    public async Task<bool> DownloadToolAsync(string toolName, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (toolName == "ffmpeg")
            return await DownloadFfmpegAsync(destinationPath, progress, cancellationToken);

        if (!DownloadUrls.TryGetValue(toolName, out var url))
            return false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await DownloadFileAsync(url, destinationPath, progress, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogException("Dependency", $"工具下载失败 ({toolName})", ex);
            throw;
        }
    }

    public async Task<string?> GetToolVersionAsync(string executablePath)
    {
        try
        {
            var output = await _processRunner.RunProcessAndReadOutputAsync(executablePath,
                executablePath.EndsWith("yt-dlp.exe", StringComparison.OrdinalIgnoreCase) ? "--version" : "-version",
                CancellationToken.None);
            var line = output?.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return line?.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 下载 ffmpeg：按显卡/驱动选择兼容的构建（旧 NVIDIA 驱动用 7.1.1，否则最新版），
    /// 解压出 ffmpeg.exe/ffprobe.exe 安装到 .tools，并做一次 NVENC 自检。
    /// </summary>
    private async Task<bool> DownloadFfmpegAsync(string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var urls = GetFfmpegUrls();
        Exception? lastException = null;

        foreach (var url in urls)
        {
            var tempZip = Path.Combine(Path.GetTempPath(), $"ffmpeg_{Guid.NewGuid():N}.zip");
            try
            {
                await DownloadFileAsync(url, tempZip, progress, cancellationToken);

                var targetDir = Path.GetDirectoryName(destinationPath)!;
                Directory.CreateDirectory(targetDir);

                using (var zip = ZipFile.OpenRead(tempZip))
                {
                    await ExtractBinariesAsync(zip, targetDir);
                }

                _logger.LogInfo("Dependency", $"ffmpeg 下载并安装完成（源：{url}）");
                await VerifyNvencAsync(destinationPath, notifyUser: true);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogException("Dependency", $"ffmpeg 下载失败（源：{url}）", ex);
            }
            finally
            {
                try { File.Delete(tempZip); } catch { }
            }
        }

        if (lastException != null)
            throw lastException;
        return false;
    }

    /// <summary>按显卡/驱动返回 ffmpeg 下载候选 URL（依次尝试，前面的失败则换下一个）。</summary>
    private List<string> GetFfmpegUrls()
    {
        var gpu = _gpuInfoService.Detect();
        var urls = new List<string>();

        if (gpu.Vendor == GpuVendor.Nvidia && gpu.DriverVersion > 0 && gpu.DriverVersion < NvencMinDriverVersion)
        {
            _logger.LogInfo("Dependency", $"检测到 NVIDIA 驱动 {gpu.DriverVersionText}（低于 ffmpeg 最新版所需的 {NvencMinDriverVersion:F2}），选择兼容的 ffmpeg 7.1.1");
            urls.Add(FfmpegCompatUrl);
            urls.Add(FfmpegCompatMirrorUrl);
        }
        else
        {
            urls.Add(FfmpegLatestUrl);
        }

        return urls;
    }

    /// <summary>从 ffmpeg zip 中解压出 bin/ffmpeg.exe 与 bin/ffprobe.exe 到目标目录。</summary>
    private static async Task ExtractBinariesAsync(ZipArchive zip, string targetDir)
    {
        var copied = false;
        foreach (var entry in zip.Entries)
        {
            var fileName = Path.GetFileName(entry.FullName);
            if (fileName != "ffmpeg.exe" && fileName != "ffprobe.exe")
                continue;

            var destPath = Path.Combine(targetDir, fileName);
            await using var source = entry.Open();
            await using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(dest);
            copied = true;
        }

        if (!copied)
            throw new InvalidDataException("压缩包中未找到 ffmpeg.exe/ffprobe.exe");
    }

    /// <summary>下载后自检 NVENC 是否可用（仅 NVIDIA 显卡），不可用时给出可读提示。</summary>
    private async Task VerifyNvencAsync(string ffmpegPath, bool notifyUser)
    {
        var gpu = _gpuInfoService.Detect();
        if (gpu.Vendor != GpuVendor.Nvidia)
            return;

        try
        {
            var output = await _processRunner.RunProcessAndReadOutputAsync(ffmpegPath,
                "-hide_banner -f lavfi -i testsrc=duration=1:size=320x240:rate=10 -pix_fmt yuv420p -c:v av1_nvenc -f null -",
                CancellationToken.None);

            if (output != null &&
                (output.Contains("minimum required Nvidia driver", StringComparison.OrdinalIgnoreCase) ||
                 output.Contains("nvenc API version", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogError("Dependency", $"NVENC 自检失败：驱动 {gpu.DriverVersionText} 低于 ffmpeg 要求（≥ {NvencMinDriverVersion:F2}）");
                if (notifyUser)
                {
                    _messenger.Send(new UiPromptMessage("NVENC 兼容性提示",
                        $"检测到 NVIDIA 驱动 {gpu.DriverVersionText}，当前 ffmpeg 的 NVENC 编码需要驱动 ≥ {NvencMinDriverVersion:F2}。\n" +
                        "建议更新显卡驱动，或改用软件编码器（libx264 / libsvtav1）。"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogException("Dependency", "NVENC 自检失败", ex);
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var handler = new HttpClientHandler();
        var proxyArg = _config.GetProxyArgument();
        if (!string.IsNullOrEmpty(proxyArg))
        {
            var proxyUrl = proxyArg.Replace("--proxy ", "");
            if (Uri.TryCreate(proxyUrl, UriKind.Absolute, out var uri))
            {
                handler.Proxy = new WebProxy(uri);
                handler.UseProxy = true;
            }
        }

        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalRead += bytesRead;
            if (totalBytes > 0)
                progress?.Report((double)totalRead / totalBytes * 100);
        }
    }

    private async Task<ToolStatus> CheckToolAsync(string toolName, string executableName, CancellationToken cancellationToken)
    {
        var status = new ToolStatus();

        var exePath = _toolPathResolver.ResolveToolPath(executableName);
        if (exePath != null)
        {
            status.IsAvailable = true;
            status.ExecutablePath = exePath;
            status.Version = await GetToolVersionAsync(exePath) ?? "未知";
        }
        else
        {
            status.IsAvailable = false;
            status.ExecutablePath = _toolPathResolver.GetDefaultToolPath(executableName);
        }

        return status;
    }
}