namespace Engine.Scanning;

public sealed record ScanOptions(
    IReadOnlyList<string> Exclusions,
    bool CountFilesBeforeScan
);
