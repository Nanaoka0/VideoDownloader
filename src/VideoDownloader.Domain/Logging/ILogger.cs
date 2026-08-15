namespace VideoDownloader.Domain.Logging;

public interface ILogger
{
    void LogInfo(string module, string message);
    void LogWarning(string module, string message);
    void LogError(string module, string message);
    void LogException(string module, string context, Exception ex);
}