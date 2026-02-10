namespace Engine.Scanning;

public sealed record ScanRequest(
    Guid ScanId,
    ScanMode Mode,
    IReadOnlyList<ScanRootTarget> Roots,
    ScanOptions Options
);
