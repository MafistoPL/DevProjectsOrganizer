namespace Engine.Models;

public sealed record Root(
    Guid Id,
    string Path,
    string Status,
    DateTime CreatedAt
);
