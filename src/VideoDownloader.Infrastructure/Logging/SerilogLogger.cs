using VideoDownloader.Domain.Logging;

namespace VideoDownloader.Infrastructure.Logging;

public class SerilogLogger : ILogger
{
    public void LogInfo(string module, string message) =>
        Serilog.Log.ForContext("Module", module).Information("{Message}", message);

    public void LogWarning(string module, string message) =>
        Serilog.Log.ForContext("Module", module).Warning("{Message}", message);

    public void LogError(string module, string message) =>
        Serilog.Log.ForContext("Module", module).Error("{Message}", message);

    public void LogException(string module, string context, Exception ex) =>
        Serilog.Log.ForContext("Module", module).Error(ex, "{Context}", context);
}