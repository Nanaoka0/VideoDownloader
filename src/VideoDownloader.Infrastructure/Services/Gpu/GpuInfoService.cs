using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using VideoDownloader.Domain.Services;

namespace VideoDownloader.Infrastructure.Services.Gpu;

/// <summary>
/// 通过 nvidia-smi（NVIDIA 驱动自带）检测 NVIDIA 驱动版本，
/// 找不到时回退到注册表显示控制器信息判断显卡厂商。
/// </summary>
public class GpuInfoService : IGpuInfoService
{
    private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private readonly object _gate = new();
    private GpuInfo? _cached;

    public GpuInfo Detect()
    {
        if (_cached != null)
            return _cached;

        lock (_gate)
        {
            if (_cached != null)
                return _cached;

            var gpu = DetectFromNvidiaSmi() ?? DetectVendorFromRegistry();
            _cached = gpu;
            return gpu;
        }
    }

    private static GpuInfo? DetectFromNvidiaSmi()
    {
        var nvidiaSmi = FindExecutable("nvidia-smi.exe");
        if (nvidiaSmi == null)
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = nvidiaSmi,
                Arguments = "--query-gpu=driver_version --format=csv,noheader",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill();
                return null;
            }

            var line = output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            if (line == null)
                return null;

            var versionText = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!double.TryParse(versionText, NumberStyles.Float, CultureInfo.InvariantCulture, out var version))
                return null;

            return new GpuInfo
            {
                Vendor = GpuVendor.Nvidia,
                DriverVersion = version,
                DriverVersionText = versionText
            };
        }
        catch
        {
            return null;
        }
    }

    private static GpuInfo DetectVendorFromRegistry()
    {
        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(DisplayClassKey);
            if (baseKey == null)
                return new GpuInfo { Vendor = GpuVendor.Unknown };

            for (var i = 0; i < 8; i++)
            {
                using var subKey = baseKey.OpenSubKey(i.ToString("D4"));
                var description = subKey?.GetValue("DriverDesc") as string;
                if (string.IsNullOrEmpty(description))
                    continue;

                if (description.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    return new GpuInfo { Vendor = GpuVendor.Nvidia };
                if (description.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                    description.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    return new GpuInfo { Vendor = GpuVendor.Amd };
                if (description.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                    return new GpuInfo { Vendor = GpuVendor.Intel };
            }

            return new GpuInfo { Vendor = GpuVendor.Unknown };
        }
        catch
        {
            return new GpuInfo { Vendor = GpuVendor.Unknown };
        }
    }

    private static string? FindExecutable(string fileName)
    {
        foreach (var dir in Environment.GetEnvironmentVariable("PATH")?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
        {
            var candidate = Path.Combine(dir.Trim('"'), fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
