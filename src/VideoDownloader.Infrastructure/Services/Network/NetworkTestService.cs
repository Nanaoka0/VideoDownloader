using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using VideoDownloader.Domain.Services.Network;

namespace VideoDownloader.Infrastructure.Services.Network;

public class NetworkTestService : INetworkTestService
{
    public async Task<string> TestNetworkAsync(bool useProxy, string proxyMode, string proxyHost, int proxyPort)
    {
        try
        {
            var handler = new HttpClientHandler();
            if (useProxy)
            {
                var proxyUrl = proxyMode == "SOCKS5"
                    ? $"socks5://{proxyHost}:{proxyPort}"
                    : $"http://{proxyHost}:{proxyPort}";

                if (!Uri.TryCreate(proxyUrl, UriKind.Absolute, out var uri))
                    return "代理地址格式无效，请检查主机名和端口";

                handler.Proxy = new WebProxy(uri);
                handler.UseProxy = true;
            }
            using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

            var response = await httpClient.GetAsync("https://www.youtube.com");
            return response.IsSuccessStatusCode ? "网络连通正常" : "网络异常";
        }
        catch (TaskCanceledException)
        {
            return "连接超时：服务器响应超过 10 秒，请检查网络或代理配置";
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException se)
        {
            var msg = se.SocketErrorCode switch
            {
                SocketError.HostNotFound => "DNS 解析失败：无法解析域名，请检查 DNS 或代理设置",
                SocketError.ConnectionRefused => "连接被拒绝：代理服务器未运行或端口错误",
                SocketError.NetworkUnreachable => "网络不可达：请检查网络连接",
                SocketError.TimedOut => "连接超时：代理服务器无响应",
                _ => $"网络错误 ({se.SocketErrorCode}): {se.Message}"
            };
            return $"{msg}";
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            var code = (int)ex.StatusCode.Value;
            var msg = code switch
            {
                407 => "代理需要身份验证 (HTTP 407)",
                502 => "代理服务器错误 (HTTP 502)",
                503 => "代理服务不可用 (HTTP 503)",
                _ => $"HTTP 错误: {code} {ex.StatusCode}"
            };
            return msg;
        }
        catch (HttpRequestException ex)
        {
            var msg = ex.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase)
                ? "SSL/TLS 证书验证失败，请检查代理是否拦截了 HTTPS 连接"
                : ex.Message;
            return $"请求失败: {msg}";
        }
        catch (Exception ex)
        {
            return $"未知错误: {ex.GetType().Name} - {ex.Message}";
        }
    }
}