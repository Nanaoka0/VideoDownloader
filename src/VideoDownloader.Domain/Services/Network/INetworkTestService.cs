namespace VideoDownloader.Domain.Services.Network;

public interface INetworkTestService
{
    Task<string> TestNetworkAsync(bool useProxy, string proxyMode, string proxyHost, int proxyPort);
}