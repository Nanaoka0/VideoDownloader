namespace VideoDownloader.Domain.Models;

public class ToolStatus
{
    public bool IsAvailable { get; set; }
    public string Version { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public bool NeedsUpdate { get; set; }
}

public class DependencyStatusModel
{
    public ToolStatus Ffmpeg { get; set; } = new();
    public ToolStatus Ffprobe { get; set; } = new();
    public ToolStatus YtDlp { get; set; } = new();
    public bool AllAvailable => Ffmpeg.IsAvailable && Ffprobe.IsAvailable && YtDlp.IsAvailable;
}