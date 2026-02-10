namespace AppHost.Persistence;

public sealed class ScanSessionEntity
{
    public Guid Id { get; set; }
    public Guid? RootId { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public string Mode { get; set; } = "roots";
    public string State { get; set; } = "queued";
    public string DiskKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long FilesScanned { get; set; }
    public long? TotalFiles { get; set; }
    public string? CurrentPath { get; set; }
    public string? OutputPath { get; set; }
    public int? DepthLimit { get; set; }
}
