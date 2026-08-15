using System.Collections.Concurrent;
using System.Diagnostics;
using VideoDownloader.Domain.Logging;
using VideoDownloader.Domain.Services.Process;
using WinProcess = System.Diagnostics.Process;

namespace VideoDownloader.Infrastructure.Services.Process;

public class ProcessRunner : IProcessRunner, IDisposable
{
    private readonly ConcurrentDictionary<int, WinProcess> _processes = new();
    private readonly ILogger _logger;

    public ProcessRunner(ILogger logger)
    {
        _logger = logger;
    }

    public int StartProcess(string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.GetEncoding(936),
            StandardErrorEncoding = System.Text.Encoding.GetEncoding(936)
        };

        var winProcess = new WinProcess { StartInfo = startInfo };
        winProcess.Start();

        _processes.TryAdd(winProcess.Id, winProcess);
        return winProcess.Id;
    }

    public async Task<int> RunProcessAsync(string fileName, string arguments, string workingDirectory,
        IProgress<string>? outputProgress, CancellationToken cancellationToken, Action<int>? onProcessStarted = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.GetEncoding(936),
            StandardErrorEncoding = System.Text.Encoding.GetEncoding(936)
        };

        using var winProcess = new WinProcess { StartInfo = startInfo };
        winProcess.Start();

        _processes.TryAdd(winProcess.Id, winProcess);
        onProcessStarted?.Invoke(winProcess.Id);

        cancellationToken.Register(() =>
        {
            try { KillProcess(winProcess.Id); }
            catch (Exception ex) { _logger.LogException("Process", $"取消时终止进程失败 (pid={winProcess.Id})", ex); }
        });

        var outputTask = Task.Run(async () =>
        {
            try
            {
                while (!winProcess.StandardOutput.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await winProcess.StandardOutput.ReadLineAsync(cancellationToken);
                    if (line != null)
                        outputProgress?.Report(line);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.LogException("Process", "读取进程标准输出流失败", ex); }
        }, cancellationToken);

        var errorTask = Task.Run(async () =>
        {
            try
            {
                while (!winProcess.StandardError.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await winProcess.StandardError.ReadLineAsync(cancellationToken);
                    if (line != null)
                        outputProgress?.Report(line);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.LogException("Process", "读取进程错误输出流失败", ex); }
        }, cancellationToken);

        await Task.WhenAll(outputTask, errorTask);
        try
        {
            await winProcess.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 进程可能已被 KillProcess（停止/取消/暂停）终止并释放，等待时不再有可关联的进程对象
            _logger.LogException("Process", $"等待进程退出失败 (pid={winProcess.Id})", ex);
        }

        _processes.TryRemove(winProcess.Id, out _);
        int exitCode;
        try
        {
            exitCode = winProcess.ExitCode;
        }
        catch (Exception)
        {
            exitCode = -1;
        }
        return exitCode;
    }

    public void KillProcess(int processId)
    {
        if (_processes.TryRemove(processId, out var winProcess))
        {
            try
            {
                if (!winProcess.HasExited)
                {
                    winProcess.Kill(entireProcessTree: true);
                    winProcess.WaitForExit(5000);
                }
            }
            catch (Exception ex) { _logger.LogException("Process", $"终止进程失败 (pid={processId})", ex); }
            finally
            {
                winProcess.Dispose();
            }
        }
        else
        {
            if (processId <= 0) return;
            try
            {
                var proc = WinProcess.GetProcessById(processId);
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                }
                proc.Dispose();
            }
            catch (Exception ex) { _logger.LogException("Process", $"按 PID 终止进程失败 (pid={processId})", ex); }
        }
    }

    public bool IsRunning(int processId)
    {
        if (_processes.TryGetValue(processId, out var winProcess))
            return !winProcess.HasExited;

        try
        {
            using var proc = WinProcess.GetProcessById(processId);
            return !proc.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> RunProcessAndReadOutputAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.GetEncoding(936),
            StandardErrorEncoding = System.Text.Encoding.GetEncoding(936)
        };

        using var winProcess = new WinProcess { StartInfo = startInfo };
        winProcess.Start();

        var output = await winProcess.StandardOutput.ReadToEndAsync();
        await winProcess.WaitForExitAsync(cancellationToken);
        return output;
    }

    public void Dispose()
    {
        foreach (var kvp in _processes)
        {
            try
            {
                if (!kvp.Value.HasExited)
                    kvp.Value.Kill(entireProcessTree: true);
                kvp.Value.Dispose();
            }
            catch (Exception ex) { _logger.LogException("Process", "释放进程资源时终止失败", ex); }
        }
        _processes.Clear();
    }
}