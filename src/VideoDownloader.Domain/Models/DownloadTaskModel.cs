namespace VideoDownloader.Domain.Models;

public enum DownloadTaskStatus
{
    Pending,
    Parsing,
    Waiting,
    Downloading,
    Paused,
    Completed,
    Cancelled,
    Failed
}

public class DownloadTaskModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public DownloadTaskStatus Status { get; set; } = DownloadTaskStatus.Pending;
    public double Progress { get; set; }
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public double Speed { get; set; }
    public string? ErrorMessage { get; set; }
    public VideoFormatModel? SelectedFormat { get; set; }
    public List<VideoFormatModel> AvailableFormats { get; set; } = new();
    public string SiteName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int ProcessId { get; set; }
}