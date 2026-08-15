namespace VideoDownloader.Domain.Models;

public enum ConversionTaskStatus
{
    Pending,
    Converting,
    Stopped,
    Completed,
    Cancelled,
    Failed
}

public enum VideoContainer
{
    Mp4,
    Mkv,
    WebM,
    Mov
}

public enum VideoCodec
{
    H264,
    H265,
    VP9,
    AV1
}

public enum AudioCodec
{
    AAC,
    MP3,
    Opus,
    FLAC,
    Vorbis,
    PCM
}

public class VideoConversionTaskModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InputFilePath { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public ConversionTaskStatus Status { get; set; } = ConversionTaskStatus.Pending;
    public double Progress { get; set; }
    public VideoContainer Container { get; set; } = VideoContainer.Mp4;
    public VideoCodec VideoCodec { get; set; } = VideoCodec.H264;
    public string VideoEncoderName { get; set; } = string.Empty;
    public AudioCodec AudioCodec { get; set; } = AudioCodec.AAC;
    public bool IsSelected { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public double DurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int ProcessId { get; set; }
}