namespace Engine.Scanning;

public abstract record ScanEvent(Guid ScanId, DateTimeOffset Timestamp);

public sealed record ScanProgressEvent(ScanProgress Progress)
    : ScanEvent(Progress.ScanId, Progress.Timestamp);

public sealed record ScanSuggestionEvent(Guid ScanId, DateTimeOffset Timestamp, ProjectSuggestion Suggestion)
    : ScanEvent(ScanId, Timestamp);

public sealed record ScanCompletedEvent(Guid ScanId, DateTimeOffset Timestamp)
    : ScanEvent(ScanId, Timestamp);

public sealed record ScanFailedEvent(Guid ScanId, DateTimeOffset Timestamp, string Error)
    : ScanEvent(ScanId, Timestamp);
