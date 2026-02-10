namespace Engine.Scanning;

public sealed record ProjectSuggestion(
    Guid Id,
    string Path,
    string Kind,
    double Score,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> Markers,
    DateTimeOffset CreatedAt
);
