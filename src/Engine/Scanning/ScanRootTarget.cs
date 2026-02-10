namespace Engine.Scanning;

public sealed record ScanRootTarget(
    Guid RootId,
    string Path,
    string DiskKey,
    bool ChangedSinceLastScan
);
