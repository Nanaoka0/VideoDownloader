namespace VideoDownloader.Domain.Models;

public enum EncoderAcceleration
{
    Cpu,
    Gpu
}

public class VideoEncoderInfo
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required VideoCodec Codec { get; init; }
    public required EncoderAcceleration Acceleration { get; init; }
    public required string Hint { get; init; }
    public int Priority { get; init; }

    public override string ToString() => DisplayName;
}

public static class VideoEncoderCatalogEntries
{
    public static readonly IReadOnlyList<VideoEncoderInfo> All = Build();

    private static List<VideoEncoderInfo> Build()
    {
        var list = new List<VideoEncoderInfo>();
        int priority = 0;

        Add("libx264", "libx264 CPU（最好质量）", VideoCodec.H264, EncoderAcceleration.Cpu, "最好质量");
        Add("libopenh264", "libopenh264 CPU（兼容）", VideoCodec.H264, EncoderAcceleration.Cpu, "兼容性好");
        Add("h264_nvenc", "h264_nvenc GPU（NVIDIA）", VideoCodec.H264, EncoderAcceleration.Gpu, "NVIDIA 硬件加速");
        Add("h264_qsv", "h264_qsv GPU（Intel）", VideoCodec.H264, EncoderAcceleration.Gpu, "Intel 硬件加速");
        Add("h264_amf", "h264_amf GPU（AMD）", VideoCodec.H264, EncoderAcceleration.Gpu, "AMD 硬件加速");
        Add("h264_mf", "h264_mf GPU（MediaFoundation）", VideoCodec.H264, EncoderAcceleration.Gpu, "MediaFoundation 硬件加速");

        Add("libx265", "libx265 CPU（最好质量）", VideoCodec.H265, EncoderAcceleration.Cpu, "最好质量");
        Add("libkvazaar", "libkvazaar CPU（兼容）", VideoCodec.H265, EncoderAcceleration.Cpu, "兼容性好");
        Add("hevc_nvenc", "hevc_nvenc GPU（NVIDIA）", VideoCodec.H265, EncoderAcceleration.Gpu, "NVIDIA 硬件加速");
        Add("hevc_qsv", "hevc_qsv GPU（Intel）", VideoCodec.H265, EncoderAcceleration.Gpu, "Intel 硬件加速");
        Add("hevc_amf", "hevc_amf GPU（AMD）", VideoCodec.H265, EncoderAcceleration.Gpu, "AMD 硬件加速");
        Add("hevc_mf", "hevc_mf GPU（MediaFoundation）", VideoCodec.H265, EncoderAcceleration.Gpu, "MediaFoundation 硬件加速");

        Add("libvpx-vp9", "libvpx-vp9 CPU（网页兼容）", VideoCodec.VP9, EncoderAcceleration.Cpu, "网页兼容性好");
        Add("vp9_qsv", "vp9_qsv GPU（Intel）", VideoCodec.VP9, EncoderAcceleration.Gpu, "Intel 硬件加速");

        Add("libaom-av1", "libaom-av1 CPU（体积小较慢）", VideoCodec.AV1, EncoderAcceleration.Cpu, "体积小");
        Add("libsvtav1", "libsvtav1 CPU（快速）", VideoCodec.AV1, EncoderAcceleration.Cpu, "速度较快");
        Add("librav1e", "librav1e CPU（快速）", VideoCodec.AV1, EncoderAcceleration.Cpu, "速度较快");
        Add("av1_nvenc", "av1_nvenc GPU（NVIDIA）", VideoCodec.AV1, EncoderAcceleration.Gpu, "NVIDIA 硬件加速");
        Add("av1_qsv", "av1_qsv GPU（Intel）", VideoCodec.AV1, EncoderAcceleration.Gpu, "Intel 硬件加速");
        Add("av1_amf", "av1_amf GPU（AMD）", VideoCodec.AV1, EncoderAcceleration.Gpu, "AMD 硬件加速");

        return list;

        void Add(string name, string displayName, VideoCodec codec, EncoderAcceleration accel, string hint)
        {
            list.Add(new VideoEncoderInfo
            {
                Name = name,
                DisplayName = displayName,
                Codec = codec,
                Acceleration = accel,
                Hint = hint,
                Priority = priority++
            });
        }
    }
}
