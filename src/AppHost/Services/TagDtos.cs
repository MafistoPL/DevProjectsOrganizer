namespace AppHost.Services;

public sealed record TagDto(
    Guid Id,
    string Name,
    bool IsSystem,
    int ProjectCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record TagLinkedProjectDto(
    Guid Id,
    string Name,
    string Path,
    string Kind,
    DateTimeOffset UpdatedAt
);
