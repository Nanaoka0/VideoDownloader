namespace VideoDownloader.Domain.Services;

public interface IToolPathResolver
{
    string ToolsDirectory { get; }
    string? ResolveToolPath(string executableName);
    string GetDefaultToolPath(string executableName);
    bool EnsureToolsDirectoryExists();
}