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
            if (IsAudioOnly)
                return string.IsNullOrWhiteSpace(AudioCodec)
                    ? $"仅音频 ({FormatId})"
                    : Bitrate > 0
                        ? $"仅音频 ({AudioCodec} {Bitrate:F0}kbps)"
                        : $"仅音频 ({AudioCodec})";

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

            return $"{resolution}{fps}{hdr} [{codec}]{bitrate}{hls}{audio}";
        }
    }
}