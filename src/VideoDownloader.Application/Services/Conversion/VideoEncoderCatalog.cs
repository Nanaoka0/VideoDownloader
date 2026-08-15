using VideoDownloader.Domain.Models;
using VideoDownloader.Domain.Services;
using VideoDownloader.Domain.Services.Conversion;
using VideoDownloader.Domain.Services.Process;

namespace VideoDownloader.Application.Services.Conversion;

public class VideoEncoderCatalog : IVideoEncoderCatalog
{
    private readonly IProcessRunner _processRunner;
    private readonly IToolPathResolver _toolPathResolver;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HashSet<string>? _availableEncoders;

    public bool IsReady { get; private set; }

    public VideoEncoderCatalog(IProcessRunner processRunner, IToolPathResolver toolPathResolver)
    {
        _processRunner = processRunner;
        _toolPathResolver = toolPathResolver;
    }

    public IReadOnlyList<VideoEncoderInfo> GetAvailableEncoders(VideoCodec codec)
    {
        var known = VideoEncoderCatalogEntries.All
            .Where(e => e.Codec == codec)
            .OrderBy(e => e.Priority)
            .ToList();

        if (!IsReady || _availableEncoders == null)
            return known;

        return known.Where(e => _availableEncoders.Contains(e.Name)).ToList();
    }

    public VideoEncoderInfo? GetDefaultEncoder(VideoCodec codec) =>
        GetAvailableEncoders(codec).FirstOrDefault();

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var ffmpegPath = _toolPathResolver.ResolveToolPath("ffmpeg.exe");
            if (ffmpegPath == null) return;

            var output = await _processRunner.RunProcessAndReadOutputAsync(ffmpegPath, "-encoders", cancellationToken);
            if (string.IsNullOrWhiteSpace(output)) return;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("Encoders:", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                set.Add(parts[1]);
            }

            _availableEncoders = set;
            IsReady = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}