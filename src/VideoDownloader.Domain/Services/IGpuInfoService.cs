namespace VideoDownloader.Domain.Services;

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}

public sealed class GpuInfo
{
    public GpuVendor Vendor { get; init; }

    /// <summary>NVIDIA 驱动版本，如 591.86；未知为 0。</summary>
    public double DriverVersion { get; init; }

    public string DriverVersionText { get; init; } = string.Empty;
}

/// <summary>检测当前机器的显卡厂商与 NVIDIA 驱动版本（用于选择兼容的 ffmpeg 构建）。</summary>
public interface IGpuInfoService
{
    GpuInfo Detect();
}
