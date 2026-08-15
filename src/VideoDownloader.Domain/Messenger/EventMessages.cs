using VideoDownloader.Domain.Models;

namespace VideoDownloader.Domain.Messenger;

public record DownloadTaskCompletedMessage(Guid TaskId, bool Success, string? ErrorMessage);
public record DownloadTaskProgressMessage(Guid TaskId, double Progress, long DownloadedBytes, long TotalBytes, double Speed);
public record DownloadTaskStatusChangedMessage(Guid TaskId, DownloadTaskStatus Status);

public record ConversionTaskCompletedMessage(Guid TaskId, bool Success, string? ErrorMessage);
public record ConversionTaskProgressMessage(Guid TaskId, double Progress);
public record ConversionTaskStatusChangedMessage(Guid TaskId, ConversionTaskStatus Status);

public record DependencyStatusChangedMessage(DependencyStatusModel Status);

public record UiPromptMessage(string Title, string Message);

public record StatusUpdateMessage(string Text);