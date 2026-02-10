namespace Engine.Scanning;

public sealed record ScanProgress(
    Guid ScanId,
    ScanState State,
    long FilesScanned,
    long? TotalFiles,
    string? CurrentPath,
    DateTimeOffset Timestamp
);
