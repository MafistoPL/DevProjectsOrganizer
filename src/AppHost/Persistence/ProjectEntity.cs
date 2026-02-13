namespace AppHost.Persistence;

public sealed class ProjectEntity
{
    public Guid Id { get; set; }
    public Guid SourceSuggestionId { get; set; }
    public Guid LastScanSessionId { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string ProjectKey { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ExtensionsSummary { get; set; } = string.Empty;
    public string MarkersJson { get; set; } = "[]";
    public string TechHintsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
