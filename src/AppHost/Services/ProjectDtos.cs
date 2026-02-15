namespace AppHost.Services;

public sealed record ProjectTagDto(
    Guid Id,
    string Name
);

public sealed record ProjectDto(
    Guid Id,
    Guid SourceSuggestionId,
    Guid LastScanSessionId,
    string RootPath,
    string Name,
    string Description,
    string Path,
    string Kind,
    double Score,
    string Reason,
    string ExtensionsSummary,
    IReadOnlyList<string> Markers,
    IReadOnlyList<string> TechHints,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ProjectTagDto> Tags
);
