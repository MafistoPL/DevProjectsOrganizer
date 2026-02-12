namespace AppHost.Services;

public sealed record ProjectSuggestionDto(
    Guid Id,
    Guid ScanSessionId,
    string RootPath,
    string Name,
    string Path,
    string Kind,
    double Score,
    string Reason,
    string ExtensionsSummary,
    IReadOnlyList<string> Markers,
    IReadOnlyList<string> TechHints,
    DateTimeOffset CreatedAt,
    string Status
);
