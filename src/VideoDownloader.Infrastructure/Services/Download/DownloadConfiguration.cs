using System.Text.Json;
using System.Text.Json.Serialization;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Services;

namespace VideoDownloader.Infrastructure.Services.Download;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DownloadConfiguration))]
public partial class DownloadConfigurationJsonContext : JsonSerializerContext
{
}

public class DownloadConfiguration : IDownloadConfiguration
{
    private ILogger _logger;

    public DownloadConfiguration(ILogger logger)
    {
        _logger = logger;
    }

    [JsonConstructor]
    internal DownloadConfiguration() { }

	public string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
	public int MaxConcurrentDownloads { get; set; } = 1;
    public bool UseProxy { get; set; }
    public string ProxyType { get; set; } = "HTTP";
    public string ProxyHttpHost { get; set; } = "127.0.0.1";
    public int ProxyHttpPort { get; set; } = 7890;
    public string ProxySocks5Host { get; set; } = "127.0.0.1";
    public int ProxySocks5Port { get; set; } = 7891;
    public bool PreferHdr { get; set; }
    public bool SuppressFirstRunTips { get; set; }
    public string Theme { get; set; } = "Default";
    public string CookieBrowser { get; set; } = "None";
    public bool BypassProxyForBilibili { get; set; } = true;
    public bool BypassProxyForYoutube { get; set; }

    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public static DownloadConfiguration Load(ILogger logger)
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                logger.LogWarning("Config", $"读取配置文件: {ConfigPath} ({json.Length} 字节)");
                var config = JsonSerializer.Deserialize(json, DownloadConfigurationJsonContext.Default.DownloadConfiguration);
                if (config != null)
                {
                    config._logger = logger;
                    logger.LogWarning("Config", $"配置加载成功: Proxy={config.UseProxy}, SuppressTips={config.SuppressFirstRunTips}");
                    return config;
                }
                logger.LogWarning("Config", "反序列化返回 null，使用默认配置");
            }
            else
            {
                logger.LogWarning("Config", $"配置文件不存在: {ConfigPath}，使用默认配置");
            }
        }
        catch (Exception ex) { logger.LogException("Config", $"读取配置文件失败: {ConfigPath}", ex); }
        return new DownloadConfiguration(logger);
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, DownloadConfigurationJsonContext.Default.DownloadConfiguration);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex) { _logger.LogException("Config", $"保存配置文件失败: {ConfigPath}", ex); }
    }

    public string GetProxyArgument()
    {
        if (!UseProxy) return string.Empty;
        return ProxyType.ToUpperInvariant() switch
        {
            "SOCKS5" => $"--proxy socks5://{ProxySocks5Host}:{ProxySocks5Port}",
            _ => $"--proxy http://{ProxyHttpHost}:{ProxyHttpPort}"
        };
    }

    public bool ShouldBypassProxy(string url)
    {
        if (!UseProxy) return false;
        if (BypassProxyForBilibili && (url.Contains("bilibili.com", StringComparison.OrdinalIgnoreCase) || url.Contains("b23.tv", StringComparison.OrdinalIgnoreCase)))
            return true;
        if (BypassProxyForYoutube && (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }
}