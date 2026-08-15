using VideoDownloader.Domain.Services;

namespace VideoDownloader.Infrastructure.Services.Dependency;

public class ToolLocationProvider : IToolPathResolver
{
    private readonly string _toolsDirectory;

    public ToolLocationProvider()
    {
        _toolsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".tools");
    }

    public string ToolsDirectory => _toolsDirectory;

    public string? ResolveToolPath(string executableName)
    {
        var localPath = Path.Combine(_toolsDirectory, executableName);
        if (File.Exists(localPath))
            return localPath;

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(dir.Trim(), executableName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    public string GetDefaultToolPath(string executableName) =>
        Path.Combine(_toolsDirectory, executableName);

    public bool EnsureToolsDirectoryExists()
    {
        try
        {
            Directory.CreateDirectory(_toolsDirectory);
            return true;
        }
        catch
        {
            return false;
        }
    }
}