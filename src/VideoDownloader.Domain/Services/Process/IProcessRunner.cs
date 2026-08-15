namespace VideoDownloader.Domain.Services.Process;

public interface IProcessRunner
{
    Task<int> RunProcessAsync(string fileName, string arguments, string workingDirectory,
        IProgress<string>? outputProgress, CancellationToken cancellationToken, Action<int>? onProcessStarted = null);
    void KillProcess(int processId);
    bool IsRunning(int processId);
    int StartProcess(string fileName, string arguments, string workingDirectory);
    Task<string?> RunProcessAndReadOutputAsync(string fileName, string arguments, CancellationToken cancellationToken);
}