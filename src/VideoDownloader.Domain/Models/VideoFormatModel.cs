namespace VideoDownloader.Domain.Models;

public class VideoFormatModel
{
    public string FormatId { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public long FileSize { get; set; }

    /// <summary>FileSize 是否为 yt-dlp 给出的估算值（-F 输出中的 "~" 前缀）。</summary>
    public bool FileSizeEstimated { get; set; }

    /// <summary>下载该视频流时会自动搭配的音频流大小（仅视频流格式使用，用于显示视频+音频合计大小）。</summary>
    public long CompanionAudioSize { get; set; }

    /// <summary>CompanionAudioSize 是否为估算值。</summary>
    public bool CompanionAudioEstimated { get; set; }

    public double Bitrate { get; set; }
    public bool HasVideo { get; set; }
    public bool HasAudio { get; set; }
    public bool IsHdr { get; set; }
    public bool IsHls { get; set; }
    public int Fps { get; set; }
    public bool IsAudioOnly => !HasVideo && HasAudio;
    public string DisplayName
    {
        get
        {
            var displaySize = FileSize;
            var displaySizeEstimated = FileSizeEstimated;
            if (HasVideo && !HasAudio && CompanionAudioSize > 0)
            {
                displaySize += CompanionAudioSize;
                displaySizeEstimated |= CompanionAudioEstimated;
            }

            var sizeText = displaySize > 0
                ? $" · {(displaySizeEstimated ? "≈" : "")}{FormatSize(displaySize)}"
                : "";

            if (IsAudioOnly)
                return (string.IsNullOrWhiteSpace(AudioCodec)
                    ? $"仅音频 ({FormatId})"
                    : Bitrate > 0
                        ? $"仅音频 ({AudioCodec} {Bitrate:F0}kbps)"
                        : $"仅音频 ({AudioCodec})") + sizeText;

            var codec = string.IsNullOrWhiteSpace(VideoCodec) ? "未知编码" : VideoCodec;
            var hdr = IsHdr ? " HDR" : "";
            var fps = Fps > 0 ? $" {Fps}fps" : "";
            var bitrate = Bitrate > 0 ? $" {Bitrate:F0}kbps" : "";
            var hls = IsHls ? " HLS" : "";
            var resolution = Width > 0 && Height > 0
                ? $"{Resolution} ({Width}x{Height})"
                : (string.IsNullOrWhiteSpace(Resolution) ? FormatId : Resolution);

            var audio = HasAudio && !string.IsNullOrWhiteSpace(AudioCodec)
                ? $" + {AudioCodec}"
                : "";

            return $"{resolution}{fps}{hdr} [{codec}]{bitrate}{hls}{audio}{sizeText}";
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}