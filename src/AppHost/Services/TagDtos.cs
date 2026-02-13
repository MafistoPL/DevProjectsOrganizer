namespace AppHost.Services;

public sealed record TagDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
